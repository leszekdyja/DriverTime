import SwiftUI

struct EntryFormView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    @State private var date = Date()
    @State private var startTime = Calendar.current.date(bySettingHour: 8, minute: 0, second: 0, of: Date()) ?? Date()
    @State private var endTime = Calendar.current.date(bySettingHour: 16, minute: 0, second: 0, of: Date()) ?? Date()
    @State private var endsNextDay = false
    @State private var activityType: WorkEvidenceActivityType = .driving
    @State private var vehicleRegistration = ""
    @State private var countryCode = "PL"
    @State private var distanceKm = ""
    @State private var description = ""
    @State private var isSaving = false

    var body: some View {
        NavigationStack {
            Form {
                Section("Czas") {
                    DatePicker("Data", selection: $date, displayedComponents: .date)
                    DatePicker("Od", selection: $startTime, displayedComponents: .hourAndMinute)
                    DatePicker("Do", selection: $endTime, displayedComponents: .hourAndMinute)
                    Toggle("Kończy się następnego dnia", isOn: $endsNextDay)
                }

                Section("Aktywność") {
                    Picker("Typ", selection: $activityType) {
                        ForEach(WorkEvidenceActivityType.allCases) { activity in
                            Text(activity.label).tag(activity)
                        }
                    }
                }

                Section("Dodatkowe dane") {
                    TextField("Pojazd", text: $vehicleRegistration)
                        .textInputAutocapitalization(.characters)
                    TextField("Kraj", text: $countryCode)
                        .textInputAutocapitalization(.characters)
                    TextField("Kilometry", text: $distanceKm)
                        .keyboardType(.decimalPad)
                    TextField("Opis", text: $description, axis: .vertical)
                        .lineLimit(3...6)
                }
            }
            .navigationTitle("Nowy wpis")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Anuluj") {
                        dismiss()
                    }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button {
                        Task {
                            isSaving = true
                            await appState.saveEntry(buildRequest())
                            isSaving = false
                            dismiss()
                        }
                    } label: {
                        if isSaving {
                            ProgressView()
                        } else {
                            Text("Zapisz")
                        }
                    }
                    .disabled(isSaving)
                }
            }
        }
    }

    private func buildRequest() -> WorkEvidenceEntryRequest {
        WorkEvidenceEntryRequest(
            date: DriverTimeFormatters.apiDate(date),
            startTime: DriverTimeFormatters.apiTime(startTime),
            endTime: DriverTimeFormatters.apiTime(endTime),
            endsNextDay: endsNextDay,
            activityType: activityType,
            vehicleRegistration: vehicleRegistration.trimmingCharacters(in: .whitespacesAndNewlines).nilIfEmpty,
            countryCode: countryCode.trimmingCharacters(in: .whitespacesAndNewlines).nilIfEmpty,
            distanceKm: Double(distanceKm.replacingOccurrences(of: ",", with: ".")),
            description: description.trimmingCharacters(in: .whitespacesAndNewlines).nilIfEmpty
        )
    }
}

private extension String {
    var nilIfEmpty: String? {
        isEmpty ? nil : self
    }
}
