import sqlite3
import unittest
from planowanie_sluzb_tkinter_v450 import DutyPlannerApp

class RepeatableTextDutiesTests(unittest.TestCase):
    def test_only_numeric_service_codes_are_unique_per_day(self):
        app=object.__new__(DutyPlannerApp)
        for code in ("UW","CH","BO","SZK","BHP","R2","ABC","A12",""):
            self.assertFalse(app.unique_daily_service_code(code),code)
        for code in ("1","12","141"," 45 "):
            self.assertTrue(app.unique_daily_service_code(code),code)

    def test_manual_text_duty_preserves_other_drivers_automatic_entries(self):
        app=object.__new__(DutyPlannerApp)
        app.conn=sqlite3.connect(":memory:")
        app.conn.row_factory=sqlite3.Row
        app.conn.executescript("""
            CREATE TABLE duties(id INTEGER PRIMARY KEY,code TEXT);
            CREATE TABLE planned_schedule(id INTEGER PRIMARY KEY,plan_date TEXT,driver_id INTEGER,duty_id INTEGER,entry_source TEXT);
            INSERT INTO duties VALUES(1,'BO'),(2,'12');
            INSERT INTO planned_schedule VALUES
            (1,'2026-12-03',10,1,'auto'),
            (2,'2026-12-03',20,1,'auto'),
            (3,'2026-12-03',30,2,'auto');
        """)
        app.wg_duty_ids=lambda:set()
        app.duty_id_by_code=lambda _:None
        try:
            self.assertEqual({2},app.automatic_plan_conflict_ids_for_manual("2026-12-03",20,1))
            self.assertEqual({3},app.automatic_plan_conflict_ids_for_manual("2026-12-03",40,2))
            app.delete_automatic_plan_conflicts_for_manual("2026-12-03",20,1)
            self.assertEqual(1,app.conn.execute("SELECT count(*) FROM planned_schedule WHERE id=1").fetchone()[0])
        finally:
            app.conn.close()
