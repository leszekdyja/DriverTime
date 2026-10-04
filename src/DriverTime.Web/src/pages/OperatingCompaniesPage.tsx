import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import {
    createOperatingCompany,
    deleteOperatingCompany,
    getOperatingCompanies,
    updateOperatingCompany,
    type OperatingCompany,
} from "../services/operatingCompaniesService";
import "../styles/drivers.css";

const emptyForm = {
    name: "", taxNumber: "", active: true, createLoginAccount: false,
    accountFirstName: "", accountLastName: "", accountEmail: "", accountPassword: "",
};

export default function OperatingCompaniesPage() {
    const [companies, setCompanies] = useState<OperatingCompany[]>([]);
    const [form, setForm] = useState(emptyForm);
    const [editingId, setEditingId] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [message, setMessage] = useState("");
    const [error, setError] = useState(false);

    async function load() {
        try {
            setCompanies(await getOperatingCompanies());
        } catch (loadError) {
            setError(true);
            setMessage(loadError instanceof Error ? loadError.message : "Nie udało się pobrać firm.");
        }
    }

    useEffect(() => { void load(); }, []);

    async function save(event: FormEvent) {
        event.preventDefault();
        setBusy(true); setMessage(""); setError(false);
        try {
            if (editingId) await updateOperatingCompany(editingId, form);
            else await createOperatingCompany(form);
            setForm(emptyForm); setEditingId(null);
            await load();
            setMessage(editingId ? "Firma została zaktualizowana." : "Firma została utworzona.");
        } catch (saveError) {
            setError(true);
            setMessage(saveError instanceof Error ? saveError.message : "Nie udało się zapisać firmy.");
        } finally { setBusy(false); }
    }

    function edit(company: OperatingCompany) {
        setEditingId(company.id);
        setForm({ ...emptyForm, name: company.name, taxNumber: company.taxNumber, active: company.active });
        setMessage("");
    }

    async function remove(company: OperatingCompany) {
        if (!window.confirm(`Usunąć firmę ${company.name}? Kierowcy pozostaną w bazie bez przypisanej firmy.`)) return;
        setBusy(true); setMessage(""); setError(false);
        try {
            await deleteOperatingCompany(company.id);
            await load();
            setMessage("Firma została usunięta. Kierowcy pozostali bez przypisania.");
        } catch (removeError) {
            setError(true);
            setMessage(removeError instanceof Error ? removeError.message : "Nie udało się usunąć firmy.");
        } finally { setBusy(false); }
    }

    return <div className="drivers-page">
        <div className="drivers-heading"><div><h2>Firmy</h2><p>Twórz firmy operacyjne i przypisuj do nich kierowców.</p></div><span className="drivers-count">{companies.length} firm</span></div>
        <section className="drivers-panel">
            <div className="section-heading"><h3>{editingId ? "Edytuj firmę" : "Nowa firma"}</h3><p>Firma jest widoczna wyłącznie w obrębie Twojego konta.</p></div>
            <form className="company-form" onSubmit={save}>
                <label>Nazwa firmy<input required maxLength={200} value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} /></label>
                <label>NIP / identyfikator<input maxLength={50} value={form.taxNumber} onChange={event => setForm({ ...form, taxNumber: event.target.value })} /></label>
                <label className="driver-table-toggle"><input type="checkbox" checked={form.active} onChange={event => setForm({ ...form, active: event.target.checked })} /><span>Aktywna</span></label>
                {!editingId && <>
                    <label className="driver-table-toggle"><input type="checkbox" checked={form.createLoginAccount} onChange={event => setForm({ ...form, createLoginAccount: event.target.checked })} /><span>Utwórz osobne konto logowania dla tej firmy</span></label>
                    {form.createLoginAccount && <div className="company-account-fields">
                        <label>Imię użytkownika<input required maxLength={100} value={form.accountFirstName} onChange={event => setForm({ ...form, accountFirstName: event.target.value })} /></label>
                        <label>Nazwisko użytkownika<input required maxLength={100} value={form.accountLastName} onChange={event => setForm({ ...form, accountLastName: event.target.value })} /></label>
                        <label>E-mail do logowania<input required type="email" maxLength={320} autoComplete="username" value={form.accountEmail} onChange={event => setForm({ ...form, accountEmail: event.target.value })} /></label>
                        <label>Hasło<input required type="password" minLength={8} autoComplete="new-password" value={form.accountPassword} onChange={event => setForm({ ...form, accountPassword: event.target.value })} /></label>
                        <p>Konto zobaczy wyłącznie kierowców przypisanych do tej firmy.</p>
                    </div>}
                </>}
                <div className="driver-row-actions"><button type="submit" disabled={busy}>{busy ? "Zapisywanie..." : editingId ? "Zapisz zmiany" : "Dodaj firmę"}</button>{editingId && <button type="button" onClick={() => { setEditingId(null); setForm(emptyForm); }}>Anuluj</button>}</div>
            </form>
            {message && <p className={`drivers-message ${error ? "error" : "success"}`}>{message}</p>}
        </section>
        <section className="drivers-panel">
            <div className="section-heading"><h3>Lista firm</h3><p>Liczba przypisanych kierowców aktualizuje się automatycznie.</p></div>
            <div className="drivers-table-wrapper"><table className="drivers-table"><thead><tr><th>Nazwa</th><th>NIP / identyfikator</th><th>Konto</th><th>Status</th><th>Kierowcy</th><th></th></tr></thead><tbody>
                {companies.map(company => <tr key={company.id}><td>{company.name}</td><td>{company.taxNumber || "—"}</td><td>{company.accountEmail || "Brak"}</td><td>{company.active ? "Aktywna" : "Nieaktywna"}</td><td>{company.driversCount}</td><td><div className="driver-row-actions"><button type="button" onClick={() => edit(company)}>Edytuj</button><button className="driver-delete-button" type="button" disabled={busy} onClick={() => void remove(company)}>Usuń</button></div></td></tr>)}
                {companies.length === 0 && <tr><td colSpan={6}>Nie utworzono jeszcze żadnej firmy.</td></tr>}
            </tbody></table></div>
        </section>
    </div>;
}
