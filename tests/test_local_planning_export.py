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

    def save_profile(self):
        profile = {
            'sourceId': exporter.export_snapshot(self.database)['sourceId'],
            'operatingCompanyId': None,
            'driverMappings': {'1': str(uuid.uuid4())},
            'dutyMappings': {'2': str(uuid.uuid4())},
            'driverKeys': {'1': {'name': 'Łukasiewicz', 'employee_no': '012'}},
            'dutyKeys': {'2': {'code': 'RN'}},
        }
        path = self.database.parent / 'local_planning_mappings.json'
        path.write_text(json.dumps(profile), encoding='utf-8')
        return profile, path

    def test_remembers_mappings_for_each_export_without_writing_sources(self):
        profile, path = self.save_profile()
        before = (self.database.read_bytes(), path.read_bytes())
        for _ in range(2):
            snapshot = exporter.export_snapshot(self.database)
            prepared = snapshot['preparedMappings']
            self.assertEqual(profile['driverMappings'], prepared['driverMappings'])
            self.assertEqual(profile['dutyMappings'], prepared['dutyMappings'])
            self.assertEqual(snapshot['exportedAtUtc'], prepared['exportedAtUtc'])
            self.assertEqual('remembered', prepared['mode'])
            self.assertEqual([], prepared['warnings'])
        self.assertEqual(before, (self.database.read_bytes(), path.read_bytes()))

    def test_different_source_profile_is_rejected(self):
        self.save_profile()
        with self.assertRaises(ValueError):
            exporter.export_snapshot(self.database, uuid.uuid4())

    def test_changed_identity_is_omitted_and_new_rows_are_not_guessed(self):
        self.save_profile()
        with closing(sqlite3.connect(self.database)) as connection:
            connection.execute("UPDATE drivers SET employee_no='013'")
            connection.execute("INSERT INTO drivers VALUES (5,'Nowy','014')")
            connection.execute("UPDATE duties SET code='R'")
            connection.commit()
        prepared = exporter.export_snapshot(self.database)['preparedMappings']
        self.assertEqual({}, prepared['driverMappings'])
        self.assertEqual({}, prepared['dutyMappings'])
        self.assertEqual(2, len(prepared['warnings']))

    def test_duplicate_driver_target_is_rejected_case_insensitively(self):
        profile, path = self.save_profile()
        with closing(sqlite3.connect(self.database)) as connection:
            connection.execute("INSERT INTO drivers VALUES (5,'Nowy','014')")
            connection.commit()
        profile['driverMappings']['5'] = profile['driverMappings']['1'].upper()
        profile['driverKeys']['5'] = {'name': 'Nowy', 'employee_no': '014'}
        path.write_text(json.dumps(profile), encoding='utf-8')
        with self.assertRaises(ValueError):
            exporter.export_snapshot(self.database)

    def test_explicit_missing_profile_is_not_silently_ignored(self):
        with self.assertRaises(ValueError):
            exporter.export_snapshot(self.database, mappings_path=self.database.parent / 'missing.json')

    def test_exports_global_policy_without_unrelated_app_metadata(self):
        with closing(sqlite3.connect(self.database)) as connection:
            connection.executescript("CREATE TABLE app_meta(key TEXT,value TEXT); INSERT INTO app_meta VALUES ('global_planning_settings_v511','2026-11'),('secret','do-not-export');")
        snapshot = exporter.export_snapshot(self.database)
        self.assertEqual({'mode': 'global', 'sourceMonth': '2026-11'}, snapshot['settingsPolicy'])
        self.assertNotIn('do-not-export', json.dumps(snapshot))


if __name__ == '__main__':
    unittest.main()
