import { Link, Outlet } from "react-router-dom";
import "../styles/layout.css";

export default function AppLayout() {
    return (
        <div className="app-container">
            <aside className="sidebar">
                <div className="logo">
                    DriverTime
                </div>

                <nav className="menu">
                    <Link to="/">Dashboard</Link>

                    <Link to="/drivers">
                        Kierowcy
                    </Link>

                    <Link to="/reports">
                        Raporty
                    </Link>

                    <Link to="/imports">
                        Importy DDD
                    </Link>
                </nav>
            </aside>

            <main className="main-content">
                <header className="topbar">
                    <h1>DriverTime</h1>
                </header>

                <section className="page-content">
                    <Outlet />
                </section>
            </main>
        </div>
    );
}
