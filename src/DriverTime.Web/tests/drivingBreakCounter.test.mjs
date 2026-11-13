import test from "node:test";
import assert from "node:assert/strict";
import { drivingSecondsSinceBreak } from "../src/components/timeline/drivingBreakCounter.ts";

const at = minute => new Date(Date.UTC(2026, 9, 1, 0, minute)).toISOString();
const activity = (type, start, end, isCrew = null, cardSlot = null) => ({
    activityType: type, startUtc: at(start), endUtc: at(end), isCrew, cardSlot
});
const end = Date.parse(at(2000));

test("only crew co-driver availability of 45 minutes resets driving", () => {
    for (const [crew, slot, expected] of [[true, 2, 60], [true, 1, 330], [false, 2, 330], [null, null, 330]]) {
        assert.equal(drivingSecondsSinceBreak([activity("DRIVING", 0, 270),
            activity("AVAILABILITY", 270, 315, crew, slot), activity("DRIVING", 315, 375)], end), expected * 60);
    }
});

test("availability split into 15+30 with driving between is not a 45-minute block", () => {
    assert.equal(drivingSecondsSinceBreak([activity("DRIVING", 0, 240), activity("AVAILABILITY", 240, 255, true, 2),
        activity("DRIVING", 255, 285), activity("AVAILABILITY", 285, 315, true, 2), activity("DRIVING", 315, 375)], end), 330 * 60);
});

test("ordinary rest can be split 15+30", () => {
    assert.equal(drivingSecondsSinceBreak([activity("DRIVING", 0, 240), activity("REST", 240, 255),
        activity("DRIVING", 255, 285), activity("REST", 285, 315), activity("DRIVING", 315, 375)], end), 60 * 60);
});

test("co-driver availability spanning midnight is continuous", () => {
    assert.equal(drivingSecondsSinceBreak([activity("DRIVING", 1140, 1410), activity("AVAILABILITY", 1410, 1440, true, 2),
        activity("AVAILABILITY", 1440, 1455, true, 2), activity("DRIVING", 1455, 1515)], end), 60 * 60);
});
