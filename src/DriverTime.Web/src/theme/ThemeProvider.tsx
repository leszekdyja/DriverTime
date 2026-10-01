import {
    useCallback,
    useEffect,
    useMemo,
    useState,
    type ReactNode,
} from "react";

import { ThemeContext, type ThemeContextValue, type ThemeMode } from "./themeContextValue";

const storageKey = "drivertime.theme";

function getInitialTheme(): ThemeMode {
    if (typeof window === "undefined") {
        return "light";
    }

    const storedTheme = window.localStorage.getItem(storageKey);

    if (storedTheme === "light" || storedTheme === "dark") {
        return storedTheme;
    }

    return window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function applyTheme(theme: ThemeMode) {
    const root = document.documentElement;

    root.dataset.theme = theme;
    root.classList.toggle("dark", theme === "dark");
    root.style.colorScheme = theme;
}

export function ThemeProvider({ children }: { children: ReactNode }) {
    const [theme, setThemeState] = useState<ThemeMode>(getInitialTheme);

    useEffect(() => {
        applyTheme(theme);
        window.localStorage.setItem(storageKey, theme);
    }, [theme]);

    const setTheme = useCallback((nextTheme: ThemeMode) => {
        setThemeState(nextTheme);
    }, []);

    const toggleTheme = useCallback(() => {
        setThemeState((currentTheme) => (currentTheme === "dark" ? "light" : "dark"));
    }, []);

    const value = useMemo<ThemeContextValue>(
        () => ({
            theme,
            isDark: theme === "dark",
            setTheme,
            toggleTheme,
        }),
        [setTheme, theme, toggleTheme],
    );

    return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}
