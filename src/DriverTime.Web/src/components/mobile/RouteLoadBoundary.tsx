import { Component, type ReactNode } from "react";

export class RouteLoadBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
    state = { failed: false };
    static getDerivedStateFromError() { return { failed: true }; }
    render() {
        if (!this.state.failed) return this.props.children;
        return <section className="desktop-only-message" role="alert">
            <h2>Nie udało się otworzyć strony</h2>
            <p>Sprawdź połączenie z internetem. Po aktualizacji systemu może być potrzebne ponowne wczytanie aplikacji.</p>
            <p>Odświeżenie usunie niezapisane zmiany w otwartych formularzach.</p>
            <button type="button" onClick={() => window.location.reload()}>Wczytaj aplikację ponownie</button>
        </section>;
    }
}
