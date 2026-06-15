import { useState, type FormEvent } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";

import { useAuth } from "../auth/AuthContext";
import "../styles/auth.css";

export default function LoginPage() {
    const { user, login } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
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
                    : "Nie udalo sie zalogowac.",
            );
        } finally {
            setIsSubmitting(false);
        }
    }

    return (
        <main className="login-page">
            <section className="login-card">
                <div className="login-brand">
                    <span className="brand-mark">DT</span>
                    <div>
                        <strong>DriverTime</strong>
                        <span>Transport management</span>
                    </div>
                </div>

                <div className="login-heading">
                    <h1>Zaloguj sie</h1>
                    <p>Uzyskaj dostep do danych swojej firmy.</p>
                </div>

                <form onSubmit={handleSubmit}>
                    <label>
                        Adres e-mail
                        <input
                            type="email"
                            autoComplete="email"
                            value={email}
                            onChange={(event) => setEmail(event.target.value)}
                            required
                        />
                    </label>
                    <label>
                        Haslo
                        <input
                            type="password"
                            autoComplete="current-password"
                            value={password}
                            onChange={(event) => setPassword(event.target.value)}
                            required
                        />
                    </label>

                    {error && <p className="login-error" role="alert">{error}</p>}

                    <button type="submit" disabled={isSubmitting}>
                        {isSubmitting ? "Logowanie..." : "Zaloguj sie"}
                    </button>
                </form>
            </section>
        </main>
    );
}
