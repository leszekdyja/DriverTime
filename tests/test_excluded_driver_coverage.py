import sqlite3
import unittest
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


SOURCE = Path(__file__).resolve().parents[1] / "planowanie_sluzb_tkinter_v450.py"


class ExcludedDriverCoverageTests(unittest.TestCase):
    def make_planner(self) -> DutyPlannerApp:
        planner = object.__new__(DutyPlannerApp)
        planner.conn = sqlite3.connect(":memory:")
        planner.conn.row_factory = sqlite3.Row
        planner.conn.executescript(
            """
            CREATE TABLE duties(
                id INTEGER PRIMARY KEY,
                code TEXT,
                name TEXT,
                line TEXT,
                duty_type TEXT,
                shift TEXT,
                work_range TEXT,
                work_hours TEXT,
                auto_plan_enabled INTEGER,
                can_plan_weekdays INTEGER,
                can_plan_saturday INTEGER,
                can_plan_sunday INTEGER
            );
            CREATE TABLE planned_schedule(
                id INTEGER PRIMARY KEY,
                plan_date TEXT,
                driver_id INTEGER,
                duty_id INTEGER,
                entry_source TEXT
            );
            CREATE TABLE monthly_excluded_duties(month_key TEXT, duty_id INTEGER);
            CREATE TABLE excluded_duties(duty_id INTEGER);
            CREATE TABLE monthly_excluded_drivers(month_key TEXT, driver_id INTEGER);

            INSERT INTO duties VALUES(
                30, '45', 'Służba-45', 'K-204', 'służba liniowa', 'WSP II',
                '13:50-23:30', '8', 1, 1, 0, 0
            );
            INSERT INTO planned_schedule VALUES(
                1, '2026-09-14', 45, 30, 'auto'
            );
            INSERT INTO monthly_excluded_drivers VALUES('2026-09', 45);
            """
        )
        return planner

    def test_service_assigned_to_monthly_excluded_driver_is_reported_missing(self) -> None:
        planner = self.make_planner()
        try:
            missing = planner.unplanned_services_for_range("2026-09-14", "2026-09-14")
        finally:
            planner.conn.close()

        self.assertEqual([("2026-09-14", "45")], [(item["date"], item["code"]) for item in missing])

    def test_service_assigned_to_available_driver_is_not_reported_missing(self) -> None:
        planner = self.make_planner()
        planner.conn.execute("DELETE FROM monthly_excluded_drivers")
        try:
            missing = planner.unplanned_services_for_range("2026-09-14", "2026-09-14")
        finally:
            planner.conn.close()

        self.assertEqual([], missing)

    def test_fixed_pair_path_rejects_driver_outside_available_month_pool(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def choose_driver_pair_for_duty_block(")
        end = source.index("            def duty_interval_for_date(", start)
        pair_source = source[start:end]

        self.assertIn("if int(driver_id) not in available_drivers_all:", pair_source)


if __name__ == "__main__":
    unittest.main()
