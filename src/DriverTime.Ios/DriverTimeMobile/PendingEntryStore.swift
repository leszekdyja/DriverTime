import Foundation

final class PendingEntryStore {
    private let key = "pl.drivertime.mobile.pendingWorkEvidenceEntries"

    func load() -> [PendingWorkEvidenceEntry] {
        guard let data = UserDefaults.standard.data(forKey: key) else {
            return []
        }

        return (try? JSONDecoder.driverTime.decode([PendingWorkEvidenceEntry].self, from: data)) ?? []
    }

    func save(_ entries: [PendingWorkEvidenceEntry]) {
        let data = try? JSONEncoder.driverTime.encode(entries)
        UserDefaults.standard.set(data, forKey: key)
    }

    func add(_ entry: PendingWorkEvidenceEntry) {
        var entries = load()
        entries.append(entry)
        save(entries)
    }

    func remove(id: UUID) {
        save(load().filter { $0.id != id })
    }
}
