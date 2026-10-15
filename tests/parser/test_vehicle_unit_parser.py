import os
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import patch


REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "src" / "parser"))

import parser_cli
import vehicle_unit_parser


class VehicleUnitDetectionTests(unittest.TestCase):
    def test_detects_vehicle_unit_by_binary_prefix(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "download.ddd"
            path.write_bytes(b"\x76\x01payload")

            self.assertTrue(parser_cli.is_vehicle_unit_file(path))

    def test_detects_vehicle_unit_by_standard_filename(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "M_20261015_vehicle.DDD"
            path.write_bytes(b"other")

            self.assertTrue(parser_cli.is_vehicle_unit_file(path))

    def test_driver_card_is_not_classified_as_vehicle_unit(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "C_driver.DDD"
            path.write_bytes(b"\x05\x20payload")

            self.assertFalse(parser_cli.is_vehicle_unit_file(path))


class VehicleUnitAdapterTests(unittest.TestCase):
    def test_normalizes_decoder_result_to_drivertime_contract(self) -> None:
        decoded = {
            "metadata": {
                "app_version": "test",
                "generation": "G2v2",
                "integrity_check": "valid",
                "coverage_pct": 100,
                "current_datetime": "2026-10-15T10:00:00Z",
            },
            "vehicle": {
                "vin": "VIN123",
                "plate": "DPL 12345",
                "registration_nation": "PL",
            },
            "activities": [{"date": "2026-10-14"}],
            "card_iw_records": [{"driver": "Jan Kowalski"}],
            "events": [{"type": "overspeed"}],
            "faults": [{"type": "sensor"}],
        }

        class FakeTachoParser:
            def __init__(self, path: str) -> None:
                self.path = path

            def parse(self):
                return decoded

        app_module = types.ModuleType("app")
        engine_module = types.ModuleType("app.engine")
        engine_module.TachoParser = FakeTachoParser

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "vehicle.DDD"
            path.write_bytes(b"\x76\x01")
            with (
                patch.dict(sys.modules, {"app": app_module, "app.engine": engine_module}),
                patch.dict(os.environ, {"DDDTACHO_PARSER_ROOT": directory}),
            ):
                result = vehicle_unit_parser.parse_vehicle_unit_ddd(path)

        self.assertEqual(result["parser"]["file_type"], "vehicle_unit")
        self.assertEqual(result["file_type"], "vehicle_unit")
        self.assertEqual(result["parser_name"], "DriverTime VU adapter / DDDTachograph Reader")
        self.assertEqual(result["vehicle_unit"]["registration_number"], "DPL 12345")
        self.assertEqual(result["vehicle_unit"]["vin"], "VIN123")
        self.assertEqual(len(result["vehicle_unit"]["daily_activities"]), 1)
        self.assertEqual(len(result["vehicle_unit"]["driver_sessions"]), 1)
        self.assertEqual(len(result["vehicle_unit"]["events"]), 1)
        self.assertEqual(len(result["vehicle_unit"]["faults"]), 1)


if __name__ == "__main__":
    unittest.main()
