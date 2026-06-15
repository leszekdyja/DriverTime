import { useState, type FormEvent } from "react";
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from "react-router-dom";

import { useAuth } from "../auth/useAuth";
import "../styles/auth.css";

const DEMO_EMAIL = "demo@drivertime.app";
const DEMO_PASSWORD = "DriverTimeDemo123!";

export default function LoginPage() {
    const { user, login } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const [searchParams] = useSearchParams();
    const isDemoLogin = searchParams.get("demo") === "1";
    const [email, setEmail] = useState(isDemoLogin ? DEMO_EMAIL : "");
    const [password, setPassword] = useState(isDemoLogin ? DEMO_PASSWORD : "");
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState("");

    if (user) {
        return <Navigate to="/" replace />;
    }

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setIsSubmitting(true);
        setError("");

        try {
            await login(email, password);
            const target = (location.state as { from?: { pathname?: string } } | null)
                ?.from?.pathname;
            navigate(target || "/", { replace: true });
        } catch (loginError) {
            setError(
                loginError instanceof Error
                    ? loginError.message
                    : "Nie udało się zalogować.",
            );
        } finally {
            setIsSubmitting(false);
        }
    }

    function fillDemoCredentials() {
        setEmail(DEMO_EMAIL);
        setPassword(DEMO_PASSWORD);
        setError("");
    }

    return (
        <main className="login-page">
            <section className="login-showcase" aria-label="DriverTime">
                <Link className="public-brand" to="/welcome">
                    <img src="/logo-mark.svg" alt="" />
                    <span><strong>DriverTime</strong><small>Fleet intelligence</small></span>
                </Link>
                <div className="login-showcase-copy">
                    <span className="login-kicker">Jedno miejsce. Pełna kontrola.</span>
                    <h1>Dane z tachografów zamienione w czytelne decyzje.</h1>
                    <p>Importuj pliki DDD, analizuj czas pracy i przygotowuj raporty floty bez zbędnej administracji.</p>
                </div>
                <div className="login-trust-row">
                    <span>EU 561 / AETR</span>
                    <span>Bezpieczne JWT</span>
                    <span>PDF i Excel</span>
                </div>
            </section>

            <section className="login-panel">
                <div className="login-card">
                    <Link className="login-back-link" to="/welcome">Wróć do strony głównej</Link>
                    <div className="login-heading">
                        <span className="login-kicker">Panel klienta</span>
                        <h2>Zaloguj się do DriverTime</h2>
                        <p>Uzyskaj dostęp do danych swojej firmy i kierowców.</p>
                    </div>

                    <form onSubmit={handleSubmit}>
                        <label>
                            Adres e-mail
                            <input
                                type="email"
                                autoComplete="email"
                                value={email}
                                onChange={(event) => setEmail(event.target.value)}
                                placeholder="name@company.pl"
                                required
                            />
                        </label>
                        <label>
                            Hasło
                            <input
                                type="password"
                                autoComplete="current-password"
                                value={password}
                                onChange={(event) => setPassword(event.target.value)}
                                placeholder="Wpisz hasło"
                                required
                            />
                        </label>

                        {error && <p className="login-error" role="alert">{error}</p>}

                        <button className="login-submit" type="submit" disabled={isSubmitting}>
                            {isSubmitting ? "Logowanie..." : "Zaloguj się"}
                        </button>
                        <button className="demo-credentials-button" type="button" onClick={fillDemoCredentials}>
                            Uzupełnij dane konta demo
                        </button>
                    </form>

                    <p className="demo-hint">
                        Demo: <strong>{DEMO_EMAIL}</strong> / <strong>{DEMO_PASSWORD}</strong>
                    </p>
                </div>
            </section>
        </main>
    );
}
