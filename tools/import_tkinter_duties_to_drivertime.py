#!/usr/bin/env python3
"""Import duties embedded in the Tkinter planner into DriverTime planning tables.

The script has two modes:
- always writes a SQL script that can be piped into PostgreSQL;
- with --execute, tries to run the same SQL directly when a PostgreSQL Python
  driver is available.
"""

from __future__ import annotations

import argparse
import base64
import json
import re
import sqlite3
import sys
import tempfile
from dataclasses import dataclass
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any
from uuid import UUID


SOURCE_FILE_NAME = "tkinter:sluzby_v30.sqlite"


@dataclass(frozen=True)
class DutyRow:
    source_id: int
    duty_number: str
    name: str
    duty_type: str | None
    capacity: str | None
    shift: str | None
    line: str | None
    start_time: str | None
    end_time: str | None
    total_minutes: int | None
    work_minutes: int | None
    break_minutes: int | None
    distance_km: Decimal | None
    notes: str
    line_codes: tuple[str, ...]


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Importuje sluzby z pliku Tkintera do tabel planowania DriverTime."
    )
    parser.add_argument("--tkinter-file", default="planowanie_sluzb_tkinter_v450.py")
    parser.add_argument("--sql-output", default="tmp/import_tkinter_duties.sql")
    parser.add_argument("--company-id", default=None, help="Opcjonalnie: import tylko do jednej firmy. Bez tego importuje do wszystkich firm.")
    parser.add_argument("--connection-string", default=None)
    parser.add_argument("--execute", action="store_true", help="Wykonaj import bezposrednio, jesli dostepny jest psycopg/psycopg2.")
    args = parser.parse_args()

    repo_root = Path.cwd()
    tkinter_file = (repo_root / args.tkinter_file).resolve()
    sql_output = (repo_root / args.sql_output).resolve()

    duties = read_tkinter_duties(tkinter_file)
    sql = build_sql(duties, parse_company_id(args.company_id))

    sql_output.parent.mkdir(parents=True, exist_ok=True)
    sql_output.write_text(sql, encoding="utf-8")

    print(f"Znaleziono sluzby w Tkinterze: {len(duties)}")
    print(f"Zapisano SQL: {sql_output}")

    if not args.execute:
        print("Nie uruchomiono importu, bo nie podano --execute.")
        return 0

    connection_string = args.connection_string or read_default_connection_string(repo_root)
    if not connection_string:
        print("Brak connection string. SQL zostal wygenerowany, ale nie wykonany.", file=sys.stderr)
        return 2

    return execute_sql_if_possible(sql, connection_string)


def read_tkinter_duties(tkinter_file: Path) -> list[DutyRow]:
    text = tkinter_file.read_text(encoding="utf-8")
    match = re.search(r'EMBEDDED_DB_B64\s*=\s*"""(.*?)"""', text, re.S)
    if not match:
        raise RuntimeError(f"Nie znaleziono EMBEDDED_DB_B64 w pliku {tkinter_file}")

    db_bytes = base64.b64decode("".join(match.group(1).split()).encode("ascii"))
    temp_db = Path(tempfile.gettempdir()) / "sluzby_v30_from_tkinter.sqlite"
    temp_db.write_bytes(db_bytes)

    connection = sqlite3.connect(temp_db)
    connection.row_factory = sqlite3.Row
    try:
        rows = connection.execute(
            """
            SELECT *
            FROM duties
            WHERE COALESCE(TRIM(code), '') <> ''
            ORDER BY CAST(code AS INTEGER), code
            """
        ).fetchall()
        return [map_duty_row(row) for row in rows]
    finally:
        connection.close()


