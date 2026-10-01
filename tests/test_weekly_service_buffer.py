import unittest
from pathlib import Path

from planowanie_sluzb_tkinter_v450 import DutyPlannerApp


SOURCE = Path(__file__).resolve().parents[1] / "planowanie_sluzb_tkinter_v450.py"


class WeeklyServiceBufferTests(unittest.TestCase):
    def test_rbh_rank_prefers_the_candidate_that_closes_the_shortage(self) -> None:
        exact_fit = DutyPlannerApp.weekly_rest_buffer_rbh_rank(
            160.0, 152.0, 8.0, False, False, 19, 20
        )
        leaves_shortage = DutyPlannerApp.weekly_rest_buffer_rbh_rank(
            160.0, 144.0, 8.0, True, False, 18, 10
        )

        self.assertLess(exact_fit, leaves_shortage)

    def test_rbh_rank_uses_same_service_as_a_tie_breaker(self) -> None:
        same_service = DutyPlannerApp.weekly_rest_buffer_rbh_rank(
            160.0, 144.0, 8.0, True, False, 18, 20
        )
        other_service = DutyPlannerApp.weekly_rest_buffer_rbh_rank(
            160.0, 144.0, 8.0, False, False, 18, 10
        )

        self.assertLess(same_service, other_service)

    def test_required_rest_buffer_runs_before_global_fallback_and_reserves(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_start = source.index("    def auto_plan_all_drivers(self) -> None:")
        auto_source = source[auto_start:]

        buffer_call = auto_source.index(
            'auto_plan_pulse("bufor wymaganych przerw: uzupełnianie RBH"'
        )
        global_call = auto_source.index(
            'auto_plan_pulse("globalne dopasowanie braków: weekendy przed tygodniem"'
        )
        reserve_call = auto_source.index(
            "fill_remaining_empty_workday_cells_with_reserve_or_wg(allow_reserve_fill=True"
        )

        self.assertLess(buffer_call, global_call)
        self.assertLess(global_call, reserve_call)

    def test_final_same_day_audit_runs_after_reserve_topup(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_source = source[source.index("    def auto_plan_all_drivers(self) -> None:"):]

        reserve_topup = auto_source.index("for _v455_post_wg_rbh_pass in range(1, 5):")
        final_reclaim = auto_source.index('"v495 końcowy audyt służby przed R/R2"')
        diagnostics = auto_source.index("drivers_below_nominal_rbh.clear()", final_reclaim)

        self.assertLess(reserve_topup, final_reclaim)
        self.assertLess(final_reclaim, diagnostics)
        reclaim_source = auto_source[final_reclaim:diagnostics]
        self.assertIn("max_passes=2", reclaim_source)
        self.assertIn("include_low_priority_missing=True", reclaim_source)
        self.assertNotIn("fill_remaining_nominal_rbh_after_rescue()", reclaim_source)

    def test_same_day_audit_replaces_reserve_with_real_high_priority_service(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def reclaim_lower_priority_entries_for_high_priority_shortages")
        end = source.index('            reclaim_lower_priority_entries_for_high_priority_shortages("v391', start)
        reclaim_source = source[start:end]

        self.assertIn("if source_duty_id in reserve_duty_id_set:", reclaim_source)
        self.assertIn("if str(entry[0]) != target_date:", reclaim_source)
        self.assertIn("if not driver_has_required_rest", reclaim_source)
        self.assertIn("and not include_low_priority_missing", reclaim_source)

    def test_final_cross_day_audit_moves_reserve_before_reporting_shortage(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_source = source[source.index("    def auto_plan_all_drivers(self) -> None:"):]

        reserve_topup = auto_source.index("for _v455_post_wg_rbh_pass in range(1, 5):")
        cross_day_call = auto_source.index(
            'final_swap_same_driver_reserve_to_wg_for_missing_service(\n'
            '                "v497 końcowy audyt służb przed rezerwami z innych dni"'
        )
        same_day_audit = auto_source.index('"v495 końcowy audyt służby przed R/R2"')
        diagnostics = auto_source.index("drivers_below_nominal_rbh.clear()", same_day_audit)

        self.assertLess(reserve_topup, cross_day_call)
        self.assertLess(cross_day_call, same_day_audit)
        self.assertLess(same_day_audit, diagnostics)

        helper_start = auto_source.index(
            "            def final_swap_same_driver_reserve_to_wg_for_missing_service"
        )
        helper_end = auto_source.index("            # v423:", helper_start)
        helper_source = auto_source[helper_start:helper_end]
        self.assertIn("same_driver_other_day_reserves", helper_source)
        self.assertIn("for driver_id_raw in available_drivers_all:", helper_source)
        self.assertIn("if target_entries:", helper_source)
        self.assertIn("target_wg_entry = None", helper_source)
        self.assertIn("temporarily_remove_auto_entry(source_reserve_entry", helper_source)
        self.assertIn("if not driver_has_required_rest(", helper_source)
        self.assertIn("missing_duty_id,", helper_source)
        self.assertIn("allow_inherited_month_start_shift_mismatch=True", helper_source)
        self.assertIn("R/R2 z dnia {source_date} zamienione na WG", helper_source)

    def test_month_end_chain_uses_true_empty_cells_before_final_diagnostics(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_source = source[source.index("    def auto_plan_all_drivers(self) -> None:"):]
        helper_start = auto_source.index(
            "            def final_chain_rescue_missing_services_from_shifted_day_off"
        )
        helper_end = auto_source.index(
            "            def final_swap_same_driver_reserve_to_wg_for_missing_service",
            helper_start,
        )
        helper_source = auto_source[helper_start:helper_end]

        self.assertIn("for target_driver_id_raw in available_drivers_all:", helper_source)
        self.assertIn("target_has_entry = driver_has_any_entry", helper_source)
        self.assertIn("if target_has_entry and target_day_off_entry is None:", helper_source)
        self.assertIn("target_kind_score = 0 if target_day_off_entry is None else 1", helper_source)
        self.assertIn("if target_day_off_entry is not None:", helper_source)
        self.assertIn("include_low_priority: bool = False", helper_source)

        high_call = auto_source.index(
            '"v499 pusty dzień -> brak wysokiego priorytetu"'
        )
        low_call = auto_source.index(
            '"v499 pusty dzień -> pozostały brak"',
            high_call,
        )
        diagnostics = auto_source.index("drivers_below_nominal_rbh.clear()", low_call)
        self.assertLess(high_call, low_call)
        self.assertLess(low_call, diagnostics)

    def test_emergency_reserve_replacement_keeps_explicit_start_shift_hard(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_source = source[source.index("    def auto_plan_all_drivers(self) -> None:"):]
        rest_start = auto_source.index("            def driver_has_required_rest(")
        rest_end = auto_source.index("            def driver_rest_block_reason", rest_start)
        rest_source = auto_source[rest_start:rest_end]

        self.assertIn("allow_inherited_month_start_shift_mismatch: bool = False", rest_source)
        self.assertIn("has_explicit_start_shift = int(driver_id) in direct_month_start_shifts", rest_source)
        self.assertIn(
            "if has_explicit_start_shift or not allow_inherited_month_start_shift_mismatch:",
            rest_source,
        )

        final_audit = auto_source.index('"v495 końcowy audyt służby przed R/R2"')
        diagnostics = auto_source.index("drivers_below_nominal_rbh.clear()", final_audit)
        self.assertIn(
            "allow_inherited_month_start_shift_mismatch=True",
            auto_source[final_audit:diagnostics],
        )

        add_start = auto_source.index("            def add_entry(")
        add_end = auto_source.index("            def buffer_weekday_duty_for_required_rest", add_start)
        add_source = auto_source[add_start:add_end]
        self.assertIn("allow_inherited_month_start_shift_mismatch: bool = False", add_source)
        self.assertIn("has_explicit_start_shift = int(driver_id) in direct_month_start_shifts", add_source)

    def test_excluded_reserves_are_hard_blocked_in_every_auto_path(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        auto_source = source[source.index("    def auto_plan_all_drivers(self) -> None:"):]

        self.assertIn("auto_plan_allowed_reserve_duty_ids = {", auto_source)
        self.assertIn("if int(duty_id) not in excluded_duty_ids", auto_source)
        self.assertIn(
            "auto_plan_disabled_reserve_duty_ids = reserve_duty_id_set - auto_plan_allowed_reserve_duty_ids",
            auto_source,
        )
        self.assertIn(
            "if duty_id in auto_plan_disabled_reserve_duty_ids and not exact_weekend_override:\n                    return",
            auto_source,
        )
        self.assertIn("if int(reserve_id) not in auto_plan_allowed_reserve_duty_ids:", auto_source)
        self.assertIn("disabled_auto_duty_ids_v268.update(auto_plan_disabled_reserve_duty_ids)", auto_source)

    def test_buffer_excludes_original_owner_and_rechecks_legal_rest(self) -> None:
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("            def assign_weekly_rest_buffer_by_rbh_shortage")
        end = source.index(
            "            def global_match_missing_services_to_empty_cells_weekend_first",
            start,
        )
        buffer_source = source[start:end]

        self.assertIn("if int(driver_id) == int(owner_driver_id):", buffer_source)
        self.assertIn("monthly_norm_hours - driver_month_work_hours", buffer_source)
        self.assertIn("return driver_has_required_rest(", buffer_source)
        self.assertIn("allow_monthly_overtime=False", buffer_source)
        self.assertNotIn("allow_technical_wg=True", buffer_source)


if __name__ == "__main__":
    unittest.main()
