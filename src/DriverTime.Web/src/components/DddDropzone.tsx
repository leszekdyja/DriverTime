import {
    useRef,
    useState,
    type ChangeEvent,
    type DragEvent,
} from "react";

import {
    DddUploadError,
    uploadDddFile,
    type DddUploadResult,
} from "../services/dddUploadService";

type UploadStatus = "ready" | "uploading" | "success" | "error" | "unsupported" | "duplicate";

type UploadItem = {
    id: string;
    file: File;
    progress: number;
    status: UploadStatus;
    message: string;
    result?: DddUploadResult;
};

type DddDropzoneProps = {
    onImportsChanged: () => Promise<void>;
};

function createFileId(file: File) {
    return `${file.name}-${file.size}-${file.lastModified}`;
}

function getStatusLabel(item: UploadItem) {
    switch (item.status) {
        case "ready":
            return "Gotowy do wysłania";
        case "uploading":
            return `Przesyłanie ${item.progress}%`;
        case "success":
            return "Import zakończony pomyślnie";
        case "unsupported":
            return "Nieobsługiwany plik. Wybierz plik .ddd";
        case "error":
            return item.message || "Import nie powiódł się";
        case "duplicate":
            return item.message || "Plik został już wcześniej zaimportowany";
    }
}

export default function DddDropzone({ onImportsChanged }: DddDropzoneProps) {
    const inputRef = useRef<HTMLInputElement>(null);
    const [items, setItems] = useState<UploadItem[]>([]);
    const [isDragging, setIsDragging] = useState(false);
    const [isUploading, setIsUploading] = useState(false);

    function addFiles(files: File[]) {
        setItems((currentItems) => {
            const existingIds = new Set(currentItems.map((item) => item.id));
            const newItems = files
                .filter((file) => !existingIds.has(createFileId(file)))
                .map<UploadItem>((file) => {
                    const isSupported = file.name.toLowerCase().endsWith(".ddd");

                    return {
                        id: createFileId(file),
                        file,
                        progress: 0,
                        status: isSupported ? "ready" : "unsupported",
                        message: "",
                    };
                });

            return [...currentItems, ...newItems];
        });
    }

    function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
        addFiles(Array.from(event.target.files ?? []));
        event.target.value = "";
    }

    function handleDrop(event: DragEvent<HTMLDivElement>) {
        event.preventDefault();
        setIsDragging(false);
        addFiles(Array.from(event.dataTransfer.files));
    }

    function updateItem(id: string, changes: Partial<UploadItem>) {
        setItems((currentItems) =>
            currentItems.map((item) =>
                item.id === id ? { ...item, ...changes } : item,
            ),
        );
    }

    async function uploadFiles() {
        const filesToUpload = items.filter(
            (item) => item.status === "ready" || item.status === "error",
        );

        if (filesToUpload.length === 0) {
            return;
        }

        setIsUploading(true);
        let hasSuccessfulImport = false;

        for (const item of filesToUpload) {
            updateItem(item.id, {
                status: "uploading",
                progress: 0,
                message: "",
            });

            try {
                const result = await uploadDddFile(item.file, (progress) => {
                    updateItem(item.id, { progress });
                });

                updateItem(item.id, {
                    status: "success",
                    progress: 100,
                    message: result.importMessage,
                    result,
                });
                hasSuccessfulImport = true;
                window.dispatchEvent(new Event("drivertime:data-changed"));
            } catch (uploadError) {
                const isDuplicate = uploadError instanceof DddUploadError && uploadError.status === 409;
                updateItem(item.id, {
                    status: isDuplicate ? "duplicate" : "error",
                    progress: isDuplicate ? 100 : item.progress,
                    message:
                        uploadError instanceof Error
                            ? uploadError.message
                            : "Import nie powiódł się.",
                });
            }
        }

        if (hasSuccessfulImport) {
            await onImportsChanged();
        }

        setIsUploading(false);
    }

    const uploadableItems = items.filter(
        (item) => item.status === "ready" || item.status === "error",
    );

    return (
        <section className="ddd-upload-card">
            <div
                className={`ddd-dropzone${isDragging ? " is-dragging" : ""}`}
                onDragEnter={(event) => {
                    event.preventDefault();
                    setIsDragging(true);
                }}
                onDragOver={(event) => event.preventDefault()}
                onDragLeave={(event) => {
                    if (!event.currentTarget.contains(event.relatedTarget as Node)) {
                        setIsDragging(false);
                    }
                }}
                onDrop={handleDrop}
            >
                <input
                    ref={inputRef}
                    type="file"
                    accept=".ddd"
                    multiple
                    onChange={handleFileChange}
                    disabled={isUploading}
                    hidden
                />
                <strong>Przeciągnij pliki DDD tutaj</strong>
                <span>lub wybierz jeden albo kilka plików z dysku</span>
                <button
                    type="button"
                    className="select-files-button"
                    onClick={() => inputRef.current?.click()}
                    disabled={isUploading}
                >
                    Wybierz pliki
                </button>
            </div>

            {items.length > 0 && (
                <div className="upload-queue" aria-live="polite">
                    {items.map((item) => (
                        <article className={`upload-item ${item.status}`} key={item.id}>
                            <div className="upload-item-heading">
                                <div>
                                    <strong>{item.file.name}</strong>
                                    <span>{getStatusLabel(item)}</span>
                                </div>
                                {!isUploading && item.status !== "success" && item.status !== "duplicate" && (
                                    <button
                                        type="button"
                                        className="remove-file-button"
                                        onClick={() =>
                                            setItems((currentItems) =>
                                                currentItems.filter(
                                                    (currentItem) => currentItem.id !== item.id,
                                                ),
                                            )
                                        }
                                    >
                                        Usuń
                                    </button>
                                )}
                            </div>

                            {item.status !== "unsupported" && (
                                <div
                                    className="upload-progress"
                                    role="progressbar"
                                    aria-label={`Postęp wysyłania ${item.file.name}`}
                                    aria-valuemin={0}
                                    aria-valuemax={100}
                                    aria-valuenow={item.progress}
                                >
                                    <span style={{ width: `${item.progress}%` }} />
                                </div>
                            )}

                            {item.result && (
                                <p>
                                    <strong>{item.result.importMessage}</strong>{" "}
                                    Aktywności: {item.result.activities.length}, pojazdy:{" "}
                                    {item.result.vehicle_uses.length}, kraje:{" "}
                                    {item.result.country_code_entries.length}
                                </p>
                            )}
                        </article>
                    ))}
                </div>
            )}

            <button
                type="button"
                className="upload-files-button"
                onClick={() => void uploadFiles()}
                disabled={isUploading || uploadableItems.length === 0}
            >
                {isUploading
                    ? "Trwa importowanie..."
                    : `Importuj pliki (${uploadableItems.length})`}
            </button>
        </section>
    );
}