def map_duty_row(row: sqlite3.Row) -> DutyRow:
    values = dict(row)
    duty_number = clean_text(values.get("code")) or str(values["id"])
    name = clean_text(values.get("name")) or f"Sluzba {duty_number}"
    line = clean_text(values.get("line"))
    start_time = normalize_time(values.get("start_time"))
    end_time = normalize_time(values.get("end_time"))
    total_minutes = parse_duration(values.get("total_time")) or minutes_between(start_time, end_time)
    work_minutes = parse_duration(values.get("work_hours")) or 8 * 60
    break_minutes = parse_duration(values.get("break_duration"))

    if duty_number.upper() == "RN":
        start_time = "20:20:00"
        end_time = "07:20:00"
        total_minutes = minutes_between(start_time, end_time)
        work_minutes = 8 * 60
        break_minutes = total_minutes - work_minutes if total_minutes is not None else break_minutes

    distance_km = parse_decimal(values.get("km_total"))
    duty_type = clean_text(values.get("duty_type"))
    shift = clean_text(values.get("shift"))
    capacity = clean_text(values.get("capacity"))

    notes_parts = [
        f"Typ: {duty_type}" if duty_type else None,
        f"Zmiana: {shift}" if shift else None,
        f"Pojemnosc: {capacity}" if capacity else None,
        f"Zakres pracy: {clean_text(values.get('work_range'))}" if clean_text(values.get("work_range")) else None,
        f"Przerwa: {clean_text(values.get('break_range'))}" if clean_text(values.get("break_range")) else None,
        f"Km dojazd: {clean_text(values.get('km_access'))}" if clean_text(values.get("km_access")) else None,
        f"Km platne: {clean_text(values.get('km_paid'))}" if clean_text(values.get("km_paid")) else None,
        "Planowanie: robocze={0}, sobota={1}, niedziela={2}".format(
            yes_no(values.get("can_plan_weekdays")),
            yes_no(values.get("can_plan_saturday")),
            yes_no(values.get("can_plan_sunday")),
        ),
        f"Wystapienia: {values.get('occurrences')}" if values.get("occurrences") is not None else None,
        "RN w DriverTime: 20:20-07:20, 8h pracy" if duty_number.upper() == "RN" else None,
        f"Tkinter id: {values.get('id')}",
    ]
    notes = "; ".join(part for part in notes_parts if part)

    return DutyRow(
        source_id=int(values["id"]),
        duty_number=duty_number[:50],
        name=name[:200],
        duty_type=duty_type,
        capacity=capacity,
        shift=shift,
        line=line,
        start_time=start_time,
        end_time=end_time,
        total_minutes=total_minutes,
        work_minutes=work_minutes,
        break_minutes=break_minutes,
        distance_km=distance_km,
        notes=notes[:4000],
        line_codes=tuple(split_lines(line)),
    )


