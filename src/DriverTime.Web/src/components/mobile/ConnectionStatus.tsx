import { useEffect, useState } from "react";

export function ConnectionStatus() {
    const [online, setOnline] = useState(() => navigator.onLine);
    useEffect(() => {
        const update = () => setOnline(navigator.onLine);
        window.addEventListener("online", update);
        window.addEventListener("offline", update);
        return () => { window.removeEventListener("online", update); window.removeEventListener("offline", update); };
    }, []);
    return online ? null : <p className="connection-status" role="status">Brak połączenia z internetem. Dane mogą być nieaktualne. Zapisy i importy wymagają połączenia — nie są kolejkowane do automatycznej synchronizacji.</p>;
}
