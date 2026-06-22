import calendar
import datetime as dt
import sys
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "src" / "parser"))

import ddd_parser


def _u24(value: int) -> bytes:
    return value.to_bytes(3, "big")


def _time_real(year: int, month: int, day: int, hour: int, minute: int) -> bytes:
    return calendar.timegm(dt.datetime(year, month, day, hour, minute).timetuple()).to_bytes(4, "big")


class CardVehicleRecordTests(unittest.TestCase):
    def test_card_vehicle_record_returns_odometer_distance_and_registration(self) -> None:
        record = (
            _u24(123456)
            + _u24(123789)
            + _time_real(2025, 7, 7, 7, 30)
            + _time_real(2025, 7, 7, 12, 45)
            + b"\x28\x01DPL 07506    \x00\x01"
        )
        block = b"\x00\x01" + record

        vehicle_uses = ddd_parser._parse_vehicle_uses_from_block(block, 100)

        self.assertEqual(len(vehicle_uses), 1)
        vehicle_use = vehicle_uses[0]
        self.assertEqual(vehicle_use.registration, "DPL 07506")
        self.assertEqual(vehicle_use.start_odometer_km, 123456)
        self.assertEqual(vehicle_use.end_odometer_km, 123789)
        self.assertEqual(vehicle_use.distance_km, 333)

    @unittest.skipUnless(
        (
            REPO_ROOT
            / "src"
            / "DriverTime.Api"
            / "bin"
            / "Debug"
            / "net8.0"
            / "import-retry"
            / "dcfe63125ec54603b712c5c5f4ea77b2_Dyja.DDD"
        ).exists(),
        "Real DDD sample is not available in this workspace.",
    )
    def test_real_driver_card_vehicle_uses_include_odometer_values(self) -> None:
        ddd_path = (
            REPO_ROOT
            / "src"
            / "DriverTime.Api"
            / "bin"
            / "Debug"
            / "net8.0"
            / "import-retry"
            / "dcfe63125ec54603b712c5c5f4ea77b2_Dyja.DDD"
        )

        payload = ddd_parser.parse_ddd_driver_card_embedded(ddd_path)
        vehicle_uses = payload["vehicle_uses"]

        self.assertGreater(len(vehicle_uses), 0)
        self.assertTrue(any(item["start_odometer_km"] is not None for item in vehicle_uses))
        self.assertTrue(any(item["end_odometer_km"] is not None for item in vehicle_uses))
        self.assertTrue(any(item["distance_km"] is not None for item in vehicle_uses))


if __name__ == "__main__":
    unittest.main()
