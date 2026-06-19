import { useCallback, useEffect, useMemo, useState } from "react";

import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    completeCardReadSession,
    failCardReadSession,
    getCardReadSessions,
    startCardReadSession,
    type CardReadSession,
} from "../services/cardReaderService";
import {
    checkCardReaderHelperHealth,
    getCardReaderDiagnostics,
    getCardReaderReaders,
    getCardReaderStatus,
    readCardAtr,
    startMockCardRead,
    startTachographStructureRead,
    startTechnicalCardRead,
    type CardReaderAtrResult,
    type CardReaderDiagnostics,
    type CardReaderHelperHealth,
    type CardReaderMockReadResult,
    type CardReaderReader,
    type CardReaderReaderList,
    type CardReaderStatus,
    type CardReaderTechnicalReadResult,
    type TachographCardReadResult,
} from "../services/cardReaderHelperService";
import "../styles/card-reader.css";

type BadgeTone = "neutral" | "success" | "warning" | "danger" | "info" | "critical";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) return "Brak danych";

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function getStatusLabel(status: string) {
    if (status === "Started") return "W trakcie";
    if (status === "Completed") return "Zakończony";
    if (status === "Failed") return "Błąd";
    return status || "Brak danych";
}

function getStatusTone(status: string): BadgeTone {
    if (status === "Started") return "info";
    if (status === "Completed") return "success";
    if (status === "Failed") return "danger";
    return "neutral";
}

function getReaderPresenceLabel(reader: Pick<CardReaderReader, "cardPresent">) {
    if (reader.cardPresent === true) return "Karta obecna";
    if (reader.cardPresent === false) return "Brak karty";
    return "Nieznany";
}

function getReaderPresenceTone(reader: Pick<CardReaderReader, "cardPresent">): BadgeTone {
    if (reader.cardPresent === true) return "success";
    if (reader.cardPresent === false) return "warning";
    return "neutral";
}

function getExportFormatLabel(format: string) {
    if (format === "TechnicalJson") return "Techniczny JSON";
    if (format === "C1B") return "C1B";
    if (format === "DDD") return "DDD";
    if (format === "Unknown") return "Nieznany";
    return format || "Nieznany";
}

function formatFileSize(bytes: number) {
    if (!bytes || bytes < 0) return "Brak danych";
    if (bytes < 1024) return `${bytes} B`;
    return `${(bytes / 1024).toFixed(1)} KB`;
}

function getApduTone(success: boolean): BadgeTone {
    return success ? "success" : "warning";
}

function upsertSession(sessions: CardReadSession[], session: CardReadSession) {
    return [session, ...sessions.filter((item) => item.id !== session.id)];
}

