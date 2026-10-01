import sqlite3
import unittest
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


SOURCE = Path(__file__).resolve().parents[1] / "planowanie_sluzb_tkinter_v450.py"


class PairShiftSeparationTests(unittest.TestCase):
    def make_planner(self) -> DutyPlannerApp:
        return object.__new__(DutyPlannerApp)

    def test_pair_option_is_persisted(self) -> None:
        planner = self.make_planner()
        planner.conn = sqlite3.connect(":memory:")
        planner.conn.row_factory = sqlite3.Row
        planner.conn.execute(
            """
            CREATE TABLE permanent_driver_pairs(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                pair_no TEXT,
                pair_order INTEGER DEFAULT 0,
                car_id INTEGER NOT NULL UNIQUE,
                driver1_id INTEGER NOT NULL,
                driver2_id INTEGER,
                avoid_same_shift INTEGER DEFAULT 0,
                note TEXT,
                active INTEGER DEFAULT 1,
                updated_at TEXT
            )
            """
        )

        planner.save_permanent_driver_pair(
            "7",
            car_id=70,
            driver1_id=101,
            driver2_id=202,
            note="test",
            avoid_same_shift=True,
        )

        row = planner.conn.execute(
            "SELECT avoid_same_shift FROM permanent_driver_pairs WHERE car_id=70"
        ).fetchone()
        self.assertEqual(1, int(row["avoid_same_shift"]))

    def test_same_shift_is_blocked_only_for_enabled_pair(self) -> None:
        planner = self.make_planner()
        duties = {
            10: {"code": "10", "name": "", "duty_type": "", "shift": "I", "line": "", "start_time": "05:00"},
            14: {"code": "14", "name": "", "duty_type": "", "shift": "I", "line": "", "start_time": "06:00"},
            41: {"code": "41", "name": "", "duty_type": "", "shift": "II", "line": "", "start_time": "14:00"},
        }
        pair_map = {
            101: {"pair_no": "7", "driver1_id": 101, "driver2_id": 202, "avoid_same_shift": True},
            202: {"pair_no": "7", "driver1_id": 101, "driver2_id": 202, "avoid_same_shift": True},
        }
        context = [("2026-09-10", 101, 10, "", 70, "7")]

        conflict = planner.pair_same_shift_conflict_for_context(
            202, 14, "2026-09-10", pair_map, context, duties
        )
        opposite_shift = planner.pair_same_shift_conflict_for_context(
            202, 41, "2026-09-10", pair_map, context, duties
        )
        pair_map[202]["avoid_same_shift"] = False
        disabled = planner.pair_same_shift_conflict_for_context(
            202, 14, "2026-09-10", pair_map, context, duties
        )

        self.assertIsNotNone(conflict)
        self.assertEqual("morning", conflict["shift"])
        self.assertIsNone(opposite_shift)
        self.assertIsNone(disabled)

    def test_third_variant_first_shift_is_not_misread_as_second_shift(self) -> None:
        planner = self.make_planner()
        duties = {
            25: {"code": "32", "name": "", "duty_type": "", "shift": "III zm I", "line": "", "start_time": "04:35"},
            63: {"code": "R", "name": "", "duty_type": "", "shift": "Rezerwa", "line": "", "start_time": "04:40"},
            64: {"code": "R2", "name": "", "duty_type": "", "shift": "Rezerwa popołudniowa", "line": "", "start_time": "14:00"},
        }
        pair_map = {
            39: {"pair_no": "24", "driver1_id": 39, "driver2_id": 40, "avoid_same_shift": True},
            40: {"pair_no": "24", "driver1_id": 39, "driver2_id": 40, "avoid_same_shift": True},
        }
        context = [("2026-09-02", 39, 25, "", 6, "24")]

        self.assertEqual("morning", planner.duty_shift_part(duties[25]))
        self.assertIsNotNone(
            planner.pair_same_shift_conflict_for_context(40, 63, "2026-09-02", pair_map, context, duties)
        )
        self.assertIsNone(
            planner.pair_same_shift_conflict_for_context(40, 64, "2026-09-02", pair_map, context, duties)
        )

    def test_existing_manual_same_shift_pair_is_reported(self) -> None:
        planner = self.make_planner()
        planner.conn = sqlite3.connect(":memory:")
        planner.conn.row_factory = sqlite3.Row
        planner.conn.executescript(
            """
            CREATE TABLE permanent_driver_pairs(
                id INTEGER PRIMARY KEY, pair_no TEXT, driver1_id INTEGER, driver2_id INTEGER,
                avoid_same_shift INTEGER, active INTEGER
            );
            CREATE TABLE drivers(id INTEGER PRIMARY KEY, name TEXT);
            CREATE TABLE duties(
                id INTEGER PRIMARY KEY, code TEXT, name TEXT, duty_type TEXT,
                shift TEXT, line TEXT, start_time TEXT
            );
            CREATE TABLE planned_schedule(
                id INTEGER PRIMARY KEY, plan_date TEXT, driver_id INTEGER, duty_id INTEGER
            );
            INSERT INTO permanent_driver_pairs VALUES (1, '7', 101, 202, 1, 1);
            INSERT INTO drivers VALUES (101, 'KIEROWCA A'), (202, 'KIEROWCA B');
            INSERT INTO duties VALUES
                (10, '10', '', '', 'I', '', '05:00'),
                (14, '14', '', '', 'I', '', '06:00');
            INSERT INTO planned_schedule VALUES
                (1, '2026-09-10', 101, 10),
                (2, '2026-09-10', 202, 14);
            """
        )

        violations = planner.pair_same_shift_violations("2026-09-01", "2026-09-30")

        self.assertEqual(1, len(violations))
        self.assertEqual("PARA_TA_SAMA_ZMIANA", violations[0]["rule_code"])
        self.assertIn("KIEROWCA A (10)", violations[0]["details"])
        self.assertIn("KIEROWCA B (14)", violations[0]["details"])

    def test_database_migration_and_central_generator_guard_are_present(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("ALTER TABLE permanent_driver_pairs ADD COLUMN avoid_same_shift", source)
        self.assertIn('text="Nie planuj pary na jednej zmianie"', source)

        generator_start = source.index("            def pair_same_shift_block_reason")
        generator_end = source.index("            def effective_preferred_shift", generator_start)
        generator_source = source[generator_start:generator_end]
        self.assertIn("pair_same_shift_conflict_for_context", generator_source)
        self.assertIn("if pair_same_shift_block_reason", generator_source)

        add_start = source.index("            def add_entry(")
        add_end = source.index("            def buffer_weekday_duty_for_required_rest", add_start)
        self.assertIn("pair_same_shift_block_reason", source[add_start:add_end])


if __name__ == "__main__":
    unittest.main()
