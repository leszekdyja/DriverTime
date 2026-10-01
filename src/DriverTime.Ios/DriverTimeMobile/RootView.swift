import SwiftUI

struct RootView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Group {
            if appState.isRestoring {
                ProgressView("Ładowanie DriverTime...")
            } else if appState.currentUser == nil {
                LoginView()
            } else if appState.assignedDriver == nil {
                DriverBindingView()
            } else {
                ActivityDashboardView()
            }
        }
        .task {
            await appState.restore()
        }
    }
}