export default function CardReaderPage() {
    const [sessions, setSessions] = useState<CardReadSession[]>([]);
    const [helperHealth, setHelperHealth] = useState<CardReaderHelperHealth | null>(null);
    const [readers, setReaders] = useState<CardReaderReaderList | null>(null);
    const [diagnostics, setDiagnostics] = useState<CardReaderDiagnostics | null>(null);
    const [selectedReaderName, setSelectedReaderName] = useState("");
    const [atrResult, setAtrResult] = useState<CardReaderAtrResult | null>(null);
    const [readerStatus, setReaderStatus] = useState<CardReaderStatus | null>(null);
    const [mockReadResult, setMockReadResult] = useState<CardReaderMockReadResult | null>(null);
    const [technicalReadResult, setTechnicalReadResult] = useState<CardReaderTechnicalReadResult | null>(null);
    const [tachographStructureResult, setTachographStructureResult] = useState<TachographCardReadResult | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isStarting, setIsStarting] = useState(false);
    const [isFailing, setIsFailing] = useState(false);
    const [isCheckingHelper, setIsCheckingHelper] = useState(false);
    const [isCheckingReaders, setIsCheckingReaders] = useState(false);
    const [isCheckingDiagnostics, setIsCheckingDiagnostics] = useState(false);
    const [isReadingAtr, setIsReadingAtr] = useState(false);
    const [isMockReading, setIsMockReading] = useState(false);
    const [isTechnicalReading, setIsTechnicalReading] = useState(false);
    const [isStructureReading, setIsStructureReading] = useState(false);
    const [error, setError] = useState("");
    const [helperError, setHelperError] = useState("");

    const activeSession = useMemo(
        () => sessions.find((session) => session.status === "Started") ?? null,
        [sessions],
    );

    const readerList = readers?.readers ?? [];
    const realReadersCount = readerList.filter((reader) => !reader.isMock).length;

    const selectedReader = useMemo(
        () => readerList.find((reader) => reader.name === selectedReaderName) ?? null,
        [readerList, selectedReaderName],
    );

    const loadSessions = useCallback(async () => {
        setIsLoading(true);
        setError("");

        try {
            setSessions(await getCardReadSessions());
        } catch (loadError) {
            setError(loadError instanceof Error
                ? loadError.message
                : "Wystąpił błąd podczas pobierania sesji odczytu kart.");
        } finally {
            setIsLoading(false);
        }
    }, []);

    useEffect(() => {
        void loadSessions();
    }, [loadSessions]);

    async function startSession() {
        setIsStarting(true);
        setError("");

        try {
            const session = await startCardReadSession({
                readerName: selectedReaderName || "Czytnik karty kierowcy",
                notes: selectedReaderName
                    ? `Sesja przygotowana dla czytnika: ${selectedReaderName}.`
                    : "Sesja przygotowana pod przyszły odczyt PC/SC.",
            });

            setSessions((current) => upsertSession(current, session));
        } catch (startError) {
            setError(startError instanceof Error
                ? startError.message
                : "Nie udało się rozpocząć sesji odczytu karty.");
        } finally {
            setIsStarting(false);
        }
    }

    async function failActiveSession() {
        if (!activeSession) return;

        setIsFailing(true);
        setError("");

        try {
            const session = await failCardReadSession(activeSession.id, {
                errorMessage: "Sesja przerwana przed realnym odczytem karty.",
                notes: selectedReaderName ? `Czytnik: ${selectedReaderName}` : undefined,
            });

            setSessions((current) => upsertSession(current, session));
        } catch (failError) {
            setError(failError instanceof Error
                ? failError.message
                : "Nie udało się przerwać sesji odczytu karty.");
        } finally {
            setIsFailing(false);
        }
    }

    async function checkHelper() {
        setIsCheckingHelper(true);
        setHelperError("");

        try {
            setHelperHealth(await checkCardReaderHelperHealth());
        } catch (checkError) {
            setHelperHealth(null);
            setHelperError(checkError instanceof Error
                ? checkError.message
                : "Helper odczytu karty jest niedostępny.");
        } finally {
            setIsCheckingHelper(false);
        }
    }

    async function checkReaders() {
        setIsCheckingReaders(true);
        setHelperError("");
        setAtrResult(null);

        try {
            const readerResponse = await getCardReaderReaders();
            setReaders(readerResponse);

            const availableReaders = readerResponse.readers;
            const currentStillExists = availableReaders.some((reader) => reader.name === selectedReaderName);

            if (availableReaders.length > 0 && !currentStillExists) {
                setSelectedReaderName(availableReaders[0].name);
            }

            if (availableReaders.length === 0) {
                setSelectedReaderName("");
            }
        } catch (checkError) {
            setReaders(null);
            setSelectedReaderName("");
            setHelperError(checkError instanceof Error
                ? checkError.message
                : "Nie udało się sprawdzić czytników.");
        } finally {
            setIsCheckingReaders(false);
        }
    }

    const refreshReaderStatus = useCallback(async (readerName?: string) => {
        try {
            const status = await getCardReaderStatus(readerName || selectedReaderName || undefined);
            setReaderStatus(status);
        } catch {
            setReaderStatus(null);
        }
    }, [selectedReaderName]);

    useEffect(() => {
        if (!helperHealth && !readers) {
            return;
        }

        void refreshReaderStatus();
        const intervalId = window.setInterval(() => {
            void refreshReaderStatus();
        }, 4000);

        return () => window.clearInterval(intervalId);
    }, [helperHealth, readers, refreshReaderStatus]);

    async function checkDiagnostics() {
        setIsCheckingDiagnostics(true);
        setHelperError("");

        try {
            const result = await getCardReaderDiagnostics();
            setDiagnostics(result);

            if (result.readers.length > 0) {
                setReaders({
                    status: result.status,
                    pcscAvailable: result.pcscAvailable,
                    message: result.message,
                    readers: result.readers.map((reader) => ({
                        name: reader.name,
                        cardPresent: reader.cardPresent,
                        status: reader.status,
                        message: reader.message,
                        errorCode: null,
                        errorCodeHex: reader.errorCodeHex,
                    })),
                });

                if (!selectedReaderName) {
                    setSelectedReaderName(result.readers[0].name);
                }
            }
        } catch (diagnosticError) {
            setDiagnostics(null);
            setHelperError(diagnosticError instanceof Error
                ? diagnosticError.message
                : "Nie udało się pobrać diagnostyki helpera.");
        } finally {
            setIsCheckingDiagnostics(false);
        }
    }

    async function checkSelectedCardAtr() {
        if (!selectedReaderName) {
            setHelperError("Najpierw wybierz czytnik.");
            return;
        }

        if (selectedReader?.isMock) {
            setHelperError("Czytnik testowy nie ma fizycznej karty ani ATR. Użyj przycisku „Testowy odczyt karty”, aby sprawdzić przepływ UI.");
            return;
        }

        setIsReadingAtr(true);
        setHelperError("");
        setAtrResult(null);

        try {
            setAtrResult(await readCardAtr(selectedReaderName));
        } catch (readError) {
            setHelperError(readError instanceof Error
                ? readError.message
                : "Nie udało się odczytać ATR karty.");
        } finally {
            setIsReadingAtr(false);
        }
    }

    async function runMockRead() {
        setIsMockReading(true);
        setHelperError("");
        setError("");

        let session = activeSession;

        try {
            if (!session) {
                session = await startCardReadSession({
                    readerName: selectedReaderName || "Czytnik karty kierowcy",
                    notes: "Automatyczna sesja testowego odczytu helpera.",
                });
                setSessions((current) => upsertSession(current, session as CardReadSession));
            }

            const helperResult = await startMockCardRead(selectedReaderName || undefined);
            setMockReadResult(helperResult);

            const completedSession = await completeCardReadSession(session.id, {
                notes: [
                    helperResult.message,
                    helperResult.selectedReaderName ? `Czytnik: ${helperResult.selectedReaderName}` : "",
                    helperResult.fileName ? `Plik testowy: ${helperResult.fileName}` : "",
                ].filter(Boolean).join(" | "),
            });

            setSessions((current) => upsertSession(current, completedSession));
        } catch (readError) {
            const message = readError instanceof Error
                ? readError.message
                : "Nie udało się wykonać testowego odczytu karty.";

            setMockReadResult(null);
            setHelperError(message);

            if (session) {
                try {
                    const failedSession = await failCardReadSession(session.id, {
                        errorMessage: message,
                        notes: selectedReaderName ? `Czytnik: ${selectedReaderName}` : undefined,
                    });
                    setSessions((current) => upsertSession(current, failedSession));
                } catch {
                    setError("Helper zgłosił błąd, ale nie udało się zapisać statusu sesji w API.");
                }
            }
        } finally {
            setIsMockReading(false);
        }
    }

    async function runTechnicalRead() {
        if (!selectedReaderName) {
            setHelperError("Najpierw wybierz czytnik.");
            return;
        }

        setIsTechnicalReading(true);
        setHelperError("");
        setError("");
        setTechnicalReadResult(null);

        let session = activeSession;

        try {
            if (!session) {
                session = await startCardReadSession({
                    readerName: selectedReaderName,
                    notes: "Automatyczna sesja technicznego odczytu karty przez lokalny helper.",
                });
                setSessions((current) => upsertSession(current, session as CardReadSession));
            }

            const helperResult = await startTechnicalCardRead(selectedReaderName);
            setTechnicalReadResult(helperResult);

            if (helperResult.success) {
                const completedSession = await completeCardReadSession(session.id, {
                    notes: [
                        helperResult.message,
                        helperResult.readerName ? `Czytnik: ${helperResult.readerName}` : "",
                        helperResult.outputFileName ? `Plik: ${helperResult.outputFileName}` : "",
                        helperResult.isImportable
                            ? `Eksport ${getExportFormatLabel(helperResult.exportFormat)} gotowy do importu.`
                            : `Etap techniczny: ${getExportFormatLabel(helperResult.exportFormat)} bez pełnego C1B/DDD.`,
                    ].filter(Boolean).join(" | "),
                });

                setSessions((current) => upsertSession(current, completedSession));
                return;
            }

            const failedSession = await failCardReadSession(session.id, {
                errorMessage: helperResult.message,
                notes: helperResult.errorDetails || `Czytnik: ${selectedReaderName}`,
            });
            setSessions((current) => upsertSession(current, failedSession));
        } catch (readError) {
            const message = readError instanceof Error
                ? readError.message
                : "Nie udało się wykonać technicznego odczytu karty.";

            setHelperError(message);

            if (session) {
                try {
                    const failedSession = await failCardReadSession(session.id, {
                        errorMessage: message,
                        notes: selectedReaderName ? `Czytnik: ${selectedReaderName}` : undefined,
                    });
                    setSessions((current) => upsertSession(current, failedSession));
                } catch {
                    setError("Helper zgłosił błąd, ale nie udało się zapisać statusu sesji w API.");
                }
            }
        } finally {
            setIsTechnicalReading(false);
        }
    }

    async function runTachographStructureRead() {
        if (!selectedReaderName) {
            setHelperError("Najpierw wybierz czytnik.");
            return;
        }

        setIsStructureReading(true);
        setHelperError("");
        setError("");
        setTachographStructureResult(null);

        let session = activeSession;

        try {
            if (!session) {
                session = await startCardReadSession({
                    readerName: selectedReaderName,
                    notes: "Automatyczna sesja odczytu struktury karty tachografu przez lokalny helper.",
                });
                setSessions((current) => upsertSession(current, session as CardReadSession));
            }

            const helperResult = await startTachographStructureRead(selectedReaderName);
            setTachographStructureResult(helperResult);

            if (helperResult.success) {
                const completedSession = await completeCardReadSession(session.id, {
                    notes: [
                        helperResult.message,
                        helperResult.readerName ? `Czytnik: ${helperResult.readerName}` : "",
                        helperResult.outputFileName ? `Plik: ${helperResult.outputFileName}` : "",
                        `Format: ${getExportFormatLabel(helperResult.exportFormat)}`,
                        helperResult.isImportable ? "Gotowy do importu." : "Odczyt struktury bez pełnego C1B/DDD.",
                    ].filter(Boolean).join(" | "),
                });

                setSessions((current) => upsertSession(current, completedSession));
                return;
            }

            const failedSession = await failCardReadSession(session.id, {
                errorMessage: helperResult.message,
                notes: helperResult.errorDetails || `Czytnik: ${selectedReaderName}`,
            });
            setSessions((current) => upsertSession(current, failedSession));
        } catch (readError) {
            const message = readError instanceof Error
                ? readError.message
                : "Nie udało się wykonać odczytu struktury karty.";

            setHelperError(message);

            if (session) {
                try {
                    const failedSession = await failCardReadSession(session.id, {
                        errorMessage: message,
                        notes: selectedReaderName ? `Czytnik: ${selectedReaderName}` : undefined,
                    });
                    setSessions((current) => upsertSession(current, failedSession));
                } catch {
                    setError("Helper zgłosił błąd, ale nie udało się zapisać statusu sesji w API.");
                }
            }
        } finally {
            setIsStructureReading(false);
        }
    }

    return (
        <div className="card-reader-page">
            <section className="card-reader-hero">
                <div>
                    <span>Moduł MVP</span>
                    <h2>Odczyt karty</h2>
                    <p>
                        Helper lokalny wykrywa czytniki PC/SC i potrafi sprawdzić ATR karty.
                        Historia sesji jest zapisywana przez backend DriverTime.
                    </p>
                </div>
                <div className="card-reader-actions">
                    <button type="button" onClick={startSession} disabled={isStarting || Boolean(activeSession)}>
                        {isStarting ? "Uruchamianie..." : "Rozpocznij sesję"}
                    </button>
                    <button className="secondary" type="button" onClick={failActiveSession} disabled={!activeSession || isFailing}>
                        {isFailing ? "Przerywanie..." : "Przerwij aktywną sesję"}
                    </button>
                </div>
            </section>

            <section className="card-reader-status-grid" aria-label="Status czytnika">
                <article>
                    <span>Helper lokalny</span>
                    <strong>{helperHealth ? "Działa" : "Nie sprawdzono"}</strong>
                    <p>{helperHealth?.message ?? "Uruchom helper lokalnie i kliknij „Sprawdź helper”."}</p>
                </article>
                <article>
                    <span>PC/SC</span>
                    <strong>{helperHealth ? helperHealth.pcscAvailable ? "Dostępne" : "Niedostępne" : "Nie sprawdzono"}</strong>
                    <p>{helperHealth ? `Wykryte realne czytniki: ${helperHealth.readersCount}.` : "Status podsystemu pojawi się po sprawdzeniu helpera."}</p>
                </article>
                <article>
                    <span>Aktywna sesja</span>
                    <strong>{activeSession ? "W trakcie" : "Brak"}</strong>
                    <p>{activeSession ? `Czytnik: ${activeSession.readerName || "Brak danych"}` : "Testowy odczyt utworzy sesję automatycznie."}</p>
                </article>
                <article>
                    <span>Status czytnika</span>
                    <strong>{readerStatus?.readerConnected ? "Podłączony" : readerStatus?.isMockReader ? "Tryb testowy" : "Brak czytnika"}</strong>
                    <p>{readerStatus?.message ?? "Status będzie odświeżany po sprawdzeniu helpera lub czytników."}</p>
                </article>
                <article>
                    <span>Status karty</span>
                    <strong>{readerStatus?.cardPresent ? "Karta włożona" : "Brak karty"}</strong>
                    <p>{readerStatus?.checkedAtUtc ? `Ostatnie sprawdzenie: ${formatDate(readerStatus.checkedAtUtc)}` : "Brak ostatniego sprawdzenia."}</p>
                </article>
                <article>
                    <span>ATR</span>
                    <strong>{readerStatus?.atr ? `${readerStatus.atrLength} bajtów` : "Brak"}</strong>
                    <p>{readerStatus?.atr || "ATR pojawi się po włożeniu karty do realnego czytnika."}</p>
                </article>
            </section>

            <section className="card-reader-panel">
                <div className="card-reader-heading">
                    <div>
                        <span>Helper lokalny</span>
                        <h3>Połączenie z czytnikiem</h3>
                        <p>Te akcje komunikują się z lokalnym helperem pod adresem http://localhost:47888.</p>
                    </div>
                </div>

                <div className="card-reader-helper-actions">
                    <button type="button" onClick={checkHelper} disabled={isCheckingHelper}>
                        {isCheckingHelper ? "Sprawdzanie..." : "Sprawdź helper"}
                    </button>
                    <button type="button" onClick={checkReaders} disabled={isCheckingReaders}>
                        {isCheckingReaders ? "Sprawdzanie..." : "Sprawdź czytniki"}
                    </button>
                    <button type="button" onClick={checkDiagnostics} disabled={isCheckingDiagnostics}>
                        {isCheckingDiagnostics ? "Diagnozowanie..." : "Diagnostyka PC/SC"}
                    </button>
                    <button type="button" onClick={checkSelectedCardAtr} disabled={!selectedReaderName || isReadingAtr}>
                        {isReadingAtr ? "Odczyt ATR..." : "Sprawdź kartę / odczytaj ATR"}
                    </button>
                    <button type="button" onClick={runTechnicalRead} disabled={!selectedReaderName || isTechnicalReading}>
                        {isTechnicalReading ? "Odczyt karty..." : "Odczytaj kartę"}
                    </button>
                    <button type="button" onClick={runTachographStructureRead} disabled={!selectedReaderName || isStructureReading}>
                        {isStructureReading ? "Odczyt struktury..." : "Odczytaj strukturę karty"}
                    </button>
                    <button type="button" onClick={runMockRead} disabled={isMockReading}>
                        {isMockReading ? "Zapisywanie sesji..." : "Testowy odczyt karty"}
                    </button>
                </div>

                {helperError ? <p className="card-reader-error" role="alert">{helperError}</p> : null}

                <div className="card-reader-helper-results">
                    <article>
                        <span>Status helpera</span>
                        <StatusBadge label={helperHealth ? "Helper działa" : "Helper niedostępny"} tone={helperHealth ? "success" : "neutral"} />
                        <p>{helperHealth ? formatDate(helperHealth.checkedAtUtc) : "Brak ostatniego sprawdzenia."}</p>
                    </article>
                    <article>
                        <span>Czytniki</span>
                        <strong>{realReadersCount}</strong>
                        <p>{readers?.message ?? "Lista pojawi się po sprawdzeniu czytników."}</p>
                    </article>
                    <article>
                        <span>Wybrany czytnik</span>
                        <strong>{selectedReaderName || "Brak"}</strong>
                        <p>{selectedReader?.message ?? "Wybierz czytnik po pobraniu listy z helpera."}</p>
                    </article>
                </div>

                {readers ? (
                    <div className="card-reader-readers">
                        <div className="card-reader-subheading">
                            <h4>Wykryte czytniki</h4>
                            <p>Wybierz realny czytnik do testu ATR albo czytnik testowy, żeby sprawdzić UI bez urządzenia.</p>
                        </div>

                        {readerList.length === 0 ? (
                            <EmptyState title="Brak wykrytych czytników" description={readers.message} />
                        ) : (
                            <div className="card-reader-reader-list">
                                {readerList.map((reader) => (
                                    <label className={`card-reader-reader-option ${reader.name === selectedReaderName ? "selected" : ""}`} key={reader.name}>
                                        <input
                                            checked={reader.name === selectedReaderName}
                                            name="selectedReader"
                                            onChange={() => {
                                                setSelectedReaderName(reader.name);
                                                setAtrResult(null);
                                                setReaderStatus(null);
                                                void refreshReaderStatus(reader.name);
                                            }}
                                            type="radio"
                                            value={reader.name}
                                        />
                                        <span className="card-reader-reader-choice">
                                            <strong>{reader.name}</strong>
                                            <span>{reader.message}</span>
                                        </span>
                                        <span className="card-reader-reader-meta">
                                            <StatusBadge
                                                label={reader.isMock ? "Tryb testowy" : getReaderPresenceLabel(reader)}
                                                tone={reader.isMock ? "info" : getReaderPresenceTone(reader)}
                                            />
                                            <small>{reader.status}</small>
                                        </span>
                                    </label>
                                ))}
                            </div>
                        )}
                    </div>
                ) : null}

                {atrResult ? (
                    <div className="card-reader-atr-result">
                        <div><span>Czytnik</span><strong>{atrResult.readerName}</strong></div>
                        <div><span>Połączenie</span><strong>{atrResult.connected ? `Połączono (${atrResult.protocol || "protokół nieznany"})` : "Brak połączenia"}</strong></div>
                        <div><span>Karta</span><StatusBadge label={atrResult.cardPresent ? "Karta obecna" : "Brak karty"} tone={atrResult.cardPresent ? "success" : "warning"} /></div>
                        <div><span>ATR HEX</span><code>{atrResult.atrHex || "Brak danych ATR"}</code></div>
                        {atrResult.errorMessage ? <p>{atrResult.errorMessage} {atrResult.errorCodeHex ? `(${atrResult.errorCodeHex})` : ""}</p> : null}
                    </div>
                ) : null}

                {diagnostics ? (
                    <section className="card-reader-diagnostics">
                        <div className="card-reader-subheading">
                            <h4>Diagnostyka PC/SC</h4>
                            <p>{diagnostics.message}</p>
                        </div>
                        <div className="card-reader-diagnostic-grid">
                            <article><span>Status</span><strong>{diagnostics.status}</strong></article>
                            <article><span>PC/SC</span><strong>{diagnostics.pcscAvailable ? "Dostępne" : "Niedostępne"}</strong></article>
                            <article><span>Ostatnie sprawdzenie</span><strong>{formatDate(diagnostics.checkedAtUtc)}</strong></article>
                            <article><span>Ostatni błąd</span><strong>{diagnostics.lastError || "Brak"}</strong></article>
                        </div>
                        {diagnostics.readers.length === 0 ? (
                            <EmptyState title="Brak danych diagnostycznych czytników" description="Podłącz ACS ACR39U i ponów diagnostykę." />
                        ) : (
                            <div className="card-reader-diagnostic-list">
                                {diagnostics.readers.map((reader) => (
                                    <article key={reader.name}>
                                        <header>
                                            <strong>{reader.name}</strong>
                                            <StatusBadge label={getReaderPresenceLabel(reader)} tone={getReaderPresenceTone(reader)} />
                                        </header>
                                        <dl>
                                            <div><dt>Status</dt><dd>{reader.status}</dd></div>
                                            <div><dt>Połączenie</dt><dd>{reader.connected ? `Połączono (${reader.protocol || "protokół nieznany"})` : "Nie połączono"}</dd></div>
                                            <div><dt>ATR</dt><dd><code>{reader.atrHex || "Brak ATR"}</code></dd></div>
                                            <div><dt>Błąd PC/SC</dt><dd>{reader.errorMessage || "Brak"} {reader.errorCodeHex ? `(${reader.errorCodeHex})` : ""}</dd></div>
                                        </dl>
                                    </article>
                                ))}
                            </div>
                        )}
                    </section>
                ) : null}

                {technicalReadResult ? (
                    <div className="card-reader-read-result">
                        <div>
                            <span>Workflow odczytu karty</span>
                            <StatusBadge
                                label={technicalReadResult.success ? "Odczyt techniczny wykonany" : "Błąd odczytu"}
                                tone={technicalReadResult.success ? "success" : "danger"}
                            />
                        </div>
                        <p>{technicalReadResult.message}</p>

                        <div className="card-reader-workflow-grid">
                            <article className="card-reader-workflow-card">
                                <header>
                                    <span>Krok 1</span>
                                    <strong>Odczyt techniczny</strong>
                                </header>
                                <dl>
                                    <div><dt>Czytnik</dt><dd>{technicalReadResult.readerName || "Brak danych"}</dd></div>
                                    <div><dt>Żądany czytnik</dt><dd>{technicalReadResult.requestedReaderName || "Nie podano"}</dd></div>
                                    <div><dt>Wybrany czytnik</dt><dd>{technicalReadResult.selectedReaderName || "Brak danych"}{technicalReadResult.selectedReaderIsMock ? " (tryb testowy)" : ""}</dd></div>
                                    <div><dt>ATR</dt><dd><code>{technicalReadResult.atr || "Brak ATR"}</code></dd></div>
                                    <div><dt>Plik wynikowy</dt><dd>{technicalReadResult.outputFileName || "Nie utworzono"}</dd></div>
                                    <div><dt>Rozmiar</dt><dd>{formatFileSize(technicalReadResult.fileSizeBytes)}</dd></div>
                                    <div><dt>Ścieżka lokalna helpera</dt><dd>{technicalReadResult.outputPath || "Brak"}</dd></div>
                                </dl>
                            </article>

                            <article className="card-reader-workflow-card">
                                <header>
                                    <span>Krok 2</span>
                                    <strong>Eksport DDD/C1B</strong>
                                </header>
                                <dl>
                                    <div><dt>Format</dt><dd>{getExportFormatLabel(technicalReadResult.exportFormat)}</dd></div>
                                    <div>
                                        <dt>Status eksportu</dt>
                                        <dd>
                                            <StatusBadge
                                                label={technicalReadResult.isImportable ? "Importowalny" : "Nieimportowalny"}
                                                tone={technicalReadResult.isImportable ? "success" : "warning"}
                                            />
                                        </dd>
                                    </div>
                                    <div><dt>Następny krok</dt><dd>{technicalReadResult.nextStepMessage || "Pełny eksport C1B/DDD wymaga kolejnego kroku implementacji APDU."}</dd></div>
                                </dl>
                            </article>

                            <article className="card-reader-workflow-card card-reader-import-step">
                                <header>
                                    <span>Krok 3</span>
                                    <strong>Import do DriverTime</strong>
                                </header>
                                {technicalReadResult.isImportable ? (
                                    <p>Plik ma format {getExportFormatLabel(technicalReadResult.exportFormat)} i będzie można przekazać go do istniejącego importu DDD.</p>
                                ) : (
                                    <p className="card-reader-import-blocked">
                                        Ten plik jest technicznym wynikiem komunikacji z kartą. Nie jest jeszcze plikiem DDD/C1B i nie może być zaimportowany do ewidencji czasu pracy.
                                    </p>
                                )}
                                <button type="button" disabled>
                                    {technicalReadResult.isImportable ? "Import do DriverTime będzie dostępny w kolejnym kroku" : "Import niedostępny dla technicznego JSON-a"}
                                </button>
                            </article>
                        </div>

                        {technicalReadResult.apduResponses.length > 0 ? (
                            <div className="card-reader-apdu-list">
                                {technicalReadResult.apduResponses.map((response) => (
                                    <article key={`${response.name}-${response.commandHex}`}>
                                        <strong>{response.name}</strong>
                                        <span>SW: {response.statusWord || "brak"}</span>
                                        <code>{response.responseHex || response.message}</code>
                                    </article>
                                ))}
                            </div>
                        ) : null}
                        {technicalReadResult.errorDetails ? <p>{technicalReadResult.errorDetails}</p> : null}
                    </div>
                ) : null}

                {tachographStructureResult ? (
                    <div className="card-reader-read-result">
                        <div>
                            <span>Odczyt struktury karty</span>
                            <StatusBadge
                                label={tachographStructureResult.success ? "Struktura sprawdzona" : "Błąd odczytu struktury"}
                                tone={tachographStructureResult.success ? "success" : "danger"}
                            />
                        </div>
                        <p>{tachographStructureResult.message}</p>

                        <div className="card-reader-workflow-grid">
                            <article className="card-reader-workflow-card">
                                <header>
                                    <span>Komunikacja</span>
                                    <strong>ATR i aplikacja</strong>
                                </header>
                                <dl>
                                    <div><dt>Czytnik</dt><dd>{tachographStructureResult.readerName || "Brak danych"}</dd></div>
                                    <div><dt>Żądany czytnik</dt><dd>{tachographStructureResult.requestedReaderName || "Nie podano"}</dd></div>
                                    <div><dt>Wybrany czytnik</dt><dd>{tachographStructureResult.selectedReaderName || "Brak danych"}{tachographStructureResult.selectedReaderIsMock ? " (tryb testowy)" : ""}</dd></div>
                                    <div><dt>ATR</dt><dd><code>{tachographStructureResult.atr || "Brak ATR"}</code></dd></div>
                                    <div><dt>Plik wynikowy</dt><dd>{tachographStructureResult.outputFileName || "Nie utworzono"}</dd></div>
                                    <div><dt>Rozmiar</dt><dd>{formatFileSize(tachographStructureResult.fileSizeBytes)}</dd></div>
                                </dl>
                            </article>

                            <article className="card-reader-workflow-card">
                                <header>
                                    <span>Eksport</span>
                                    <strong>{getExportFormatLabel(tachographStructureResult.exportFormat)}</strong>
                                </header>
                                <p>{tachographStructureResult.nextStepMessage}</p>
                            </article>

                            <article className="card-reader-workflow-card card-reader-import-step">
                                <header>
                                    <span>Import</span>
                                    <strong>Import do DriverTime</strong>
                                </header>
                                <p className="card-reader-import-blocked">
                                    Ten wynik jest technicznym odczytem struktury karty. Nie jest jeszcze plikiem DDD/C1B i nie może być zaimportowany do ewidencji czasu pracy.
                                </p>
                                <button type="button" disabled>Import niedostępny</button>
                            </article>
                        </div>

                        {tachographStructureResult.apduResponses.length > 0 ? (
                            <div className="card-reader-apdu-list">
                                <h4>Próby SELECT aplikacji</h4>
                                {tachographStructureResult.apduResponses.map((response) => (
                                    <article key={`${response.name}-${response.commandHex}`}>
                                        <strong>{response.name}</strong>
                                        <StatusBadge label={`SW: ${response.statusWord || "brak"}`} tone={getApduTone(response.success)} />
                                        <span>{response.statusMeaning}</span>
                                        <code>{response.responseHex || response.message}</code>
                                    </article>
                                ))}
                            </div>
                        ) : null}

                        {tachographStructureResult.fileReads.length > 0 ? (
                            <div className="card-reader-file-read-list">
                                <h4>Próby plików EF</h4>
                                {tachographStructureResult.fileReads.map((file) => (
                                    <article key={`${file.fileName}-${file.fileId}`}>
                                        <header>
                                            <div>
                                                <strong>{file.fileName}</strong>
                                                <span>{file.description}</span>
                                            </div>
                                            <StatusBadge
                                                label={file.readSucceeded ? "Odczytano fragment" : file.selectSucceeded ? "Wybrano plik" : "Nie wybrano"}
                                                tone={file.readSucceeded ? "success" : file.selectSucceeded ? "info" : "warning"}
                                            />
                                        </header>
                                        <dl>
                                            <div><dt>File ID</dt><dd>{file.fileId}</dd></div>
                                            <div><dt>SELECT SW</dt><dd>{file.selectResponse.statusWord || "brak"} - {file.selectResponse.statusMeaning}</dd></div>
                                            <div><dt>READ SW</dt><dd>{file.readResponse ? `${file.readResponse.statusWord || "brak"} - ${file.readResponse.statusMeaning}` : "Nie wykonano"}</dd></div>
                                            <div><dt>Dane</dt><dd><code>{file.readResponse?.dataHex || "Brak danych"}</code></dd></div>
                                        </dl>
                                        <p>{file.message}</p>
                                    </article>
                                ))}
                            </div>
                        ) : null}

                        {tachographStructureResult.errorDetails ? <p>{tachographStructureResult.errorDetails}</p> : null}
                    </div>
                ) : null}

                {mockReadResult ? (
                    <div className="card-reader-mock-result">
                        <strong>{mockReadResult.fileName}</strong>
                        <span>Tryb: {mockReadResult.mockMode ? "testowy bez odczytu APDU" : "nieznany"}</span>
                        <span>Czytnik: {mockReadResult.selectedReaderName || "Nie wybrano"}</span>
                        <span>Start: {formatDate(mockReadResult.startedAtUtc)}</span>
                        <span>Koniec: {formatDate(mockReadResult.completedAtUtc)}</span>
                        <span>Ścieżka: {mockReadResult.filePath}</span>
                    </div>
                ) : null}
            </section>

            <section className="card-reader-panel">
                <div className="card-reader-heading">
                    <div>
                        <span>Historia</span>
                        <h3>Ostatnie sesje odczytu kart</h3>
                        <p>Historia pochodzi z backendu DriverTime i tabeli CardReadSessions.</p>
                    </div>
                    <button type="button" onClick={() => void loadSessions()} disabled={isLoading}>Odśwież</button>
                </div>

                {error ? <p className="card-reader-error" role="alert">{error}</p> : null}

                {isLoading ? (
                    <TableSkeleton rows={5} columns={5} />
                ) : sessions.length === 0 ? (
                    <EmptyState title="Brak sesji odczytu" description="Rozpocznij testowy odczyt, aby zapisać pierwszą sesję w historii." />
                ) : (
                    <div className="card-reader-table-wrapper">
                        <table className="card-reader-table">
                            <thead>
                                <tr><th>Status</th><th>Czytnik</th><th>Start</th><th>Zakończenie</th><th>Uwagi</th></tr>
                            </thead>
                            <tbody>
                                {sessions.map((session) => (
                                    <tr key={session.id}>
                                        <td><StatusBadge label={getStatusLabel(session.status)} tone={getStatusTone(session.status)} /></td>
                                        <td>{session.readerName || "Brak"}</td>
                                        <td>{formatDate(session.startedAtUtc)}</td>
                                        <td>{formatDate(session.completedAtUtc ?? session.failedAtUtc)}</td>
                                        <td>{session.errorMessage || session.notes || "Brak"}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </section>
        </div>
    );
}
