import { useEffect, useState } from "react";
import { localSyncRequest, type LocalPlanningSyncPreview, type LocalPlanningMatch, type LocalPlanningImportOptions,
    type LocalPlanningImportRequest, type LocalPlanningImportPlan, type LocalPlanningImportResult, type LocalPlanningBatch } from "../../services/localPlanningSyncService";
import { readPreparedLocalPlanningMappings } from "../../services/preparedLocalPlanningMappings";

export default function LocalPlanningManualImportPanel({ file, preview, onBusyChange }: {
    file: File | null; preview: LocalPlanningSyncPreview | null; onBusyChange: (busy: boolean) => void;
}) {
    const [options, setOptions] = useState<LocalPlanningImportOptions | null>(null);
    const [company, setCompany] = useState("");
    const [month, setMonth] = useState("");
    const [schedule, setSchedule] = useState("");
    const [driverMaps, setDriverMaps] = useState<Record<string, string>>({});
    const [dutyMaps, setDutyMaps] = useState<Record<string, string>>({});
    const [decisions, setDecisions] = useState<Record<string, string>>({});
    const [allowSkipping, setAllowSkipping] = useState(false);
    const [acknowledgeDefinitions, setAcknowledgeDefinitions] = useState(false);
    const [reviewed, setReviewed] = useState(false);
    const [plan, setPlan] = useState<LocalPlanningImportPlan | null>(null);
    const [prepared, setPrepared] = useState<LocalPlanningImportRequest | null>(null);
    const [result, setResult] = useState<LocalPlanningImportResult | null>(null);
    const [batches, setBatches] = useState<LocalPlanningBatch[]>([]);
    const [undo, setUndo] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [hasPreparedMappings, setHasPreparedMappings] = useState(false);

    useEffect(() => {
        let active = true;
        setHasPreparedMappings(false);
        if (file && file.size <= 16 * 1024 * 1024) void file.text().then(text => {
            if (active) setHasPreparedMappings(Boolean(JSON.parse(text)?.oneTimeMappings));
        }).catch(() => { /* The main preview reports invalid files. */ });
        return () => { active = false; };
    }, [file]);

    useEffect(() => {
        let active = true;
        setOptions(null); setDriverMaps({}); setDutyMaps({}); setSchedule(""); setPrepared(null); setPlan(null);
        localSyncRequest<LocalPlanningImportOptions>(`options${company ? `?operatingCompanyId=${company}` : ""}`)
            .then(value => { if (active) setOptions(value); }).catch(reason => { if (active) setError(String(reason)); });
        return () => { active = false; };
    }, [company]);
    useEffect(() => {
        let active = true;
        localSyncRequest<LocalPlanningBatch[]>("batches").then(value => { if (active) setBatches(value); })
            .catch(reason => { if (active) setError(String(reason)); });
        return () => { active = false; };
    }, []);

    function invalidate() { setPrepared(null); setReviewed(false); setResult(null); }
    function begin() { setBusy(true); onBusyChange(true); setError(""); setNotice(""); }
    function finish() { setBusy(false); onBusyChange(false); }

    function suggestions() {
        if (!preview || !options) return;
        const suggested = (rows: LocalPlanningMatch[], allowed: { id: string }[]) => Object.fromEntries(rows
            .filter(row => row.candidateIds.length === 1 && allowed.some(option => option.id === row.candidateIds[0]))
            .map(row => [row.sourceId, row.candidateIds[0]]));
        setDriverMaps(suggested(preview.driverSuggestions, options.drivers));
        setDutyMaps(suggested(preview.dutySuggestions, options.duties));
        setDecisions({}); setPlan(null); invalidate();
    }

    async function preparedMappings() {
        if (!file || !options) return;
        begin();
        try {
            const maps = readPreparedLocalPlanningMappings(JSON.parse(await file.text()), options);
            setDriverMaps(maps.driverMappings); setDutyMaps(maps.dutyMappings);
            setDecisions({}); setPlan(null); invalidate();
            setNotice(`Wczytano dopasowania tylko dla tego pliku: kierowcy ${Object.keys(maps.driverMappings).length}, służby ${Object.keys(maps.dutyMappings).length}. Nie zapisano grafiku ani stałych reguł. Sprawdź je przed ręcznym zatwierdzeniem.`);
        } catch (reason) { setError(reason instanceof Error ? reason.message : "Nie udało się wczytać dopasowań."); }
        finally { finish(); }
    }

    async function prepare() {
        if (!file || !month || !options) return;
        begin(); setPrepared(null); setReviewed(false); setResult(null);
        try {
            const [year, selectedMonth] = month.split("-").map(Number);
            const request: LocalPlanningImportRequest = {
                snapshot: JSON.parse(await file.text()), operationId: crypto.randomUUID(), year, month: selectedMonth,
                operatingCompanyId: options.operatingCompanyId, scheduleId: schedule || null,
                driverMappings: driverMaps, dutyMappings: dutyMaps, decisions, allowSkipping, acknowledgeDefinitions, revisionToken: "",
            };
            const response = await localSyncRequest<LocalPlanningImportPlan>("prepare", request);
            setPlan(response); setPrepared({ ...request, revisionToken: response.revisionToken });
        } catch (reason) { setError(reason instanceof Error ? reason.message : "Błąd przygotowania importu."); }
        finally { finish(); }
    }

    async function save() {
        if (!prepared || !plan?.canApply || !reviewed) return;
        begin();
        try {
            const saved = await localSyncRequest<LocalPlanningImportResult>("confirm", prepared);
            setResult(saved); setPrepared(null); setReviewed(false);
            setNotice("Zapisano wyłącznie wskazane wpisy. Kopia poprzednich wartości jest w historii importu. Baza na komputerze nie została zmieniona.");
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : "Błąd zapisu. Możesz ponowić tę samą operację.");
        } finally {
            try { setBatches(await localSyncRequest<LocalPlanningBatch[]>("batches")); } catch { /* Saving status is kept independently of the history refresh. */ }
            finish();
        }
    }

    async function revert() {
        if (!undo) return;
        begin();
        try {
            await localSyncRequest(`batches/${undo}/revert`, {});
            setUndo(""); invalidate(); setPlan(null);
            setNotice("Cofnięto zmiany importu. Grafik pozostaje na liście, a historia i kopia danych są zachowane.");
            setBatches(await localSyncRequest<LocalPlanningBatch[]>("batches"));
        } catch (reason) { setError(reason instanceof Error ? reason.message : "Nie udało się cofnąć importu."); }
        finally { finish(); }
    }

    function mapping(title: string, rows: LocalPlanningMatch[], values: Record<string, string>, kind: "driver" | "duty") {
        const available = kind === "driver" ? options?.drivers : options?.duties;
        return <details><summary>{title} — wybierz dopasowania lub świadomie pomiń</summary>
            <div className="drivers-table-wrapper local-sync-scroll"><table className="drivers-table planning-table">
                <thead><tr><th>Pozycja z komputera</th><th>Pozycja na VPS w wybranej firmie</th></tr></thead>
                <tbody>{rows.map(row => <tr key={row.sourceId}><td>{row.label} (id {row.sourceId})</td><td>
                    <select aria-label={`Dopasowanie ${row.label} ${row.sourceId}`} disabled={busy} value={values[row.sourceId] ?? ""}
                        onChange={event => { const next = { ...values }; if (event.target.value) next[row.sourceId] = event.target.value; else delete next[row.sourceId];
                            if (kind === "driver") setDriverMaps(next); else setDutyMaps(next); setDecisions({}); setPlan(null); invalidate(); }}>
                        <option value="">Pomiń — nie dopasowano</option>
                        {available?.map(option => <option key={option.id} value={option.id}>{option.label} — ID {option.id.slice(0, 8)}</option>)}
                    </select></td></tr>)}</tbody>
            </table></div></details>;
    }

    return <div className="local-sync-manual">
        <h4>Ręczny zapis na VPS</h4>
        <p>Nie ma zapisu automatycznego ani synchronizacji w tle. Każdy miesiąc zapisujesz osobnym przyciskiem. Brakujące osoby lub służby dodaj najpierw w odpowiedniej zakładce albo świadomie pomiń ich wpisy.</p>
        {error ? <p role="alert">{error}</p> : null}{notice ? <p role="status">{notice}</p> : null}
        {file && preview && options ? <>
            {options.canChooseCompany ? <label>Firma docelowa<select disabled={busy} value={company} onChange={event => {
                setCompany(event.target.value); setDecisions({}); invalidate(); }}>
                <option value="">Tylko konto główne (bez udostępnienia firmom)</option>
                {options.companies.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}
            </select></label> : <p>Import ograniczony do firmy bieżącego konta.</p>}
            <label>Miesiąc<select disabled={busy} value={month} onChange={event => { setMonth(event.target.value); setSchedule(""); setDecisions({}); setPlan(null); invalidate(); }}>
                <option value="">Wybierz miesiąc</option>{preview.months.map(value => <option key={value}>{value}</option>)}
            </select></label>
            <label>Grafik docelowy<select disabled={busy || !month} value={schedule} onChange={event => { setSchedule(event.target.value); setDecisions({}); setPlan(null); invalidate(); }}>
                <option value="">Grafik importu tego źródła (utwórz, jeśli nie istnieje)</option>
                {options.schedules.filter(item => `${item.year}-${String(item.month).padStart(2, "0")}` === month)
                    .map(item => <option key={item.id} value={item.id}>{item.label}</option>)}
            </select></label>
            <button type="button" disabled={busy} onClick={suggestions}>Wstaw jednoznaczne propozycje — następnie sprawdź</button>
            {hasPreparedMappings ? <button className="planning-primary-button" type="button" disabled={busy} onClick={() => void preparedMappings()}>Wczytaj jednorazowe dopasowania z tego pliku</button> : null}
            {mapping("Kierowcy", preview.driverSuggestions, driverMaps, "driver")}
            {mapping("Służby", preview.dutySuggestions, dutyMaps, "duty")}
            <label><input type="checkbox" disabled={busy} checked={allowSkipping} onChange={event => { setAllowSkipping(event.target.checked); invalidate(); }} /> Zgadzam się na import częściowy: wpisy bez dopasowania zostaną pominięte.</label>
            <label><input type="checkbox" disabled={busy} checked={acknowledgeDefinitions} onChange={event => { setAcknowledgeDefinitions(event.target.checked); invalidate(); }} /> Rozumiem, że zapisywane są komórki grafiku według czasów i RBH wybranych służb VPS; ustawienia generatora lokalnego nie są jeszcze synchronizowane.</label>
            <button type="button" className="planning-primary-button" disabled={busy || !month} onClick={() => void prepare()}>Przelicz kolizje i przygotuj zapis</button>
            {plan ? <>
                <p>Wybrano: {plan.selectedEntries}; nowe: {plan.newEntries}; identyczne: {plan.unchangedEntries}; pominięte: {plan.skippedEntries}; kolizje: {plan.conflicts.length}.</p>
                <ul>{plan.warnings.map(value => <li key={value}>{value}</li>)}</ul>
                {plan.conflicts.length > 0 ? <>
                    <button disabled={busy} type="button" onClick={() => { setDecisions(Object.fromEntries(plan.conflicts.map(c => [c.sourceEntryId, "KeepServer"]))); invalidate(); }}>Zachowaj VPS we wszystkich kolizjach</button>
                    <button disabled={busy} type="button" onClick={() => { setDecisions({ ...decisions, ...Object.fromEntries(plan.conflicts.filter(c => c.serverStatus === "Generated").map(c => [c.sourceEntryId, "UseLocal"])) }); invalidate(); }}>Wybierz komputer tylko zamiast wygenerowanych wpisów VPS</button>
                    <div className="drivers-table-wrapper local-sync-scroll"><table className="drivers-table planning-table">
                        <thead><tr><th>Data / kierowca</th><th>Komputer</th><th>VPS</th><th>Decyzja</th></tr></thead>
                        <tbody>{plan.conflicts.map(c => <tr key={c.sourceEntryId}><td>{c.date} {c.driver}</td><td>{c.localDuty}</td><td>{c.serverDuty} ({c.serverStatus})</td><td>
                            <select disabled={busy} value={decisions[c.sourceEntryId] ?? ""} onChange={event => { const next = { ...decisions }; if (event.target.value) next[c.sourceEntryId] = event.target.value; else delete next[c.sourceEntryId]; setDecisions(next); invalidate(); }}>
                                <option value="">Wybierz ręcznie</option><option value="KeepServer">Zachowaj VPS</option><option value="UseLocal">Zapisz wpis z komputera</option>
                            </select></td></tr>)}</tbody>
                    </table></div>
                    {!prepared ? <p>Po zmianie decyzji kliknij ponownie „Przelicz kolizje i przygotuj zapis”.</p> : null}
                </> : null}
            </> : null}
            {prepared && plan?.canApply ? <>
                <label><input type="checkbox" disabled={busy} checked={reviewed} onChange={event => setReviewed(event.target.checked)} /> Sprawdziłem dopasowania, pominięcia i decyzje o kolizjach. Chcę wykonać ten zapis.</label>
                <button className="planning-primary-button" type="button" disabled={busy || !reviewed} onClick={() => void save()}>{busy ? "Operacja w toku..." : "Zapisz na VPS — ręcznie"}</button>
            </> : null}
            {result ? <p>Zapisano: nowych {result.created}, zmienionych {result.updated}, zachowanych VPS {result.keptServer}, identycznych {result.unchanged}, pominiętych {result.skipped}. <a href="/planning">Odśwież listę grafików</a></p> : null}
        </> : null}
        <details><summary>Historia ręcznych importów i cofanie ({batches.length})</summary>
            {batches.map(batch => <p key={batch.id}>{batch.year}-{String(batch.month).padStart(2, "0")} — {new Date(batch.createdAtUtc).toLocaleString("pl-PL", { timeZone: "Europe/Warsaw" })}
                {batch.revertedAtUtc ? " — cofnięto" : <button type="button" disabled={busy} onClick={() => setUndo(batch.id)}>Cofnij ten import</button>}</p>)}
            {undo ? <div><p>Cofnąć zmiany tego importu? Później edytowane komórki są chronione — operacja zostanie wtedy zablokowana.</p>
                <button type="button" disabled={busy} onClick={() => void revert()}>Potwierdzam cofnięcie</button><button type="button" disabled={busy} onClick={() => setUndo("")}>Anuluj</button></div> : null}
        </details>
    </div>;
}
