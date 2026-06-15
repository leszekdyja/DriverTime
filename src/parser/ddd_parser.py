#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Kierowca Czas App - MVP do analizy czasu pracy/jazdy kierowcy z pliku .ddd.

Ta wersja ma parser W BUDOWANY w aplikacje. Nie wymaga osobnego pliku
`dddparser.exe`, wskazywania parsera ani kompilowania Go. Po zbudowaniu przez
PyInstaller plik `KierowcaCzas.exe` zawiera parser oraz analizator.

Zakres parsera wbudowanego MVP:
- pliki .DDD z karty kierowcy: sekcja DriverActivityData, EF 0x0504,
- odczyt dziennych rekordow aktywności i ActivityChangeInfo,
- eksport pomocniczego JSON obok pliku .ddd.

Zakres do kolejnej wersji:
- pelny parser VU / pojazdu,
- weryfikacja podpisow i certyfikatow,
- rozszerzone raporty kontrolne.

Aplikacja analizuje aktywności, wykrywa podstawowe naruszenia norm 561/2006
i rysuje os czasu aktywności.

Zmiany v12:
- odpoczynek tygodniowy jest zaliczany w dowolnym dniu tygodnia,
- ciagle bloki REST sa laczone z tolerancja 2 minut, zeby podzial dzienny
  albo drobna luka nie rozbijaly odpoczynku tygodniowego,
- blok 24-45h bezposrednio po pelnym odpoczynku tygodniowym nie tworzy juz
  automatycznie skroconego odpoczynku tygodniowego ani dlugu rekompensaty.

