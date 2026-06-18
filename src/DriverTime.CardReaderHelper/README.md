# DriverTime Card Reader Helper

Ten projekt to lokalny helper MVP dla przyszłego fizycznego odczytu kart kierowców.

## Jak działa

- Helper uruchamia się lokalnie na komputerze użytkownika.
- Nasłuchuje pod adresem `http://localhost:47888`.
- Aplikacja webowa DriverTime komunikuje się z helperem przez `localhost`.
- Obecny upload/import plików DDD w głównej aplikacji pozostaje bez zmian.

## Wymagania lokalne

- System Windows.
- Włączona usługa Windows Smart Card / Karta inteligentna.
- Podłączony fizyczny czytnik kart inteligentnych zgodny z PC/SC.
- Sterowniki czytnika zainstalowane w systemie, jeśli Windows ich nie wykrywa automatycznie.

## Endpointy MVP

- `GET /health` - sprawdza, czy helper działa, czy PC/SC jest dostępne i ile czytników wykryto.
- `GET /api/readers` - zwraca realnie wykryte czytniki PC/SC oraz informację, czy karta jest obecna, jeśli system pozwala to ustalić.
- `GET /api/readers/{readerName}/atr` - łączy się z kartą w wybranym czytniku i odczytuje ATR, czyli podstawową odpowiedź identyfikującą kartę.
- `POST /api/card/read/start` - działa nadal w trybie testowym i nie wykonuje realnego odczytu danych DDD/C1B.

## Obecne ograniczenia

Ten etap wykrywa lokalne czytniki PC/SC i testuje obecność karty przez odczyt ATR. Helper nadal nie pobiera danych z karty kierowcy, nie wykonuje komend APDU `SELECT` i nie zapisuje pliku DDD/C1B. Realny odczyt karty zostanie dodany w kolejnym kroku.

## Ważne

Helper jest osobnym procesem lokalnym. Nie modyfikuje istniejącego endpointu importu DDD i nie zmienia obecnego przepływu uploadu plików w DriverTime.
