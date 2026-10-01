import { useEffect, useState, type FormEvent } from "react";

import {
    getCompanySettings,
    updateCompanySettings,
    type CompanySettings,
} from "../services/companySettingsService";
import "../styles/company-settings.css";

const emptySettings: CompanySettings = {
    name: "",
    vatNumber: "",
    address: "",
    email: "",
    phone: "",
};

function trimSettings(settings: CompanySettings): CompanySettings {
    return {
        name: settings.name.trim(),
        vatNumber: settings.vatNumber.trim(),
        address: settings.address.trim(),
        email: settings.email.trim(),
        phone: settings.phone.trim(),
    };
}

export default function CompanySettingsPage() {
    const [settings, setSettings] = useState<CompanySettings>(emptySettings);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");

    useEffect(() => {
        async function loadSettings() {
            try {
                setSettings(await getCompanySettings());
            } catch (loadError) {
                setError(loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania ustawień firmy.");
            } finally {
                setIsLoading(false);
            }
        }

        void loadSettings();
    }, []);

    function updateField(field: keyof CompanySettings, value: string) {
        setSettings((current) => ({ ...current, [field]: value }));
        setSuccess("");
    }

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const normalized = trimSettings(settings);

        if (!normalized.name) {
            setError("Nazwa firmy jest wymagana.");
            return;
        }

        if (normalized.email && !/^\S+@\S+\.\S+$/.test(normalized.email)) {
            setError("Podaj poprawny adres e-mail firmy.");
            return;
        }

        setIsSaving(true);
        setError("");
        setSuccess("");

        try {
            const saved = await updateCompanySettings(normalized);
            setSettings(saved);
            setSuccess("Ustawienia firmy zostały zapisane.");
            window.dispatchEvent(new CustomEvent("drivertime:company-updated", {
                detail: { name: saved.name },
            }));
        } catch (saveError) {
            setError(saveError instanceof Error
                ? saveError.message
                : "Wystąpił błąd podczas zapisywania ustawień firmy.");
        } finally {
            setIsSaving(false);
        }
    }

    return (
        <div className="company-settings-page">
            <div className="company-settings-heading">
                <div>
                    <h2>Ustawienia firmy</h2>
                    <p>Dane wykorzystywane w raportach PDF i Excel.</p>
                </div>
            </div>

            {isLoading ? (
                <div className="company-settings-skeleton" aria-busy="true">
                    <div className="ui-skeleton" />
                    <div className="ui-skeleton" />
                    <div className="ui-skeleton" />
                    <div className="ui-skeleton" />
                </div>
            ) : error && !settings.name ? (
                <p className="company-settings-message error" role="alert">{error}</p>
            ) : (
                <form className="company-settings-form" onSubmit={handleSubmit}>
                    <div className="company-settings-form-heading">
                        <h3>Dane firmy</h3>
                        <p>Pola kontaktowe sa opcjonalne.</p>
                    </div>

                    <label className="company-settings-wide">
                        Nazwa firmy <span aria-hidden="true">*</span>
                        <input value={settings.name} onChange={(event) => updateField("name", event.target.value)} required maxLength={200} />
                    </label>
                    <label>
                        NIP
                        <input value={settings.vatNumber} onChange={(event) => updateField("vatNumber", event.target.value)} maxLength={50} />
                    </label>
                    <label>
                        Telefon
                        <input type="tel" value={settings.phone} onChange={(event) => updateField("phone", event.target.value)} maxLength={50} />
                    </label>
                    <label className="company-settings-wide">
                        Adres
                        <textarea value={settings.address} onChange={(event) => updateField("address", event.target.value)} rows={3} maxLength={500} />
                    </label>
                    <label className="company-settings-wide">
                        Email
                        <input type="email" value={settings.email} onChange={(event) => updateField("email", event.target.value)} maxLength={320} />
                    </label>

                    {error && <p className="company-settings-message error company-settings-wide" role="alert">{error}</p>}
                    {success && <p className="company-settings-message success company-settings-wide" role="status">{success}</p>}

                    <div className="company-settings-actions company-settings-wide">
                        <button type="submit" disabled={isSaving}>
                            {isSaving ? "Zapisywanie..." : "Zapisz ustawienia"}
                        </button>
                    </div>
                </form>
            )}
        </div>
    );
}
