import sqlite3
import unittest
from pathlib import Path


SOURCE = Path(__file__).resolve().parents[1] / "planowanie_sluzb_tkinter_v450.py"


def _auto_plan_source() -> str:
    text = SOURCE.read_text(encoding="utf-8")
    start = text.index("    def auto_plan_all_drivers(self) -> None:")
    next_def = text.find("\n    def ", start + 1)
    if next_def == -1:
        next_def = len(text)
    return text[start:next_def]


class AutoPlanUniqueTransactionGuardTests(unittest.TestCase):
    def test_auto_plan_final_save_deduplicates_and_upserts_driver_day(self) -> None:
        src = _auto_plan_source()
        self.assertIn("deduped_index_by_key", src)
        self.assertIn("auto_plan_driver_day_unique_conflict", src)
        self.assertIn("ON CONFLICT(plan_date, driver_id) DO UPDATE", src)
        self.assertIn("WHERE COALESCE(NULLIF(TRIM(planned_schedule.entry_source), ''), 'manual')='auto'", src)
        self.assertIn("DUPLIKAT_KIEROWCA_DZIEN", src)

    def test_auto_plan_does_not_commit_reset_before_generation_save(self) -> None:
        src = _auto_plan_source()
        begin_pos = src.index('self.conn.execute("BEGIN IMMEDIATE")')
        save_pos = src.index("final_reset_stats = self.reset_month_before_replan")
        transactional_setup = src[begin_pos:save_pos]
        self.assertNotIn("commit=True", transactional_setup)
        self.assertNotIn("self.conn.commit()", transactional_setup)
        self.assertIn("self.clear_manual_entry_buffers_before_auto_plan(start, end, month_key, commit=False)", transactional_setup)
        self.assertIn("self.force_cold_start_month_context(start, end, month_key, commit=False)", transactional_setup)

    def test_auto_plan_has_rollback_path_for_generation_errors(self) -> None:
        src = _auto_plan_source()
        self.assertIn("self.conn.rollback()", src)
        self.assertIn("auto_plan_all_drivers", src)

    def test_sqlite_upsert_replaces_only_auto_entry_on_driver_day_conflict(self) -> None:
        conn = sqlite3.connect(":memory:")
        conn.execute(
            """
            CREATE TABLE planned_schedule(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                plan_date TEXT NOT NULL,
                driver_id INTEGER NOT NULL,
                duty_id INTEGER NOT NULL,
                note TEXT,
                entry_source TEXT,
                car_id INTEGER,
                pair_no TEXT,
                updated_at TEXT,
                UNIQUE(plan_date, driver_id)
            )
            """
        )
        upsert_sql = """
            INSERT INTO planned_schedule(plan_date, driver_id, duty_id, note, entry_source, car_id, pair_no)
            VALUES (?, ?, ?, ?, 'auto', ?, ?)
            ON CONFLICT(plan_date, driver_id) DO UPDATE SET
                duty_id=excluded.duty_id,
                note=excluded.note,
                entry_source='auto',
                car_id=excluded.car_id,
                pair_no=excluded.pair_no,
                updated_at=CURRENT_TIMESTAMP
            WHERE COALESCE(NULLIF(TRIM(planned_schedule.entry_source), ''), 'manual')='auto'
        """
        conn.execute(upsert_sql, ("2026-08-10", 44, 100, "pierwszy auto", None, None))
        conn.execute(upsert_sql, ("2026-08-10", 44, 15, "zamiana auto", None, None))
        row = conn.execute("SELECT duty_id, note, entry_source FROM planned_schedule WHERE plan_date=? AND driver_id=?", ("2026-08-10", 44)).fetchone()
        self.assertEqual((15, "zamiana auto", "auto"), row)

        conn.execute("DELETE FROM planned_schedule")
        conn.execute(
            "INSERT INTO planned_schedule(plan_date, driver_id, duty_id, note, entry_source) VALUES (?, ?, ?, ?, 'manual')",
            ("2026-08-11", 44, 99, "reczny"),
        )
        cur = conn.execute(upsert_sql, ("2026-08-11", 44, 15, "auto nie moze nadpisac", None, None))
        self.assertEqual(0, cur.rowcount)
        row = conn.execute("SELECT duty_id, note, entry_source FROM planned_schedule WHERE plan_date=? AND driver_id=?", ("2026-08-11", 44)).fetchone()
        self.assertEqual((99, "reczny", "manual"), row)

    def test_sqlite_transaction_rolls_back_generated_plan_after_error(self) -> None:
        conn = sqlite3.connect(":memory:")
        conn.execute(
            """
            CREATE TABLE planned_schedule(
                plan_date TEXT NOT NULL,
                driver_id INTEGER NOT NULL,
                duty_id INTEGER NOT NULL,
                entry_source TEXT,
                UNIQUE(plan_date, driver_id)
            )
            """
        )
        conn.execute("INSERT INTO planned_schedule VALUES (?, ?, ?, 'auto')", ("2026-08-01", 1, 10))
        conn.commit()
        try:
            conn.execute("BEGIN IMMEDIATE")
            conn.execute("DELETE FROM planned_schedule WHERE entry_source='auto'")
            conn.execute("INSERT INTO planned_schedule VALUES (?, ?, ?, 'auto')", ("2026-08-01", 1, 11))
            raise RuntimeError("forced planning failure")
        except RuntimeError:
            conn.rollback()
        row = conn.execute("SELECT plan_date, driver_id, duty_id, entry_source FROM planned_schedule").fetchone()
        self.assertEqual(("2026-08-01", 1, 10, "auto"), row)

    def test_auto_plan_rn_candidates_prefer_second_shift_with_full_block_fallback(self) -> None:
        src = _auto_plan_source()
        self.assertIn("def rn_driver_has_second_shift_for_date", src)
        self.assertIn("effective_preferred_shift_for_date(int(driver_id), day_obj) == \"afternoon\"", src)
        self.assertIn("if not rn_driver_has_second_shift_for_date(driver_id, target_date_obj):", src)
        self.assertIn("if not allow_shift_mismatch and not rn_driver_has_second_shift_for_block(driver_id, block):", src)
        self.assertIn("allow_shift_mismatch=True", src)

    def test_auto_plan_final_rescue_can_sacrifice_other_day_low_priority_for_high_priority(self) -> None:
        src = _auto_plan_source()
        self.assertIn("def final_rescue_high_priority_by_sacrificing_other_day_low_priority", src)
        self.assertIn("source_duty_id not in shortage_skip_duty_ids", src)
        self.assertIn("WG zamiast niskiego priorytetu", src)
        self.assertIn("if target_day_off_entry is None:", src)
        self.assertIn("source_date == target_date", src)
        self.assertIn("pelna obsada wysokiego priorytetu przed ciagloscia tygodniowa", src)
        self.assertIn("def find_auto_low_priority_entry", src)
        self.assertIn("kosztem sluzby niskiego priorytetu", src)
        self.assertGreaterEqual(src.count("final_rescue_high_priority_by_sacrificing_other_day_low_priority("), 4)


    def test_auto_plan_rechecks_rbh_after_final_technical_wg(self) -> None:
        src = _auto_plan_source()
        final_wg_pos = src.index('v428_force_fill_all_visible_empty_cells("koncowe uzupelnienie pustych komorek po stabilizacji")')
        final_guard_pos = src.index("for _v455_post_wg_rbh_pass in range(1, 5):")
        final_topup_pos = src.index("v429_topup_rbh_from_technical_day_offs(", final_guard_pos)
        final_diagnostics_pos = src.index("drivers_below_nominal_rbh.clear()", final_topup_pos)
        final_guard = src[final_guard_pos:final_diagnostics_pos]

        self.assertLess(final_wg_pos, final_guard_pos)
        self.assertLess(final_guard_pos, final_topup_pos)
        self.assertIn("if int(_v455_changes or 0) <= 0:", final_guard)
        self.assertIn("v428_force_fill_all_visible_empty_cells(", final_guard)
        self.assertNotIn("for _v455_post_wg_rbh_pass in range(0):", src)

if __name__ == "__main__":
    unittest.main()
