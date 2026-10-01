# DriverTime Mobile iOS

SwiftUI client for manual driver activity registration synchronized with the DriverTime API.

## Requirements

- macOS with Xcode 16 or newer
- XcodeGen

Install XcodeGen:

```bash
brew install xcodegen
```

Generate and open the project:

```bash
cd src/DriverTime.Ios
xcodegen generate
open DriverTimeMobile.xcodeproj
```

## API synchronization

The app uses the existing DriverTime endpoints:

- `POST /api/auth/login`
- `GET /api/auth/me`
- `GET /api/drivers`
- `GET /api/drivers/{driverId}/work-evidence`
- `POST /api/drivers/{driverId}/work-evidence/entries`
- `DELETE /api/work-evidence/entries/{entryId}`

Set the API address on the login screen, for example:

```text
https://twoja-domena.pl
```

Do not add `/api` at the end. The mobile client adds API paths itself.

## Current scope

- Login with DriverTime account.
- Assign the device to exactly one driver from the current company.
- Add multiple manual activities per day.
- Start/stop quick activity timer.
- Pull current monthly evidence from DriverTime.
- Keep unsent entries in a local pending queue and retry synchronization.
- Delete synchronized entries.

## Driver assignment

After the first login the app asks which driver this iPhone belongs to. The selected driver is stored on the device and every synchronization request uses only that driver's `DriverId`.

The user can clear the assignment in Settings and choose another driver. For production-grade isolation this should be extended with a backend-issued driver pairing token so the API also enforces the driver scope server-side.

The app intentionally does not implement planning or reports. It is a focused mobile capture client for DriverTime work evidence.
