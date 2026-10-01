import SwiftUI

struct DriverBindingView: View {
    @EnvironmentObject private var appState: AppState
    @State private var search = ""
    @State private var isRefreshing = false

    private var filteredDrivers: [Driver] {
        guard !search.trimmingCharacters(in: .whitespaces).isEmpty else {
            return appState.drivers
        }

        let value = search.lowercased()
        return appState.drivers.filter {
            $0.displayName.lowercased().contains(value)
                || $0.cardNumber.lowercased().contains(value)
        }
    }

    var body: some View {
        NavigationStack {
            List {
                Section {
                    Text("Wybierz kierowcę, do którego przypisany jest ten telefon. Po przypisaniu aplikacja będzie synchronizować tylko jego ewidencję.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }

                Section {
                    TextField("Szukaj kierowcy", text: $search)
                        .textInputAutocapitalization(.never)
                }

                Section("Kierowcy") {
                    ForEach(filteredDrivers) { driver in
                        Button {
                            Task {
                                await appState.assignDevice(to: driver)
                            }
                        } label: {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(driver.displayName)
                                    .font(.headline)
                                Text("Karta: \(driver.cardNumber)")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                        }
                    }
                }
            }
            .navigationTitle("Przypisz kierowcę")
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Wyloguj") {
                        appState.logout()
                    }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        Task {
                            isRefreshing = true
                            await appState.reloadDriversForBinding()
                            isRefreshing = false
                        }
                    } label: {
                        if isRefreshing {
                            ProgressView()
                        } else {
                            Image(systemName: "arrow.clockwise")
                        }
                    }
                }
            }
        }
    }
}
