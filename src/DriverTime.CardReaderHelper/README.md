# DriverTime Card Reader Helper

Ten projekt to lokalny helper MVP dla przyszłego fizycznego odczytu kart kierowców.

## Jak działa

- Helper uruchamia się lokalnie na komputerze użytkownika.
- Nasłuchuje pod adresem `http://localhost:47888`.
- Aplikacja webowa DriverTime komunikuje się z helperem przez `localhost`.
- Pełny odczyt zapisuje lokalną kopię `.ddd`, a aplikacja webowa automatycznie przekazuje ją do istniejącego importu DriverTime.

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
- `POST /api/card/read/ddd` - odczytuje przez PC/SC wymagane pliki EF karty kierowcy, zapisuje plik `.ddd` i zwraca jego zawartość aplikacji webowej do automatycznego importu.

## Obecne ograniczenia

Tryb testowy/mock nie komunikuje się z kartą i nie generuje pliku DDD. Pełny odczyt wymaga fizycznego czytnika i karty kierowcy. Helper pobiera transparentne pliki EF pierwszej generacji aplikacji tachografu. Karty drugiej generacji wymagające bezpiecznej komunikacji dla danych G2 mogą udostępnić tylko zgodną część pierwszej generacji.

## Ważne

Helper jest osobnym procesem lokalnym. Korzysta z istniejącego, uwierzytelnionego endpointu importu DDD. Dzięki temu nowy kierowca jest przypisywany do firmy konta, na którym wykonano odczyt, a kolejna karta tej samej osoby jest łączona z istniejącym kierowcą przez standardową logikę importu.
