import test from "node:test";
import assert from "node:assert/strict";
import { readPreparedLocalPlanningMappings } from "../src/services/preparedLocalPlanningMappings.ts";

function fixture() {
    const file = { sourceId: "source-one", exportedAtUtc: "2026-10-27T10:00:00Z",
        tables: { drivers: [{ id: 1 }, { id: 2 }], duties: [{ id: 3 }] },
        oneTimeMappings: { sourceId: "source-one", exportedAtUtc: "2026-10-27T10:00:00Z", operatingCompanyId: null,
            driverMappings: { 1: "driver-a" }, dutyMappings: { 3: "duty-a" } } };
    const scope = { operatingCompanyId: null, drivers: [{ id: "driver-a" }], duties: [{ id: "duty-a" }] };
    return { file, scope };
}

test("loads mappings for the same snapshot without modifying input", () => {
    const { file, scope } = fixture(); const before = JSON.stringify(file);
    const result = readPreparedLocalPlanningMappings(file, scope);
    assert.deepEqual(result, { driverMappings: { 1: "driver-a" }, dutyMappings: { 3: "duty-a" }, warnings: [] });
    assert.equal(JSON.stringify(file), before);
});
test("rejects another source", () => {
    const { file, scope } = fixture(); file.oneTimeMappings.sourceId = "other";
    assert.throws(() => readPreparedLocalPlanningMappings(file, scope));
});
test("rejects a later export of the same database", () => {
    const { file, scope } = fixture(); file.exportedAtUtc = "2026-10-28T10:00:00Z";
    assert.throws(() => readPreparedLocalPlanningMappings(file, scope));
});
test("rejects another company or a record outside available options", () => {
    const { file, scope } = fixture();
    assert.throws(() => readPreparedLocalPlanningMappings(file, { ...scope, operatingCompanyId: "other-company" }));
    assert.throws(() => readPreparedLocalPlanningMappings(file, { ...scope, drivers: [] }));
    assert.throws(() => readPreparedLocalPlanningMappings(file, { ...scope, duties: [] }));
});
test("rejects unknown source IDs and duplicate driver targets", () => {
    const { file, scope } = fixture(); file.oneTimeMappings.driverMappings[2] = "driver-a";
    assert.throws(() => readPreparedLocalPlanningMappings(file, scope));
    delete file.oneTimeMappings.driverMappings[2]; file.oneTimeMappings.driverMappings[999] = "driver-a";
    assert.throws(() => readPreparedLocalPlanningMappings(file, scope));
});
test("rejects malformed or missing profiles", () => {
    const { file, scope } = fixture();
    assert.throws(() => readPreparedLocalPlanningMappings(null, scope));
    delete file.oneTimeMappings;
    assert.throws(() => readPreparedLocalPlanningMappings(file, scope));
});
test("supports remembered profiles and source identity warnings", () => {
    const { file, scope } = fixture();
    file.preparedMappings = { ...file.oneTimeMappings, mode: "remembered", warnings: ["Source row changed", 123] };
    delete file.oneTimeMappings;
    const result = readPreparedLocalPlanningMappings(file, scope);
    assert.equal(result.driverMappings[1], "driver-a");
    assert.deepEqual(result.warnings, ["Source row changed"]);
});
