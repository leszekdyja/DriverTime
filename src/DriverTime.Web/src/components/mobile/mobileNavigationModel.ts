export type NavigationItem = { to: string; label: string; icon: string };

export const mobilePrimaryPaths = ["/", "/alerts", "/drivers", "/vehicles"];
export const desktopOnlyPaths = ["/planning", "/card-reader"];

export function getMobileNavigation(items: NavigationItem[]) {
    const allowed = items.filter(item => !desktopOnlyPaths.includes(item.to));
    return {
        primary: mobilePrimaryPaths.flatMap(path => allowed.filter(item => item.to === path)),
        more: allowed.filter(item => !mobilePrimaryPaths.includes(item.to)),
    };
}

export function matchesNavigationPath(pathname: string, path: string) {
    return path === "/" ? pathname === "/" : pathname === path || pathname.startsWith(`${path}/`);
}
