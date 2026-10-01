import calendar
import unittest
from datetime import date
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


SOURCE = Path(__file__).resolve().parents[1] / "planowanie_sluzb_tkinter_v450.py"


class WeekendPriorityTests(unittest.TestCase):
    def setUp(self) -> None:
        self.planner = object.__new__(DutyPlannerApp)

    def test_weekends_and_holidays_are_ordered_before_workdays(self) -> None:
        year, month = 2026, 11
        order = self.planner.weekend_first_planning_day_numbers(year, month)
        expected_days = list(range(1, calendar.monthrange(year, month)[1] + 1))
        weekend_days = [
            day_no
            for day_no in expected_days
            if self.planner.is_planning_weekend_or_holiday(date(year, month, day_no))
        ]
        workdays = [day_no for day_no in expected_days if day_no not in weekend_days]

        self.assertEqual(weekend_days + workdays, order)
        self.assertIn(11, weekend_days)  # Narodowe Święto Niepodległości, środa
        self.assertLess(order.index(11), order.index(2))

    def test_weekend_shift_reference_uses_the_correct_calendar_week(self) -> None:
        saturday = date(2026, 11, 7)
        sunday = date(2026, 11, 8)

        self.assertEqual(date(2026, 11, 6), self.planner.planning_shift_reference_date(saturday))
        self.assertEqual(date(2026, 11, 9), self.planner.planning_shift_reference_date(sunday))
        self.assertEqual(date(2026, 11, 10), self.planner.planning_shift_reference_date(date(2026, 11, 10)))

    def test_auto_planner_uses_hard_weekend_first_order(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("    def auto_plan_all_drivers(self) -> None:")
        auto_source = source[start:]

        self.assertIn("planning_day_numbers = self.weekend_first_planning_day_numbers(year, month)", auto_source)
        self.assertIn("reference_day = self.planning_shift_reference_date(day_obj)", auto_source)
        self.assertNotIn("skipped_late_saturday_duties", auto_source)
        self.assertNotIn("high_priority_required_duty_ids_v382.remove(duty_id)", auto_source)

    def test_weekend_coverage_is_not_blocked_by_old_monthly_limits(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def weekend_auto_block_reason")
        end = source.index("            def would_make_consecutive_weekend", start)
        helper_source = source[start:end]

        self.assertIn("driver_has_adjacent_calendar_weekend_day", helper_source)
        self.assertNotIn("limit 2 weekendow", helper_source)
        self.assertNotIn("dwa weekendy pracy z rzedu", helper_source)

    def test_weekend_load_is_a_strong_preference_not_a_hard_filter(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("    def auto_plan_all_drivers(self) -> None:")
        auto_source = source[start:]

        self.assertGreaterEqual(auto_source.count("len(driver_month_weekend_limit_keys(driver_id)) * 5000"), 2)
        self.assertIn("allow_monthly_overtime=True", auto_source)

    def test_weekend_pair_block_can_cross_monthly_norm_and_uses_fairness(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def choose_driver_pair_for_duty_block")
        end = source.index("            def duty_interval_for_date", start)
        pair_source = source[start:end]

        self.assertIn(
            "allow_monthly_overtime=self.is_planning_weekend_or_holiday(target_date_obj)",
            pair_source,
        )
        self.assertIn(
            "len(driver_month_weekend_limit_keys(int(driver_id))) * 5000",
            pair_source,
        )

    def test_driver_card_exposes_hard_saturday_sunday_block(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_start = source.index("    def auto_plan_all_drivers(self) -> None:")
        add_start = source.index("            def add_entry(", auto_start)
        add_end = source.index("            def buffer_weekday_duty_for_required_rest", add_start)
        add_source = source[add_start:add_end]

        self.assertIn('text="Nie planuj sob./niedz."', source)
        self.assertIn("driver_db_no_weekends_var", source)
        self.assertIn("COALESCE(x.no_weekends, 0) AS no_plan_weekends", source)
        self.assertIn("is_driver_blocked_by_day_exclusion(driver_id, target_day_obj)", add_source)
        self.assertIn("not self.is_day_off_duty(duty_for_duplicate_check)", add_source)


if __name__ == "__main__":
    unittest.main()
