import ast
import unittest
from datetime import date, timedelta
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


class RnWeekdayHolidayTests(unittest.TestCase):
    def test_november_holiday_before_workday_requires_two(self):
        planner = object.__new__(DutyPlannerApp)
        self.assertEqual(2, planner.rn_required_count_for_date(date(2026, 11, 11)))

    def test_holiday_before_another_holiday_or_weekend_requires_one(self):
        planner = object.__new__(DutyPlannerApp)
        self.assertEqual(1, planner.rn_required_count_for_date(date(2026, 12, 24)))
        self.assertEqual(1, planner.rn_required_count_for_date(date(2026, 12, 25)))

    def test_full_rn_reservation_protects_holiday_and_all_other_block_days(self):
        source = Path(__file__).resolve().parents[1] / 'planowanie_sluzb_tkinter_v450.py'
        tree = ast.parse(source.read_text(encoding='utf-8'))
        helper = next(node for node in ast.walk(tree) if isinstance(node, ast.FunctionDef)
                      and node.name == 'driver_reserved_for_full_rn_block')
        namespace = {'date': date, 'timedelta': timedelta,
                     'rn_week_drivers': {('niedziela-piatek', '2026-11-08'): [12, 30]}}
        exec(compile(ast.Module(body=[helper], type_ignores=[]), str(source), 'exec'), namespace)
        reserved = namespace['driver_reserved_for_full_rn_block']
        for day in range(8, 14):
            self.assertTrue(reserved(12, date(2026, 11, day)))
            self.assertFalse(reserved(21, date(2026, 11, day)))
        self.assertFalse(reserved(12, date(2026, 11, 14)))
