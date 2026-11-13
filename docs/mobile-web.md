# DriverTime mobile web

## Scope

The existing web application adapts to narrow viewports (up to 900 CSS pixels)
and primary coarse-pointer devices without hover. The desktop layout remains
available otherwise. This is responsive presentation, not a security boundary.
Authentication and company restrictions continue to be enforced by the API.

**User requirement: the planning schedule stays on computers only.**
Mobile navigation excludes `/planning`, and a direct route renders an explanatory
message before the lazy planning page can mount or fetch its data. Planning
toggles on the driver list are hidden on mobile. No planning algorithm, assignment,
schedule editor, synchronisation or PDF-generation logic was changed.

The card-reader route also requires the desktop: the existing PC/SC helper lives
on that computer's localhost. Mobile users can import existing DDD files instead.

## Interface

- Four primary links and an accessible native-dialog More menu; the same account
  permissions filter the navigation. The current company remains visible.
- Driver and vehicle tables become labelled cards. Inputs and main controls have
  touch-sized targets, with safe-area spacing for notches and home indicators.
- Tachograph charts default to scrolling on mobile. Range selection is an explicit
  separate mode; zoom and exact time entry remain available. Time interpretation
  and compliance calculations were not changed.
- Route-level loading reduces initial JavaScript. An error boundary provides a
  user-initiated reload if a chunk fails to load after an update or network loss.

## Home-screen installation and data

The manifest launches the normal authenticated online application in standalone
mode. App icons are generated at build time (192/512 PNG), also served in Vite dev.
There is no service worker, offline database, background synchronisation, push
subscription or automatic replay of writes. No driver records are downloaded for
offline use. The server marks API responses `no-store`; hashed static assets alone
can be cached. The manifest and HTML revalidate. Synchronisation stays manual.

## Verification

- `npm run build`
- `node --experimental-strip-types --test tests/*.test.mjs`
- `npx eslint src/components/mobile src/hooks/useMobileLayout.ts scripts/appIcons.ts vite.config.ts`
- `tests/mobile-preview.html` through Vite uses fictitious records and intercepts
  all API calls; writes fail explicitly. This fixture is not a production entry.
- Browser checks: 320/390/768 px lists without document-wide horizontal overflow,
  More menu open/close, light/dark themes, chart mode/zoom, mobile planning block
  and desktop guard pass-through at 1440 px.

These are browser viewport checks, not certification on physical iPhones,
Android devices or all Safari versions. Device-specific installation and gesture
behaviour should also be checked on the user's actual devices.
