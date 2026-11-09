import unittest
from datetime import date
from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


class ChristmasWorkLimitTests(unittest.TestCase):
    def test_every_other_christmas_workday_blocks_candidate(self):
        for candidate in (24, 25, 26):
            for worked in (24, 25, 26):
                self.assertEqual(candidate != worked, DutyPlannerApp.christmas_work_limit_exceeded(
                    date(2026, 12, candidate), {date(2026, 12, worked)}))

    def test_other_dates_years_and_empty_context_do_not_block(self):
        for candidate in (23, 24, 25, 26, 27):
            self.assertFalse(DutyPlannerApp.christmas_work_limit_exceeded(date(2026, 12, candidate),
                {date(2025, 12, 24), date(2026, 12, 23), date(2026, 12, 27)}))
        self.assertFalse(DutyPlannerApp.christmas_work_limit_exceeded(date(2026, 12, 24), set()))

    def test_rn_blocks_split_each_christmas_date_without_losing_days(self):
        planner = object.__new__(DutyPlannerApp)
        for year in range(2026, 2040):
            blocks = planner.rn_planning_blocks_for_month(year, 12)
            days = [day for block in blocks for day in block]
            self.assertEqual(31, len(days))
            self.assertEqual(31, len(set(days)))
            for day in (24, 25, 26):
                self.assertIn([date(year, 12, day)], blocks)


if __name__ == '__main__':
    unittest.main()
