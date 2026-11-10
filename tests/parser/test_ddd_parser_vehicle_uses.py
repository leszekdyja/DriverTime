import calendar
import datetime as dt
import sys
import unittest
import tempfile
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "src" / "parser"))

import ddd_parser


def _u24(value: int) -> bytes:
    return value.to_bytes(3, "big")


def _time_real(year: int, month: int, day: int, hour: int, minute: int) -> bytes:
    return calendar.timegm(dt.datetime(year, month, day, hour, minute).timetuple()).to_bytes(4, "big")


class CardVehicleRecordTests(unittest.TestCase):
    def test_generation_two_reads_vin_from_same_vehicle_record_and_skips_signatures(self):
        def record(plate, vin):
            return (_u24(1000) + _u24(1200) + _time_real(2026, 9, 1, 8, 0)
                    + _time_real(2026, 9, 1, 16, 0) + b"\x28\x01"
                    + plate.encode('ascii').ljust(13, b' ') + b"\x00\x01" + vin.encode('ascii'))
        first = record('DTR12345', 'WVWZZZ1JZXW000001')
        second = record('DTR54321', 'WVWZZZ1JZXW000002')
        payload = b'\x00\x00' + first + second
        def block(kind, content):
            return b'\x05\x05' + bytes([kind]) + len(content).to_bytes(2, 'big') + content
        raw = block(0, b'\x00\x00' + first[:31]) + block(2, payload) + block(3, payload)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'card.DDD'
            path.write_bytes(raw)
            uses = ddd_parser.parse_ddd_vehicle_uses_embedded(path)
        self.assertEqual(2, len(uses))
        self.assertEqual({'DTR12345': 'WVWZZZ1JZXW000001', 'DTR54321': 'WVWZZZ1JZXW000002'},
                         {item.registration: item.vin for item in uses})
        conflict_payload = b'\x00\x00' + first + record('DTR12345', 'WVWZZZ1JZXW000003')
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'conflict.DDD'
            path.write_bytes(block(2, conflict_payload))
            ambiguous = ddd_parser.parse_ddd_vehicle_uses_embedded(path)
        self.assertEqual(1, len(ambiguous))
        self.assertEqual('', ambiguous[0].vin)

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
