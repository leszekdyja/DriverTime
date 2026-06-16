import { createContext } from "react";

export type ThemeMode = "light" | "dark";

export type ThemeContextValue = {
    theme: ThemeMode;
    isDark: boolean;
    setTheme: (theme: ThemeMode) => void;
    toggleTheme: () => void;
};

export const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);
