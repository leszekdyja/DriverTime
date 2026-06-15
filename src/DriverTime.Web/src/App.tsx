import { BrowserRouter, Route, Routes } from "react-router-dom";

import AppLayout from "./layouts/AppLayout";

import DashboardPage from "./pages/DashboardPage";
import DriversPage from "./pages/DriversPage";
import ImportDetailsPage from "./pages/ImportDetailsPage";
import ImportsPage from "./pages/ImportsPage";
import ReportsPage from "./pages/ReportsPage";
import ViolationsPage from "./pages/ViolationsPage";

export default function App() {
    return (
        <BrowserRouter>
            <Routes>
                <Route path="/" element={<AppLayout />}>
                    <Route
                        index
                        element={<DashboardPage />}
                    />

                    <Route
                        path="imports"
                        element={<ImportsPage />}
                    />

                    <Route
                        path="imports/:id"
                        element={<ImportDetailsPage />}
                    />

                    <Route
                        path="drivers"
                        element={<DriversPage />}
                    />

                    <Route
                        path="reports"
                        element={<ReportsPage />}
                    />

                    <Route
                        path="violations"
                        element={<ViolationsPage />}
                    />
                </Route>
            </Routes>
        </BrowserRouter>
    );
}
