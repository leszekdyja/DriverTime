#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import json
import sys
import traceback
from pathlib import Path

from ddd_parser import (
    parse_ddd_driver_card_embedded,
    normalize_activities,
    analyze_data_continuity,
    analyze_weekly_rest_compensations,
    analyze_rest_overview,
    analyze_daily_summaries,
    extract_country_code_entries,
    analyze_country_code_entries,
    analyze,
)


def main() -> int:
    if len(sys.argv) < 2:
        print(json.dumps({
            "success": False,
            "error": "Missing .ddd file path argument."
        }, ensure_ascii=False))
        return 1

    ddd_path = Path(sys.argv[1])

    if not ddd_path.exists():
        print(json.dumps({
            "success": False,
            "error": f"File not found: {ddd_path}"
        }, ensure_ascii=False))
        return 1

    try:
        raw_json = parse_ddd_driver_card_embedded(ddd_path)

        activities = normalize_activities(raw_json)
        continuity_issues = analyze_data_continuity(activities)
        compensations = analyze_weekly_rest_compensations(activities)
        rest_overview = analyze_rest_overview(activities, compensations)
        daily_summaries = analyze_daily_summaries(activities)

        country_entries = analyze_country_code_entries(
            extract_country_code_entries(raw_json),
            daily_summaries
        )

        violations = analyze(activities, False)

        result = {
            "success": True,
            "sourceFile": str(ddd_path),
            "summary": {
                "activitiesCount": len(activities),
                "violationsCount": len(violations),
                "dailySummariesCount": len(daily_summaries),
                "countryEntriesCount": len(country_entries),
                "continuityIssuesCount": len(continuity_issues),
                "compensationsCount": len(compensations),
                "restsCount": len(rest_overview)
            },
            "raw": raw_json
        }

        print(json.dumps(result, ensure_ascii=False, default=str))
        return 0

    except Exception as exc:
        print(json.dumps({
            "success": False,
            "error": str(exc),
            "trace": traceback.format_exc()
        }, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())