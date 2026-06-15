import { useState, type ChangeEvent, type FormEvent } from "react";

import {
    uploadDddFile,
    type DddUploadResult,
} from "../services/dddUploadService";
import "../styles/imports.css";

export default function ImportsPage() {
    const [selectedFile, setSelectedFile] = useState<File | null>(null);
    const [isUploading, setIsUploading] = useState(false);
    const [error, setError] = useState("");
    const [result, setResult] = useState<DddUploadResult | null>(null);

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
        </div>
    );
}