Wersja z raportem naruszen: w aplikacji oraz eksporcie HTML/PDF dostepna
jest tabela: data naruszenia, czego dotyczy, kara wg taryfikatora, kod/grupa, waga, okres, wartosc, limit, opis. Dodano analize ciaglosci danych, rekompensat skroconych odpoczynkow tygodniowych, wymaganych terminow rozpoczecia odpoczynkow, profesjonalny widok tabel oraz przeglad wszystkich odpoczynkow z filtrem listy rozwijanej oraz dzienne zestawienie jazdy, przerw i dyspozycyjnośći.
"""

from __future__ import annotations

import calendar
import csv
import datetime as dt
import json
import os
import re
import sys
import tempfile
import textwrap
import webbrowser
import traceback
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Optional

try:
    import tkinter as tk
    from tkinter import filedialog, messagebox, ttk
    import tkinter.font as tkfont
except Exception as exc:  # pragma: no cover
    raise SystemExit(f"Tkinter nie jest dostepny: {exc}")

# Wykresy są rysowane natywnie w Tkinter na Canvas.
# Dzięki temu aplikacja i plik EXE nie wymagają matplotlib ani innych bibliotek graficznych. Dodano analize PL-ek/kodow kraju tylko dla wlozenia i wyjecia karty z tachografu.

APP_TITLE = "Centrum zgodności kierowcy"
PARSER_HELP_TEXT = (
    "Parser jest wbudowany w aplikacje. Nie trzeba wskazywac dddparser.exe."
)

# Kody aktywności tachografu sa mapowane konserwatywnie.
# DRIVING: jazda; WORK: inna praca; AVAILABILITY: dyspozycyjność;
# REST: przerwa/odpoczynek. UNKNOWN nie jest traktowany jako odpoczynek.
ACTIVITY_ALIASES = {
    "driving": "DRIVING",
    "drive": "DRIVING",
    "jazda": "DRIVING",
    "prowadzenie": "DRIVING",
    "carddriveractivitydriving": "DRIVING",
    "work": "WORK",
    "working": "WORK",
    "otherwork": "WORK",
    "other_work": "WORK",
    "inna praca": "WORK",
    "praca": "WORK",
    "carddriveractivitywork": "WORK",
    "availability": "AVAILABILITY",
    "available": "AVAILABILITY",
    "dyspozycyjność": "AVAILABILITY",
    "dyspozycyjność": "AVAILABILITY",
    "poa": "AVAILABILITY",
    "periodofavailability": "AVAILABILITY",
    "rest": "REST",
    "break": "REST",
    "resting": "REST",
    "odpoczynek": "REST",
    "przerwa": "REST",
    "pause": "REST",
    "carddriveractivityrest": "REST",
}

ACTIVITY_LABELS_PL = {
    "DRIVING": "Jazda",
    "WORK": "Inna praca",
    "AVAILABILITY": "Dyspozycyjność",
    "REST": "Przerwa / odpoczynek",
    "UNKNOWN": "Nieznana aktywność",
}

ACTIVITY_ICONS = {
    "DRIVING": "🚚",
    "WORK": "⚒",
    "AVAILABILITY": "⌛",
    "REST": "🛏",
    "UNKNOWN": "❓",
}


def activity_icon(kind: Any) -> str:
    if kind is None:
        return ACTIVITY_ICONS["UNKNOWN"]
    return ACTIVITY_ICONS.get(str(kind), ACTIVITY_ICONS["UNKNOWN"])


def activity_label(kind: Any) -> str:
    """Return a Polish, user-facing name for a tachograph activity code."""
    if kind is None:
        return ACTIVITY_LABELS_PL["UNKNOWN"]
    text = str(kind)
    return ACTIVITY_LABELS_PL.get(text, text)


def activity_label_with_icon(kind: Any) -> str:
    """Return icon + Polish label for GUI views that list activities."""
    return f"{activity_icon(kind)} {activity_label(kind)}"


def strip_activity_parentheses(text: Any) -> str:
    """Remove technical parenthesized activity codes from report labels."""
    cleaned = re.sub(r"\s*\([^)]*\)", "", str(text or "")).strip()
    return cleaned or ACTIVITY_LABELS_PL["UNKNOWN"]


def activity_report_label_with_icon(kind: Any) -> str:
    """Return icon + Polish activity label for reports, without technical codes in parentheses."""
    normalized = normalize_kind(kind)
    icon_kind = normalized if normalized != "UNKNOWN" else kind
    label_kind = normalized if normalized != "UNKNOWN" else kind
    return f"{activity_icon(icon_kind)} {strip_activity_parentheses(activity_label(label_kind))}"


def _safe_date_from_text(text: str) -> Optional[dt.date]:
    """Parse YYYY-MM-DD used by GUI date filters."""
    text = (text or "").strip()
    if not text:
        return None
    try:
        return dt.date.fromisoformat(text)
    except ValueError:
        return None


def _period_overlaps_range(start: dt.datetime, end: dt.datetime, range_start: Optional[dt.datetime], range_end_excl: Optional[dt.datetime]) -> bool:
    """Return True when a datetime period overlaps selected inclusive date range."""
    if range_start and end <= range_start:
        return False
    if range_end_excl and start >= range_end_excl:
        return False
    return True


def _date_in_range(day: dt.date, date_from: Optional[dt.date], date_to: Optional[dt.date]) -> bool:
    if date_from and day < date_from:
        return False
    if date_to and day > date_to:
        return False
    return True

@dataclass(frozen=True)
class Activity:
    start: dt.datetime
    end: dt.datetime
    kind: str
    source: str = ""
    vehicle_registration: str = ""

    @property
    def duration(self) -> dt.timedelta:
        return self.end - self.start


@dataclass(frozen=True)
class VehicleUse:
    start: dt.datetime
    end: dt.datetime
    registration: str
    source: str = ""


@dataclass(frozen=True)
class Violation:
    rule: str
    severity: str
    start: dt.datetime
    end: dt.datetime
    value: str
    limit: str
    description: str

@dataclass(frozen=True)
class TariffInfo:
    amount: str
    code: str
    category: str
    basis: str
    note: str
    amount_zl: Optional[int] = None


@dataclass(frozen=True)
class ContinuityIssue:
    start: dt.datetime
    end: dt.datetime
    duration: dt.timedelta
    kind: str
    impact: str
    recommendation: str


@dataclass(frozen=True)
class CompensationDebt:
    reduced_rest_start: dt.datetime
    reduced_rest_end: dt.datetime
    reduced_rest_duration: dt.timedelta
    shortage: dt.timedelta
    deadline: dt.datetime
    status: str
    compensation_period: str
    continuity_status: str
    recommendation: str


@dataclass(frozen=True)
class RestOverview:
    start: dt.datetime
    end: dt.datetime
    duration: dt.timedelta
    rest_type: str
    status: str
    deadline_or_compensation: str
    note: str


@dataclass(frozen=True)
class DailySummary:
    day: dt.date
    driving: dt.timedelta
    break_rest: dt.timedelta
    availability: dt.timedelta
    work: dt.timedelta
    unknown: dt.timedelta
    total_recorded: dt.timedelta
    first_activity: Optional[dt.datetime]
    last_activity: Optional[dt.datetime]
    entries: int


@dataclass(frozen=True)
class CountryCodeEntry:
    timestamp: dt.datetime
    entry_type: str
    country_code: str
    country_name: str
    status: str
    related_day: str
    note: str
    source: str = ""



# Kody krajow tachografu wg listy JRC/Digital Tachograph.
# PL ma kod alfa "PL" i kod numeryczny 0x28.
NATION_NUMERIC_TO_ALPHA = {
    0x00: "---", 0x01: "A", 0x02: "AL", 0x03: "AND", 0x04: "ARM", 0x05: "AZ",
    0x06: "B", 0x07: "BG", 0x08: "BIH", 0x09: "BY", 0x0A: "CH", 0x0B: "CY",
    0x0C: "CZ", 0x0D: "D", 0x0E: "DK", 0x0F: "E", 0x10: "EST", 0x11: "F",
    0x12: "FIN", 0x13: "FL", 0x14: "FR", 0x15: "UK", 0x16: "GE", 0x17: "GR",
    0x18: "H", 0x19: "HR", 0x1A: "I", 0x1B: "IRL", 0x1C: "IS", 0x1D: "KZ",
    0x1E: "L", 0x1F: "LT", 0x20: "LV", 0x21: "M", 0x22: "MC", 0x23: "MD",
    0x24: "MK", 0x25: "N", 0x26: "NL", 0x27: "P", 0x28: "PL", 0x29: "RO",
    0x2A: "RSM", 0x2B: "RUS", 0x2C: "S", 0x2D: "SK", 0x2E: "SLO", 0x2F: "TM",
    0x30: "TR", 0x31: "UA", 0x32: "V", 0x33: "YU", 0x34: "MNE", 0x35: "SRB",
    0x36: "UZ", 0x37: "TJ", 0x38: "KG", 0x39: "IL", 0xFD: "EC", 0xFE: "EUR", 0xFF: "WLD",
}

COUNTRY_NAMES_PL = {
    "---": "brak informacji", "A": "Austria", "AL": "Albania", "AND": "Andora", "ARM": "Armenia",
    "AZ": "Azerbejdzan", "B": "Belgia", "BG": "Bulgaria", "BIH": "Bosnia i Hercegowina",
    "BY": "Bialorus", "CH": "Szwajcaria", "CY": "Cypr", "CZ": "Czechy", "D": "Niemcy",
    "DK": "Dania", "E": "Hiszpania", "EST": "Estonia", "F": "Francja", "FIN": "Finlandia",
    "FL": "Liechtenstein", "FR": "Wyspy Owcze", "GE": "Gruzja", "GR": "Grecja", "H": "Wegry",
    "HR": "Chorwacja", "I": "Wlochy", "IRL": "Irlandia", "IS": "Islandia", "KZ": "Kazachstan",
    "L": "Luksemburg", "LT": "Litwa", "LV": "Lotwa", "M": "Malta", "MC": "Monako",
    "MD": "Moldawia", "MK": "Macedonia Polnocna", "MNE": "Czarnogora", "N": "Norwegia",
    "NL": "Holandia", "P": "Portugalia", "PL": "Polska", "RO": "Rumunia", "RSM": "San Marino",
    "RUS": "Rosja", "S": "Szwecja", "SK": "Slowacja", "SLO": "Slowenia", "SRB": "Serbia",
    "TJ": "Tadzykistan", "TM": "Turkmenistan", "TR": "Turcja", "UA": "Ukraina", "UK": "Wielka Brytania",
    "UZ": "Uzbekistan", "V": "Watykan", "YU": "Jugoslawia", "EC": "Wspolnota Europejska",
    "EUR": "reszta Europy", "WLD": "reszta swiata",
}

COUNTRY_NAME_TO_ALPHA = {
    "polska": "PL", "poland": "PL", "pl": "PL",
    "niemcy": "D", "germany": "D", "de": "D", "d": "D",
    "czechy": "CZ", "czech republic": "CZ", "cz": "CZ",
    "slowacja": "SK", "slovakia": "SK", "sk": "SK",
    "litwa": "LT", "lithuania": "LT", "lt": "LT",
    "ukraina": "UA", "ukraine": "UA", "ua": "UA",
    "francja": "F", "france": "F", "f": "F",
    "belgia": "B", "belgium": "B", "b": "B",
    "holandia": "NL", "netherlands": "NL", "nl": "NL",
    "hiszpania": "E", "spain": "E", "e": "E",
    "wlochy": "I", "italy": "I", "i": "I",
    "austria": "A", "a": "A",
}

ENTRY_TYPE_LABELS = {
    0: "Rozpoczecie - karta/wpis automatyczny",
    1: "Zakonczenie - wyjecie karty/wpis automatyczny",
    2: "Rozpoczecie - wpis manualny",
    3: "Zakonczenie - wpis manualny",
    4: "Rozpoczecie - przyjete przez tachograf",
    5: "Zakonczenie - przyjete przez tachograf",
}


def normalize_country_code(value: Any) -> str:
    if value is None:
        return "---"
    if isinstance(value, bool):
        return "---"
    if isinstance(value, int):
        return NATION_NUMERIC_TO_ALPHA.get(value, f"0x{value:02X}")
    if isinstance(value, float) and value.is_integer():
        return normalize_country_code(int(value))
    raw = str(value).strip()
    if not raw:
        return "---"
    lowered = raw.lower().strip()
    if lowered in COUNTRY_NAME_TO_ALPHA:
        return COUNTRY_NAME_TO_ALPHA[lowered]
    m = re.fullmatch(r"\(?0x([0-9a-fA-F]{1,2})\)?", raw)
    if m:
        return normalize_country_code(int(m.group(1), 16))
    m = re.fullmatch(r"\(?([0-9A-Fa-f]{2})\)?H", raw)
    if m:
        return normalize_country_code(int(m.group(1), 16))
    if raw.isdigit():
        return normalize_country_code(int(raw))
    upper = raw.upper().replace(" ", "")
    if upper in COUNTRY_NAMES_PL:
        return upper
    # Popularne rozroznienie: ISO "DE" w plikach JSON traktujemy jak tachografowe "D".
    if upper == "DE":
        return "D"
    if upper == "GB":
        return "UK"
    if 1 <= len(upper) <= 3 and upper.isalpha():
        return upper
    return raw


def country_name_pl(code: str) -> str:
    return COUNTRY_NAMES_PL.get(code, "nieznany kraj")


def is_start_country_entry(entry_type: str) -> bool:
    text = entry_type.lower()
    return "rozpoc" in text or "wloz" in text or "włoż" in text or "begin" in text or "start" in text or "insert" in text


def is_end_country_entry(entry_type: str) -> bool:
    text = entry_type.lower()
    return "zakonc" in text or "zako" in text or "wyjec" in text or "wyję" in text or "end" in text or "finish" in text or "withdraw" in text or "remove" in text


def is_card_insert_or_withdraw_country_entry(entry_type: str) -> bool:
    """Return True for country-code records connected with putting in/removing the card.

    This deliberately excludes synthetic daily warnings, border crossings and generic
    location/country records. The PL-ki tab should show only actual country codes
    recorded for card insertion/start and card withdrawal/end events.
    """
    text = entry_type.lower()
    if any(token in text for token in ("brak wpisu", "border", "granica", "location", "lokalizacja")):
        return False
    return is_start_country_entry(entry_type) or is_end_country_entry(entry_type)


def card_country_entry_display_type(entry_type: str) -> str:
    if is_start_country_entry(entry_type):
        return with_manual_entry_icon("Wlozenie karty - kod kraju", entry_type)
    if is_end_country_entry(entry_type):
        return with_manual_entry_icon("Wyjecie karty - kod kraju", entry_type)
    return with_manual_entry_icon(entry_type, entry_type)


def parse_datetime(value: Any) -> Optional[dt.datetime]:
    """Parse many timestamp variants found in tachograph JSON outputs."""
    if value is None:
        return None
    if isinstance(value, dt.datetime):
        return value.replace(tzinfo=None)
    if isinstance(value, (int, float)):
        # Unix seconds or milliseconds.
        try:
            if value > 10_000_000_000:
                value = value / 1000
            return dt.datetime.utcfromtimestamp(value)
        except Exception:
            return None
    if not isinstance(value, str):
        return None
    raw = value.strip()
    if not raw:
        return None

    # Examples: 2026-05-01T12:30:00Z, 2026-05-01 12:30, 20260501123000
    cleaned = raw.replace("Z", "+00:00")
    cleaned = cleaned.replace("UTC", "+00:00")
    for fmt in (
        "%Y-%m-%dT%H:%M:%S%z",
        "%Y-%m-%dT%H:%M:%S.%f%z",
        "%Y-%m-%d %H:%M:%S%z",
        "%Y-%m-%dT%H:%M:%S",
        "%Y-%m-%dT%H:%M:%S.%f",
        "%Y-%m-%d %H:%M:%S",
        "%Y-%m-%d %H:%M",
        "%Y/%m/%d %H:%M:%S",
        "%d.%m.%Y %H:%M:%S",
        "%d.%m.%Y %H:%M",
        "%Y%m%d%H%M%S",
        "%Y-%m-%d",
    ):
        try:
            parsed = dt.datetime.strptime(cleaned, fmt)
            return parsed.astimezone(dt.timezone.utc).replace(tzinfo=None) if parsed.tzinfo else parsed
        except ValueError:
            pass
    try:
        parsed = dt.datetime.fromisoformat(cleaned)
        return parsed.astimezone(dt.timezone.utc).replace(tzinfo=None) if parsed.tzinfo else parsed
    except ValueError:
        return None


def normalize_kind(value: Any) -> str:
    if value is None:
        return "UNKNOWN"
    text = str(value).strip()
    if not text:
        return "UNKNOWN"
    simple = re.sub(r"[^a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ_]+", "", text).lower()
    simple = simple.replace("ą", "a").replace("ć", "c").replace("ę", "e")
    simple = simple.replace("ł", "l").replace("ń", "n").replace("ó", "o")
    simple = simple.replace("ś", "s").replace("ź", "z").replace("ż", "z")
    for key, target in ACTIVITY_ALIASES.items():
        k = key.lower().replace(" ", "").replace("_", "")
        k = k.replace("ą", "a").replace("ć", "c").replace("ę", "e")
        k = k.replace("ł", "l").replace("ń", "n").replace("ó", "o")
        k = k.replace("ś", "s").replace("ź", "z").replace("ż", "z")
        if simple == k or k in simple:
            return target
    # Niektore parsery zwracaja numeryczne kody aktywności.
    # Najczestsza konwencja bywa: 0 rest, 1 availability, 2 work, 3 driving.
    if text in {"0", "00"}:
        return "REST"
    if text in {"1", "01"}:
        return "AVAILABILITY"
    if text in {"2", "02"}:
        return "WORK"
    if text in {"3", "03"}:
        return "DRIVING"
    return "UNKNOWN"


def walk_json(node: Any, path: str = "") -> Iterable[tuple[str, Any]]:
    yield path, node
    if isinstance(node, dict):
        for key, value in node.items():
            child = f"{path}.{key}" if path else str(key)
            yield from walk_json(value, child)
    elif isinstance(node, list):
        for i, value in enumerate(node):
            child = f"{path}[{i}]"
            yield from walk_json(value, child)


def find_first_datetime(record: dict[str, Any], names: tuple[str, ...]) -> Optional[dt.datetime]:
    for key, value in record.items():
        lower = key.lower()
        if any(name in lower for name in names):
            parsed = parse_datetime(value)
            if parsed:
                return parsed
    return None


def find_first_activity_value(record: dict[str, Any]) -> Any:
    preferred = ("activity", "kind", "type", "mode", "status")
    for key, value in record.items():
        lower = key.lower()
        if any(p in lower for p in preferred) and not any(t in lower for t in ("time", "date", "start", "end")):
            if isinstance(value, (str, int, float, bool)):
                return value
            if isinstance(value, dict):
                for inner in ("name", "value", "type", "activity"):
                    if inner in value:
                        return value[inner]
    return None


REGISTRATION_KEYWORDS = (
    "registration", "registracyj", "regno", "reg_no", "regnumber", "reg_number",
    "vrn", "vehicle_registration", "vehicleregistration", "licenceplate",
    "licenseplate", "numberplate", "plate", "numerrejestracyjny", "nrrejestracyjny",
)


def _registration_compact(value: str) -> str:
    return value.replace(" ", "").replace("-", "")


def _is_plausible_vehicle_registration(value: str) -> bool:
    compact = _registration_compact(value)
    if len(compact) < 3 or len(compact) > 10:
        return False
    if set(compact) <= {"0"}:
        return False
    if compact in {"BRAK", "NIEZNANY", "UNKNOWN", "NONE", "NULL", "WLD", "EUR"}:
        return False
    if not any(ch.isalpha() for ch in compact):
        return False
    if not any(ch.isdigit() for ch in compact):
        return False
    return True


def _first_plausible_registration(raw: str) -> str:
    """Pick the first plausible registration and drop parser padding/trailing junk."""
    raw = re.sub(r"(?i)^(vehicle|registration|number|plate|nr|numer|rejestracyjny)[:=\s-]+", "", raw).strip()
    raw = raw.replace("\x00", "|")
    raw = re.sub(r"[^0-9A-Za-zĄĆĘŁŃÓŚŹŻąćęłńóśźż -]", "|", raw)
    # Dwa lub więcej odstępów w polu tachografu zwykle oznacza padding po
    # właściwym numerze. Wszystko po takim paddingu traktujemy jako następne
    # pole rekordu, nie jako część rejestracji.
    cleaned_segments: list[str] = []
    for segment in re.split(r"\|+", raw):
        segment = re.split(r"\s{2,}", segment, maxsplit=1)[0]
        segment = re.sub(r"\s+", " ", segment).strip(" -").upper()
        if segment:
            cleaned_segments.append(segment)
    if not cleaned_segments:
        return ""
    for segment in cleaned_segments:
        if _is_plausible_vehicle_registration(segment):
            return segment
    raw = " ".join(cleaned_segments).strip(" -")

    # Najpierw sprawdź pełną wartość. Dotyczy to prawidłowych numerów ze spacją,
    # np. "WE 12345" albo "AB-123-CD".
    if _is_plausible_vehicle_registration(raw):
        return raw

    # Jeżeli pole z tachografu zawierało dopisane bajty/śmieci na końcu, wybierz
    # pierwszy krótki fragment wyglądający jak numer rejestracyjny zamiast
    # najdłuższego ciągu znaków.
    patterns = (
        r"\b[A-ZĄĆĘŁŃÓŚŹŻ]{1,3}[ -]?[A-Z0-9]{2,7}\b",
        r"\b[A-Z0-9]{3,10}\b",
    )
    for pattern in patterns:
        for match in re.finditer(pattern, raw):
            candidate = re.sub(r"\s+", " ", match.group(0)).strip(" -")
            if _is_plausible_vehicle_registration(candidate):
                return candidate
    return ""


def normalize_vehicle_registration(value: Any) -> str:
    """Return a clean vehicle registration number or an empty string."""
    if value is None or isinstance(value, bool):
        return ""
    raw = str(value).strip().replace("\x00", "|")
    if not raw:
        return ""
    return _first_plausible_registration(raw)


def vehicle_registration_display(activity: Activity) -> str:
    return normalize_vehicle_registration(getattr(activity, "vehicle_registration", "")) or "-"


MANUAL_ENTRY_ICON = "✋"


def is_manual_entry_text(value: Any) -> bool:
    """Return True when text/metadata points to a manual tachograph entry."""
    text = str(value or "").lower()
    return any(token in text for token in (
        "manual",
        "wpis manualny",
        "reczny",
        "ręczny",
        "recznie",
        "ręcznie",
        "manualentry",
        "manual_entry",
    ))


def node_has_manual_entry_marker(value: Any, depth: int = 0) -> bool:
    """Detect common parser flags that mean an activity was entered manually."""
    if depth > 4:
        return False
    if isinstance(value, dict):
        for key, item in value.items():
            key_text = str(key).lower()
            if any(token in key_text for token in ("manual", "reczny", "ręczny")):
                if item not in (False, None, "", 0, "0", "false", "False"):
                    return True
            if isinstance(item, str) and is_manual_entry_text(item):
                return True
            if isinstance(item, (dict, list, tuple)) and node_has_manual_entry_marker(item, depth + 1):
                return True
    elif isinstance(value, (list, tuple)):
        return any(node_has_manual_entry_marker(item, depth + 1) for item in value)
    elif isinstance(value, str):
        return is_manual_entry_text(value)
    return False


def mark_source_as_manual(source: str) -> str:
    source = str(source or "")
    return source if is_manual_entry_text(source) else f"{source}:manual"


def with_manual_entry_icon(label: Any, *markers: Any) -> str:
    """Prefix visible report/table text with the hand icon for manual entries.

    The icon is added directly next to the specific manual row/entry, but only
    once. Technical markers stay internal and are not otherwise displayed.
    """
    text = str(label or "")
    if text.strip().startswith(MANUAL_ENTRY_ICON):
        return text
    if any(is_manual_entry_text(marker) for marker in markers):
        return f"{MANUAL_ENTRY_ICON} {text}"
    return text


def activity_is_manual(activity: Activity) -> bool:
    return is_manual_entry_text(getattr(activity, "source", "")) or is_manual_entry_text(getattr(activity, "kind", ""))


def country_entry_is_manual(entry: CountryCodeEntry) -> bool:
    return (
        is_manual_entry_text(getattr(entry, "entry_type", ""))
        or is_manual_entry_text(getattr(entry, "source", ""))
        or is_manual_entry_text(getattr(entry, "note", ""))
    )


def activity_report_label_with_manual_icon(activity: Activity) -> str:
    """Return report label with a hand icon directly at manually entered rows."""
    label = activity_report_label_with_icon(activity.kind)
    return with_manual_entry_icon(label, getattr(activity, "source", ""), getattr(activity, "kind", ""))


def activity_table_label_with_manual_icon(activity: Activity) -> str:
    """Return GUI activity label with the same manual-entry marker as reports."""
    label = activity_label_with_icon(activity.kind)
    return with_manual_entry_icon(label, getattr(activity, "source", ""), getattr(activity, "kind", ""))


def _registration_bytes_to_segments(chunk: bytes) -> list[str]:
    """Decode a tachograph registration field into text fragments.

    Non-text bytes are treated as separators. This prevents the next binary field
    from being glued to the registration as extra trailing characters.
    """
    chars: list[str] = []
    for byte in chunk:
        if byte in (0, 32):
            chars.append(" ")
        elif 48 <= byte <= 57 or 65 <= byte <= 90 or 97 <= byte <= 122 or byte == 45:
            chars.append(chr(byte))
        else:
            chars.append("|")
    text = "".join(chars)
    return [part.strip(" -") for part in re.split(r"\|+", text) if part.strip(" -")]


def _registration_from_bytes(chunk: bytes) -> str:
    """Best-effort registration extraction from tachograph byte fields."""
    if not chunk:
        return ""

    # CardVehiclesUsed zwykle zapisuje: nationNumeric + codePage + 13 znaków
    # numeru rejestracyjnego. Najpierw czytamy dokładnie to pole, żeby nie
    # doklejać kolejnych bajtów rekordu do numeru.
    windows = [chunk[2:15], chunk[2:16], chunk[1:15], chunk[:15], chunk]
    candidates: list[tuple[int, str]] = []
    for priority, window in enumerate(windows):
        if not window:
            continue
        for segment in _registration_bytes_to_segments(window):
            value = normalize_vehicle_registration(segment)
            if value:
                candidates.append((priority, value))
                break
        if candidates and candidates[-1][0] == priority:
            break

    if not candidates:
        return ""
    candidates.sort(key=lambda item: (item[0], len(_registration_compact(item[1]))))
    return candidates[0][1]


def _looks_like_registration_key(key: str) -> bool:
    lower = key.lower().replace(" ", "").replace("-", "_")
    if any(bad in lower for bad in ("country", "nation", "region")):
        return False
    return any(token in lower for token in REGISTRATION_KEYWORDS)


def find_first_registration_value(node: Any, depth: int = 0) -> str:
    if depth > 4:
        return ""
    if isinstance(node, dict):
        for key, value in node.items():
            if _looks_like_registration_key(str(key)):
                if isinstance(value, (str, int, float)):
                    reg = normalize_vehicle_registration(value)
                    if reg:
                        return reg
                if isinstance(value, dict):
                    for inner_key in ("value", "number", "text", "string", "vehicleRegistrationNumber", "registrationNumber"):
                        if inner_key in value:
                            reg = normalize_vehicle_registration(value[inner_key])
                            if reg:
                                return reg
                    reg = find_first_registration_value(value, depth + 1)
                    if reg:
                        return reg
            if isinstance(value, (dict, list)):
                reg = find_first_registration_value(value, depth + 1)
                if reg:
                    return reg
    elif isinstance(node, list):
        for item in node:
            reg = find_first_registration_value(item, depth + 1)
            if reg:
                return reg
    return ""


def extract_vehicle_uses(data: Any) -> list[VehicleUse]:
    """Extract vehicle registration periods from embedded JSON or common parser outputs."""
    result: list[VehicleUse] = []
    seen: set[tuple[dt.datetime, dt.datetime, str]] = set()
    for path, node in walk_json(data):
        if not isinstance(node, dict):
            continue
        path_lower = path.lower()
        reg = find_first_registration_value(node)
        if not reg:
            continue
        start = find_first_datetime(node, ("vehiclefirstuse", "firstuse", "first_use", "start", "begin", "from", "time_start", "starttime"))
        end = find_first_datetime(node, ("vehiclelastuse", "lastuse", "last_use", "end", "stop", "to", "until", "time_end", "endtime"))
        if (not start or not end or end <= start) and "vehicle_uses" in path_lower:
            start = parse_datetime(node.get("start"))
            end = parse_datetime(node.get("end"))
        if not start or not end or end <= start:
            continue
        if end - start > dt.timedelta(days=370):
            continue
        key = (start, end, reg)
        if key in seen:
            continue
        seen.add(key)
        result.append(VehicleUse(start=start, end=end, registration=reg, source=path))
    return sorted(result, key=lambda item: (item.start, item.end, item.registration))


def _activity_with_vehicle(activity: Activity, start: dt.datetime, end: dt.datetime, registration: str) -> Activity:
    return Activity(start=start, end=end, kind=activity.kind, source=activity.source, vehicle_registration=registration)


def apply_vehicle_registrations(activities: list[Activity], vehicle_uses: list[VehicleUse]) -> list[Activity]:
    """Assign vehicle registration numbers to activities, splitting only when needed."""
    if not activities or not vehicle_uses:
        return activities
    uses = [item for item in sorted(vehicle_uses, key=lambda v: (v.start, v.end)) if item.registration and item.end > item.start]
    if not uses:
        return activities
    result: list[Activity] = []
    for activity in activities:
        if vehicle_registration_display(activity) != "-":
            result.append(activity)
            continue
        overlaps: list[tuple[dt.datetime, dt.datetime, str]] = []
        for vehicle in uses:
            if vehicle.end <= activity.start or vehicle.start >= activity.end:
                continue
            start = max(activity.start, vehicle.start)
            end = min(activity.end, vehicle.end)
            if end > start:
                overlaps.append((start, end, vehicle.registration))
        if not overlaps:
            result.append(activity)
            continue
        overlaps.sort(key=lambda item: (item[0], item[1]))
        cursor = activity.start
        for start, end, registration in overlaps:
            if start > cursor:
                result.append(_activity_with_vehicle(activity, cursor, start, ""))
            part_start = max(start, cursor)
            if end > part_start:
                result.append(_activity_with_vehicle(activity, part_start, end, registration))
                cursor = max(cursor, end)
        if cursor < activity.end:
            result.append(_activity_with_vehicle(activity, cursor, activity.end, ""))
    return dedupe_and_merge(result)


def extract_explicit_intervals(data: Any) -> list[Activity]:
    """Extract records that already contain start/end + activity."""
    result: list[Activity] = []
    for path, node in walk_json(data):
        if not isinstance(node, dict):
            continue
        start = find_first_datetime(node, ("start", "begin", "from", "time_start", "starttime"))
        end = find_first_datetime(node, ("end", "stop", "to", "until", "time_end", "endtime"))
        if not start or not end or end <= start:
            continue
        kind = normalize_kind(find_first_activity_value(node))
        if kind == "UNKNOWN":
            # Czasem nazwa sciezki zawiera typ aktywności.
            kind = normalize_kind(path)
        if kind != "UNKNOWN":
            source = mark_source_as_manual(path) if node_has_manual_entry_marker(node) else path
            result.append(Activity(start=start, end=end, kind=kind, source=source, vehicle_registration=find_first_registration_value(node)))
    return dedupe_and_merge(result)


def extract_change_points(data: Any) -> list[Activity]:
    """Extract change-point records containing time + activity, then build intervals."""
    points: list[tuple[dt.datetime, str, str]] = []
    for path, node in walk_json(data):
        if not isinstance(node, dict):
            continue
        when = find_first_datetime(node, ("time", "date", "timestamp", "change", "record"))
        if not when:
            continue
        kind = normalize_kind(find_first_activity_value(node))
        if kind == "UNKNOWN":
            kind = normalize_kind(path)
        if kind != "UNKNOWN":
            source = mark_source_as_manual(path) if node_has_manual_entry_marker(node) else path
            points.append((when, kind, source))
    points = sorted(set(points), key=lambda x: x[0])
    activities: list[Activity] = []
    for i in range(len(points) - 1):
        start, kind, path = points[i]
        end = points[i + 1][0]
        if end > start:
            activities.append(Activity(start, end, kind, path))
    return dedupe_and_merge(activities)


def dedupe_and_merge(items: list[Activity]) -> list[Activity]:
    unique = sorted(set(items), key=lambda a: (a.start, a.end, a.kind))
    merged: list[Activity] = []
    for item in unique:
        if item.duration <= dt.timedelta(0):
            continue
        if (
            merged
            and merged[-1].kind == item.kind
            and merged[-1].end == item.start
            and getattr(merged[-1], "vehicle_registration", "") == getattr(item, "vehicle_registration", "")
        ):
            prev = merged[-1]
            merged[-1] = Activity(prev.start, item.end, prev.kind, prev.source, prev.vehicle_registration)
        else:
            merged.append(item)
    return merged


def normalize_activities(data: Any) -> list[Activity]:
    explicit = extract_explicit_intervals(data)
    activities = explicit if explicit else extract_change_points(data)
    return apply_vehicle_registrations(activities, extract_vehicle_uses(data))


# ---------------------------------------------------------------------------
# WBUDOWANY PARSER .DDD - KARTA KIEROWCY / DRIVER ACTIVITY DATA EF 0x0504
# ---------------------------------------------------------------------------

EMBEDDED_PARSER_NAME = "wbudowany parser DDD EF 0x0504"
DDD_DRIVER_ACTIVITY_TAG = b"\x05\x04"
DDD_DRIVER_VEHICLES_TAG = b"\x05\x05"
DDD_DRIVER_PLACES_TAG = b"\x05\x06"
DDD_DRIVER_IDENTIFICATION_TAG = b"\x05\x20"


def _be16(data: bytes, offset: int) -> int:
    return (data[offset] << 8) | data[offset + 1]


def _be32(data: bytes, offset: int) -> int:
    return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]


def _safe_time_real(seconds: int) -> Optional[dt.datetime]:
    try:
        when = dt.datetime.utcfromtimestamp(seconds)
    except Exception:
        return None
    if 1990 <= when.year <= 2050:
        return when
    return None


def _find_driver_activity_blocks(raw: bytes) -> list[tuple[int, bytes]]:
    """Find TLV blocks with tag 0x0504.

    In driver-card DDD files the elementary file header is commonly:
    tag(2) + signature/status byte(1) + length(2) + value.
    Example seen in real card dumps: 05 04 00 30 cc ... where 0x30cc is
    the DriverActivityData block length. A conservative fallback also checks
    tag(2) + length(3), because some dumps/documentation present the same
    five bytes as tag + 24-bit length.
    """
    blocks: list[tuple[int, bytes]] = []
    start = 0
    while True:
        pos = raw.find(DDD_DRIVER_ACTIVITY_TAG, start)
        if pos < 0:
            break

        # Preferred: tag(2), signature/status(1), length(2).
        if pos + 5 <= len(raw):
            length = _be16(raw, pos + 3)
            value_start = pos + 5
            value_end = value_start + length
            if 8 <= length <= len(raw) - value_start:
                blocks.append((pos, raw[value_start:value_end]))

        # Fallback: tag(2), 24-bit length.
        if pos + 5 <= len(raw):
            length24 = (raw[pos + 2] << 16) | (raw[pos + 3] << 8) | raw[pos + 4]
            value_start = pos + 5
            value_end = value_start + length24
            if 8 <= length24 <= len(raw) - value_start:
                block = raw[value_start:value_end]
                if (pos, block) not in blocks:
                    blocks.append((pos, block))

        start = pos + 2
    return blocks



def _find_tlv_blocks(raw: bytes, tag: bytes) -> list[tuple[int, bytes]]:
    """Find common tachograph EF blocks: tag(2)+status(1)+len(2)+value or tag(2)+len(3)+value."""
    blocks: list[tuple[int, bytes]] = []
    start = 0
    while True:
        pos = raw.find(tag, start)
        if pos < 0:
            break
        if pos + 5 <= len(raw):
            length = _be16(raw, pos + 3)
            value_start = pos + 5
            value_end = value_start + length
            if 1 <= length <= len(raw) - value_start:
                blocks.append((pos, raw[value_start:value_end]))
            length24 = (raw[pos + 2] << 16) | (raw[pos + 3] << 8) | raw[pos + 4]
            value_end = value_start + length24
            if 1 <= length24 <= len(raw) - value_start:
                candidate = (pos, raw[value_start:value_end])
                if candidate not in blocks:
                    blocks.append(candidate)
        start = pos + 2
    return blocks


def _decode_card_text(value: bytes) -> str:
    """Decode tachograph card text, ignoring the leading code-page byte."""
    payload = value[1:] if value and value[0] < 0x20 else value
    return payload.decode("latin-1", errors="ignore").replace("\x00", "").strip(" \xff")


def parse_ddd_driver_identification_embedded(ddd_path: Path) -> dict[str, Any]:
    """Parse CardIdentification EF 0x0520 using the standard driver-card layout."""
    raw = ddd_path.read_bytes()

    for _, block in _find_tlv_blocks(raw, DDD_DRIVER_IDENTIFICATION_TAG):
        if len(block) < 141:
            continue

        issuing_country = normalize_country_code(block[0])
        card_number = _decode_card_text(block[1:17])
        expiry = _safe_time_real(_be32(block, 61))
        surname = _decode_card_text(block[65:101])
        first_name = _decode_card_text(block[101:137])

        if card_number:
            return {
                "first_name": first_name,
                "last_name": surname,
                "card_number": card_number,
                "card_expiry_date": expiry.date().isoformat() if expiry else "",
                "card_issuing_country": issuing_country if issuing_country != "---" else "",
            }

    return {
        "first_name": "",
        "last_name": "",
        "card_number": "",
        "card_expiry_date": "",
        "card_issuing_country": "",
    }


def _entry_type_label(value: int) -> str:
    return ENTRY_TYPE_LABELS.get(value, f"Typ wpisu {value}")


def _parse_place_records_from_block(block: bytes, block_offset: int) -> list[CountryCodeEntry]:
    """Parse CardPlaceDailyWorkPeriod EF 0x0506 records from a driver card.

    Layout used here: PlacePointerNewestRecord(1), then PlaceRecord entries of 10 bytes:
    EntryTime(4), EntryType(1), DailyWorkPeriodCountry(1), Region(1), Odometer(3).
    """
    result: list[CountryCodeEntry] = []
    if len(block) < 11:
        return result
    payload = block[1:]
    for offset in range(0, len(payload) - 9, 10):
        record = payload[offset:offset + 10]
        when = _safe_time_real(_be32(record, 0))
        if not when:
            continue
        entry_type_raw = record[4]
        code = normalize_country_code(record[5])
        region = record[6]
        odometer = (record[7] << 16) | (record[8] << 8) | record[9]
        if code == "---" and entry_type_raw == 0 and odometer == 0:
            continue
        label = _entry_type_label(entry_type_raw)
        status = "OK"
        if code == "---":
            status = "brak kodu kraju"
        elif code == "PL":
            status = "PL"
        note = f"Region: {region}; licznik: {odometer} km" if odometer else f"Region: {region}"
        result.append(CountryCodeEntry(
            timestamp=when,
            entry_type=label,
            country_code=code,
            country_name=country_name_pl(code),
            status=status,
            related_day=f"{when:%Y-%m-%d}",
            note=note,
            source=f"embedded:EF0506@{block_offset}:record@{offset}",
        ))
    return sorted(result, key=lambda x: x.timestamp)


def parse_ddd_country_code_entries_embedded(ddd_path: Path) -> list[CountryCodeEntry]:
    raw = ddd_path.read_bytes()
    entries: list[CountryCodeEntry] = []
    for block_offset, block in _find_tlv_blocks(raw, DDD_DRIVER_PLACES_TAG):
        entries.extend(_parse_place_records_from_block(block, block_offset))
    return sorted(entries, key=lambda x: x.timestamp)


def _parse_vehicle_uses_from_block(block: bytes, block_offset: int) -> list[VehicleUse]:
    """Best-effort parser for CardVehiclesUsed EF 0x0505.

    Driver cards store vehicle-use periods together with a vehicle registration
    identification. The exact record layout differs by generation/vendor, so this
    routine scans the EF block for plausible consecutive TimeReal values and then
    reads the registration bytes that follow them.
    """
    result: list[VehicleUse] = []
    seen: set[tuple[dt.datetime, dt.datetime, str]] = set()
    if len(block) < 14:
        return result
    for offset in range(0, len(block) - 12):
        start = _safe_time_real(_be32(block, offset))
        end = _safe_time_real(_be32(block, offset + 4))
        if not start or not end or end <= start:
            continue
        if end - start > dt.timedelta(days=370):
            continue
        registration = _registration_from_bytes(block[offset + 8:offset + 36])
        if not registration:
            continue
        key = (start, end, registration)
        if key in seen:
            continue
        seen.add(key)
        result.append(VehicleUse(
            start=start,
            end=end,
            registration=registration,
            source=f"embedded:EF0505@{block_offset}:record@{offset}",
        ))
    return sorted(result, key=lambda x: (x.start, x.end, x.registration))


def parse_ddd_vehicle_uses_embedded(ddd_path: Path) -> list[VehicleUse]:
    raw = ddd_path.read_bytes()
    vehicles: list[VehicleUse] = []
    for block_offset, block in _find_tlv_blocks(raw, DDD_DRIVER_VEHICLES_TAG):
        vehicles.extend(_parse_vehicle_uses_from_block(block, block_offset))
    # Deduplicate overlapping duplicate scans of the same EF record.
    unique: dict[tuple[dt.datetime, dt.datetime, str], VehicleUse] = {}
    for item in vehicles:
        unique.setdefault((item.start, item.end, item.registration), item)
    return sorted(unique.values(), key=lambda x: (x.start, x.end, x.registration))


def _activity_word_to_kind(word: int) -> tuple[int, str]:
    minutes = word & 0x07FF
    activity_code = (word >> 11) & 0x03
    kind = {
        0: "REST",
        1: "AVAILABILITY",
        2: "WORK",
        3: "DRIVING",
    }.get(activity_code, "UNKNOWN")
    return minutes, kind


def _ring_slice(ring: bytes, start: int, length: int) -> bytes:
    """Return bytes from a cyclic tachograph activity buffer."""
    if not ring or length <= 0:
        return b""
    start %= len(ring)
    end = start + length
    if end <= len(ring):
        return ring[start:end]
    return ring[start:] + ring[:end % len(ring)]


def _daily_record_header_from_ring(ring: bytes, offset: int) -> Optional[tuple[int, dt.datetime]]:
    """Read and validate a daily-record header at a cyclic-buffer offset."""
    if len(ring) < 12 or not (0 <= offset < len(ring)):
        return None
    header = _ring_slice(ring, offset, 12)
    if len(header) < 12:
        return None
    record_len = _be16(header, 2)
    record_date = _safe_time_real(_be32(header, 4))
    if not record_date:
        return None
    if not (12 <= record_len <= min(4096, len(ring))):
        return None
    return record_len, record_date


def _ordered_activity_ring_from_pointers(ring: bytes, oldest: int, newest: int) -> bytes:
    """Linearize EF 0x0504 records without dropping the newest day.

    ActivityPointerNewestDayRecord can point to the newest daily record itself.
    The old slice ring[oldest:newest] then excluded that newest record, which
    made the last card-withdrawal day disappear from the analysis.
    """
    if not ring or not (0 <= oldest < len(ring)) or not (0 <= newest < len(ring)):
        return b""

    newest_is_record_start = _daily_record_header_from_ring(ring, newest) is not None
    chunks: list[bytes] = []
    seen_offsets: set[int] = set()
    pos = oldest
    total = 0

    for _ in range(max(1, len(ring) // 12 + 2)):
        if pos in seen_offsets:
            return b""
        seen_offsets.add(pos)

        header = _daily_record_header_from_ring(ring, pos)
        if header is None:
            return b""
        record_len, _record_date = header
        if total + record_len > len(ring):
            return b""

        chunks.append(_ring_slice(ring, pos, record_len))
        total += record_len

        if newest_is_record_start and pos == newest:
            return b"".join(chunks)

        next_pos = (pos + record_len) % len(ring)
        if not newest_is_record_start and next_pos == newest:
            return b"".join(chunks)
        pos = next_pos

    return b""


def _ordered_activity_ring(block: bytes) -> bytes:
    if len(block) < 8:
        return b""
    oldest = _be16(block, 0)
    newest = _be16(block, 2)
    ring = block[4:]
    if not ring:
        return b""

    ordered = _ordered_activity_ring_from_pointers(ring, oldest, newest)
    if ordered:
        return ordered

    # Legacy fallback for vendor-specific pointer semantics. If newest is an
    # exclusive end pointer this keeps the original behaviour. If that slice has
    # no valid daily records, fall back to scanning the full ring rather than
    # silently losing the final day.
    if 0 <= oldest < len(ring) and 0 <= newest < len(ring):
        if newest < oldest:
            candidate = ring[oldest:] + ring[:newest]
            if _daily_record_candidates(candidate):
                return candidate
        if newest > oldest:
            candidate = ring[oldest:newest]
            if _daily_record_candidates(candidate):
                return candidate

    # Fallback: parse the whole ring when pointers are invalid or vendor-specific.
    return ring


def _daily_record_candidates(ordered: bytes) -> list[tuple[int, int, dt.datetime, bytes]]:
    """Return plausible CardActivityDailyRecord candidates.

    Record layout from Annex 1B-style structures:
    previousLength(2), recordLength(2), activityRecordDate TimeReal(4),
    dailyPresenceCounter(2), dayDistance(2), ActivityChangeInfo[]
    """
    candidates: list[tuple[int, int, dt.datetime, bytes]] = []
    i = 0
    hard_limit = len(ordered)
    while i + 12 <= hard_limit:
        record_len = _be16(ordered, i + 2)
        record_date = _safe_time_real(_be32(ordered, i + 4))
        if record_date and 12 <= record_len <= min(4096, hard_limit - i):
            payload = ordered[i + 12:i + record_len]
            if len(payload) >= 2 and len(payload) % 2 == 0:
                candidates.append((i, record_len, record_date, payload))
                i += record_len
                continue
        i += 1
    return candidates


def _activities_from_daily_record(record_date: dt.datetime, payload: bytes, source: str) -> list[Activity]:
    day_start = dt.datetime.combine(record_date.date(), dt.time.min)
    points: list[tuple[int, str]] = []
    for i in range(0, len(payload) - 1, 2):
        word = _be16(payload, i)
        minute, kind = _activity_word_to_kind(word)
        if 0 <= minute <= 1440 and kind != "UNKNOWN":
            points.append((minute, kind))

    # Keep last state per minute and sort.
    by_minute: dict[int, str] = {}
    for minute, kind in points:
        by_minute[minute] = kind
    ordered_points = sorted(by_minute.items())
    if len(ordered_points) < 1:
        return []

    activities: list[Activity] = []
    for idx, (minute, kind) in enumerate(ordered_points):
        next_minute = ordered_points[idx + 1][0] if idx + 1 < len(ordered_points) else 1440
        if next_minute <= minute:
            continue
        start = day_start + dt.timedelta(minutes=minute)
        end = day_start + dt.timedelta(minutes=min(next_minute, 1440))
        if end > start:
            activities.append(Activity(start, end, kind, source))
    return activities


def parse_ddd_driver_card_embedded(ddd_path: Path) -> dict[str, Any]:
    """Parse driver-card .DDD activities with the embedded parser.

    This is intentionally focused on the activity records needed by the time
    analysis engine. It does not yet verify signatures or parse all card fields.
    """
    raw = ddd_path.read_bytes()
    blocks = _find_driver_activity_blocks(raw)
    all_activities: list[Activity] = []
    diagnostics: list[dict[str, Any]] = []

    for block_offset, block in blocks:
        ordered = _ordered_activity_ring(block)
        records = _daily_record_candidates(ordered)
        diagnostics.append({
            "tag": "0x0504",
            "offset": block_offset,
            "block_length": len(block),
            "ordered_length": len(ordered),
            "daily_records_found": len(records),
        })
        for record_offset, record_len, record_date, payload in records:
            source = f"embedded:EF0504@{block_offset}:record@{record_offset}"
            all_activities.extend(_activities_from_daily_record(record_date, payload, source))

    vehicle_uses = parse_ddd_vehicle_uses_embedded(ddd_path)
    activities = apply_vehicle_registrations(
        dedupe_and_merge(sorted(all_activities, key=lambda a: (a.start, a.end, a.kind))),
        vehicle_uses,
    )
    country_entries = parse_ddd_country_code_entries_embedded(ddd_path)
    driver = parse_ddd_driver_identification_embedded(ddd_path)
    file_modified = _file_modified_date(ddd_path)
    return {
        "driver": driver,
        "parser": {
            "name": EMBEDDED_PARSER_NAME,
            "version": "0.4-driver-identification",
            "file_type": "driver_card",
            "scope": "Driver identification EF 0x0520 and activity data EF 0x0504-0x0506",
            "card_read_date": file_modified.isoformat() if file_modified else "",
            "card_read_date_source": "data modyfikacji pliku .DDD",
            "default_control_period_days": 56,
            "diagnostics": diagnostics,
        },
        "vehicle_uses": [
            {
                "start": v.start.isoformat(sep=" "),
                "end": v.end.isoformat(sep=" "),
                "vehicle_registration": v.registration,
                "source": v.source,
            }
            for v in vehicle_uses
        ],
        "activities": [
            {
                "start": a.start.isoformat(sep=" "),
                "end": a.end.isoformat(sep=" "),
                "activity": activity_label(a.kind),
                "activity_code": a.kind,
                "vehicle_registration": vehicle_registration_display(a),
                "source": a.source,
            }
            for a in activities
        ],
        "country_code_entries": [
            {
                "timestamp": e.timestamp.isoformat(sep=" "),
                "entry_type": e.entry_type,
                "country_code": e.country_code,
                "country_name": e.country_name,
                "status": e.status,
                "related_day": e.related_day,
                "note": e.note,
                "source": e.source,
            }
            for e in country_entries
        ],
    }



def fmt_hours(delta: dt.timedelta) -> str:
    minutes = round(delta.total_seconds() / 60)
    sign = "-" if minutes < 0 else ""
    minutes = abs(minutes)
    return f"{sign}{minutes // 60:02d}:{minutes % 60:02d}"


def fmt_duration_report(delta: dt.timedelta) -> str:
    """Format activity duration for reports. Above 24h use days, hours and minutes."""
    total_minutes = round(delta.total_seconds() / 60)
    sign = "-" if total_minutes < 0 else ""
    total_minutes = abs(total_minutes)
    if total_minutes <= 24 * 60:
        return fmt_hours(delta)
    days, rest = divmod(total_minutes, 24 * 60)
    hours, minutes = divmod(rest, 60)
    day_word = "dzień" if days == 1 else "dni"
    return f"{sign}{days} {day_word} {hours:02d} godz. {minutes:02d} min"


def iso_week_start(date_time: dt.datetime) -> dt.datetime:
    d = date_time.date()
    monday = d - dt.timedelta(days=d.weekday())
    return dt.datetime.combine(monday, dt.time.min)


def split_by_day_or_week(activity: Activity, mode: str) -> Iterable[tuple[dt.datetime, dt.datetime]]:
    current = activity.start
    while current < activity.end:
        if mode == "day":
            boundary = dt.datetime.combine(current.date() + dt.timedelta(days=1), dt.time.min)
        else:
            boundary = iso_week_start(current) + dt.timedelta(days=7)
        end = min(activity.end, boundary)
        yield current, end
        current = end


def is_break_kind(kind: str, count_availability_as_break: bool) -> bool:
    return kind == "REST" or (count_availability_as_break and kind == "AVAILABILITY")


def check_break_45_after_4h30(activities: list[Activity], count_availability_as_break: bool) -> list[Violation]:
    violations: list[Violation] = []
    driving_since_break = dt.timedelta(0)
    period_start: Optional[dt.datetime] = None
    break_parts: list[dt.timedelta] = []

    for a in activities:
        if a.kind == "DRIVING":
            if period_start is None:
                period_start = a.start
            driving_since_break += a.duration
            break_parts.clear()
            if driving_since_break > dt.timedelta(hours=4, minutes=30):
                excess = driving_since_break - dt.timedelta(hours=4, minutes=30)
                violations.append(Violation(
                    rule="Przerwa po 4h30 jazdy",
                    severity="wysokie",
                    start=period_start,
                    end=a.end,
                    value=f"jazda bez wymaganej przerwy: {fmt_hours(driving_since_break)}",
                    limit="04:30 jazdy + 00:45 przerwy albo 00:15 + 00:30",
                    description=f"Przekroczono ciag jazdy o {fmt_hours(excess)} przed pelna przerwa.",
                ))
                # Po wykryciu nie resetujemy natychmiast, aby raport pokazal kolejne odcinki do przerwy.
        elif is_break_kind(a.kind, count_availability_as_break):
            break_parts.append(a.duration)
            total = sum((x for x in break_parts), dt.timedelta(0))
            if a.duration >= dt.timedelta(minutes=45) or (
                len(break_parts) >= 2
                and break_parts[-2] >= dt.timedelta(minutes=15)
                and break_parts[-1] >= dt.timedelta(minutes=30)
            ) or total >= dt.timedelta(minutes=45):
                driving_since_break = dt.timedelta(0)
                period_start = None
                break_parts.clear()
        elif a.kind == "WORK":
            # Inna praca przerywa odpoczynek i nie resetuje limitu jazdy.
            break_parts.clear()
    return squash_duplicate_violations(violations)


def squash_duplicate_violations(items: list[Violation]) -> list[Violation]:
    result: list[Violation] = []
    seen: set[tuple[str, dt.datetime, dt.datetime, str]] = set()
    for v in items:
        key = (v.rule, v.start, v.end, v.value)
        if key not in seen:
            result.append(v)
            seen.add(key)
    return result


def check_daily_driving(activities: list[Activity]) -> list[Violation]:
    by_day: dict[dt.date, dt.timedelta] = {}
    for a in activities:
        if a.kind != "DRIVING":
            continue
        for start, end in split_by_day_or_week(a, "day"):
            by_day[start.date()] = by_day.get(start.date(), dt.timedelta(0)) + (end - start)

    by_week_extended: dict[dt.datetime, int] = {}
    violations: list[Violation] = []
    for day, total in sorted(by_day.items()):
        start = dt.datetime.combine(day, dt.time.min)
        end = start + dt.timedelta(days=1)
        week = iso_week_start(start)
        if total > dt.timedelta(hours=10):
            violations.append(Violation(
                "Dzienny czas jazdy", "krytyczne", start, end,
                fmt_hours(total), "09:00 standardowo, 10:00 maks. 2 razy w tygodniu",
                "Dzienny czas jazdy przekroczyl 10 godzin.",
            ))
        elif total > dt.timedelta(hours=9):
            by_week_extended[week] = by_week_extended.get(week, 0) + 1
            if by_week_extended[week] > 2:
                violations.append(Violation(
                    "Dzienny czas jazdy", "wysokie", start, end,
                    fmt_hours(total), "10:00 tylko 2 razy w tygodniu",
                    "Trzecie lub kolejne wydluzenie jazdy dziennej powyzej 9 godzin w tym tygodniu.",
                ))
    return violations


def check_weekly_driving(activities: list[Activity]) -> list[Violation]:
    by_week: dict[dt.datetime, dt.timedelta] = {}
    for a in activities:
        if a.kind != "DRIVING":
            continue
        for start, end in split_by_day_or_week(a, "week"):
            week = iso_week_start(start)
            by_week[week] = by_week.get(week, dt.timedelta(0)) + (end - start)

    violations: list[Violation] = []
    weeks = sorted(by_week)
    for week in weeks:
        total = by_week[week]
        if total > dt.timedelta(hours=56):
            violations.append(Violation(
                "Tygodniowy czas jazdy", "krytyczne", week, week + dt.timedelta(days=7),
                fmt_hours(total), "56:00",
                "Tygodniowy czas jazdy przekroczyl 56 godzin.",
            ))
    for week in weeks:
        next_week = week + dt.timedelta(days=7)
        if next_week in by_week:
            total2 = by_week[week] + by_week[next_week]
            if total2 > dt.timedelta(hours=90):
                violations.append(Violation(
                    "Dwutygodniowy czas jazdy", "krytyczne", week, week + dt.timedelta(days=14),
                    fmt_hours(total2), "90:00",
                    "Laczny czas jazdy w dwoch kolejnych tygodniach przekroczyl 90 godzin.",
                ))
    return violations


REST_MERGE_TOLERANCE = dt.timedelta(minutes=2)


def rest_blocks(activities: list[Activity], count_availability_as_break: bool = False) -> list[Activity]:
    """Return continuous rest blocks used for daily and weekly rest checks.

    Odpoczynek tygodniowy moze wypasc w dowolnym dniu tygodnia. Dlatego kazdy
    ciagly blok REST od 24:00 jest pozniej kwalifikowany jako odpoczynek
    tygodniowy, niezaleznie od tego, czy zaczyna sie w weekend, czy w srodku
    tygodnia.

    Parsery tachografu potrafia rozbic ten sam odpoczynek na granicy doby albo
    zostawic techniczna luke 1-2 minuty. Taka luka nie powinna automatycznie
    kasowac odpoczynku tygodniowego, wiec sasiednie bloki REST laczymy z mala
    tolerancja. Luka nadal pozostaje widoczna w raporcie ciaglosci danych.
    """
    blocks: list[Activity] = []
    current: Optional[Activity] = None

    for a in sorted(activities, key=lambda item: (item.start, item.end)):
        rest_like = a.kind == "REST" or (count_availability_as_break and a.kind == "AVAILABILITY")
        if rest_like:
            if current and a.start <= current.end + REST_MERGE_TOLERANCE:
                current = Activity(
                    current.start,
                    max(current.end, a.end),
                    "REST",
                    current.source,
                    getattr(current, "vehicle_registration", ""),
                )
            else:
                if current:
                    blocks.append(current)
                current = Activity(
                    a.start,
                    a.end,
                    "REST",
                    a.source,
                    getattr(a, "vehicle_registration", ""),
                )
        else:
            if current:
                blocks.append(current)
                current = None

    if current:
        blocks.append(current)
    return blocks


def check_daily_rest(activities: list[Activity]) -> list[Violation]:
    blocks = rest_blocks(activities)
    qualifying = [b for b in blocks if b.duration >= dt.timedelta(hours=9)]
    violations: list[Violation] = []
    if not activities:
        return violations
    if not qualifying:
        violations.append(Violation(
            "Odpoczynek dobowy", "wysokie", activities[0].start, activities[-1].end,
            "brak >=09:00", "11:00 regularny albo 09:00 skrocony",
            "W danych nie znaleziono odpoczynku dobowego trwajacego co najmniej 9 godzin.",
        ))
        return violations

    reduced_by_week: dict[dt.datetime, int] = {}
    for block in qualifying:
        if dt.timedelta(hours=9) <= block.duration < dt.timedelta(hours=11):
            week = iso_week_start(block.start)
            reduced_by_week[week] = reduced_by_week.get(week, 0) + 1
            if reduced_by_week[week] > 3:
                violations.append(Violation(
                    "Skrocony odpoczynek dobowy", "srednie", block.start, block.end,
                    fmt_hours(block.duration), "maks. 3 skrocenia 09:00-10:59 w tygodniu/okresie miedzy odpoczynkami tygodniowymi",
                    "Wykryto wiecej niz trzy skrocone odpoczynki dobowe w przyblizonym okresie tygodniowym.",
                ))

    # Przyblizone sprawdzenie: po zakonczeniu odpoczynku dobowego/tygodniowego nastepny odpoczynek >=9h
    # powinien wystapic w ciagu 24h. Pelne wdrozenie wymaga obslugi zalogi kilkuosobowej i wyjątkow.
    for prev, nxt in zip(qualifying, qualifying[1:]):
        deadline = prev.end + dt.timedelta(hours=24)
        if nxt.start > deadline:
            delay = nxt.start - deadline
            violations.append(Violation(
                "Odpoczynek dobowy w 24h", "wysokie", prev.end, nxt.start,
                f"rzeczywisty start: {nxt.start:%Y-%m-%d %H:%M}; opoznienie: {fmt_hours(delay)}; przerwa miedzy odpoczynkami: {fmt_hours(nxt.start - prev.end)}",
                f"odpoczynek >=09:00 powinien rozpoczac sie najpozniej: {deadline:%Y-%m-%d %H:%M}",
                f"Nastepny odpoczynek dobowy zaczal sie po uplywie 24h od konca poprzedniego odpoczynku. Powinien rozpoczac sie najpozniej {deadline:%Y-%m-%d %H:%M}; faktycznie rozpoczal sie {nxt.start:%Y-%m-%d %H:%M}.",
            ))
    return violations


WEEKLY_REST_MIN = dt.timedelta(hours=24)
REGULAR_WEEKLY_REST_MIN = dt.timedelta(hours=45)
MIN_ACTIVE_BEFORE_REDUCED_WEEKLY_AFTER_REGULAR = dt.timedelta(hours=24)


def _activity_overlap_duration(activity: Activity, start: dt.datetime, end: dt.datetime) -> dt.timedelta:
    """Return the part of an activity that overlaps the selected datetime range."""
    overlap_start = max(activity.start, start)
    overlap_end = min(activity.end, end)
    if overlap_end <= overlap_start:
        return dt.timedelta(0)
    return overlap_end - overlap_start


def non_rest_activity_duration_between(activities: list[Activity], start: dt.datetime, end: dt.datetime) -> dt.timedelta:
    """Sum DRIVING/WORK/AVAILABILITY/UNKNOWN time between two rests.

    This is used to avoid a false weekly-rest debt when a 24-45h REST block is
    only an additional long rest directly after a regular weekly rest. In that
    situation the previous 45h+ weekly rest has already closed the weekly cycle,
    so the next 24-45h block should not automatically create compensation debt.
    """
    if end <= start:
        return dt.timedelta(0)
    total = dt.timedelta(0)
    for activity in activities:
        if activity.end <= start or activity.start >= end:
            continue
        if activity.kind != "REST":
            total += _activity_overlap_duration(activity, start, end)
    return total


def weekly_rest_key(block: Activity) -> tuple[dt.datetime, dt.datetime]:
    return (block.start, block.end)


def is_extra_reduced_rest_after_regular_weekly(
    block: Activity,
    previous_effective_weekly: Optional[Activity],
    activities: list[Activity],
) -> bool:
    """Return True when a 24-45h rest should not create weekly compensation.

    A long REST block of 24-45h can appear shortly after a full weekly rest, for
    example after taking a regular weekly rest from Saturday to Tuesday and then
    another 26h rest after a short period of activity. Earlier versions treated
    every such block as a reduced weekly rest and generated compensation. That is
    too aggressive: if the previous effective weekly rest was regular and there
    was less than 24h of non-rest activity before the next 24-45h block, this
    later block is treated as an additional long daily/rest extension, not as a
    required reduced weekly rest.
    """
    if previous_effective_weekly is None:
        return False
    if previous_effective_weekly.duration < REGULAR_WEEKLY_REST_MIN:
        return False
    if not (WEEKLY_REST_MIN <= block.duration < REGULAR_WEEKLY_REST_MIN):
        return False
    active_between = non_rest_activity_duration_between(activities, previous_effective_weekly.end, block.start)
    return active_between < MIN_ACTIVE_BEFORE_REDUCED_WEEKLY_AFTER_REGULAR


def effective_weekly_rest_blocks(activities: list[Activity]) -> list[Activity]:
    """Return weekly rests used by the compliance logic.

    Regular weekly rests (45h+) are always effective. Reduced 24-45h rests are
    effective unless they are only an extra long rest directly after an already
    valid regular weekly rest. This prevents false compensation debts for cases
    like 2026-05-02 14:30 - 2026-05-05 06:40 followed by a separate 26h rest.
    """
    acts = sorted(activities, key=lambda a: (a.start, a.end))
    candidates = [b for b in rest_blocks(acts) if b.duration >= WEEKLY_REST_MIN]
    effective: list[Activity] = []
    for block in candidates:
        if block.duration >= REGULAR_WEEKLY_REST_MIN:
            effective.append(block)
            continue
        previous = effective[-1] if effective else None
        if is_extra_reduced_rest_after_regular_weekly(block, previous, acts):
            continue
        effective.append(block)
    return effective


def ignored_reduced_weekly_rest_keys(activities: list[Activity]) -> set[tuple[dt.datetime, dt.datetime]]:
    """Return 24-45h REST blocks ignored as weekly rests by the v12 logic."""
    acts = sorted(activities, key=lambda a: (a.start, a.end))
    candidates = [b for b in rest_blocks(acts) if WEEKLY_REST_MIN <= b.duration < REGULAR_WEEKLY_REST_MIN]
    effective_keys = {weekly_rest_key(block) for block in effective_weekly_rest_blocks(acts)}
    return {weekly_rest_key(block) for block in candidates if weekly_rest_key(block) not in effective_keys}


def check_weekly_rest(activities: list[Activity]) -> list[Violation]:
    blocks = rest_blocks(activities)
    weekly_candidates = [b for b in blocks if b.duration >= WEEKLY_REST_MIN]
    weekly = effective_weekly_rest_blocks(activities)
    violations: list[Violation] = []
    for block in weekly:
        if WEEKLY_REST_MIN <= block.duration < REGULAR_WEEKLY_REST_MIN:
            violations.append(Violation(
                "Skrocony odpoczynek tygodniowy", "informacyjne", block.start, block.end,
                fmt_hours(block.duration), "45:00 regularny albo min. 24:00 skrocony + kompensata",
                "Odpoczynek tygodniowy jest skrocony; moze wystapic w dowolnym dniu tygodnia; nalezy zaplanowac kompensate.",
            ))
    for prev, nxt in zip(weekly, weekly[1:]):
        latest_start = prev.end + dt.timedelta(days=6)
        if nxt.start > latest_start:
            delay = nxt.start - latest_start
            violations.append(Violation(
                "Termin odpoczynku tygodniowego", "wysokie", prev.end, nxt.start,
                f"rzeczywisty start: {nxt.start:%Y-%m-%d %H:%M}; opoznienie: {fmt_hours(delay)}; od poprzedniego odpoczynku: {fmt_hours(nxt.start - prev.end)}",
                f"odpoczynek tygodniowy powinien rozpoczac sie najpozniej: {latest_start:%Y-%m-%d %H:%M}",
                f"Nastepny odpoczynek tygodniowy rozpoczal sie za pozno. Powinien rozpoczac sie najpozniej {latest_start:%Y-%m-%d %H:%M}; faktycznie rozpoczal sie {nxt.start:%Y-%m-%d %H:%M}.",
            ))
    if activities and not weekly_candidates:
        violations.append(Violation(
            "Odpoczynek tygodniowy", "srednie", activities[0].start, activities[-1].end,
            "brak >=24:00", "min. 24:00 skrocony lub 45:00 regularny",
            "W analizowanym zakresie nie znaleziono odpoczynku tygodniowego. To moze byc normalne przy krotkim wycinku danych.",
        ))
    return violations


def analyze(activities: list[Activity], count_availability_as_break: bool) -> list[Violation]:
    acts = sorted(activities, key=lambda a: a.start)
    return sorted(
        check_break_45_after_4h30(acts, count_availability_as_break)
        + check_daily_driving(acts)
        + check_weekly_driving(acts)
        + check_daily_rest(acts)
        + check_weekly_rest(acts)
        + check_data_continuity_violations(acts)
        + check_weekly_rest_compensation_violations(acts),
        key=lambda v: (v.start, v.rule),
    )


DATA_GAP_TOLERANCE = dt.timedelta(minutes=2)
OVERLAP_TOLERANCE = dt.timedelta(minutes=1)


def analyze_data_continuity(activities: list[Activity]) -> list[ContinuityIssue]:
    """Find gaps, overlaps and UNKNOWN records that make rest/break assessment uncertain.

    Brak danych nie jest liczony jako odpoczynek. Tabela wskazuje okresy, w ktorych
    nie da sie pewnie wykluczyc skrocenia przerw lub odpoczynku bez dodatkowych
    wpisow manualnych, pliku z pojazdu/VU albo brakujacego pliku z karty.
    """
    acts = sorted(activities, key=lambda a: (a.start, a.end))
    issues: list[ContinuityIssue] = []
    if not acts:
        return issues

    for a in acts:
        if a.kind == "UNKNOWN":
            issues.append(ContinuityIssue(
                a.start,
                a.end,
                a.duration,
                "Nieznana aktywnosc",
                "Nie mozna zaliczyc tego okresu jako przerwy lub odpoczynku bez potwierdzenia.",
                "Sprawdz wpis manualny albo dane z pojazdu/VU dla tego zakresu.",
            ))

    for prev, nxt in zip(acts, acts[1:]):
        if nxt.start > prev.end + DATA_GAP_TOLERANCE:
            duration = nxt.start - prev.end
            issues.append(ContinuityIssue(
                prev.end,
                nxt.start,
                duration,
                "Brak ciaglosci danych",
                "Luka w danych moze ukrywac jazde, prace albo odpoczynek; aplikacja nie traktuje luki jako odpoczynku.",
                "Uzupelnij wpis manualny, wczytaj brakujacy plik z karty/kolejnego pojazdu albo porownaj z wydrukiem z tachografu.",
            ))
        elif nxt.start < prev.end - OVERLAP_TOLERANCE:
            duration = prev.end - nxt.start
            issues.append(ContinuityIssue(
                nxt.start,
                prev.end,
                duration,
                "Nakladanie danych",
                "Nakladajace sie okresy aktywności moga zafalszowac obliczenie przerw i odpoczynkow.",
                "Sprawdz zrodlo danych i usun duplikat lub bledny rekord przed kontrola.",
            ))
    return sorted(issues, key=lambda x: (x.start, x.kind))


def continuity_issues_between(issues: list[ContinuityIssue], start: dt.datetime, end: dt.datetime) -> list[ContinuityIssue]:
    return [issue for issue in issues if issue.end > start and issue.start < end]


def check_data_continuity_violations(activities: list[Activity]) -> list[Violation]:
    result: list[Violation] = []
    for issue in analyze_data_continuity(activities):
        result.append(Violation(
            issue.kind,
            "do weryfikacji",
            issue.start,
            issue.end,
            fmt_hours(issue.duration),
            "pelna ciaglosc danych aktywności",
            f"{issue.impact} Zalecenie: {issue.recommendation}",
        ))
    return result


def compensation_deadline_for_reduced_weekly_rest(rest_start: dt.datetime) -> dt.datetime:
    """Exclusive deadline: Monday 00:00 after the third week following the week in question."""
    return iso_week_start(rest_start) + dt.timedelta(days=28)


def fmt_deadline(deadline_exclusive: dt.datetime) -> str:
    # Przepis mowi o koncu tygodnia; dla czytelnosci pokazujemy niedziele 23:59.
    return f"{deadline_exclusive - dt.timedelta(minutes=1):%Y-%m-%d %H:%M}"


def analyze_weekly_rest_compensations(activities: list[Activity]) -> list[CompensationDebt]:
    acts = sorted(activities, key=lambda a: a.start)
    if not acts:
        return []
    issues = analyze_data_continuity(acts)
    blocks = rest_blocks(acts)
    weekly_rests = effective_weekly_rest_blocks(acts)
    data_end = acts[-1].end
    result: list[CompensationDebt] = []

    for block in weekly_rests:
        if not (WEEKLY_REST_MIN <= block.duration < REGULAR_WEEKLY_REST_MIN):
            continue
        shortage = REGULAR_WEEKLY_REST_MIN - block.duration
        deadline = compensation_deadline_for_reduced_weekly_rest(block.start)
        check_end = min(deadline, data_end)
        period_issues = continuity_issues_between(issues, block.end, check_end) if check_end > block.end else []

        found_period = "-"
        for candidate in blocks:
            if candidate.start < block.end:
                continue
            if candidate.end > deadline:
                continue
            # Kompensata musi byc odebrana jednorazowo i dolaczona do innego odpoczynku min. 9 h.
            if candidate.duration >= dt.timedelta(hours=9) + shortage:
                found_period = f"{candidate.start:%Y-%m-%d %H:%M} - {candidate.end:%Y-%m-%d %H:%M} ({fmt_hours(candidate.duration)})"
                break

        continuity_status = "OK - brak luk w kontrolowanym okresie" if not period_issues else f"niepewne - luki/nieznane okresy: {len(period_issues)}"
        if found_period != "-":
            status = "odebrano"
            recommendation = "Wykryto odpoczynek wystarczajacy do dolaczenia rekompensaty. Zweryfikuj, czy nie zostal juz przypisany do innego skrocenia."
        elif period_issues:
            status = "nie mozna potwierdzic"
            recommendation = (
                f"Nie da sie potwierdzic odbioru przez brak ciaglosci danych. Termin ustawowy: do {fmt_deadline(deadline)}. "
                "Uzupelnij brakujace dane przed kwalifikacja naruszenia."
            )
        elif data_end < deadline:
            status = "do odebrania"
            recommendation = f"Rekompensate {fmt_hours(shortage)} nalezy odebrac jednorazowo do {fmt_deadline(deadline)} i dolaczyc do odpoczynku min. 09:00."
        else:
            status = "nieodebrano w terminie"
            recommendation = f"Rekompensate {fmt_hours(shortage)} nalezalo odebrac najpozniej do {fmt_deadline(deadline)}. W danych nie znaleziono odbioru."

        result.append(CompensationDebt(
            reduced_rest_start=block.start,
            reduced_rest_end=block.end,
            reduced_rest_duration=block.duration,
            shortage=shortage,
            deadline=deadline,
            status=status,
            compensation_period=found_period,
            continuity_status=continuity_status,
            recommendation=recommendation,
        ))
    return sorted(result, key=lambda x: x.reduced_rest_start)


def check_weekly_rest_compensation_violations(activities: list[Activity]) -> list[Violation]:
    result: list[Violation] = []
    for debt in analyze_weekly_rest_compensations(activities):
        if debt.status == "odebrano":
            continue
        severity = "wysokie" if debt.status == "nieodebrano w terminie" else "do weryfikacji"
        result.append(Violation(
            "Rekompensata skroconego odpoczynku tygodniowego",
            severity,
            debt.reduced_rest_start,
            debt.deadline,
            f"brakujacy odpoczynek: {fmt_hours(debt.shortage)}; status: {debt.status}",
            f"odbior en bloc do {fmt_deadline(debt.deadline)}, dolaczony do odpoczynku min. 09:00",
            debt.recommendation,
        ))
    return result


def continuity_report_row(issue: ContinuityIssue) -> list[str]:
    return [
        f"{issue.start:%Y-%m-%d %H:%M}",
        f"{issue.end:%Y-%m-%d %H:%M}",
        fmt_hours(issue.duration),
        issue.kind,
        issue.impact,
        issue.recommendation,
    ]


def compensation_report_row(item: CompensationDebt) -> list[str]:
    return [
        f"{item.reduced_rest_start:%Y-%m-%d %H:%M}",
        f"{item.reduced_rest_end:%Y-%m-%d %H:%M}",
        fmt_hours(item.reduced_rest_duration),
        fmt_hours(item.shortage),
        fmt_deadline(item.deadline),
        item.status,
        item.compensation_period,
        item.continuity_status,
        item.recommendation,
    ]


REST_FILTER_OPTIONS = [
    "Wszystkie odpoczynki",
    "Dobowe - wszystkie",
    "Dobowe pelne",
    "Dobowe skrocone",
    "Tygodniowe - wszystkie",
    "Tygodniowe pelne",
    "Tygodniowe skrocone",
]


def _compensation_lookup(compensations: list[CompensationDebt]) -> dict[tuple[dt.datetime, dt.datetime], CompensationDebt]:
    return {(c.reduced_rest_start, c.reduced_rest_end): c for c in compensations}


def classify_rest_block(
    block: Activity,
    compensations: list[CompensationDebt],
    effective_weekly_keys: Optional[set[tuple[dt.datetime, dt.datetime]]] = None,
    ignored_weekly_keys: Optional[set[tuple[dt.datetime, dt.datetime]]] = None,
) -> RestOverview:
    """Classify a continuous REST block for browsing in the GUI.

    Zasada widoku: jeden ciagly blok odpoczynku jest pokazany raz, w najwyzszej
    kategorii, ktora spelnia. Dlatego odpoczynek 45h jest pokazywany jako
    tygodniowy pelny, a nie jednoczesnie jako dobowy. Od v12 blok 24-45h nie jest
    automatycznie pokazywany jako skrocony tygodniowy, jezeli jest tylko
    dodatkowym dlugim odpoczynkiem po regularnym odpoczynku tygodniowym.
    """
    duration = block.duration
    key = weekly_rest_key(block)
    effective_weekly_keys = effective_weekly_keys or set()
    ignored_weekly_keys = ignored_weekly_keys or set()
    comp = _compensation_lookup(compensations).get((block.start, block.end))
    if duration >= REGULAR_WEEKLY_REST_MIN:
        return RestOverview(
            block.start,
            block.end,
            duration,
            "Tygodniowy pelny",
            "OK",
            "-",
            "Regularny odpoczynek tygodniowy minimum 45:00. Moze wystapic w dowolnym dniu tygodnia.",
        )
    if duration >= WEEKLY_REST_MIN and (key in effective_weekly_keys or comp):
        if comp:
            deadline = fmt_deadline(comp.deadline)
            status = comp.status
            extra = f"Brakujaca rekompensata: {fmt_hours(comp.shortage)}; termin: {deadline}"
            note = comp.recommendation
        else:
            status = "do rekompensaty"
            extra = "wymaga wyliczenia rekompensaty"
            note = "Skrocony odpoczynek tygodniowy minimum 24:00 i ponizej 45:00. Moze wystapic w dowolnym dniu tygodnia. Nalezy odebrac brakujacy czas jednorazowo."
        return RestOverview(block.start, block.end, duration, "Tygodniowy skrocony", status, extra, note)
    if duration >= WEEKLY_REST_MIN and key in ignored_weekly_keys:
        return RestOverview(
            block.start,
            block.end,
            duration,
            "Dobowy pelny",
            "OK - dodatkowy dlugi odpoczynek",
            "bez rekompensaty tygodniowej",
            "Blok trwa ponad 24:00, ale nie zostal zaliczony jako skrocony odpoczynek tygodniowy, bo poprzedni regularny odpoczynek tygodniowy zamknal juz cykl. Program nie tworzy dla niego dlugu rekompensaty.",
        )
    if duration >= dt.timedelta(hours=11):
        return RestOverview(
            block.start,
            block.end,
            duration,
            "Dobowy pelny",
            "OK",
            "-",
            "Regularny odpoczynek dobowy minimum 11:00.",
        )
    if duration >= dt.timedelta(hours=9):
        return RestOverview(
            block.start,
            block.end,
            duration,
            "Dobowy skrocony",
            "skrocony",
            "limit kontrolny: maks. 3 skrocenia w przyblizonym tygodniu/okresie",
            "Skrocony odpoczynek dobowy minimum 09:00 i ponizej 11:00.",
        )
    return RestOverview(
        block.start,
        block.end,
        duration,
        "Ponizej progu odpoczynku dobowego",
        "do weryfikacji",
        "ponizej 09:00",
        "Blok REST jest za krotki, aby samodzielnie traktowac go jako odpoczynek dobowy.",
    )


def analyze_rest_overview(activities: list[Activity], compensations: Optional[list[CompensationDebt]] = None) -> list[RestOverview]:
    compensations = compensations or []
    acts = sorted(activities, key=lambda a: a.start)
    blocks = rest_blocks(acts)
    effective_keys = {weekly_rest_key(block) for block in effective_weekly_rest_blocks(acts)}
    ignored_keys = ignored_reduced_weekly_rest_keys(acts)
    result = [
        classify_rest_block(block, compensations, effective_keys, ignored_keys)
        for block in blocks
        if block.duration >= dt.timedelta(hours=9)
    ]
    return sorted(result, key=lambda item: item.start)


def rest_matches_filter(item: RestOverview, selected_filter: str) -> bool:
    f = selected_filter or "Wszystkie odpoczynki"
    rest_type = item.rest_type.lower()
    if f == "Wszystkie odpoczynki":
        return True
    if f == "Dobowe - wszystkie":
        return rest_type.startswith("dobowy")
    if f == "Dobowe pelne":
        return item.rest_type == "Dobowy pelny"
    if f == "Dobowe skrocone":
        return item.rest_type == "Dobowy skrocony"
    if f == "Tygodniowe - wszystkie":
        return rest_type.startswith("tygodniowy")
    if f == "Tygodniowe pelne":
        return item.rest_type == "Tygodniowy pelny"
    if f == "Tygodniowe skrocone":
        return item.rest_type == "Tygodniowy skrocony"
    return True


def rest_overview_row(item: RestOverview) -> list[str]:
    return [
        item.rest_type,
        f"{item.start:%Y-%m-%d %H:%M}",
        f"{item.end:%Y-%m-%d %H:%M}",
        fmt_hours(item.duration),
        item.status,
        item.deadline_or_compensation,
        item.note,
    ]


def export_rests_report_csv(path: Path, rests: list[RestOverview]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f, delimiter=";")
        writer.writerow(["Typ odpoczynku", "Start", "Koniec", "Czas trwania", "Status", "Termin/rekompensata", "Uwagi"])
        for item in rests:
            writer.writerow(rest_overview_row(item))


def analyze_daily_summaries(activities: list[Activity]) -> list[DailySummary]:
    """Aggregate all activities per calendar day.

    Aktywności przechodzace przez polnoc sa dzielone na czesci dzienne, aby
    tabela nie zawyzala czasu na dniu rozpoczecia aktywności.
    """
    buckets: dict[dt.date, dict[str, Any]] = {}
    for activity in sorted(activities, key=lambda a: a.start):
        if activity.end <= activity.start:
            continue
        current = activity.start
        while current < activity.end:
            next_midnight = dt.datetime.combine(current.date() + dt.timedelta(days=1), dt.time.min)
            part_end = min(activity.end, next_midnight)
            duration = part_end - current
            day = current.date()
            bucket = buckets.setdefault(day, {
                "DRIVING": dt.timedelta(0),
                "REST": dt.timedelta(0),
                "AVAILABILITY": dt.timedelta(0),
                "WORK": dt.timedelta(0),
                "UNKNOWN": dt.timedelta(0),
                "first": None,
                "last": None,
                "entries": 0,
            })
            kind = activity.kind if activity.kind in {"DRIVING", "REST", "AVAILABILITY", "WORK"} else "UNKNOWN"
            bucket[kind] += duration
            bucket["entries"] += 1
            bucket["first"] = current if bucket["first"] is None else min(bucket["first"], current)
            bucket["last"] = part_end if bucket["last"] is None else max(bucket["last"], part_end)
            current = part_end

    result: list[DailySummary] = []
    for day in sorted(buckets):
        b = buckets[day]
        total = b["DRIVING"] + b["REST"] + b["AVAILABILITY"] + b["WORK"] + b["UNKNOWN"]
        result.append(DailySummary(
            day=day,
            driving=b["DRIVING"],
            break_rest=b["REST"],
            availability=b["AVAILABILITY"],
            work=b["WORK"],
            unknown=b["UNKNOWN"],
            total_recorded=total,
            first_activity=b["first"],
            last_activity=b["last"],
            entries=int(b["entries"]),
        ))
    return result


def daily_summary_row(item: DailySummary) -> list[str]:
    return [
        f"{item.day:%Y-%m-%d}",
        fmt_hours(item.driving),
        fmt_hours(item.break_rest),
        fmt_hours(item.availability),
        fmt_hours(item.work),
        fmt_hours(item.unknown),
        fmt_hours(item.total_recorded),
        f"{item.first_activity:%Y-%m-%d %H:%M}" if item.first_activity else "-",
        f"{item.last_activity:%Y-%m-%d %H:%M}" if item.last_activity else "-",
        str(item.entries),
    ]


def daily_summary_row_gui(item: DailySummary) -> list[str]:
    return [
        f"{item.day:%Y-%m-%d}",
        f"{activity_icon('DRIVING')} {fmt_hours(item.driving)}",
        f"{activity_icon('REST')} {fmt_hours(item.break_rest)}",
        f"{activity_icon('AVAILABILITY')} {fmt_hours(item.availability)}",
        f"{activity_icon('WORK')} {fmt_hours(item.work)}",
        f"{activity_icon('UNKNOWN')} {fmt_hours(item.unknown)}",
        fmt_hours(item.total_recorded),
        f"{item.first_activity:%Y-%m-%d %H:%M}" if item.first_activity else "-",
        f"{item.last_activity:%Y-%m-%d %H:%M}" if item.last_activity else "-",
        str(item.entries),
    ]


def export_daily_report_csv(path: Path, daily_summaries: list[DailySummary]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f, delimiter=";")
        writer.writerow(["Data", "Jazda", "Przerwa/odpoczynek", "Dyspozycyjność", "Inna praca", "Nieznane", "Suma zapisu", "Pierwsza aktywność", "Ostatnia aktywność", "Liczba wpisów/części"])
        for item in daily_summaries:
            writer.writerow(daily_summary_row(item))


def _find_country_field(record: dict[str, Any]) -> Any:
    preferred = (
        "dailyworkperiodcountry", "country_code", "countrycode", "nation_code", "nationcode",
        "country", "nation", "memberstate", "member_state", "placecountry",
    )
    for key, value in record.items():
        lower = key.lower()
        if any(name in lower for name in preferred):
            if isinstance(value, dict):
                for inner in ("alpha", "code", "value", "name", "country", "nation"):
                    if inner in value:
                        return value[inner]
            return value
    return None


def _find_entry_type_field(record: dict[str, Any], path: str) -> str:
    for key, value in record.items():
        lower = key.lower()
        if "entrytype" in lower or lower in {"type", "entry_type", "kind"}:
            if isinstance(value, int):
                return _entry_type_label(value)
            if isinstance(value, str):
                txt = value.strip()
                if txt.isdigit():
                    return _entry_type_label(int(txt))
                return txt
    p = path.lower()
    if "border" in p:
        return "Przekroczenie granicy"
    if "end" in p or "finish" in p or "withdraw" in p:
        return "Zakonczenie"
    if "begin" in p or "start" in p or "insert" in p:
        return "Rozpoczecie"
    return "Nieustalony typ wpisu"


def _country_entry_note(entry: CountryCodeEntry, previous: Optional[CountryCodeEntry], next_entry: Optional[CountryCodeEntry]) -> tuple[str, str]:
    notes: list[str] = []
    status = entry.status
    if entry.country_code == "---":
        status = "brak kodu kraju"
        notes.append("Zdarzenie wlozenia/wyjecia karty nie zawiera kodu kraju.")
    if entry.country_code == "PL":
        notes.append("Kod kraju: PL - Polska.")
    if is_end_country_entry(entry.entry_type):
        notes.append("Kod kraju zapisany przy wyjeciu karty z tachografu.")
        if previous and is_start_country_entry(previous.entry_type) and previous.timestamp.date() == entry.timestamp.date():
            notes.append(f"Poprzednie wlozenie karty tego dnia: {previous.country_code} {previous.timestamp:%H:%M}.")
        elif previous:
            notes.append(f"Poprzedni wpis karty: {previous.country_code} {previous.timestamp:%Y-%m-%d %H:%M}.")
    elif is_start_country_entry(entry.entry_type):
        notes.append("Kod kraju zapisany przy wlozeniu karty do tachografu.")
        if next_entry and is_end_country_entry(next_entry.entry_type) and next_entry.timestamp.date() == entry.timestamp.date():
            notes.append(f"Powiazane wyjecie karty tego dnia: {next_entry.country_code} {next_entry.timestamp:%H:%M}.")
    if not notes:
        notes.append("Rzeczywisty wpis kodu kraju z danych tachografu.")
    if status == "OK" and entry.country_code == "PL":
        status = "PL"
    return status, " ".join(notes)


def extract_country_code_entries(data: Any) -> list[CountryCodeEntry]:
    """Extract tzw. PL-ki/country-code entries from embedded-parser JSON or common parser outputs."""
    result: list[CountryCodeEntry] = []
    seen: set[tuple[dt.datetime, str, str, str]] = set()

    for path, node in walk_json(data):
        if not isinstance(node, dict):
            continue
        path_lower = path.lower()
        if "country_code_entries" in path_lower:
            when = parse_datetime(node.get("timestamp") or node.get("entry_time") or node.get("EntryTime"))
            code_value = node.get("country_code") or node.get("country") or node.get("DailyWorkPeriodCountry")
            entry_type = str(node.get("entry_type") or "Nieustalony typ wpisu")
        else:
            if not any(token in path_lower for token in ("place", "country", "nation", "location", "border")):
                continue
            when = find_first_datetime(node, ("entrytime", "entry_time", "time", "date", "timestamp"))
            code_value = _find_country_field(node)
            entry_type = _find_entry_type_field(node, path)
        if not when or code_value is None:
            continue
        code = normalize_country_code(code_value)
        key = (when, entry_type, code, path)
        if key in seen:
            continue
        seen.add(key)
        source = mark_source_as_manual(path) if (node_has_manual_entry_marker(node) or is_manual_entry_text(entry_type)) else path
        result.append(CountryCodeEntry(
            timestamp=when,
            entry_type=entry_type,
            country_code=code,
            country_name=country_name_pl(code),
            status="PL" if code == "PL" else ("brak kodu kraju" if code == "---" else "OK"),
            related_day=f"{when:%Y-%m-%d}",
            note="",
            source=source,
        ))

    ordered = sorted(result, key=lambda x: x.timestamp)
    enriched: list[CountryCodeEntry] = []
    for idx, entry in enumerate(ordered):
        previous = ordered[idx - 1] if idx > 0 else None
        next_entry = ordered[idx + 1] if idx + 1 < len(ordered) else None
        status, note = _country_entry_note(entry, previous, next_entry)
        enriched.append(CountryCodeEntry(
            timestamp=entry.timestamp,
            entry_type=card_country_entry_display_type(entry.entry_type),
            country_code=entry.country_code,
            country_name=entry.country_name,
            status=status,
            related_day=entry.related_day,
            note=entry.note or note,
            source=entry.source,
        ))
    return enriched


def daily_summary_has_active_work_period(day_item: DailySummary) -> bool:
    """Return True only for days that should be checked for start/end country code entries.

    Dni zawierajace wyłącznie odpoczynek/przerwe nie tworza dziennego okresu
    pracy do analizy PL-ek. Dzięki temu aplikacja nie generuje falszywych
    brakow kodu kraju rozpoczecia lub zakonczenia dla pustych dni albo dni
    z samym odpoczynkiem. UNKNOWN traktujemy ostrożnościowo jako aktywnosc,
    bo brak interpretacji nie powinien automatycznie zwalniac z kontroli.
    """
    active = day_item.driving + day_item.availability + day_item.work + day_item.unknown
    return active > dt.timedelta(0)


def analyze_country_code_entries(entries: list[CountryCodeEntry], daily_summaries: list[DailySummary]) -> list[CountryCodeEntry]:
    """Return only real card insertion/withdrawal country-code entries.

    Earlier versions generated synthetic rows for every active day without start/end
    country entries. That produced false warnings at 00:00. This version does not
    infer missing PL-ki from daily activity. It shows only the country code saved
    when the card was put into or removed from the tachograph. If such a real
    card event has an empty country code, the row remains visible as
    "brak kodu kraju".
    """
    del daily_summaries  # The PL-ki view must not create day-based synthetic rows.
    filtered = [entry for entry in entries if is_card_insert_or_withdraw_country_entry(entry.entry_type)]
    return sorted(filtered, key=lambda x: (x.timestamp, x.entry_type))


def country_code_row(item: CountryCodeEntry) -> list[str]:
    entry_type = with_manual_entry_icon(
        card_country_entry_display_type(item.entry_type),
        item.entry_type,
        item.source,
        item.note,
    )
    return [
        f"{item.timestamp:%Y-%m-%d %H:%M}",
        entry_type,
        item.country_code,
        item.country_name,
        item.status,
        item.related_day,
        item.note,
    ]


def export_country_codes_report_csv(path: Path, entries: list[CountryCodeEntry]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f, delimiter=";")
        writer.writerow(["Czas wpisu", "Rodzaj wpisu", "Kod kraju", "Kraj", "Status", "Dzien pracy", "Uwagi"])
        for item in entries:
            writer.writerow(country_code_row(item))


def export_continuity_report_csv(path: Path, continuity: list[ContinuityIssue], compensations: list[CompensationDebt]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f, delimiter=";")
        writer.writerow(["REKOMPENSATY SKROCONYCH ODPOCZYNKOW TYGODNIOWYCH"])
        writer.writerow(["Start skroconego odpoczynku", "Koniec", "Czas odpoczynku", "Brakujaca rekompensata", "Termin odbioru", "Status", "Odebrano w okresie", "Ciaglosc danych", "Rekomendacja"])
        for item in compensations:
            writer.writerow(compensation_report_row(item))
        writer.writerow([])
        writer.writerow(["CIAGLOSC DANYCH"])
        writer.writerow(["Start", "Koniec", "Czas", "Typ", "Wplyw", "Zalecenie"])
        for issue in continuity:
            writer.writerow(continuity_report_row(issue))


def ceil_minutes(delta: dt.timedelta) -> int:
    seconds = max(0, int(delta.total_seconds()))
    return (seconds + 59) // 60


def ceil_started_units(delta: dt.timedelta, unit_minutes: int) -> int:
    minutes = ceil_minutes(delta)
    return max(1, (minutes + unit_minutes - 1) // unit_minutes)


def parse_hhmm_from_text(text: str) -> Optional[dt.timedelta]:
    match = re.search(r"(\d{1,3}):(\d{2})", text or "")
    if not match:
        return None
    return dt.timedelta(hours=int(match.group(1)), minutes=int(match.group(2)))


def money_label(amount: int, units: int = 1, rate: Optional[int] = None) -> str:
    if rate is not None and units > 1:
        return f"{rate} zl x {units} = {amount} zl"
    return f"{amount} zl"


def tariff_default(note: str = "Wymaga recznej kwalifikacji po kontroli pelnego okresu i wyjatkow.") -> TariffInfo:
    return TariffInfo(
        amount="do oceny",
        code="-",
        category="manualnie",
        basis="-",
        note=note,
        amount_zl=None,
    )


def tariff_daily_over_9(excess: dt.timedelta) -> TariffInfo:
    if excess <= dt.timedelta(minutes=15):
        return TariffInfo("0 zl", "-", "ponizej progu", "zal. nr 3 poz. 5.1", "Przekroczenie nie przekracza progu taryfikatora powyzej 15 minut.", 0)
    if excess < dt.timedelta(hours=1):
        return TariffInfo("50 zl", "-", "naruszenie", "zal. nr 3 poz. 5.1 pkt 1", "Przekroczenie powyzej 15 minut do mniej niz 1 godziny.", 50)
    if excess < dt.timedelta(hours=2):
        return TariffInfo("150 zl", "1.2 PN", "powazne naruszenie", "zal. nr 3 poz. 5.1 pkt 2", "Przekroczenie od 1 godziny do mniej niz 2 godzin.", 150)
    units = ceil_started_units(excess - dt.timedelta(hours=2), 60)
    amount = units * 200
    return TariffInfo(money_label(amount, units, 200), "1.3 BPN", "bardzo powazne naruszenie", "zal. nr 3 poz. 5.1 pkt 3", "Stawka za kazda rozpoczeta godzine od 2 godzin przekroczenia.", amount)


def tariff_daily_over_10(excess: dt.timedelta) -> TariffInfo:
    if excess < dt.timedelta(hours=1):
        return TariffInfo("100 zl", "-", "naruszenie", "zal. nr 3 poz. 5.2 pkt 1", "Przekroczenie do mniej niz 1 godziny.", 100)
    if excess < dt.timedelta(hours=2):
        return TariffInfo("200 zl", "1.5 PN", "powazne naruszenie", "zal. nr 3 poz. 5.2 pkt 2", "Przekroczenie od 1 godziny do mniej niz 2 godzin.", 200)
    units = ceil_started_units(excess - dt.timedelta(hours=2), 60)
    amount = units * 250
    return TariffInfo(money_label(amount, units, 250), "1.6 BPN", "bardzo powazne naruszenie", "zal. nr 3 poz. 5.2 pkt 3", "Stawka za kazda rozpoczeta godzine od 2 godzin przekroczenia.", amount)


def tariff_weekly_driving(excess: dt.timedelta) -> TariffInfo:
    if excess <= dt.timedelta(minutes=30):
        return TariffInfo("0 zl", "-", "ponizej progu", "zal. nr 3 poz. 5.3", "Przekroczenie nie przekracza progu powyzej 30 minut.", 0)
    if excess < dt.timedelta(hours=4):
        return TariffInfo("150 zl", "-", "naruszenie", "zal. nr 3 poz. 5.3 pkt 1", "Przekroczenie powyzej 30 minut do mniej niz 4 godzin.", 150)
    if excess < dt.timedelta(hours=9):
        return TariffInfo("250 zl", "1.8 PN", "powazne naruszenie", "zal. nr 3 poz. 5.3 pkt 2", "Przekroczenie od 4 godzin do mniej niz 9 godzin.", 250)
    if excess < dt.timedelta(hours=14):
        return TariffInfo("350 zl", "1.9 BPN", "bardzo powazne naruszenie", "zal. nr 3 poz. 5.3 pkt 3", "Przekroczenie od 9 godzin do mniej niz 14 godzin.", 350)
    units = ceil_started_units(excess - dt.timedelta(hours=14), 60)
    amount = units * 550
    return TariffInfo(money_label(amount, units, 550), "1.10 NN", "najpowazniejsze naruszenie", "zal. nr 3 poz. 5.3 pkt 4", "Stawka za kazda rozpoczeta godzine od 14 godzin przekroczenia.", amount)


def tariff_two_week_driving(excess: dt.timedelta) -> TariffInfo:
    if excess < dt.timedelta(hours=10):
        return TariffInfo("250 zl", "-", "naruszenie", "zal. nr 3 poz. 5.4 pkt 1", "Przekroczenie do mniej niz 10 godzin.", 250)
    if excess < dt.timedelta(hours=15):
        return TariffInfo("350 zl", "1.11 PN", "powazne naruszenie", "zal. nr 3 poz. 5.4 pkt 2", "Przekroczenie od 10 godzin do mniej niz 15 godzin.", 350)
    if excess < dt.timedelta(hours=22, minutes=30):
        return TariffInfo("550 zl", "1.12 BPN", "bardzo powazne naruszenie", "zal. nr 3 poz. 5.4 pkt 3", "Przekroczenie od 15 godzin do mniej niz 22 godzin i 30 minut.", 550)
    units = ceil_started_units(excess - dt.timedelta(hours=22, minutes=30), 60)
    amount = units * 700
    return TariffInfo(money_label(amount, units, 700), "1.13 NN", "najpowazniejsze naruszenie", "zal. nr 3 poz. 5.4 pkt 4", "Stawka za kazda rozpoczeta godzine od 22 godzin i 30 minut przekroczenia.", amount)


def tariff_break(excess: dt.timedelta) -> TariffInfo:
    if excess < dt.timedelta(minutes=30):
        return TariffInfo("100 zl", "-", "naruszenie", "zal. nr 3 poz. 5.11 pkt 1", "Przekroczenie czasu prowadzenia bez przerwy do mniej niz 30 minut.", 100)
    if excess < dt.timedelta(hours=1, minutes=30):
        return TariffInfo("250 zl", "1.14 PN", "powazne naruszenie", "zal. nr 3 poz. 5.11 pkt 2", "Przekroczenie od 30 minut do mniej niz 1 godziny i 30 minut.", 250)
    units = ceil_started_units(excess - dt.timedelta(hours=1, minutes=30), 30)
    amount = units * 350
    return TariffInfo(money_label(amount, units, 350), "1.15 BPN", "bardzo powazne naruszenie", "zal. nr 3 poz. 5.11 pkt 3", "Stawka za kazde rozpoczęte 30 minut od 1 godziny i 30 minut przekroczenia.", amount)


def tariff_regular_daily_rest(shortage: dt.timedelta) -> TariffInfo:
    if shortage <= dt.timedelta(hours=1):
        return TariffInfo("100 zl", "-", "naruszenie", "zal. nr 3 poz. 5.5 pkt 1", "Skrocenie wymaganego regularnego odpoczynku dziennego do 1 godziny.", 100)
    if shortage <= dt.timedelta(hours=2, minutes=30):
        return TariffInfo("200 zl", "1.16 PN", "powazne naruszenie", "zal. nr 3 poz. 5.5 pkt 2", "Skrocenie powyzej 1 godziny do 2 godzin i 30 minut.", 200)
    units = ceil_started_units(shortage - dt.timedelta(hours=2, minutes=30), 60)
    amount = units * 350
    return TariffInfo(money_label(amount, units, 350), "1.17 BPN", "bardzo powazne naruszenie", "zal. nr 3 poz. 5.5 pkt 3", "Stawka za kazda rozpoczeta godzine powyzej 2 godzin i 30 minut skrocenia.", amount)


def tariff_reduced_daily_rest(shortage: dt.timedelta) -> TariffInfo:
    if shortage <= dt.timedelta(hours=1):
        return TariffInfo("150 zl", "-", "naruszenie", "zal. nr 3 poz. 5.7 pkt 1", "Skrocenie wymaganego skroconego odpoczynku dziennego do 1 godziny.", 150)
    if shortage <= dt.timedelta(hours=2):
        return TariffInfo("350 zl", "1.18/1.22 PN", "powazne naruszenie", "zal. nr 3 poz. 5.7 pkt 2", "Skrocenie powyzej 1 godziny do 2 godzin.", 350)
    units = ceil_started_units(shortage - dt.timedelta(hours=2), 60)
    amount = units * 550
    return TariffInfo(money_label(amount, units, 550), "1.19/1.23 BPN", "bardzo powazne naruszenie", "zal. nr 3 poz. 5.7 pkt 3", "Stawka za kazda rozpoczeta godzine powyzej 2 godzin skrocenia.", amount)


def tariff_weekly_rest_shortened(shortage: dt.timedelta, potential: bool) -> TariffInfo:
    prefix = "potencjalnie " if potential else ""
    amount_zl: Optional[int]
    if shortage <= dt.timedelta(hours=3):
        amount = 150
        label = f"{prefix}{amount} zl"
        code = "-"
        category = "naruszenie"
        basis = "zal. nr 3 poz. 5.8 pkt 1"
        note = "Skrocenie regularnego odpoczynku tygodniowego do 3 godzin. Kara tylko jesli skrocenie nie bylo dozwolone."
    elif shortage <= dt.timedelta(hours=9):
        amount = 300
        label = f"{prefix}{amount} zl"
        code = "1.26 PN"
        category = "powazne naruszenie"
        basis = "zal. nr 3 poz. 5.8 pkt 2"
        note = "Skrocenie powyzej 3 godzin do 9 godzin. Kara tylko jesli skrocenie nie bylo dozwolone."
    else:
        units = ceil_started_units(shortage - dt.timedelta(hours=9), 60)
        amount = units * 400
        label = f"{prefix}{money_label(amount, units, 400)}"
        code = "1.27 BPN"
        category = "bardzo powazne naruszenie"
        basis = "zal. nr 3 poz. 5.8 pkt 3"
        note = "Stawka za kazda rozpoczeta godzine powyzej 9 godzin skrocenia. Kara tylko jesli skrocenie nie bylo dozwolone."
    amount_zl = None if potential else amount
    return TariffInfo(label, code, category, basis, note, amount_zl)


def tariff_weekly_rest_deadline(excess: dt.timedelta) -> TariffInfo:
    if excess < dt.timedelta(hours=3):
        return TariffInfo("350 zl", "-", "naruszenie", "zal. nr 3 poz. 5.10 pkt 1", "Przekroczenie 6 kolejnych okresow 24-godzinnych do mniej niz 3 godzin.", 350)
    if excess < dt.timedelta(hours=12):
        return TariffInfo("450 zl", "1.28 PN", "powazne naruszenie", "zal. nr 3 poz. 5.10 pkt 2", "Przekroczenie od 3 godzin do mniej niz 12 godzin.", 450)
    return TariffInfo("550 zl", "1.28 BPN", "bardzo powazne naruszenie", "zal. nr 3 poz. 5.10 pkt 3", "Przekroczenie od 12 godzin.", 550)


def tariff_for_violation(v: Violation) -> TariffInfo:
    rule = v.rule.lower()
    description = v.description.lower()
    value_td = parse_hhmm_from_text(v.value)

    if "przerwa po 4h30" in rule:
        driving = value_td
        if driving is None:
            return tariff_default("Nie udalo sie odczytac dlugosci jazdy bez przerwy.")
        return tariff_break(driving - dt.timedelta(hours=4, minutes=30))

    if "dzienny czas jazdy" in rule:
        total = value_td
        if total is None:
            return tariff_default("Nie udalo sie odczytac dziennego czasu jazdy.")
        if "10 godzin" in description or total > dt.timedelta(hours=10):
            return tariff_daily_over_10(total - dt.timedelta(hours=10))
        return tariff_daily_over_9(total - dt.timedelta(hours=9))

    if "tygodniowy czas jazdy" in rule:
        total = value_td
        if total is None:
            return tariff_default("Nie udalo sie odczytac tygodniowego czasu jazdy.")
        return tariff_weekly_driving(total - dt.timedelta(hours=56))

    if "dwutygodniowy czas jazdy" in rule:
        total = value_td
        if total is None:
            return tariff_default("Nie udalo sie odczytac dwutygodniowego czasu jazdy.")
        return tariff_two_week_driving(total - dt.timedelta(hours=90))

    if "skrocony odpoczynek dobowy" in rule:
        duration = value_td
        if duration is None:
            return tariff_default("Nie udalo sie odczytac dlugosci odpoczynku dobowego.")
        # Raport wykrywa czwarte i kolejne skrocenia 9-11h. Taryfikator dla kary
        # zalezy od kwalifikacji: regularny odpoczynek skrocony albo skrocony odpoczynek
        # ponizej 9h. Dla tego przypadku pokazujemy regularny odpoczynek 11h jako punkt odniesienia.
        return tariff_regular_daily_rest(dt.timedelta(hours=11) - duration)

    if "odpoczynek dobowy" in rule:
        if "brak >=09" in v.value:
            return TariffInfo("do oceny", "5.7", "manualnie", "zal. nr 3 poz. 5.7", "Brak odpoczynku co najmniej 9h moze oznaczac skrocenie wymaganego skroconego odpoczynku dziennego; kwota zalezy od rzeczywistego skrocenia.", None)
        return tariff_default("Naruszenie odpoczynku dobowego wymaga ustalenia faktycznej dlugosci odpoczynku w okresie 24h.")

    if "rekompensata skroconego odpoczynku tygodniowego" in rule:
        return tariff_default("Brak lub brak potwierdzenia odbioru rekompensaty wymaga oceny razem ze skroconym odpoczynkiem tygodniowym i pelnym okresem kontroli.")

    if "skrocony odpoczynek tygodniowy" in rule:
        duration = value_td
        if duration is None:
            return tariff_default("Nie udalo sie odczytac dlugosci odpoczynku tygodniowego.")
        return tariff_weekly_rest_shortened(dt.timedelta(hours=45) - duration, potential=True)

    if "termin odpoczynku tygodniowego" in rule:
        return tariff_weekly_rest_deadline((v.end - v.start) - dt.timedelta(days=6))

    if "odpoczynek tygodniowy" in rule:
        return TariffInfo("do oceny", "5.8/5.9/5.10", "manualnie", "zal. nr 3 poz. 5.8-5.10", "Brak odpoczynku tygodniowego wymaga sprawdzenia pelnego okresu danych i poprzedniego odpoczynku.", None)

    return tariff_default()


def estimated_total_penalty(violations: list[Violation]) -> int:
    total = 0
    for violation in violations:
        info = tariff_for_violation(violation)
        if info.amount_zl is not None:
            total += info.amount_zl
    return total


def fmt_date(value: dt.datetime) -> str:
    return value.strftime("%Y-%m-%d")


def fmt_datetime_short(value: dt.datetime) -> str:
    return value.strftime("%Y-%m-%d %H:%M")


def fmt_violation_period(v: Violation) -> str:
    if v.start.date() == v.end.date():
        return f"{v.start:%H:%M} - {v.end:%H:%M}"
    return f"{fmt_datetime_short(v.start)} - {fmt_datetime_short(v.end)}"


def required_rest_start_deadline(v: Violation) -> str:
    """Return the latest legally expected start time for late-rest violations."""
    rule = v.rule.lower()
    if "odpoczynek dobowy w 24h" in rule:
        return f"{v.start + dt.timedelta(hours=24):%Y-%m-%d %H:%M}"
    if "termin odpoczynku tygodniowego" in rule:
        return f"{v.start + dt.timedelta(days=6):%Y-%m-%d %H:%M}"
    if "rekompensata skroconego odpoczynku tygodniowego" in rule:
        return fmt_deadline(v.end)
    return "-"


def actual_rest_start_for_violation(v: Violation) -> str:
    rule = v.rule.lower()
    if "odpoczynek dobowy w 24h" in rule or "termin odpoczynku tygodniowego" in rule:
        return f"{v.end:%Y-%m-%d %H:%M}"
    return "-"


VIOLATION_REPORT_HEADERS = [
    "data naruszenia",
    "czego dotyczy",
    "kara wg taryfikatora",
    "pozycja/kod",
    "kategoria taryfikatora",
    "waga analizy",
    "termin wymagany",
    "start faktyczny",
    "okres",
    "wartosc",
    "limit",
    "opis",
]


def violation_report_row(v: Violation) -> list[str]:
    """Return one row for the control-style violation report table."""
    tariff = tariff_for_violation(v)
    return [
        fmt_date(v.start),
        v.rule,
        tariff.amount,
        tariff.code,
        tariff.category,
        v.severity,
        required_rest_start_deadline(v),
        actual_rest_start_for_violation(v),
        fmt_violation_period(v),
        v.value,
        v.limit,
        v.description,
    ]


ACTIVITY_REPORT_HEADERS = [
    "start",
    "koniec",
    "typ aktywności",
    "czas trwania",
    "pojazd / nr rej.",
]


def activity_report_row(activity: Activity) -> list[str]:
    """Return one row for the activity report table."""
    return [
        fmt_datetime_short(activity.start),
        fmt_datetime_short(activity.end),
        activity_report_label_with_manual_icon(activity),
        fmt_duration_report(activity.duration),
        vehicle_registration_display(activity),
    ]


def activity_totals_by_kind(activities: list[Activity]) -> dict[str, dt.timedelta]:
    totals = {key: dt.timedelta(0) for key in ACTIVITY_LABELS_PL}
    for activity in activities:
        kind = str(activity.kind) if activity.kind else "UNKNOWN"
        if kind not in totals:
            kind = "UNKNOWN"
        totals[kind] += activity.duration
    return totals


def export_activities_report_html(path: Path, activities: list[Activity], range_label: str = "") -> None:
    """Eksportuje czytelny raport aktywności do HTML."""
    def esc(x: Any) -> str:
        return str(x).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

    rows = "\n".join(
        "<tr>" + "".join(f"<td>{esc(cell)}</td>" for cell in activity_report_row(activity)) + "</tr>"
        for activity in activities
    )
    if not rows:
        rows = '<tr><td colspan="5" class="empty">Brak aktywności w analizowanym okresie.</td></tr>'

    totals = activity_totals_by_kind(activities)
    total_rows = "\n".join(
        f"<tr><td>{esc(activity_report_label_with_icon(kind))}</td><td>{esc(fmt_duration_report(total))}</td></tr>"
        for kind, total in totals.items()
        if total > dt.timedelta(0)
    ) or '<tr><td colspan="2" class="empty">Brak zsumowanych aktywności.</td></tr>'

    first_time = esc(fmt_datetime_short(activities[0].start)) if activities else "-"
    last_time = esc(fmt_datetime_short(activities[-1].end)) if activities else "-"
    range_text = esc(range_label) if range_label else "wybrany zakres"
    generated = f"{dt.datetime.now():%Y-%m-%d %H:%M:%S}"

    html = f"""<!doctype html>
