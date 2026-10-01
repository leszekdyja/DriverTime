import SwiftUI

struct SettingsView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss
    @State private var confirmingReset = false

    var body: some View {
        NavigationStack {
            List {
                Section("Przypisany kierowca") {
                    if let driver = appState.assignedDriver {
                        LabeledContent("Kierowca", value: driver.displayName)
                        LabeledContent("Karta", value: driver.cardNumber)
                    }
                    Button(role: .destructive) {
                        confirmingReset = true
                    } label: {
                        Text("Zmień przypisanie telefonu")
                    }
                }

                Section("Synchronizacja") {
                    LabeledContent("API", value: appState.api.baseURL)
                    LabeledContent("Oczekujące wpisy", value: "\(appState.pendingEntries.count)")
                    Button("Synchronizuj teraz") {
                        Task {
                            await appState.refresh()
                        }
                    }
                }

                Section {
                    Button(role: .destructive) {
                        appState.logout()
                        dismiss()
                    } label: {
                        Text("Wyloguj")
                    }
                }
            }
            .navigationTitle("Ustawienia")
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Gotowe") {
                        dismiss()
                    }
                }
            }
            .confirmationDialog(
                "Zmienić przypisanie telefonu?",
                isPresented: $confirmingReset,
                titleVisibility: .visible
            ) {
                Button("Odblokuj wybór kierowcy", role: .destructive) {
                    appState.clearDeviceAssignment()
                    dismiss()
                }
                Button("Anuluj", role: .cancel) {}
            } message: {
                Text("Po tej operacji aplikacja poprosi o ponowne wybranie kierowcy.")
            }
        }
    }
}
