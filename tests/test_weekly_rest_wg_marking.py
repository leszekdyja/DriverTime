import unittest
from datetime import date, datetime
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


SOURCE = Path(__file__).resolve().parents[1] / "planowanie_sluzb_tkinter_v450.py"


class WeeklyRestWgMarkingTests(unittest.TestCase):
    def make_planner(self) -> DutyPlannerApp:
        return object.__new__(DutyPlannerApp)

    def test_listwan_has_forced_consecutive_reduced_weekly_rests(self) -> None:
        planner = self.make_planner()
        raw_intervals = [
            ("2026-09-01 14:00", "2026-09-01 23:05"),
            ("2026-09-04 14:00", "2026-09-04 23:05"),
            ("2026-09-05 16:35", "2026-09-06 01:50"),
            ("2026-09-07 05:00", "2026-09-07 15:45"),
            ("2026-09-08 05:00", "2026-09-08 15:45"),
            ("2026-09-09 05:00", "2026-09-09 15:45"),
            ("2026-09-10 05:00", "2026-09-10 15:45"),
            ("2026-09-11 05:00", "2026-09-11 15:45"),
            ("2026-09-12 04:00", "2026-09-12 15:50"),
            ("2026-09-14 05:05", "2026-09-14 16:15"),
            ("2026-09-15 05:05", "2026-09-15 16:15"),
            ("2026-09-16 05:05", "2026-09-16 16:15"),
            ("2026-09-17 05:05", "2026-09-17 16:15"),
            ("2026-09-18 04:35", "2026-09-18 16:25"),
            ("2026-09-19 04:30", "2026-09-19 15:20"),
            ("2026-09-21 04:20", "2026-09-21 15:30"),
            ("2026-09-22 04:20", "2026-09-22 15:30"),
            ("2026-09-23 04:20", "2026-09-23 15:30"),
            ("2026-09-24 04:20", "2026-09-24 15:30"),
            ("2026-09-25 04:20", "2026-09-25 15:30"),
            ("2026-09-26 16:20", "2026-09-27 03:20"),
            ("2026-09-28 17:25", "2026-09-29 03:04"),
        ]
        intervals = [
            (
                datetime.strptime(start, "%Y-%m-%d %H:%M"),
                datetime.strptime(end, "%Y-%m-%d %H:%M"),
            )
            for start, end in raw_intervals
        ]

        violations = planner.consecutive_reduced_weekly_rest_violations(
            intervals,
            reduced_weekly_rest=24.0,
            regular_weekly_rest=45.0,
        )

        # Jedną z pięciu krótkich przerw można pominąć jako tygodniową, ponieważ
        # kolejna zaczyna się jeszcze w granicy sześciu dób. Trzy układy
        # krótki->krótki pozostają rzeczywiście wymuszone.
        self.assertEqual(3, len(violations))
        first = violations[0]
        self.assertAlmostEqual(27 + 10 / 60, float(first["first"]["hours"]))
        self.assertAlmostEqual(37 + 15 / 60, float(first["second"]["hours"]))

        planner.has_vacation_rest_day_for_driver_between = lambda *_args: False
        entries = [
            {"start_dt": start_dt, "end_dt": end_dt, "plan_date": start_dt.date().isoformat()}
            for start_dt, end_dt in intervals
        ]
        report = planner.weekly_rest_violations(
            8,
            entries,
            "2026-09-01",
            "2026-09-30",
            min_weekly_rest=35.0,
            regular_weekly_rest=45.0,
            reduced_weekly_rest=24.0,
            check_regulation_561=True,
        )
        alternating_rest_errors = [
            item
            for item in report
            if item["rule_code"] == "ODPOCZYNEK_TYGODNIOWY_45_PO_SKROCONYM"
        ]
        self.assertEqual(3, len(alternating_rest_errors))

    def test_wloch_sequence_skips_two_redundant_regular_rests(self) -> None:
        planner = self.make_planner()
        raw_intervals = [
            ("2026-09-02 16:15", "2026-09-03 02:10"),
            ("2026-09-03 14:00", "2026-09-03 23:05"),
            ("2026-09-04 16:30", "2026-09-05 03:35"),
            ("2026-09-05 15:50", "2026-09-06 03:45"),
            ("2026-09-07 03:50", "2026-09-07 14:30"),
            ("2026-09-10 03:50", "2026-09-10 14:30"),
            ("2026-09-11 16:30", "2026-09-12 03:35"),
            ("2026-09-12 17:05", "2026-09-13 03:03"),
            ("2026-09-15 04:40", "2026-09-15 14:40"),
            ("2026-09-16 04:40", "2026-09-16 14:40"),
            ("2026-09-18 16:30", "2026-09-19 01:50"),
            ("2026-09-19 15:50", "2026-09-20 03:45"),
            ("2026-09-21 04:35", "2026-09-21 15:10"),
            ("2026-09-22 04:35", "2026-09-22 15:10"),
            ("2026-09-23 04:35", "2026-09-23 15:10"),
            ("2026-09-24 04:35", "2026-09-24 15:10"),
            ("2026-09-25 04:35", "2026-09-25 15:10"),
            ("2026-09-28 18:10", "2026-09-29 03:10"),
        ]
        intervals = [
            (
                datetime.strptime(start, "%Y-%m-%d %H:%M"),
                datetime.strptime(end, "%Y-%m-%d %H:%M"),
            )
            for start, end in raw_intervals
        ]

        selected = planner.selected_weekly_rest_gaps(intervals, 24.0, 45.0)
        selected_kinds = ["regular" if float(item["hours"]) >= 45.0 else "reduced" for item in selected]
        redundant = planner.redundant_regular_weekly_rest_gaps(intervals, 24.0, 45.0)

        self.assertEqual(["reduced", "regular", "reduced", "regular"], selected_kinds)
        self.assertEqual(2, len(redundant))
        self.assertEqual([], planner.consecutive_reduced_weekly_rest_violations(intervals, 24.0, 45.0))

    def test_rbh_topup_prefers_breaking_redundant_regular_rest(self) -> None:
        planner = self.make_planner()
        intervals = [
            (datetime(2026, 9, 6, 0), datetime(2026, 9, 6, 4)),
            (datetime(2026, 9, 7, 4), datetime(2026, 9, 7, 14)),   # 24 h
            (datetime(2026, 9, 10, 4), datetime(2026, 9, 10, 14)), # 62 h
            (datetime(2026, 9, 11, 16), datetime(2026, 9, 12, 3)), # 26 h
            (datetime(2026, 9, 15, 4), datetime(2026, 9, 15, 14)), # 49 h
            (datetime(2026, 9, 16, 4), datetime(2026, 9, 16, 14)),
            (datetime(2026, 9, 18, 16), datetime(2026, 9, 19, 2)), # 50 h redundant
            (datetime(2026, 9, 20, 16), datetime(2026, 9, 21, 2)),
            (datetime(2026, 9, 22, 4), datetime(2026, 9, 22, 14)),
            (datetime(2026, 9, 25, 16), datetime(2026, 9, 26, 2)), # 74 h
        ]
        candidate_in_redundant_gap = (datetime(2026, 9, 17, 4, 40), datetime(2026, 9, 17, 14, 40))
        candidate_in_daily_gap = (datetime(2026, 9, 21, 12), datetime(2026, 9, 21, 20))

        redundant_rank = planner.weekly_rest_candidate_optimization_rank(
            intervals, candidate_in_redundant_gap, 24.0, 45.0
        )
        daily_rank = planner.weekly_rest_candidate_optimization_rank(
            intervals, candidate_in_daily_gap, 24.0, 45.0
        )

        self.assertLess(redundant_rank, daily_rank)

    def test_regular_45_hour_rest_resets_reduced_rest_sequence(self) -> None:
        planner = self.make_planner()
        intervals = [
            (datetime(2026, 9, 1, 8), datetime(2026, 9, 1, 16)),
            (datetime(2026, 9, 2, 22), datetime(2026, 9, 3, 6)),  # 30 h
            (datetime(2026, 9, 5, 8), datetime(2026, 9, 5, 16)),  # 50 h
            (datetime(2026, 9, 6, 22), datetime(2026, 9, 7, 6)),  # 30 h
        ]

        violations = planner.consecutive_reduced_weekly_rest_violations(
            intervals,
            reduced_weekly_rest=24.0,
            regular_weekly_rest=45.0,
        )

        self.assertEqual([], violations)

    def test_candidate_creating_second_reduced_rest_is_rejected(self) -> None:
        planner = self.make_planner()
        existing = [
            (datetime(2026, 9, 1, 8), datetime(2026, 9, 1, 16)),
            (datetime(2026, 9, 2, 22), datetime(2026, 9, 3, 6)),  # 30 h
            (datetime(2026, 9, 5, 8), datetime(2026, 9, 5, 16)),  # 50 h
        ]

        violation = planner.new_consecutive_reduced_weekly_rest_violation(
            existing,
            (datetime(2026, 9, 4, 12), datetime(2026, 9, 4, 20)),
            reduced_weekly_rest=24.0,
            regular_weekly_rest=45.0,
        )

        self.assertIsNotNone(violation)

    def test_gryska_old_august_violation_does_not_block_first_september_duty(self) -> None:
        planner = self.make_planner()
        existing = [
            (datetime(2026, 8, 18, 16, 55), datetime(2026, 8, 19, 2, 55)),
            (datetime(2026, 8, 19, 16, 55), datetime(2026, 8, 20, 2, 55)),
            (datetime(2026, 8, 20, 16, 55), datetime(2026, 8, 21, 2, 55)),
            (datetime(2026, 8, 21, 16, 55), datetime(2026, 8, 22, 2, 55)),
            (datetime(2026, 8, 23, 4, 30), datetime(2026, 8, 23, 15, 20)),
            (datetime(2026, 8, 24, 4, 55), datetime(2026, 8, 24, 14, 55)),
            (datetime(2026, 8, 25, 4, 55), datetime(2026, 8, 25, 14, 55)),
            (datetime(2026, 8, 26, 16, 2), datetime(2026, 8, 27, 3, 20)),
            (datetime(2026, 8, 28, 4, 35), datetime(2026, 8, 28, 15, 5)),
            (datetime(2026, 8, 29, 5, 0), datetime(2026, 8, 29, 15, 3)),
        ]
        first_september_duty = (
            datetime(2026, 9, 1, 16, 40),
            datetime(2026, 9, 2, 3, 15),
        )

        before = planner.consecutive_reduced_weekly_rest_violations(existing, 24.0, 45.0)
        after = planner.consecutive_reduced_weekly_rest_violations(
            existing + [first_september_duty], 24.0, 45.0
        )
        new_violation = planner.new_consecutive_reduced_weekly_rest_violation(
            existing, first_september_duty, 24.0, 45.0
        )

        self.assertEqual(1, len(before))
        # Późniejszy regularny odpoczynek może zamknąć starą sekwencję z sierpnia;
        # kandydat nie tworzy nowego naruszenia we wrześniu.
        self.assertEqual(0, len(after))
        self.assertIsNone(new_violation)

    def test_weekend_boundary_itself_uses_daily_rest_limit(self) -> None:
        rn_end = datetime(2026, 9, 5, 7, 20)
        next_start = datetime(2026, 9, 6, 4, 30)

        required, rest_kind = DutyPlannerApp.required_rest_between_services(
            rn_end,
            next_start,
            min_daily_rest=9.0,
            min_weekly_rest=24.0,
        )
        actual = (next_start - rn_end).total_seconds() / 3600.0

        self.assertEqual("daily", rest_kind)
        self.assertEqual(9.0, required)
        self.assertAlmostEqual(21 + 10 / 60, actual)
        self.assertGreater(actual, required)

    def test_friday_rn_requires_24_hours_before_sunday_service(self) -> None:
        planner = self.make_planner()
        rn = {"code": "RN", "name": "Rezerwa nocna", "duty_type": "", "shift": "Rezerwa nocna", "line": ""}
        normal_duty = {"code": "41", "name": "", "duty_type": "", "shift": "I", "line": ""}
        rn_end = datetime(2026, 9, 5, 7, 20)
        sunday_start = datetime(2026, 9, 6, 4, 30)

        self.assertTrue(
            planner.friday_rn_requires_weekly_rest_before_sunday(rn, rn_end, sunday_start)
        )
        self.assertFalse(
            planner.friday_rn_requires_weekly_rest_before_sunday(normal_duty, rn_end, sunday_start)
        )
        self.assertLess((sunday_start - rn_end).total_seconds() / 3600.0, 24.0)

    def test_pilarczyk_sunday_monday_boundary_itself_uses_daily_rest_limit(self) -> None:
        rn_end = datetime(2026, 9, 6, 7, 20)
        next_start = datetime(2026, 9, 7, 4, 30)

        required, rest_kind = DutyPlannerApp.required_rest_between_services(
            rn_end,
            next_start,
            min_daily_rest=9.0,
            min_weekly_rest=24.0,
        )
        actual = (next_start - rn_end).total_seconds() / 3600.0

        self.assertEqual("daily", rest_kind)
        self.assertEqual(9.0, required)
        self.assertAlmostEqual(21 + 10 / 60, actual)
        self.assertGreater(actual, required)

    def test_pilarczyk_rn_before_manual_wg_uses_following_regular_weekly_rest(self) -> None:
        planner = self.make_planner()
        intervals = [
            (datetime(2026, 9, 1, 4, 20), datetime(2026, 9, 1, 15, 30)),
            (datetime(2026, 9, 2, 4, 20), datetime(2026, 9, 2, 15, 30)),
            (datetime(2026, 9, 3, 4, 20), datetime(2026, 9, 3, 15, 30)),
            (datetime(2026, 9, 4, 4, 20), datetime(2026, 9, 4, 15, 30)),
            # Hipotetyczna sobotnia służba, która domyka Pilarczykowi 8 RBH.
            (datetime(2026, 9, 5, 4, 30), datetime(2026, 9, 5, 15, 30)),
            (datetime(2026, 9, 7, 3, 45), datetime(2026, 9, 7, 15, 10)),
            (datetime(2026, 9, 8, 3, 45), datetime(2026, 9, 8, 15, 10)),
            (datetime(2026, 9, 9, 3, 45), datetime(2026, 9, 9, 15, 10)),
            (datetime(2026, 9, 10, 3, 45), datetime(2026, 9, 10, 15, 10)),
            (datetime(2026, 9, 11, 3, 45), datetime(2026, 9, 11, 15, 10)),
            (datetime(2026, 9, 12, 20, 20), datetime(2026, 9, 13, 7, 20)),
            # Następna realna służba dopiero po ręcznym WG i ciągu UW.
            (datetime(2026, 9, 28, 18, 10), datetime(2026, 9, 29, 3, 10)),
        ]

        selected = planner.selected_weekly_rest_gaps(intervals, 24.0, 45.0)
        selected_kinds = [
            "regular" if float(item["hours"]) >= 45.0 else "reduced"
            for item in selected
        ]

        self.assertEqual(["reduced", "regular"], selected_kinds)
        self.assertEqual(
            6,
            planner.weekly_rest_work_date_count(
                planner.merged_service_intervals(intervals),
                selected[0]["end"],
                selected[1]["start"],
            ),
        )
        self.assertEqual(
            [],
            planner.consecutive_reduced_weekly_rest_violations(intervals, 24.0, 45.0),
        )
        self.assertEqual([], planner.weekly_rest_deadline_violations(intervals, 24.0))

    def test_pilarczyk_full_sequence_exceeds_six_day_weekly_rest_deadline(self) -> None:
        planner = self.make_planner()
        intervals = [
            (datetime(2026, 8, 31, 4, 35), datetime(2026, 8, 31, 16, 25)),
            (datetime(2026, 9, 1, 18, 10), datetime(2026, 9, 2, 3, 10)),
            (datetime(2026, 9, 2, 18, 10), datetime(2026, 9, 3, 3, 10)),
            (datetime(2026, 9, 3, 18, 10), datetime(2026, 9, 4, 3, 10)),
            (datetime(2026, 9, 4, 18, 10), datetime(2026, 9, 5, 3, 10)),
            (datetime(2026, 9, 5, 20, 20), datetime(2026, 9, 6, 7, 20)),
            (datetime(2026, 9, 7, 4, 30), datetime(2026, 9, 7, 15, 10)),
            (datetime(2026, 9, 8, 4, 30), datetime(2026, 9, 8, 15, 10)),
            (datetime(2026, 9, 9, 4, 30), datetime(2026, 9, 9, 15, 10)),
            (datetime(2026, 9, 12, 4, 30), datetime(2026, 9, 12, 15, 30)),
        ]

        violations = planner.weekly_rest_deadline_violations(intervals, 24.0)

        self.assertEqual(1, len(violations))
        self.assertEqual(datetime(2026, 9, 7, 18, 10), violations[0]["deadline"])
        self.assertEqual(datetime(2026, 9, 9, 15, 10), violations[0]["observed_start"])

    def test_weekly_rest_may_start_during_week_before_six_day_deadline(self) -> None:
        planner = self.make_planner()
        intervals = [
            (datetime(2026, 8, 31, 4, 35), datetime(2026, 8, 31, 16, 25)),
            (datetime(2026, 9, 1, 18, 10), datetime(2026, 9, 2, 3, 10)),
            (datetime(2026, 9, 2, 18, 10), datetime(2026, 9, 3, 3, 10)),
            (datetime(2026, 9, 3, 18, 10), datetime(2026, 9, 4, 3, 10)),
            (datetime(2026, 9, 4, 18, 10), datetime(2026, 9, 5, 3, 10)),
            (datetime(2026, 9, 5, 20, 20), datetime(2026, 9, 6, 7, 20)),
            (datetime(2026, 9, 7, 4, 30), datetime(2026, 9, 7, 15, 10)),
            (datetime(2026, 9, 9, 4, 30), datetime(2026, 9, 9, 15, 10)),
        ]

        violations = planner.weekly_rest_deadline_violations(intervals, 24.0)

        self.assertEqual([], violations)

    def test_generator_rejects_service_that_pushes_rest_past_six_day_deadline(self) -> None:
        planner = self.make_planner()
        existing = [
            (datetime(2026, 8, 31, 4, 35), datetime(2026, 8, 31, 16, 25)),
            (datetime(2026, 9, 1, 18, 10), datetime(2026, 9, 2, 3, 10)),
            (datetime(2026, 9, 2, 18, 10), datetime(2026, 9, 3, 3, 10)),
            (datetime(2026, 9, 3, 18, 10), datetime(2026, 9, 4, 3, 10)),
            (datetime(2026, 9, 4, 18, 10), datetime(2026, 9, 5, 3, 10)),
            (datetime(2026, 9, 5, 20, 20), datetime(2026, 9, 6, 7, 20)),
            (datetime(2026, 9, 7, 4, 30), datetime(2026, 9, 7, 15, 10)),
        ]
        candidate = (datetime(2026, 9, 8, 4, 30), datetime(2026, 9, 8, 15, 10))

        violation = planner.new_weekly_rest_deadline_violation(existing, candidate, 24.0)

        self.assertIsNotNone(violation)
        self.assertEqual(datetime(2026, 9, 7, 18, 10), violation["deadline"])

    def test_seven_consecutive_rn_days_are_rejected_without_earlier_anchor(self) -> None:
        planner = self.make_planner()
        intervals = [
            (datetime(2026, 9, day, 20, 20), datetime(2026, 9, day + 1, 7, 20))
            for day in range(5, 11)
        ]
        friday_rn = (datetime(2026, 9, 11, 20, 20), datetime(2026, 9, 12, 7, 20))

        run_length = planner.work_date_run_containing_candidate_without_weekly_rest(
            intervals,
            friday_rn,
            24.0,
        )

        self.assertEqual(7, run_length)

    def test_actual_24_hour_gap_resets_work_date_run(self) -> None:
        planner = self.make_planner()
        intervals = [
            (datetime(2026, 9, day, 5, 0), datetime(2026, 9, day, 15, 0))
            for day in range(1, 5)
        ]
        sunday_rn = (datetime(2026, 9, 6, 20, 20), datetime(2026, 9, 7, 7, 20))

        run_length = planner.work_date_run_containing_candidate_without_weekly_rest(
            intervals,
            sunday_rn,
            24.0,
        )

        self.assertEqual(1, run_length)

    def test_szymanski_candidate_creating_shifted_second_reduced_rest_is_rejected(self) -> None:
        planner = self.make_planner()
        existing = [
            (datetime(2026, 9, 7, 4, 35), datetime(2026, 9, 7, 15, 5)),
            (datetime(2026, 9, 8, 4, 35), datetime(2026, 9, 8, 15, 5)),
            (datetime(2026, 9, 9, 4, 35), datetime(2026, 9, 9, 15, 5)),
            (datetime(2026, 9, 10, 4, 35), datetime(2026, 9, 10, 15, 5)),
            (datetime(2026, 9, 11, 4, 35), datetime(2026, 9, 11, 15, 5)),
            (datetime(2026, 9, 12, 16, 15), datetime(2026, 9, 13, 3, 25)),
        ]
        candidate = (datetime(2026, 9, 14, 4, 40), datetime(2026, 9, 14, 15, 50))

        violation = planner.new_consecutive_reduced_weekly_rest_violation(
            existing,
            candidate,
            reduced_weekly_rest=24.0,
            regular_weekly_rest=45.0,
        )

        self.assertIsNotNone(violation)

    def test_automatic_rn_is_never_forced_through_rest_block(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("                def add_rn_entry_if_missing(")
        end = source.index("                rn_blocks = rn_block_days()", start)
        helper_source = source[start:end]

        self.assertIn("if not rest_ok:\n                        return False", helper_source)
        self.assertNotIn("RN wpisane obowiazkowo mimo blokady odpoczynku", helper_source)

    def test_normal_weekday_transition_uses_daily_rest_limit(self) -> None:
        required, rest_kind = DutyPlannerApp.required_rest_between_services(
            datetime(2026, 9, 7, 16, 0),
            datetime(2026, 9, 8, 6, 0),
            min_daily_rest=9.0,
            min_weekly_rest=24.0,
        )

        self.assertEqual("daily", rest_kind)
        self.assertEqual(9.0, required)

    def test_existing_vacation_and_weekend_cover_required_weekly_rest(self) -> None:
        gap_start = datetime(2026, 9, 11, 0, 0)
        gap_end = datetime(2026, 9, 14, 4, 30)
        existing_days_off = {
            date(2026, 9, 11),
            date(2026, 9, 12),
            date(2026, 9, 13),
        }

        covered = DutyPlannerApp.existing_day_off_hours_in_rest_gap(
            gap_start,
            gap_end,
            existing_days_off,
        )

        self.assertEqual(72.0, covered)
        self.assertGreaterEqual(covered, 45.0)

    def test_partial_calendar_days_are_counted_only_inside_the_real_gap(self) -> None:
        covered = DutyPlannerApp.existing_day_off_hours_in_rest_gap(
            datetime(2026, 9, 11, 18, 0),
            datetime(2026, 9, 12, 6, 0),
            {date(2026, 9, 11), date(2026, 9, 12)},
        )

        self.assertEqual(12.0, covered)

    def test_auto_planner_credits_existing_day_off_entries_before_adding_wg(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def mark_required_regulation_wg_days() -> None:")
        end = source.index("            def fill_remaining_empty_workday_cells_with_reserve_or_wg", start)
        marker_source = source[start:end]

        self.assertIn("self.existing_day_off_hours_in_rest_gap(", marker_source)
        self.assertIn("self.is_day_off_duty(existing_duty)", marker_source)
        self.assertIn("self.selected_weekly_rest_gaps(", marker_source)
        self.assertIn("if gap_key not in selected_rest_keys", marker_source)
        required_check = "if marked_hours + 1e-6 >= required_hours:\n                            continue\n                        for day_obj in candidate_days:"
        self.assertIn(required_check, marker_source)
        self.assertNotIn("marked_hours = 0.0", marker_source)

    def test_technical_wg_never_fills_an_empty_workday(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def v428_force_fill_all_visible_empty_cells")
        end = source.index("            def v429_topup_rbh_from_technical_day_offs", start)
        final_fill_source = source[start:end]

        self.assertIn("if is_norm_workday:", final_fill_source)
        self.assertIn("continue", final_fill_source)
        self.assertIn("WG w dni robocze wolno dodać wyłącznie", final_fill_source)

    def test_vacation_gap_protects_only_weekends_and_holidays(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def rebuild_vacation_weekend_protection()")
        end = source.index("            rebuild_vacation_weekend_protection()", start)
        protection_source = source[start:end]

        self.assertIn("if self.is_planning_weekend_or_holiday(gap_day)", protection_source)
        self.assertNotIn("protected.update(gap_days)", protection_source)


if __name__ == "__main__":
    unittest.main()
