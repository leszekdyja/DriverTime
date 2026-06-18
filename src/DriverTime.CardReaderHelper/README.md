# DriverTime Card Reader Helper

Ten projekt to lokalny helper MVP dla przyszłego fizycznego odczytu kart kierowców.

## Jak działa

- Helper uruchamia się lokalnie na komputerze użytkownika.
- Nasłuchuje pod adresem `http://localhost:47888`.
- Aplikacja webowa DriverTime może komunikować się z helperem przez `localhost`.
- Obecna wersja działa w trybie bezpiecznym/mock i nie wykonuje realnego odczytu PC/SC ani APDU.

## Endpointy MVP

- `GET /health` - sprawdza, czy helper działa.
- `GET /api/readers` - zwraca informację o trybie mock i pustą listę czytników.
- `POST /api/card/read/start` - zwraca testowy wynik odczytu karty.

## Co będzie dalej

Kolejny etap doda realną obsługę PC/SC, wykrywanie czytników i fizyczny odczyt karty kierowcy.

## Ważne

Obecny upload/import plików DDD w głównej aplikacji pozostaje bez zmian. Helper jest osobnym procesem lokalnym i nie modyfikuje istniejącego endpointu importu DDD.
