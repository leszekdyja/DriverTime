import calendar
import sqlite3
import unittest
from datetime import date
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


SOURCE = Path(__file__).resolve().parents[1] / "planowanie_sluzb_tkinter_v450.py"


class RnContinuityTests(unittest.TestCase):
    def setUp(self) -> None:
        self.planner = object.__new__(DutyPlannerApp)

    def test_rn_requires_two_from_sunday_to_friday_and_one_on_saturday(self) -> None:
        year, month = 2026, 6
        month_days = calendar.monthrange(year, month)[1]

        counts = {
            date(year, month, day_no): self.planner.rn_required_count_for_date(date(year, month, day_no))
            for day_no in range(1, month_days + 1)
        }

        for day_obj, required_count in counts.items():
            expected = 1 if day_obj.weekday() == 5 else 2
            self.assertEqual(expected, required_count, day_obj.isoformat())

    def test_weekday_holiday_does_not_split_sunday_friday_rn_block(self) -> None:
        holiday = date(2026, 6, 4)  # Boże Ciało, czwartek
        self.assertTrue(self.planner.is_public_holiday(holiday))
        self.assertEqual(2, self.planner.rn_required_count_for_date(holiday))

        blocks = self.planner.rn_planning_blocks_for_month(2026, 6)
        holiday_block = next(block for block in blocks if holiday in block)

        self.assertEqual(
            [date(2026, 6, day_no) for day_no in range(1, 6)],
            holiday_block,
        )
        self.assertEqual([date(2026, 6, 6)], next(block for block in blocks if date(2026, 6, 6) in block))

    def test_month_blocks_cover_every_date_once_and_keep_sunday_friday_continuous(self) -> None:
        blocks = self.planner.rn_planning_blocks_for_month(2026, 8)
        flattened = [day_obj for block in blocks for day_obj in block]

        self.assertEqual([date(2026, 8, day_no) for day_no in range(1, 32)], flattened)
        for block in blocks:
            if len(block) == 1 and block[0].weekday() == 5:
                continue
            self.assertTrue(all(day_obj.weekday() != 5 for day_obj in block))
            self.assertTrue(all((right - left).days == 1 for left, right in zip(block, block[1:])))

    def test_full_rn_block_hour_limit_includes_friday(self) -> None:
        block = [date(2026, 9, 6 + offset) for offset in range(6)]

        self.assertFalse(
            self.planner.rn_full_block_hours_within_limits(
                136.0,
                {},
                block,
                8.0,
                176.0,
                48.0,
            )
        )
        self.assertTrue(
            self.planner.rn_full_block_hours_within_limits(
                128.0,
                {},
                block,
                8.0,
                176.0,
                48.0,
            )
        )

    def test_full_rn_block_projects_weekly_hours_for_all_six_days(self) -> None:
        block = [date(2026, 9, 6 + offset) for offset in range(6)]
        monday_iso = date(2026, 9, 7).isocalendar()

        self.assertFalse(
            self.planner.rn_full_block_hours_within_limits(
                0.0,
                {(monday_iso.year, monday_iso.week): 16.0},
                block,
                8.0,
                176.0,
                48.0,
            )
        )

    def test_auto_planner_uses_rn_calendar_blocks_not_holiday_saturday_rules(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("    def auto_plan_all_drivers(self) -> None:")
        auto_source = source[start:]

        self.assertIn("self.rn_planning_blocks_for_month(year, month)", auto_source)
        self.assertNotIn("self.is_planning_saturday(", auto_source)

    def test_auto_planner_orders_weekends_before_full_rn_blocks(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("    def auto_plan_all_drivers(self) -> None:")
        auto_source = source[start:]
        preplan_start = auto_source.index("            def preplan_month_rn_before_services(*, reserve_only: bool = False) -> None:")
        preplan_end = auto_source.index("            for order_index, target_day", preplan_start)
        preplan_source = auto_source[preplan_start:preplan_end]

        self.assertIn("planning_day_numbers = self.weekend_first_planning_day_numbers(year, month)", auto_source)
        self.assertIn("if not rn_preplanned and not self.is_planning_weekend_or_holiday(target_date_obj):", auto_source)
        self.assertIn("preplan_month_rn_before_services()", auto_source)
        self.assertIn("if self.is_planning_weekend_or_holiday(target_date_obj)\n                    or int(duty_id) not in shortage_skip_duty_ids", auto_source)
        self.assertNotIn("RN obowiazkowe - uzupelnienie pojedynczego dnia po przerwaniu bloku", preplan_source)

    def test_saturday_rn_is_reserved_before_adjacent_full_blocks(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_source = source[source.index("    def auto_plan_all_drivers(self) -> None:"):]
        saturday_start = auto_source.index("                    if is_saturday_block:")
        saturday_end = auto_source.index("                    block_key = rn_block_key_for_date(block[0])", saturday_start)
        saturday_source = auto_source[saturday_start:saturday_end]
        preplan_start = auto_source.index("            def preplan_month_rn_before_services(*, reserve_only: bool = False) -> None:")
        preplan_end = auto_source.index("            preplan_month_rn_before_services()", preplan_start)
        preplan_source = auto_source[preplan_start:preplan_end]

        self.assertIn("saturday_block=True", saturday_source)
        self.assertIn("if reserve_only:", saturday_source)
        self.assertIn("for driver_id in selected:", saturday_source)
        self.assertIn("remembered.append(driver_id)", saturday_source)
        self.assertIn("driver_reserved_for_rn_weekend(driver_id, block[0])", preplan_source)
        self.assertIn("allow_shift_mismatch=True", preplan_source)
        self.assertIn(
            "0 if len(item) == 1 and self.is_rn_single_coverage_day(item[0]) else 1",
            preplan_source,
        )
        self.assertIn("driver_reserved_for_adjacent_saturday_rn(driver_id, block)", preplan_source)

    def test_saturday_rn_driver_is_protected_from_next_sunday_service(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def driver_reserved_for_rn_weekend(")
        end = source.index("            def driver_reserved_for_exact_saturday_rn(", start)
        helper_source = source[start:end]

        self.assertIn("{block_start, block_start + timedelta(days=1)}", helper_source)

    def test_real_weekly_rest_replaces_calendar_date_run_filter(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def driver_has_required_rest(")
        end = source.index("            def driver_rest_block_reason(", start)
        helper_source = source[start:end]

        self.assertIn("if max_consecutive_work_days > 0 and not check_weekly_rest:", helper_source)
        self.assertIn("work_date_run_containing_candidate_without_weekly_rest", helper_source)
        self.assertIn("self.new_weekly_rest_deadline_violation(", helper_source)

    def test_previous_month_rn_blocks_automatic_rotation_but_older_month_does_not(self) -> None:
        planner = object.__new__(DutyPlannerApp)
        planner.conn = sqlite3.connect(":memory:")
        planner.conn.row_factory = sqlite3.Row
        planner.conn.executescript(
            """
            CREATE TABLE duties(id INTEGER PRIMARY KEY, code TEXT);
            CREATE TABLE planned_schedule(
                plan_date TEXT,
                driver_id INTEGER,
                duty_id INTEGER,
                entry_source TEXT
            );
            INSERT INTO duties VALUES(70, 'RN');
            INSERT INTO planned_schedule VALUES('2026-08-10', 1, 70, 'auto');
            INSERT INTO planned_schedule VALUES('2026-08-11', 3, 70, 'manual');
            INSERT INTO planned_schedule VALUES('2026-07-10', 2, 70, 'auto');
            """
        )
        try:
            blocked = planner.previous_month_rn_driver_ids(2026, 9)
            automatic_window = planner.recent_rn_driver_ids_for_automatic(2026, 9)
        finally:
            planner.conn.close()

        self.assertEqual({1, 3}, blocked)
        self.assertEqual({1, 3}, automatic_window)
        self.assertNotIn(2, blocked)

    def test_preplanning_hard_rejects_previous_month_rn_driver(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def preplan_month_rn_before_services(*, reserve_only: bool = False) -> None:")
        end = source.index("            preplan_month_rn_before_services()", start)
        preplan_source = source[start:end]

        self.assertIn("if driver_id in previous_rn_driver_ids:", preplan_source)
        self.assertIn("if int(driver_id) in previous_rn_driver_ids:", preplan_source)

    def test_no_rn_flag_is_a_hard_preplanning_and_central_write_guard(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("    def auto_plan_all_drivers(self) -> None:")
        auto_source = source[start:]
        preplan_start = auto_source.index("            def preplan_month_rn_before_services(*, reserve_only: bool = False) -> None:")
        preplan_end = auto_source.index("            preplan_month_rn_before_services()", preplan_start)
        preplan_source = auto_source[preplan_start:preplan_end]
        add_start = auto_source.index("            def add_entry(")
        add_end = auto_source.index("            def buffer_weekday_duty_for_required_rest", add_start)
        add_source = auto_source[add_start:add_end]

        self.assertIn("if driver_id in no_rn_driver_ids:\n                        return False", preplan_source)
        self.assertIn("if int(driver_id) in no_rn_driver_ids:\n                        return False", preplan_source)
        self.assertIn("if duty_id in rn_duty_ids and driver_id in no_rn_driver_ids:", add_source)
        self.assertNotIn("score += 50000", preplan_source)

    def test_full_block_shift_fallback_cannot_bypass_no_rn_flag(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("                def driver_available_for_full_rn_block(")
        end = source.index("                def block_driver_score(", start)
        helper_source = source[start:end]

        no_rn_guard = helper_source.index("if driver_id in no_rn_driver_ids:")
        shift_fallback = helper_source.index("if not allow_shift_mismatch and not rn_driver_has_second_shift_for_block")
        self.assertLess(no_rn_guard, shift_fallback)

    def test_saturday_rn_prefers_second_shift_without_blocking_legal_first_shift(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def rn_driver_has_second_shift_for_block(")
        end = source.index("            def weekly_duty_continuity_score", start)
        helper_source = source[start:end]

        self.assertIn("if len(block) == 1 and block[0].weekday() == 5:", helper_source)
        self.assertIn("return True", helper_source)
        self.assertIn("def saturday_rn_next_monday_rank", helper_source)
        self.assertIn("next_monday = block[0] + timedelta(days=2)", helper_source)
        self.assertIn("effective_preferred_shift_for_date(int(driver_id), next_monday) == \"afternoon\"", helper_source)
        self.assertIn("self.is_day_off_duty(duty)", helper_source)
        self.assertIn("return 2", helper_source)

    def test_mandatory_rn_reserves_full_block_without_daily_rescue(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def preplan_month_rn_before_services(*, reserve_only: bool = False) -> None:")
        end = source.index("            preplan_month_rn_before_services()", start)
        preplan_source = source[start:end]

        self.assertIn("allow_required_rn_weekend_overlap=True", preplan_source)
        self.assertIn("rn_full_block_reserved_driver_ids.add(int(driver_id))", preplan_source)
        self.assertIn("if reserve_only:\n                        continue", preplan_source)
        self.assertNotIn("uzupelnienie pojedynczego dnia po przerwaniu bloku", preplan_source)

    def test_atomic_rn_pair_is_not_split_when_partner_is_unavailable(self) -> None:
        selected = self.planner.select_atomic_rn_candidates(
            2,
            [(9, 31)],
            {9, 40, 41},
            {9: (0, 9), 40: (1, 40), 41: (2, 41)},
        )

        self.assertEqual([40, 41], selected)
        self.assertNotIn(9, selected)

    def test_atomic_rn_pair_is_selected_together_when_both_are_available(self) -> None:
        selected = self.planner.select_atomic_rn_candidates(
            2,
            [(9, 31)],
            {9, 31, 40, 41},
            {9: (0, 9), 31: (0, 31), 40: (1, 40), 41: (2, 41)},
        )

        self.assertEqual([9, 31], selected)

    def test_single_saturday_rn_never_uses_half_of_active_pair(self) -> None:
        selected = self.planner.select_atomic_rn_candidates(
            1,
            [(9, 31)],
            {9, 31, 40},
            {9: (0, 9), 31: (0, 31), 40: (1, 40)},
        )

        self.assertEqual([40], selected)

    def test_preplanner_uses_atomic_pair_selector(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def preplan_month_rn_before_services(*, reserve_only: bool = False) -> None:")
        end = source.index("            preplan_month_rn_before_services()", start)
        preplan_source = source[start:end]

        self.assertIn("preplan_rn_pair_groups = self.active_rn_pair_groups()", preplan_source)
        self.assertIn("def select_atomic_rn_block_drivers(", preplan_source)
        self.assertIn("self.select_atomic_rn_candidates(", preplan_source)
        self.assertIn("if any(is_driver_blocked_by_day_exclusion(driver_id, day_obj) for day_obj in block):", preplan_source)

    def test_mandatory_rn_reuses_current_month_driver_only_after_normal_rotation_fails(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("                def select_atomic_rn_block_drivers(")
        end = source.index("                def add_rn_entry_if_missing(", start)
        selector_source = source[start:end]

        self.assertIn("allow_reused_driver: bool = False", selector_source)
        self.assertIn("if len(selected) >= required_count or not excluded_driver_ids:", selector_source)
        self.assertIn("allow_reused_driver=True", selector_source)
        self.assertIn("100000 if driver_id in excluded_driver_ids", selector_source)
        self.assertIn("driver_available_for_full_rn_block", selector_source)

        preplan_start = source.index("            def preplan_month_rn_before_services(*, reserve_only: bool = False) -> None:")
        preplan_end = source.index("            preplan_month_rn_before_services()", preplan_start)
        preplan_source = source[preplan_start:preplan_end]
        self.assertIn("if driver_id in no_rn_driver_ids:", preplan_source)
        self.assertIn("if driver_id in previous_rn_driver_ids:", preplan_source)


if __name__ == "__main__":
    unittest.main()
