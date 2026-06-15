import {
    useCallback,
    useEffect,
    useState,
} from "react";
import { Link } from "react-router-dom";

import DddDropzone from "../components/DddDropzone";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
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

    const loadImports = useCallback(async () => {
        setIsLoadingImports(true);
        setImportsError("");

        try {
            setImports(await getDddImports());
        } catch (loadError) {
            setImportsError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystapil blad podczas pobierania importow.",
            );
        } finally {
            setIsLoadingImports(false);
        }
    }, []);

    useEffect(() => {
        void loadImports();
    }, [loadImports]);

    return (
        <div className="imports-page">
            <h2>Importy DDD</h2>
            <p>Przeslij pliki z kart kierowcow lub tachografow.</p>

            <DddDropzone onImportsChanged={loadImports} />

            <section className="imports-list">
                <h3>Lista importow</h3>

                {isLoadingImports && imports.length === 0 && <TableSkeleton rows={5} columns={7} />}

                {importsError && (
                    <p className="message error-message" role="alert">
                        {importsError}
                    </p>
                )}

                {!isLoadingImports && !importsError && imports.length === 0 && (
                    <EmptyState
                        title="Brak importow DDD"
                        description="Przeciagnij pliki DDD do pola powyzej, aby rozpoczac analize danych kierowcow."
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
                                    <th>Aktywnosci</th>
                                    <th>Status kierowcy</th>
                                    <th aria-label="Akcje" />
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
                                                    : "Istniejacy kierowca"}
                                            </span>
                                        </td>
                                        <td>
                                            <Link
                                                className="details-button"
                                                to={`/imports/${dddImport.id}`}
                                            >
                                                Szczegoly
                                            </Link>
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
