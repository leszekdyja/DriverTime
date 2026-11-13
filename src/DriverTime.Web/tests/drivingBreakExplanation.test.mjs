import test from "node:test";
import assert from "node:assert/strict";
import { drivingBreakExplanation } from "../src/utils/drivingBreakExplanation.ts";

test("explains one minute as recorded overrun, not missing break", () => {
    const result = drivingBreakExplanation({ code: "CONTINUOUS_DRIVING_BREAK", excessMinutes: 1,
        businessDetails: { continuousDrivingMinutes: 271, drivingLimitMinutes: 270 } });
    assert.ok(result.lines.some((line) => line.includes("4 godz. 31 min")));
    assert.ok(result.lines.some((line) => line.includes("nie długość brakującej przerwy")));
    assert.ok(result.note.includes("nie ponowne przeliczenie"));
});

test("uses detailed analysis consistently and formats Polish local time", () => {
    const result = drivingBreakExplanation({ code: "CONTINUOUS_DRIVING_BREAK", excessMinutes: 1,
        ruleAnalysis: { continuousDrivingMinutes: 277, drivingLimitMinutes: 270, exceededMinutes: 7,
            violationDetectedAtUtc: "2026-09-25T11:13:00Z", segments: [], isEstimated: false } });
    assert.ok(result.lines.some((line) => line.includes("7 min")));
    assert.ok(result.lines.some((line) => line.includes("13:13")));
});

test("does not invent metrics or breaks when missing", () => {
    const result = drivingBreakExplanation({ code: "CONTINUOUS_DRIVING_BREAK" });
    assert.ok(result.lines.some((line) => line.includes("nie zawiera pełnego")));
    assert.equal(result.breaks.length, 0);
    assert.equal(drivingBreakExplanation({ code: "DAILY_REST" }), null);
});

test("uncertain availability is clearly marked as requiring verification", () => {
    const result = drivingBreakExplanation({ code: "CONTINUOUS_DRIVING_BREAK", metadata: { requiresVerification: "true" } });
    assert.equal(result.requiresVerification, true);
    assert.ok(result.lines[0].includes("nie potwierdza naruszenia"));
});
