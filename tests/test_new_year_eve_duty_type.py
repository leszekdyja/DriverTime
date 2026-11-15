import sqlite3
import unittest
from datetime import date
from types import SimpleNamespace

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


class NewYearEveDutyTypeTests(unittest.TestCase):
    def test_saved_choice_controls_only_new_year_eve_for_selected_year(self):
        with sqlite3.connect(":memory:") as conn:
            conn.execute("CREATE TABLE app_meta (key TEXT PRIMARY KEY, value TEXT)")
            for label, setting, weekday in (
                ("Sobota", "saturday", 5),
                ("Niedziela", "sunday", 6),
                ("Dzień roboczy", "workday", 0),
                ("Według kalendarza", "calendar", 6),
            ):
                with self.subTest(label=label):
                    planner = object.__new__(DutyPlannerApp)
                    planner.conn = conn
                    planner.get_target_year_month = lambda: (2028, 12)
                    planner.new_year_eve_choice = SimpleNamespace(get=lambda: setting)
                    planner.apply_target_month_changed = lambda: None
                    planner.set_status = lambda *_: None
                    planner.save_new_year_eve_duty_type()

                    reopened = object.__new__(DutyPlannerApp)
                    reopened.conn = conn
                    self.assertEqual(setting, reopened.new_year_eve_duty_type(2028))
                    self.assertEqual(weekday, reopened.planning_weekday(date(2028, 12, 31)))
                    self.assertEqual(6, reopened.planning_weekday(date(2028, 12, 25)))
                    self.assertEqual(date(2026, 12, 31).weekday(), reopened.planning_weekday(date(2026, 12, 31)))

                    flags = ("can_plan_weekdays", "can_plan_saturday", "can_plan_sunday")
                    for i, flag in enumerate(flags):
                        duty = dict.fromkeys(flags, 0)
                        duty[flag] = 1
                        self.assertEqual(i == (0 if weekday < 5 else weekday - 4),
                                         reopened.duty_allowed_on_weekday(duty, weekday))
