import { NavLink, Outlet } from "react-router-dom";

import "../styles/layout.css";

const navigationItems = [
    { to: "/", label: "Dashboard", shortLabel: "D" },
    { to: "/drivers", label: "Kierowcy", shortLabel: "K" },
    { to: "/reports", label: "Raporty", shortLabel: "R" },
    { to: "/violations", label: "Naruszenia", shortLabel: "N" },
    { to: "/imports", label: "Importy DDD", shortLabel: "I" },
];

const currentDateFormatter = new Intl.DateTimeFormat("pl-PL", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
});

export default function AppLayout() {
    return (
        <div className="app-shell">
            <aside className="sidebar">
                <div className="brand">
                    <span className="brand-mark">DT</span>
                    <div>
                        <strong>DriverTime</strong>
                        <span>Transport management</span>
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
                            <span className="nav-icon" aria-hidden="true">
                                {item.shortLabel}
                            </span>
                            <span>{item.label}</span>
                        </NavLink>
                    ))}
                </nav>

                <div className="sidebar-footer">
                    <span>DriverTime</span>
                    <small>Local workspace</small>
                </div>
            </aside>

            <div className="app-main">
                <header className="topbar">
                    <div className="topbar-title">
                        <strong>DriverTime</strong>
                        <span>{currentDateFormatter.format(new Date())}</span>
                    </div>

                    <div className="profile-placeholder" aria-label="Przyszly profil uzytkownika">
                        <span className="profile-avatar">U</span>
                        <div>
                            <strong>Profil uzytkownika</strong>
                            <span>Funkcja planowana</span>
                        </div>
                    </div>
                </header>

                <main className="page-content">
                    <Outlet />
                </main>
            </div>
        </div>
    );
}
