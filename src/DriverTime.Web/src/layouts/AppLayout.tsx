import { Link, NavLink, Outlet, useLocation } from "react-router-dom";
import type { ReactNode } from "react";

import { useAuth } from "../auth/useAuth";
import { useTheme } from "../theme/useTheme";
import "../styles/layout.css";

const navigationItems = [
    { to: "/", label: "Dashboard", icon: "dashboard" },
    { to: "/alerts", label: "Alerty", icon: "alerts" },
    { to: "/drivers", label: "Kierowcy", icon: "drivers" },
    { to: "/card-reader", label: "Odczyt karty", icon: "cardReader" },
    { to: "/vehicles", label: "Pojazdy", icon: "vehicles" },
    { to: "/downloads", label: "Odczyty", icon: "downloads" },
    { to: "/reports", label: "Raporty", icon: "reports" },
    { to: "/violations", label: "Naruszenia", icon: "alerts" },
    { to: "/imports", label: "Importy DDD", icon: "imports" },
    { to: "/import-monitoring", label: "Monitoring importów", icon: "monitoring" },
    { to: "/company-settings", label: "Ustawienia firmy", icon: "company" },
    { to: "/account", label: "Moje konto", icon: "account" },
];

const roleLabels: Record<string, string> = {
    Admin: "Administrator",
    Dispatcher: "Dyspozytor",
    Driver: "Kierowca",
};

function formatRole(role?: string) {
    return role ? roleLabels[role] ?? role : "";
}

const currentDateFormatter = new Intl.DateTimeFormat("pl-PL", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
});

export default function AppLayout() {
    const { user, logout } = useAuth();
    const { isDark, toggleTheme } = useTheme();
    const location = useLocation();
    const initials = `${user?.firstName?.[0] ?? ""}${user?.lastName?.[0] ?? ""}` || "U";
    const currentSection = [...navigationItems]
        .sort((left, right) => right.to.length - left.to.length)
        .find((item) => item.to === "/"
            ? location.pathname === "/"
            : location.pathname.startsWith(item.to));

    return (
        <div className="app-shell">
            <aside className="sidebar">
                <div className="brand">
                    <span className="brand-mark">DT</span>
                    <div>
                        <strong>DriverTime</strong>
                        <span>Fleet intelligence</span>
                    </div>
                </div>

                <nav className="sidebar-nav" aria-label="Nawigacja glowna">
                    {navigationItems.map((item) => (
                        <NavLink
                            key={item.to}
                            to={item.to}
                            end={item.to === "/"}
                            className={({ isActive }) =>
                                `nav-link${isActive ? " active" : ""}`
                            }
                        >
                            <NavigationIcon name={item.icon} />
                            <span>{item.label}</span>
                        </NavLink>
                    ))}
                </nav>

                <div className="sidebar-footer">
                    <span className="sidebar-status"><i /> System operacyjny</span>
                    <small>DriverTime SaaS workspace</small>
                </div>
            </aside>

            <div className="app-main">
                <header className="topbar">
                    <div className="topbar-title">
                        <span className="topbar-eyebrow">DriverTime / {currentSection?.label ?? "Panel"}</span>
                        <strong>{currentSection?.label ?? "DriverTime"}</strong>
                    </div>

                    <div className="topbar-actions">
                        <span className="topbar-date">{currentDateFormatter.format(new Date())}</span>
                        <button
                            className="theme-toggle"
                            type="button"
                            onClick={toggleTheme}
                            aria-label={isDark ? "Przełącz na jasny motyw" : "Przełącz na ciemny motyw"}
                            aria-pressed={isDark}
                        >
                            <span className="theme-toggle-track" aria-hidden="true">
                                <span className="theme-toggle-thumb">{isDark ? "N" : "D"}</span>
                            </span>
                            <span>{isDark ? "Ciemny" : "Jasny"}</span>
                        </button>
                        <Link className="profile-placeholder" aria-label="Profil użytkownika" to="/account">
                            <span className="profile-avatar">{initials}</span>
                            <div>
                                <strong>{`${user?.firstName ?? ""} ${user?.lastName ?? ""}`.trim() || user?.email}</strong>
                                <span>{user?.companyName} / {formatRole(user?.role)}</span>
                            </div>
                        </Link>
                        <button className="logout-button" type="button" onClick={logout}>
                            Wyloguj
                        </button>
                    </div>
                </header>

                <main className="page-content">
                    <Outlet />
                </main>
            </div>
        </div>
    );
}

function NavigationIcon({ name }: { name: string }) {
    const paths: Record<string, ReactNode> = {
        dashboard: <><rect x="3" y="3" width="7" height="7" rx="2" /><rect x="14" y="3" width="7" height="7" rx="2" /><rect x="3" y="14" width="7" height="7" rx="2" /><rect x="14" y="14" width="7" height="7" rx="2" /></>,
        drivers: <><circle cx="9" cy="8" r="4" /><path d="M3 21v-2a6 6 0 0 1 12 0v2" /><path d="M16 4.5a4 4 0 0 1 0 7" /><path d="M18 15a6 6 0 0 1 3 5" /></>,
        cardReader: <><rect x="3" y="5" width="18" height="14" rx="2" /><path d="M7 9h5" /><path d="M7 13h10" /><path d="M16 9h1" /></>,
        vehicles: <><path d="M5 17h14" /><path d="M7 17v2" /><path d="M17 17v2" /><path d="M6 13l2-5h8l2 5" /><path d="M4 13h16v4H4z" /><circle cx="8" cy="15" r="1" /><circle cx="16" cy="15" r="1" /></>,
        downloads: <><path d="M12 3v10" /><path d="m8 9 4 4 4-4" /><path d="M5 21h14" /><path d="M7 17h10" /></>,
        reports: <><path d="M4 19V9" /><path d="M10 19V5" /><path d="M16 19v-7" /><path d="M22 19V3" /><path d="M2 21h22" /></>,
        alerts: <><path d="M12 3 2.8 20h18.4L12 3Z" /><path d="M12 9v5" /><path d="M12 17.5h.01" /></>,
        imports: <><path d="M12 3v12" /><path d="m7 10 5 5 5-5" /><path d="M4 19h16" /></>,
        monitoring: <><path d="M4 19V5" /><path d="M4 19h16" /><path d="M8 15l3-3 3 2 4-6" /><circle cx="18" cy="8" r="1.5" /></>,
        company: <><path d="M4 21V7l8-4v18" /><path d="M12 9h8v12" /><path d="M7 9h2M7 13h2M7 17h2M15 13h2M15 17h2" /></>,
        account: <><circle cx="12" cy="8" r="4" /><path d="M4 21a8 8 0 0 1 16 0" /></>,
    };

    return <span className="nav-icon" aria-hidden="true"><svg viewBox="0 0 24 24">{paths[name]}</svg></span>;
}
