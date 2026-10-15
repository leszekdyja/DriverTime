#!/usr/bin/env python3
"""Adapter for vehicle-unit DDD files.

The low-level decoder is the MIT-licensed DDDTachograph Reader pinned by the
API container. This module keeps DriverTime's stable, small JSON contract and
does not expose the decoder's multi-megabyte diagnostic tree to the backend.
"""

from __future__ import annotations

import os
import sys
from pathlib import Path
from typing import Any


def parse_vehicle_unit_ddd(path: Path) -> dict[str, Any]:
    parser_root = Path(os.environ.get("DDDTACHO_PARSER_ROOT", "/opt/ddd-tachograph-reader"))
    if not parser_root.exists():
        raise RuntimeError(
            "Parser danych tachografu VU nie jest zainstalowany. "
            "Ustaw DDDTACHO_PARSER_ROOT na katalog DDDTachograph Reader."
        )

    root_text = str(parser_root)
    if root_text not in sys.path:
        sys.path.insert(0, root_text)

    from app.engine import TachoParser  # type: ignore[import-not-found]

    parsed = TachoParser(str(path)).parse()
    parse_error = (parsed.get("metadata") or {}).get("parse_error")
    if parse_error:
        message = parse_error.get("message") if isinstance(parse_error, dict) else str(parse_error)
        raise RuntimeError(f"Nie udało się odczytać danych tachografu: {message}")

    metadata = parsed.get("metadata") or {}
    vehicle = parsed.get("vehicle") or {}
    parser_name = "DriverTime VU adapter / DDDTachograph Reader"
    parser_version = str(metadata.get("app_version") or "unknown")
    downloaded_at = str(metadata.get("current_datetime") or "")
    return {
        "driver": {},
        "parser_name": parser_name,
        "parser_version": parser_version,
        "file_type": "vehicle_unit",
        "card_read_date": downloaded_at,
        "parser": {
            "name": parser_name,
            "version": parser_version,
            "file_type": "vehicle_unit",
            "card_read_date": downloaded_at,
        },
        "vehicle_unit": {
            "generation": str(metadata.get("generation") or ""),
            "integrity_check": str(metadata.get("integrity_check") or ""),
            "coverage_percent": metadata.get("coverage_pct"),
            "vin": str(vehicle.get("vin") or ""),
            "registration_number": str(vehicle.get("plate") or ""),
            "registration_country": str(vehicle.get("registration_nation") or ""),
            "downloaded_at": downloaded_at,
            "daily_activities": parsed.get("activities") or [],
            "driver_sessions": parsed.get("card_iw_records") or [],
            "inserted_drivers": parsed.get("inserted_drivers") or [],
            "places": parsed.get("places") or [],
            "events": parsed.get("events") or [],
            "faults": parsed.get("faults") or [],
            "calibrations": parsed.get("calibrations") or [],
            "company_locks": parsed.get("company_locks") or [],
            "overspeeding_events": parsed.get("overspeeding_events") or [],
        },
        "activities": [],
        "vehicle_uses": [],
        "country_code_entries": [],
    }
