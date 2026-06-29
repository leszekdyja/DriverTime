import { useEffect, useState, type FormEvent, type ReactNode } from "react";

import PlanningSchedulesTab from "../components/planning/PlanningSchedulesTab";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    confirmPlanningDutiesPdfImport,
    createPlanningDuty,
    deletePlanningDuty,
    getPlanningDuty,
    getPlanningDuties,
    previewPlanningDutiesPdf,
    updatePlanningDuty,
    type PlanningDuty,
    type PlanningDutyDetails,
    type PlanningDutyPayload,
    type PlanningDutyPdfImportConfirmResult,
    type PlanningDutyPdfImportConfidence,
    type PlanningDutyPdfImportPreview,
    type PlanningDutyPdfImportPreviewItem,
} from "../services/planningDutiesService";
import "../styles/drivers.css";
import "../styles/planning.css";

type PlanningDutyFormState = {
    dutyNumber: string;
    name: string;
    validFrom: string;
    vehicleRequirement: string;
    startTime: string;
    endTime: string;
    totalDurationMinutes: string;
    workMinutes: string;
    breakMinutes: string;
    drivingMinutes: string;
    distanceKm: string;
    notes: string;
};

type ImportDraft = {
    dutyNumber: string;
    name: string;
    line: string;
    validFrom: string;
    vehicleRequirement: string;
    startTime: string;
    endTime: string;
    workMinutes: string;
    drivingMinutes: string;
    breakMinutes: string;
    distanceKm: string;
    notes: string;
    stops: PlanningDutyPdfImportPreviewItem["stops"];
    confidence: PlanningDutyPdfImportConfidence;
};

const emptyForm: PlanningDutyFormState = {
    dutyNumber: "",
    name: "",
    validFrom: "",
    vehicleRequirement: "",
    startTime: "",
    endTime: "",
    totalDurationMinutes: "",
    workMinutes: "",
    breakMinutes: "",
    drivingMinutes: "",
    distanceKm: "",
    notes: "",
};

const emptyConfidence: PlanningDutyPdfImportConfidence = {
    dutyNumber: 0,
    startTime: 0,
    endTime: 0,
    line: 0,
    stops: 0,
    workingMinutes: 0,
    drivingMinutes: 0,
    breakMinutes: 0,
    distanceKm: 0,
};

const dateFormatter = new Intl.DateTimeFormat("pl-PL", { dateStyle: "medium" });

