const helperBaseUrl = "http://localhost:47888";

export type CardReaderReader = {
    name: string;
    cardPresent: boolean | null;
    status: string;
    message: string;
    errorCode: number | null;
    errorCodeHex: string;
    isMock?: boolean;
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
    mockModeAvailable?: boolean;
    readers: CardReaderReader[];
};

export type CardReaderAtrResult = {
    readerName: string;
    cardPresent: boolean;
    connected: boolean;
    atrHex: string;
    atrLength: number;
    status: string;
    errorMessage: string;
    errorCode: number | null;
    errorCodeHex: string;
    activeProtocol: number;
    protocol: string;
    pcscState: number;
};

export type CardReaderStatus = {
    readerName: string;
    readerConnected: boolean;
    cardPresent: boolean;
    atr: string;
    atrLength: number;
    status: string;
    message: string;
    errorMessage: string;
    isMockReader: boolean;
    checkedAtUtc: string;
};

export type CardReaderDiagnosticReader = {
    name: string;
    cardPresent: boolean | null;
    status: string;
    message: string;
    atrHex: string;
    atrLength: number;
    connected: boolean;
    protocol: string;
    errorMessage: string;
    errorCodeHex: string;
};

export type CardReaderDiagnostics = {
    status: string;
    pcscAvailable: boolean;
    message: string;
    checkedAtUtc: string;
    readersCount: number;
    readers: CardReaderDiagnosticReader[];
    lastError: string;
};

export type CardReaderMockReadResult = {
    status: string;
    message: string;
    selectedReaderName: string;
    mockMode?: boolean;
    startedAtUtc: string;
    completedAtUtc: string;
    fileName: string;
    filePath: string;
};

export type CardReaderApduResponse = {
    name: string;
    commandHex: string;
    responseHex: string;
    statusWord: string;
    success: boolean;
    message: string;
};

export type CardReaderStructuredApduResponse = {
    name: string;
    description: string;
    commandHex: string;
    responseHex: string;
    dataHex: string;
    statusWord: string;
    sw1: string;
    sw2: string;
    success: boolean;
    statusMeaning: string;
    message: string;
};

export type CardReaderFileReadResult = {
    fileName: string;
    fileId: string;
    description: string;
    selectResponse: CardReaderStructuredApduResponse;
    readResponse: CardReaderStructuredApduResponse | null;
    selectSucceeded: boolean;
    readSucceeded: boolean;
    message: string;
};

export type CardReaderExportFormat = "TechnicalJson" | "C1B" | "DDD" | "Unknown" | string;

export type CardReaderDddExportResult = {
    isImportable: boolean;
    exportFormat: CardReaderExportFormat;
    nextStepMessage: string;
    outputFileName: string;
    outputPath: string;
    fileSizeBytes: number;
};

export type CardReaderTechnicalReadResult = {
    success: boolean;
    message: string;
    requestedReaderName: string;
    selectedReaderName: string;
    selectedReaderIsMock: boolean;
    readerName: string;
    atr: string;
    outputFileName: string;
    outputPath: string;
    fileSizeBytes: number;
    startedAtUtc: string;
    finishedAtUtc: string;
    errorDetails: string;
    isMock: boolean;
    fullDddExportReady: boolean;
    isImportable: boolean;
    exportFormat: CardReaderExportFormat;
    nextStepMessage: string;
    apduResponses: CardReaderApduResponse[];
};

export type TachographCardReadResult = {
    success: boolean;
    message: string;
    requestedReaderName: string;
    selectedReaderName: string;
    selectedReaderIsMock: boolean;
    readerName: string;
    atr: string;
    outputFileName: string;
    outputPath: string;
    fileSizeBytes: number;
    startedAtUtc: string;
    finishedAtUtc: string;
    errorDetails: string;
    isMock: boolean;
    isImportable: boolean;
    exportFormat: CardReaderExportFormat;
    nextStepMessage: string;
    apduResponses: CardReaderStructuredApduResponse[];
    fileReads: CardReaderFileReadResult[];
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

export function getCardReaderDiagnostics(): Promise<CardReaderDiagnostics> {
    return getJson<CardReaderDiagnostics>(
        "/api/diagnostics",
        "Nie udało się pobrać diagnostyki helpera.",
    );
}

export function readCardAtr(readerName: string): Promise<CardReaderAtrResult> {
    return getJson<CardReaderAtrResult>(
        `/api/readers/${encodeURIComponent(readerName)}/atr`,
        "Nie udało się odczytać ATR karty.",
    );
}

export function getCardReaderStatus(readerName?: string): Promise<CardReaderStatus> {
    const query = readerName ? `?readerName=${encodeURIComponent(readerName)}` : "";

    return getJson<CardReaderStatus>(
        `/api/reader-status${query}`,
        "Nie udało się pobrać statusu czytnika.",
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
            body: JSON.stringify({ selectedReaderName, readerName: selectedReaderName }),
        },
    );
}

export function startTechnicalCardRead(
    selectedReaderName?: string,
): Promise<CardReaderTechnicalReadResult> {
    return getJson<CardReaderTechnicalReadResult>(
        "/api/card/read/technical",
        "Nie udało się uruchomić technicznego odczytu karty.",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ selectedReaderName, readerName: selectedReaderName }),
        },
    );
}

export function startTachographStructureRead(
    selectedReaderName?: string,
): Promise<TachographCardReadResult> {
    return getJson<TachographCardReadResult>(
        "/api/card/read/tachograph-structure",
        "Nie udało się uruchomić odczytu struktury karty.",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ selectedReaderName, readerName: selectedReaderName }),
        },
    );
}
