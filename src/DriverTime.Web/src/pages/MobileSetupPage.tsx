import "../styles/mobile-setup.css";

export default function MobileSetupPage() {
    const parameters = new URLSearchParams(window.location.search);
    const apiUrl = parameters.get("apiUrl") ?? "";
    const token = parameters.get("token") ?? "";
    const deepLink = `drivertime://mobile-setup?apiUrl=${encodeURIComponent(apiUrl)}&token=${encodeURIComponent(token)}`;
    const apkUrl = "/downloads/drivertime-driver.apk";
    const isValid = apiUrl.length > 0 && token.length > 0;

    return (
        <main className="mobile-setup-page">
            <section className="mobile-setup-panel">
                <div>
                    <p className="mobile-setup-eyebrow">DriverTime</p>
                    <h1>Aplikacja kierowcy</h1>
                    <p className="mobile-setup-lead">
                        Ten link zawiera konfigurację aplikacji dla jednego kierowcy.
                    </p>
                </div>

                {!isValid ? (
                    <p className="mobile-setup-error">
                        Link konfiguracji jest niepełny. Wygeneruj nowy link w systemie DriverTime.
                    </p>
                ) : (
                    <>
                        <div className="mobile-setup-steps">
                            <article>
                                <span>1</span>
                                <div>
                                    <h2>Pobierz aplikację</h2>
                                    <p>Zainstaluj aplikację DriverTime na telefonie z Androidem.</p>
                                    <a className="mobile-setup-primary" href={apkUrl} download>
                                        Pobierz APK
                                    </a>
                                </div>
                            </article>

                            <article>
                                <span>2</span>
                                <div>
                                    <h2>Uruchom konfigurację</h2>
                                    <p>Po instalacji wróć do tej strony i otwórz konfigurację.</p>
                                    <a className="mobile-setup-secondary" href={deepLink}>
                                        Otwórz w aplikacji
                                    </a>
                                </div>
                            </article>
                        </div>

                        <p className="mobile-setup-note">
                            Konfiguracja przypisze telefon tylko do wskazanego kierowcy.
                        </p>
                    </>
                )}
            </section>
        </main>
    );
}
