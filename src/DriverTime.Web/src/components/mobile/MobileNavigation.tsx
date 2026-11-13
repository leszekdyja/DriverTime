import { useEffect, useRef, useState, type ReactNode } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { getMobileNavigation, matchesNavigationPath, type NavigationItem } from "./mobileNavigationModel";

type Props = { items: NavigationItem[]; icon: (name: string) => ReactNode };

export function MobileNavigation({ items, icon }: Props) {
    const { primary, more } = getMobileNavigation(items);
    const location = useLocation();
    const [open, setOpen] = useState(false);
    const dialog = useRef<HTMLDialogElement>(null);
    const moreActive = more.some(item => matchesNavigationPath(location.pathname, item.to));

    useEffect(() => { setOpen(false); }, [location.pathname]);
    useEffect(() => {
        if (open && !dialog.current?.open) dialog.current?.showModal();
        if (!open && dialog.current?.open) dialog.current.close();
        if (!open) return;
        const previous = document.body.style.overflow;
        document.body.style.overflow = "hidden";
        return () => { document.body.style.overflow = previous; };
    }, [open]);

    return <>
        <nav className="mobile-bottom-nav" aria-label="Nawigacja mobilna">
            {primary.map(item => <NavLink key={item.to} to={item.to} end={item.to === "/"}
                className={({ isActive }) => `mobile-nav-item${isActive ? " active" : ""}`}>
                {icon(item.icon)}<span>{item.label}</span>
            </NavLink>)}
            <button className={`mobile-nav-item${open || moreActive ? " active" : ""}`} type="button"
                aria-haspopup="dialog" aria-expanded={open} aria-controls="mobile-more-menu" onClick={() => setOpen(true)}>
                <span className="mobile-more-icon" aria-hidden="true">•••</span><span>Więcej</span>
            </button>
        </nav>
        <dialog ref={dialog} id="mobile-more-menu" className="mobile-more-dialog" aria-labelledby="mobile-menu-title"
            onCancel={() => setOpen(false)} onClose={() => setOpen(false)} onClick={event => {
                if (event.target !== event.currentTarget) return;
                const bounds = event.currentTarget.getBoundingClientRect();
                if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) setOpen(false);
            }}>
            <header><h2 id="mobile-menu-title">DriverTime — więcej</h2>
                <button type="button" onClick={() => setOpen(false)} aria-label="Zamknij menu">✕</button></header>
            <nav aria-label="Pozostałe zakładki">
                {more.map(item => <NavLink key={item.to} to={item.to} onClick={() => setOpen(false)}
                    className={({ isActive }) => `mobile-more-link${isActive ? " active" : ""}`}>
                    {icon(item.icon)}<span>{item.label}</span><span aria-hidden="true">›</span>
                </NavLink>)}
            </nav>
            <p className="mobile-menu-note">Planowanie grafiku i obsługa czytnika są dostępne na komputerze.</p>
            <details className="mobile-install-help"><summary>Dodaj DriverTime do ekranu głównego</summary>
                <p>Otwórz menu przeglądarki lub opcję Udostępnij i wybierz dodanie do ekranu głównego. Nazwa opcji zależy od przeglądarki.</p>
                <p>DriverTime wymaga internetu. Dane kierowców i grafiki nie są pobierane do pracy offline.</p>
            </details>
        </dialog>
    </>;
}
