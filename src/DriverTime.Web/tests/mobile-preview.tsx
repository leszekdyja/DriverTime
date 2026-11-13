// Development-only fixture. Not a production entry point; never uses a live API.
import { createRoot } from "react-dom/client";
import { MemoryRouter, Route, Routes, Link } from "react-router-dom";
import { AuthContext } from "../src/auth/authContextValue";
import { ThemeProvider } from "../src/theme/ThemeProvider";
import AppLayout from "../src/layouts/AppLayout";
import { DesktopOnly } from "../src/components/mobile/DesktopOnly";
import DriversPage from "../src/pages/DriversPage";
import VehiclesPage from "../src/pages/VehiclesPage";
import TachographTimeline from "../src/components/timeline/TachographTimeline";
import "../src/index.css";
import "../src/styles/theme.css";
import "../src/styles/ui.css";
import "../src/styles/saas-demo.css";

const user = { id: "demo", companyId: "demo", companyName: "Firma demonstracyjna", firstName: "Użytkownik", lastName: "Testowy", email: "demo@example.invalid", role: "Admin" };
const drivers = [{ id: "demo-driver", firstName: "Jan", lastName: "Przykładowy", cardNumber: "TEST0000000001", cardExpiryDate: "2029-10-01", cardIssuingCountry: "PL", operatingCompanyId: "demo-company", operatingCompanyName: "Firma demonstracyjna", includeInPlanning: true, isActive: true }];
const vehicles = [{ id: "demo-vehicle", registrationNumber: "TEST 12345", vin: "DEMONSTRACYJNYVIN1", active: true, operatingCompanyId: "demo-company", operatingCompanyName: "Firma demonstracyjna" }];
window.fetch = async (input, options) => {
    if (options?.method && options.method !== "GET") throw new Error("Test interfejsu nie pozwala zapisywać danych");
    const path = new URL(String(input), location.origin).pathname;
    const data = path === "/api/drivers" ? drivers : path === "/api/vehicles" ? vehicles
        : path === "/api/operating-companies" ? [{ id: "demo-company", name: "Firma demonstracyjna", active: true }] : [];
    return new Response(JSON.stringify(data), { status: 200, headers: { "Content-Type": "application/json" } });
};
const at = (minutes: number) => new Date(Date.UTC(2026, 9, 1, 0, minutes)).toISOString();
const activities = [
    { id: "drive1", activityType: "DRIVING", startUtc: at(0), endUtc: at(270), isCrew: true, cardSlot: 1 },
    { id: "available", activityType: "AVAILABILITY", startUtc: at(270), endUtc: at(315), isCrew: true, cardSlot: 2 },
    { id: "drive2", activityType: "DRIVING", startUtc: at(315), endUtc: at(375), isCrew: true, cardSlot: 1 },
    { id: "rest", activityType: "REST", startUtc: at(375), endUtc: at(1440) },
];
const screen = new URLSearchParams(location.search).get("screen") || "drivers";
createRoot(document.getElementById("root")!).render(
    <ThemeProvider><AuthContext.Provider value={{ user, isLoading: false, login: async () => {}, logout: () => {} }}>
        <MemoryRouter initialEntries={[screen === "timeline" ? "/drivers/demo" : `/${screen}`]}>
            <Routes><Route element={<AppLayout />}>
                <Route path="drivers" element={<DriversPage />} />
                <Route path="vehicles" element={<VehiclesPage />} />
                <Route path="drivers/demo" element={<TachographTimeline activities={activities} day="2026-10-01" />} />
                <Route path="planning" element={<DesktopOnly><p data-testid="desktop-planning-sentinel">Komputerowy widok grafiku — test blokady</p></DesktopOnly>} />
                <Route path="*" element={<section><h2>Podgląd mobilny</h2><Link to="/drivers">Kierowcy</Link></section>} />
            </Route></Routes>
        </MemoryRouter>
    </AuthContext.Provider></ThemeProvider>
);
