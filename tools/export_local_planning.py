"""Export a consistent, read-only planning snapshot for DriverTime's sync preview."""

import argparse
import datetime as dt
import json
import os
from pathlib import Path
import socket
import sqlite3
import uuid


PLANNING_TABLES = (
    "drivers", "duties", "planned_schedule", "cars", "driver_pairs",
    "permanent_driver_pairs", "rn_driver_pairs", "duty_blocks",
    "duty_driver_exclusions", "driver_day_plan_exclusions",
    "driver_month_shift_preferences", "excluded_duties",
    "monthly_excluded_duties", "monthly_excluded_drivers",
    "fixed_driver_duties", "weekend_duty_overrides", "original_schedule",
)


def export_snapshot(database_path, source_id=None, mappings_path=None):
    database_path = Path(database_path).resolve(strict=True)
    source_id = uuid.UUID(str(source_id)) if source_id else uuid.uuid5(
        uuid.NAMESPACE_URL,
        "drivertime-local-planner:" + socket.gethostname().lower() + ":" + str(database_path).casefold(),
    )
    if source_id.int == 0:
        raise ValueError("Source ID must not be empty.")
    connection = sqlite3.connect(database_path.as_uri() + "?mode=ro", uri=True)
    connection.row_factory = sqlite3.Row
    try:
        connection.execute("PRAGMA query_only=ON")
        connection.execute("BEGIN")
        available = {row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        required = {"drivers", "duties", "planned_schedule"}
        if not required.issubset(available):
            raise ValueError("This is not a supported local planning database.")
        tables = {}
        for table in PLANNING_TABLES:
            if table in available:
                tables[table] = [dict(row) for row in connection.execute('SELECT * FROM "' + table + '"')]
        snapshot = {
            "schemaVersion": 1,
            "sourceId": str(source_id),
            "exportedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
            "tables": tables,
        }
        if "app_meta" in available:
            policy = connection.execute(
                "SELECT value FROM app_meta WHERE key='global_planning_settings_v511'"
            ).fetchone()
            if policy:
                snapshot["settingsPolicy"] = {"mode": "global", "sourceMonth": policy[0]}
    finally:
        connection.close()
    profile_path = Path(mappings_path) if mappings_path else database_path.parent / "local_planning_mappings.json"
    if mappings_path and not profile_path.is_file():
        raise ValueError("The selected saved mappings file does not exist.")
    if profile_path.exists():
        attach_remembered_mappings(snapshot, json.loads(profile_path.read_text(encoding="utf-8")))
    return snapshot


def attach_remembered_mappings(snapshot, profile):
    if not isinstance(profile, dict) or profile.get("sourceId") != snapshot["sourceId"]:
        raise ValueError("Saved mappings belong to another database source. Check the source ID.")
    scope = profile.get("operatingCompanyId")
    if scope is not None:
        if not isinstance(scope, str) or uuid.UUID(scope).int == 0:
            raise ValueError("Invalid saved destination company ID.")
    prepared = {"sourceId": snapshot["sourceId"], "exportedAtUtc": snapshot["exportedAtUtc"],
                "operatingCompanyId": scope, "mode": "remembered", "warnings": []}
    for mapping_key, keys_key, table, identity_fields in (
        ("driverMappings", "driverKeys", "drivers", ("name", "employee_no")),
        ("dutyMappings", "dutyKeys", "duties", ("code",)),
    ):
        mappings, identities = profile.get(mapping_key), profile.get(keys_key)
        if not isinstance(mappings, dict) or not isinstance(identities, dict):
            raise ValueError("Saved mappings have no source identity keys.")
        rows = {str(row["id"]): row for row in snapshot["tables"][table]}
        accepted = {}
        for source_id, target_id in mappings.items():
            if not isinstance(source_id, str) or not source_id.isdigit() or int(source_id) <= 0:
                raise ValueError("Invalid saved source record ID.")
            if not isinstance(target_id, str) or uuid.UUID(target_id).int == 0:
                raise ValueError("Invalid saved VPS record ID.")
            if source_id not in rows:
                continue
            original = identities.get(source_id)
            if not isinstance(original, dict) or any(original.get(field, "") != rows[source_id].get(field, "") for field in identity_fields):
                prepared["warnings"].append(f"{table} id {source_id}: source identity changed; mapping was omitted.")
                continue
            accepted[source_id] = str(uuid.UUID(target_id))
        if table == "drivers" and len(set(accepted.values())) != len(accepted):
            raise ValueError("Two source drivers point to the same VPS driver.")
        prepared[mapping_key] = accepted
    snapshot["preparedMappings"] = prepared


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--database", type=Path, default=Path(os.environ.get("APPDATA", ".")) / "PlanowanieSluzb" / "sluzby_v30.sqlite")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-id", help="Keep this UUID when moving the master database to another computer.")
    parser.add_argument("--mappings", type=Path, help="Saved mappings; defaults to local_planning_mappings.json next to the SQLite database.")
    arguments = parser.parse_args()
    try:
        snapshot = export_snapshot(arguments.database, arguments.source_id, arguments.mappings)
        # Exclusive creation prevents overwriting either a previous export or the master database.
        with arguments.output.open("x", encoding="utf-8") as output:
            json.dump(snapshot, output, ensure_ascii=False, separators=(",", ":"))
    except (OSError, ValueError, sqlite3.Error) as error:
        parser.exit(1, "Export failed: " + str(error) + "\n")
    print(json.dumps({
        "output": str(arguments.output.resolve()),
        "sourceId": snapshot["sourceId"],
        "entries": len(snapshot["tables"]["planned_schedule"]),
        "readOnlySource": True,
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
