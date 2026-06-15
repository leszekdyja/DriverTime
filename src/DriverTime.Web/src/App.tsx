import { BrowserRouter, Route, Routes } from "react-router-dom";

import AppLayout from "./layouts/AppLayout";

import DashboardPage from "./pages/DashboardPage";
import DriversPage from "./pages/DriversPage";
import ImportsPage from "./pages/ImportsPage";
import ReportsPage from "./pages/ReportsPage";

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
                        path="drivers"
                        element={<DriversPage />}
                    />

                    <Route
                        path="reports"
                        element={<ReportsPage />}
                    />
                </Route>
            </Routes>
        </BrowserRouter>
    );
}