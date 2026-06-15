# DriverTime - instrukcje dla agentów AI / Codex

## Cel projektu

DriverTime to aplikacja do zarz¹dzania transportem i analiz¹ plików DDD z kart kierowców oraz tachografów.

Aplikacja ma dzia³aæ najpierw lokalnie, bez logowania u¿ytkowników. Logowanie, konta i role dodamy póŸniej.

## Technologia

Backend:
- ASP.NET Core
- Clean Architecture
- Entity Framework Core
- PostgreSQL
- API REST
- Swagger

Frontend:
- React
- TypeScript
- Vite
- komunikacja z backendem przez API

Parser:
- Pythonowy parser DDD uruchamiany z backendu
- backend zapisuje wynik importu do bazy danych

## Zasady pracy

1. Nie commitowaæ bezpoœrednio do ga³êzi main.
2. Ka¿de zadanie robiæ na osobnej ga³êzi.
3. Ka¿da zmiana powinna koñczyæ siê Pull Requestem.
4. Przed PR uruchomiæ:
   - dotnet build
   - dotnet test, jeœli testy istniej¹
   - npm install, jeœli trzeba
   - npm run build dla frontendu
5. Nie usuwaæ istniej¹cych funkcji bez wyraŸnego powodu.
6. Nie zmieniaæ architektury projektu bez potrzeby.
7. Kod ma byæ prosty, czytelny i stabilny.
8. Najpierw aplikacja ma dzia³aæ, wygl¹d poprawimy póŸniej.

## Priorytety najbli¿szych prac

1. Frontend importu plików DDD w React.
2. Lista importów DDD.
3. Szczegó³y importu:
   - dane kierowcy
   - aktywnoœci kierowcy
   - kraje
   - pojazdy
4. Dashboard:
   - liczba importów
   - liczba kierowców
   - ostatnie importy
   - podstawowe statystyki aktywnoœci
5. Raporty:
   - aktywnoœci kierowcy
   - naruszenia/czas pracy póŸniej
   - eksport PDF póŸniej

## Styl kodu

- Podawaj pe³ne pliki, nie fragmenty.
- Zachowuj istniej¹ce namespace i strukturê katalogów.
- Backend: osobne DTO, serwisy i kontrolery.
- Frontend: komponenty czytelne, bez nadmiernego komplikowania.
- Nazwy po angielsku w kodzie.
- Komentarze tylko tam, gdzie pomagaj¹.

## Zakazy

- Nie dodawaæ logowania teraz.
- Nie dodawaæ p³atnoœci.
- Nie dodawaæ mikroserwisów.
- Nie przepisywaæ ca³ego projektu od zera.
- Nie zmieniaæ bazy danych bez migracji EF Core.
- Nie commitowaæ plików bin, obj, node_modules ani build output.

## Docelowy tryb pracy

U¿ytkownik chce pracowaæ tak:

1. AI/Codex dostaje zadanie.
2. AI tworzy branch.
3. AI robi kod.
4. AI uruchamia build/testy.
5. AI tworzy PR.
6. U¿ytkownik sprawdza i zatwierdza.
7. Dopiero potem merge do main.