function formatDate(value: string | null) {
    if (!value) return "Brak danych";

    const date = new Date(`${value}T00:00:00`);
    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function formatTime(value: string | null) {
    return value ? value.slice(0, 5) : "Brak danych";
}

function formatMinutes(value: number | null) {
    return value === null || value === undefined ? "Brak danych" : `${value} min`;
}

function formatKm(value: number | null) {
    return value === null || value === undefined ? "Brak danych" : `${value.toLocaleString("pl-PL")} km`;
}

function toNumberOrNull(value: string) {
    if (value.trim() === "") return null;

    const normalized = value.replace(",", ".");
    const parsed = Number(normalized);

    return Number.isFinite(parsed) ? parsed : null;
}

function toIntegerOrNull(value: string) {
    const parsed = toNumberOrNull(value);

    return parsed === null ? null : Math.trunc(parsed);
}

function toPayload(form: PlanningDutyFormState): PlanningDutyPayload {
    return {
        dutyNumber: form.dutyNumber.trim(),
        name: form.name.trim(),
        validFrom: form.validFrom || null,
        vehicleRequirement: form.vehicleRequirement.trim() || null,
        startTime: form.startTime ? `${form.startTime}:00` : null,
        endTime: form.endTime ? `${form.endTime}:00` : null,
        totalDurationMinutes: toIntegerOrNull(form.totalDurationMinutes),
        workMinutes: toIntegerOrNull(form.workMinutes),
        breakMinutes: toIntegerOrNull(form.breakMinutes),
        drivingMinutes: toIntegerOrNull(form.drivingMinutes),
        distanceKm: toNumberOrNull(form.distanceKm),
        notes: form.notes.trim() || null,
        lines: [],
        stops: [],
    };
}

function fromDetails(details: PlanningDutyDetails): PlanningDutyFormState {
    return {
        dutyNumber: details.dutyNumber,
        name: details.name,
        validFrom: details.validFrom ?? "",
        vehicleRequirement: details.vehicleRequirement ?? "",
        startTime: details.startTime?.slice(0, 5) ?? "",
        endTime: details.endTime?.slice(0, 5) ?? "",
        totalDurationMinutes: details.totalDurationMinutes?.toString() ?? "",
        workMinutes: details.workMinutes?.toString() ?? "",
        breakMinutes: details.breakMinutes?.toString() ?? "",
        drivingMinutes: details.drivingMinutes?.toString() ?? "",
        distanceKm: details.distanceKm?.toString() ?? "",
        notes: details.notes ?? "",
    };
}

function toImportDraft(item: PlanningDutyPdfImportPreviewItem): ImportDraft {
    return {
        dutyNumber: item.dutyNumber ?? "",
        name: item.name ?? "",
        line: item.lines.map((line) => line.lineCode).join(" / "),
        validFrom: item.validFrom ?? "",
        vehicleRequirement: item.vehicleRequirement ?? "",
        startTime: item.startTime?.slice(0, 5) ?? "",
        endTime: item.endTime?.slice(0, 5) ?? "",
        workMinutes: item.workMinutes?.toString() ?? "",
        drivingMinutes: item.drivingMinutes?.toString() ?? "",
        breakMinutes: item.breakMinutes?.toString() ?? "",
        distanceKm: item.distanceKm?.toString() ?? "",
        notes: item.notes ?? "",
        stops: item.stops ?? [],
        confidence: item.confidence ?? emptyConfidence,
    };
}

function getConfidenceBadge(confidence: number) {
    if (confidence >= 90) return `🟢 ${confidence}%`;
    if (confidence >= 70) return `🟡 ${confidence}%`;
    if (confidence >= 40) return `🟠 ${confidence}%`;
    return `🔴 ${confidence}%`;
}

function getConfidenceClass(confidence: number) {
    if (confidence < 40) return " confidence-danger";
    if (confidence < 70) return " confidence-warning";
    return "";
}

function getImportDraftErrors(draft: ImportDraft) {
    const errors: Record<string, string> = {};

    if (!draft.dutyNumber.trim()) errors.dutyNumber = "Numer służby jest wymagany.";
    if (!draft.startTime.trim()) errors.startTime = "Godzina rozpoczęcia jest wymagana.";
    if (!draft.endTime.trim()) errors.endTime = "Godzina zakończenia jest wymagana.";

    return errors;
}

function getAverageConfidence(preview: PlanningDutyPdfImportPreview | null) {
    if (!preview || preview.duties.length === 0) return 0;

    const values = preview.duties.flatMap((duty) => Object.values(duty.confidence ?? emptyConfidence));
    if (values.length === 0) return 0;

    return Math.round(values.reduce((sum, value) => sum + value, 0) / values.length);
}

export default function PlanningPage() {
    const [duties, setDuties] = useState<PlanningDuty[]>([]);
    const [selectedDuty, setSelectedDuty] = useState<PlanningDutyDetails | null>(null);
    const [dutyToDelete, setDutyToDelete] = useState<PlanningDuty | null>(null);
    const [form, setForm] = useState<PlanningDutyFormState>(emptyForm);
    const [isEditing, setIsEditing] = useState(false);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [message, setMessage] = useState("");
    const [pdfFile, setPdfFile] = useState<File | null>(null);
    const [pdfPreview, setPdfPreview] = useState<PlanningDutyPdfImportPreview | null>(null);
    const [importResult, setImportResult] = useState<PlanningDutyPdfImportConfirmResult | null>(null);
    const [importDrafts, setImportDrafts] = useState<ImportDraft[]>([]);
    const [isPreviewLoading, setIsPreviewLoading] = useState(false);
    const [isImporting, setIsImporting] = useState(false);
    const [isMessageError, setIsMessageError] = useState(false);

    async function loadDuties() {
        setIsLoading(true);
        setMessage("");
        setIsMessageError(false);

        try {
            setDuties(await getPlanningDuties());
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Wystąpił błąd podczas pobierania służb.");
        } finally {
            setIsLoading(false);
        }
    }

    async function showDetails(id: string) {
        setMessage("");
        setIsMessageError(false);

        try {
            const details = await getPlanningDuty(id);
            setSelectedDuty(details);
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się pobrać szczegółów służby.");
        }
    }

    function startCreate() {
        setIsEditing(false);
        setSelectedDuty(null);
        setForm(emptyForm);
        setMessage("");
        setIsMessageError(false);
    }

    async function startEdit(duty: PlanningDuty) {
        setMessage("");
        setIsMessageError(false);

        try {
            const details = await getPlanningDuty(duty.id);
            setSelectedDuty(details);
            setForm(fromDetails(details));
            setIsEditing(true);
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się otworzyć edycji służby.");
        }
    }

    async function saveDuty(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setIsSaving(true);
        setMessage("");
        setIsMessageError(false);

        try {
            const payload = toPayload(form);
            const saved = isEditing && selectedDuty
                ? await updatePlanningDuty(selectedDuty.id, payload)
                : await createPlanningDuty(payload);

            setSelectedDuty(saved);
            setForm(fromDetails(saved));
            setIsEditing(true);
            await loadDuties();
            setMessage("Służba została zapisana.");
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się zapisać służby.");
        } finally {
            setIsSaving(false);
        }
    }

    async function previewPdfImport() {
        if (!pdfFile) {
            setIsMessageError(true);
            setMessage("Wybierz plik PDF przed przygotowaniem podglądu.");
            return;
        }

        setIsPreviewLoading(true);
        setMessage("");
        setIsMessageError(false);
        setPdfPreview(null);
        setImportDrafts([]);
        setImportResult(null);

        try {
            const preview = await previewPlanningDutiesPdf(pdfFile);
            setPdfPreview(preview);
            setImportDrafts(preview.duties.map(toImportDraft));
            setMessage("Podgląd importu PDF został przygotowany. Dane nie zostały zapisane do bazy.");
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się przygotować podglądu importu PDF.");
        } finally {
            setIsPreviewLoading(false);
        }
    }

    async function confirmDelete() {
        if (!dutyToDelete) return;

        setIsDeleting(true);
        setMessage("");
        setIsMessageError(false);

        try {
            await deletePlanningDuty(dutyToDelete.id);
            if (selectedDuty?.id === dutyToDelete.id) {
                setSelectedDuty(null);
                setForm(emptyForm);
                setIsEditing(false);
            }
            setDutyToDelete(null);
            await loadDuties();
            setMessage("Służba została usunięta.");
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się usunąć służby.");
        } finally {
            setIsDeleting(false);
        }
    }

    function updateImportDraft(index: number, field: keyof ImportDraft, value: string) {
        setImportDrafts((current) => current.map((draft, draftIndex) => (
            draftIndex === index ? { ...draft, [field]: value } : draft
        )));
    }


    function hasImportValidationErrors() {
        return importDrafts.length === 0 || importDrafts.some((draft) => Object.keys(getImportDraftErrors(draft)).length > 0);
    }

    async function confirmPdfImport() {
        if (!pdfPreview || hasImportValidationErrors()) {
            setIsMessageError(true);
            setMessage("Uzupełnij wymagane pola przed importem do biblioteki.");
            return;
        }

        setIsImporting(true);
        setMessage("");
        setIsMessageError(false);
        setImportResult(null);

        try {
            const result = await confirmPlanningDutiesPdfImport({
                sourceFileName: pdfPreview.fileName,
                duties: importDrafts.map((draft) => ({
                    dutyNumber: draft.dutyNumber.trim(),
                    dutyName: draft.name.trim() || null,
                    line: draft.line.trim() || null,
                    validFrom: draft.validFrom || null,
                    vehicleRequirement: draft.vehicleRequirement.trim() || null,
                    startTime: draft.startTime ? `${draft.startTime}:00` : null,
                    endTime: draft.endTime ? `${draft.endTime}:00` : null,
                    workingMinutes: toIntegerOrNull(draft.workMinutes),
                    drivingMinutes: toIntegerOrNull(draft.drivingMinutes),
                    breakMinutes: toIntegerOrNull(draft.breakMinutes),
                    distanceKm: toNumberOrNull(draft.distanceKm),
                    notes: draft.notes.trim() || null,
                    stops: draft.stops.map((stop) => ({
                        stopName: stop.stopName,
                        arrivalTime: stop.arrivalTime,
                        departureTime: stop.departureTime,
                        km: stop.km,
                        lineCode: stop.lineCode,
                        sequence: stop.sequence,
                    })),
                })),
            });

            setImportResult(result);
            await loadDuties();
            setMessage("Import PDF został zapisany do biblioteki służb.");
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się zapisać importu PDF do biblioteki.");
        } finally {
            setIsImporting(false);
        }
    }
    function cancelImportVerification() {
        setPdfPreview(null);
        setImportDrafts([]);
        setImportResult(null);
        setPdfFile(null);
        setImportResult(null);
        setMessage("");
        setIsMessageError(false);
    }

    useEffect(() => {
        void loadDuties();
    }, []);

    const averageConfidence = getAverageConfidence(pdfPreview);

    return (
        <div className="drivers-page planning-page">
            <div className="drivers-heading">
                <div>
                    <h2>Planowanie</h2>
                    <p>Baza służb i tras do późniejszego importu oraz ręcznej edycji.</p>
                </div>
                <span className="drivers-count">{duties.length} służb</span>
            </div>

            {message ? (
                <p className={`drivers-message${isMessageError ? " error" : " success"}`} role={isMessageError ? "alert" : "status"}>
                    {message}
                </p>
            ) : null}

            <section className="drivers-panel planning-import-panel">
                <div className="section-heading planning-panel-heading">
                    <div>
                        <h3>Import PDF służb</h3>
                        <p>Podgląd i ręczna weryfikacja danych z tekstowego pliku PDF. OCR i zapis do bazy pojawią się w kolejnych etapach.</p>
                    </div>
                </div>

                <div className="planning-import-controls">
                    <label>
                        Plik PDF
                        <input
                            type="file"
                            accept="application/pdf,.pdf"
                            onChange={(event) => {
                                setPdfFile(event.target.files?.[0] ?? null);
                                setPdfPreview(null);
                                setImportDrafts([]);
                                setImportResult(null);
                            }}
                        />
                    </label>
                    <button
                        className="planning-primary-button"
                        type="button"
                        onClick={() => void previewPdfImport()}
                        disabled={!pdfFile || isPreviewLoading}
                    >
                        {isPreviewLoading ? "Analizowanie..." : "Podgląd importu"}
                    </button>
                </div>

                {pdfPreview ? (
                    <div className="planning-import-preview">
                        <div className="planning-preview-summary planning-verification-summary">
                            <span><strong>Plik:</strong> {pdfPreview.fileName}</span>
                            <span><strong>Rozmiar:</strong> {pdfPreview.fileSizeBytes.toLocaleString("pl-PL")} B</span>
                            <span><strong>Rozpoznano służb:</strong> {pdfPreview.detectedDutyCount}</span>
                            <span><strong>Średnia pewność:</strong> {averageConfidence}%</span>
                            <span><strong>Liczba ostrzeżeń:</strong> {pdfPreview.warnings.length}</span>
                        </div>

                        {pdfPreview.warnings.length > 0 ? (
                            <div className="planning-warning-list" role="status">
                                {pdfPreview.warnings.map((warning) => <p key={warning}>{warning}</p>)}
                            </div>
                        ) : null}

                        {importDrafts.length > 0 ? (
                            <div className="planning-verification-list">
                                {importDrafts.map((draft, index) => {
                                    const errors = getImportDraftErrors(draft);
                                    return (
                                        <section className="planning-verification-card" key={`${draft.dutyNumber || "sluzba"}-${index}`}>
                                            <h4>Służba {draft.dutyNumber || index + 1}</h4>
                                            <div className="planning-verification-grid">
                                                <VerificationField label="Numer służby" confidence={draft.confidence.dutyNumber} error={errors.dutyNumber}>
                                                    <input className={getConfidenceClass(draft.confidence.dutyNumber)} value={draft.dutyNumber} onChange={(event) => updateImportDraft(index, "dutyNumber", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Nazwa (opcjonalnie)">
                                                    <input value={draft.name} onChange={(event) => updateImportDraft(index, "name", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Linia" confidence={draft.confidence.line}>
                                                    <input className={getConfidenceClass(draft.confidence.line)} value={draft.line} onChange={(event) => updateImportDraft(index, "line", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Ważna od">
                                                    <input type="date" value={draft.validFrom} onChange={(event) => updateImportDraft(index, "validFrom", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Wymagany pojazd">
                                                    <input value={draft.vehicleRequirement} onChange={(event) => updateImportDraft(index, "vehicleRequirement", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Godzina rozpoczęcia" confidence={draft.confidence.startTime} error={errors.startTime}>
                                                    <input className={getConfidenceClass(draft.confidence.startTime)} type="time" value={draft.startTime} onChange={(event) => updateImportDraft(index, "startTime", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Godzina zakończenia" confidence={draft.confidence.endTime} error={errors.endTime}>
                                                    <input className={getConfidenceClass(draft.confidence.endTime)} type="time" value={draft.endTime} onChange={(event) => updateImportDraft(index, "endTime", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Czas pracy" confidence={draft.confidence.workingMinutes}>
                                                    <input className={getConfidenceClass(draft.confidence.workingMinutes)} type="number" min="0" value={draft.workMinutes} onChange={(event) => updateImportDraft(index, "workMinutes", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Czas jazdy" confidence={draft.confidence.drivingMinutes}>
                                                    <input className={getConfidenceClass(draft.confidence.drivingMinutes)} type="number" min="0" value={draft.drivingMinutes} onChange={(event) => updateImportDraft(index, "drivingMinutes", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Czas przerwy" confidence={draft.confidence.breakMinutes}>
                                                    <input className={getConfidenceClass(draft.confidence.breakMinutes)} type="number" min="0" value={draft.breakMinutes} onChange={(event) => updateImportDraft(index, "breakMinutes", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Kilometry" confidence={draft.confidence.distanceKm}>
                                                    <input className={getConfidenceClass(draft.confidence.distanceKm)} type="number" min="0" step="0.01" value={draft.distanceKm} onChange={(event) => updateImportDraft(index, "distanceKm", event.target.value)} />
                                                </VerificationField>
                                                <VerificationField label="Uwagi">
                                                    <textarea value={draft.notes} onChange={(event) => updateImportDraft(index, "notes", event.target.value)} />
                                                </VerificationField>
                                            </div>
                                        </section>
                                    );
                                })}
                            </div>
                        ) : null}

                        {importResult ? (
                            <div className="planning-import-result" role="status">
                                <div className="planning-preview-summary">
                                    <span><strong>Dodano:</strong> {importResult.createdCount}</span>
                                    <span><strong>Zaktualizowano:</strong> {importResult.updatedCount}</span>
                                    <span><strong>Bez zmian:</strong> {importResult.unchangedCount}</span>
                                    <span><strong>Pominięto:</strong> {importResult.skippedCount}</span>
                                    <span><strong>Błędy:</strong> {importResult.errors.length}</span>
                                </div>
                                {importResult.errors.length > 0 ? (
                                    <div className="planning-warning-list">
                                        {importResult.errors.map((error) => <p key={error}>{error}</p>)}
                                    </div>
                                ) : null}
                                {importResult.items.length > 0 ? (
                                    <div className="drivers-table-wrapper">
                                        <table className="drivers-table planning-table">
                                            <thead>
                                                <tr>
                                                    <th>Numer</th>
                                                    <th>Linia</th>
                                                    <th>Status</th>
                                                    <th>Komunikat</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {importResult.items.map((item, index) => (
                                                    <tr key={`${item.dutyNumber ?? "wynik"}-${index}`}>
                                                        <td>{item.dutyNumber ?? "Brak danych"}</td>
                                                        <td>{item.line ?? "Brak danych"}</td>
                                                        <td>{item.status}</td>
                                                        <td>{item.message}</td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    </div>
                                ) : null}
                            </div>
                        ) : null}

                        <div className="planning-import-actions">
                            <button
                                className="planning-primary-button"
                                type="button"
                                onClick={() => void confirmPdfImport()}
                                disabled={hasImportValidationErrors() || isImporting}
                            >
                                {isImporting ? "Importowanie..." : "Importuj do biblioteki"}
                            </button>
                            <button className="planning-secondary-button" type="button" onClick={cancelImportVerification} disabled={isImporting}>
                                Anuluj
                            </button>
                        </div>
                    </div>
                ) : null}
            </section>

            <PlanningSchedulesTab />

            <div className="planning-grid">
                <section className="drivers-panel">
                    <div className="section-heading planning-panel-heading">
                        <div>
                            <h3>Baza służb</h3>
                            <p>Lista służb planowania przypisanych do aktualnej firmy.</p>
                        </div>
                        <div className="driver-row-actions">
                            <button className="planning-secondary-button" type="button" onClick={() => void loadDuties()} disabled={isLoading}>
                                Odśwież
                            </button>
                            <button className="planning-primary-button" type="button" onClick={startCreate}>
                                Dodaj służbę
                            </button>
                        </div>
                    </div>

                    {isLoading ? <TableSkeleton rows={6} columns={7} /> : null}

                    {!isLoading && duties.length === 0 ? (
                        <EmptyState title="Brak służb" description="Dodaj pierwszą służbę ręcznie. Import z PDF pojawi się w kolejnym etapie." />
                    ) : null}

                    {!isLoading && duties.length > 0 ? (
                        <div className="drivers-table-wrapper">
                            <table className="drivers-table planning-table">
                                <thead>
                                    <tr>
                                        <th>Numer</th>
                                        <th>Linie</th>
                                        <th>Ważna od</th>
                                        <th>Typ pojazdu</th>
                                        <th>Praca</th>
                                        <th>Km</th>
                                        <th>Akcje</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {duties.map((duty) => (
                                        <tr key={duty.id}>
                                            <td>{duty.dutyNumber}</td>
                                            <td>{duty.lines.length > 0 ? duty.lines.map((line) => line.lineCode).join(" / ") : "Brak danych"}</td>
                                            <td>{formatDate(duty.validFrom)}</td>
                                            <td>{duty.vehicleRequirement ?? "Brak danych"}</td>
                                            <td>{formatMinutes(duty.workMinutes)}</td>
                                            <td>{formatKm(duty.distanceKm)}</td>
                                            <td>
                                                <div className="driver-row-actions">
                                                    <button className="planning-link-button" type="button" onClick={() => void showDetails(duty.id)}>
                                                        Szczegóły
                                                    </button>
                                                    <button className="planning-link-button" type="button" onClick={() => void startEdit(duty)}>
                                                        Edytuj
                                                    </button>
                                                    <button className="driver-delete-button" type="button" onClick={() => setDutyToDelete(duty)}>
                                                        Usuń
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    ) : null}
                </section>

                <section className="driver-form planning-form-panel">
                    <div className="section-heading">
                        <h3>{isEditing ? "Edytuj służbę" : "Dodaj służbę"}</h3>
                        <p>Na tym etapie zapisujemy podstawowe dane służby. Linie i przystanki będą rozwijane przy imporcie PDF.</p>
                    </div>

                    <form className="planning-form" onSubmit={(event) => void saveDuty(event)}>
                        <label>Numer służby<input value={form.dutyNumber} onChange={(event) => setForm({ ...form, dutyNumber: event.target.value })} required maxLength={50} /></label>
                        <label>Nazwa<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required maxLength={200} /></label>
                        <label>Ważna od<input type="date" value={form.validFrom} onChange={(event) => setForm({ ...form, validFrom: event.target.value })} /></label>
                        <label>Wymagany pojazd<input value={form.vehicleRequirement} onChange={(event) => setForm({ ...form, vehicleRequirement: event.target.value })} maxLength={200} /></label>
                        <div className="planning-form-row">
                            <label>Start<input type="time" value={form.startTime} onChange={(event) => setForm({ ...form, startTime: event.target.value })} /></label>
                            <label>Koniec<input type="time" value={form.endTime} onChange={(event) => setForm({ ...form, endTime: event.target.value })} /></label>
                        </div>
                        <div className="planning-form-row">
                            <label>Czas całkowity min<input type="number" min="0" value={form.totalDurationMinutes} onChange={(event) => setForm({ ...form, totalDurationMinutes: event.target.value })} /></label>
                            <label>Czas pracy min<input type="number" min="0" value={form.workMinutes} onChange={(event) => setForm({ ...form, workMinutes: event.target.value })} /></label>
                        </div>
                        <div className="planning-form-row">
                            <label>Przerwy min<input type="number" min="0" value={form.breakMinutes} onChange={(event) => setForm({ ...form, breakMinutes: event.target.value })} /></label>
                            <label>Czas jazdy min<input type="number" min="0" value={form.drivingMinutes} onChange={(event) => setForm({ ...form, drivingMinutes: event.target.value })} /></label>
                        </div>
                        <label>Kilometry<input type="number" min="0" step="0.01" value={form.distanceKm} onChange={(event) => setForm({ ...form, distanceKm: event.target.value })} /></label>
                        <label>Uwagi<textarea value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} maxLength={4000} /></label>
                        <div className="driver-row-actions planning-form-actions">
                            <button type="submit" disabled={isSaving}>{isSaving ? "Zapisywanie..." : "Zapisz"}</button>
                            {isEditing ? <button className="planning-secondary-button" type="button" onClick={startCreate}>Nowa służba</button> : null}
                        </div>
                    </form>

                    <div className="planning-details-box">
                        <h4>Szczegóły służby</h4>
                        {selectedDuty ? (
                            <>
                                <div className="planning-duty-details-grid">
                                    <p><strong>Numer służby:</strong> {selectedDuty.dutyNumber}</p>
                                    <p><strong>Ważna od:</strong> {formatDate(selectedDuty.validFrom)}</p>
                                    <p><strong>Linie:</strong> {selectedDuty.lines.length > 0 ? selectedDuty.lines.map((line) => line.lineCode).join(" / ") : "Brak danych"}</p>
                                    <p><strong>Pojazd:</strong> {selectedDuty.vehicleRequirement ?? "Brak danych"}</p>
                                    <p><strong>Czas pracy:</strong> {formatMinutes(selectedDuty.workMinutes)}</p>
                                    <p><strong>Przerwy:</strong> {formatMinutes(selectedDuty.breakMinutes)}</p>
                                    <p><strong>Dzienny przebieg:</strong> {formatKm(selectedDuty.distanceKm)}</p>
                                </div>
                                {selectedDuty.stops.length > 0 ? (
                                    <div className="planning-stops-details">
                                        <h5>Lista przystanków</h5>
                                        <div className="drivers-table-wrapper">
                                            <table className="drivers-table planning-table planning-stops-table">
                                                <thead>
                                                    <tr>
                                                        <th>LP</th>
                                                        <th>Przystanek</th>
                                                        <th>Km</th>
                                                        <th>Przyjazd</th>
                                                        <th>Odjazd</th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {selectedDuty.stops.map((stop) => (
                                                        <tr key={stop.id || `${stop.sequence}-${stop.stopName}`}>
                                                            <td>{stop.sequence}</td>
                                                            <td>{stop.stopName}</td>
                                                            <td>{stop.km ?? "-"}</td>
                                                            <td>{formatTime(stop.arrivalTime)}</td>
                                                            <td>{formatTime(stop.departureTime)}</td>
                                                        </tr>
                                                    ))}
                                                </tbody>
                                            </table>
                                        </div>
                                    </div>
                                ) : <p><strong>Przystanki:</strong> Brak danych</p>}
                            </>
                        ) : (
                            <p>Wybierz służbę z listy albo zapisz nową, aby zobaczyć szczegóły.</p>
                        )}
                    </div>
                </section>
            </div>

            {dutyToDelete ? (
                <div className="driver-delete-modal-backdrop" role="presentation" onClick={() => !isDeleting && setDutyToDelete(null)}>
                    <section className="driver-delete-modal" role="dialog" aria-modal="true" aria-labelledby="planning-delete-title" onClick={(event) => event.stopPropagation()}>
                        <h3 id="planning-delete-title">Usuń służbę</h3>
                        <p>Czy na pewno chcesz usunąć służbę {dutyToDelete.name}?</p>
                        <div className="driver-delete-modal-actions">
                            <button type="button" onClick={() => setDutyToDelete(null)} disabled={isDeleting}>Anuluj</button>
                            <button className="danger" type="button" onClick={() => void confirmDelete()} disabled={isDeleting}>{isDeleting ? "Usuwanie..." : "Usuń"}</button>
                        </div>
                    </section>
                </div>
            ) : null}
        </div>
    );
}

function VerificationField({
    label,
    confidence,
    error,
    children,
}: {
    label: string;
    confidence?: number;
    error?: string;
    children: ReactNode;
}) {
    return (
        <label className="planning-verification-field">
            <span className="planning-field-heading">
                <span>{label}</span>
                {confidence !== undefined ? <small>{getConfidenceBadge(confidence)}</small> : null}
            </span>
            {children}
            {error ? <span className="planning-field-error">{error}</span> : null}
        </label>
    );
}












