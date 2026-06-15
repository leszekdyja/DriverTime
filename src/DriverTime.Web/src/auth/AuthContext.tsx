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

        window.addEventListener("drivertime:logout", clearExpiredSession);

        return () => window.removeEventListener("drivertime:logout", clearExpiredSession);
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
