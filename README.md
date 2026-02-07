# Dokumentacja Projektowa Systemu Zgłaszania Zdarzeń Niepożądanych (SafeCare)
Autor: Justyna Sienkiewicz (justyna.sienkiewicz.stud@pw.edu.pl)
## 1. Wstęp i opis działania serwisu

### 1.1. Cel projektu
Celem projektu było stworzenie aplikacji internetowej „SafeCare”, służącej do elektronicznego zgłaszania zdarzeń niepożądanych w placówce medycznej. System ma na celu usprawnienie komunikacji między pacjentami/opiekunami a personelem szpitala oraz zwiększenie bezpieczeństwa pacjentów poprzez identyfikację i analizę błędów medycznych oraz organizacyjnych.
Z punktu widzenia pacjenta istotne jest, aby formularz umożliwiający zgłoszenie zdarzeń niepożądanych był prosty, czytelny oraz dostępny na urządzeniach moblinych.

### 1.2. Technologia
Aplikacja została zrealizowana w oparciu o nowoczesne technologie webowe:

- Framework: .NET 10 / Blazor Web App.
- Model renderowania: Interactive Server Rendering (zapewniający dynamiczną reaktywność interfejsu).
- Baza danych: PostgreSQL.
- ORM: Entity Framework Core (podejście Code-First).

### 1.3 Bezpieczeństwo
Aplikacja została zabezpieczona m.in. poprzez:
- Requeset Limit: ograniczenia liczby zapytań przesyłanych do serwera z jednego adresu IP w określonej jednostce czasu. Należy jednak pamiętać, że w przypadku wybranej technologii zapytanie HTTP jest wykonywane jednorazowo podczas ładowania aplikacji, po czym wszelkie działania wykonywane są przez połączenia SignalR. Wyjątek stanowi wykonanie logowania i wylogowania, które wymaga oddzielnego zapytania HTTP w celu prawidłowego ustanowienia poświadczeń.
- Zabezpieczenie przeciw botom wypełniającym formularz:
  - HoneyPot: pola formularza niewidoczne dla użytkownika, ale czytelne dla botów, celowo upodobnione do typowych pól formularza (np. pole adresu)
  - Minimalny czas wypełnienia formualrza: dla typowego użytkownika niemożliwe jest uzupełnienie formularza w ciągu kilku sekund. Jeżeli formularz zostanie wypełniony bardzo szybko, zostanie on zablokowany jako podejrzenie udziału bota.

### 1.4. Funkcjonalności systemu
System został podzielony na dwa główne moduły: moduł publiczny (dla zgłaszającego) oraz moduł administracyjny (dla personelu).

#### A. Moduł Publiczny (Dostęp bez logowania)
Dedykowany pacjentom oraz ich opiekunom. Użytkownik nie musi zakładać konta, aby dokonać zgłoszenia. Proces ten realizowany jest poprzez wieloetapowy formularz (Wizard), który zbiera następujące dane:

- Dane osoby zgłaszającej: Imię, nazwisko, kontakt.
- Dane pacjenta: Imię, nazwisko, data urodzenia, płeć (jeśli zgłoszenie dotyczy innej osoby).
- Czas i miejsce: Data i godzina zdarzenia (lub zakres dat od-do), oddział szpitalny (wybierany ze słownika).
- Rodzaj zdarzenia: Kategoryzacja problemu (np. działalność kliniczna, farmakoterapia, sprzęt).
- Możliwość wyboru wielu zdarzeń w ramach kilku kategorii i/lub dodanie innego rodzaju zdarzenia
- Opis szczegółowy: Pole tekstowe na dokładny opis sytuacji.

#### B. Moduł Administracyjny (Dashboard) 
Dostępny wyłącznie dla autoryzowanych pracowników szpitala. Po zalogowaniu użytkownik otrzymuje dostęp do:

- Listy zgłoszeń: Tabela prezentująca wszystkie zgłoszenia z możliwością ich sortowania oraz filtrowania.
- Mechanizmu stronicowania: Pozwala na wygodne przeglądanie dużej liczby rekordów.
- Trwałość stanu widoku: Parametry filtrowania, sortowania i numer strony są zapisywane w adresie URL (Query Strings). Dzięki temu odświeżenie strony lub przesłanie linku innemu pracownikowi nie powoduje utraty kontekstu pracy.
- Szczegółów zgłoszenia: Widok pozwalający na analizę pełnych danych zgłoszenia, zmianę jego statusu (np. Nowe, W trakcie rozpatrywania, Zakończone, Odrzucone) lub usunięcie rekordu z bazy.

