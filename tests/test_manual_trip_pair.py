import sqlite3
import unittest
from datetime import datetime, timedelta
from types import SimpleNamespace

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


class ManualTripPairTests(unittest.TestCase):
    def make_planner(self) -> DutyPlannerApp:
        return object.__new__(DutyPlannerApp)

    def test_zaglebie_is_handled_as_multi_driver_trip(self) -> None:
        planner = self.make_planner()
        duty = {
            "code": "ZAGLEBIE",
            "name": "Zagłębie Lubin",
            "duty_type": "",
            "shift": "",
            "line": "",
        }

        self.assertTrue(planner.is_zaglebie_duty(duty))
        self.assertTrue(planner.is_multi_driver_manual_duty(duty))
        self.assertEqual("Zagłębie", planner.manual_multi_driver_duty_title(duty))

    def test_najem_and_zaglebie_count_as_workdays_but_wg_does_not(self) -> None:
        planner = self.make_planner()
        base = {"name": "", "duty_type": "specjalna", "shift": "", "line": ""}

        self.assertTrue(planner.counts_as_workday({**base, "code": "NAJEM"}))
        self.assertTrue(planner.counts_as_workday({**base, "code": "NAJEM2"}))
        self.assertTrue(planner.counts_as_workday({**base, "code": "ZAGŁĘBIE"}))
        self.assertTrue(planner.counts_as_workday({**base, "code": "42", "duty_type": "służba liniowa"}))
        self.assertFalse(planner.counts_as_workday({**base, "code": "WG", "duty_type": "wolne/nieobecność"}))
        self.assertFalse(planner.counts_as_workday({**base, "code": "UW", "duty_type": "wolne/nieobecność"}))

    def test_najem_and_zaglebie_use_service_times_for_rest(self) -> None:
        planner = self.make_planner()
        base = {
            "name": "",
            "duty_type": "specjalna",
            "shift": "",
            "line": "",
            "start_time": "06:00",
            "end_time": "16:00",
            "total_time": "10",
            "work_range": "06:00-16:00",
        }

        for code in ("NAJEM", "NAJEM2", "ZAGŁĘBIE"):
            duty = {**base, "code": code}
            self.assertTrue(planner.is_rest_relevant_duty(duty), code)
            start_dt, end_dt = planner.duty_datetimes_for_duty("2026-09-12", duty)
            self.assertEqual(datetime(2026, 9, 12, 6, 0), start_dt)
            self.assertEqual(datetime(2026, 9, 12, 16, 0), end_dt)

    def test_compliance_detects_short_rest_before_najem(self) -> None:
        planner = self.make_planner()
        rows = [
            {
                "plan_date": "2026-09-11", "driver_id": 20, "duty_id": 45,
                "driver_name": "KIEROWCA", "code": "45", "duty_name": "Służba 45",
                "duty_type": "służba liniowa", "start_time": "13:50", "end_time": "23:30",
                "work_range": "13:50-23:30", "total_time": "9:40", "work_hours": "8",
                "break_duration": "", "break_range": "", "name": "Służba 45", "shift": "II", "line": "",
            },
            {
                "plan_date": "2026-09-12", "driver_id": 20, "duty_id": 70,
                "driver_name": "KIEROWCA", "code": "NAJEM", "duty_name": "Najem",
                "duty_type": "specjalna", "start_time": "06:00", "end_time": "16:00",
                "work_range": "06:00-16:00", "total_time": "10", "work_hours": "8",
                "break_duration": "", "break_range": "", "name": "Najem", "shift": "", "line": "",
            },
        ]
        planner.rows = lambda *_args, **_kwargs: rows
        planner.compliance_float = lambda key, default: {
            "min_daily_rest_hours": 9.0,
            "max_weekly_work_hours": 200.0,
            "max_consecutive_work_days": 20.0,
            "avg_weekly_work_hours": 200.0,
        }.get(key, default)
        planner.compliance_bool = lambda *_args, **_kwargs: False
        planner.standard_service_rbh_hours = lambda row: float(row["work_hours"])
        planner.parse_duration_hours = lambda _value: 0.0
        planner.work_norm_hours_for_range = lambda *_args: 200.0
        planner.contextual_compliance_suggestion = lambda *_args: ""

        violations = planner.build_compliance_violations("2026-09-11", "2026-09-12")
        daily_rest = [item for item in violations if item["rule_code"] == "ODPOCZYNEK_DOBOWY"]

        self.assertEqual(1, len(daily_rest))
        self.assertIn("45→NAJEM", daily_rest[0]["details"])
        self.assertIn("6,50<9 h", daily_rest[0]["details"])

    def test_compliance_detects_eleven_day_run_containing_najem(self) -> None:
        planner = self.make_planner()
        first_day = datetime(2026, 9, 10)
        rows = []
        for offset in range(11):
            is_najem = offset in {0, 1, 2, 3, 8, 9}
            rows.append(
                {
                    "plan_date": (first_day + timedelta(days=offset)).strftime("%Y-%m-%d"),
                    "driver_id": 20,
                    "duty_id": 70 if is_najem else 42,
                    "driver_name": "STRAWCZYŃSKI",
                    "code": "NAJEM" if is_najem else "42",
                    "duty_name": "Najem" if is_najem else "Służba 42",
                    "duty_type": "specjalna" if is_najem else "służba liniowa",
                    "start_time": "06:00" if is_najem else "08:00",
                    "end_time": "16:00",
                    "work_range": "",
                    "total_time": "10" if is_najem else "8",
                    "work_hours": "8",
                    "break_duration": "",
                    "break_range": "",
                    "name": "Najem" if is_najem else "Służba 42",
                    "shift": "",
                    "line": "",
                }
            )

        planner.rows = lambda *_args, **_kwargs: rows
        planner.compliance_float = lambda key, default: {
            "max_weekly_work_hours": 200.0,
            "max_consecutive_work_days": 6.0,
            "avg_weekly_work_hours": 200.0,
        }.get(key, default)
        planner.compliance_bool = lambda *_args, **_kwargs: False
        planner.duty_datetimes_for_duty = lambda plan_date, _row: (
            datetime.strptime(plan_date + " 08:00", "%Y-%m-%d %H:%M"),
            datetime.strptime(plan_date + " 16:00", "%Y-%m-%d %H:%M"),
        )
        planner.standard_service_rbh_hours = lambda row: float(row["work_hours"])
        planner.parse_duration_hours = lambda _value: 0.0
        planner.work_norm_hours_for_range = lambda *_args: 200.0
        planner.contextual_compliance_suggestion = lambda *_args: ""

        violations = planner.build_compliance_violations("2026-09-10", "2026-09-20")
        consecutive = [item for item in violations if item["rule_code"] == "CIAG_DNI_PRACY"]

        self.assertEqual(1, len(consecutive))
        self.assertIn("11>6", consecutive[0]["details"])
        self.assertIn("NAJEM", consecutive[0]["details"])

    def test_selected_companion_plans_exactly_both_drivers_without_prompt(self) -> None:
        planner = self.make_planner()
        planner.rn_pair_driver_field_has_value = lambda: True
        planner.require_selected_rn_pair_driver_id = lambda: 202
        planner.manual_trip_existing_plan_row = lambda *_args, **_kwargs: None
        planner.ask_manual_trip_driver_count = lambda _title: self.fail(
            "Nie należy pytać o liczbę kierowców, gdy para jest już wskazana."
        )
        duty = {
            "code": "NAJEM",
            "name": "Najem",
            "duty_type": "",
            "shift": "",
            "line": "",
        }

        driver_ids = DutyPlannerApp.resolve_manual_multi_driver_ids(
            planner,
            duty,
            base_driver_id=101,
            car_id=None,
            plan_date="2026-08-18",
        )

        self.assertEqual({101, 202}, driver_ids)

    def test_zaglebie_can_be_saved_more_than_once_for_the_same_day(self) -> None:
        planner = self.make_planner()
        planner.conn = sqlite3.connect(":memory:")
        planner.conn.row_factory = sqlite3.Row
        planner.conn.execute(
            """
            CREATE TABLE planned_schedule(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                plan_date TEXT NOT NULL,
                driver_id INTEGER NOT NULL,
                duty_id INTEGER NOT NULL,
                note TEXT,
                entry_source TEXT,
                car_id INTEGER,
                pair_no TEXT,
                updated_at TEXT,
                UNIQUE(plan_date, driver_id)
            )
            """
        )
        planner.note_var = SimpleNamespace(get=lambda: "")
        planner._manual_range_mode = True
        planner._manual_edit_plan_ids = []
        planner._manual_edit_plan_id = None
        planner.manual_note_with_companion_and_car = lambda *_args, **_kwargs: "Ręcznie: Zagłębie"
        planner.is_driver_forbidden_for_duty = lambda *_args: False
        planner.manual_driver_conflict_blocks_save = lambda *_args: False
        planner.delete_automatic_plan_conflicts_for_manual = lambda *_args: 0
        planner.reload_manual_plan_context_after_change = lambda *_args, **_kwargs: None
        planner.audit_and_store = lambda *_args: 0
        duty = {
            "code": "ZAGLEBIE",
            "name": "Zagłębie Lubin",
            "duty_type": "",
            "shift": "",
            "line": "",
        }

        first_result = DutyPlannerApp.save_reservation_plan_entries_single(
            planner,
            "2026-08-18",
            77,
            duty,
            {101},
            car_id=9,
        )
        second_result = DutyPlannerApp.save_reservation_plan_entries_single(
            planner,
            "2026-08-18",
            77,
            duty,
            {202},
            car_id=9,
        )

        rows = planner.conn.execute(
            "SELECT driver_id, duty_id, car_id, entry_source FROM planned_schedule ORDER BY driver_id"
        ).fetchall()
        self.assertEqual("ok", first_result)
        self.assertEqual("ok", second_result)
        self.assertEqual(
            [(101, 77, 9, "manual"), (202, 77, 9, "manual")],
            [tuple(row) for row in rows],
        )

    def test_missing_zaglebie_companion_replaces_only_automatic_entry(self) -> None:
        planner = self.make_planner()
        planner.conn = sqlite3.connect(":memory:")
        planner.conn.row_factory = sqlite3.Row
        planner.conn.executescript(
            """
            CREATE TABLE drivers(id INTEGER PRIMARY KEY, name TEXT NOT NULL);
            CREATE TABLE duties(
                id INTEGER PRIMARY KEY,
                code TEXT,
                name TEXT,
                duty_type TEXT,
                shift TEXT,
                line TEXT
            );
            CREATE TABLE planned_schedule(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                plan_date TEXT NOT NULL,
                driver_id INTEGER NOT NULL,
                duty_id INTEGER NOT NULL,
                note TEXT,
                entry_source TEXT,
                car_id INTEGER,
                pair_no TEXT,
                UNIQUE(plan_date, driver_id)
            );
            INSERT INTO drivers(id, name) VALUES (22, 'SWĘDRAK'), (25, 'ANDRZEJEWSKI');
            INSERT INTO duties(id, code, name, duty_type, shift, line)
            VALUES (70, 'ZAGŁĘBIE', 'ZAGŁĘBIE', 'specjalna', '', ''),
                   (19, '19', 'Służba 19', 'służba liniowa', 'I', '');
            INSERT INTO planned_schedule(plan_date, driver_id, duty_id, note, entry_source)
            VALUES ('2026-09-02', 25, 70, 'Ręcznie; z kierowcą SWĘDRAK', 'manual'),
                   ('2026-09-02', 22, 19, 'Automatycznie wg zasad', 'auto');
            """
        )

        changed = DutyPlannerApp.enforce_manual_trip_companions_for_period(
            planner,
            "2026-09-01",
            "2026-09-30",
        )

        rows = planner.conn.execute(
            """
            SELECT driver_id, duty_id, entry_source
            FROM planned_schedule
            WHERE plan_date='2026-09-02'
            ORDER BY driver_id
            """
        ).fetchall()
        self.assertEqual(2, changed)
        self.assertEqual(
            [(22, 70, "manual"), (25, 70, "manual")],
            [tuple(row) for row in rows],
        )


if __name__ == "__main__":
    unittest.main()