def build_sql(duties: list[DutyRow], company_id: UUID | None) -> str:
    now = datetime.now(timezone.utc).isoformat()
    rows_sql = ",\n".join(format_temp_row(duty) for duty in duties)
    lines_sql = ",\n".join(
        format_temp_line_row(duty.duty_number, line_code)
        for duty in duties
        for line_code in duty.line_codes
    )

    company_filter_sql = (
        f"WHERE \"Id\" = '{company_id}'::uuid"
        if company_id
        else ""
    )

    return f"""-- Generated by tools/import_tkinter_duties_to_drivertime.py
-- Source: {SOURCE_FILE_NAME}
-- Generated at: {now}

BEGIN;

CREATE TEMP TABLE tmp_tkinter_duties (
    source_id integer NOT NULL,
    duty_number text NOT NULL,
    name text NOT NULL,
    vehicle_requirement text NULL,
    start_time time NULL,
    end_time time NULL,
    total_duration_minutes integer NULL,
    work_minutes integer NULL,
    break_minutes integer NULL,
    distance_km numeric(10,2) NULL,
    notes text NULL,
    source_file_name text NOT NULL
) ON COMMIT DROP;

INSERT INTO tmp_tkinter_duties (
    source_id,
    duty_number,
    name,
    vehicle_requirement,
    start_time,
    end_time,
    total_duration_minutes,
    work_minutes,
    break_minutes,
    distance_km,
    notes,
    source_file_name
) VALUES
{rows_sql};

CREATE TEMP TABLE tmp_tkinter_duty_lines (
    duty_number text NOT NULL,
    line_code text NOT NULL
) ON COMMIT DROP;

INSERT INTO tmp_tkinter_duty_lines (duty_number, line_code) VALUES
{lines_sql if lines_sql else "('','')"};

DO $$
DECLARE
    company_row record;
    duty_row record;
    duty_id uuid;
    processed_company_count integer := 0;
BEGIN
    FOR company_row IN
        SELECT "Id" AS company_id
        FROM "Companies"
        {company_filter_sql}
        ORDER BY "CreatedAt"
    LOOP
        processed_company_count := processed_company_count + 1;

        FOR duty_row IN SELECT * FROM tmp_tkinter_duties LOOP
            duty_id := NULL;

            SELECT "Id" INTO duty_id
            FROM "PlanningDuties"
            WHERE "CompanyId" = company_row.company_id
              AND "DutyNumber" = duty_row.duty_number
            ORDER BY CASE WHEN "SourceFileName" = '{SOURCE_FILE_NAME}' THEN 0 ELSE 1 END, "CreatedAt"
            LIMIT 1;

        IF duty_id IS NULL THEN
            duty_id := gen_random_uuid();

            INSERT INTO "PlanningDuties" (
                "Id",
                "CompanyId",
                "DutyNumber",
                "Name",
                "ValidFrom",
                "VehicleRequirement",
                "StartTime",
                "EndTime",
                "TotalDurationMinutes",
                "WorkMinutes",
                "BreakMinutes",
                "DrivingMinutes",
                "DistanceKm",
                "Notes",
                "SourceFileName",
                "CreatedAtUtc",
                "UpdatedAtUtc",
                "CreatedAt"
            ) VALUES (
                duty_id,
                company_row.company_id,
                duty_row.duty_number,
                duty_row.name,
                NULL,
                duty_row.vehicle_requirement,
                duty_row.start_time,
                duty_row.end_time,
                duty_row.total_duration_minutes,
                duty_row.work_minutes,
                duty_row.break_minutes,
                NULL,
                duty_row.distance_km,
                duty_row.notes,
                duty_row.source_file_name,
                now(),
                NULL,
                now()
            );
        ELSE
            UPDATE "PlanningDuties"
            SET "Name" = duty_row.name,
                "VehicleRequirement" = duty_row.vehicle_requirement,
                "StartTime" = duty_row.start_time,
                "EndTime" = duty_row.end_time,
                "TotalDurationMinutes" = duty_row.total_duration_minutes,
                "WorkMinutes" = duty_row.work_minutes,
                "BreakMinutes" = duty_row.break_minutes,
                "DistanceKm" = duty_row.distance_km,
                "Notes" = duty_row.notes,
                "SourceFileName" = duty_row.source_file_name,
                "UpdatedAtUtc" = now()
            WHERE "Id" = duty_id;

            DELETE FROM "PlanningDutyLines"
            WHERE "PlanningDutyId" = duty_id;
        END IF;

            INSERT INTO "PlanningDutyLines" (
                "Id",
                "PlanningDutyId",
                "LineCode",
                "Variant",
                "DistanceKm",
                "CreatedAt"
            )
            SELECT gen_random_uuid(), duty_id, line_code, NULL, NULL, now()
            FROM tmp_tkinter_duty_lines
            WHERE duty_number = duty_row.duty_number
              AND line_code <> '';
        END LOOP;
    END LOOP;

    IF processed_company_count = 0 THEN
        RAISE EXCEPTION 'Brak firm w tabeli Companies albo nie znaleziono firmy podanej przez --company-id.';
    END IF;
END $$;

COMMIT;
"""


def format_temp_row(duty: DutyRow) -> str:
    vehicle_requirement = f"Pojemnosc {duty.capacity}" if duty.capacity else None
    return "(" + ", ".join(
        [
            str(duty.source_id),
            sql_literal(duty.duty_number),
            sql_literal(duty.name),
            sql_literal(vehicle_requirement),
            sql_time(duty.start_time),
            sql_time(duty.end_time),
            sql_int(duty.total_minutes),
            sql_int(duty.work_minutes),
            sql_int(duty.break_minutes),
            sql_decimal(duty.distance_km),
            sql_literal(duty.notes),
            sql_literal(SOURCE_FILE_NAME),
        ]
    ) + ")"


def format_temp_line_row(duty_number: str, line_code: str) -> str:
    return f"({sql_literal(duty_number)}, {sql_literal(line_code[:50])})"


def clean_text(value: Any) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    if not text or text == "-":
        return None
    return text


