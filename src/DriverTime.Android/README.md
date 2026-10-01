# DriverTime Android

Native Android client for driver activity registration synchronized with DriverTime.

## Scope

- Login to DriverTime API.
- Assign the device to exactly one driver from the current company.
- Synchronize only the assigned driver's work evidence.
- Add multiple activities per day.
- Quick start/stop activity timer.
- Manual activity form.
- Local pending queue for entries that cannot be synchronized immediately.

## API endpoints

- `POST /api/auth/login`
- `GET /api/drivers`
- `GET /api/drivers/{driverId}/work-evidence`
- `POST /api/drivers/{driverId}/work-evidence/entries`
- `DELETE /api/work-evidence/entries/{entryId}`

## Build

Open this folder in Android Studio:

```text
src/DriverTime.Android
```

Then sync Gradle and run the `app` configuration.

The app asks for the DriverTime base URL on login, for example:

```text
https://twoja-domena.pl
```

Do not add `/api`; the app appends API paths itself.

## Driver assignment

After login the user must choose a driver. The selected driver is stored locally and every sync request uses only that `DriverId`.

For production-grade isolation, add server-side driver device pairing or driver-scoped tokens. The current Android app enforces the scope client-side.
