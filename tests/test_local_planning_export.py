import hashlib
from contextlib import closing
import importlib.util
import json
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import unittest
import uuid


SCRIPT = Path(__file__).resolve().parents[1] / "tools" / "export_local_planning.py"
spec = importlib.util.spec_from_file_location("local_planning_export", SCRIPT)
exporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(exporter)


class LocalPlanningExportTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.database = Path(self.directory.name) / "planner.sqlite"
        with closing(sqlite3.connect(self.database)) as connection:
            connection.executescript("""
                CREATE TABLE drivers (id INTEGER, name TEXT, employee_no TEXT);
                CREATE TABLE duties (id INTEGER, code TEXT, work_hours REAL);
                CREATE TABLE planned_schedule (id INTEGER, driver_id INTEGER, duty_id INTEGER,
                    plan_date TEXT, entry_source TEXT, note TEXT);
                CREATE TABLE driver_day_plan_exclusions (driver_id INTEGER, no_sundays INTEGER);
                CREATE TABLE planned_schedule_backup (id INTEGER);
                CREATE TABLE Users (secret TEXT);
                INSERT INTO drivers VALUES (1,'Łukasiewicz','012');
                INSERT INTO duties VALUES (2,'RN',8.5);
                INSERT INTO planned_schedule VALUES (3,1,2,'2026-10-31','manual','ręcznie');
                INSERT INTO driver_day_plan_exclusions VALUES (1,1);
                INSERT INTO planned_schedule_backup VALUES (999);
                INSERT INTO Users VALUES ('not-for-export');
            """)

    def test_export_does_not_modify_database(self):
        before = hashlib.sha256(self.database.read_bytes()).digest()
        exporter.export_snapshot(self.database)
        self.assertEqual(before, hashlib.sha256(self.database.read_bytes()).digest())

    def test_preserves_manual_entries_unicode_and_extra_settings(self):
        snapshot = exporter.export_snapshot(self.database)
        self.assertEqual('Łukasiewicz', snapshot['tables']['drivers'][0]['name'])
        self.assertEqual('012', snapshot['tables']['drivers'][0]['employee_no'])
        self.assertEqual('manual', snapshot['tables']['planned_schedule'][0]['entry_source'])
        self.assertEqual(1, snapshot['tables']['driver_day_plan_exclusions'][0]['no_sundays'])
        self.assertEqual(8.5, snapshot['tables']['duties'][0]['work_hours'])
        json.dumps(snapshot)

    def test_excludes_backups_and_nonplanning_tables(self):
        tables = exporter.export_snapshot(self.database)['tables']
        self.assertNotIn('planned_schedule_backup', tables)
        self.assertNotIn('Users', tables)

    def test_source_identity_stable_and_override_supports_moving_database(self):
        first = exporter.export_snapshot(self.database)
        second = exporter.export_snapshot(self.database)
        self.assertEqual(first['sourceId'], second['sourceId'])
        source_id = uuid.uuid4()
        self.assertEqual(str(source_id), exporter.export_snapshot(self.database, source_id)['sourceId'])
        self.assertTrue(first['exportedAtUtc'].endswith('Z'))

    def test_missing_database_is_not_created(self):
        missing = Path(self.directory.name) / 'missing.sqlite'
        with self.assertRaises(FileNotFoundError):
            exporter.export_snapshot(missing)
        self.assertFalse(missing.exists())

    def test_invalid_schema_is_rejected(self):
        with closing(sqlite3.connect(self.database)) as connection:
            connection.execute('DROP TABLE planned_schedule')
        with self.assertRaises(ValueError):
            exporter.export_snapshot(self.database)

    def test_output_cannot_overwrite_master_database(self):
        before = hashlib.sha256(self.database.read_bytes()).digest()
        result = subprocess.run([sys.executable, str(SCRIPT), '--database', str(self.database),
                                 '--output', str(self.database)], capture_output=True, text=True)
        self.assertNotEqual(0, result.returncode)
        self.assertEqual(before, hashlib.sha256(self.database.read_bytes()).digest())

    def test_cli_creates_readable_snapshot(self):
        output = Path(self.directory.name) / 'snapshot.json'
        subprocess.run([sys.executable, str(SCRIPT), '--database', str(self.database), '--output', str(output)],
                       check=True, capture_output=True)
        snapshot = json.loads(output.read_text(encoding='utf-8'))
        self.assertEqual(1, snapshot['schemaVersion'])
        self.assertEqual(1, len(snapshot['tables']['planned_schedule']))


if __name__ == '__main__':
    unittest.main()
