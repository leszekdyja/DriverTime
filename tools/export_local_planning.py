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


def export_snapshot(database_path, source_id=None):
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
        return {
            "schemaVersion": 1,
            "sourceId": str(source_id),
            "exportedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
            "tables": tables,
        }
    finally:
        connection.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--database", type=Path, default=Path(os.environ.get("APPDATA", ".")) / "PlanowanieSluzb" / "sluzby_v30.sqlite")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-id", help="Keep this UUID when moving the master database to another computer.")
    arguments = parser.parse_args()
    try:
        snapshot = export_snapshot(arguments.database, arguments.source_id)
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
