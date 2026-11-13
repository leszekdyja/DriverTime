import datetime as dt
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "src" / "parser"))
import ddd_parser


class MultiManningTests(unittest.TestCase):
    def parse(self, words):
        payload = b"".join(word.to_bytes(2, "big") for word in words)
        return ddd_parser._activities_from_daily_record(dt.datetime(2026, 10, 1), payload, "test")

    def test_inserted_crew_driver_and_codriver_are_distinguished(self):
        activities = self.parse([0x4000 | 3 << 11, 0xC000 | 1 << 11 | 270])
        self.assertEqual([(True, 1, "DRIVING"), (True, 2, "AVAILABILITY")],
                         [(a.is_crew, a.card_slot, a.kind) for a in activities])

    def test_manual_known_entry_is_not_crew(self):
        for word in [0x6000, 0xE000, 0x2000]:
            activity = self.parse([word])[0]
            self.assertIsNone(activity.is_crew)
            self.assertIsNone(activity.card_slot)

    def test_single_driver_is_explicitly_false(self):
        self.assertIs(self.parse([3 << 11])[0].is_crew, False)

    def test_crew_and_slot_changes_are_not_merged(self):
        activities = self.parse([1 << 11, 0xC000 | 1 << 11 | 60,
                                 0x4000 | 1 << 11 | 120])
        self.assertEqual(3, len(ddd_parser.dedupe_and_merge(activities)))

    def test_registration_assignment_preserves_crew(self):
        activity = self.parse([0xC000 | 1 << 11])[0]
        use = ddd_parser.VehicleUse(activity.start, activity.end, "DTR12345")
        result = ddd_parser.apply_vehicle_registrations([activity], [use])[0]
        self.assertEqual((True, 2), (result.is_crew, result.card_slot))

    def test_explicit_json_preserves_crew(self):
        activities = ddd_parser.normalize_activities({"activities": [{
            "start": "2026-10-01 00:00:00", "end": "2026-10-01 01:00:00",
            "activity": "DRIVING", "is_crew": True, "card_slot": 1}]})
        self.assertEqual((True, 1), (activities[0].is_crew, activities[0].card_slot))


if __name__ == "__main__":
    unittest.main()
