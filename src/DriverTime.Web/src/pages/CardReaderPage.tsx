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
    readCardAtr,
    startMockCardRead,
    type CardReaderAtrResult,
    type CardReaderDiagnostics,
    type CardReaderHelperHealth,
    type CardReaderMockReadResult,
    type CardReaderReader,
    type CardReaderReaderList,
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
    const [mockReadResult, setMockReadResult] = useState<CardReaderMockReadResult | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isStarting, setIsStarting] = useState(false);
    const [isFailing, setIsFailing] = useState(false);
    const [isCheckingHelper, setIsCheckingHelper] = useState(false);
    const [isCheckingReaders, setIsCheckingReaders] = useState(false);
    const [isCheckingDiagnostics, setIsCheckingDiagnostics] = useState(false);
    const [isReadingAtr, setIsReadingAtr] = useState(false);
    const [isMockReading, setIsMockReading] = useState(false);
    const [error, setError] = useState("");
    const [helperError, setHelperError] = useState("");

    const activeSession = useMemo(
        () => sessions.find((session) => session.status === "Started") ?? null,
        [sessions],
    );

    const readerList = readers?.readers ?? [];

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
                    <p>{helperHealth ? `Wykryte czytniki: ${helperHealth.readersCount}.` : "Status podsystemu pojawi się po sprawdzeniu helpera."}</p>
                </article>
                <article>
                    <span>Aktywna sesja</span>
                    <strong>{activeSession ? "W trakcie" : "Brak"}</strong>
                    <p>{activeSession ? `Czytnik: ${activeSession.readerName || "Brak danych"}` : "Testowy odczyt utworzy sesję automatycznie."}</p>
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
                        <strong>{readerList.length}</strong>
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
                            <p>Wybierz czytnik, a następnie sprawdź obecność karty przez odczyt ATR.</p>
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
                                            }}
                                            type="radio"
                                            value={reader.name}
                                        />
                                        <span className="card-reader-reader-choice">
                                            <strong>{reader.name}</strong>
                                            <span>{reader.message}</span>
                                        </span>
                                        <span className="card-reader-reader-meta">
                                            <StatusBadge label={getReaderPresenceLabel(reader)} tone={getReaderPresenceTone(reader)} />
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

                {mockReadResult ? (
                    <div className="card-reader-mock-result">
                        <strong>{mockReadResult.fileName}</strong>
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
