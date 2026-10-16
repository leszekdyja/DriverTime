import ast
import textwrap
import unittest
from datetime import date, datetime
from pathlib import Path
from types import SimpleNamespace


SOURCE = Path(__file__).resolve().parents[1] / 'planowanie_sluzb_tkinter_v450.py'


class WeekendLowPriorityReclaimTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        tree = ast.parse(SOURCE.read_text(encoding='utf-8'))
        names = {'duty_can_yield_to_high_priority', 'duty_is_mandatory_for_shortage_audit',
                 'final_rescue_high_priority_by_sacrificing_other_day_low_priority'}
        cls.helpers = [node for node in ast.walk(tree)
                       if isinstance(node, ast.FunctionDef) and node.name in names]

    def scenario(self, legal=True, manual=False, dated_requirement=False, target_empty=False, low_code=80):
        low = ('2026-11-01', 1, low_code, 'Automatycznie', None, None)
        off = ('2026-11-06', 1, 9, 'Automatycznie', None, None)
        entries = [low] + ([] if target_empty else [off])
        missing = [('2026-11-06', 12, '12', 'Brak kierowcy')]
        hours = {1: 160.0}
        rest_checks = []
        duties = {low_code: {'code': str(low_code)}, 12: {'code': '12'}, 9: {'code': 'WG'}}

        def remove(entry, required_counted, work_counted):
            entries.remove(entry)
            if work_counted:
                hours[1] -= 8

        def restore(entry, required_counted, work_counted):
            entries.append(entry)
            if work_counted:
                hours[1] += 8

        def add(day, driver, duty, notes, counted):
            entries.append((day, driver, duty, '; '.join(notes), None, None))
            if duty != 9:
                hours[1] += 8

        def rest(*args, **kwargs):
            rest_checks.append(list(entries))
            return legal

        namespace = {
            'date': date, 'datetime': datetime,
            'rn_duty_ids': {99}, 'reserve_duty_id_set': {7, 8},
            'shortage_skip_duty_ids': set(), 'weekend_shortage_optional_duty_ids': {80, 86, 139},
            'unfilled_duties': missing, 'entries': entries, 'planned_context_entries': entries,
            'available_drivers_all': {1}, 'duty_rows_by_id': duties,
            'manual_locked_driver_dates': {('2026-11-01', 1)} if manual else set(),
            'duty_driver_exclusions': {}, 'driver_month_work_hours': hours,
            'monthly_norm_hours': 160, 'pattern_days': 30,
            'weekly_duty_driver': {}, 'weekly_driver_duty': {},
            'driver_month_work_count': {1: 0}, 'driver_week_work_count': {(1, 1): 0},
            'skipped_weekend_shortage_optional_duties': [],
            'shortage_rescue_block_details': [], 'AUTO_NOTE': 'Automatycznie',
            'self': SimpleNamespace(
                is_planning_weekend_or_holiday=lambda day: day.weekday() >= 5,
                is_day_off_duty=lambda duty: duty['code'] == 'WG',
                duty_id_allowed_on_date=lambda *args: True,
                standard_service_rbh_hours=lambda duty: 8.0,
                week_index_in_month=lambda day: 1,
            ),
            'auto_plan_pulse': lambda *args: None,
            'rebuild_unfilled_required_services_before_save': lambda: None,
            'exact_weekend_override_count': lambda *args: 1 if dated_requirement else 0,
            'unfilled_duty_blocks_nominal_topup': lambda item: True,
            'entry_duty': lambda entry: duties[entry[2]],
            'duty_sort_key': lambda duty: duty,
            'day_off_duty_id_for_date': lambda day: 9,
            'is_driver_blocked_by_day_exclusion': lambda *args: False,
            'would_make_consecutive_weekend': lambda *args: False,
            'is_hard_weekend_override_entry': lambda entry: dated_requirement,
            'temporarily_remove_auto_entry': remove, 'restore_auto_entry': restore,
            'driver_has_required_rest': rest,
            'driver_rest_block_reason': lambda *args, **kwargs: 'Brak odpoczynku',
            'add_entry': add,
            'existing_driver_day_context_entry': lambda day, driver: next(
                (entry for entry in entries if entry[0] == day and entry[1] == driver), None),
            'prune_resolved_unfilled_duties': lambda: None,
        }
        code = ('def create():\n'
                '    shortage_replanned_from_12_15 = 0\n'
                '    shortage_replanned_from_other_days = 0\n')
        code += '\n'.join(textwrap.indent(ast.unparse(node), '    ') for node in self.helpers)
        code += '\n    return (final_rescue_high_priority_by_sacrificing_other_day_low_priority, duty_is_mandatory_for_shortage_audit)\n'
        exec(compile(code, str(SOURCE), 'exec'), namespace)
        rescue, mandatory = namespace['create']()
        namespace['mandatory'] = mandatory
        count = rescue('test', max_passes=1)
        return count, entries, missing, hours, rest_checks, namespace

    def test_weekend_80_can_fill_friday_high_priority_even_without_skip_codes(self):
        count, entries, missing, hours, checks, ns = self.scenario()
        self.assertEqual(1, count)
        self.assertEqual([], missing)
        self.assertEqual(160.0, hours[1])
        self.assertEqual({('2026-11-01', 9), ('2026-11-06', 12)}, {(e[0], e[2]) for e in entries})
        self.assertEqual([[]], checks)  # Validate the projected plan after removing both old entries.
        self.assertEqual([('2026-11-01', '80')], ns['skipped_weekend_shortage_optional_duties'])

    def test_failed_rest_check_restores_both_entries_and_hours(self):
        count, entries, missing, hours, checks, ns = self.scenario(legal=False)
        self.assertEqual(0, count)
        self.assertEqual({80, 9}, {e[2] for e in entries})
        self.assertEqual(160.0, hours[1])
        self.assertEqual(1, len(missing))
        self.assertEqual([], ns['skipped_weekend_shortage_optional_duties'])

    def test_manual_and_explicit_weekend_requirements_remain_protected(self):
        for options in ({'manual': True}, {'dated_requirement': True}):
            with self.subTest(options=options):
                count, entries, _, _, checks, _ = self.scenario(**options)
                self.assertEqual(0, count)
                self.assertEqual({80, 9}, {e[2] for e in entries})
                self.assertEqual([], checks)

    def test_empty_target_cell_can_also_receive_freed_driver(self):
        count, entries, _, _, _, _ = self.scenario(target_empty=True)
        self.assertEqual(1, count)
        self.assertIn(('2026-11-06', 12), {(e[0], e[2]) for e in entries})

    def test_weekend_86_can_also_yield_to_weekday_service(self):
        count, entries, _, _, _, ns = self.scenario(low_code=86)
        self.assertEqual(1, count)
        self.assertNotIn(86, {e[2] for e in entries})
        self.assertEqual([('2026-11-01', '86')], ns['skipped_weekend_shortage_optional_duties'])

    def test_weekend_low_priority_is_deferred_but_weekday_and_rn_stay_high(self):
        _, _, _, _, _, ns = self.scenario()
        mandatory = ns['mandatory']
        for code in (80, 86, 139):
            self.assertFalse(mandatory(code, date(2026, 11, 7)))
            self.assertTrue(mandatory(code, date(2026, 11, 6)))
        self.assertTrue(mandatory(99, date(2026, 11, 7)))
        self.assertTrue(mandatory(12, date(2026, 11, 7)))

    def test_explicit_required_weekend_80_is_not_deferred(self):
        _, _, _, _, _, ns = self.scenario(dated_requirement=True)
        self.assertTrue(ns['mandatory'](80, date(2026, 11, 7)))
