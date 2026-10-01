import SwiftUI

struct ActivityDashboardView: View {
    @EnvironmentObject private var appState: AppState
    @State private var showingManualForm = false
    @State private var timerActivity: WorkEvidenceActivityType = .driving
    @State private var timerStart: Date?
    @State private var isShowingSettings = false

    private var todayEntries: [WorkEvidenceEntry] {
        let today = DriverTimeFormatters.apiDate(Date())
        return appState.month?.days.first(where: { $0.date == today })?.entries ?? []
    }

    var body: some View {
        NavigationStack {
            List {
                if let driver = appState.assignedDriver {
                    Section {
                        VStack(alignment: .leading, spacing: 4) {
                            Text(driver.displayName)
                                .font(.title3.bold())
                            Text("Synchronizacja tylko dla tego kierowcy")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                    }
                }

                Section("Szybka rejestracja") {
                    Picker("Aktywność", selection: $timerActivity) {
                        ForEach(WorkEvidenceActivityType.allCases) { activity in
                            Text(activity.label).tag(activity)
                        }
                    }

                    if let timerStart {
                        LabeledContent("Start", value: DriverTimeFormatters.apiTime(timerStart))
                        Button(role: .destructive) {
                            stopTimer()
                        } label: {
                            Label("Zakończ i wyślij", systemImage: "stop.fill")
                        }
                    } else {
                        Button {
                            timerStart = Date()
                        } label: {
                            Label("Start", systemImage: "play.fill")
                        }
                    }
                }

                if !appState.pendingEntries.isEmpty {
                    Section("Oczekuje na synchronizację") {
                        ForEach(appState.pendingEntries.filter { $0.driverId == appState.assignedDriver?.id }) { pending in
                            VStack(alignment: .leading, spacing: 4) {
                                Text("\(pending.request.activityType.label), \(pending.request.date)")
                                Text("\(pending.request.startTime)-\(pending.request.endTime)")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                                if let lastError = pending.lastError {
                                    Text(lastError)
                                        .font(.caption2)
                                        .foregroundStyle(.red)
                                }
                            }
                        }
                    }
                }

                Section("Dzisiejsze wpisy") {
                    if todayEntries.isEmpty {
                        Text("Brak wpisów na dziś.")
                            .foregroundStyle(.secondary)
                    } else {
                        ForEach(todayEntries) { entry in
                            VStack(alignment: .leading, spacing: 4) {
                                Text(entry.activityType.label)
                                    .font(.headline)
                                Text("\(entry.startTime.prefix(5))-\(entry.endTime.prefix(5)) · \(DriverTimeFormatters.minutesLabel(entry.durationMinutes))")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                                if let vehicle = entry.vehicleRegistration, !vehicle.isEmpty {
                                    Text(vehicle)
                                        .font(.caption)
                                }
                            }
                            .swipeActions {
                                Button(role: .destructive) {
                                    Task {
                                        await appState.deleteEntry(entry)
                                    }
                                } label: {
                                    Label("Usuń", systemImage: "trash")
                                }
                            }
                        }
                    }
                }

                if let summary = appState.month?.summary {
                    Section("Miesiąc") {
                        LabeledContent("Jazda", value: DriverTimeFormatters.minutesLabel(summary.drivingMinutes))
                        LabeledContent("Inna praca", value: DriverTimeFormatters.minutesLabel(summary.otherWorkMinutes))
                        LabeledContent("Dyspozycyjność", value: DriverTimeFormatters.minutesLabel(summary.availabilityMinutes))
                        LabeledContent("Razem", value: DriverTimeFormatters.minutesLabel(summary.totalTrackedMinutes))
                    }
                }

                if let error = appState.errorMessage {
                    Section {
                        Text(error)
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle("Aktywności")
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button {
                        isShowingSettings = true
                    } label: {
                        Image(systemName: "gearshape")
                    }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        showingManualForm = true
                    } label: {
                        Image(systemName: "plus")
                    }
                }
            }
            .refreshable {
                await appState.refresh()
            }
            .sheet(isPresented: $showingManualForm) {
                EntryFormView()
            }
            .sheet(isPresented: $isShowingSettings) {
                SettingsView()
            }
            .task {
                await appState.refresh()
            }
        }
    }

    private func stopTimer() {
        guard let timerStart else { return }
        let end = Date()
        let startDay = DriverTimeFormatters.apiDate(timerStart)
        let endDay = DriverTimeFormatters.apiDate(end)

        let request = WorkEvidenceEntryRequest(
            date: startDay,
            startTime: DriverTimeFormatters.apiTime(timerStart),
            endTime: DriverTimeFormatters.apiTime(end),
            endsNextDay: startDay != endDay,
            activityType: timerActivity,
            countryCode: "PL"
        )

        self.timerStart = nil
        Task {
            await appState.saveEntry(request)
        }
    }
}
