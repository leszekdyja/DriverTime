import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { useMobileLayout } from "../../hooks/useMobileLayout";

export function DesktopOnly({ children, reader = false }: { children: ReactNode; reader?: boolean }) {
    const mobile = useMobileLayout();
    if (!mobile) return children;
    return <section className="desktop-only-message">
        <span aria-hidden="true">▤</span>
        <h2>{reader ? "Odczyt karty na komputerze" : "Grafik dostępny na komputerze"}</h2>
        <p>{reader ? "Czytnik wymaga lokalnego helpera na komputerze z podłączonym urządzeniem. Na telefonie możesz wczytać gotowy plik DDD i przeglądać wyniki."
            : "Tworzenie, przeglądanie i edycja grafiku pozostają w wersji komputerowej DriverTime. Otwórz tę samą stronę na komputerze."}</p>
        <Link to={reader ? "/imports" : "/"}>{reader ? "Przejdź do importów DDD" : "Wróć do dashboardu"}</Link>
    </section>;
}
