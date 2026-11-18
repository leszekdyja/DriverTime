import sqlite3
import unittest
from datetime import date
from types import SimpleNamespace
from planowanie_sluzb_tkinter_v450 import DutyPlannerApp

class BarborkaDutyTypeTests(unittest.TestCase):
    def test_default_is_saturday_and_calendar_norm_unchanged(self):
        app = object.__new__(DutyPlannerApp)
        self.assertEqual(5, app.planning_weekday(date(2026, 12, 4)))
        self.assertEqual(160, app.monthly_work_norm_hours(2026, 12))
        self.assertEqual(8, app.planned_entry_rbh_hours({"code": "UW"}, date(2026, 12, 4)))

    def test_all_choices_persist_per_year_independently_of_new_year_eve(self):
        with sqlite3.connect(":memory:") as conn:
            conn.execute("CREATE TABLE app_meta (key TEXT PRIMARY KEY, value TEXT)")
            for setting, expected in (("calendar", 4), ("workday", 4), ("saturday", 5), ("sunday", 6)):
                app = object.__new__(DutyPlannerApp)
                app.conn = conn
                app.get_target_year_month = lambda: (2026, 12)
                app.barborka_choice = SimpleNamespace(get=lambda: setting)
                app.apply_target_month_changed = lambda: None
                app.set_status = lambda *_: None
                app.save_barborka_duty_type()
                reopened = object.__new__(DutyPlannerApp)
                reopened.conn = conn
                self.assertEqual(expected, reopened.planning_weekday(date(2026, 12, 4)))
                self.assertEqual(5, reopened.planning_weekday(date(2027, 12, 4)))
                self.assertEqual(3, reopened.planning_weekday(date(2026, 12, 31)))
                duty = dict(can_plan_weekdays=0, can_plan_saturday=1, can_plan_sunday=0)
                self.assertEqual(expected == 5, reopened.duty_allowed_on_weekday(duty, expected))
