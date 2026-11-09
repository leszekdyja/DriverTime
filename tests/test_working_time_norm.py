import unittest
from datetime import date
from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


class WorkingTimeNormTests(unittest.TestCase):
    def setUp(self):
        self.planner = object.__new__(DutyPlannerApp)

    def test_december_2026_is_160_including_christmas_eve_and_saturday_holiday(self):
        self.assertEqual(160, self.planner.monthly_work_norm_hours(2026, 12))
        self.assertEqual(160, self.planner.work_norm_hours_for_range('2026-12-01', '2026-12-31'))
        for day in (24, 25, 26):
            self.assertTrue(self.planner.is_public_holiday(date(2026, 12, day)))

    def test_christmas_eve_only_from_2025_and_sunday_does_not_reduce_norm(self):
        self.assertNotIn(date(2024, 12, 24), self.planner.polish_public_holidays(2024))
        for year, expected in ((2024, 160), (2025, 160), (2026, 160), (2027, 168), (2028, 152)):
            self.assertEqual(expected, self.planner.monthly_work_norm_hours(year, 12), year)

    def test_all_months_2026_and_reversed_range(self):
        expected = [160, 160, 176, 168, 160, 168, 184, 160, 176, 176, 160, 160]
        for month, hours in enumerate(expected, 1):
            self.assertEqual(hours, self.planner.monthly_work_norm_hours(2026, month), month)
        self.assertEqual(0, self.planner.work_norm_hours_for_range('2026-12-31', '2026-12-01'))


if __name__ == '__main__':
    unittest.main()