## 2. Dane dostępowe (Login i Hasło)
Do celów testowych i weryfikacji projektu utworzono konto administratora z następującymi poświadczeniami:

- Login / Nazwa użytkownika: admin
- Hasło: Admin123!

## 3. Zrzuty ekranu
Poniżej przedstawiono kluczowe widoki aplikacji, obrazujące ścieżkę użytkownika niezalogowanego oraz panel administracyjny.

### 3.1. Widok przed zalogowaniem (Formularz zgłoszeniowy)

![](./img/Zrzut%20ekranu%202026-02-07%20225641.png)
Rys. 1. Ekran startowy formularza – Dane osoby zgłaszającej i pacjenta.

![](./img/Zrzut%20ekranu%202026-02-07%20225655.png)
Rys. 2. Wybór czasu, miejsca i kategorii zdarzenia.

![](./img/Zrzut%20ekranu%202026-02-07%20231241.png)
Rys. 3. Szczegółowy wybór rodzaju zdarzenia w ramach wybranych kategorii

![](./img/Zrzut%20ekranu%202026-02-07%20231351.png)
Rys. 4. Finalizacja zgłoszenia – dokładny opis.

### 3.2. Logowanie do systemu

![](./img/Zrzut%20ekranu%202026-02-07%20225737.png)
Rys. 5. Panel logowania dla personelu.

### 3.3. Widok po zalogowaniu (Panel Administratora)
![](./img/Zrzut%20ekranu%202026-02-07%20225747.png)
Rys. 6. Dashboard – Tabela zgłoszeń z funkcją sortowania i filtrowania.

![](./img/Zrzut%20ekranu%202026-02-07%20225849.png)
Rys. 7. Widok szczegółów zgłoszenia i edycja statusu.

### 3.4 Widok mobilny
![](/img/Zrzut%20ekranu%202026-02-07%20232022.png)
Rys. 8. Widok mobilny formularza

![](./img/Zrzut%20ekranu%202026-02-07%20232109.png)
Rys. 9. Widok mobilny panelu logowania oraz panelu administratora

## 4. Opis struktury bazy danych i relacji
Baza danych została zaprojektowana w oparciu o paradygmat relacyjny z wykorzystaniem Entity Framework Core. Struktura składa się z tabel systemowych (Identity) oraz tabel domenowych obsługujących logikę zgłoszeń.

### 4.1. Tabele tożsamości (Identity)
Aplikacja wykorzystuje standardowy mechanizm ASP.NET Core Identity do zarządzania uwierzytelnianiem.

- AspNetUsers: Przechowuje dane użytkowników (administratorów), m.in. login, skrót hasła, e-mail.
- AspNetRoles: Przechowuje role w systemie (np. Administrator).
- Tabele powiązane (AspNetUserRoles, AspNetUserClaims itp.) zarządzają relacjami między użytkownikami a ich uprawnieniami.

W pierwszym etapie projektu wykorzystana została jedynie tabela AspNetUsers. Nadawanie ról nie było na tym etapie konieczne, ponieważ zakładamy że istnieje podział jedynie na użytkowników zalogowanych oraz niezalogowanych.

### 4.2. Tabele domenowe i relacje
Główna logika biznesowa opiera się na następujących encjach:

- IncidentReports (Zgłoszenia): Jest to główna tabela przechowująca informacje o zgłoszeniu (daty, dane osobowe, opis, status).
- Relacja Jeden-do-Wielu z Departments: Każde zgłoszenie jest przypisane do jednego oddziału (klucz obcy DepartmentId). Jeden oddział może mieć wiele zgłoszeń.
- Departments (Oddziały): Tabela słownikowa przechowująca listę oddziałów szpitalnych (np. Ortopedia, Pediatria). Służy do normalizacji danych i ułatwia raportowanie według jednostek organizacyjnych.
- IncidentDefinitions (Definicje zdarzeń): Tabela słownikowa zawierająca zdefiniowane typy zdarzeń niepożądanych (np. "niewłaściwa identyfikacja pacjenta", "podanie niewłaściwej jednostki krwi").
- IncidentDefinitionIncidentReport (Tabela łącząca): Relacja Wiele-do-Wielu łącząca IncidentReports oraz IncidentDefinitions. Jedno zgłoszenie (IncidentReport) może dotyczyć kilku różnych definicji problemów jednocześnie (np. błąd sprzętowy ORAZ błąd ludzki), a jedna definicja zdarzenia może wystąpić w wielu różnych raportach.
