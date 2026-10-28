import sqlite3
import unittest
import tempfile
from pathlib import Path
from unittest.mock import Mock

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


class GlobalPlanningSettingsTests(unittest.TestCase):
    def setUp(self):
        self.app = object.__new__(DutyPlannerApp)
        self.app.conn = sqlite3.connect(':memory:')
        self.app.conn.row_factory = sqlite3.Row
        self.addCleanup(self.app.conn.close)
        self.app.conn.executescript('''
            CREATE TABLE app_meta(key TEXT PRIMARY KEY,value TEXT);
            CREATE TABLE excluded_duties(duty_id INTEGER PRIMARY KEY);
            CREATE TABLE monthly_excluded_duties(month_key TEXT,duty_id INTEGER,PRIMARY KEY(month_key,duty_id));
            CREATE TABLE monthly_excluded_drivers(month_key TEXT,driver_id INTEGER,PRIMARY KEY(month_key,driver_id));
            CREATE TABLE fixed_driver_duties(month_key TEXT,driver_id INTEGER,duty_id INTEGER,duty1_id INTEGER,duty2_id INTEGER,updated_at TEXT,PRIMARY KEY(month_key,driver_id));
            CREATE TABLE driver_month_shift_preferences(month_key TEXT,driver_id INTEGER,start_shift TEXT,updated_at TEXT,PRIMARY KEY(month_key,driver_id));
            CREATE TABLE planned_schedule(id INTEGER PRIMARY KEY,plan_date TEXT,note TEXT,entry_source TEXT);
            INSERT INTO monthly_excluded_duties VALUES ('2026-11',1),('2026-12',2);
            INSERT INTO monthly_excluded_drivers VALUES ('2026-11',10),('2026-12',20);
            INSERT INTO fixed_driver_duties VALUES ('2026-11',30,3,3,4,'now'),('2026-12',30,5,5,6,'old');
            INSERT INTO driver_month_shift_preferences VALUES ('2026-11',30,'morning','now'),('2026-12',30,'morning','old'),('2026-10',40,'afternoon','old');
            INSERT INTO planned_schedule VALUES (1,'2026-11-02','manual cell','manual'),(2,'2026-12-02','generated cell','auto');
        ''')

    def activate(self):
        self.app.ensure_global_planning_settings_v511()

    def test_november_is_shared_across_months_and_years(self):
        self.activate()
        for month in ('2026-11', '2026-12', '2027-01', '2028-02'):
            self.assertEqual({1}, self.app.excluded_duty_ids_for_month(month))
            self.assertEqual({10}, self.app.excluded_driver_ids_for_month(month))
            self.assertEqual({30: {'morning': 3, 'afternoon': 4}}, self.app.fixed_duties_for_month(month))
        self.assertEqual({10}, self.app.excluded_driver_ids_for_date_range('2027-01-01', '2027-02-28'))

    def test_migration_is_idempotent_and_preserves_plans_and_old_settings(self):
        before = self.app.conn.execute('SELECT * FROM planned_schedule').fetchall()
        self.activate()
        self.activate()
        self.assertEqual(before, self.app.conn.execute('SELECT * FROM planned_schedule').fetchall())
        self.assertEqual(1, self.app.conn.execute("SELECT count(*) FROM monthly_excluded_drivers WHERE month_key='2026-12' AND driver_id=20").fetchone()[0])
        self.assertEqual(1, self.app.conn.execute("SELECT count(*) FROM app_meta").fetchone()[0])

    def test_removed_global_legacy_exclusion_does_not_return_on_restart(self):
        self.app.conn.execute('INSERT INTO excluded_duties VALUES (7)')
        self.app.conn.commit()
        self.activate()
        self.assertEqual({1, 7}, self.app.excluded_duty_ids_for_month('2027-01'))
        self.app.conn.execute("DELETE FROM monthly_excluded_duties WHERE month_key='2026-11' AND duty_id=7")
        self.app.conn.commit()
        self.activate()
        self.assertEqual({1}, self.app.excluded_duty_ids_for_month('2027-01'))

    def test_november_shift_anchor_keeps_rotation_and_ignores_old_other_months(self):
        self.activate()
        self.assertEqual({30: 'morning'}, self.app.driver_month_start_shift_effective_map('2026-11'))
        self.assertEqual({30: 'afternoon'}, self.app.driver_month_start_shift_effective_map('2026-12'))
        self.assertEqual({30: 'morning'}, self.app.driver_month_start_shift_effective_map('2027-01'))
        self.assertEqual({30: 'afternoon'}, self.app.driver_month_start_shift_map('2026-12'))

    def test_edit_in_december_updates_shared_anchor_and_auto_clears_it(self):
        self.activate()
        self.app.driver_month_start_shift_target_month_key = lambda **kwargs: '2026-12'
        self.app.auto_status_label = Mock()
        self.app.load_driver_month_start_shift_tree = Mock()
        self.app.load_driver_month_start_shift_preference = Mock()
        self.app.set_driver_month_start_shift_preference_for_driver(30, 'morning')
        self.assertEqual('morning', self.app.driver_month_start_shift_effective_map('2026-12')[30])
        self.assertEqual('afternoon', self.app.driver_month_start_shift_effective_map('2027-01')[30])
        self.app.set_driver_month_start_shift_preference_for_driver(30, 'auto')
        self.assertEqual({}, self.app.driver_month_start_shift_effective_map('2027-01'))

    def test_edit_future_driver_exclusion_is_global_without_touching_plans(self):
        self.activate()
        self.app.target_month_key = lambda: '2027-02'
        self.app.selected_driver_ids_from_exclusion_tree = lambda: [10]
        self.app.load_monthly_excluded_driver_ids = Mock()
        self.app.refresh_all = Mock()
        self.app.auto_status_label = Mock()
        self.app.driver_exclusion_status_label = Mock()
        self.app.set_selected_drivers_month_exclusion(False)
        self.assertEqual(set(), self.app.excluded_driver_ids_for_month('2027-03'))
        self.assertEqual(2, self.app.conn.execute('SELECT count(*) FROM planned_schedule').fetchone()[0])

    def test_first_activation_backs_up_before_migration_only_once(self):
        with tempfile.TemporaryDirectory() as directory:
            self.app.db_path = Path(directory) / 'planner.sqlite'
            self.activate()
            self.activate()
            backups = list((Path(directory) / 'backups').glob('before_global_settings_*.sqlite'))
            self.assertEqual(1, len(backups))
            backup = sqlite3.connect(backups[0])
            try:
                self.assertEqual(2, backup.execute('SELECT count(*) FROM planned_schedule').fetchone()[0])
                self.assertEqual(0, backup.execute('SELECT count(*) FROM app_meta').fetchone()[0])
            finally:
                backup.close()

    def test_fixed_duty_saved_in_future_month_updates_global_assignment(self):
        self.activate()
        self.app.target_month_key = lambda: '2027-02'
        self.app.fixed_driver_var = Mock(get=lambda: 'driver')
        self.app.fixed_duty_var = Mock(get=lambda: 'first')
        self.app.fixed_duty2_var = Mock(get=lambda: 'second')
        self.app.resolve_driver_id_from_text = lambda *args, **kwargs: 30
        self.app.resolve_duty_id_from_text = lambda text: 8 if text == 'first' else 9
        self.app.refresh_all = Mock()
        self.app.auto_status_label = Mock()
        self.app.save_fixed_driver_duty()
        self.assertEqual({30: {'morning': 8, 'afternoon': 9}}, self.app.fixed_duties_for_month('2027-03'))


if __name__ == '__main__':
    unittest.main()
