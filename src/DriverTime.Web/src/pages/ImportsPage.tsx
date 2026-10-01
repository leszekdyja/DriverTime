import {
    useCallback,
    useEffect,
    useState,
} from "react";
import { Link } from "react-router-dom";

import DddDropzone from "../components/DddDropzone";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    deleteDddImport,
    getDddImports,
    type DddImport,
} from "../services/dddImportsService";
import "../styles/imports.css";

const uploadDateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function displayValue(value: string) {
    return value || "Brak danych";
}

function formatUploadDate(value: string) {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : uploadDateFormatter.format(date);
}

export default function ImportsPage() {
    const [imports, setImports] = useState<DddImport[]>([]);
    const [isLoadingImports, setIsLoadingImports] = useState(true);
    const [importsError, setImportsError] = useState("");
    const [deletingImportId, setDeletingImportId] = useState<string | null>(null);
    const [deleteMessage, setDeleteMessage] = useState("");

    const loadImports = useCallback(async () => {
        setIsLoadingImports(true);
        setImportsError("");

        try {
            setImports(await getDddImports());
        } catch (loadError) {
            setImportsError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania importów.",
            );
        } finally {
            setIsLoadingImports(false);
        }
    }, []);

    useEffect(() => {
        void loadImports();
    }, [loadImports]);

    async function handleDelete(dddImport: DddImport) {
        const confirmed = window.confirm(
            `Czy na pewno usunąć import "${dddImport.fileName}"? Powi?zane aktywności, pojazdy i wpisy krajów zostaną usunięte.`,
        );

        if (!confirmed) return;

        setDeletingImportId(dddImport.id);
        setImportsError("");
        setDeleteMessage("");

        try {
            await deleteDddImport(dddImport.id);
            await loadImports();
            setDeleteMessage(`Usunięto import "${dddImport.fileName}".`);
            window.dispatchEvent(new Event("drivertime:data-changed"));
        } catch (deleteError) {
            setImportsError(
                deleteError instanceof Error
                    ? deleteError.message
                    : "Wystąpił błąd podczas usuwania importu.",
            );
        } finally {
            setDeletingImportId(null);
        }
    }

    return (
        <div className="imports-page">
            <h2>Importy DDD</h2>
            <p>prześlij pliki z kart kierowców lub tachografów.</p>

            <DddDropzone onImportsChanged={loadImports} />

            <section className="imports-list">
                <div className="imports-list-heading">
                    <div>
                        <h3>Historia importów</h3>
                        <p>Zaimportowane pliki DDD uporządkowane od najnowszych.</p>
                    </div>
                    {!isLoadingImports && !importsError && (
                        <span>{imports.length} importów</span>
                    )}
                </div>

                {isLoadingImports && imports.length === 0 && <TableSkeleton rows={5} columns={7} />}

                {importsError && (
                    <p className="message error-message" role="alert">
                        {importsError}
                    </p>
                )}

                {deleteMessage && (
                    <p className="message success-message" role="status">
                        {deleteMessage}
                    </p>
                )}

                {!isLoadingImports && !importsError && imports.length === 0 && (
                    <EmptyState
                        title="Brak importów DDD"
                        description="Przeciągnij pliki DDD do pola powyżej, aby rozpocząć analizę danych kierowców."
                    />
                )}

                {!importsError && imports.length > 0 && (
                    <div className={isLoadingImports ? "imports-table-wrapper is-refreshing" : "imports-table-wrapper"} aria-busy={isLoadingImports}>
                        <table className="imports-table">
                            <thead>
                                <tr>
                                    <th>Nazwa pliku</th>
                                    <th>Kierowca</th>
                                    <th>Numer karty</th>
                                    <th>Data importu</th>
                                    <th>Aktywności</th>
                                    <th>Status kierowcy</th>
                                    <th>Akcje</th>
                                </tr>
                            </thead>
                            <tbody>
                                {imports.map((dddImport) => (
                                    <tr key={dddImport.id}>
                                        <td>{displayValue(dddImport.fileName)}</td>
                                        <td>
                                            {displayValue(
                                                [
                                                    dddImport.driverFirstName,
                                                    dddImport.driverLastName,
                                                ]
                                                    .filter(Boolean)
                                                    .join(" "),
                                            )}
                                        </td>
                                        <td>{displayValue(dddImport.driverCardNumber)}</td>
                                        <td>{formatUploadDate(dddImport.uploadedAtUtc)}</td>
                                        <td>{dddImport.activitiesCount}</td>
                                        <td>
                                            <span className={`driver-import-status ${dddImport.driverStatus}`}>
                                                {dddImport.driverStatus === "new"
                                                    ? "Nowy kierowca"
                                                    : "Istniejący kierowca"}
                                            </span>
                                        </td>
                                        <td>
                                            <div className="imports-actions">
                                                <Link
                                                    className="details-button"
                                                    to={`/imports/${dddImport.id}`}
                                                >
                                                    Szczegóły
                                                </Link>
                                                <button
                                                    className="delete-import-button"
                                                    type="button"
                                                    onClick={() => void handleDelete(dddImport)}
                                                    disabled={deletingImportId !== null}
                                                >
                                                    {deletingImportId === dddImport.id
                                                        ? "Usuwanie..."
                                                        : "Usuń"}
                                                </button>
                                            </div>
                                        </td>
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
