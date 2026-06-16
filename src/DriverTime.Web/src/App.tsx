import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";

import { AuthProvider } from "./auth/AuthContext";
import ProtectedRoute from "./auth/ProtectedRoute";
import AppLayout from "./layouts/AppLayout";
import { ThemeProvider } from "./theme/ThemeProvider";

import DashboardPage from "./pages/DashboardPage";
import AccountPage from "./pages/AccountPage";
import CompanySettingsPage from "./pages/CompanySettingsPage";
import DriverDetailsPage from "./pages/DriverDetailsPage";
import DriversPage from "./pages/DriversPage";
import ImportDetailsPage from "./pages/ImportDetailsPage";
import ImportsPage from "./pages/ImportsPage";
import LandingPage from "./pages/LandingPage";
import LoginPage from "./pages/LoginPage";
import ReportsPage from "./pages/ReportsPage";
import ViolationsPage from "./pages/ViolationsPage";

export default function App() {
    return (
        <ThemeProvider>
            <BrowserRouter>
                <AuthProvider>
                    <Routes>
                        <Route path="/welcome" element={<LandingPage />} />
                        <Route path="/login" element={<LoginPage />} />
                        <Route element={<ProtectedRoute />}>
                            <Route path="/" element={<AppLayout />}>
                                <Route index element={<DashboardPage />} />
                                <Route path="imports" element={<ImportsPage />} />
                                <Route path="imports/:id" element={<ImportDetailsPage />} />
                                <Route path="drivers" element={<DriversPage />} />
                                <Route path="drivers/:id" element={<DriverDetailsPage />} />
                                <Route path="reports" element={<ReportsPage />} />
                                <Route path="violations" element={<ViolationsPage />} />
                                <Route path="company-settings" element={<CompanySettingsPage />} />
                                <Route path="account" element={<AccountPage />} />
                                <Route path="*" element={<Navigate to="/" replace />} />
                            </Route>
                        </Route>
                    </Routes>
                </AuthProvider>
            </BrowserRouter>
        </ThemeProvider>
    );
}
