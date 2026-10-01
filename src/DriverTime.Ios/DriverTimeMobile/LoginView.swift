import SwiftUI

struct LoginView: View {
    @EnvironmentObject private var appState: AppState
    @State private var baseURL = UserDefaults.standard.string(forKey: "pl.drivertime.mobile.baseURL") ?? "https://"
    @State private var email = ""
    @State private var password = ""
    @State private var isBusy = false

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField("Adres systemu DriverTime", text: $baseURL)
                        .textInputAutocapitalization(.never)
                        .keyboardType(.URL)
                        .autocorrectionDisabled()
                    TextField("E-mail", text: $email)
                        .textInputAutocapitalization(.never)
                        .keyboardType(.emailAddress)
                        .autocorrectionDisabled()
                    SecureField("Hasło", text: $password)
                } header: {
                    Text("Połączenie")
                } footer: {
                    Text("Podaj adres bez końcowego /api, np. https://twoja-domena.pl.")
                }

                if let error = appState.errorMessage {
                    Section {
                        Text(error)
                            .foregroundStyle(.red)
                    }
                }

                Section {
                    Button {
                        Task {
                            isBusy = true
                            await appState.login(baseURL: baseURL, email: email, password: password)
                            isBusy = false
                        }
                    } label: {
                        if isBusy {
                            ProgressView()
                        } else {
                            Text("Zaloguj")
                        }
                    }
                    .disabled(isBusy || baseURL.normalizedBaseURL.isEmpty || email.isEmpty || password.isEmpty)
                }
            }
            .navigationTitle("DriverTime")
        }
    }
}
