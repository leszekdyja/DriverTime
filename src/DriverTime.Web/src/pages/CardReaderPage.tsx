import { useCallback, useEffect, useMemo, useState } from "react";

import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    failCardReadSession,
    getCardReadSessions,
    startCardReadSession,
    type CardReadSession,
} from "../services/cardReaderService";
import {
    checkCardReaderHelperHealth,
    getCardReaderReaders,
    startMockCardRead,
    type CardReaderHelperHealth,
    type CardReaderMockReadResult,
    type CardReaderReaderList,
} from "../services/cardReaderHelperService";
import "../styles/card-reader.css";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) {
        return "Brak danych";
    }

    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateFormatter.format(date);
}

function getStatusLabel(status: string) {
    if (status === "Started") return "W trakcie";
    if (status === "Completed") return "Zakończony";
    if (status === "Failed") return "Błąd";
    return status || "Brak danych";
}

function getStatusTone(status: string) {
    if (status === "Started") return "info";
    if (status === "Completed") return "success";
    if (status === "Failed") return "danger";
    return "neutral";
}

export default function CardReaderPage() {
    const [sessions, setSessions] = useState<CardReadSession[]>([]);
    const [helperHealth, setHelperHealth] = useState<CardReaderHelperHealth | null>(null);
    const [readers, setReaders] = useState<CardReaderReaderList | null>(null);
    const [mockReadResult, setMockReadResult] = useState<CardReaderMockReadResult | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isStarting, setIsStarting] = useState(false);
    const [isFailing, setIsFailing] = useState(false);
    const [isCheckingHelper, setIsCheckingHelper] = useState(false);
    const [isCheckingReaders, setIsCheckingReaders] = useState(false);
    const [isMockReading, setIsMockReading] = useState(false);
    const [error, setError] = useState("");
    const [helperError, setHelperError] = useState("");

    const activeSession = useMemo(
        () => sessions.find((session) => session.status === "Started") ?? null,
        [sessions],
    );

    const loadSessions = useCallback(async () => {
        setIsLoading(true);
        setError("");

        try {
            setSessions(await getCardReadSessions());
        } catch (loadError) {
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania sesji odczytu kart.",
            );
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
                readerName: "Czytnik karty kierowcy",
                notes: "Sesja przygotowana pod przyszły helper PC/SC.",
            });

            setSessions((current) => [session, ...current.filter((item) => item.id !== session.id)]);
        } catch (startError) {
            setError(
                startError instanceof Error
                    ? startError.message
                    : "Nie udało się rozpocząć sesji odczytu karty.",
            );
        } finally {
            setIsStarting(false);
        }
    }

    async function failActiveSession() {
        if (!activeSession) {
            return;
        }

        setIsFailing(true);
        setError("");

        try {
            const session = await failCardReadSession(activeSession.id, {
                errorMessage: "Sesja przerwana przed podłączeniem fizycznego helpera.",
            });

            setSessions((current) =>
                current.map((item) => item.id === session.id ? session : item),
            );
        } catch (failError) {
            setError(
                failError instanceof Error
                    ? failError.message
                    : "Nie udało się przerwać sesji odczytu karty.",
            );
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
            setHelperError(
                checkError instanceof Error
                    ? checkError.message
                    : "Helper odczytu karty jest niedostępny.",
            );
        } finally {
            setIsCheckingHelper(false);
        }
    }

    async function checkReaders() {
        setIsCheckingReaders(true);
        setHelperError("");

        try {
            setReaders(await getCardReaderReaders());
        } catch (checkError) {
            setReaders(null);
            setHelperError(
                checkError instanceof Error
                    ? checkError.message
                    : "Nie udało się sprawdzić czytników.",
            );
        } finally {
            setIsCheckingReaders(false);
        }
    }

    async function runMockRead() {
        setIsMockReading(true);
        setHelperError("");

        try {
            setMockReadResult(await startMockCardRead());
        } catch (readError) {
            setMockReadResult(null);
            setHelperError(
                readError instanceof Error
                    ? readError.message
                    : "Nie udało się wykonać testowego odczytu karty.",
            );
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
                        To pierwszy krok modułu czytnika kart kierowców. Aplikacja śledzi
                        sesje odczytu, a lokalny helper działa w trybie bezpiecznym/mock.
                        Realny odczyt PC/SC zostanie dodany w kolejnym etapie.
                    </p>
                </div>
                <div className="card-reader-actions">
                    <button type="button" onClick={startSession} disabled={isStarting}>
                        {isStarting ? "Uruchamianie..." : "Rozpocznij sesję"}
                    </button>
                    <button
                        className="secondary"
                        type="button"
                        onClick={failActiveSession}
                        disabled={!activeSession || isFailing}
                    >
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
                    <span>Tryb odczytu</span>
                    <strong>Mock</strong>
                    <p>Bezpieczny tryb MVP nie odpytuje jeszcze fizycznej karty ani PC/SC.</p>
                </article>
                <article>
                    <span>Aktywna sesja</span>
                    <strong>{activeSession ? "Tak" : "Nie"}</strong>
                    <p>{activeSession ? formatDate(activeSession.startedAtUtc) : "Brak aktywnej sesji."}</p>
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
                    <button type="button" onClick={runMockRead} disabled={isMockReading}>
                        {isMockReading ? "Odczyt..." : "Testowy odczyt karty"}
                    </button>
                </div>

                {helperError ? (
                    <p className="card-reader-error" role="alert">
                        {helperError}
                    </p>
                ) : null}

                <div className="card-reader-helper-results">
                    <article>
                        <span>Status helpera</span>
                        <StatusBadge
                            label={helperHealth ? "Helper działa" : "Helper niedostępny"}
                            tone={helperHealth ? "success" : "neutral"}
                        />
                        <p>{helperHealth ? formatDate(helperHealth.checkedAtUtc) : "Brak ostatniego sprawdzenia."}</p>
                    </article>
                    <article>
                        <span>Czytniki</span>
                        <strong>{readers ? readers.readers.length : 0}</strong>
                        <p>{readers?.message ?? "Lista pojawi się po sprawdzeniu helpera."}</p>
                    </article>
                    <article>
                        <span>Wynik testowego odczytu</span>
                        <strong>{mockReadResult?.status ?? "Brak"}</strong>
                        <p>{mockReadResult?.message ?? "Testowy wynik pojawi się po uruchomieniu odczytu."}</p>
                    </article>
                </div>

                {mockReadResult ? (
                    <div className="card-reader-mock-result">
                        <strong>{mockReadResult.fileName}</strong>
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
                        <p>Lista pokazuje sesje trackingowe, bez zmiany obecnego importu plików DDD.</p>
                    </div>
                    <button type="button" onClick={() => void loadSessions()} disabled={isLoading}>
                        Odśwież
                    </button>
                </div>

                {error ? (
                    <p className="card-reader-error" role="alert">
                        {error}
                    </p>
                ) : null}

                {isLoading ? (
                    <TableSkeleton rows={5} columns={5} />
                ) : sessions.length === 0 ? (
                    <EmptyState
                        title="Brak sesji odczytu"
                        description="Rozpocznij pierwszą sesję, aby zobaczyć ją w historii."
                    />
                ) : (
                    <div className="card-reader-table-wrapper">
                        <table className="card-reader-table">
                            <thead>
                                <tr>
                                    <th>Status</th>
                                    <th>Start</th>
                                    <th>Zakończenie</th>
                                    <th>Numer karty</th>
                                    <th>Uwagi</th>
                                </tr>
                            </thead>
                            <tbody>
                                {sessions.map((session) => (
                                    <tr key={session.id}>
                                        <td>
                                            <StatusBadge
                                                label={getStatusLabel(session.status)}
                                                tone={getStatusTone(session.status)}
                                            />
                                        </td>
                                        <td>{formatDate(session.startedAtUtc)}</td>
                                        <td>{formatDate(session.completedAtUtc ?? session.failedAtUtc)}</td>
                                        <td>{session.driverCardNumber || "Brak"}</td>
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
