const helperBaseUrl = "http://localhost:47888";

export type CardReaderHelperHealth = {
    status: string;
    mode: string;
    service: string;
    message: string;
    checkedAtUtc: string;
};

export type CardReaderReaderList = {
    status: string;
    message: string;
    readers: unknown[];
};

export type CardReaderMockReadResult = {
    status: string;
    message: string;
    startedAtUtc: string;
    completedAtUtc: string;
    fileName: string;
    filePath: string;
};

async function getJson<T>(
    path: string,
    errorMessage: string,
    init?: RequestInit,
): Promise<T> {
    const response = await fetch(`${helperBaseUrl}${path}`, init);

    if (!response.ok) {
        throw new Error(errorMessage);
    }

    return response.json() as Promise<T>;
}

export function checkCardReaderHelperHealth(): Promise<CardReaderHelperHealth> {
    return getJson<CardReaderHelperHealth>(
        "/health",
        "Helper odczytu karty jest niedostępny.",
    );
}

export function getCardReaderReaders(): Promise<CardReaderReaderList> {
    return getJson<CardReaderReaderList>(
        "/api/readers",
        "Nie udało się pobrać listy czytników z helpera.",
    );
}

export function startMockCardRead(): Promise<CardReaderMockReadResult> {
    return getJson<CardReaderMockReadResult>(
        "/api/card/read/start",
        "Nie udało się uruchomić testowego odczytu karty.",
        { method: "POST" },
    );
}
