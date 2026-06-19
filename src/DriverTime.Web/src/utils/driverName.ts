export function formatDriverName(firstName?: string | null, lastName?: string | null) {
    const normalizedLastName = lastName?.trim() ?? "";
    const normalizedFirstName = firstName?.trim() ?? "";

    return [normalizedLastName, normalizedFirstName].filter(Boolean).join(" ");
}

export function formatDriverNameOrFallback(
    firstName?: string | null,
    lastName?: string | null,
    fallback = "Brak danych",
) {
    return formatDriverName(firstName, lastName) || fallback;
}
