import test from "node:test";
import assert from "node:assert/strict";
import { cardExpiryAlertDrivers } from "../src/services/cardExpiryAlertDrivers.ts";

test("confirmed retired cards cannot produce dashboard expiry alerts", () => {
    const drivers = [{ id: "expired-old", name: "Hendrykowski" }, { id: "current", name: "Hendrykowski" },
        { id: "expired-unreplaced", name: "Other" }];
    const active = [{ driverId: "current" }, { driverId: "expired-unreplaced" }];
    assert.deepEqual(cardExpiryAlertDrivers(drivers, active), [drivers[1], drivers[2]]);
    assert.equal(drivers.length, 3);
});

test("download API failure does not suppress unrelated expiry alerts", () => {
    const drivers = [{ id: "expired" }];
    assert.deepEqual(cardExpiryAlertDrivers(drivers, null), drivers);
});

test("a successful empty active-card response is distinct from a request failure", () => {
    assert.deepEqual(cardExpiryAlertDrivers([{ id: "old" }], []), []);
});
