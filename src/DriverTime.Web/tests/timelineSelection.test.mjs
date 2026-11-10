import test from "node:test";
import assert from "node:assert/strict";
import { summarizeTimelineSelection } from "../src/components/timeline/timelineSelection.ts";

const at = (minute) => Date.UTC(2026, 8, 25, 0, minute);
const segment = (start, end, type = "DRIVING") => ({ start: new Date(at(start)), end: new Date(at(end)), type, driverName: "Kierowca", vehicleRegistration: "DTR12345" });

test("clips both ends and supports reversed selection", () => {
    const segments = [segment(0, 30), segment(30, 60, "REST")];
    const result = summarizeTimelineSelection(segments, at(45), at(15));
    assert.equal(result.durationSeconds, 1800);
    assert.equal(result.seconds.DRIVING, 900);
    assert.equal(result.seconds.REST, 900);
    assert.deepEqual(result.vehicles, ["DTR12345"]);
});

test("gaps are unknown, not rest", () => {
    const result = summarizeTimelineSelection([segment(0, 10)], at(0), at(30));
    assert.equal(result.seconds.UNKNOWN, 1200);
    assert.equal(result.seconds.REST, 0);
});

test("duplicates never double count and conflicting types are flagged", () => {
    const result = summarizeTimelineSelection([segment(0, 30), segment(0, 30), segment(15, 30, "WORK")], at(0), at(30));
    assert.equal(result.seconds.DRIVING, 900);
    assert.equal(result.seconds.UNKNOWN, 900);
    assert.equal(result.conflictingSeconds, 900);
    assert.equal(Object.values(result.seconds).reduce((a,b) => a+b, 0), 1800);
});

test("range crosses midnight without losing minutes", () => {
    const result = summarizeTimelineSelection([segment(1430, 1450, "REST")], at(1435), at(1445));
    assert.equal(result.seconds.REST, 600);
    assert.equal(result.durationSeconds, 600);
});

test("empty and zero ranges remain safe", () => {
    assert.equal(summarizeTimelineSelection([], at(0), at(60)).seconds.UNKNOWN, 3600);
    assert.equal(summarizeTimelineSelection([], at(0), at(0)).durationSeconds, 0);
});
