import datetime as dt
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "src" / "parser"))
import ddd_parser

class CardWithdrawalActivityTests(unittest.TestCase):
    def test_unknown_after_withdrawal_is_not_continued_work_or_rest(self):
        for activity in range(4):
            minute, kind = ddd_parser._activity_word_to_kind(0x2000 | activity << 11 | 62)
            self.assertEqual((62, "UNKNOWN"), (minute, kind))

    def test_confirmed_manual_entry_is_preserved(self):
        self.assertEqual((62, "WORK"), ddd_parser._activity_word_to_kind(0x6000 | 2 << 11 | 62))
        self.assertEqual((62, "REST"), ddd_parser._activity_word_to_kind(0x6000 | 62))

    def test_inserted_card_availability_remains_availability(self):
        self.assertEqual((62, "AVAILABILITY"), ddd_parser._activity_word_to_kind(1 << 11 | 62))

    def test_withdrawal_creates_unknown_interval_to_next_entry(self):
        words = [2 << 11, 0x2000 | 2 << 11 | 62, 0x6000 | 600]
        payload = b"".join(word.to_bytes(2, "big") for word in words)
        result = ddd_parser._activities_from_daily_record(dt.datetime(2026, 10, 31), payload, "test")
        self.assertEqual(["WORK", "UNKNOWN", "REST"], [item.kind for item in result])
        self.assertEqual(538, (result[1].end - result[1].start).total_seconds() / 60)

if __name__ == "__main__":
    unittest.main()
