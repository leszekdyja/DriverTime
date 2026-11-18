import unittest
from datetime import date
from planowanie_sluzb_tkinter_v450 import DutyPlannerApp

class SickLeaveRbhTests(unittest.TestCase):
    def test_sick_leave_counts_eight_hours_on_calendar_workdays(self):
        app=object.__new__(DutyPlannerApp)
        app._barborka_types={2026:"saturday"}
        app._new_year_eve_types={2026:"sunday"}
        duty={"code":"CH","work_hours":0,"duty_type":"wolne/nieobecność"}
        for day in (date(2026,12,3),date(2026,12,4),date(2026,12,31)):
            self.assertEqual(8,app.planned_entry_rbh_hours(duty,day))
            self.assertFalse(app.should_hide_vacation_on_date(duty,day))
        for day in (date(2026,12,5),date(2026,12,6),date(2026,12,24),date(2026,12,25),date(2026,12,26)):
            self.assertEqual(0,app.planned_entry_rbh_hours(duty,day))
        self.assertTrue(app.is_paid_rest_only_for_compliance(duty))
