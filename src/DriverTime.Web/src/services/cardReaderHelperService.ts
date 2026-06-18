const helperBaseUrl = "http://localhost:47888";

export type CardReaderReader = {
    name: string;
    cardPresent: boolean | null;
    status: string;
    message: string;
};

export type CardReaderHelperHealth = {
    status: string;
    mode: string;
    service: string;
    pcscAvailable: boolean;
    readersCount: number;
    message: string;
    checkedAtUtc: string;
};

export type CardReaderReaderList = {
    status: string;
    pcscAvailable: boolean;
    message: string;
    readers: CardReaderReader[];
};

export type CardReaderAtrResult = {
    readerName: string;
    cardPresent: boolean;
    atrHex: string;
    atrLength: number;
    status: string;
    errorMessage: string;
};

export type CardReaderMockReadResult = {
    status: string;
    message: string;
    selectedReaderName: string;
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

export function readCardAtr(readerName: string): Promise<CardReaderAtrResult> {
    return getJson<CardReaderAtrResult>(
        `/api/readers/${encodeURIComponent(readerName)}/atr`,
        "Nie udało się odczytać ATR karty.",
    );
}

export function startMockCardRead(
    selectedReaderName?: string,
): Promise<CardReaderMockReadResult> {
    return getJson<CardReaderMockReadResult>(
        "/api/card/read/start",
        "Nie udało się uruchomić testowego odczytu karty.",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ selectedReaderName }),
        },
    );
}
