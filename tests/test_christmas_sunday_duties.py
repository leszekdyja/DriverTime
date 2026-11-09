import unittest
from datetime import date

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


class ChristmasSundayDutiesTests(unittest.TestCase):
    def setUp(self):
        self.planner = object.__new__(DutyPlannerApp)

    def test_christmas_uses_sunday_in_every_calendar_weekday(self):
        for year in range(2026, 2040):
            for day in (25, 26):
                with self.subTest(year=year, day=day):
                    holiday = date(year, 12, day)
                    self.assertEqual(6, self.planner.planning_weekday(holiday))
                    self.assertFalse(self.planner.is_planning_saturday(holiday))
                    self.assertTrue(self.planner.is_planning_weekend_or_holiday(holiday))
                    for weekdays, saturday, sunday in ((1, 0, 0), (0, 1, 0), (0, 0, 1), (1, 1, 1)):
                        duty = dict(auto_plan_enabled=1, can_plan_weekdays=weekdays,
                                    can_plan_saturday=saturday, can_plan_sunday=sunday)
                        self.assertEqual(bool(sunday), self.planner.duty_allowed_on_weekday(
                            duty, self.planner.planning_weekday(holiday)))

    def test_other_holidays_and_regular_days_are_unchanged(self):
        for holiday in (date(2026, 12, 24), date(2026, 11, 11), date(2027, 1, 1)):
            self.assertEqual(5, self.planner.planning_weekday(holiday))
        for day in (date(2026, 12, 23), date(2026, 12, 27), date(2026, 12, 28)):
            self.assertEqual(day.weekday(), self.planner.planning_weekday(day))


if __name__ == '__main__':
    unittest.main()
