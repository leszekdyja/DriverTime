import { apiFetch } from "./apiClient";

export type CardReadSessionStatus = "Started" | "Completed" | "Failed" | string;

export type CardReadSession = {
    id: string;
    companyId: string;
    userId: string | null;
    status: CardReadSessionStatus;
    readerName: string;
    driverCardNumber: string;
    dddFileId: string | null;
    errorMessage: string;
    notes: string;
    startedAtUtc: string;
    completedAtUtc: string | null;
    failedAtUtc: string | null;
    createdAtUtc: string;
};

export type StartCardReadSessionRequest = {
    readerName?: string;
    notes?: string;
};

export type CompleteCardReadSessionRequest = {
    driverCardNumber?: string;
    dddFileId?: string;
    notes?: string;
};

export type FailCardReadSessionRequest = {
    errorMessage?: string;
    notes?: string;
};

async function getJson<T>(
    path: string,
    errorMessage: string,
    init?: RequestInit,
): Promise<T> {
    const response = await apiFetch(path, init);

    if (!response.ok) {
        throw new Error(errorMessage);
    }

    return response.json() as Promise<T>;
}

export function getCardReadSessions(): Promise<CardReadSession[]> {
    return getJson<CardReadSession[]>(
        "/api/card-reader/sessions",
        "Nie udało się pobrać sesji odczytu kart.",
    );
}

export function startCardReadSession(
    request: StartCardReadSessionRequest = {},
): Promise<CardReadSession> {
    return getJson<CardReadSession>(
        "/api/card-reader/sessions/start",
        "Nie udało się rozpocząć sesji odczytu karty.",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(request),
        },
    );
}

export function completeCardReadSession(
    id: string,
    request: CompleteCardReadSessionRequest = {},
): Promise<CardReadSession> {
    return getJson<CardReadSession>(
        `/api/card-reader/sessions/${id}/complete`,
        "Nie udało się zakończyć sesji odczytu karty.",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(request),
        },
    );
}

export function failCardReadSession(
    id: string,
    request: FailCardReadSessionRequest = {},
): Promise<CardReadSession> {
    return getJson<CardReadSession>(
        `/api/card-reader/sessions/${id}/fail`,
        "Nie udało się oznaczyć sesji odczytu jako błędnej.",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(request),
        },
    );
}
