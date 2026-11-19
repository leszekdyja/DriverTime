import ast
import unittest
from datetime import date
from types import SimpleNamespace
from pathlib import Path

class StrictWeeklyShiftTests(unittest.TestCase):
    def test_later_weeks_block_numeric_duty_with_wrong_shift_before_rescue_rules(self):
        source=(Path(__file__).resolve().parents[1]/"planowanie_sluzb_tkinter_v450.py").read_text(encoding="utf-8")
        node=next(n for n in ast.walk(ast.parse(source)) if isinstance(n,ast.FunctionDef) and n.name=="direct_month_start_shift_block_reason")
        env=dict(date=date, duty_rows_by_id={1:{"code":"12"}},
                 self=SimpleNamespace(normalize_code=lambda s:s,preferred_shift_label=lambda s:s),
                 duty_matches_week_shift=lambda *args:False,
                 effective_preferred_shift_for_date=lambda *args:"afternoon")
        exec(compile(ast.fix_missing_locations(ast.Module(body=[node],type_ignores=[])),"shift_rule","exec"),env)
        for day in (2,9,16,23,30):
            reason=env["direct_month_start_shift_block_reason"](5,1,date(2026,11,day))
            self.assertTrue(reason.startswith("kolejność zmian kierowcy:"),reason)

    def test_final_save_cannot_bypass_weekly_shift_block(self):
        source=(Path(__file__).resolve().parents[1]/"planowanie_sluzb_tkinter_v450.py").read_text(encoding="utf-8")
        self.assertIn('or start_shift_reason.startswith("kolejność zmian kierowcy:")',source)
        self.assertNotIn("full_coverage_allow_shift_change = True",source)
