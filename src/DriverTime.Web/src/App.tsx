import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";

import { lazy, Suspense } from "react";
import { DesktopOnly } from "./components/mobile/DesktopOnly";
import { RouteLoadBoundary } from "./components/mobile/RouteLoadBoundary";
import { AuthProvider } from "./auth/AuthContext";
import ProtectedRoute from "./auth/ProtectedRoute";
import MainAccountRoute from "./auth/MainAccountRoute";
import AppLayout from "./layouts/AppLayout";
import { ThemeProvider } from "./theme/ThemeProvider";

const DashboardPage = lazy(() => import("./pages/DashboardPage"));
const AccountPage = lazy(() => import("./pages/AccountPage"));
const AlertsPage = lazy(() => import("./pages/AlertsPage"));
const CardReaderPage = lazy(() => import("./pages/CardReaderPage"));
const CompanySettingsPage = lazy(() => import("./pages/CompanySettingsPage"));
const DriverDetailsPage = lazy(() => import("./pages/DriverDetailsPage"));
const DriversPage = lazy(() => import("./pages/DriversPage"));
const DownloadsPage = lazy(() => import("./pages/DownloadsPage"));
const ImportDetailsPage = lazy(() => import("./pages/ImportDetailsPage"));
const ImportMonitoringPage = lazy(() => import("./pages/ImportMonitoringPage"));
const ImportsPage = lazy(() => import("./pages/ImportsPage"));
const LandingPage = lazy(() => import("./pages/LandingPage"));
const LoginPage = lazy(() => import("./pages/LoginPage"));
const PlanningPage = lazy(() => import("./pages/PlanningPage"));
const OperatingCompaniesPage = lazy(() => import("./pages/OperatingCompaniesPage"));
const ReportsPage = lazy(() => import("./pages/ReportsPage"));
const VehicleDetailsPage = lazy(() => import("./pages/VehicleDetailsPage"));
const VehiclesPage = lazy(() => import("./pages/VehiclesPage"));
const ViolationsPage = lazy(() => import("./pages/ViolationsPage"));
const WorkEvidencePage = lazy(() => import("./pages/WorkEvidencePage"));

export default function App() {
    return (
        <ThemeProvider>
            <BrowserRouter>
                <AuthProvider>
                    <RouteLoadBoundary><Suspense fallback={<p className="route-loading" role="status">Ładowanie strony…</p>}>
                    <Routes>
                        <Route path="/welcome" element={<LandingPage />} />
                        <Route path="/login" element={<LoginPage />} />
                        <Route path="/mobile/setup" element={<Navigate to="/login" replace />} />
                        <Route element={<ProtectedRoute />}>
                            <Route path="/" element={<AppLayout />}>
                                <Route index element={<DashboardPage />} />
                                <Route path="alerts" element={<AlertsPage />} />
                                <Route path="imports" element={<ImportsPage />} />
                                <Route path="import-monitoring" element={<ImportMonitoringPage />} />
                                <Route path="imports/:id" element={<ImportDetailsPage />} />
                                <Route path="drivers" element={<DriversPage />} />
                                <Route element={<MainAccountRoute />}>
                                    <Route path="companies" element={<OperatingCompaniesPage />} />
                                </Route>
                                <Route path="drivers/:id" element={<DriverDetailsPage />} />
                                <Route path="card-reader" element={<DesktopOnly reader><CardReaderPage /></DesktopOnly>} />
                                <Route path="downloads" element={<DownloadsPage />} />
                                <Route path="vehicles" element={<VehiclesPage />} />
                                <Route path="planning" element={<DesktopOnly><PlanningPage /></DesktopOnly>} />
                                <Route path="work-evidence" element={<WorkEvidencePage />} />
                                <Route path="vehicles/:vehicleId" element={<VehicleDetailsPage />} />
                                <Route path="reports" element={<ReportsPage />} />
                                <Route path="violations" element={<ViolationsPage />} />
                                <Route path="company-settings" element={<CompanySettingsPage />} />
                                <Route path="account" element={<AccountPage />} />
                                <Route path="*" element={<Navigate to="/" replace />} />
                            </Route>
                        </Route>
                    </Routes>
                    </Suspense></RouteLoadBoundary>
                </AuthProvider>
            </BrowserRouter>
        </ThemeProvider>
    );
}

