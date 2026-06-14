import { useEffect, useState } from "react";
import { getDddImports, uploadDddFile } from "../services/dddService";
import type { DddFileListItemDto } from "../models/dddModels";

interface ImportsPageProps {
    onImportSelected: (dddFileId: string) => void;
}

export function ImportsPage(props: ImportsPageProps) {

    const [imports, setImports] =
        useState<DddFileListItemDto[]>([]);

    const [selectedFile, setSelectedFile] =
        useState<File | null>(null);

    const [isLoading, setIsLoading] =
        useState(false);

    const [message, setMessage] =
        useState("");

    async function loadImports() {

        setIsLoading(true);
        setMessage("");

        try {

            const data =
                await getDddImports();

            setImports(data);

        } catch (error) {

            setMessage(
                error instanceof Error
                    ? error.message
                    : "Nie udało się pobrać listy importów.");
        }
        finally {

            setIsLoading(false);
        }
    }

    async function handleUpload() {

        if (!selectedFile) {

            setMessage(
                "Wybierz plik .ddd przed importem.");

            return;
        }

        setIsLoading(true);
        setMessage("");

        try {

            await uploadDddFile(selectedFile);

            setSelectedFile(null);

            setMessage(
                "Plik DDD został zaimportowany.");

            await loadImports();

        } catch (error) {

            setMessage(
                error instanceof Error
                    ? error.message
                    : "Nie udało się zaimportować pliku DDD.");
        }
        finally {

            setIsLoading(false);
        }
    }

    useEffect(() => {

        const loadData = async () => {

            await loadImports();
        };

        void loadData();

    }, []);

    return (

        <section className="page-section">

            <h2>
                Importy DDD
            </h2>

            <div className="card">

                <h3>
                    Dodaj plik DDD
                </h3>

                <input
                    type="file"
                    accept=".ddd"
                    onChange={(event) =>
                        setSelectedFile(
                            event.target.files?.[0] ?? null)
                    }
                />

                <button
                    onClick={handleUpload}
                    disabled={isLoading}>

                    Importuj plik

                </button>

                {message && (
                    <p className="message">
                        {message}
                    </p>
                )}

            </div>

            <div className="card">

                <h3>
                    Lista importów
                </h3>

                {isLoading && (
                    <p>
                        Ładowanie danych...
                    </p>
                )}

                {!isLoading && imports.length === 0 && (
                    <p>
                        Brak zaimportowanych plików DDD.
                    </p>
                )}

                {imports.length > 0 && (

                    <table>

                        <thead>

                            <tr>
                                <th>Plik</th>
                                <th>Kierowca</th>
                                <th>Karta</th>
                                <th>Data importu</th>
                                <th>Aktywności</th>
                                <th>Akcja</th>
                            </tr>

                        </thead>

                        <tbody>

                            {imports.map((item) => (

                                <tr key={item.id}>

                                    <td>
                                        {item.fileName}
                                    </td>

                                    <td>
                                        {item.driverName || "-"}
                                    </td>

                                    <td>
                                        {item.driverCardNumber || "-"}
                                    </td>

                                    <td>
                                        {item.importedAt || "-"}
                                    </td>

                                    <td>
                                        {item.activitiesCount}
                                    </td>

                                    <td>

                                        <button
                                            onClick={() =>
                                                props.onImportSelected(
                                                    item.id)
                                            }>

                                            Otwórz

                                        </button>

                                    </td>

                                </tr>

                            ))}

                        </tbody>

                    </table>

                )}

            </div>

        </section>
    );
}