<html lang="pl">
<head>
<meta charset="utf-8">
<title>Raport aktywności kierowcy</title>
<style>
body{{font-family:Arial,sans-serif;margin:24px;background:#f5f7fb;color:#172033;}}
.toolbar{{display:flex;justify-content:flex-end;margin:0 0 14px 0;}}
.print-btn{{background:#12284a;color:white;border:0;border-radius:8px;padding:9px 16px;font-weight:bold;cursor:pointer;}}
.print-btn:hover{{background:#1f3a5f;}}
h1{{margin:0 0 4px 0;color:#12284a;}}
h2{{margin-top:22px;color:#1f3a5f;}}
.meta{{margin:2px 0 18px 0;color:#46556b;font-size:13px;}}
.summary{{display:flex;gap:12px;margin:14px 0 18px 0;flex-wrap:wrap;}}
.card{{background:white;border:1px solid #dbe3ef;border-radius:10px;padding:10px 14px;box-shadow:0 1px 4px rgba(18,40,74,.08);}}
.card strong{{display:block;font-size:20px;color:#12284a;}}
table{{border-collapse:collapse;width:100%;background:white;box-shadow:0 2px 8px rgba(18,40,74,.08);border-radius:10px;overflow:hidden;margin-bottom:22px;}}
th,td{{border:1px solid #d7dee8;padding:8px 10px;font-size:12.5px;vertical-align:top;line-height:1.35;}}
th{{background:#12284a;color:white;text-align:left;position:sticky;top:0;z-index:1;}}
tr:nth-child(even){{background:#f3f6fb;}}
td:nth-child(1),td:nth-child(2),td:nth-child(4),td:nth-child(5){{white-space:nowrap;}}
td:nth-child(3){{font-weight:bold;}}
.empty{{text-align:center;color:#5c687a;padding:18px;}}
@media print{{body{{background:white;margin:12mm;}} .card,table{{box-shadow:none;}} th{{position:static;}} .no-print{{display:none!important;}}}}
</style>
</head>
<body>
<div class="toolbar no-print"><button class="print-btn" onclick="window.print()">Drukuj</button></div>
<h1>Raport aktywności</h1>
<p class="meta">Wygenerowano: {generated}</p>
<p class="meta">Zakres raportu: {range_text}</p>
<div class="summary">
  <div class="card"><strong>{len(activities)}</strong>Liczba wpisów aktywności</div>
  <div class="card"><strong>{first_time}</strong>Pierwsza aktywność w raporcie</div>
  <div class="card"><strong>{last_time}</strong>Ostatnia aktywność w raporcie</div>
</div>
<h2>Podsumowanie czasu według aktywności</h2>
<table><thead><tr><th>Aktywność</th><th>Czas łącznie</th></tr></thead><tbody>{total_rows}</tbody></table>
<h2>Szczegółowa lista aktywności</h2>
<table>
<thead><tr><th>Start</th><th>Koniec</th><th>Typ aktywności</th><th>Czas trwania</th><th>Pojazd / nr rej.</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</body>
</html>"""
    path.write_text(html, encoding="utf-8")


def export_activities_report_pdf(path: Path, activities: list[Activity], range_label: str = "") -> None:
    """Eksportuje raport aktywności do PDF w ukladzie graficznym zblizonym do HTML."""
    totals = activity_totals_by_kind(activities)
    total_rows = [
        [activity_report_label_with_icon(kind), fmt_duration_report(total)]
        for kind, total in totals.items()
        if total > dt.timedelta(0)
    ]
    first_time = fmt_datetime_short(activities[0].start) if activities else "-"
    last_time = fmt_datetime_short(activities[-1].end) if activities else "-"
    cards = [
        (str(len(activities)), "Liczba wpisow aktywnosci"),
        (first_time, "Pierwsza aktywnosc w raporcie"),
        (last_time, "Ostatnia aktywnosc w raporcie"),
    ]
    sections = [
        {
            "title": "Podsumowanie czasu wedlug aktywnosci",
            "headers": ["Aktywnosc", "Czas lacznie"],
            "rows": total_rows,
            "col_widths": [520, 274],
            "font_size": 8.0,
            "max_lines": 3,
            "empty_text": "Brak zsumowanych aktywnosci.",
            "bold_cols": [0],
        },
        {
            "title": "Szczegolowa lista aktywnosci",
            "headers": ["Start", "Koniec", "Typ aktywnosci", "Czas trwania", "Pojazd / nr rej."],
            "rows": [activity_report_row(activity) for activity in activities],
            "col_widths": [112, 112, 332, 108, 130],
            "font_size": 7.4,
            "max_lines": 4,
            "empty_text": "Brak aktywnosci w analizowanym okresie.",
            "bold_cols": [2],
        },
    ]
    _write_html_like_report_pdf(path, "Raport aktywnosci", range_label, cards, sections)


def export_activities_chart_report_pdf(path: Path, activities: list[Activity], range_label: str = "") -> None:
    """Eksportuje raport aktywności z wykresem osi czasu do PDF.

    Raport używa wyłącznie standardowej biblioteki Pythona i wbudowanego
    generatora PDF. Wykres dzieli aktywności według dni kalendarzowych i pokazuje
    oś 00:00-24:00 z kolorami zgodnymi z zakładką "Oś czasu" w aplikacji.
    """
    acts = sorted([a for a in activities if a.end > a.start], key=lambda a: (a.start, a.end, a.kind))
    page_w, page_h = 842, 595  # A4 poziomo
    margin = 24
    bottom = 34
    pages: list[list[str]] = []
    ops: list[str] = []
    y = page_h - margin
    page_no = 0

    kind_colors = {
        "DRIVING": (0.85, 0.26, 0.26),
        "WORK": (0.89, 0.60, 0.14),
        "AVAILABILITY": (0.18, 0.50, 0.93),
        "REST": (0.18, 0.62, 0.34),
        "UNKNOWN": (0.55, 0.59, 0.64),
    }
    kind_order = ["DRIVING", "WORK", "AVAILABILITY", "REST", "UNKNOWN"]

    def start_page() -> None:
        nonlocal ops, y, page_no
        page_no += 1
        ops = []
        _pdf_fill_rect(ops, 0, 0, page_w, page_h, (0.98, 0.99, 1.00))
        y = page_h - margin

    def finish_page() -> None:
        _pdf_draw_text(ops, margin, 15, f"Strona {page_no}", 7.5, False, (0.36, 0.41, 0.48))
        pages.append(ops[:])

    def new_page() -> None:
        finish_page()
        start_page()

    def ensure_space(height: float) -> None:
        if y - height < bottom:
            new_page()

    def add_title_block() -> None:
        nonlocal y
        _pdf_draw_text(ops, margin, y - 18, "Raport aktywnosci z wykresem", 18, True, (0.07, 0.16, 0.29))
        y -= 30
        _pdf_draw_text(ops, margin, y - 8, f"Wygenerowano: {dt.datetime.now():%Y-%m-%d %H:%M:%S}", 8.5, False, (0.27, 0.33, 0.42))
        y -= 14
        _pdf_draw_text(ops, margin, y - 8, f"Zakres raportu: {range_label or 'wybrany zakres'}", 8.5, True, (0.27, 0.33, 0.42))
        y -= 20

    def add_cards(cards: list[tuple[str, str]]) -> None:
        nonlocal y
        if not cards:
            return
        ensure_space(56)
        gap = 10
        table_w = page_w - (2 * margin)
        card_w = (table_w - gap * (len(cards) - 1)) / max(1, len(cards))
        card_h = 44
        cur_x = margin
        for value, label in cards:
            _pdf_fill_rect(ops, cur_x, y - card_h, card_w, card_h, (1, 1, 1))
            _pdf_stroke_rect(ops, cur_x, y - card_h, card_w, card_h, (0.86, 0.89, 0.94), 0.8)
            _pdf_draw_text(ops, cur_x + 10, y - 19, value, 13.5, True, (0.07, 0.16, 0.29))
            _pdf_draw_wrapped_text(ops, cur_x + 10, y - 24, label, card_w - 20, 7.3, False, (0.36, 0.41, 0.48), max_lines=2)
            cur_x += card_w + gap
        y -= card_h + 20

    def split_days(all_days: list[dt.date], chunk_size: int = 7) -> list[list[dt.date]]:
        return [all_days[i:i + chunk_size] for i in range(0, len(all_days), chunk_size)]

    def draw_activity_charts(days: list[dt.date]) -> None:
        nonlocal y
        if not days:
            ensure_space(36)
            _pdf_draw_text(ops, margin, y - 14, "Brak dni do pokazania na wykresie.", 10, True, (0.45, 0.50, 0.58))
            y -= 32
            return

        chart_left = 108
        chart_right = 22
        chart_w = page_w - chart_left - chart_right
        row_h = 48
        band_h = 16
        title_h = 56
        legend_h = 34
        chunk_h = title_h + len(days) * row_h + legend_h
        ensure_space(chunk_h)

        first_day = days[0]
        last_day = days[-1]
        _pdf_draw_text(ops, margin, y - 12, f"Wykres osi czasu: {first_day:%Y-%m-%d} - {last_day:%Y-%m-%d}", 12, True, (0.12, 0.23, 0.37))
        y -= 24
        _pdf_draw_text(ops, margin, y - 8, "Skala godzinowa 00:00-24:00. Aktywnosci przechodzace przez polnoc sa dzielone na wlasciwe dni.", 8.0, False, (0.36, 0.41, 0.48))
        y -= 22

        grid_top = y
        grid_bottom = y - len(days) * row_h + 7
        for hour in range(0, 25, 3):
            x = chart_left + (hour / 24.0) * chart_w
            _pdf_draw_line(ops, x, grid_top + 3, x, grid_bottom, (0.83, 0.86, 0.90), 0.45)
            if hour < 24:
                _pdf_draw_text(ops, x + 2, grid_top + 8, f"{hour:02d}:00", 6.8, False, (0.36, 0.41, 0.48))

        visible_segments = 0
        for idx, day in enumerate(days):
            day_start = dt.datetime.combine(day, dt.time.min)
            day_end = day_start + dt.timedelta(days=1)
            row_top = y - idx * row_h
            row_bottom = row_top - row_h + 4
            fill = (1, 1, 1) if idx % 2 == 0 else (0.95, 0.97, 0.99)
            _pdf_fill_rect(ops, margin, row_bottom, page_w - (2 * margin), row_h - 4, fill)
            _pdf_stroke_rect(ops, margin, row_bottom, page_w - (2 * margin), row_h - 4, (0.88, 0.90, 0.93), 0.4)
            _pdf_draw_text(ops, margin + 6, row_top - 15, f"{day:%Y-%m-%d}", 8.2, True, (0.14, 0.20, 0.28))
            _pdf_draw_text(ops, margin + 6, row_top - 29, "00:00-24:00", 6.8, False, (0.42, 0.47, 0.54))
            band_y = row_top - 33
            _pdf_draw_line(ops, chart_left, band_y - 3, chart_left + chart_w, band_y - 3, (0.82, 0.86, 0.91), 0.5)

            for activity in acts:
                clipped_start = max(activity.start, day_start)
                clipped_end = min(activity.end, day_end)
                if clipped_end <= clipped_start:
                    continue
                visible_segments += 1
                kind = activity.kind if activity.kind in kind_colors else "UNKNOWN"
                x1 = chart_left + ((clipped_start - day_start).total_seconds() / 86400.0) * chart_w
                x2 = chart_left + ((clipped_end - day_start).total_seconds() / 86400.0) * chart_w
                if x2 - x1 < 1.2:
                    x2 = x1 + 1.2
                _pdf_fill_rect(ops, x1, band_y, x2 - x1, band_h, kind_colors[kind])
                _pdf_stroke_rect(ops, x1, band_y, x2 - x1, band_h, (0.12, 0.12, 0.12), 0.25)
                segment_width = x2 - x1
                duration = clipped_end - clipped_start
                if segment_width >= 74:
                    label = f"{activity_label(kind)} {fmt_duration_report(duration)}"
                    _pdf_draw_text(ops, x1 + 3, band_y + 5, label, 6.4, True, (1, 1, 1))
                elif segment_width >= 32:
                    _pdf_draw_text(ops, x1 + 2, band_y + 5, fmt_duration_report(duration), 6.0, True, (1, 1, 1))

        y -= len(days) * row_h + 8
        if visible_segments == 0:
            _pdf_draw_text(ops, chart_left, y + 20, "Brak aktywnosci w tym fragmencie zakresu.", 8.5, True, (0.45, 0.50, 0.58))

        legend_x = chart_left
        legend_y = y - 8
        for kind in kind_order:
            _pdf_fill_rect(ops, legend_x, legend_y, 13, 13, kind_colors[kind])
            _pdf_stroke_rect(ops, legend_x, legend_y, 13, 13, (0.12, 0.12, 0.12), 0.25)
            _pdf_draw_text(ops, legend_x + 18, legend_y + 3, activity_label(kind), 7.2, False, (0.14, 0.20, 0.28))
            legend_x += 145
        y -= legend_h

    def add_table(title: str, headers: list[str], rows: list[list[Any]], col_widths: list[float], font_size: float = 7.0, max_lines: int = 5, bold_cols: Optional[set[int]] = None) -> None:
        nonlocal y
        bold_cols = bold_cols or set()
        table_w = page_w - 2 * margin
        if abs(sum(col_widths) - table_w) > 1:
            factor = table_w / sum(col_widths)
            col_widths = [w * factor for w in col_widths]
        ensure_space(66)
        _pdf_draw_text(ops, margin, y - 12, title, 11.5, True, (0.12, 0.23, 0.37))
        y -= 20
        header_h = _pdf_draw_table_header(ops, margin, y, col_widths, headers, font_size)
        y -= header_h
        if not rows:
            row_h = 24
            _pdf_fill_rect(ops, margin, y - row_h, table_w, row_h, (1, 1, 1))
            _pdf_stroke_rect(ops, margin, y - row_h, table_w, row_h, (0.84, 0.87, 0.91), 0.6)
            _pdf_draw_text(ops, margin + 8, y - 15, "Brak danych w analizowanym zakresie.", 8, True, (0.36, 0.41, 0.48))
            y -= row_h + 18
            return
        for idx, row in enumerate(rows):
            wrapped = [_pdf_wrap_lines(cell, max(10, width - 8), font_size, max_lines=max_lines) for cell, width in zip(row, col_widths)]
            row_h = max(18, max(len(lines) for lines in wrapped) * (font_size + 2.0) + 8)
            if y - row_h < bottom:
                new_page()
                _pdf_draw_text(ops, margin, y - 12, title + " - ciag dalszy", 11, True, (0.12, 0.23, 0.37))
                y -= 20
                header_h = _pdf_draw_table_header(ops, margin, y, col_widths, headers, font_size)
                y -= header_h
            fill = (1, 1, 1) if idx % 2 == 0 else (0.95, 0.96, 0.98)
            y -= _pdf_draw_table_row(ops, margin, y, col_widths, row, font_size, fill, max_lines=max_lines, bold_cols=bold_cols)
        y -= 18

    start_page()
    add_title_block()

    if acts:
        days = sorted({day for activity in acts for day in _activity_days(activity)})
        first_time = fmt_datetime_short(acts[0].start)
        last_time = fmt_datetime_short(acts[-1].end)
    else:
        days = []
        first_time = "-"
        last_time = "-"

    totals = activity_totals_by_kind(acts)
    add_cards([
        (str(len(acts)), "Liczba wpisow aktywnosci"),
        (str(len(days)), "Liczba dni na wykresie"),
        (first_time, "Pierwsza aktywnosc"),
        (last_time, "Ostatnia aktywnosc"),
    ])

    for chunk_idx, day_chunk in enumerate(split_days(days, 7)):
        if chunk_idx > 0:
            new_page()
        draw_activity_charts(day_chunk)

    if y < 180:
        new_page()
    total_rows = [
        [activity_label(kind), fmt_duration_report(total)]
        for kind, total in totals.items()
        if total > dt.timedelta(0)
    ]
    add_table(
        "Podsumowanie czasu wedlug aktywnosci",
        ["Aktywnosc", "Czas lacznie"],
        total_rows,
        [520, 274],
        font_size=8.0,
        max_lines=3,
        bold_cols={0},
    )

    daily_rows = [daily_summary_row(item) for item in analyze_daily_summaries(acts)]
    add_table(
        "Dane dzienne",
        ["Data", "Jazda", "Przerwa/odpoczynek", "Dyspozycyjnosc", "Inna praca", "Nieznane", "Suma", "Pierwsza", "Ostatnia", "Wpisy"],
        daily_rows,
        [72, 62, 90, 78, 72, 62, 62, 108, 108, 40],
        font_size=6.2,
        max_lines=2,
        bold_cols={0},
    )

    activity_rows = [activity_report_row(activity) for activity in acts]
    add_table(
        "Szczegolowa lista aktywnosci",
        ["Start", "Koniec", "Typ aktywnosci", "Czas trwania", "Pojazd / nr rej."],
        activity_rows,
        [112, 112, 332, 108, 130],
        font_size=7.0,
        max_lines=4,
        bold_cols={2},
    )

    finish_page()
    _write_simple_pdf(path, pages, page_size=(page_w, page_h))


def export_violations_report_csv(path: Path, violations: list[Violation]) -> None:
    with path.open("w", newline="", encoding="utf-8-sig") as f:
        writer = csv.writer(f, delimiter=";")
        writer.writerow(VIOLATION_REPORT_HEADERS)
        for v in violations:
            writer.writerow(violation_report_row(v))

def export_violations_report_html(path: Path, violations: list[Violation], range_label: str = "") -> None:
    """Eksportuje czytelny raport naruszen do HTML."""
    def esc(x: Any) -> str:
        return str(x).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

    rows = "\n".join(
        "<tr>" + "".join(f"<td>{esc(cell)}</td>" for cell in violation_report_row(v)) + "</tr>"
        for v in violations
    )
    if not rows:
        rows = '<tr><td colspan="12" class="empty">Brak naruszen w analizowanym okresie.</td></tr>'

    first_day = esc(violations[0].start.date()) if violations else "-"
    last_day = esc(violations[-1].start.date()) if violations else "-"
    range_text = esc(range_label) if range_label else "wybrany zakres"
    generated = f"{dt.datetime.now():%Y-%m-%d %H:%M:%S}"

    html = f"""<!doctype html>
<html lang="pl">
<head>
<meta charset="utf-8">
<title>Raport naruszeń kierowcy</title>
<style>
body{{font-family:Arial,sans-serif;margin:24px;background:#f5f7fb;color:#172033;}}
.toolbar{{display:flex;justify-content:flex-end;margin:0 0 14px 0;}}
.print-btn{{background:#12284a;color:white;border:0;border-radius:8px;padding:9px 16px;font-weight:bold;cursor:pointer;}}
.print-btn:hover{{background:#1f3a5f;}}
h1{{margin:0 0 4px 0;color:#12284a;}}
.meta{{margin:2px 0 18px 0;color:#46556b;font-size:13px;}}
.summary{{display:flex;gap:12px;margin:14px 0 18px 0;flex-wrap:wrap;}}
.card{{background:white;border:1px solid #dbe3ef;border-radius:10px;padding:10px 14px;box-shadow:0 1px 4px rgba(18,40,74,.08);}}
.card strong{{display:block;font-size:20px;color:#12284a;}}
table{{border-collapse:collapse;width:100%;background:white;box-shadow:0 2px 8px rgba(18,40,74,.08);border-radius:10px;overflow:hidden;}}
th,td{{border:1px solid #d7dee8;padding:8px 10px;font-size:12.5px;vertical-align:top;line-height:1.35;}}
th{{background:#12284a;color:white;text-align:left;position:sticky;top:0;z-index:1;}}
tr:nth-child(even){{background:#f3f6fb;}}
td:nth-child(1){{white-space:nowrap;font-weight:bold;}}
td:nth-child(3){{white-space:nowrap;font-weight:bold;color:#7a2f00;}}
td:nth-child(6){{white-space:nowrap;}}
.empty{{text-align:center;color:#5c687a;padding:18px;}}
@media print{{body{{background:white;margin:12mm;}} .card,table{{box-shadow:none;}} th{{position:static;}} .no-print{{display:none!important;}}}}
</style>
</head>
<body>
<div class="toolbar no-print"><button class="print-btn" onclick="window.print()">Drukuj</button></div>
<h1>Raport naruszeń</h1>
<p class="meta">Wygenerowano: {generated}</p>
<p class="meta">Zakres raportu: {range_text}</p>
<div class="summary">
  <div class="card"><strong>{len(violations)}</strong>Liczba naruszen</div>
  <div class="card"><strong>{first_day}</strong>Pierwsze naruszenie w raporcie</div>
  <div class="card"><strong>{last_day}</strong>Ostatnie naruszenie w raporcie</div>
</div>
<table>
<thead><tr>
<th>Data naruszenia</th><th>Czego dotyczy</th><th>Kara wg taryfikatora</th><th>Pozycja/kod</th><th>Kategoria taryfikatora</th><th>Waga analizy</th><th>Termin wymagany</th><th>Start faktyczny</th><th>Okres</th><th>Wartosc</th><th>Limit</th><th>Opis</th>
</tr></thead>
<tbody>{rows}</tbody>
</table>
</body>
</html>"""
    path.write_text(html, encoding="utf-8")


def export_csv(path: Path, activities: list[Activity], violations: list[Violation], daily_summaries: Optional[list[DailySummary]] = None, country_entries: Optional[list[CountryCodeEntry]] = None) -> None:
    with path.open("w", newline="", encoding="utf-8-sig") as f:
        writer = csv.writer(f, delimiter=";")
        writer.writerow(["AKTYWNOŚCI"])
        writer.writerow(["start", "koniec", "typ", "czas", "pojazd / nr rej."])
        for a in activities:
            writer.writerow([a.start, a.end, activity_report_label_with_manual_icon(a), fmt_duration_report(a.duration), vehicle_registration_display(a)])
        writer.writerow([])
        writer.writerow(["DANE DZIENNE"])
        writer.writerow(["Data", "Jazda", "Przerwa/odpoczynek", "Dyspozycyjność", "Inna praca", "Nieznane", "Suma zapisu", "Pierwsza aktywność", "Ostatnia aktywność", "Liczba wpisów/części"])
        for item in (daily_summaries if daily_summaries is not None else analyze_daily_summaries(activities)):
            writer.writerow(daily_summary_row(item))
        writer.writerow([])
        writer.writerow([])
        writer.writerow(["PL-KI / KODY KRAJU PRZY WLOZENIU I WYJECIU KARTY"])
        writer.writerow(["Czas wpisu", "Rodzaj wpisu", "Kod kraju", "Kraj", "Status", "Dzien pracy", "Uwagi"])
        for item in (country_entries or []):
            writer.writerow(country_code_row(item))
        writer.writerow([])
        writer.writerow(["RAPORT NARUSZEN"])
        writer.writerow(VIOLATION_REPORT_HEADERS)
        for v in violations:
            writer.writerow(violation_report_row(v))


def export_html(path: Path, activities: list[Activity], violations: list[Violation], continuity: Optional[list[ContinuityIssue]] = None, compensations: Optional[list[CompensationDebt]] = None, daily_summaries: Optional[list[DailySummary]] = None, country_entries: Optional[list[CountryCodeEntry]] = None) -> None:
    def esc(x: Any) -> str:
        return str(x).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

    rows_v = "\n".join(
        "<tr>" + "".join(f"<td>{esc(cell)}</td>" for cell in violation_report_row(v)) + "</tr>"
        for v in violations
    )
    rows_a = "\n".join(
        "<tr>" + "".join(f"<td>{esc(cell)}</td>" for cell in activity_report_row(a)) + "</tr>"
        for a in activities
    )
    continuity = continuity or []
    compensations = compensations or []
    daily_summaries = daily_summaries if daily_summaries is not None else analyze_daily_summaries(activities)
    country_entries = country_entries or []
    rows_daily = "\n".join(
        "<tr>" + "".join(f"<td>{esc(cell)}</td>" for cell in daily_summary_row(item)) + "</tr>"
        for item in daily_summaries
    )
    rests = analyze_rest_overview(activities, compensations)
    rows_rests = "\n".join(
        "<tr>" + "".join(f"<td>{esc(cell)}</td>" for cell in rest_overview_row(item)) + "</tr>"
        for item in rests
    )
    rows_c = "\n".join(
        "<tr>" + "".join(f"<td>{esc(cell)}</td>" for cell in continuity_report_row(issue)) + "</tr>"
        for issue in continuity
    )
    rows_comp = "\n".join(
        "<tr>" + "".join(f"<td>{esc(cell)}</td>" for cell in compensation_report_row(item)) + "</tr>"
        for item in compensations
    )
    rows_country = "\n".join(
        "<tr>" + "".join(f"<td>{esc(cell)}</td>" for cell in country_code_row(item)) + "</tr>"
        for item in country_entries
    )
    html = f"""<!doctype html>
<html lang="pl"><head><meta charset="utf-8"><title>Raport czasu pracy kierowcy</title>
<style>
body{{font-family:Arial,sans-serif;margin:24px;background:#f7f9fc;color:#162033;}}
h1{{margin-bottom:6px;}}
h2{{margin-top:28px;color:#1f3a5f;}}
table{{border-collapse:collapse;width:100%;margin-bottom:24px;background:white;box-shadow:0 1px 4px rgba(0,0,0,.08);}}
th,td{{border:1px solid #d7dee8;padding:8px 10px;font-size:13px;vertical-align:top;}}
th{{background:#1f3a5f;color:white;text-align:left;position:sticky;top:0;}}
tr:nth-child(even){{background:#f4f7fb;}}
.badge{{display:inline-block;padding:2px 8px;border-radius:10px;background:#e8eef8;color:#1f3a5f;font-weight:bold;}}
</style></head><body>
<h1>Raport czasu pracy kierowcy</h1>
<p>Wygenerowano: {dt.datetime.now():%Y-%m-%d %H:%M:%S}</p>
<h2>Raport naruszen <span class="badge">{len(violations)}</span></h2>
<table><thead><tr><th>Data naruszenia</th><th>Czego dotyczy</th><th>Kara wg taryfikatora</th><th>Pozycja/kod</th><th>Kategoria taryfikatora</th><th>Waga analizy</th><th>Termin wymagany</th><th>Start faktyczny</th><th>Okres</th><th>Wartosc</th><th>Limit</th><th>Opis</th></tr></thead><tbody>{rows_v}</tbody></table>
<h2>Dane dzienne <span class="badge">{len(daily_summaries)}</span></h2>
<table><thead><tr><th>Data</th><th>Jazda</th><th>Przerwa/odpoczynek</th><th>Dyspozycyjność</th><th>Inna praca</th><th>Nieznane</th><th>Suma zapisu</th><th>Pierwsza aktywność</th><th>Ostatnia aktywność</th><th>Liczba wpisów/części</th></tr></thead><tbody>{rows_daily}</tbody></table>
<h2>Przeglad odpoczynkow <span class="badge">{len(rests)}</span></h2>
<table><thead><tr><th>Typ odpoczynku</th><th>Start</th><th>Koniec</th><th>Czas trwania</th><th>Status</th><th>Termin/rekompensata</th><th>Uwagi</th></tr></thead><tbody>{rows_rests}</tbody></table>
<h2>PL-ki / kody kraju przy wlozeniu i wyjeciu karty <span class="badge">{len(country_entries)}</span></h2>
<table><thead><tr><th>Czas wpisu</th><th>Rodzaj wpisu</th><th>Kod kraju</th><th>Kraj</th><th>Status</th><th>Dzien pracy</th><th>Uwagi</th></tr></thead><tbody>{rows_country}</tbody></table>
<h2>Rekompensaty skroconych odpoczynkow tygodniowych <span class="badge">{len(compensations)}</span></h2>
<table><thead><tr><th>Start skroconego odpoczynku</th><th>Koniec</th><th>Czas odpoczynku</th><th>Brakujaca rekompensata</th><th>Termin odbioru</th><th>Status</th><th>Odebrano w okresie</th><th>Ciaglosc danych</th><th>Rekomendacja</th></tr></thead><tbody>{rows_comp}</tbody></table>
<h2>Ciaglosc danych <span class="badge">{len(continuity)}</span></h2>
<table><thead><tr><th>Start</th><th>Koniec</th><th>Czas</th><th>Typ</th><th>Wplyw</th><th>Zalecenie</th></tr></thead><tbody>{rows_c}</tbody></table>
<h2>Aktywności <span class="badge">{len(activities)}</span></h2>
<table><thead><tr><th>Start</th><th>Koniec</th><th>Typ</th><th>Czas</th><th>Pojazd / nr rej.</th></tr></thead><tbody>{rows_a}</tbody></table>
</body></html>"""
    path.write_text(html, encoding="utf-8")


PDF_TEXT_REPLACEMENTS = str.maketrans({
    "ą": "a", "ć": "c", "ę": "e", "ł": "l", "ń": "n", "ó": "o", "ś": "s", "ź": "z", "ż": "z",
    "Ą": "A", "Ć": "C", "Ę": "E", "Ł": "L", "Ń": "N", "Ó": "O", "Ś": "S", "Ź": "Z", "Ż": "Z",
    "—": "-", "–": "-", "−": "-", "…": "...",
    "🚚": "[Jazda]", "🛏": "[Odpoczynek]", "⌛": "[Dyspozycyjnosc]", "⚒": "[Inna praca]", "❓": "[Nieznane]",
    "✋": "[Recznie]",
})


def _pdf_clean_text(value: Any) -> str:
    """Convert user-facing Polish/emoji text to a safe single-byte PDF representation."""
    text_value = str(value).replace("\r", " ").replace("\n", " ").translate(PDF_TEXT_REPLACEMENTS)
    # Built-in PDF base fonts are most portable in one-file EXE mode when limited to Latin-1.
    return text_value.encode("latin-1", "ignore").decode("latin-1")


def _pdf_escape(value: Any) -> str:
    text_value = _pdf_clean_text(value)
    return text_value.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")


def _write_simple_pdf(path: Path, pages: list[list[str]], page_size: tuple[int, int] = (595, 842)) -> None:
    """Write a compact multipage PDF using only Python standard library."""
    if not pages:
        pages = [[]]

    page_width, page_height = page_size
    objects: dict[int, bytes] = {}
    page_ids: list[int] = []
    next_id = 5

    objects[3] = b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
    objects[4] = b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"

    for page_ops in pages:
        content = ("\n".join(page_ops) + "\n").encode("latin-1", "ignore")
        content_id = next_id
        page_id = next_id + 1
        next_id += 2
        objects[content_id] = b"<< /Length " + str(len(content)).encode("ascii") + b" >>\nstream\n" + content + b"endstream"
        objects[page_id] = (
            f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {page_width} {page_height}] "
            f"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> "
            f"/Contents {content_id} 0 R >>"
        ).encode("ascii")
        page_ids.append(page_id)

    kids = " ".join(f"{pid} 0 R" for pid in page_ids)
    objects[1] = b"<< /Type /Catalog /Pages 2 0 R >>"
    objects[2] = f"<< /Type /Pages /Kids [{kids}] /Count {len(page_ids)} >>".encode("ascii")

    ordered_ids = sorted(objects)
    out = bytearray(b"%PDF-1.4\n%\xe2\xe3\xcf\xd3\n")
    offsets: dict[int, int] = {}
    for obj_id in ordered_ids:
        offsets[obj_id] = len(out)
        out.extend(f"{obj_id} 0 obj\n".encode("ascii"))
        out.extend(objects[obj_id])
        out.extend(b"\nendobj\n")
    xref_offset = len(out)
    max_id = max(ordered_ids)
    out.extend(f"xref\n0 {max_id + 1}\n".encode("ascii"))
    out.extend(b"0000000000 65535 f \n")
    for obj_id in range(1, max_id + 1):
        off = offsets.get(obj_id, 0)
        if off:
            out.extend(f"{off:010d} 00000 n \n".encode("ascii"))
        else:
            out.extend(b"0000000000 00000 f \n")
    out.extend(f"trailer\n<< /Size {max_id + 1} /Root 1 0 R >>\nstartxref\n{xref_offset}\n%%EOF\n".encode("ascii"))
    path.write_bytes(bytes(out))


def _pdf_color(color: tuple[float, float, float]) -> str:
    return f"{color[0]:.3f} {color[1]:.3f} {color[2]:.3f}"


def _pdf_fill_rect(ops: list[str], x: float, y: float, w: float, h: float, color: tuple[float, float, float]) -> None:
    ops.append(f"q {_pdf_color(color)} rg {x:.2f} {y:.2f} {w:.2f} {h:.2f} re f Q")


def _pdf_stroke_rect(ops: list[str], x: float, y: float, w: float, h: float, color: tuple[float, float, float] = (0.84, 0.87, 0.91), line_width: float = 0.6) -> None:
    ops.append(f"q {_pdf_color(color)} RG {line_width:.2f} w {x:.2f} {y:.2f} {w:.2f} {h:.2f} re S Q")


def _pdf_draw_line(ops: list[str], x1: float, y1: float, x2: float, y2: float, color: tuple[float, float, float] = (0.70, 0.74, 0.80), line_width: float = 0.5) -> None:
    ops.append(f"q {_pdf_color(color)} RG {line_width:.2f} w {x1:.2f} {y1:.2f} m {x2:.2f} {y2:.2f} l S Q")


def _pdf_draw_text(ops: list[str], x: float, y: float, value: Any, size: float = 8, bold: bool = False, color: tuple[float, float, float] = (0.09, 0.13, 0.20)) -> None:
    font = "F2" if bold else "F1"
    ops.append(f"q {_pdf_color(color)} rg BT /{font} {size:.2f} Tf 1 0 0 1 {x:.2f} {y:.2f} Tm ({_pdf_escape(value)}) Tj ET Q")


def _pdf_wrap_lines(value: Any, width_pt: float, font_size: float, max_lines: Optional[int] = None) -> list[str]:
    cleaned = _pdf_clean_text(value).strip()
    if not cleaned:
        return [""]
    # Approximation for Helvetica in points. It is intentionally conservative to avoid clipping.
    chars = max(5, int(width_pt / max(font_size * 0.55, 1)))
    lines = textwrap.wrap(cleaned, width=chars, break_long_words=False, replace_whitespace=True) or [cleaned[:chars]]
    if max_lines is not None and len(lines) > max_lines:
        lines = lines[:max_lines]
        if lines:
            lines[-1] = (lines[-1][: max(0, chars - 3)] + "...").strip()
    return lines


def _pdf_draw_wrapped_text(ops: list[str], x: float, top_y: float, value: Any, width: float, font_size: float = 8, bold: bool = False, color: tuple[float, float, float] = (0.09, 0.13, 0.20), max_lines: Optional[int] = None, line_gap: float = 2.0) -> float:
    lines = _pdf_wrap_lines(value, width, font_size, max_lines)
    line_height = font_size + line_gap
    y = top_y - font_size
    for line in lines:
        _pdf_draw_text(ops, x, y, line, font_size, bold, color)
        y -= line_height
    return len(lines) * line_height


def _pdf_draw_table_header(ops: list[str], x: float, y_top: float, col_widths: list[float], headers: list[str], font_size: float, header_fill: tuple[float, float, float] = (0.07, 0.16, 0.29)) -> float:
    header_lines = [
        _pdf_wrap_lines(header, max(12, width - 8), font_size, max_lines=2)
        for header, width in zip(headers, col_widths)
    ]
    row_h = max(len(lines) for lines in header_lines) * (font_size + 2.0) + 8
    cur_x = x
    for width, lines in zip(col_widths, header_lines):
        _pdf_fill_rect(ops, cur_x, y_top - row_h, width, row_h, header_fill)
        _pdf_stroke_rect(ops, cur_x, y_top - row_h, width, row_h, (0.07, 0.16, 0.29), 0.8)
        ty = y_top - 6
        for line in lines:
            _pdf_draw_text(ops, cur_x + 4, ty - font_size, line, font_size, True, (1, 1, 1))
            ty -= font_size + 2.0
        cur_x += width
    return row_h


def _pdf_draw_table_row(ops: list[str], x: float, y_top: float, col_widths: list[float], row: list[Any], font_size: float, fill: tuple[float, float, float], max_lines: int = 6, bold_cols: Optional[set[int]] = None, color_cols: Optional[dict[int, tuple[float, float, float]]] = None) -> float:
    bold_cols = bold_cols or set()
    color_cols = color_cols or {}
    wrapped = [
        _pdf_wrap_lines(cell, max(10, width - 8), font_size, max_lines=max_lines)
        for cell, width in zip(row, col_widths)
    ]
    row_h = max(18, max(len(lines) for lines in wrapped) * (font_size + 2.0) + 8)
    cur_x = x
    for idx, (width, lines) in enumerate(zip(col_widths, wrapped)):
        _pdf_fill_rect(ops, cur_x, y_top - row_h, width, row_h, fill)
        _pdf_stroke_rect(ops, cur_x, y_top - row_h, width, row_h, (0.84, 0.87, 0.91), 0.6)
        ty = y_top - 6
        for line in lines:
            _pdf_draw_text(ops, cur_x + 4, ty - font_size, line, font_size, idx in bold_cols, color_cols.get(idx, (0.09, 0.13, 0.20)))
            ty -= font_size + 2.0
        cur_x += width
    return row_h


def _write_html_like_report_pdf(
    path: Path,
    title: str,
    range_label: str,
    cards: list[tuple[str, str]],
    sections: list[dict[str, Any]],
) -> None:
    """Create a visually structured PDF that mirrors the HTML reports without external libraries."""
    page_w, page_h = 842, 595  # A4 landscape, closer to the wide HTML tables.
    margin = 24
    table_w = page_w - (2 * margin)
    bottom = 34
    pages: list[list[str]] = []
    ops: list[str] = []
    y = page_h - margin
    page_no = 0

    def start_page() -> None:
        nonlocal ops, y, page_no
        page_no += 1
        ops = []
        _pdf_fill_rect(ops, 0, 0, page_w, page_h, (0.96, 0.97, 0.98))
        y = page_h - margin

    def finish_page() -> None:
        _pdf_draw_text(ops, margin, 15, f"Strona {page_no}", 7.5, False, (0.36, 0.41, 0.48))
        pages.append(ops[:])

    def ensure_space(height: float) -> None:
        nonlocal y
        if y - height < bottom:
            finish_page()
            start_page()

    def add_title_block() -> None:
        nonlocal y
        _pdf_draw_text(ops, margin, y - 18, title, 18, True, (0.07, 0.16, 0.29))
        y -= 30
        _pdf_draw_text(ops, margin, y - 8, f"Wygenerowano: {dt.datetime.now():%Y-%m-%d %H:%M:%S}", 8.5, False, (0.27, 0.33, 0.42))
        y -= 14
        _pdf_draw_text(ops, margin, y - 8, f"Zakres raportu: {range_label or 'wybrany zakres'}", 8.5, True, (0.27, 0.33, 0.42))
        y -= 20

    def add_cards() -> None:
        nonlocal y
        if not cards:
            return
        ensure_space(54)
        gap = 10
        card_w = (table_w - gap * (len(cards) - 1)) / max(1, len(cards))
        card_h = 44
        cur_x = margin
        for value, label in cards:
            _pdf_fill_rect(ops, cur_x, y - card_h, card_w, card_h, (1, 1, 1))
            _pdf_stroke_rect(ops, cur_x, y - card_h, card_w, card_h, (0.86, 0.89, 0.94), 0.8)
            _pdf_draw_text(ops, cur_x + 10, y - 19, value, 14, True, (0.07, 0.16, 0.29))
            _pdf_draw_wrapped_text(ops, cur_x + 10, y - 24, label, card_w - 20, 7.5, False, (0.36, 0.41, 0.48), max_lines=2)
            cur_x += card_w + gap
        y -= card_h + 22

    def add_section(section: dict[str, Any]) -> None:
        nonlocal y
        title_text = section["title"]
        headers = section["headers"]
        rows = section["rows"]
        col_widths = section["col_widths"]
        font_size = section.get("font_size", 7.0)
        max_lines = section.get("max_lines", 6)
        empty_text = section.get("empty_text", "Brak danych w analizowanym okresie.")
        bold_cols = set(section.get("bold_cols", []))
        color_cols = section.get("color_cols", {})
        if abs(sum(col_widths) - table_w) > 1:
            factor = table_w / sum(col_widths)
            col_widths = [w * factor for w in col_widths]

        ensure_space(62)
        _pdf_draw_text(ops, margin, y - 12, title_text, 12, True, (0.12, 0.23, 0.37))
        y -= 20
        header_h = _pdf_draw_table_header(ops, margin, y, col_widths, headers, font_size, section.get("header_font_size", font_size)) if False else _pdf_draw_table_header(ops, margin, y, col_widths, headers, font_size)
        y -= header_h

        if not rows:
            row_h = 24
            _pdf_fill_rect(ops, margin, y - row_h, table_w, row_h, (1, 1, 1))
            _pdf_stroke_rect(ops, margin, y - row_h, table_w, row_h, (0.84, 0.87, 0.91), 0.6)
            _pdf_draw_text(ops, margin + 8, y - 15, empty_text, 8, True, (0.36, 0.41, 0.48))
            y -= row_h + 20
            return

        for idx, row in enumerate(rows):
            wrapped = [_pdf_wrap_lines(cell, max(10, width - 8), font_size, max_lines=max_lines) for cell, width in zip(row, col_widths)]
            row_h = max(18, max(len(lines) for lines in wrapped) * (font_size + 2.0) + 8)
            if y - row_h < bottom:
                finish_page()
                start_page()
                _pdf_draw_text(ops, margin, y - 12, title_text + " - ciag dalszy", 11, True, (0.12, 0.23, 0.37))
                y -= 20
                header_h = _pdf_draw_table_header(ops, margin, y, col_widths, headers, font_size)
                y -= header_h
            fill = (1, 1, 1) if idx % 2 == 0 else (0.95, 0.96, 0.98)
            row_h = _pdf_draw_table_row(ops, margin, y, col_widths, row, font_size, fill, max_lines=max_lines, bold_cols=bold_cols, color_cols=color_cols)
            y -= row_h
        y -= 20

    start_page()
    add_title_block()
    add_cards()
    for section in sections:
        add_section(section)
    finish_page()
    _write_simple_pdf(path, pages, page_size=(page_w, page_h))


def export_pdf(path: Path, activities: list[Activity], violations: list[Violation], continuity: Optional[list[ContinuityIssue]] = None, compensations: Optional[list[CompensationDebt]] = None, daily_summaries: Optional[list[DailySummary]] = None, country_entries: Optional[list[CountryCodeEntry]] = None) -> None:
    """Eksportuje raport kontrolny do PDF bez zewnetrznych bibliotek."""
    continuity = continuity or []
    compensations = compensations or []
    daily_summaries = daily_summaries if daily_summaries is not None else analyze_daily_summaries(activities)
    country_entries = country_entries or []
    rests = analyze_rest_overview(activities, compensations)

    pages: list[list[str]] = [[]]
    y = 810
    page_no = 1

    def add_text(x: int, y_pos: int, value: Any, size: int = 9, bold: bool = False) -> None:
        font = "F2" if bold else "F1"
        pages[-1].append(f"BT /{font} {size} Tf 1 0 0 1 {x} {y_pos} Tm ({_pdf_escape(value)}) Tj ET")

    def new_page() -> None:
        nonlocal y, page_no
        add_text(40, 24, f"Strona {page_no}", 8, False)
        pages.append([])
        page_no += 1
        y = 810

    def add_line(value: Any = "", size: int = 9, bold: bool = False, indent: int = 0) -> None:
        nonlocal y
        if y < 42:
            new_page()
        add_text(40 + indent, y, value, size, bold)
        y -= max(size + 4, 10)

    def add_wrapped(value: Any, size: int = 8, bold: bool = False, indent: int = 0, width: int = 124) -> None:
        cleaned = _pdf_clean_text(value)
        if not cleaned.strip():
            add_line("", size, bold, indent)
            return
        for part in textwrap.wrap(cleaned, width=width, break_long_words=False, replace_whitespace=True) or [cleaned[:width]]:
            add_line(part, size, bold, indent)

    def add_section(title: str, headers: list[str], rows: list[list[Any]], max_rows: Optional[int] = None) -> None:
        nonlocal y
        if y < 120:
            new_page()
        add_line("", 5)
        add_line(f"{title} ({len(rows)})", 12, True)
        add_wrapped(" | ".join(headers), 8, True, 0, 120)
        shown_rows = rows if max_rows is None else rows[:max_rows]
        for row in shown_rows:
            add_wrapped(" | ".join(str(cell) for cell in row), 7, False, 8, 132)
        if max_rows is not None and len(rows) > max_rows:
            add_wrapped(f"... pominieto {len(rows) - max_rows} kolejnych wierszy w PDF. Pelny zakres danych jest dostepny w raporcie HTML.", 8, True, 8, 120)

    add_line("Raport czasu pracy kierowcy", 17, True)
    add_line(f"Wygenerowano: {dt.datetime.now():%Y-%m-%d %H:%M:%S}", 9)
    add_line("Format PDF generowany bez dodatkowych bibliotek. Polskie znaki i ikony sa uproszczone dla zgodnosci z PDF.", 8)
    add_line("")
    add_line(f"Liczba aktywnosci: {len(activities)}", 9, True)
    add_line(f"Liczba naruszen: {len(violations)}", 9, True)
    add_line(f"Dni w zestawieniu dziennym: {len(daily_summaries)}", 9, True)

    add_section("Raport naruszen", VIOLATION_REPORT_HEADERS, [violation_report_row(v) for v in violations])
    add_section("Dane dzienne", ["Data", "Jazda", "Przerwa/odpoczynek", "Dyspozycyjnosc", "Inna praca", "Nieznane", "Suma zapisu", "Pierwsza aktywnosc", "Ostatnia aktywnosc", "Liczba wpisow/czesci"], [daily_summary_row(item) for item in daily_summaries])
    add_section("Przeglad odpoczynkow", ["Typ odpoczynku", "Start", "Koniec", "Czas trwania", "Status", "Termin/rekompensata", "Uwagi"], [rest_overview_row(item) for item in rests])
    add_section("PL-ki / kody kraju przy wlozeniu i wyjeciu karty", ["Czas wpisu", "Rodzaj wpisu", "Kod kraju", "Kraj", "Status", "Dzien pracy", "Uwagi"], [country_code_row(item) for item in country_entries])
    add_section("Rekompensaty skroconych odpoczynkow tygodniowych", ["Start", "Koniec", "Czas odpoczynku", "Brakujaca rekompensata", "Termin odbioru", "Status", "Odebrano w okresie", "Ciaglosc danych", "Rekomendacja"], [compensation_report_row(item) for item in compensations])
    add_section("Ciaglosc danych", ["Start", "Koniec", "Czas", "Typ", "Wplyw", "Zalecenie"], [continuity_report_row(issue) for issue in continuity])
    add_section("Aktywnosci", ["Start", "Koniec", "Typ", "Czas", "Pojazd / nr rej."], [[a.start, a.end, activity_report_label_with_manual_icon(a), fmt_duration_report(a.duration), vehicle_registration_display(a)] for a in activities], max_rows=1200)

    add_text(40, 24, f"Strona {page_no}", 8, False)
    _write_simple_pdf(path, pages)

def export_violations_report_pdf(path: Path, violations: list[Violation], range_label: str = "") -> None:
    """Eksportuje raport naruszen do PDF w ukladzie graficznym zblizonym do HTML."""
    first_day = str(violations[0].start.date()) if violations else "-"
    last_day = str(violations[-1].start.date()) if violations else "-"
    cards = [
        (str(len(violations)), "Liczba naruszen"),
        (first_day, "Pierwsze naruszenie w raporcie"),
        (last_day, "Ostatnie naruszenie w raporcie"),
    ]
    sections = [
        {
            "title": "Tabela naruszen",
            "headers": [
                "Data naruszenia", "Czego dotyczy", "Kara wg taryfikatora", "Pozycja/kod",
                "Kategoria taryfikatora", "Waga analizy", "Termin wymagany", "Start faktyczny",
                "Okres", "Wartosc", "Limit", "Opis",
            ],
            "rows": [violation_report_row(v) for v in violations],
            "col_widths": [52, 118, 55, 45, 70, 42, 62, 62, 65, 38, 38, 147],
            "font_size": 5.7,
            "max_lines": 7,
            "empty_text": "Brak naruszen w analizowanym okresie.",
            "bold_cols": [0, 2],
            "color_cols": {2: (0.48, 0.18, 0.00)},
        },
    ]
    _write_html_like_report_pdf(path, "Raport naruszen", range_label, cards, sections)


def _activity_days(activity: Activity) -> list[dt.date]:
    """Return all calendar days touched by an activity, including cross-midnight activities."""
    if activity.end <= activity.start:
        return [activity.start.date()]
    days: list[dt.date] = []
    current = activity.start.date()
    last_moment = activity.end - dt.timedelta(microseconds=1)
    last_day = last_moment.date()
    while current <= last_day:
        days.append(current)
        current += dt.timedelta(days=1)
    return days


CARD_READ_DATE_KEYWORDS = (
    "download", "readout", "read_out", "read-out", "odczyt", "pobran",
    "extraction", "extract", "generated", "generation", "export",
    "carddownload", "card_download", "cardread", "card_read", "dataodczytu",
)


def _file_modified_date(path: Optional[Path]) -> Optional[dt.date]:
    """Return file modification date, used as fallback for card readout date."""
    if not path:
        return None
    try:
        return dt.datetime.fromtimestamp(path.stat().st_mtime).date()
    except OSError:
        return None


def infer_card_read_date(data: Any, activities: list[Activity], input_path: Optional[Path] = None) -> tuple[Optional[dt.date], str]:
    """Infer the day the driver card was read/downloaded.

    Priority:
    1. explicit JSON/parser metadata that looks like readout/download/export date,
    2. modification date of the selected .DDD/.JSON file,
    3. last day of activity data.

    The value is used only to set the default 56-day control window.
    """
    candidates: list[tuple[dt.datetime, str]] = []
    for path, node in walk_json(data):
        if not isinstance(node, dict):
            continue
        for key, value in node.items():
            key_text = str(key).lower().replace(" ", "").replace("-", "_")
            path_text = f"{path}.{key}".lower().replace(" ", "").replace("-", "_")
            if not any(token in key_text or token in path_text for token in CARD_READ_DATE_KEYWORDS):
                continue
            parsed = parse_datetime(value)
            if parsed:
                candidates.append((parsed, "data odczytu karty"))
    if candidates:
        # Prefer the newest plausible readout/export timestamp, because files can contain
        # several generation/download metadata fields.
        parsed, source = sorted(candidates, key=lambda item: item[0])[-1]
        return parsed.date(), source

    modified = _file_modified_date(input_path)
    if modified:
        return modified, "data modyfikacji wybranego pliku"

    if activities:
        return max(activity.end.date() for activity in activities), "ostatni dzien aktywnosci - brak daty odczytu"

    return None, "brak daty odczytu"


def default_56_day_range(card_read_date: dt.date) -> tuple[dt.date, dt.date]:
    """Default control range: from 56 days before card readout to readout day."""
    return card_read_date - dt.timedelta(days=56), card_read_date


REPORT_RANGE_OPTIONS = (
    "Ostatni tydzień",
    "Ostatnie 28 dni",
    "Ostatni miesiąc",
    "Ostatnie 56 dni",
    "Cały okres",
)


def subtract_one_calendar_month(day: dt.date) -> dt.date:
    """Return the same day in the previous calendar month when possible."""
    year = day.year
    month = day.month - 1
    if month == 0:
        year -= 1
        month = 12
    last_day = calendar.monthrange(year, month)[1]
    return dt.date(year, month, min(day.day, last_day))


def report_range_from_label(label: str, end_day: dt.date) -> tuple[dt.date, dt.date, str]:
    """Return date range for the selected violations/activity report period."""
    normalized = (label or "").strip().lower()
    if normalized == "ostatni tydzień":
        return end_day - dt.timedelta(days=6), end_day, "ostatni tydzień"
    if normalized == "ostatnie 28 dni":
        return end_day - dt.timedelta(days=27), end_day, "ostatnie 28 dni"
    if normalized == "ostatni miesiąc":
        return subtract_one_calendar_month(end_day), end_day, "ostatni miesiąc"
    return end_day - dt.timedelta(days=55), end_day, "ostatnie 56 dni"


def sanitize_filename_part(value: Any, fallback: str = "kierowca") -> str:
    """Return a filesystem-safe filename component."""
    text = str(value or "").strip()
    if not text:
        text = fallback
    # Keep Polish letters, but remove characters illegal/problematic on Windows.
    text = re.sub(r'[\\/:*?"<>|]+', " ", text)
    text = re.sub(r"\s+", " ", text).strip(" ._-")
    return text or fallback


def _recursive_find_surname(data: Any) -> Optional[str]:
    """Try to find driver's surname in JSON generated by external parsers."""
    preferred_keys = {
        "surname", "last_name", "lastname", "lastName", "family_name", "familyName",
        "driverSurname", "driver_surname", "cardHolderSurname", "card_holder_surname",
        "holderSurname", "holder_surname", "nazwisko",
    }
    if isinstance(data, dict):
        # First pass: exact/preferred key names.
        for key, value in data.items():
            key_text = str(key)
            if key_text in preferred_keys or key_text.lower() in {k.lower() for k in preferred_keys}:
                if isinstance(value, (str, int)) and str(value).strip():
                    return sanitize_filename_part(value)
        # Second pass: nested values.
        for value in data.values():
            found = _recursive_find_surname(value)
            if found:
                return found
    elif isinstance(data, list):
        for item in data:
            found = _recursive_find_surname(item)
            if found:
                return found
    return None


def infer_driver_surname(data: Any, input_path: Optional[Path] = None) -> str:
    """Infer surname for report filenames. Fall back to file name/kierowca."""
    found = _recursive_find_surname(data)
    if found:
        return found
    if input_path is not None:
        stem = sanitize_filename_part(input_path.stem, "kierowca")
        # If the file looks like "Kowalski_2026-05-02.ddd", take the first meaningful part.
        generic = {"karta", "card", "driver", "kierowca", "plik", "ddd", "odczyt", "download", "tacho", "tachograf"}
        parts = [
            p for p in re.split(r"[ _.-]+", stem)
            if p and not re.fullmatch(r"\d{2,4}", p) and p.lower() not in generic
        ]
        if parts:
            return sanitize_filename_part(parts[0], "kierowca")
    return "kierowca"


def date_span_days(start_day: dt.date, end_day: dt.date) -> list[dt.date]:
    """Return all dates between start and end inclusive."""
    if start_day > end_day:
        start_day, end_day = end_day, start_day
    total = (end_day - start_day).days
    return [start_day + dt.timedelta(days=i) for i in range(total + 1)]


MONTH_NAMES_PL = [
    "", "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec",
    "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień",
]
WEEKDAY_NAMES_PL = ["Pn", "Wt", "Śr", "Cz", "Pt", "So", "Nd"]


class CalendarDatePicker(tk.Toplevel):
    """Maly kalendarz Tkinter bez zewnetrznych bibliotek.

    Pozwala szybko wybrac miesiac i rok z list rozwijanych oraz wpisac date
    recznie w formacie RRRR-MM-DD. Dla wykresu moze ograniczac wybor tylko do
    dni z danymi, a dla zakresu dat moze pozwalac na kazdy dzien w przedziale.
    """

    def __init__(
        self,
        parent: tk.Misc,
        available_days: list[dt.date],
        selected_day: dt.date,
        on_select: Any,
        title: str = "Wybierz dzien",
        info_text: str = "Aktywne sa dni, w ktorych sa dane z tachografu.",
        selectable_only_available: bool = True,
    ) -> None:
        super().__init__(parent)
        self.title(title)
        self.info_text = info_text
        self.selectable_only_available = selectable_only_available
        self.resizable(False, False)
        self.transient(parent)
        self.grab_set()

        self.available_days = sorted(set(available_days))
        if not self.available_days:
            raise ValueError("Brak dni do wyboru")
        self.available_set = set(self.available_days)
        self.min_day = self.available_days[0]
        self.max_day = self.available_days[-1]
        self.on_select = on_select

        if self.min_day <= selected_day <= self.max_day:
            self.selected_day = selected_day
        else:
            self.selected_day = self.min_day
        if self.selectable_only_available and self.selected_day not in self.available_set:
            self.selected_day = self.available_days[0]

        self.view_year = self.selected_day.year
        self.view_month = self.selected_day.month
        self.month_var = tk.StringVar(value=MONTH_NAMES_PL[self.view_month])
        self.year_var = tk.StringVar(value=str(self.view_year))
        self.manual_date_var = tk.StringVar(value=self.selected_day.isoformat())

        self.configure(background="#ffffff")
        self._build_shell()
        self._render_month()
        self.bind("<Escape>", lambda _event: self.destroy())
        self.bind("<Return>", lambda _event: self._apply_manual_date())

        self.update_idletasks()
        try:
            px = parent.winfo_rootx()
            py = parent.winfo_rooty()
            pw = parent.winfo_width()
            ph = parent.winfo_height()
            x = px + max(30, (pw - self.winfo_width()) // 2)
            y = py + max(30, (ph - self.winfo_height()) // 3)
            self.geometry(f"+{x}+{y}")
        except tk.TclError:
            pass

    def _build_shell(self) -> None:
        self.container = tk.Frame(self, background="#ffffff", padx=10, pady=8)
        self.container.pack(fill=tk.BOTH, expand=True)

        header = tk.Frame(self.container, background="#ffffff")
        header.pack(fill=tk.X, pady=(0, 6))

        ttk.Label(header, text="Miesiac:").pack(side=tk.LEFT)
        self.month_combo = ttk.Combobox(
            header,
            textvariable=self.month_var,
            values=MONTH_NAMES_PL[1:],
            width=12,
            state="readonly",
        )
        self.month_combo.pack(side=tk.LEFT, padx=(4, 8))
        self.month_combo.bind("<<ComboboxSelected>>", lambda _event: self._jump_to_selected_month())

        ttk.Label(header, text="Rok:").pack(side=tk.LEFT)
        min_year = self.min_day.year
        max_year = self.max_day.year
        self.year_combo = ttk.Combobox(
            header,
            textvariable=self.year_var,
            values=[str(year) for year in range(min_year, max_year + 1)],
            width=6,
            state="readonly",
        )
        self.year_combo.pack(side=tk.LEFT, padx=(4, 0))
        self.year_combo.bind("<<ComboboxSelected>>", lambda _event: self._jump_to_selected_month())

        self.days_frame = tk.Frame(self.container, background="#ffffff")
        self.days_frame.pack(fill=tk.BOTH, expand=True)

        manual = tk.Frame(self.container, background="#ffffff")
        manual.pack(fill=tk.X, pady=(7, 0))
        ttk.Label(manual, text="Data:").pack(side=tk.LEFT)
        self.manual_entry = ttk.Entry(manual, textvariable=self.manual_date_var, width=12, justify="center")
        self.manual_entry.pack(side=tk.LEFT, padx=(4, 5))
        self.manual_entry.bind("<Return>", lambda _event: self._apply_manual_date())
        ttk.Button(manual, text="Ustaw", command=self._apply_manual_date).pack(side=tk.LEFT)
        ttk.Button(manual, text="Zamknij", command=self.destroy).pack(side=tk.RIGHT)

        tk.Label(
            self.container,
            text=self.info_text,
            background="#ffffff",
            foreground="#536174",
            font=("Arial", 8),
            wraplength=285,
            justify="left",
        ).pack(fill=tk.X, pady=(6, 0))

    def _month_index(self, year: int, month: int) -> int:
        return year * 12 + month

    def _jump_to_selected_month(self) -> None:
        month_name = self.month_var.get()
        try:
            month = MONTH_NAMES_PL.index(month_name)
            year = int(self.year_var.get())
        except (ValueError, TypeError):
            return
        if month < 1:
            return
        min_idx = self._month_index(self.min_day.year, self.min_day.month)
        max_idx = self._month_index(self.max_day.year, self.max_day.month)
        chosen_idx = self._month_index(year, month)
        if chosen_idx < min_idx:
            year, month = self.min_day.year, self.min_day.month
        elif chosen_idx > max_idx:
            year, month = self.max_day.year, self.max_day.month
        self.view_year = year
        self.view_month = month
        self.month_var.set(MONTH_NAMES_PL[self.view_month])
        self.year_var.set(str(self.view_year))
        self._render_month()

    def _can_select(self, day: dt.date) -> bool:
        if not (self.min_day <= day <= self.max_day):
            return False
        if self.selectable_only_available and day not in self.available_set:
            return False
        return True

    def _select_day(self, day: dt.date) -> None:
        if not self._can_select(day):
            return
        self.on_select(day)
        self.destroy()

    def _apply_manual_date(self) -> None:
        raw = self.manual_date_var.get().strip()
        try:
            day = dt.date.fromisoformat(raw)
        except ValueError:
            messagebox.showwarning("Nieprawidlowa data", "Wpisz date w formacie RRRR-MM-DD, np. 2026-05-02.")
            self.manual_entry.focus_set()
            return
        if not self._can_select(day):
            if self.selectable_only_available:
                msg = "Ten dzien nie ma danych w aktualnym zakresie. Wybierz dzien podswietlony w kalendarzu."
            else:
                msg = f"Data musi byc w zakresie {self.min_day.isoformat()} - {self.max_day.isoformat()}."
            messagebox.showwarning("Data poza zakresem", msg)
            self.manual_entry.focus_set()
            return
        self._select_day(day)

    def _render_month(self) -> None:
        for widget in self.days_frame.winfo_children():
            widget.destroy()

        self.month_var.set(MONTH_NAMES_PL[self.view_month])
        self.year_var.set(str(self.view_year))

        for col, name in enumerate(WEEKDAY_NAMES_PL):
            tk.Label(
                self.days_frame,
                text=name,
                width=4,
                background="#eef3f8",
                foreground="#334155",
                font=("Arial", 8, "bold"),
                pady=3,
            ).grid(row=0, column=col, padx=1, pady=1, sticky="nsew")

        cal = calendar.Calendar(firstweekday=0)
        weeks = cal.monthdatescalendar(self.view_year, self.view_month)
        for row_idx, week in enumerate(weeks, start=1):
            for col_idx, day in enumerate(week):
                in_month = day.month == self.view_month
                has_data = day in self.available_set
                is_selected = day == self.selected_day
                can_select = self._can_select(day) and in_month

                if is_selected:
                    bg = "#1d4ed8"
                    fg = "#ffffff"
                    font = ("Arial", 8, "bold")
                elif can_select and has_data:
                    bg = "#dbeafe"
                    fg = "#123a6f"
                    font = ("Arial", 8, "bold")
                elif can_select:
                    bg = "#f8fafc"
                    fg = "#334155"
                    font = ("Arial", 8)
                else:
                    bg = "#ffffff" if not in_month else "#f1f5f9"
                    fg = "#cbd5e1"
                    font = ("Arial", 8)

                btn = tk.Button(
                    self.days_frame,
                    text=str(day.day),
                    width=4,
                    height=1,
                    background=bg,
                    foreground=fg,
                    disabledforeground="#cbd5e1",
                    relief=tk.FLAT,
                    command=lambda d=day: self._select_day(d),
                    state=(tk.NORMAL if can_select else tk.DISABLED),
                    font=font,
                    padx=0,
                    pady=0,
                )
                btn.grid(row=row_idx, column=col_idx, padx=1, pady=1, sticky="nsew")


class DriverTimeApp(tk.Tk):
    def configure_window_for_screen(self) -> None:
        """Dopasuj skale i rozmiar okna do aktualnej rozdzielczosci ekranu."""
        try:
            screen_w = max(1024, int(self.winfo_screenwidth()))
            screen_h = max(720, int(self.winfo_screenheight()))
            base_scale = min(screen_w / 1366.0, screen_h / 768.0)
            ui_scale = max(0.88, min(1.28, base_scale))
            try:
                current_scaling = float(self.tk.call("tk", "scaling"))
                self.tk.call("tk", "scaling", max(1.0, min(2.2, current_scaling * ui_scale)))
            except Exception:
                pass

            width = min(max(1120, int(screen_w * 0.92)), max(900, screen_w - 40))
            height = min(max(720, int(screen_h * 0.88)), max(650, screen_h - 80))
            min_w = min(1080, width)
            min_h = min(680, height)
            x = max(0, (screen_w - width) // 2)
            y = max(0, (screen_h - height) // 2)
            self.geometry(f"{width}x{height}+{x}+{y}")
            self.minsize(min_w, min_h)
        except Exception:
            self.geometry("1280x820")
            self.minsize(1080, 680)

    def __init__(self) -> None:
        super().__init__()
        self.title(APP_TITLE)
        self.configure_window_for_screen()
        self.activities: list[Activity] = []
        self.violations: list[Violation] = []
        self.continuity_issues: list[ContinuityIssue] = []
        self.compensations: list[CompensationDebt] = []
        self.rest_overview: list[RestOverview] = []
        self.daily_summaries: list[DailySummary] = []
        self.country_entries: list[CountryCodeEntry] = []
        self.violation_tree_items: dict[str, Violation] = {}
        self.raw_json: Any = None
        self.input_file_path: Optional[Path] = None
        self.card_read_date: Optional[dt.date] = None
        self.card_read_date_source: str = ""
        self.driver_surname: str = "kierowca"
        self.default_date_from: Optional[dt.date] = None
        self.default_date_to: Optional[dt.date] = None
        self._build_ui()

    def _apply_modern_variant_style(self) -> None:
        """Klasyczny wygląd Tkinter/ttk, bez narzuconego kolorowego motywu."""
        try:
            self.option_add("*Font", "TkDefaultFont 10")
            self.option_add("*TCombobox*Listbox.font", "TkDefaultFont 10")
        except Exception:
            pass

    def _build_variant_header(self) -> None:
        """Nagłówek koncepcyjny z kolorowego UI jest celowo wyłączony."""
        return

    def _build_ui(self) -> None:
        self._apply_modern_variant_style()
        self._build_variant_header()
        top = ttk.Frame(self, padding=10)
        top.pack(side=tk.TOP, fill=tk.X)

        self.ddd_path = tk.StringVar()
        self.file_type = tk.StringVar(value="card")
        self.availability_as_break = tk.BooleanVar(value=False)
        self.auto_analyze_after_file = tk.BooleanVar(value=True)
        self.violations_count_text = tk.StringVar(value="Naruszenia: 0")
        self.rest_filter = tk.StringVar(value=REST_FILTER_OPTIONS[0])
        self.rests_count_text = tk.StringVar(value="Odpoczynki: 0")
        self.daily_count_text = tk.StringVar(value="Dni: 0")
        self.country_count_text = tk.StringVar(value="PL-ki/kody kraju karty: 0")
        self.chart_day_filter = tk.StringVar(value="")
        self.chart_day_labels: list[str] = []
        self.chart_zoom = 1.0
        self.chart_zoom_text = tk.StringVar(value="100%")
        self.date_from_filter = tk.StringVar(value="")
        self.date_to_filter = tk.StringVar(value="")
        self.date_filter_status = tk.StringVar(value="Zakres dat: brak danych")
        self._date_filter_after_id: Optional[str] = None
        self._suspend_date_auto_refresh = False
        self.report_format = tk.StringVar(value="HTML")
        self.report_range = tk.StringVar(value="Ostatnie 56 dni")
        self.activity_report_range = tk.StringVar(value="Ostatnie 56 dni")


        ttk.Label(top, text="Plik .DDD / .JSON:").grid(row=0, column=0, sticky="w")
        ttk.Entry(top, textvariable=self.ddd_path, width=70).grid(row=0, column=1, sticky="ew", padx=6)
        ttk.Button(top, text="Wybierz i analizuj", command=self.select_input, style="Accent.TButton").grid(row=0, column=2)
        ttk.Button(top, text="Analizuj ponownie", command=self.run_analysis).grid(row=0, column=3, padx=6)

        ttk.Label(top, text="Typ pliku:").grid(row=1, column=0, sticky="w", pady=6)
        ttk.Radiobutton(top, text="karta kierowcy", variable=self.file_type, value="card").grid(row=1, column=1, sticky="w")
        ttk.Radiobutton(top, text="pojazd/VU", variable=self.file_type, value="vu").grid(row=1, column=2, sticky="w")

        ttk.Checkbutton(
            top,
            text="Traktuj dyspozycyjność jako przerwe/odpoczynek przy kontroli 4h30",
            variable=self.availability_as_break,
        ).grid(row=2, column=1, columnspan=4, sticky="w")
        ttk.Checkbutton(
            top,
            text="Analizuj automatycznie po wybraniu pliku",
            variable=self.auto_analyze_after_file,
        ).grid(row=3, column=1, columnspan=4, sticky="w")

        date_filter_frame = ttk.LabelFrame(top, text="Zakres dat", padding=(8, 5))
        date_filter_frame.grid(row=4, column=1, columnspan=7, sticky="ew", pady=(8, 0))
        for col in range(10):
            date_filter_frame.columnconfigure(col, weight=0)
        date_filter_frame.columnconfigure(9, weight=1)
        ttk.Label(date_filter_frame, text="Od:").grid(row=0, column=0, sticky="w")
        self.date_from_entry = ttk.Entry(date_filter_frame, textvariable=self.date_from_filter, width=12, justify="center")
        self.date_from_entry.grid(row=0, column=1, sticky="w", padx=(4, 5))
        self.date_from_entry.bind("<Return>", lambda _event: self.apply_manual_date_filter())
        self.date_from_entry.bind("<FocusOut>", lambda _event: self.apply_manual_date_filter(show_error=False))
        ttk.Button(date_filter_frame, text="Kalendarz", command=lambda: self.open_date_filter_calendar("from")).grid(row=0, column=2, sticky="w")
        ttk.Label(date_filter_frame, text="Do:").grid(row=0, column=3, sticky="w", padx=(12, 0))
        self.date_to_entry = ttk.Entry(date_filter_frame, textvariable=self.date_to_filter, width=12, justify="center")
        self.date_to_entry.grid(row=0, column=4, sticky="w", padx=(4, 5))
        self.date_to_entry.bind("<Return>", lambda _event: self.apply_manual_date_filter())
        self.date_to_entry.bind("<FocusOut>", lambda _event: self.apply_manual_date_filter(show_error=False))
        ttk.Button(date_filter_frame, text="Kalendarz", command=lambda: self.open_date_filter_calendar("to")).grid(row=0, column=5, sticky="w")
        ttk.Button(date_filter_frame, text="56 dni", command=self.reset_date_range_filter).grid(row=0, column=6, sticky="w", padx=(10, 4))
        ttk.Button(date_filter_frame, text="Cały zakres", command=lambda: (self.set_full_date_range_filter(), self.refresh_all())).grid(row=0, column=7, sticky="w", padx=(4, 6))
        ttk.Label(date_filter_frame, text="Zmiana dat działa automatycznie.", foreground="#46566c").grid(row=0, column=8, sticky="w", padx=(8, 0))
        ttk.Label(date_filter_frame, textvariable=self.date_filter_status, wraplength=880, justify="left").grid(row=1, column=0, columnspan=10, sticky="w", pady=(4, 0))
        self.date_from_filter.trace_add("write", self.on_date_filter_changed)
        self.date_to_filter.trace_add("write", self.on_date_filter_changed)

        report_frame = ttk.LabelFrame(top, text="Raporty", padding=(8, 5))
        report_frame.grid(row=0, column=4, columnspan=4, rowspan=3, padx=6, sticky="w")
        ttk.Label(report_frame, text="Format:").grid(row=0, column=0, sticky="w", padx=(0, 4))
        ttk.Combobox(
            report_frame,
            textvariable=self.report_format,
            values=("HTML", "PDF"),
            state="readonly",
            width=6,
        ).grid(row=0, column=1, sticky="w", padx=(0, 10))

        ttk.Label(report_frame, text="Naruszenia:").grid(row=0, column=2, sticky="w", padx=(0, 4))
        ttk.Combobox(
            report_frame,
            textvariable=self.report_range,
            values=REPORT_RANGE_OPTIONS,
            state="readonly",
            width=18,
        ).grid(row=0, column=3, sticky="w", padx=(0, 6))
        ttk.Button(report_frame, text="Raport naruszeń", command=self.generate_report).grid(row=0, column=4, sticky="w")

        ttk.Label(report_frame, text="Aktywności:").grid(row=1, column=2, sticky="w", padx=(0, 4), pady=(4, 0))
        ttk.Combobox(
            report_frame,
            textvariable=self.activity_report_range,
            values=REPORT_RANGE_OPTIONS,
            state="readonly",
            width=18,
        ).grid(row=1, column=3, sticky="w", padx=(0, 6), pady=(4, 0))
        ttk.Button(report_frame, text="Raport aktywności", command=self.generate_activity_report).grid(row=1, column=4, sticky="w", pady=(4, 0))

        ttk.Label(report_frame, text="Wykres PDF:").grid(row=2, column=2, sticky="w", padx=(0, 4), pady=(4, 0))
        ttk.Button(
            report_frame,
            text="Raport z wykresem PDF",
            command=self.generate_activity_chart_report,
        ).grid(row=2, column=3, columnspan=2, sticky="w", padx=(0, 6), pady=(4, 0))
        top.columnconfigure(1, weight=1)

        self.notebook = ttk.Notebook(self)
        self.notebook.pack(fill=tk.BOTH, expand=True, padx=10, pady=(0, 10))

        self.summary_tab = ttk.Frame(self.notebook, padding=10)
        self.activities_tab = ttk.Frame(self.notebook, padding=10)
        self.violations_tab = ttk.Frame(self.notebook, padding=10)
        self.rests_tab = ttk.Frame(self.notebook, padding=10)
        self.daily_tab = ttk.Frame(self.notebook, padding=10)
        self.continuity_tab = ttk.Frame(self.notebook, padding=10)
        self.country_tab = ttk.Frame(self.notebook, padding=10)
        self.chart_tab = ttk.Frame(self.notebook, padding=10)
        self.log_tab = ttk.Frame(self.notebook, padding=10)
        self.about_tab = ttk.Frame(self.notebook, padding=10)

        # V3: centrum zgodnosci - naruszenia i szczegoly kontrolne sa pierwsze.
        self.notebook.add(self.violations_tab, text="Naruszenia")
        self.notebook.add(self.continuity_tab, text="Ciągłość danych")
        self.notebook.add(self.rests_tab, text="Odpoczynki")
        self.notebook.add(self.country_tab, text="PL-ki")
        self.notebook.add(self.daily_tab, text="Dni")
        self.notebook.add(self.activities_tab, text="Aktywności")
        self.notebook.add(self.chart_tab, text="Oś czasu")
        self.notebook.add(self.summary_tab, text="Przegląd")
        self.notebook.add(self.about_tab, text="O programie")

        about_frame = ttk.Frame(self.about_tab, padding=24)
        about_frame.pack(fill=tk.BOTH, expand=True)
        ttk.Label(
            about_frame,
            text="© Leszek Dyja",
            font=("Arial", 18, "bold"),
        ).pack(anchor="w", pady=(0, 8))
        ttk.Label(
            about_frame,
            text="Wszelkie prawa zastrzeżone",
            font=("Arial", 12),
        ).pack(anchor="w")

        self.summary_text = tk.Text(self.summary_tab, wrap="word", height=10)
        self.summary_text.pack(fill=tk.BOTH, expand=True)

        ttk.Label(
            self.activities_tab,
            text="Ikony: 🚚 jazda | 🛏 przerwa/odpoczynek | ⌛ dyspozycyjność | ⚒ inna praca | ❓ nieznane.",
            wraplength=1080,
            justify="left",
        ).pack(anchor="w", fill=tk.X, pady=(0, 6))
        self.activities_tree = self._tree(self.activities_tab, ["start", "koniec", "typ", "czas", "pojazd"])
        violations_header = ttk.Frame(self.violations_tab)
        violations_header.pack(fill=tk.X, pady=(0, 6))
        ttk.Label(
            violations_header,
            text="Raport z naruszen - tabela widoczna w GUI",
            font=("TkDefaultFont", 11, "bold"),
        ).pack(side=tk.LEFT, anchor="w")
        ttk.Button(violations_header, text="Otworz szczegoly", command=self.open_selected_violation_details).pack(side=tk.RIGHT, anchor="e", padx=(8, 0))
        ttk.Label(violations_header, textvariable=self.violations_count_text).pack(side=tk.RIGHT, anchor="e")
        ttk.Label(
            self.violations_tab,
            text="Dwuklik otwiera szczegóły. Tabela zawiera datę naruszenia, opis, karę, kategorię, terminy, wartość i limit.",
            wraplength=1080,
            justify="left",
        ).pack(anchor="w", fill=tk.X, pady=(0, 6))
        self.violations_tree = self._tree(
            self.violations_tab,
            ["data_naruszenia", "czego_dotyczy", "kara", "kod", "kategoria", "waga", "termin_wymagany", "start_faktyczny", "okres", "wartosc", "limit", "opis"],
            height=22,
        )
        self.violations_tree.bind("<Double-1>", self.open_violation_details_from_event)
        self.violations_tree.bind("<Return>", lambda _event: self.open_selected_violation_details())

        rests_header = ttk.Frame(self.rests_tab)
        rests_header.pack(fill=tk.X, pady=(0, 6))
        ttk.Label(
            rests_header,
            text="Przeglad odpoczynkow",
            font=("TkDefaultFont", 11, "bold"),
        ).pack(side=tk.LEFT, anchor="w")
        ttk.Label(rests_header, text="Filtr:").pack(side=tk.LEFT, padx=(18, 4))
        rest_filter_box = ttk.Combobox(
            rests_header,
            textvariable=self.rest_filter,
            values=REST_FILTER_OPTIONS,
            width=28,
            state="readonly",
        )
        rest_filter_box.pack(side=tk.LEFT, anchor="w")
        rest_filter_box.bind("<<ComboboxSelected>>", lambda _event: self.refresh_rests())
        ttk.Label(rests_header, textvariable=self.rests_count_text).pack(side=tk.RIGHT, anchor="e")
        ttk.Label(
            self.rests_tab,
            text="Filtruj odpoczynki z listy. Bloki poniżej 09:00 nie są zaliczane jako odpoczynek dobowy.",
            wraplength=1080,
            justify="left",
        ).pack(anchor="w", fill=tk.X, pady=(0, 6))
        self.rests_tree = self._tree(
            self.rests_tab,
            ["rest_type", "rest_start", "rest_end", "rest_duration", "status", "deadline_compensation", "note"],
            height=22,
        )

        daily_header = ttk.Frame(self.daily_tab)
        daily_header.pack(fill=tk.X, pady=(0, 6))
        ttk.Label(
            daily_header,
            text="Dane z kazdego dnia",
            font=("TkDefaultFont", 11, "bold"),
        ).pack(side=tk.LEFT, anchor="w")
        ttk.Label(daily_header, textvariable=self.daily_count_text).pack(side=tk.RIGHT, anchor="e")
        ttk.Label(
            self.daily_tab,
            text="Suma aktywności z każdego dnia. Aktywności przechodzące przez północ są dzielone na właściwe dni.",
            wraplength=1080,
            justify="left",
        ).pack(anchor="w", fill=tk.X, pady=(0, 6))
        self.daily_tree = self._tree(
            self.daily_tab,
            ["day", "daily_drive", "daily_break", "daily_availability", "daily_work", "daily_unknown", "daily_total", "daily_first", "daily_last", "daily_entries"],
            height=22,
        )

        country_header = ttk.Frame(self.country_tab)
        country_header.pack(fill=tk.X, pady=(0, 6))
        ttk.Label(
            country_header,
            text="Kody kraju przy karcie",
            font=("TkDefaultFont", 11, "bold"),
        ).pack(side=tk.LEFT, anchor="w")
        ttk.Label(country_header, textvariable=self.country_count_text).pack(side=tk.RIGHT, anchor="e")
        ttk.Label(
            self.country_tab,
            text="Wpisy kodu kraju przy włożeniu i wyjęciu karty. Dni bez aktywności są pomijane przy wykrywaniu braków.",
            wraplength=1080,
            justify="left",
        ).pack(anchor="w", fill=tk.X, pady=(0, 6))
        self.country_tree = self._tree(
            self.country_tab,
            ["country_time", "country_entry_type", "country_code", "country_name", "country_status", "country_day", "country_note"],
            height=22,
        )

        comp_header = ttk.Frame(self.continuity_tab)
        comp_header.pack(fill=tk.X, pady=(0, 6))
        ttk.Label(
            comp_header,
            text="Rekompensaty skroconych odpoczynkow tygodniowych",
            font=("TkDefaultFont", 11, "bold"),
        ).pack(side=tk.LEFT, anchor="w")
        ttk.Label(
            self.continuity_tab,
            text="Brakujące godziny, termin odbioru oraz potwierdzenie rekompensaty na podstawie ciągłości danych.",
            wraplength=1080,
            justify="left",
        ).pack(anchor="w", fill=tk.X, pady=(0, 6))
        self.compensation_tree = self._tree(
            self.continuity_tab,
            ["rest_start", "rest_end", "rest_duration", "shortage", "deadline", "status", "comp_period", "continuity", "recommendation"],
            height=9,
        )
        ttk.Label(
            self.continuity_tab,
            text="Luki w danych",
            font=("TkDefaultFont", 11, "bold"),
        ).pack(anchor="w", pady=(12, 6))
        self.continuity_tree = self._tree(
            self.continuity_tab,
            ["gap_start", "gap_end", "gap_duration", "gap_type", "impact", "recommendation"],
            height=9,
        )

        self.chart_container = ttk.Frame(self.chart_tab)
        self.chart_container.pack(fill=tk.BOTH, expand=True)

        self.log_text = tk.Text(self.log_tab, wrap="word")
        self.log_text.pack(fill=tk.BOTH, expand=True)
        self.refresh_violations()
        self.refresh_daily_summaries()
        self.refresh_rests()
        self.log("Gotowe. Wybierz plik .ddd - analiza uruchomi sie automatycznie. Raporty maja sugerowane nazwy: nazwisko kierowcy - data. Interfejs ma dopasowane i krótsze opisy zakładek.")

    def _setup_table_style(self) -> None:
        """Zostaw domyślny, klasyczny wygląd tabel ttk."""
        return

    def _tree(self, parent: ttk.Frame, columns: list[str], height: int = 12) -> ttk.Treeview:
        if not getattr(self, "_table_style_ready", False):
            self._setup_table_style()
            self._table_style_ready = True
        frame = ttk.Frame(parent, padding=(0, 0, 0, 4))
        frame.pack(fill=tk.BOTH, expand=True)
        tree = ttk.Treeview(frame, columns=columns, show="headings", height=height)
        vsb = ttk.Scrollbar(frame, orient="vertical", command=tree.yview)
        hsb = ttk.Scrollbar(frame, orient="horizontal", command=tree.xview)
        tree.configure(yscrollcommand=vsb.set, xscrollcommand=hsb.set)
        tree.grid(row=0, column=0, sticky="nsew")
        vsb.grid(row=0, column=1, sticky="ns")
        hsb.grid(row=1, column=0, sticky="ew")
        frame.rowconfigure(0, weight=1)
        frame.columnconfigure(0, weight=1)
        labels = {
            "day": "Data",
            "daily_drive": "🚚 Jazda",
            "daily_break": "🛏 Przerwa/odpoczynek",
            "daily_availability": "⌛ Dyspozycyjność",
            "daily_work": "⚒ Inna praca",
            "daily_unknown": "❓ Nieznane",
            "daily_total": "Suma zapisu",
            "daily_first": "Pierwsza aktywność",
            "daily_last": "Ostatnia aktywność",
            "daily_entries": "Liczba wpisów/części",
            "data_naruszenia": "Data naruszenia",
            "czego_dotyczy": "Czego dotyczy",
            "kara": "Kara wg taryfikatora",
            "kod": "Pozycja/kod",
            "kategoria": "Kategoria taryfikatora",
            "waga": "Waga analizy",
            "termin_wymagany": "Termin wymagany",
            "start_faktyczny": "Start faktyczny",
            "okres": "Okres",
            "wartosc": "Wartosc",
            "limit": "Limit",
            "opis": "Opis",
            "rest_type": "Typ odpoczynku",
            "rest_start": "Start",
            "rest_end": "Koniec",
            "rest_duration": "Czas trwania",
            "deadline_compensation": "Termin / rekompensata",
            "note": "Uwagi",
            "shortage": "Brakujaca rekompensata",
            "deadline": "Termin odbioru",
            "status": "Status",
            "comp_period": "Odebrano w okresie",
            "continuity": "Ciaglosc danych",
            "recommendation": "Rekomendacja",
            "gap_start": "Start",
            "gap_end": "Koniec",
            "gap_duration": "Czas",
            "gap_type": "Typ",
            "impact": "Wplyw",
            "country_time": "Czas wpisu",
            "country_entry_type": "Rodzaj wpisu",
            "country_code": "Kod kraju",
            "country_name": "Kraj",
            "country_status": "Status",
            "country_day": "Dzien pracy",
            "country_note": "Uwagi",
            "pojazd": "Pojazd / nr rej.",
        }
        widths = {
            "day": 105,
            "daily_drive": 95,
            "daily_break": 135,
            "daily_availability": 135,
            "daily_work": 110,
            "daily_unknown": 105,
            "daily_total": 105,
            "daily_first": 140,
            "daily_last": 140,
            "daily_entries": 130,
            "data_naruszenia": 112,
            "czego_dotyczy": 210,
            "kara": 145,
            "kod": 105,
            "kategoria": 160,
            "waga": 115,
            "termin_wymagany": 145,
            "start_faktyczny": 145,
            "okres": 185,
            "wartosc": 220,
            "limit": 250,
            "opis": 360,
            "start": 145,
            "koniec": 145,
            "typ": 165,
            "czas": 95,
            "pojazd": 135,
            "rest_type": 170,
            "rest_start": 155,
            "rest_end": 155,
            "rest_duration": 120,
            "deadline_compensation": 245,
            "note": 360,
            "shortage": 145,
            "deadline": 145,
            "status": 160,
            "comp_period": 230,
            "continuity": 220,
            "recommendation": 360,
            "gap_start": 145,
            "gap_end": 145,
            "gap_duration": 90,
            "gap_type": 160,
            "impact": 340,
            "country_time": 145,
            "country_entry_type": 230,
            "country_code": 88,
            "country_name": 145,
            "country_status": 130,
            "country_day": 105,
            "country_note": 380,
        }
        anchors = {
            "day": "center",
            "daily_drive": "center",
            "daily_break": "center",
            "daily_availability": "center",
            "daily_work": "center",
            "daily_unknown": "center",
            "daily_total": "center",
            "daily_first": "center",
            "daily_last": "center",
            "daily_entries": "center",
            "kara": "center",
            "kod": "center",
            "waga": "center",
            "termin_wymagany": "center",
            "start_faktyczny": "center",
            "deadline": "center",
            "status": "center",
            "czas": "center",
            "pojazd": "center",
            "gap_duration": "center",
            "rest_duration": "center",
            "rest_type": "center",
            "deadline_compensation": "center",
            "shortage": "center",
            "country_time": "center",
            "country_code": "center",
            "country_status": "center",
            "country_day": "center",
        }
        for col in columns:
            tree.heading(col, text=labels.get(col, col.replace("_", " ")))
            tree.column(col, width=widths.get(col, 130), minwidth=70, stretch=True, anchor=anchors.get(col, "w"))
        # Klasyczny UI: bez kolorowania wierszy tabel.
        return tree

    def _row_tags(self, index: int, severity_or_status: str = "") -> tuple[str, ...]:
        base = "even" if index % 2 == 0 else "odd"
        text = (severity_or_status or "").lower()
        if "kryty" in text or "nieodebrano" in text:
            return (base, "critical")
        if "wysok" in text:
            return (base, "high")
        if "sred" in text or "śred" in text or "skrocon" in text or "skrócon" in text or "do rekompensaty" in text:
            return (base, "medium")
        if "weryfik" in text or "nie mozna" in text or "nie można" in text or "niepew" in text:
            return (base, "verify")
        if "odebrano" in text or "pelna" in text or "pełna" in text or text == "ok":
            return (base, "ok")
        if "brak" in text:
            return (base, "verify")
        if text == "pl":
            return (base, "ok")
        if "inform" in text:
            return (base, "info")
        return (base,)

    def _auto_fit_tree_columns(self, tree: ttk.Treeview, max_width: int = 360) -> None:
        """Dopasuj szerokości kolumn do nagłówków i widocznych wartości."""
        try:
            font = tkfont.nametofont("TkDefaultFont")
        except Exception:
            font = None

        for col in tree["columns"]:
            header = str(tree.heading(col, "text") or col)
            if font:
                wanted = font.measure(header) + 34
            else:
                wanted = len(header) * 8 + 34
            for item in tree.get_children("")[:80]:
                values = tree.item(item, "values")
                try:
                    idx = list(tree["columns"]).index(col)
                    value = str(values[idx]) if idx < len(values) else ""
                except Exception:
                    value = ""
                if font:
                    wanted = max(wanted, font.measure(value) + 28)
                else:
                    wanted = max(wanted, len(value) * 8 + 28)
            wanted = max(78, min(max_width, wanted))
            compact_columns = {"czas", "daily_drive", "daily_break", "daily_availability", "daily_work", "daily_unknown", "daily_total", "daily_entries", "country_code", "kod", "kara", "waga", "gap_duration", "rest_duration", "shortage"}
            if col in compact_columns:
                wanted = min(wanted, 150)
            tree.column(col, width=wanted)

    def select_input(self) -> None:
        path = filedialog.askopenfilename(
            title="Wybierz plik .ddd albo JSON",
            filetypes=[("Pliki tachografu/JSON", "*.ddd *.DDD *.json"), ("Wszystkie pliki", "*.*")],
        )
        if path:
            self.ddd_path.set(path)
            self.log(f"Wybrano plik: {path}")
            if self.auto_analyze_after_file.get():
                self.run_analysis(show_finished_message=False)

    def select_parser(self) -> None:
        # Zachowane tylko dla kompatybilnosci ze starszymi wersjami pliku.
        return

    def update_parser_status(self) -> None:
        # Zachowane tylko dla kompatybilnosci ze starszymi wersjami pliku.
        return

    def log(self, text: str) -> None:
        self.log_text.insert(tk.END, f"[{dt.datetime.now():%H:%M:%S}] {text}\n")
        self.log_text.see(tk.END)

    def run_analysis(self, show_finished_message: bool = True) -> None:
        try:
            path = Path(self.ddd_path.get().strip())
            self.input_file_path = path
            if not path.exists():
                messagebox.showwarning("Brak pliku", "Wybierz istniejacy plik .ddd albo .json.")
                return
            if path.suffix.lower() == ".json":
                self.raw_json = json.loads(path.read_text(encoding="utf-8"))
                self.log(f"Wczytano JSON: {path}")
            else:
                if self.file_type.get() != "card":
                    messagebox.showwarning(
                        "Obsluga VU w kolejnej wersji",
                        "Ta wersja obsluguje pliki .DDD z karty kierowcy. "
                        "Dla plikow pojazdu/VU dodamy osobny modul odczytu danych.",
                    )
                    return
                self.log("Odczytuje dane z pliku .DDD: aktywnosci oraz kody kraju przy wlozeniu/wyjeciu karty")
                self.raw_json = parse_ddd_driver_card_embedded(path)
                out_json = path.with_suffix(path.suffix + ".json")
                out_json.write_text(json.dumps(self.raw_json, ensure_ascii=False, indent=2), encoding="utf-8")
                self.log(f"Zapisano JSON pomocniczy: {out_json}")

            self.activities = normalize_activities(self.raw_json)
            self.continuity_issues = analyze_data_continuity(self.activities)
            self.compensations = analyze_weekly_rest_compensations(self.activities)
            self.rest_overview = analyze_rest_overview(self.activities, self.compensations)
            self.daily_summaries = analyze_daily_summaries(self.activities)
            self.country_entries = analyze_country_code_entries(extract_country_code_entries(self.raw_json), self.daily_summaries)
            self.violations = analyze(self.activities, self.availability_as_break.get())
            self.card_read_date, self.card_read_date_source = infer_card_read_date(self.raw_json, self.activities, self.input_file_path)
            self.driver_surname = infer_driver_surname(self.raw_json, self.input_file_path)
            self.set_default_56_day_date_range_filter()
            self.refresh_all()
            self.notebook.select(self.violations_tab)
            if not self.activities:
                messagebox.showinfo(
                    "Brak aktywności",
                    "Nie znalazlem aktywności w pliku .DDD. Wygenerowany JSON diagnostyczny zostal zapisany obok pliku; trzeba sprawdzic, czy to karta kierowcy i czy sekcja EF 0x0504 jest obecna.",
                )
            else:
                self.log(f"Analiza zakonczona. Aktywności: {len(self.activities)}, naruszenia: {len(self.violations)}")
                if show_finished_message:
                    messagebox.showinfo("Analiza zakonczona", f"Aktywności: {len(self.activities)}\nNaruszenia: {len(self.violations)}")
        except Exception as exc:
            self.log(traceback.format_exc())
            messagebox.showerror("Blad analizy", str(exc))

    def load_demo(self) -> None:
        base = dt.datetime.now().replace(hour=6, minute=0, second=0, microsecond=0) - dt.timedelta(days=2)
        self.activities = [
            Activity(base, base + dt.timedelta(hours=4, minutes=40), "DRIVING", "demo"),
            Activity(base + dt.timedelta(hours=4, minutes=40), base + dt.timedelta(hours=5), "REST", "demo"),
            Activity(base + dt.timedelta(hours=5), base + dt.timedelta(hours=10, minutes=30), "DRIVING", "demo"),
            Activity(base + dt.timedelta(hours=10, minutes=30), base + dt.timedelta(hours=11), "WORK", "demo"),
            Activity(base + dt.timedelta(hours=11), base + dt.timedelta(hours=19, minutes=45), "REST", "demo"),
            Activity(base + dt.timedelta(days=1, hours=6), base + dt.timedelta(days=1, hours=16, minutes=20), "DRIVING", "demo"),
            Activity(base + dt.timedelta(days=1, hours=16, minutes=20), base + dt.timedelta(days=2, hours=3, minutes=30), "REST", "demo"),
        ]
        self.continuity_issues = analyze_data_continuity(self.activities)
        self.compensations = analyze_weekly_rest_compensations(self.activities)
        self.rest_overview = analyze_rest_overview(self.activities, self.compensations)
        self.daily_summaries = analyze_daily_summaries(self.activities)
        self.country_entries = analyze_country_code_entries([
            CountryCodeEntry(base, "Wlozenie karty - kod kraju", "PL", "Polska", "PL", f"{base:%Y-%m-%d}", "Dane przykladowe: wlozenie karty w PL", "demo"),
            CountryCodeEntry(base + dt.timedelta(hours=19, minutes=45), "Wyjecie karty - kod kraju", "PL", "Polska", "PL", f"{base:%Y-%m-%d}", "Dane przykladowe: wyjecie karty w PL", "demo"),
        ], self.daily_summaries)
        self.violations = analyze(self.activities, self.availability_as_break.get())
        self.card_read_date = max(activity.end.date() for activity in self.activities)
        self.card_read_date_source = "dane przykladowe"
        self.driver_surname = "Kowalski"
        self.set_default_56_day_date_range_filter()
        self.refresh_all()
        self.notebook.select(self.continuity_tab)
        self.log("Wczytano dane przykladowe.")

    def data_days(self) -> list[dt.date]:
        """Return all days present in analysis results."""
        days: set[dt.date] = set()
        for activity in self.activities:
            days.update(_activity_days(activity))
        for violation in self.violations:
            days.update(_activity_days(Activity(violation.start, violation.end, "UNKNOWN")))
        for rest in self.rest_overview:
            days.update(_activity_days(Activity(rest.start, rest.end, "REST")))
        for daily in self.daily_summaries:
            days.add(daily.day)
        for country in self.country_entries:
            days.add(country.timestamp.date())
            related = _safe_date_from_text(country.related_day)
            if related:
                days.add(related)
        for issue in self.continuity_issues:
            days.update(_activity_days(Activity(issue.start, issue.end, "UNKNOWN")))
        for comp in self.compensations:
            days.update(_activity_days(Activity(comp.reduced_rest_start, comp.reduced_rest_end, "REST")))
            days.add(comp.deadline.date())
        return sorted(days)

    def available_filter_days(self) -> list[dt.date]:
        """Return selectable days for date-range pickers, including default 56-day window."""
        days = set(self.data_days())
        if self.default_date_from and self.default_date_to:
            days.update(date_span_days(self.default_date_from, self.default_date_to))
        elif self.card_read_date:
            date_from, date_to = default_56_day_range(self.card_read_date)
            days.update(date_span_days(date_from, date_to))
        return sorted(days)

    def set_default_56_day_date_range_filter(self) -> None:
        """After analysis set default range: card readout date minus 56 days through readout day."""
        if not self.card_read_date:
            days = self.data_days()
            if not days:
                self._suspend_date_auto_refresh = True
                try:
                    self.date_from_filter.set("")
                    self.date_to_filter.set("")
                finally:
                    self._suspend_date_auto_refresh = False
                self.default_date_from = None
                self.default_date_to = None
                self.date_filter_status.set("Zakres dat: brak danych")
                return
            self.card_read_date = days[-1]
            self.card_read_date_source = "ostatni dzien danych"

        self.default_date_from, self.default_date_to = default_56_day_range(self.card_read_date)
        self._suspend_date_auto_refresh = True
        try:
            self.date_from_filter.set(self.default_date_from.isoformat())
            self.date_to_filter.set(self.default_date_to.isoformat())
        finally:
            self._suspend_date_auto_refresh = False
        self._update_date_filter_status()
        self.log(
            f"Domyslny zakres ustawiony na 56 dni przed dniem odczytania karty: "
            f"{self.default_date_from.isoformat()} - {self.default_date_to.isoformat()} "
            f"(data odczytu: {self.card_read_date.isoformat()}, zrodlo: {self.card_read_date_source})."
        )

    def set_full_date_range_filter(self) -> None:
        """Set range filter to all dates found in analysis results."""
        days = self.data_days()
        if not days:
            self._suspend_date_auto_refresh = True
            try:
                self.date_from_filter.set("")
                self.date_to_filter.set("")
            finally:
                self._suspend_date_auto_refresh = False
            self.date_filter_status.set("Zakres dat: brak danych")
            return
        self._suspend_date_auto_refresh = True
        try:
            self.date_from_filter.set(days[0].isoformat())
            self.date_to_filter.set(days[-1].isoformat())
        finally:
            self._suspend_date_auto_refresh = False
        self._update_date_filter_status()

    def reset_date_range_filter(self) -> None:
        self.set_default_56_day_date_range_filter()
        self.refresh_all()

    def on_date_filter_changed(self, *_args: object) -> None:
        """Automatycznie zastosuj zakres dat po zmianie pola Od/Do."""
        if self._suspend_date_auto_refresh:
            return
        if self._date_filter_after_id:
            try:
                self.after_cancel(self._date_filter_after_id)
            except tk.TclError:
                pass
        self._date_filter_after_id = self.after(450, self.auto_apply_date_filter)

    def auto_apply_date_filter(self) -> None:
        self._date_filter_after_id = None
        raw_from = self.date_from_filter.get().strip()
        raw_to = self.date_to_filter.get().strip()
        for raw in (raw_from, raw_to):
            if raw and len(raw) < 10:
                self.date_filter_status.set("Wpisz pełną datę w formacie RRRR-MM-DD. Zakres zastosuje się automatycznie.")
                return
            if raw and _safe_date_from_text(raw) is None:
                self.date_filter_status.set("Nieprawidłowa data - wpisz RRRR-MM-DD.")
                return
        self.apply_manual_date_filter(show_error=False)

    def apply_manual_date_filter(self, show_error: bool = True) -> None:
        """Zastosuj daty wpisane recznie w polach Od/Do."""
        raw_from = self.date_from_filter.get().strip()
        raw_to = self.date_to_filter.get().strip()
        try:
            date_from = dt.date.fromisoformat(raw_from) if raw_from else None
            date_to = dt.date.fromisoformat(raw_to) if raw_to else None
        except ValueError:
            self.date_filter_status.set("Nieprawidlowa data - wpisz RRRR-MM-DD")
            if show_error:
                messagebox.showwarning("Nieprawidlowa data", "Wpisz date w formacie RRRR-MM-DD, np. 2026-05-02.")
            return
        if date_from and date_to and date_from > date_to:
            date_from, date_to = date_to, date_from
            self._suspend_date_auto_refresh = True
            try:
                self.date_from_filter.set(date_from.isoformat())
                self.date_to_filter.set(date_to.isoformat())
            finally:
                self._suspend_date_auto_refresh = False
        self._update_date_filter_status()
        self.refresh_all()

    def _selected_date_range(self) -> tuple[Optional[dt.date], Optional[dt.date]]:
        date_from = _safe_date_from_text(self.date_from_filter.get())
        date_to = _safe_date_from_text(self.date_to_filter.get())
        if date_from and date_to and date_from > date_to:
            date_from, date_to = date_to, date_from
            self._suspend_date_auto_refresh = True
            try:
                self.date_from_filter.set(date_from.isoformat())
                self.date_to_filter.set(date_to.isoformat())
            finally:
                self._suspend_date_auto_refresh = False
        return date_from, date_to

    def _selected_datetime_range(self) -> tuple[Optional[dt.datetime], Optional[dt.datetime]]:
        date_from, date_to = self._selected_date_range()
        start = dt.datetime.combine(date_from, dt.time.min) if date_from else None
        end_excl = dt.datetime.combine(date_to + dt.timedelta(days=1), dt.time.min) if date_to else None
        return start, end_excl

    def _update_date_filter_status(self) -> None:
        date_from, date_to = self._selected_date_range()
        read_suffix = f" | odczyt karty: {self.card_read_date.isoformat()}" if self.card_read_date else ""
        if date_from and date_to:
            self.date_filter_status.set(f"Aktywny zakres: {date_from.isoformat()} - {date_to.isoformat()}{read_suffix}")
        elif date_from:
            self.date_filter_status.set(f"Aktywny zakres: od {date_from.isoformat()}{read_suffix}")
        elif date_to:
            self.date_filter_status.set(f"Aktywny zakres: do {date_to.isoformat()}{read_suffix}")
        else:
            self.date_filter_status.set(f"Zakres dat: caly okres / brak danych{read_suffix}")

    def open_date_filter_calendar(self, side: str) -> None:
        days = self.available_filter_days()
        if not days:
            messagebox.showwarning("Brak danych", "Najpierw wykonaj analize pliku, aby wybrac zakres dat.")
            return
        date_from, date_to = self._selected_date_range()
        selected = date_from if side == "from" else date_to
        if selected not in set(days):
            selected = days[0] if side == "from" else days[-1]

        def apply_selected(day: dt.date) -> None:
            self._suspend_date_auto_refresh = True
            try:
                if side == "from":
                    self.date_from_filter.set(day.isoformat())
                else:
                    self.date_to_filter.set(day.isoformat())
            finally:
                self._suspend_date_auto_refresh = False
            self.apply_manual_date_filter(show_error=False)

        CalendarDatePicker(
            self,
            available_days=days,
            selected_day=selected,
            on_select=apply_selected,
            title=("Wybierz date poczatkowa" if side == "from" else "Wybierz date koncowa"),
            info_text="Wybierz date z kalendarza albo wpisz recznie RRRR-MM-DD. Zakres dziala we wszystkich zakladkach.",
            selectable_only_available=False,
        )

    def _activity_in_selected_range(self, activity: Activity) -> bool:
        start, end_excl = self._selected_datetime_range()
        return _period_overlaps_range(activity.start, activity.end, start, end_excl)

    def _violation_in_selected_range(self, violation: Violation) -> bool:
        start, end_excl = self._selected_datetime_range()
        return _period_overlaps_range(violation.start, violation.end, start, end_excl)

    def _rest_in_selected_range(self, rest: RestOverview) -> bool:
        start, end_excl = self._selected_datetime_range()
        return _period_overlaps_range(rest.start, rest.end, start, end_excl)

    def _daily_in_selected_range(self, summary: DailySummary) -> bool:
        date_from, date_to = self._selected_date_range()
        return _date_in_range(summary.day, date_from, date_to)

    def _country_in_selected_range(self, entry: CountryCodeEntry) -> bool:
        date_from, date_to = self._selected_date_range()
        if _date_in_range(entry.timestamp.date(), date_from, date_to):
            return True
        related = _safe_date_from_text(entry.related_day)
        return bool(related and _date_in_range(related, date_from, date_to))

    def _continuity_issue_in_selected_range(self, issue: ContinuityIssue) -> bool:
        start, end_excl = self._selected_datetime_range()
        return _period_overlaps_range(issue.start, issue.end, start, end_excl)

    def _compensation_in_selected_range(self, comp: CompensationDebt) -> bool:
        start, end_excl = self._selected_datetime_range()
        if _period_overlaps_range(comp.reduced_rest_start, comp.reduced_rest_end, start, end_excl):
            return True
        date_from, date_to = self._selected_date_range()
        return _date_in_range(comp.deadline.date(), date_from, date_to)

    def filtered_activities(self) -> list[Activity]:
        return [a for a in self.activities if self._activity_in_selected_range(a)]

    def filtered_violations(self) -> list[Violation]:
        return [v for v in self.violations if self._violation_in_selected_range(v)]

    def filtered_daily_summaries(self) -> list[DailySummary]:
        return [d for d in self.daily_summaries if self._daily_in_selected_range(d)]

    def filtered_rest_overview(self) -> list[RestOverview]:
        return [r for r in self.rest_overview if self._rest_in_selected_range(r)]

    def filtered_country_entries(self) -> list[CountryCodeEntry]:
        return [c for c in self.country_entries if self._country_in_selected_range(c)]

    def filtered_continuity_issues(self) -> list[ContinuityIssue]:
        return [i for i in self.continuity_issues if self._continuity_issue_in_selected_range(i)]

    def filtered_compensations(self) -> list[CompensationDebt]:
        return [c for c in self.compensations if self._compensation_in_selected_range(c)]

    def refresh_all(self) -> None:
        self._update_date_filter_status()
        self.refresh_summary()
        self.refresh_activities()
        self.refresh_violations()
        self.refresh_daily_summaries()
        self.refresh_rests()
        self.refresh_continuity()
        self.refresh_country_codes()
        self.refresh_chart()

    def refresh_summary(self) -> None:
        self.summary_text.delete("1.0", tk.END)
        filtered_activities = self.filtered_activities()
        filtered_violations = self.filtered_violations()
        filtered_continuity = self.filtered_continuity_issues()
        filtered_compensations = self.filtered_compensations()
        filtered_rests = self.filtered_rest_overview()
        filtered_daily = self.filtered_daily_summaries()
        filtered_country = self.filtered_country_entries()
        total_drive = sum((a.duration for a in filtered_activities if a.kind == "DRIVING"), dt.timedelta(0))
        total_work = sum((a.duration for a in filtered_activities if a.kind == "WORK"), dt.timedelta(0))
        total_rest = sum((a.duration for a in filtered_activities if a.kind == "REST"), dt.timedelta(0))
        total_avail = sum((a.duration for a in filtered_activities if a.kind == "AVAILABILITY"), dt.timedelta(0))
        start = filtered_activities[0].start if filtered_activities else "-"
        end = filtered_activities[-1].end if filtered_activities else "-"
        lines = [
            "PODSUMOWANIE",
            f"Zakres danych: {start} - {end}",
            f"Aktywny filtr dat: {self.date_filter_status.get()}",
            f"Liczba aktywności w zakresie: {len(filtered_activities)} / {len(self.activities)}",
            f"Liczba naruszen/ostrzezen w zakresie: {len(filtered_violations)} / {len(self.violations)}",
            f"Luki/niepewne okresy ciaglosci danych w zakresie: {len(filtered_continuity)} / {len(self.continuity_issues)}",
            f"Skrocone odpoczynki tygodniowe do rekompensaty w zakresie: {len(filtered_compensations)} / {len(self.compensations)}",
            f"Odpoczynki w przegladzie w zakresie: {len(filtered_rests)} / {len(self.rest_overview)}",
            f"Dni w zestawieniu dziennym w zakresie: {len(filtered_daily)} / {len(self.daily_summaries)}",
            f"Wpisy kodu kraju przy wlozeniu/wyjeciu karty w zakresie: {len(filtered_country)} / {len(self.country_entries)}",
            f"Szacowana suma kar wg taryfikatora w zakresie: {estimated_total_penalty(filtered_violations)} zl",
            "Raport naruszen z taryfikatorem jest dostepny w zakladce: Raport naruszen.",
            "Zestawienie jazdy, przerw, dyspozycyjnośći i pracy z kazdego dnia jest dostepne w zakladce: Dane dzienne.",
            "Przeglad odpoczynkow z filtrem jest dostepny w zakladce: Odpoczynki.",
            "Analiza ciaglosci i rekompensat jest dostepna w zakladce: Ciaglosc i rekompensaty.",
            "Analiza PL-ek/kodow kraju rozpoczecia i zakonczenia jest dostepna w zakladce: PL-ki / kody kraju.",
            "",
            f"Jazda: {fmt_hours(total_drive)}",
            f"Inna praca: {fmt_hours(total_work)}",
            f"Dyspozycyjność: {fmt_hours(total_avail)}",
            f"Odpoczynek/przerwa: {fmt_hours(total_rest)}",
            "",
            "Zakres MVP:",
            "- art. 6: 9/10h dziennie, 56h tygodniowo, 90h w 2 tygodnie",
            "- art. 7: przerwa 45 min po 4h30 jazdy albo 15+30",
            "- art. 8: kontrola odpoczynku dobowego/tygodniowego oraz rekompensaty skroconego odpoczynku tygodniowego",
            "",
            "Nie obsluzono jeszcze wyjatkow: zaloga kilkuosobowa, prom/pociag, przewozy okazjonalne, odstepstwa art. 12, przypisanie jednej dlugiej pauzy do wielu rekompensat.",
        ]
        self.summary_text.insert(tk.END, "\n".join(lines))

    def refresh_activities(self) -> None:
        self.activities_tree.delete(*self.activities_tree.get_children())
        filtered = self.filtered_activities()
        if not filtered:
            self.activities_tree.insert("", tk.END, values=("-", "-", "Brak aktywności w wybranym zakresie dat", "-", "-"), tags=("info",))
            return
        for idx, a in enumerate(filtered):
            self.activities_tree.insert("", tk.END, values=(a.start, a.end, activity_table_label_with_manual_icon(a), fmt_duration_report(a.duration), vehicle_registration_display(a)), tags=self._row_tags(idx, a.kind))
        self._auto_fit_tree_columns(self.activities_tree, max_width=260)

    def refresh_violations(self) -> None:
        self.violations_tree.delete(*self.violations_tree.get_children())
        self.violation_tree_items.clear()
        filtered = self.filtered_violations()
        self.violations_count_text.set(f"Naruszenia: {len(filtered)} / {len(self.violations)}")
        if not filtered:
            self.violations_tree.insert(
                "",
                tk.END,
                values=("-", "Brak naruszen w wybranym zakresie dat", "-", "-", "-", "-", "-", "-", "-", "-", "-", "Zmien zakres dat albo wykonaj analize pliku."),
                tags=("ok",),
            )
            return
        for idx, v in enumerate(filtered):
            item_id = self.violations_tree.insert("", tk.END, values=violation_report_row(v), tags=self._row_tags(idx, v.severity))
            self.violation_tree_items[item_id] = v
        self._auto_fit_tree_columns(self.violations_tree, max_width=360)

    def refresh_daily_summaries(self) -> None:
        self.daily_tree.delete(*self.daily_tree.get_children())
        filtered = self.filtered_daily_summaries()
        self.daily_count_text.set(f"Dni: {len(filtered)} / {len(self.daily_summaries)}")
        if not filtered:
            self.daily_tree.insert(
                "",
                tk.END,
                values=("-", "-", "-", "-", "-", "-", "-", "-", "-", "Brak danych dziennych w wybranym zakresie dat."),
                tags=("info",),
            )
            return
        for idx, item in enumerate(filtered):
            tag_hint = ""
            if item.unknown > dt.timedelta(0):
                tag_hint = "do weryfikacji"
            self.daily_tree.insert("", tk.END, values=daily_summary_row_gui(item), tags=self._row_tags(idx, tag_hint))
        self._auto_fit_tree_columns(self.daily_tree, max_width=220)

    def refresh_rests(self) -> None:
        self.rests_tree.delete(*self.rests_tree.get_children())
        selected_filter = self.rest_filter.get() or REST_FILTER_OPTIONS[0]
        in_range = self.filtered_rest_overview()
        filtered = [item for item in in_range if rest_matches_filter(item, selected_filter)]
        self.rests_count_text.set(f"Odpoczynki: {len(filtered)} / {len(self.rest_overview)}")

        if not filtered:
            self.rests_tree.insert(
                "",
                tk.END,
                values=(selected_filter, "-", "-", "-", "Brak danych", "-", "Brak odpoczynkow dla wybranego typu i zakresu dat."),
                tags=("info",),
            )
            return
        for idx, item in enumerate(filtered):
            self.rests_tree.insert("", tk.END, values=rest_overview_row(item), tags=self._row_tags(idx, item.status + " " + item.rest_type))
        self._auto_fit_tree_columns(self.rests_tree, max_width=360)

    def refresh_country_codes(self) -> None:
        self.country_tree.delete(*self.country_tree.get_children())
        filtered = self.filtered_country_entries()
        self.country_count_text.set(f"PL-ki/kody kraju karty: {len(filtered)} / {len(self.country_entries)}")
        if not filtered:
            self.country_tree.insert(
                "",
                tk.END,
                values=("-", "Brak wpisow kraju", "-", "-", "Brak danych", "-", "Brak PL-ek/kodow kraju w wybranym zakresie dat."),
                tags=("info",),
            )
            return
        for idx, item in enumerate(filtered):
            self.country_tree.insert("", tk.END, values=country_code_row(item), tags=self._row_tags(idx, item.status))
        self._auto_fit_tree_columns(self.country_tree, max_width=360)

    def refresh_continuity(self) -> None:
        self.compensation_tree.delete(*self.compensation_tree.get_children())
        self.continuity_tree.delete(*self.continuity_tree.get_children())

        filtered_compensations = self.filtered_compensations()
        filtered_issues = self.filtered_continuity_issues()

        if not filtered_compensations:
            self.compensation_tree.insert(
                "",
                tk.END,
                values=("-", "-", "-", "-", "-", "Brak skroconych odpoczynkow tygodniowych", "-", "-", "Brak rekompensat w wybranym zakresie dat."),
                tags=("ok",),
            )
        else:
            for idx, item in enumerate(filtered_compensations):
                self.compensation_tree.insert("", tk.END, values=compensation_report_row(item), tags=self._row_tags(idx, item.status))

        if not filtered_issues:
            self.continuity_tree.insert(
                "",
                tk.END,
                values=("-", "-", "-", "Pelna ciaglosc", "Brak wykrytych luk w wybranym zakresie dat.", "Nie trzeba uzupelniac danych dla wykrytego zakresu."),
                tags=("ok",),
            )
        else:
            for idx, issue in enumerate(filtered_issues):
                self.continuity_tree.insert("", tk.END, values=continuity_report_row(issue), tags=self._row_tags(idx, issue.kind))
        self._auto_fit_tree_columns(self.compensation_tree, max_width=360)
        self._auto_fit_tree_columns(self.continuity_tree, max_width=360)

    def open_violation_details_from_event(self, event: tk.Event) -> None:
        """Open violation details after double-clicking a row in the violations table."""
        row_id = self.violations_tree.identify_row(event.y)
        if not row_id:
            return
        self.violations_tree.selection_set(row_id)
        self.violations_tree.focus(row_id)
        violation = self.violation_tree_items.get(row_id)
        if violation is not None:
            self.show_violation_details_window(violation)

    def open_selected_violation_details(self) -> None:
        """Open details for the currently selected violation row."""
        selected = self.violations_tree.selection()
        if not selected:
            messagebox.showinfo("Brak wyboru", "Zaznacz naruszenie w tabeli albo kliknij je dwukrotnie.")
            return
        violation = self.violation_tree_items.get(selected[0])
        if violation is None:
            messagebox.showinfo("Brak naruszenia", "Ten wiersz jest informacyjny. Po analizie dwuklik na naruszeniu otworzy szczegoly.")
            return
        self.show_violation_details_window(violation)

    def show_violation_details_window(self, violation: Violation) -> None:
        """Show one violation in a maximized top-level window."""
        win = tk.Toplevel(self)
        win.title(f"Szczegoly naruszenia - {violation.rule}")
        win.transient(self)
        win.grab_set()

        # Pelne okno: Windows obsluguje state('zoomed'), Linux czesto attributes('-zoomed', True).
        try:
            win.state("zoomed")
        except tk.TclError:
            try:
                win.attributes("-zoomed", True)
            except tk.TclError:
                width = max(1000, win.winfo_screenwidth() - 80)
                height = max(700, win.winfo_screenheight() - 80)
                win.geometry(f"{width}x{height}+20+20")

        container = ttk.Frame(win, padding=16)
        container.pack(fill=tk.BOTH, expand=True)
        container.columnconfigure(1, weight=1)

        ttk.Label(container, text="Szczegoly naruszenia", font=("TkDefaultFont", 16, "bold")).grid(
            row=0, column=0, columnspan=2, sticky="w", pady=(0, 14)
        )

        tariff = tariff_for_violation(violation)
        rows = [
            ("Data naruszenia", fmt_date(violation.start)),
            ("Czego dotyczy", violation.rule),
            ("Kara wg taryfikatora", tariff.amount),
            ("Pozycja/kod", tariff.code),
            ("Kategoria taryfikatora", tariff.category),
            ("Podstawa taryfikatora", tariff.basis),
            ("Uwagi taryfikatora", tariff.note),
            ("Waga analizy", violation.severity),
            ("Termin wymagany", required_rest_start_deadline(violation)),
            ("Start faktyczny", actual_rest_start_for_violation(violation)),
            ("Okres", fmt_violation_period(violation)),
            ("Wartosc", violation.value),
            ("Limit", violation.limit),
            ("Opis", violation.description),
        ]
        for index, (label, value) in enumerate(rows, start=1):
            ttk.Label(container, text=label + ":", font=("TkDefaultFont", 10, "bold")).grid(
                row=index, column=0, sticky="nw", padx=(0, 12), pady=5
            )
            ttk.Label(container, text=value, wraplength=1000, justify="left").grid(
                row=index, column=1, sticky="new", pady=5
            )

        details_row = len(rows) + 1
        buttons_row = details_row + 1
        container.rowconfigure(details_row, weight=1)

        details = (
            "RAPORT NARUSZENIA\n"
            f"Data naruszenia: {fmt_date(violation.start)}\n"
            f"Czego dotyczy: {violation.rule}\n"
            f"Kara wg taryfikatora: {tariff.amount}\n"
            f"Pozycja/kod: {tariff.code}\n"
            f"Kategoria taryfikatora: {tariff.category}\n"
            f"Podstawa taryfikatora: {tariff.basis}\n"
            f"Uwagi taryfikatora: {tariff.note}\n"
            f"Waga analizy: {violation.severity}\n"
            f"Termin wymagany: {required_rest_start_deadline(violation)}\n"
            f"Start faktyczny: {actual_rest_start_for_violation(violation)}\n"
            f"Okres: {fmt_violation_period(violation)}\n"
            f"Wartosc: {violation.value}\n"
            f"Limit: {violation.limit}\n"
            f"Opis: {violation.description}\n"
        )

        text_frame = ttk.Frame(container)
        text_frame.grid(row=details_row, column=0, columnspan=2, sticky="nsew", pady=(16, 10))
        text_frame.rowconfigure(0, weight=1)
        text_frame.columnconfigure(0, weight=1)
        text = tk.Text(text_frame, wrap="word")
        scroll = ttk.Scrollbar(text_frame, orient="vertical", command=text.yview)
        text.configure(yscrollcommand=scroll.set)
        text.grid(row=0, column=0, sticky="nsew")
        scroll.grid(row=0, column=1, sticky="ns")
        text.insert("1.0", details)
        text.configure(state="disabled")

        buttons = ttk.Frame(container)
        buttons.grid(row=buttons_row, column=0, columnspan=2, sticky="e")

        def copy_details() -> None:
            win.clipboard_clear()
            win.clipboard_append(details)
            win.update()

        ttk.Button(buttons, text="Kopiuj szczegoly", command=copy_details).pack(side=tk.LEFT, padx=(0, 8))
        ttk.Button(buttons, text="Zamknij", command=win.destroy).pack(side=tk.LEFT)
        win.bind("<Escape>", lambda _event: win.destroy())
        win.focus_force()

    def open_chart_calendar(self) -> None:
        """Otwiera kalendarz wyboru dnia wykresu bez użycia zewnętrznych bibliotek."""
        if not self.activities:
            messagebox.showwarning("Brak danych", "Najpierw wykonaj analizę pliku.")
            return

        activities = sorted(self.filtered_activities(), key=lambda a: (a.start, a.end))
        available_days = sorted({d for a in activities for d in _activity_days(a)})
        if not available_days:
            messagebox.showwarning("Brak dni", "Brak dni do pokazania na wykresie.")
            return

        current_text = self.chart_day_filter.get()
        try:
            selected_day = dt.date.fromisoformat(current_text)
        except ValueError:
            selected_day = available_days[0]
        if selected_day not in set(available_days):
            selected_day = available_days[0]

        CalendarDatePicker(
            self,
            available_days=available_days,
            selected_day=selected_day,
            on_select=self.set_chart_day_from_calendar,
            title="Wybierz dzien wykresu",
            info_text="Dla wykresu aktywne sa tylko dni z aktywnosciami w aktualnym zakresie dat.",
            selectable_only_available=True,
        )

    def set_chart_day_from_calendar(self, selected_day: dt.date) -> None:
        """Ustawia dzien wskazany w kalendarzu i odswieza wykres."""
        self.chart_day_filter.set(selected_day.isoformat())
        self.refresh_chart()

    def _chart_day_bounds(self) -> tuple[Optional[dt.date], Optional[dt.date]]:
        """Zwraca minimalny i maksymalny dzien dostepny dla wykresu po aktualnym filtrze."""
        activities = sorted(self.filtered_activities(), key=lambda a: (a.start, a.end))
        days = sorted({d for a in activities for d in _activity_days(a)})
        if not days:
            return None, None
        return days[0], days[-1]

    def apply_chart_day_from_entry(self, show_error: bool = True) -> None:
        """Pozwala recznie wpisac dzien wykresu w formacie RRRR-MM-DD."""
        raw = self.chart_day_filter.get().strip()
        try:
            selected_day = dt.date.fromisoformat(raw)
        except ValueError:
            if show_error:
                messagebox.showwarning("Nieprawidlowa data", "Wpisz date wykresu w formacie RRRR-MM-DD, np. 2026-05-02.")
            return
        min_day, max_day = self._chart_day_bounds()
        if min_day and max_day:
            if selected_day < min_day:
                selected_day = min_day
            elif selected_day > max_day:
                selected_day = max_day
            self.chart_day_filter.set(selected_day.isoformat())
        self.refresh_chart()

    def shift_chart_day(self, days: int) -> None:
        """Przesuwa wybrany dzien wykresu o podana liczbe dni i odswieza widok."""
        min_day, max_day = self._chart_day_bounds()
        if not min_day or not max_day:
            return
        try:
            current = dt.date.fromisoformat(self.chart_day_filter.get().strip())
        except ValueError:
            current = max_day
        new_day = current + dt.timedelta(days=days)
        if new_day < min_day:
            new_day = min_day
        elif new_day > max_day:
            new_day = max_day
        self.chart_day_filter.set(new_day.isoformat())
        self.refresh_chart()

    def set_chart_zoom(self, value: float) -> None:
        """Ustawia powiekszenie wykresu i odswieza rysunek."""
        self.chart_zoom = max(0.60, min(3.00, value))
        self.chart_zoom_text.set(f"{int(round(self.chart_zoom * 100))}%")
        self.refresh_chart()

    def zoom_chart_in(self) -> None:
        self.set_chart_zoom(self.chart_zoom + 0.25)

    def zoom_chart_out(self) -> None:
        self.set_chart_zoom(self.chart_zoom - 0.25)

    def reset_chart_zoom(self) -> None:
        self.set_chart_zoom(1.0)

    def refresh_chart(self) -> None:
        """Rysuje przewijany wykres aktywności z ostatnich 6 dni, dzień pod dniem."""
        for widget in self.chart_container.winfo_children():
            widget.destroy()
        if not self.activities:
            ttk.Label(self.chart_container, text="Brak aktywności do wykresu.").pack(anchor="w", padx=10, pady=10)
            return

        activities = sorted(self.filtered_activities(), key=lambda a: (a.start, a.end))
        if not activities:
            ttk.Label(self.chart_container, text="Brak aktywności do wykresu w wybranym zakresie dat.").pack(anchor="w", padx=10, pady=10)
            return

        day_values = sorted({d for a in activities for d in _activity_days(a)})
        if not day_values:
            ttk.Label(self.chart_container, text="Brak dni do pokazania na wykresie.").pack(anchor="w", padx=10, pady=10)
            return

        self.chart_day_labels = [day.isoformat() for day in day_values]
        current = self.chart_day_filter.get().strip()
        try:
            selected_day = dt.date.fromisoformat(current)
        except ValueError:
            selected_day = day_values[-1]
        if selected_day < day_values[0]:
            selected_day = day_values[0]
        elif selected_day > day_values[-1]:
            selected_day = day_values[-1]
        self.chart_day_filter.set(selected_day.isoformat())
        visible_days = [selected_day - dt.timedelta(days=offset) for offset in range(5, -1, -1)]

        header = ttk.Frame(self.chart_container)
        header.pack(fill=tk.X, pady=(0, 8))
        ttk.Label(
            header,
            text="Wykres aktywności - ostatnie 6 dni",
            font=("Arial", 13, "bold"),
        ).pack(side=tk.LEFT)
        ttk.Label(header, text="Do dnia:").pack(side=tk.LEFT, padx=(18, 4))
        ttk.Button(header, text="←", width=3, command=lambda: self.shift_chart_day(-1)).pack(side=tk.LEFT, padx=(0, 3))
        chart_day_entry = ttk.Entry(header, textvariable=self.chart_day_filter, width=12, justify="center")
        chart_day_entry.pack(side=tk.LEFT)
        chart_day_entry.bind("<Return>", lambda _event: self.apply_chart_day_from_entry())
        chart_day_entry.bind("<FocusOut>", lambda _event: self.apply_chart_day_from_entry(show_error=False))
        ttk.Button(header, text="→", width=3, command=lambda: self.shift_chart_day(1)).pack(side=tk.LEFT, padx=(3, 6))
        ttk.Button(header, text="Kalendarz", command=self.open_chart_calendar).pack(side=tk.LEFT, padx=(0, 10))
        ttk.Label(header, text="Powiększenie:").pack(side=tk.LEFT, padx=(6, 4))
        ttk.Button(header, text="−", width=3, command=self.zoom_chart_out).pack(side=tk.LEFT)
        ttk.Button(header, text="100%", width=5, command=self.reset_chart_zoom).pack(side=tk.LEFT, padx=3)
        ttk.Button(header, text="+", width=3, command=self.zoom_chart_in).pack(side=tk.LEFT)
        ttk.Label(header, textvariable=self.chart_zoom_text, width=5).pack(side=tk.LEFT, padx=(4, 12))
        ttk.Label(
            header,
            text="Opis na pasku: czas w dobie / łączny czas ciągu aktywności. Po najechaniu na REST zobaczysz też klasyfikację odpoczynku. Powyżej 24 h: dni, godziny, minuty.",
        ).pack(side=tk.LEFT, padx=8)

        outer = ttk.Frame(self.chart_container)
        outer.pack(fill=tk.BOTH, expand=True)
        outer.rowconfigure(0, weight=1)
        outer.columnconfigure(0, weight=1)

        canvas = tk.Canvas(outer, background="#ffffff", highlightthickness=1, highlightbackground="#d8dee9")
        yscroll = ttk.Scrollbar(outer, orient="vertical", command=canvas.yview)
        xscroll = ttk.Scrollbar(outer, orient="horizontal", command=canvas.xview)
        canvas.configure(yscrollcommand=yscroll.set, xscrollcommand=xscroll.set)
        canvas.grid(row=0, column=0, sticky="nsew")
        yscroll.grid(row=0, column=1, sticky="ns")
        xscroll.grid(row=1, column=0, sticky="ew")

        zoom = max(0.60, min(3.00, float(self.chart_zoom)))
        left = int(168 * max(0.90, min(1.25, zoom)))
        right = 80
        top = int(78 * max(0.85, min(1.25, zoom)))
        day_h = int(92 * max(0.75, min(1.80, zoom)))
        band_h = int(26 * max(0.80, min(1.65, zoom)))
        bottom = int(90 * max(0.85, min(1.25, zoom)))
        chart_width = int(1440 * zoom)
        total_seconds = 24 * 3600.0
        width = left + chart_width + right
        height = top + len(visible_days) * day_h + bottom

        colors = {
            "DRIVING": "#d94343",
            "WORK": "#e39a24",
            "AVAILABILITY": "#2f80ed",
            "REST": "#2e9d57",
            "UNKNOWN": "#8c96a3",
        }
        light_rows = ["#fbfcfe", "#f4f7fb"]
        rows = ["DRIVING", "WORK", "AVAILABILITY", "REST", "UNKNOWN"]

        def build_activity_run_map(items: list[Activity]) -> dict[int, tuple[dt.datetime, dt.datetime]]:
            """Merge adjacent entries of the same kind into one continuous run for chart labels."""
            run_map: dict[int, tuple[dt.datetime, dt.datetime]] = {}
            if not items:
                return run_map
            indexed = [(idx, a) for idx, a in enumerate(items) if a.end > a.start]
            indexed.sort(key=lambda pair: (pair[1].start, pair[1].end))
            if not indexed:
                return run_map

            default_tolerance = dt.timedelta(minutes=1)
            run_indices: list[int] = []
            run_kind: Optional[tuple[str, str]] = None
            run_start: Optional[dt.datetime] = None
            run_end: Optional[dt.datetime] = None

            def flush() -> None:
                if run_start is None or run_end is None:
                    return
                for item_idx in run_indices:
                    run_map[item_idx] = (run_start, run_end)

            for item_idx, activity in indexed:
                kind = activity.kind if activity.kind in colors else "UNKNOWN"
                run_key = (kind, vehicle_registration_display(activity))
                tolerance = REST_MERGE_TOLERANCE if kind == "REST" else default_tolerance
                if (
                    run_indices
                    and run_key == run_kind
                    and run_end is not None
                    and activity.start <= run_end + tolerance
                ):
                    run_indices.append(item_idx)
                    if activity.end > run_end:
                        run_end = activity.end
                    if run_start is not None and activity.start < run_start:
                        run_start = activity.start
                    continue

                flush()
                run_indices = [item_idx]
                run_kind = run_key
                run_start = activity.start
                run_end = activity.end

            flush()
            return run_map

        run_map = build_activity_run_map(activities)

        def rest_hover_label(item: RestOverview) -> str:
            rest_type = (item.rest_type or "").strip()
            if rest_type == "Tygodniowy pelny":
                return "odpoczynek tygodniowy"
            if rest_type == "Tygodniowy skrocony":
                return "odpoczynek tygodniowy skrócony"
            if rest_type in {"Dobowy pelny", "Dobowy skrocony"}:
                return "odpoczynek dzienny"
            return "przerwa / odpoczynek poniżej progu"

        rest_overview_map = {
            (item.start, item.end): item
            for item in self.rest_overview
        }

        def find_rest_overview_for_run(run_start: dt.datetime, run_end: dt.datetime) -> Optional[RestOverview]:
            exact = rest_overview_map.get((run_start, run_end))
            if exact:
                return exact
            for item in self.rest_overview:
                if item.start <= run_start and item.end >= run_end:
                    return item
            return None

        def x_pos(day_start: dt.datetime, moment: dt.datetime) -> float:
            return left + ((moment - day_start).total_seconds() / total_seconds) * chart_width

        tooltip = tk.Toplevel(self)
        tooltip.withdraw()
        tooltip.overrideredirect(True)
        tooltip_label = ttk.Label(
            tooltip,
            text="",
            justify="left",
            background="#162033",
            foreground="#ffffff",
            padding=(9, 6),
            font=("Arial", 9),
        )
        tooltip_label.pack()

        def show_tooltip(event: tk.Event, text: str) -> None:
            tooltip_label.configure(text=text)
            tooltip.geometry(f"+{event.x_root + 14}+{event.y_root + 14}")
            tooltip.deiconify()
            tooltip.lift()

        def hide_tooltip(_event: tk.Event) -> None:
            tooltip.withdraw()

        def on_canvas_destroy(_event: tk.Event) -> None:
            try:
                tooltip.destroy()
            except tk.TclError:
                pass

        canvas.bind("<Destroy>", on_canvas_destroy, add="+")

        def on_ctrl_mousewheel(event: tk.Event) -> str:
            delta = getattr(event, "delta", 0)
            if delta > 0:
                self.zoom_chart_in()
            elif delta < 0:
                self.zoom_chart_out()
            return "break"

        canvas.bind("<Control-MouseWheel>", on_ctrl_mousewheel)

        # Skala godzin wspólna dla wszystkich dni.
        canvas.create_text(left, 26, anchor="w", text=f"Ostatnie {len(visible_days)} dni do: {selected_day:%Y-%m-%d} | skala 00:00-24:00 | powiększenie {int(round(zoom * 100))}%", font=("Arial", 10, "bold"), fill="#243447")
        base_day = dt.datetime.combine(visible_days[0], dt.time.min)
        for hour in range(0, 25):
            x = left + (hour / 24.0) * chart_width
            line_color = "#cfd7e3" if hour % 3 == 0 else "#e8edf4"
            canvas.create_line(x, top - 28, x, height - bottom + 22, fill=line_color)
            if hour < 24:
                canvas.create_text(x + 2, top - 38, anchor="sw", text=f"{hour:02d}:00", font=("Arial", 8), fill="#46566c")
        canvas.create_line(left, top - 10, left + chart_width, top - 10, fill="#b8c3d4")

        visible_count = 0
        for d_idx, day in enumerate(visible_days):
            day_start = dt.datetime.combine(day, dt.time.min)
            day_end = day_start + dt.timedelta(days=1)
            y = top + d_idx * day_h
            canvas.create_rectangle(0, y - 10, width, y + day_h - 12, fill=light_rows[d_idx % 2], outline="")
            canvas.create_text(18, y + 8, anchor="w", text=f"{day:%Y-%m-%d}", font=("Arial", 10, "bold"), fill="#243447")
            canvas.create_text(18, y + 31, anchor="w", text="00:00-24:00", font=("Arial", 8), fill="#667085")
            canvas.create_line(left, y + band_h + 10, left + chart_width, y + band_h + 10, fill="#dfe6ef")

            for idx, a in enumerate(activities):
                clipped_start = max(a.start, day_start)
                clipped_end = min(a.end, day_end)
                if clipped_end <= clipped_start:
                    continue
                visible_count += 1
                kind = a.kind if a.kind in colors else "UNKNOWN"
                x1 = x_pos(day_start, clipped_start)
                x2 = max(x1 + 3, x_pos(day_start, clipped_end))
                duration = clipped_end - clipped_start
                run_start, run_end = run_map.get(idx, (a.start, a.end))
                run_total = run_end - run_start
                if run_total < duration:
                    run_total = duration
                run_crosses_day = run_start < day_start or run_end > day_end or run_total != duration
                label_duration = fmt_duration_report(duration)
                label_total = fmt_duration_report(run_total)
                label_core = f"{label_duration} / {label_total}" if run_crosses_day else label_duration
                tag = f"activity_bar_{d_idx}_{idx}"
                tooltip_text = (
                    f"Dzień: {day:%Y-%m-%d}\n"
                    f"Rodzaj: {activity_label_with_icon(kind)}\n"
                    f"Czas w tej dobie: {label_duration}\n"
                    f"Łączny czas ciągu aktywności: {label_total}\n"
                    f"Od: {clipped_start:%Y-%m-%d %H:%M}\n"
                    f"Do: {clipped_end:%Y-%m-%d %H:%M}"
                )
                if kind == "REST":
                    matched_rest = find_rest_overview_for_run(run_start, run_end)
                    if matched_rest:
                        tooltip_text += (
                            f"\nKlasyfikacja odpoczynku: {rest_hover_label(matched_rest)}"
                            f"\nTyp wg analizy: {matched_rest.rest_type}"
                        )
                    elif run_total >= REGULAR_WEEKLY_REST_MIN:
                        tooltip_text += "\nKlasyfikacja odpoczynku: odpoczynek tygodniowy"
                    elif run_total >= WEEKLY_REST_MIN:
                        tooltip_text += "\nKlasyfikacja odpoczynku: odpoczynek tygodniowy skrócony / dodatkowy długi odpoczynek"
                    elif run_total >= dt.timedelta(hours=9):
                        tooltip_text += "\nKlasyfikacja odpoczynku: odpoczynek dzienny"
                if run_crosses_day:
                    tooltip_text += f"\nCiąg aktywności: {run_start:%Y-%m-%d %H:%M} - {run_end:%Y-%m-%d %H:%M}"
                if run_start < day_start:
                    continuation_to_midnight = day_start - run_start
                    continuation_to_segment_end = clipped_end - run_start
                    tooltip_text += (
                        f"\nKontynuacja z poprzedniej doby: {fmt_duration_report(continuation_to_midnight)} do 00:00, "
                        f"{fmt_duration_report(continuation_to_segment_end)} do końca tego odcinka."
                    )
                if a.start < day_start or a.end > day_end or run_crosses_day:
                    tooltip_text += "\nUwaga: aktywność/ciąg aktywności przecina granicę doby."
                vehicle_reg = vehicle_registration_display(a)
                if kind == "DRIVING" and vehicle_reg != "-":
                    tooltip_text += f"\nNr rejestracyjny: {vehicle_reg}"

                canvas.create_rectangle(
                    x1,
                    y,
                    x2,
                    y + band_h,
                    fill=colors.get(kind, "#8c96a3"),
                    outline="#000000",
                    width=1,
                    tags=(tag,),
                )
                if x2 - x1 >= 128:
                    canvas.create_text((x1 + x2) / 2, y + band_h / 2, text=f"{activity_icon(kind)} {label_core}", fill="#ffffff", font=("Arial", 8, "bold"), tags=(tag,))
                elif x2 - x1 >= 72:
                    canvas.create_text((x1 + x2) / 2, y + band_h / 2, text=label_core, fill="#ffffff", font=("Arial", 8, "bold"), tags=(tag,))
                elif x2 - x1 >= 22:
                    canvas.create_text((x1 + x2) / 2, y + band_h / 2, text=activity_icon(kind), fill="#ffffff", font=("Arial", 9, "bold"), tags=(tag,))
                canvas.tag_bind(tag, "<Enter>", lambda event, text=tooltip_text: show_tooltip(event, text))
                canvas.tag_bind(tag, "<Motion>", lambda event, text=tooltip_text: show_tooltip(event, text))
                canvas.tag_bind(tag, "<Leave>", hide_tooltip)

        if visible_count == 0:
            canvas.create_text(left, top + 30, anchor="w", text="Brak aktywności w ostatnich 6 dniach wybranego zakresu.", font=("Arial", 11, "bold"), fill="#7a869a")

        legend_y = height - 54
        legend_x = left
        for kind in rows:
            canvas.create_rectangle(legend_x, legend_y, legend_x + 18, legend_y + 18, fill=colors[kind], outline="#000000")
            canvas.create_text(legend_x + 24, legend_y + 9, anchor="w", text=activity_label_with_icon(kind), font=("Arial", 9), fill="#243447")
            legend_x += 185

        canvas.create_text(left, height - 24, anchor="w", text="Wykres 6-dniowy: czas w dobie / łączny czas ciągu aktywności. Po najechaniu na REST zobaczysz klasyfikację: odpoczynek tygodniowy, tygodniowy skrócony albo dzienny.", font=("Arial", 9), fill="#46566c")
        canvas.configure(scrollregion=(0, 0, width, height))
        canvas.yview_moveto(0)

    def suggested_report_filename(self, prefix: str = "raport", extension: str = ".csv") -> str:
        """Zaproponuj nazwę raportu w formacie: nazwisko kierowcy - data."""
        date_from, date_to = self._selected_date_range()
        if self.card_read_date:
            day = self.card_read_date
        elif date_to:
            day = date_to
        elif date_from:
            day = date_from
        else:
            day = dt.date.today()
        ext = extension if extension.startswith(".") else f".{extension}"
        surname = sanitize_filename_part(getattr(self, "driver_surname", "kierowca"), "kierowca")
        return f"{surname} - {day:%Y-%m-%d}{ext}"

    def _ask_report_path(self, extension: str, filetypes: list[tuple[str, str]], prefix: str = "raport") -> str:
        return filedialog.asksaveasfilename(
            defaultextension=extension,
            initialfile=self.suggested_report_filename(prefix, extension),
            filetypes=filetypes,
        )

    def save_csv(self) -> None:
        activities = self.filtered_activities()
        violations = self.filtered_violations()
        if not activities and not violations:
            messagebox.showwarning("Brak danych", "Brak danych w wybranym zakresie dat albo analiza nie zostala jeszcze wykonana.")
            return
        path = self._ask_report_path(".csv", [("CSV", "*.csv")], "raport")
        if path:
            export_csv(Path(path), activities, violations, self.filtered_daily_summaries(), self.filtered_country_entries())
            self.log(f"Zapisano raport dla zakresu {self.date_filter_status.get()}: {path}")

    def save_violations_report_csv(self) -> None:
        violations = self.filtered_violations()
        if not violations:
            messagebox.showwarning("Brak naruszen", "Brak naruszen do zapisania w wybranym zakresie dat.")
            return
        path = self._ask_report_path(".csv", [("CSV", "*.csv")], "raport naruszen")
        if path:
            export_violations_report_csv(Path(path), violations)
            self.log(f"Zapisano raport naruszen raport dla zakresu {self.date_filter_status.get()}: {path}")

    def save_rests_report_csv(self) -> None:
        rests = self.filtered_rest_overview()
        if not rests:
            messagebox.showwarning("Brak danych", "Brak odpoczynkow do zapisania w wybranym zakresie dat.")
            return
        path = self._ask_report_path(".csv", [("CSV", "*.csv")], "raport odpoczynki")
        if path:
            export_rests_report_csv(Path(path), rests)
            self.log(f"Zapisano raport odpoczynkow raport dla zakresu {self.date_filter_status.get()}: {path}")

    def save_daily_report_csv(self) -> None:
        daily = self.filtered_daily_summaries()
        if not daily:
            messagebox.showwarning("Brak danych", "Brak danych dziennych do zapisania w wybranym zakresie dat.")
            return
        path = self._ask_report_path(".csv", [("CSV", "*.csv")], "raport dane dzienne")
        if path:
            export_daily_report_csv(Path(path), daily)
            self.log(f"Zapisano dane dzienne raport dla zakresu {self.date_filter_status.get()}: {path}")

    def save_country_codes_report_csv(self) -> None:
        entries = self.filtered_country_entries()
        if not entries:
            messagebox.showwarning("Brak danych", "Brak PL-ek/kodow kraju do zapisania w wybranym zakresie dat.")
            return
        path = self._ask_report_path(".csv", [("CSV", "*.csv")], "raport plki")
        if path:
            export_country_codes_report_csv(Path(path), entries)
            self.log(f"Zapisano raport PL-ek/kodow kraju raport dla zakresu {self.date_filter_status.get()}: {path}")

    def save_continuity_report_csv(self) -> None:
        issues = self.filtered_continuity_issues()
        compensations = self.filtered_compensations()
        if not issues and not compensations:
            messagebox.showwarning("Brak danych", "Brak danych ciaglosci/rekompensat w wybranym zakresie dat.")
            return
        path = self._ask_report_path(".csv", [("CSV", "*.csv")], "raport ciaglosc")
        if path:
            export_continuity_report_csv(Path(path), issues, compensations)
            self.log(f"Zapisano raport ciaglosci i rekompensat raport dla zakresu {self.date_filter_status.get()}: {path}")

    def _base_report_end_day(self, items_kind: str = "violations") -> tuple[Optional[dt.date], str]:
        """Return report end day, preferring card readout date and then available data."""
        if self.card_read_date:
            return self.card_read_date, self.card_read_date_source or "data odczytu karty"
        days = self.data_days()
        if days:
            return days[-1], "ostatni dzien danych"
        if items_kind == "activities" and self.activities:
            return max(a.end.date() for a in self.activities), "ostatni dzien aktywnosci"
        if items_kind == "violations" and self.violations:
            return max(v.end.date() for v in self.violations), "ostatni dzien naruszenia"
        return None, "brak danych"

    def _full_report_range(self, items_kind: str) -> tuple[Optional[dt.date], Optional[dt.date], str]:
        """Return full data range for the selected report kind."""
        if items_kind == "activities" and self.activities:
            return min(a.start.date() for a in self.activities), max(a.end.date() for a in self.activities), "cały okres aktywności"
        if items_kind == "violations" and self.violations:
            return min(v.start.date() for v in self.violations), max(v.end.date() for v in self.violations), "cały okres naruszeń"
        days = self.data_days()
        if days:
            return days[0], days[-1], "cały okres danych"
        return None, None, "brak danych"

    def _report_selected_range(self, range_value: Optional[str] = None, items_kind: str = "violations") -> tuple[Optional[dt.date], Optional[dt.date], str]:
        """Return selected report range for violations or activities."""
        selected_label = range_value or "Ostatnie 56 dni"
        normalized = selected_label.strip().lower()
        if normalized in ("cały okres", "caly okres"):
            return self._full_report_range(items_kind)

        end_day, source = self._base_report_end_day(items_kind)
        if not end_day:
            return None, None, "brak danych"
        start_day, end_day, period_label = report_range_from_label(selected_label, end_day)
        public_source = "data odczytu karty" if str(source).lower().startswith("pole json:") else source
        return start_day, end_day, f"{period_label}, {public_source}"

    def report_violations_selected_range(self) -> tuple[list[Violation], str]:
        """Return only violations from the selected reporting window."""
        selected_label = self.report_range.get() if hasattr(self, "report_range") else "Ostatnie 56 dni"
        date_from, date_to, source = self._report_selected_range(selected_label, "violations")
        if not date_from or not date_to:
            return [], "brak danych"
        start = dt.datetime.combine(date_from, dt.time.min)
        end_excl = dt.datetime.combine(date_to + dt.timedelta(days=1), dt.time.min)
        violations = [v for v in self.violations if _period_overlaps_range(v.start, v.end, start, end_excl)]
        violations.sort(key=lambda v: (v.start, v.end, v.rule))
        label = f"{date_from.isoformat()} - {date_to.isoformat()} ({source})"
        return violations, label

    def report_activities_selected_range(self) -> tuple[list[Activity], str]:
        """Return only activities from the selected activity reporting window."""
        selected_label = self.activity_report_range.get() if hasattr(self, "activity_report_range") else "Ostatnie 56 dni"
        date_from, date_to, source = self._report_selected_range(selected_label, "activities")
        if not date_from or not date_to:
            return [], "brak danych"
        start = dt.datetime.combine(date_from, dt.time.min)
        end_excl = dt.datetime.combine(date_to + dt.timedelta(days=1), dt.time.min)
        activities = [a for a in self.activities if _period_overlaps_range(a.start, a.end, start, end_excl)]
        activities.sort(key=lambda a: (a.start, a.end, a.kind))
        label = f"{date_from.isoformat()} - {date_to.isoformat()} ({source})"
        return activities, label

    def generate_report(self) -> None:
        fmt = self.report_format.get().strip().upper()
        if fmt == "PDF":
            self.save_pdf()
        else:
            self.save_html()

    def generate_activity_report(self) -> None:
        fmt = self.report_format.get().strip().upper()
        if fmt == "PDF":
            self.save_activities_pdf()
        else:
            self.save_activities_html()

    def generate_activity_chart_report(self) -> None:
        """Generuje raport PDF aktywności z wykresem osi czasu."""
        self.save_activities_chart_pdf()

    def _build_report_preview_text(
        self,
        title: str,
        fmt: str,
        range_label: str,
        headers: list[str],
        rows: list[list[str]],
        max_rows: int = 250,
    ) -> str:
        """Buduje czytelny podglad tekstowy raportu w oknie aplikacji."""
        lines: list[str] = [
            title,
            f"Format: {fmt}",
            f"Zakres raportu: {range_label}",
            f"Liczba wierszy: {len(rows)}",
            "",
        ]
        if not rows:
            lines.append("Brak wierszy w raporcie.")
            return "\n".join(lines)

        visible_headers = [str(h) for h in headers]
        lines.append(" | ".join(visible_headers))
        lines.append("-" * min(180, max(60, len(lines[-1]))))
        for row in rows[:max_rows]:
            cells = [str(cell).replace("\n", " ") for cell in row]
            lines.append(" | ".join(cells))
        if len(rows) > max_rows:
            lines.append("")
            lines.append(f"Pokazano pierwsze {max_rows} wierszy z {len(rows)}. Pełny raport zostanie zapisany do pliku.")
        return "\n".join(lines)

    def _open_temp_html_preview(self, exporter: Any, report_name: str) -> None:
        """Otwiera tymczasowy podglad HTML w domyslnej przegladarce."""
        try:
            safe_name = sanitize_filename_part(report_name, "raport")
            with tempfile.NamedTemporaryFile("w", suffix=f"_{safe_name}.html", delete=False, encoding="utf-8") as tmp:
                temp_path = Path(tmp.name)
            exporter(temp_path)
            webbrowser.open(temp_path.resolve().as_uri())
        except Exception as exc:
            messagebox.showerror("Nie mozna otworzyc podgladu", str(exc))

    def _show_report_preview(
        self,
        *,
        title: str,
        fmt: str,
        range_label: str,
        headers: list[str],
        rows: list[list[str]],
        extension: str,
        filetypes: list[tuple[str, str]],
        prefix: str,
        exporter: Any,
        log_message: str,
        html_preview_exporter: Optional[Any] = None,
    ) -> None:
        """Pokazuje okno raportu przed zapisem i dopiero z niego zapisuje plik."""
        preview = tk.Toplevel(self)
        preview.title(f"Podglad raportu - {title}")
        preview.transient(self)
        preview.grab_set()

        try:
            screen_w = preview.winfo_screenwidth()
            screen_h = preview.winfo_screenheight()
            width = min(1100, max(820, int(screen_w * 0.78)))
            height = min(760, max(560, int(screen_h * 0.78)))
            x = max(0, (screen_w - width) // 2)
            y = max(0, (screen_h - height) // 2)
            preview.geometry(f"{width}x{height}+{x}+{y}")
        except Exception:
            preview.geometry("1000x700")

        container = ttk.Frame(preview, padding=12)
        container.pack(fill=tk.BOTH, expand=True)
        ttk.Label(container, text=title, font=("Arial", 14, "bold")).pack(anchor="w")
        ttk.Label(
            container,
            text=f"Format: {fmt}    Zakres: {range_label}",
            foreground="#46566c",
            wraplength=1000,
            justify="left",
        ).pack(anchor="w", pady=(2, 10))

        text_frame = ttk.Frame(container)
        text_frame.pack(fill=tk.BOTH, expand=True)
        text_widget = tk.Text(text_frame, wrap="none", height=22)
        yscroll = ttk.Scrollbar(text_frame, orient="vertical", command=text_widget.yview)
        xscroll = ttk.Scrollbar(text_frame, orient="horizontal", command=text_widget.xview)
        text_widget.configure(yscrollcommand=yscroll.set, xscrollcommand=xscroll.set)
        text_widget.grid(row=0, column=0, sticky="nsew")
        yscroll.grid(row=0, column=1, sticky="ns")
        xscroll.grid(row=1, column=0, sticky="ew")
        text_frame.rowconfigure(0, weight=1)
        text_frame.columnconfigure(0, weight=1)

        text_widget.insert("1.0", self._build_report_preview_text(title, fmt, range_label, headers, rows))
        text_widget.configure(state="disabled")

        button_frame = ttk.Frame(container)
        button_frame.pack(fill=tk.X, pady=(10, 0))

        def save_report() -> None:
            path = self._ask_report_path(extension, filetypes, prefix)
            if not path:
                return
            try:
                exporter(Path(path))
                self.log(f"{log_message}: {path}")
                messagebox.showinfo("Zapisano raport", f"Raport zapisano:\n{path}")
                preview.destroy()
            except Exception as exc:
                messagebox.showerror("Blad zapisu raportu", str(exc))

        ttk.Button(button_frame, text="Zapisz raport", command=save_report).pack(side=tk.LEFT)
        if html_preview_exporter is not None:
            ttk.Button(
                button_frame,
                text="Otwórz podgląd HTML",
                command=lambda: self._open_temp_html_preview(html_preview_exporter, title),
            ).pack(side=tk.LEFT, padx=(8, 0))
        ttk.Button(button_frame, text="Zamknij", command=preview.destroy).pack(side=tk.RIGHT)

    def save_html(self) -> None:
        violations, range_label = self.report_violations_selected_range()
        if not self.violations:
            messagebox.showwarning("Brak danych", "Brak naruszen albo analiza nie zostala jeszcze wykonana.")
            return
        if not violations:
            messagebox.showwarning("Brak naruszen", "Brak naruszen w wybranym zakresie raportu.")
            return
        rows = [violation_report_row(v) for v in violations]
        self._show_report_preview(
            title="Raport naruszeń",
            fmt="HTML",
            range_label=range_label,
            headers=VIOLATION_REPORT_HEADERS,
            rows=rows,
            extension=".html",
            filetypes=[("HTML", "*.html")],
            prefix="raport naruszen",
            exporter=lambda path: export_violations_report_html(path, violations, range_label),
            html_preview_exporter=lambda path: export_violations_report_html(path, violations, range_label),
            log_message=f"Zapisano raport naruszen HTML dla wybranego zakresu: {range_label}",
        )

    def save_pdf(self) -> None:
        violations, range_label = self.report_violations_selected_range()
        if not self.violations:
            messagebox.showwarning("Brak danych", "Brak naruszen albo analiza nie zostala jeszcze wykonana.")
            return
        if not violations:
            messagebox.showwarning("Brak naruszen", "Brak naruszen w wybranym zakresie raportu.")
            return
        rows = [violation_report_row(v) for v in violations]
        self._show_report_preview(
            title="Raport naruszeń",
            fmt="PDF",
            range_label=range_label,
            headers=VIOLATION_REPORT_HEADERS,
            rows=rows,
            extension=".pdf",
            filetypes=[("PDF", "*.pdf")],
            prefix="raport naruszen",
            exporter=lambda path: export_violations_report_pdf(path, violations, range_label),
            html_preview_exporter=lambda path: export_violations_report_html(path, violations, range_label),
            log_message=f"Zapisano raport naruszen PDF dla wybranego zakresu: {range_label}",
        )

    def save_activities_html(self) -> None:
        activities, range_label = self.report_activities_selected_range()
        if not self.activities:
            messagebox.showwarning("Brak danych", "Brak aktywności albo analiza nie zostala jeszcze wykonana.")
            return
        if not activities:
            messagebox.showwarning("Brak aktywności", "Brak aktywności w wybranym zakresie raportu.")
            return
        rows = [activity_report_row(a) for a in activities]
        self._show_report_preview(
            title="Raport aktywności",
            fmt="HTML",
            range_label=range_label,
            headers=ACTIVITY_REPORT_HEADERS,
            rows=rows,
            extension=".html",
            filetypes=[("HTML", "*.html")],
            prefix="raport aktywnosci",
            exporter=lambda path: export_activities_report_html(path, activities, range_label),
            html_preview_exporter=lambda path: export_activities_report_html(path, activities, range_label),
            log_message=f"Zapisano raport aktywności HTML dla wybranego zakresu: {range_label}",
        )

    def save_activities_pdf(self) -> None:
        activities, range_label = self.report_activities_selected_range()
        if not self.activities:
            messagebox.showwarning("Brak danych", "Brak aktywności albo analiza nie zostala jeszcze wykonana.")
            return
        if not activities:
            messagebox.showwarning("Brak aktywności", "Brak aktywności w wybranym zakresie raportu.")
            return
        rows = [activity_report_row(a) for a in activities]
        self._show_report_preview(
            title="Raport aktywności",
            fmt="PDF",
            range_label=range_label,
            headers=ACTIVITY_REPORT_HEADERS,
            rows=rows,
            extension=".pdf",
            filetypes=[("PDF", "*.pdf")],
            prefix="raport aktywnosci",
            exporter=lambda path: export_activities_report_pdf(path, activities, range_label),
            html_preview_exporter=lambda path: export_activities_report_html(path, activities, range_label),
            log_message=f"Zapisano raport aktywności PDF dla wybranego zakresu: {range_label}",
        )

    def save_activities_chart_pdf(self) -> None:
        activities, range_label = self.report_activities_selected_range()
        if not self.activities:
            messagebox.showwarning("Brak danych", "Brak aktywności albo analiza nie zostala jeszcze wykonana.")
            return
        if not activities:
            messagebox.showwarning("Brak aktywności", "Brak aktywności w wybranym zakresie raportu.")
            return
        daily_rows = [daily_summary_row(item) for item in analyze_daily_summaries(activities)]
        self._show_report_preview(
            title="Raport aktywności z wykresem",
            fmt="PDF",
            range_label=range_label,
            headers=["Data", "Jazda", "Przerwa/odpoczynek", "Dyspozycyjność", "Inna praca", "Nieznane", "Suma zapisu", "Pierwsza aktywność", "Ostatnia aktywność", "Liczba wpisów/części"],
            rows=daily_rows,
            extension=".pdf",
            filetypes=[("PDF", "*.pdf")],
            prefix="raport aktywnosci z wykresem",
            exporter=lambda path: export_activities_chart_report_pdf(path, activities, range_label),
            html_preview_exporter=None,
            log_message=f"Zapisano raport aktywności z wykresem PDF dla wybranego zakresu: {range_label}",
        )



def main() -> None:
    app = DriverTimeApp()
    app.mainloop()


if __name__ == "__main__":
    main()
