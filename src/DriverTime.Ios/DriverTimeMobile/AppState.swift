import Foundation

@MainActor
final class AppState: ObservableObject {
    @Published var isRestoring = true
    @Published var currentUser: CurrentUser?
    @Published var assignedDriver: Driver?
    @Published var drivers: [Driver] = []
    @Published var month: WorkEvidenceMonth?
    @Published var pendingEntries: [PendingWorkEvidenceEntry] = []
    @Published var errorMessage: String?
    @Published var isSyncing = false

    let api = DriverTimeApiClient()
    private let pendingStore = PendingEntryStore()

    private let keychainService = "pl.drivertime.mobile"
    private let tokenAccount = "authToken"
    private let baseURLKey = "pl.drivertime.mobile.baseURL"
    private let assignedDriverKey = "pl.drivertime.mobile.assignedDriver"

    init() {
        api.baseURL = UserDefaults.standard.string(forKey: baseURLKey) ?? ""
        api.token = KeychainStore.read(service: keychainService, account: tokenAccount)
        pendingEntries = pendingStore.load()
    }

    func restore() async {
        defer { isRestoring = false }

        guard api.token != nil, !api.baseURL.isEmpty else {
            return
        }

        do {
            currentUser = try await api.currentUser()
            try loadStoredDriverBinding()
            if assignedDriver != nil {
                await refresh()
            } else {
                try await loadDriversForBinding()
            }
        } catch {
            logout()
            errorMessage = error.localizedDescription
        }
    }

    func login(baseURL: String, email: String, password: String) async {
        errorMessage = nil
        api.baseURL = baseURL.normalizedBaseURL

        do {
            let response = try await api.login(email: email, password: password)
            api.token = response.token
            currentUser = response.user
            KeychainStore.save(response.token, service: keychainService, account: tokenAccount)
            UserDefaults.standard.set(api.baseURL, forKey: baseURLKey)
            try await loadDriversForBinding()
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func logout() {
        api.token = nil
        currentUser = nil
        assignedDriver = nil
        drivers = []
        month = nil
        KeychainStore.delete(service: keychainService, account: tokenAccount)
    }

    func clearDeviceAssignment() {
        assignedDriver = nil
        month = nil
        UserDefaults.standard.removeObject(forKey: assignedDriverKey)
    }

    func assignDevice(to driver: Driver) async {
        assignedDriver = driver

        if let data = try? JSONEncoder.driverTime.encode(driver) {
            UserDefaults.standard.set(data, forKey: assignedDriverKey)
        }

        await refresh()
    }

    func reloadDriversForBinding() async {
        do {
            try await loadDriversForBinding()
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func refresh() async {
        guard let driver = assignedDriver else {
            return
        }

        isSyncing = true
        errorMessage = nil
        defer { isSyncing = false }

        do {
            try await synchronizePendingEntries()
            let components = Calendar.current.dateComponents([.year, .month], from: Date())
            month = try await api.workEvidence(
                driverId: driver.id,
                year: components.year ?? 2026,
                month: components.month ?? 1
            )
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func saveEntry(_ request: WorkEvidenceEntryRequest) async {
        guard let driver = assignedDriver else {
            errorMessage = "Telefon nie jest przypisany do kierowcy."
            return
        }

        do {
            _ = try await api.createEntry(driverId: driver.id, request: request)
            await refresh()
        } catch {
            let pending = PendingWorkEvidenceEntry(
                id: UUID(),
                driverId: driver.id,
                request: request,
                createdAt: Date(),
                lastError: error.localizedDescription
            )
            pendingStore.add(pending)
            pendingEntries = pendingStore.load()
            errorMessage = "Brak synchronizacji. Wpis zapisano lokalnie i zostanie wysłany później."
        }
    }

    func deleteEntry(_ entry: WorkEvidenceEntry) async {
        do {
            try await api.deleteEntry(entryId: entry.id)
            await refresh()
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    private func loadDriversForBinding() async throws {
        drivers = try await api.drivers()
    }

    private func loadStoredDriverBinding() throws {
        guard let data = UserDefaults.standard.data(forKey: assignedDriverKey) else {
            assignedDriver = nil
            return
        }

        assignedDriver = try JSONDecoder.driverTime.decode(Driver.self, from: data)
    }

    private func synchronizePendingEntries() async throws {
        guard let driver = assignedDriver else {
            return
        }

        var remaining: [PendingWorkEvidenceEntry] = []

        for entry in pendingStore.load() {
            guard entry.driverId == driver.id else {
                remaining.append(entry)
                continue
            }

            do {
                _ = try await api.createEntry(driverId: driver.id, request: entry.request)
            } catch {
                var failed = entry
                failed.lastError = error.localizedDescription
                remaining.append(failed)
            }
        }

        pendingStore.save(remaining)
        pendingEntries = remaining
    }
}
