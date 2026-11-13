import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { inflateSync } from "node:zlib";
import { getMobileNavigation, matchesNavigationPath } from "../src/components/mobile/mobileNavigationModel.ts";
import { createAppIcon } from "../scripts/appIcons.ts";

const paths = ["/", "/alerts", "/drivers", "/vehicles", "/planning", "/card-reader", "/reports", "/companies", "/account"];
const items = paths.map(to => ({ to, label: to, icon: "dashboard" }));

test("mobile has four primary links, no planning or card reader", () => {
    const navigation = getMobileNavigation(items);
    assert.deepEqual(navigation.primary.map(item => item.to), ["/", "/alerts", "/drivers", "/vehicles"]);
    assert.ok(![...navigation.primary, ...navigation.more].some(item => ["/planning", "/card-reader"].includes(item.to)));
    assert.ok(navigation.more.some(item => item.to === "/reports"));
});

test("navigation does not restore companies hidden by company permissions", () => {
    const navigation = getMobileNavigation(items.filter(item => item.to !== "/companies"));
    assert.ok(!navigation.more.some(item => item.to === "/companies"));
});

test("navigation selection handles nested routes without prefix collisions", () => {
    assert.ok(matchesNavigationPath("/drivers/123", "/drivers"));
    assert.ok(!matchesNavigationPath("/drivers-old", "/drivers"));
    assert.ok(!matchesNavigationPath("/drivers", "/"));
});

test("planning route is guarded before rendering the lazy page", () => {
    const app = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
    assert.match(app, /path="planning" element=\{<DesktopOnly><PlanningPage\s*\/><\/DesktopOnly>\}/);
    assert.match(app, /lazy\(\(\) => import\("\.\/pages\/PlanningPage"\)\)/);
});

test("PWA manifest launches online application and has generated PNG icons", () => {
    const manifest = JSON.parse(readFileSync(new URL("../public/manifest.webmanifest", import.meta.url), "utf8"));
    assert.equal(manifest.display, "standalone");
    assert.equal(manifest.start_url, "/");
    for (const size of [192, 512]) {
        const png = Buffer.from(createAppIcon(size));
        assert.deepEqual([...png.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
        assert.equal(png.readUInt32BE(16), size);
        assert.equal(png.readUInt32BE(20), size);
        const idat = png.indexOf(Buffer.from("IDAT"));
        const length = png.readUInt32BE(idat - 4);
        assert.equal(inflateSync(png.subarray(idat + 4, idat + 4 + length)).length, size * (size * 4 + 1));
    }
});
