import { useSyncExternalStore } from "react";

export const mobileLayoutQuery = "(max-width: 900px), (hover: none) and (pointer: coarse)";

function subscribe(callback: () => void) {
    const query = window.matchMedia(mobileLayoutQuery);
    query.addEventListener("change", callback);
    return () => query.removeEventListener("change", callback);
}

export function useMobileLayout() {
    return useSyncExternalStore(subscribe, () => window.matchMedia(mobileLayoutQuery).matches, () => false);
}
