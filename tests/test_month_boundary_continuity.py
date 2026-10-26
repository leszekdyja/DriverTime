import ast
import sqlite3
import unittest
from datetime import date
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


class MonthBoundaryContinuityTests(unittest.TestCase):
    def setUp(self):
        self.planner = object.__new__(DutyPlannerApp)
        self.planner.conn = sqlite3.connect(':memory:')
        self.planner.conn.row_factory = sqlite3.Row
        self.planner.conn.execute('CREATE TABLE planned_schedule(plan_date TEXT, driver_id INTEGER, duty_id INTEGER)')
        self.duties = {
            1: {'code': '1', 'name': 'Służba 1', 'duty_type': 'SERVICE', 'shift': 'I', 'line': '', 'start_time': '05:00'},
            2: {'code': '2', 'name': 'Służba 2', 'duty_type': 'SERVICE', 'shift': 'II', 'line': '', 'start_time': '14:00'},
            3: {'code': 'RN', 'name': 'RN', 'duty_type': 'SERVICE', 'shift': 'II', 'line': '', 'start_time': '20:00'},
            4: {'code': 'W', 'name': 'W', 'duty_type': 'wolne', 'shift': '', 'line': '', 'start_time': ''},
        }
        self.planner.conn.execute('CREATE TABLE duties(id INTEGER, code TEXT)')
        self.planner.conn.executemany('INSERT INTO duties VALUES (?,?)', [(key, value['code']) for key, value in self.duties.items()])

    def tearDown(self):
        self.planner.conn.close()

    def insert(self, *rows):
        self.planner.conn.executemany('INSERT INTO planned_schedule VALUES (?,?,?)', rows)

    def carry(self, year=2026, month=10, drivers=None):
        return self.planner.month_boundary_continuity_duties(year, month, drivers or {1, 2, 3}, self.duties)

    def test_latest_regular_duty_in_same_week_is_carried(self):
        self.insert(('2026-09-28', 1, 1), ('2026-09-30', 1, 2), ('2026-09-30', 2, 1))
        self.assertEqual({1: 2, 2: 1}, self.carry())

    def test_older_weeks_and_excluded_drivers_do_not_seed_continuity(self):
        self.insert(('2026-09-25', 1, 1), ('2026-09-30', 2, 2))
        self.assertEqual({}, self.carry(drivers={1}))

    def test_reserves_night_duties_and_day_off_do_not_replace_regular_duty(self):
        self.insert(('2026-09-28', 1, 1), ('2026-09-29', 1, 3), ('2026-09-30', 1, 4))
        self.assertEqual({1: 1}, self.carry())

    def test_new_week_or_weekend_month_start_does_not_inherit(self):
        self.insert(('2026-05-29', 1, 1), ('2026-10-30', 1, 1))
        self.assertEqual({}, self.carry(2026, 6))  # Monday
        self.assertEqual({}, self.carry(2026, 11))  # Sunday

    def test_duty_has_only_one_owner_using_latest_assignment(self):
        self.insert(('2026-09-29', 1, 1), ('2026-09-30', 2, 1))
        self.assertEqual({2: 1}, self.carry())

    def test_december_uses_november_monday_not_the_whole_month(self):
        self.insert(('2026-11-27', 1, 1), ('2026-11-30', 2, 2))
        self.assertEqual({2: 2}, self.carry(2026, 12))

    def test_rn_continues_from_last_day_of_previous_month(self):
        self.insert(('2026-11-30', 1, 3), ('2026-11-30', 2, 3), ('2026-11-29', 3, 3))
        self.assertEqual({1, 2}, self.planner.month_boundary_rn_drivers(2026, 12))

    def test_rn_on_sunday_carries_into_monday_month_start(self):
        self.insert(('2027-01-31', 1, 3), ('2027-01-31', 2, 3))
        self.assertEqual({1, 2}, self.planner.month_boundary_rn_drivers(2027, 2))

    def test_friday_rn_does_not_create_seventh_night_on_saturday_month_start(self):
        self.insert(('2026-07-31', 1, 3))
        self.assertEqual(set(), self.planner.month_boundary_rn_drivers(2026, 8))

    def test_previous_month_rotation_exception_only_applies_to_started_block(self):
        source = Path(__file__).resolve().parents[1] / 'planowanie_sluzb_tkinter_v450.py'
        node = next(n for n in ast.walk(ast.parse(source.read_text(encoding='utf-8')))
                    if isinstance(n, ast.FunctionDef) and n.name == 'continues_boundary_rn')
        namespace = {'date': date, 'year': 2026, 'month': 12,
                     'boundary_rn_driver_ids': {1, 2}, 'self': self.planner}
        exec(compile(ast.Module(body=[node], type_ignores=[]), str(source), 'exec'), namespace)
        continuing = namespace['continues_boundary_rn']
        self.assertTrue(continuing(1, date(2026, 12, 1)))
        self.assertTrue(continuing(1, date(2026, 12, 4)))
        self.assertFalse(continuing(1, date(2026, 12, 6)))
        self.assertFalse(continuing(1, date(2027, 1, 1)))
        self.assertFalse(continuing(3, date(2026, 12, 1)))