def normalize_time(value: Any) -> str | None:
    text = clean_text(value)
    if not text:
        return None
    match = re.fullmatch(r"(\d{1,2})[:.](\d{1,2})", text)
    if not match:
        return None
    hour = int(match.group(1))
    minute = int(match.group(2))
    if not (0 <= hour <= 23 and 0 <= minute <= 59):
        return None
    return f"{hour:02d}:{minute:02d}:00"


def parse_duration(value: Any) -> int | None:
    text = clean_text(value)
    if not text:
        return None
    normalized = text.replace(",", ".")
    if re.fullmatch(r"\d+", normalized):
        return int(normalized) * 60
    match = re.fullmatch(r"(\d+)[.:](\d{1,2})", normalized)
    if not match:
        return None
    hours = int(match.group(1))
    minute_text = match.group(2)
    minutes = int(minute_text) if len(minute_text) == 2 else int(minute_text) * 10
    if minutes > 59:
        return None
    return hours * 60 + minutes


def minutes_between(start_time: str | None, end_time: str | None) -> int | None:
    if not start_time or not end_time:
        return None
    start_hour, start_minute, _ = [int(part) for part in start_time.split(":")]
    end_hour, end_minute, _ = [int(part) for part in end_time.split(":")]
    start = start_hour * 60 + start_minute
    end = end_hour * 60 + end_minute
    if end < start:
        end += 24 * 60
    return end - start


def parse_decimal(value: Any) -> Decimal | None:
    text = clean_text(value)
    if not text:
        return None
    try:
        return Decimal(text.replace(",", "."))
    except InvalidOperation:
        return None


def split_lines(value: str | None) -> list[str]:
    if not value:
        return []
    result: list[str] = []
    for part in re.split(r"[/,;+\s]+", value):
        text = part.strip()
        if text and text not in result:
            result.append(text)
    return result


def yes_no(value: Any) -> str:
    return "tak" if str(value).strip() == "1" else "nie"


def sql_literal(value: str | None) -> str:
    if value is None:
        return "NULL"
    return "'" + value.replace("'", "''") + "'"


def sql_time(value: str | None) -> str:
    if value is None:
        return "NULL"
    return f"{sql_literal(value)}::time"


def sql_int(value: int | None) -> str:
    return "NULL" if value is None else str(value)


def sql_decimal(value: Decimal | None) -> str:
    return "NULL" if value is None else str(value)


def parse_company_id(value: str | None) -> UUID | None:
    if not value:
        return None
    return UUID(value)


def read_default_connection_string(repo_root: Path) -> str | None:
    settings_path = repo_root / "src" / "DriverTime.Api" / "appsettings.Development.json"
    if not settings_path.exists():
        return None
    settings = json.loads(settings_path.read_text(encoding="utf-8-sig"))
    connection_strings = settings.get("ConnectionStrings", {})
    return connection_strings.get("DefaultConnection")


def execute_sql_if_possible(sql: str, connection_string: str) -> int:
    psycopg_module = try_import("psycopg")
    if psycopg_module is not None:
        try:
            with psycopg_module.connect(connection_string) as connection:
                with connection.cursor() as cursor:
                    cursor.execute(sql)
            print("Import wykonany bezposrednio przez psycopg.")
            return 0
        except Exception as exc:
            print(f"Nie udalo sie wykonac importu przez psycopg: {exc}", file=sys.stderr)
            return 2

    psycopg2_module = try_import("psycopg2")
    if psycopg2_module is not None:
        try:
            connection = psycopg2_module.connect(connection_string)
            try:
                with connection.cursor() as cursor:
                    cursor.execute(sql)
                connection.commit()
            finally:
                connection.close()
            print("Import wykonany bezposrednio przez psycopg2.")
            return 0
        except Exception as exc:
            print(f"Nie udalo sie wykonac importu przez psycopg2: {exc}", file=sys.stderr)
            return 2

    print(
        "Brak sterownika PostgreSQL dla Pythona. SQL zostal wygenerowany i mozna go uruchomic przez psql.",
        file=sys.stderr,
    )
    return 2


def try_import(module_name: str) -> Any | None:
    try:
        return __import__(module_name)
    except ImportError:
        return None


if __name__ == "__main__":
    raise SystemExit(main())





