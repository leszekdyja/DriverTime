import {
    useEffect,
    useState,
    type ReactNode,
} from "react";

import { AuthContext } from "./authContextValue";
import { AUTH_TOKEN_KEY } from "../services/apiClient";
import {
    getCurrentUser,
    login as loginRequest,
    type CurrentUser,
} from "../services/authService";

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<CurrentUser | null>(null);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        async function restoreSession() {
            if (!localStorage.getItem(AUTH_TOKEN_KEY)) {
                setIsLoading(false);
                return;
            }

            try {
                setUser(await getCurrentUser());
            } catch {
                localStorage.removeItem(AUTH_TOKEN_KEY);
            } finally {
                setIsLoading(false);
            }
        }

        void restoreSession();
    }, []);

    useEffect(() => {
        function clearExpiredSession() {
            setUser(null);
        }

        function updateCompany(event: Event) {
            const detail = (event as CustomEvent<{ name?: string }>).detail;
            if (!detail?.name) return;
            setUser((current) => current
                ? { ...current, companyName: detail.name ?? current.companyName }
                : current);
        }

        window.addEventListener("drivertime:logout", clearExpiredSession);
        window.addEventListener("drivertime:company-updated", updateCompany);

        return () => {
            window.removeEventListener("drivertime:logout", clearExpiredSession);
            window.removeEventListener("drivertime:company-updated", updateCompany);
        };
    }, []);

    async function login(email: string, password: string) {
        const result = await loginRequest(email, password);
        localStorage.setItem(AUTH_TOKEN_KEY, result.token);
        setUser(result.user);
    }

    function logout() {
        localStorage.removeItem(AUTH_TOKEN_KEY);
        setUser(null);
    }

    return (
        <AuthContext.Provider value={{ user, isLoading, login, logout }}>
            {children}
        </AuthContext.Provider>
    );
}
