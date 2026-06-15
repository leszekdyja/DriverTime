const configuredApiUrl = import.meta.env.VITE_API_URL as string | undefined;

export const API_URL = (configuredApiUrl ?? "https://localhost:65468").replace(
    /\/$/,
    "",
);
