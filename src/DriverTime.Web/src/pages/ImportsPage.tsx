import {
    useCallback,
    useEffect,
    useState,
    type ChangeEvent,
    type FormEvent,
} from "react";
import { Link } from "react-router-dom";

import {
    uploadDddFile,
    type DddUploadResult,
} from "../services/dddUploadService";
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
    const [selectedFile, setSelectedFile] = useState<File | null>(null);
    const [isUploading, setIsUploading] = useState(false);
    const [error, setError] = useState("");
    const [result, setResult] = useState<DddUploadResult | null>(null);
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

    function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
        const file = event.target.files?.[0] ?? null;

        setError("");
        setResult(null);

        if (file && !file.name.toLowerCase().endsWith(".ddd")) {
            setSelectedFile(null);
            setError("Wybierz plik z rozszerzeniem .ddd.");
            event.target.value = "";
            return;
        }

        setSelectedFile(file);
    }

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (!selectedFile) {
            setError("Najpierw wybierz plik DDD.");
            return;
        }

        setIsUploading(true);
        setError("");
        setResult(null);

        try {
            setResult(await uploadDddFile(selectedFile));
            await loadImports();
        } catch (uploadError) {
            setError(
                uploadError instanceof Error
                    ? uploadError.message
                    : "Wystapil blad podczas przesylania pliku.",
            );
        } finally {
            setIsUploading(false);
        }
    }

    return (
        <div className="imports-page">
            <h2>Importy DDD</h2>
            <p>Przeslij plik z karty kierowcy lub tachografu.</p>

            <form className="upload-form" onSubmit={handleSubmit}>
                <label htmlFor="ddd-file">Plik DDD</label>
                <input
                    id="ddd-file"
                    type="file"
                    accept=".ddd"
                    onChange={handleFileChange}
                    disabled={isUploading}
                />

                {selectedFile && (
                    <p className="selected-file">Wybrano: {selectedFile.name}</p>
                )}

                <button type="submit" disabled={isUploading || !selectedFile}>
                    {isUploading ? "Przesylanie..." : "Przeslij plik"}
                </button>
            </form>

            {isUploading && (
                <p className="status-message" role="status">
                    Plik jest przesylany i analizowany...
                </p>
            )}

            {error && (
                <p className="message error-message" role="alert">
                    {error}
                </p>
            )}

            {result && (
                <section className="message success-message" aria-live="polite">
                    <h3>Import zakonczony pomyslnie</h3>
                    <dl className="result-details">
                        <div>
                            <dt>Typ pliku</dt>
                            <dd>{result.file_type || "Brak danych"}</dd>
                        </div>
                        <div>
                            <dt>Data odczytu karty</dt>
                            <dd>{result.card_read_date || "Brak danych"}</dd>
                        </div>
                        <div>
                            <dt>Aktywnosci</dt>
                            <dd>{result.activities.length}</dd>
                        </div>
                        <div>
                            <dt>Pojazdy</dt>
                            <dd>{result.vehicle_uses.length}</dd>
                        </div>
                        <div>
                            <dt>Wpisy krajow</dt>
                            <dd>{result.country_code_entries.length}</dd>
                        </div>
                    </dl>
                </section>
            )}

            <section className="imports-list">
                <h3>Lista importow</h3>

                {isLoadingImports && (
                    <p className="status-message" role="status">
                        Ladowanie importow...
                    </p>
                )}

                {importsError && (
                    <p className="message error-message" role="alert">
                        {importsError}
                    </p>
                )}

                {!isLoadingImports && !importsError && imports.length === 0 && (
                    <p>Brak zaimportowanych plikow DDD.</p>
                )}

                {!isLoadingImports && !importsError && imports.length > 0 && (
                    <div className="imports-table-wrapper">
                        <table className="imports-table">
                            <thead>
                                <tr>
                                    <th>Nazwa pliku</th>
                                    <th>Imie kierowcy</th>
                                    <th>Nazwisko kierowcy</th>
                                    <th>Numer karty</th>
                                    <th>Data importu</th>
                                </tr>
                            </thead>
                            <tbody>
                                {imports.map((dddImport) => (
                                    <tr key={dddImport.id}>
                                        <td>
                                            <Link
                                                className="details-link"
                                                to={`/imports/${dddImport.id}`}
                                            >
                                                {displayValue(dddImport.fileName)}
                                            </Link>
                                        </td>
                                        <td>{displayValue(dddImport.driverFirstName)}</td>
                                        <td>{displayValue(dddImport.driverLastName)}</td>
                                        <td>{displayValue(dddImport.driverCardNumber)}</td>
                                        <td>{formatUploadDate(dddImport.uploadedAtUtc)}</td>
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
