import unittest
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


SOURCE = Path(__file__).resolve().parents[1] / "planowanie_sluzb_tkinter_v450.py"


class ConsolidatedPlanningTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source = SOURCE.read_text(encoding="utf-8")
        start = cls.source.index("    def auto_plan_all_drivers(self) -> None:")
        cls.auto_source = cls.source[start:]

    def test_monthly_handover_analysis_is_available_in_the_shared_file(self) -> None:
        self.assertIn('text="Analiza służby do przekazania"', self.source)
        self.assertIn("    def analyze_monthly_duty_handover(self) -> None:", self.source)
        self.assertIn("    def simulate_monthly_handover_variant(", self.source)
        self.assertIn("monthly_excluded_duties", self.source)

    def test_monthly_handover_candidates_still_return_real_services(self) -> None:
        planner = object.__new__(DutyPlannerApp)
        planner.excluded_duty_ids_for_month = lambda _month_key: set()
        planner.rows = lambda _sql: [
            {
                "id": 25,
                "code": "25",
                "name": "Służba 25",
                "duty_type": "SERVICE",
                "auto_plan_enabled": 1,
                "can_plan_weekdays": 1,
                "can_plan_saturday": 1,
                "can_plan_sunday": 1,
            }
        ]
        planner.duty_allowed_on_weekday = lambda _row, _weekday: True
        planner.planning_weekday = lambda day: day.weekday()

        result = planner.monthly_handover_candidates(
            "2026-08-01", "2026-08-31", "2026-08"
        )

        self.assertEqual(1, len(result))
        self.assertEqual(25, result[0]["id"])
        self.assertEqual(31, result[0]["occurrences"])

    def test_global_matching_uses_saved_entries_and_processes_weekends_first(self) -> None:
        start = self.auto_source.index(
            "            def global_match_missing_services_to_empty_cells_weekend_first"
        )
        end = self.auto_source.index(
            "            # Końcówka planowania bez używania technicznego WG/W do RBH.",
            start,
        )
        matching_source = self.auto_source[start:end]

        self.assertIn("for entry in entries", matching_source)
        self.assertIn("weekend_rank = 0 if self.is_planning_weekend_or_holiday(day_obj) else 1", matching_source)
        self.assertIn("driver_to_duty", matching_source)
        self.assertIn("def augment(", matching_source)

    def test_technical_wg_is_disabled_and_required_rest_marking_remains(self) -> None:
        self.assertNotIn("allow_technical_wg=True", self.auto_source)
        self.assertIn("for _v451_final_wg_pass in range(0):", self.auto_source)
        self.assertIn("mark_required_regulation_wg_days()", self.auto_source)
        self.assertIn("existing_day_off_hours_in_rest_gap(", self.auto_source)

    def test_final_weekly_continuity_rewrite_is_skipped(self) -> None:
        start = self.auto_source.rindex(
            "            self.enforce_manual_rn_pairs_for_period(start, end)"
        )
        end = self.auto_source.index(
            "            self.result_start_var.set(start)",
            start,
        )
        final_save_source = self.auto_source[start:end]

        self.assertNotIn("auto_rewrite_weekly_continuity_for_month", final_save_source)
        self.assertNotIn("for continuity_round", final_save_source)
        self.assertIn("v473: pomiń dawną końcową korektę ciągłości", final_save_source)
        self.assertIn("weekday_weekly_duty_owner", self.auto_source)
        self.assertIn("assign_weekly_rest_buffer_by_rbh_shortage", self.auto_source)

    def test_direct_month_start_shift_is_hard_in_first_week(self) -> None:
        self.assertIn("direct_month_start_shifts = self.driver_month_start_shift_map(month_key)", self.auto_source)
        self.assertIn("def direct_month_start_shift_block_reason", self.auto_source)
        self.assertIn(
            'preferred = getattr(self, "_active_month_start_shifts", {}).get(int(driver_id))',
            self.auto_source,
        )
        self.assertIn("has_real_month_work = any(", self.auto_source)
        self.assertIn("if shift_week_index_for_date(day_obj) != 0 and has_real_month_work:", self.auto_source)
        self.assertGreaterEqual(
            self.auto_source.count("direct_month_start_shift_block_reason("),
            4,
        )

    def test_inherited_month_start_shift_is_effective_and_hard(self) -> None:
        import sqlite3

        planner = object.__new__(DutyPlannerApp)
        planner.conn = sqlite3.connect(":memory:")
        planner.conn.row_factory = sqlite3.Row
        planner.conn.execute(
            """
            CREATE TABLE driver_month_shift_preferences(
                month_key TEXT NOT NULL,
                driver_id INTEGER NOT NULL,
                start_shift TEXT NOT NULL,
                updated_at TEXT
            )
            """
        )
        planner.conn.execute(
            "INSERT INTO driver_month_shift_preferences VALUES ('2026-08', 36, 'morning', '2026-08-01')"
        )
        try:
            info = planner.driver_month_start_shift_effective_info(36, "2026-09")
            effective = planner.driver_month_start_shift_effective_map("2026-09")
        finally:
            planner.conn.close()

        self.assertIsNotNone(info)
        self.assertEqual("afternoon", info[0])
        self.assertEqual("inherited", info[1])
        self.assertEqual("afternoon", effective[36])

    def test_shift_form_does_not_present_inherited_value_as_direct_choice(self) -> None:
        start = self.source.index("    def load_driver_month_start_shift_preference(self) -> None:")
        end = self.source.index("    def save_driver_month_start_shift_preference(self) -> None:", start)
        load_source = self.source[start:end]

        self.assertIn("self.driver_month_start_shift_map(month_key).get", load_source)
        self.assertNotIn("driver_month_start_shift_effective_info", load_source)

    def test_weekend_service_can_be_rebalanced_to_close_rbh_without_losing_coverage(self) -> None:
        start = self.auto_source.index(
            "            def rebalance_filled_weekend_services_for_rbh"
        )
        end = self.auto_source.index(
            "            def assign_weekly_rest_buffer_by_rbh_shortage",
            start,
        )
        rebalance_source = self.auto_source[start:end]

        self.assertIn("temporarily_remove_auto_entry", rebalance_source)
        self.assertIn("reserve_slot_for_source", rebalance_source)
        self.assertIn("driver_has_required_rest", rebalance_source)
        self.assertIn("maksymalnie 6 dni pracy sprawdzone", rebalance_source)
        self.assertIn("bilans RBH obu kierowców zachowany", rebalance_source)
        self.assertIn(
            'rebalance_filled_weekend_services_for_rbh(\n                "v487 weekendowa zamiana służby dla niedoboru RBH"',
            self.auto_source,
        )


if __name__ == "__main__":
    unittest.main()
