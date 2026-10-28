import calendar
import datetime as dt
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'src' / 'parser'))
from ddd_parser import parse_card_download_time


def block(date, generation=0):
    seconds = calendar.timegm(date.timetuple())
    return bytes([5, 14, generation, 0, 4]) + seconds.to_bytes(4, 'big')


class CardDownloadTimeTests(unittest.TestCase):
    def test_reads_latest_generation_timestamp(self):
        first, second = dt.datetime(2025, 7, 16), dt.datetime(2026, 10, 27, 23, 45, 6)
        self.assertEqual(second, parse_card_download_time(block(first) + block(second, 2)))

    def test_missing_and_invalid_data_do_not_use_current_time(self):
        for raw in (b'', b'no card date', b'\x05\x0e\x00\x00\x04\x00\x00\x00\x00',
                    block(dt.datetime(2045, 1, 1)), block(dt.datetime(2026, 1, 1), 1)):
            self.assertIsNone(parse_card_download_time(raw))

    def test_identical_payload_keeps_old_readout_on_reimport(self):
        old = dt.datetime(2024, 7, 16)
        self.assertEqual(old, parse_card_download_time(block(old)))
        self.assertEqual(old, parse_card_download_time(bytes(block(old))))
