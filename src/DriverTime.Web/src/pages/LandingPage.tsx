import { Link } from "react-router-dom";

import { useAuth } from "../auth/useAuth";
import "../styles/landing.css";

const features = [
    {
        number: "01",
        title: "Automatyczny import DDD",
        description: "Przeciągnij pliki kart kierowców i od razu zobacz aktywności, pojazdy oraz historię importów.",
    },
    {
        number: "02",
        title: "Kontrola ryzyka floty",
        description: "Dashboard porządkuje alerty, naruszenia EU 561/AETR i kierowców wymagających uwagi.",
    },
    {
        number: "03",
        title: "Raporty gotowe do pracy",
        description: "Generuj profesjonalne raporty PDF i Excel dla wybranego kierowcy i zakresu dat.",
    },
];

export default function LandingPage() {
    const { user } = useAuth();

    return (
        <main className="landing-page">
            <header className="landing-header">
                <Link className="public-brand" to="/welcome">
                    <img src="/logo-mark.svg" alt="" />
                    <span><strong>DriverTime</strong><small>Fleet intelligence</small></span>
                </Link>
                <nav className="landing-nav" aria-label="Nawigacja strony startowej">
                    <a href="#features">Funkcje</a>
                    <a href="#reports">Raporty</a>
                    <Link className="landing-login-link" to={user ? "/" : "/login"}>
                        {user ? "Przejdź do panelu" : "Zaloguj się"}
                    </Link>
                </nav>
            </header>

            <section className="landing-hero">
                <div className="landing-hero-copy">
                    <span className="landing-eyebrow">Nowoczesne zarządzanie czasem pracy</span>
                    <h1>Twoja flota.<br /><span>Jedno źródło prawdy.</span></h1>
                    <p>
                        DriverTime łączy dane DDD, kierowców, naruszenia i raporty w jednym
                        przejrzystym panelu przygotowanym dla transportu.
                    </p>
                    <div className="landing-actions">
                        <Link className="landing-primary-action" to={user ? "/" : "/login?demo=1"}>
                            {user ? "Otwórz dashboard" : "Uruchom demo"}
                        </Link>
                        <a className="landing-secondary-action" href="#features">Zobacz możliwości</a>
                    </div>
                    <div className="landing-proof">
                        <span><strong>DDD</strong> szybki import</span>
                        <span><strong>EU 561</strong> analiza naruszeń</span>
                        <span><strong>24/7</strong> dostęp do danych</span>
                    </div>
                </div>

                <div className="landing-dashboard-preview" aria-label="Podgląd dashboardu DriverTime">
                    <div className="preview-window-bar"><i /><i /><i /><span>DriverTime / Dashboard</span></div>
                    <div className="preview-grid">
                        <article><span>Aktywne alerty</span><strong>3</strong><small>1 wymaga uwagi</small></article>
                        <article><span>Kierowcy</span><strong>24</strong><small>22 aktywnych</small></article>
                        <article><span>Importy DDD</span><strong>148</strong><small>+12 w tym tygodniu</small></article>
                    </div>
                    <div className="preview-content">
                        <div className="preview-chart">
                            <div className="preview-section-heading"><strong>Aktywność floty</strong><span>Ostatnie 7 dni</span></div>
                            <div className="preview-bars">
                                {[58, 78, 64, 88, 72, 92, 68].map((height, index) => (
                                    <i key={index} style={{ height: `${height}%` }} />
                                ))}
                            </div>
                        </div>
                        <div className="preview-risk">
                            <div className="preview-section-heading"><strong>Ryzyko</strong><span>Na żywo</span></div>
                            <p><i className="risk-low" />Niskie <strong>18</strong></p>
                            <p><i className="risk-medium" />Średnie <strong>4</strong></p>
                            <p><i className="risk-high" />Wysokie <strong>2</strong></p>
                        </div>
                    </div>
                </div>
            </section>

            <section className="landing-features" id="features">
                <div className="landing-section-heading">
                    <span>Platforma DriverTime</span>
                    <h2>Od surowego pliku do decyzji operacyjnej.</h2>
                </div>
                <div className="landing-feature-grid">
                    {features.map((feature) => (
                        <article key={feature.number}>
                            <span>{feature.number}</span>
                            <h3>{feature.title}</h3>
                            <p>{feature.description}</p>
                        </article>
                    ))}
                </div>
            </section>

            <section className="landing-reports" id="reports">
                <div>
                    <span className="landing-eyebrow">Raportowanie bez kompromisów</span>
                    <h2>Dane gotowe do analizy, kontroli i rozmowy z klientem.</h2>
                    <p>
                        Czytelne podsumowania czasu jazdy, pracy, odpoczynku i dyspozycyjności.
                        Eksportuj wybrany zakres do PDF lub Excel bez ręcznego przepisywania danych.
                    </p>
                    <ul>
                        <li>Raport kierowcy z pełną tabelą aktywności</li>
                        <li>Dane firmy i kierowcy w profesjonalnym układzie</li>
                        <li>Historia importów i audyt danych źródłowych</li>
                    </ul>
                </div>
                <div className="landing-report-card">
                    <div><img src="/logo-mark.svg" alt="" /><span>DriverTime Report</span><small>15 czerwca 2026</small></div>
                    <h3>Raport aktywności kierowcy</h3>
                    <p>Marek Kowalski / PL-MK-78451236</p>
                    <div className="report-metrics">
                        <span><small>Jazda</small><strong>42:18</strong></span>
                        <span><small>Praca</small><strong>08:45</strong></span>
                        <span><small>Odpoczynek</small><strong>61:30</strong></span>
                    </div>
                    <div className="report-lines"><i /><i /><i /><i /><i /></div>
                </div>
            </section>

            <section className="landing-cta">
                <div><span>DriverTime Demo</span><h2>Zobacz gotowy panel z przykładowymi danymi floty.</h2></div>
                <Link to={user ? "/" : "/login?demo=1"}>{user ? "Wróć do panelu" : "Zaloguj się do demo"}</Link>
            </section>

            <footer className="landing-footer">
                <span>© {new Date().getFullYear()} DriverTime</span>
                <span>Fleet data. Clear decisions.</span>
            </footer>
        </main>
    );
}
