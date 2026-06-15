import { createContext } from "react";

import type { CurrentUser } from "../services/authService";

export type AuthContextValue = {
    user: CurrentUser | null;
    isLoading: boolean;
    login: (email: string, password: string) => Promise<void>;
    logout: () => void;
};

export const AuthContext = createContext<AuthContextValue | null>(null);
