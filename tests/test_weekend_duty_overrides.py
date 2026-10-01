import unittest
from datetime import date
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


SOURCE = Path(__file__).resolve().parents[1] / "planowanie_sluzb_tkinter_v450.py"


class WeekendDutyOverrideTests(unittest.TestCase):
    def test_code_parser_accepts_common_separators_and_removes_duplicates(self) -> None:
        actual = DutyPlannerApp.parse_weekend_duty_codes("18, 41;59  18")

        self.assertEqual(["18", "41", "59"], actual)

    def test_exact_date_override_wins_before_standard_day_flags(self) -> None:
        class Harness:
            def is_weekend_duty_override(self, duty_id: int, target_date: date) -> bool:
                return duty_id == 18 and target_date == date(2026, 10, 3)

            def rows(self, *_args, **_kwargs):
                raise AssertionError("standard duty flags must not be queried for an exact override")

        allowed = DutyPlannerApp.duty_id_allowed_on_date(Harness(), 18, date(2026, 10, 3))

        self.assertTrue(allowed)

    def test_generator_and_unplanned_report_use_the_same_date_override(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_start = source.index("    def auto_plan_all_drivers(self) -> None:")
        report_start = source.index("    def unplanned_services_for_range", auto_start)
        auto_source = source[auto_start:report_start]
        report_source = source[report_start:]

        self.assertIn("weekend_override_counts_by_date = self.weekend_duty_override_counts(start, end)", auto_source)
        self.assertIn("def required_rn_count_for_planning(", auto_source)
        self.assertIn("def duty_required_on_exact_date(", auto_source)
        self.assertIn("exact_weekend_override_ids = weekend_override_ids_by_date.get(target_date, set())", auto_source)
        self.assertIn("exact_weekend_override = duty_id in weekend_override_ids_by_date.get(iso_date, set())", report_source)
        self.assertIn("if not exact_weekend_override and not self.duty_allowed_on_weekday", report_source)

    def test_ui_and_database_keep_overrides_in_a_separate_feature(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")

        self.assertIn("CREATE TABLE IF NOT EXISTS weekend_duty_overrides", source)
        self.assertIn('self.create_weekend_duty_overrides_tab()', source)
        self.assertIn('self.create_scrollable_tab("Służby sob./niedz."', source)
        self.assertIn("UNIQUE(plan_date, duty_id)", source)
        self.assertIn("required_count INTEGER NOT NULL DEFAULT 1", source)
        self.assertIn('text="Liczba obsad:"', source)

    def test_override_counts_preserve_multiple_reserve_and_rn_requirements(self) -> None:
        class Harness:
            def rows(self, *_args, **_kwargs):
                return [
                    {"plan_date": "2026-10-03", "duty_id": 63, "required_count": 4},
                    {"plan_date": "2026-10-03", "duty_id": 72, "required_count": 3},
                    {"plan_date": "2026-10-04", "duty_id": 81, "required_count": 5},
                ]

        counts = DutyPlannerApp.weekend_duty_override_counts(Harness(), "2026-10-01", "2026-10-31")

        self.assertEqual({63: 4, 72: 3}, counts["2026-10-03"])
        self.assertEqual({81: 5}, counts["2026-10-04"])

    def test_unplanned_report_counts_missing_copies_instead_of_hiding_them(self) -> None:
        class Harness:
            def plan_month_key_for_range(self, *_args):
                return "2026-10"

            def excluded_duty_ids_for_month(self, *_args):
                return set()

            def excluded_driver_ids_for_month(self, *_args):
                return set()

            def weekend_duty_override_counts(self, *_args):
                return {"2026-10-03": {63: 3}}

            def rows(self, sql, *_args):
                if "FROM duties" in sql:
                    return [{
                        "id": 63,
                        "code": "R",
                        "name": "Rezerwa",
                        "line": "",
                        "duty_type": "rezerwa",
                        "shift": "I",
                        "work_range": "04:40-14:40",
                        "work_hours": "8",
                        "auto_plan_enabled": 0,
                        "can_plan_weekdays": 0,
                        "can_plan_saturday": 0,
                        "can_plan_sunday": 0,
                    }]
                if "FROM planned_schedule" in sql:
                    return [{"plan_date": "2026-10-03", "duty_id": 63, "driver_id": 7}]
                return []

            def is_rn_duty(self, *_args):
                return False

            def duty_id_by_code(self, code):
                return 63 if code == "R" else None

            def planning_weekday(self, day):
                return day.weekday()

            def is_day_off_duty(self, *_args):
                return False

            def duty_allowed_on_weekday(self, *_args):
                return False

            def short_unplanned_reason_suggestion(self, *_args):
                return ("brak", "uzupełnij")

        missing = DutyPlannerApp.unplanned_services_for_range(Harness(), "2026-10-03", "2026-10-03")

        self.assertEqual(1, len(missing))
        self.assertEqual("R", missing[0]["code"])
        self.assertEqual(2, missing[0]["missing"])

    def test_picker_lists_reserves_and_supports_multiple_selection(self) -> None:
        class FakeListbox:
            def __init__(self) -> None:
                self.values = []

            def delete(self, *_args) -> None:
                self.values.clear()

            def insert(self, _position, value) -> None:
                self.values.append(value)

            def curselection(self):
                return (0, 1)

        planner = object.__new__(DutyPlannerApp)
        planner.weekend_override_duty_listbox = FakeListbox()
        planner.rows = lambda *_args, **_kwargs: [
            {"id": 63, "code": "R", "name": "Rezerwa", "duty_type": "rezerwa", "shift": "I", "work_range": "04:40-14:40"},
            {"id": 72, "code": "R2", "name": "Rezerwa II", "duty_type": "rezerwa", "shift": "II", "work_range": "14:00-22:00"},
            {"id": 64, "code": "WG", "name": "Wolne", "duty_type": "wolne", "shift": "", "work_range": ""},
        ]
        planner.is_day_off_duty = lambda duty: duty["code"] == "WG"

        planner.load_weekend_override_duty_choices()

        self.assertEqual([63, 72], planner.weekend_override_duty_ids_by_index)
        self.assertEqual([63, 72], planner.selected_weekend_override_duty_ids())
        self.assertTrue(any(value.startswith("R —") for value in planner.weekend_override_duty_listbox.values))
        self.assertTrue(any(value.startswith("R2 —") for value in planner.weekend_override_duty_listbox.values))

    def test_exact_weekend_reserve_override_bypasses_only_automatic_exclusion(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_source = source[source.index("    def auto_plan_all_drivers(self) -> None:"):]

        self.assertIn(
            "if duty_id in auto_plan_disabled_reserve_duty_ids and not exact_weekend_override:",
            auto_source,
        )
        self.assertIn(
            "and (int(row[\"id\"]) in exact_weekend_override_ids or int(row[\"id\"]) not in reserve_duty_id_set)",
            auto_source,
        )
        self.assertIn(
            "if int(entry[2]) in disabled_auto_duty_ids_v268 and not entry_is_exact_override:",
            auto_source,
        )
        report_source = source[source.index("    def unplanned_services_for_range"):]
        self.assertIn(
            "if duty_id in {int(x) for x in reserve_duty_ids} and not exact_weekend_override:",
            report_source,
        )
        self.assertIn("v501: datowane R/R2 mogą wymagać kilku niezależnych obsad", auto_source)
        self.assertIn("weekend_override_counts_by_date.get(iso_date, {}).get(rn_duty_id", report_source)

    def test_dated_weekend_reserve_is_not_released_by_final_rescue_stages(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_source = source[source.index("    def auto_plan_all_drivers(self) -> None:"):]

        self.assertIn("def is_hard_weekend_override_entry(", auto_source)
        self.assertGreaterEqual(auto_source.count("is_hard_weekend_override_entry("), 8)

        reclaim_start = auto_source.index("            def reclaim_lower_priority_entries_for_high_priority_shortages")
        final_swap_start = auto_source.index("            def final_swap_same_driver_reserve_to_wg_for_missing_service")
        release_start = auto_source.index("            def fill_unfilled_by_releasing_other_day_reserve_or_low_priority")
        move_start = auto_source.index("            def move_reserve_before_working_weekend_to_wg")
        consistency_start = auto_source.index("            def enforce_reserve_shift_consistency")

        self.assertIn("if is_hard_weekend_override_entry(entry):", auto_source[reclaim_start:final_swap_start])
        self.assertIn("if is_hard_weekend_override_entry(entry):", auto_source[final_swap_start:release_start])
        self.assertIn("if is_hard_weekend_override_entry(source_entry):", auto_source[release_start:move_start])
        self.assertIn("if is_hard_weekend_override_entry(entry):", auto_source[move_start:consistency_start])
        self.assertIn("if is_hard_weekend_override_entry(entry):", auto_source[consistency_start:])


if __name__ == "__main__":
    unittest.main()
