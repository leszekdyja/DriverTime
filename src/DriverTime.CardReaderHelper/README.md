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
- Podłączony fizyczny czytnik kart inteligentnych zgodny z PC/SC, np. ACS ACR39U.
- Sterowniki czytnika zainstalowane w systemie, jeśli Windows ich nie wykrywa automatycznie.

## Endpointy MVP

- `GET /health` - sprawdza, czy helper działa, czy PC/SC jest dostępne i ile czytników wykryto.
- `GET /api/readers` - zwraca wykryte czytniki PC/SC oraz informację, czy karta jest obecna, jeśli system pozwala to ustalić.
  Jeżeli nie ma fizycznego czytnika, endpoint zwraca jawnie oznaczony czytnik testowy, który służy tylko do sprawdzenia interfejsu i historii sesji.
- `GET /api/diagnostics` - zwraca szczegółową diagnostykę PC/SC: listę czytników, status połączenia, ATR, protokół i komunikaty błędów.
- `GET /api/readers/{readerName}/atr` - łączy się z kartą w wybranym czytniku i odczytuje ATR, czyli podstawową odpowiedź identyfikującą kartę.
- `GET /api/reader-status?readerName=...` - zwraca bieżący stan czytnika i karty: nazwę czytnika, informację czy czytnik jest podłączony, czy karta jest włożona, ATR oraz flagę trybu testowego.
- `POST /api/card/read/start` - działa nadal w trybie testowym i nie wykonuje realnego odczytu danych DDD/C1B.
  Endpoint może działać bez fizycznego czytnika i zwraca wynik mockowy z nazwą testowego pliku.

## Obecne ograniczenia

Ten etap wykrywa lokalne czytniki PC/SC i testuje obecność karty przez odczyt ATR. Gdy czytnika nie ma, można użyć trybu testowego/mock, który nie komunikuje się z kartą i nie generuje prawdziwego pliku DDD/C1B. Helper nadal nie pobiera danych z karty kierowcy, nie wykonuje komend APDU `SELECT` i nie zapisuje pliku DDD/C1B. Realny odczyt karty zostanie dodany w kolejnym kroku.

## Ważne

Helper jest osobnym procesem lokalnym. Nie modyfikuje istniejącego endpointu importu DDD i nie zmienia obecnego przepływu uploadu plików w DriverTime.
