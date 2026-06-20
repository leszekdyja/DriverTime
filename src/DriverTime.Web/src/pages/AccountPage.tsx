import { useEffect, useState, type FormEvent } from "react";

import {
    changeAccountPassword,
    getAccountProfile,
    updateAccountProfile,
    type AccountProfile,
} from "../services/accountService";
import "../styles/account.css";

const emptyProfile: AccountProfile = {
    id: "",
    firstName: "",
    lastName: "",
    email: "",
    role: "",
    companyName: "",
};

const roleLabels: Record<string, string> = {
    Admin: "Administrator",
    Dispatcher: "Dyspozytor",
    Driver: "Kierowca",
};

function formatRole(role?: string) {
    return role ? roleLabels[role] ?? role : "";
}

export default function AccountPage() {
    const [profile, setProfile] = useState<AccountProfile>(emptyProfile);
    const [currentPassword, setCurrentPassword] = useState("");
    const [newPassword, setNewPassword] = useState("");
    const [confirmPassword, setConfirmPassword] = useState("");
    const [isLoading, setIsLoading] = useState(true);
    const [isSavingProfile, setIsSavingProfile] = useState(false);
    const [isChangingPassword, setIsChangingPassword] = useState(false);
    const [loadError, setLoadError] = useState("");
    const [profileMessage, setProfileMessage] = useState({ type: "", text: "" });
    const [passwordMessage, setPasswordMessage] = useState({ type: "", text: "" });

    useEffect(() => {
        async function loadProfile() {
            try {
                setProfile(await getAccountProfile());
            } catch (error) {
                setLoadError(error instanceof Error ? error.message : "Nie udało się pobrać profilu.");
            } finally {
                setIsLoading(false);
            }
        }

        void loadProfile();
    }, []);

    function updateField(field: "firstName" | "lastName" | "email", value: string) {
        setProfile((current) => ({ ...current, [field]: value }));
        setProfileMessage({ type: "", text: "" });
    }

    async function handleProfileSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const normalized = {
            firstName: profile.firstName.trim(),
            lastName: profile.lastName.trim(),
            email: profile.email.trim().toLowerCase(),
        };

        if (!/^\S+@\S+\.\S+$/.test(normalized.email)) {
            setProfileMessage({ type: "error", text: "Podaj poprawny adres e-mail." });
            return;
        }

        setIsSavingProfile(true);
        setProfileMessage({ type: "", text: "" });

        try {
            const saved = await updateAccountProfile(normalized);
            setProfile(saved);
            setProfileMessage({ type: "success", text: "Profil został zapisany." });
            window.dispatchEvent(new CustomEvent("drivertime:profile-updated", { detail: saved }));
        } catch (error) {
            setProfileMessage({
                type: "error",
                text: error instanceof Error ? error.message : "Nie udało się zapisać profilu.",
            });
        } finally {
            setIsSavingProfile(false);
        }
    }

    async function handlePasswordSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (newPassword.length < 8) {
            setPasswordMessage({ type: "error", text: "Nowe hasło musi miec co najmniej 8 znakow." });
            return;
        }

        if (newPassword !== confirmPassword) {
            setPasswordMessage({ type: "error", text: "Nowe hasła nie są takie same." });
            return;
        }

        setIsChangingPassword(true);
        setPasswordMessage({ type: "", text: "" });

        try {
            await changeAccountPassword({ currentPassword, newPassword });
            setCurrentPassword("");
            setNewPassword("");
            setConfirmPassword("");
            setPasswordMessage({ type: "success", text: "hasło zostało zmienione." });
        } catch (error) {
            setPasswordMessage({
                type: "error",
                text: error instanceof Error ? error.message : "Nie udało się zmienić hasła.",
            });
        } finally {
            setIsChangingPassword(false);
        }
    }

    if (isLoading) {
        return (
            <div className="account-page" aria-busy="true">
                <div className="account-skeleton ui-skeleton" />
                <div className="account-skeleton ui-skeleton" />
            </div>
        );
    }

    if (loadError) {
        return <p className="account-message error" role="alert">{loadError}</p>;
    }

    return (
        <div className="account-page">
            <div className="account-heading">
                <h2>Moje konto</h2>
                <p>zarządzaj danymi profilu i hasłem dostępu.</p>
            </div>

            <div className="account-grid">
                <form className="account-card" onSubmit={handleProfileSubmit}>
                    <div className="account-card-heading">
                        <h3>Dane profilu</h3>
                        <p>{profile.companyName} / {formatRole(profile.role)}</p>
                    </div>

                    <label>
                        Imie
                        <input value={profile.firstName} onChange={(event) => updateField("firstName", event.target.value)} maxLength={100} />
                    </label>
                    <label>
                        Nazwisko
                        <input value={profile.lastName} onChange={(event) => updateField("lastName", event.target.value)} maxLength={100} />
                    </label>
                    <label>
                        Email <span aria-hidden="true">*</span>
                        <input type="email" value={profile.email} onChange={(event) => updateField("email", event.target.value)} required maxLength={320} />
                    </label>

                    {profileMessage.text && <p className={`account-message ${profileMessage.type}`} role={profileMessage.type === "error" ? "alert" : "status"}>{profileMessage.text}</p>}

                    <button type="submit" disabled={isSavingProfile}>
                        {isSavingProfile ? "Zapisywanie..." : "Zapisz profil"}
                    </button>
                </form>

                <form className="account-card" onSubmit={handlePasswordSubmit}>
                    <div className="account-card-heading">
                        <h3>Zmiana hasła</h3>
                        <p>Nowe hasło musi miec co najmniej 8 znakow.</p>
                    </div>

                    <label>
                        Aktualne hasło
                        <input type="password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} required autoComplete="current-password" />
                    </label>
                    <label>
                        Nowe hasło
                        <input type="password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} required minLength={8} maxLength={200} autoComplete="new-password" />
                    </label>
                    <label>
                        powtórz nowe hasło
                        <input type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} required minLength={8} maxLength={200} autoComplete="new-password" />
                    </label>

                    {passwordMessage.text && <p className={`account-message ${passwordMessage.type}`} role={passwordMessage.type === "error" ? "alert" : "status"}>{passwordMessage.text}</p>}

                    <button type="submit" disabled={isChangingPassword}>
                        {isChangingPassword ? "Zmienianie..." : "Zmień hasło"}
                    </button>
                </form>
            </div>
        </div>
    );
}
