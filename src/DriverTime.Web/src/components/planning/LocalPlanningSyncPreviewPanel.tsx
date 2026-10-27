import { useEffect, useRef, useState } from "react";
import { previewLocalPlanningSync, type LocalPlanningSyncPreview, type LocalPlanningMatch } from "../../services/localPlanningSyncService";
import LocalPlanningManualImportPanel from "./LocalPlanningManualImportPanel";

const statusLabels = { Missing: "Brak dopasowania", Suggested: "Propozycja — do potwierdzenia", Ambiguous: "Niejednoznaczne" };

export default function LocalPlanningSyncPreviewPanel() {
    const [file, setFile] = useState<File | null>(null);
    const [preview, setPreview] = useState<LocalPlanningSyncPreview | null>(null);
    const [error, setError] = useState("");
    const [loading, setLoading] = useState(false);
    const [manualBusy, setManualBusy] = useState(false);
    const request = useRef<AbortController | null>(null);
    useEffect(() => () => request.current?.abort(), []);

    function resetFile(next: File | null) {
        request.current?.abort();
        request.current = null;
        setFile(next); setPreview(null); setError(""); setLoading(false);
    }

    async function loadPreview() {
        if (!file) return;
        request.current?.abort();
        const controller = new AbortController();
        request.current = controller;
        setLoading(true); setError(""); setPreview(null);
        try {
            const result = await previewLocalPlanningSync(file, controller.signal);
            if (!controller.signal.aborted) setPreview(result);
        } catch (reason) {
            if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : "Błąd podglądu.");
        } finally {
            if (request.current === controller) setLoading(false);
        }
    }

    function matches(title: string, rows: LocalPlanningMatch[]) {
        return <details><summary>{title} ({rows.length})</summary>
            <div className="drivers-table-wrapper"><table className="drivers-table planning-table">
                <thead><tr><th>Id lokalne</th><th>Nazwa lokalna</th><th>Dopasowanie w bieżącej firmie</th><th>Kandydaci</th></tr></thead>
                <tbody>{rows.map(row => <tr key={row.sourceId}><td>{row.sourceId}</td><td>{row.label}</td>
                    <td>{statusLabels[row.status]}</td><td>{row.candidateIds.length}</td></tr>)}</tbody>
            </table></div></details>;
    }

    return <section className="planning-panel" aria-labelledby="local-sync-title">
        <h3 id="local-sync-title">Synchronizacja z komputerem — podgląd</h3>
        <p>Główna baza pozostaje na komputerze. Podgląd tylko porównuje dane; zapis wymaga osobnego ręcznego zatwierdzenia poniżej. Nie ma synchronizacji automatycznej.</p>
        <p>Wybierz eksport JSON przygotowany narzędziem <code>tools/export_local_planning.py</code>. Nie przesyłaj tu pliku SQLite.</p>
        <label>Eksport planera lokalnego (JSON, do 16 MB)
            <input disabled={manualBusy} type="file" accept=".json,application/json" onChange={event => resetFile(event.target.files?.[0] ?? null)} />
        </label>
        <button className="planning-primary-button" type="button" disabled={!file || loading || manualBusy} onClick={() => void loadPreview()}>
            {loading ? "Porównywanie..." : "Sprawdź różnice bez zapisywania"}
        </button>
        {error ? <p role="alert">{error}</p> : null}
        {preview ? <div aria-live="polite">
            <p>Eksport: {new Date(preview.exportedAtUtc).toLocaleString("pl-PL", { timeZone: "Europe/Warsaw" })}. Źródło: {preview.sourceId}.</p>
            <p>Kierowcy: {preview.driversCount}; służby: {preview.dutiesCount}; wpisy: {preview.entriesCount}, w tym ręczne: {preview.manualEntriesCount}.</p>
            <p>Miesiące: {preview.months.join(", ") || "brak"}.</p>
            <p>Możliwe kolizje: z wpisami ręcznymi {preview.possibleManualConflicts}, z wygenerowanymi {preview.possibleGeneratedConflicts}. Liczone według propozycji dopasowania, nie jako zatwierdzony import.</p>
            <ul>{preview.warnings.map(warning => <li key={warning}>{warning}</li>)}</ul>
            {matches("Kierowcy", preview.driverSuggestions)}
            {matches("Służby", preview.dutySuggestions)}
            <details><summary>Zachowane ustawienia — jeszcze niestosowane na VPS</summary>
                <ul>{Object.entries(preview.settingsCounts).map(([table, count]) => <li key={table}>{table}: {count}</li>)}</ul>
            </details>
        </div> : null}
        <LocalPlanningManualImportPanel key={file ? `${file.name}-${file.lastModified}` : "history"} file={file} preview={preview} onBusyChange={setManualBusy} />
    </section>;
}
