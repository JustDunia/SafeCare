-- SQL Seed Script for SafeCare Database
-- This script inserts the dictionary data the public form depends on: Departments and
-- IncidentDefinitions. It deliberately seeds no IncidentReports — those were demo filler.
--
-- Do not reintroduce rows with explicit "Id" values for a table the application also writes
-- to. PostgreSQL does not advance an identity sequence when an id is supplied, so the seeded
-- rows and the application's first insert collide on the same id. The demo reports did
-- exactly that: on a freshly seeded database the public form rejected every submission with
-- a duplicate key violation until the sequence caught up.

-- =============================================================================
-- DEPARTMENTS
-- =============================================================================
INSERT INTO "Departments" ("Id", "Name", "Code") VALUES
(1, 'Ortopedia', 'ORT'),
(2, 'Pediatria', 'PED'),
(3, 'Kardiologia', 'KAR'),
(4, 'Neurologia', 'NEU'),
(5, 'Chirurgia', 'CHI'),
(6, 'Ginekologia', 'GIN'),
(7, 'Onkologia', 'ONK'),
(8, 'Dermatologia', 'DER'),
(9, 'Psychiatria', 'PSY'),
(10, 'Radiologia', 'RAD');

-- =============================================================================
-- INCIDENT DEFINITIONS
-- =============================================================================
INSERT INTO "IncidentDefinitions" ("Id", "Name", "Category") VALUES
(1, 'ciało obce pozostawione w polu operacyjnym', 'Clinical'),
(2, 'mylna identyfikacja pacjenta', 'Clinical'),
(3, 'mylna identyfikacja procedury', 'Clinical'),
(4, 'mylna identyfikacja miejsca operowanego', 'Clinical'),
(5, 'błędna diagnoza', 'Clinical'),
(6, 'embolia płucna po zabiegu operacyjnym', 'Clinical'),
(7, 'niedostarczenie opieki', 'Clinical'),
(8, 'dostarczenie niewłaściwej opieki', 'Clinical'),
(9, 'pomyłka w podawaniu leku: nie ten lek', 'Pharmacotherapy'),
(10, 'pomyłka w podawaniu leku: niewłaściwa dawka', 'Pharmacotherapy'),
(11, 'pomyłka w podawaniu leku: niewłaściwy czas podania leku', 'Pharmacotherapy'),
(12, 'pomyłka w podawaniu leku: niewłaściwa droga podania leku', 'Pharmacotherapy'),
(13, 'pomyłka w podawaniu leku: niewłaściwy rozpuszczalnik', 'Pharmacotherapy'),
(14, 'pomyłka w podawaniu leku: podanie leku po upływie terminu ważności', 'Pharmacotherapy'),
(15, 'niepożądane działanie leku/ reakcja alergiczna', 'Pharmacotherapy'),
(16, 'niewłaściwa identyfikacja pacjenta przed przetoczeniem', 'Transfusion'),
(17, 'podanie niewłaściwej jednostki', 'Transfusion'),
(18, 'inne działania związane z przetoczeniem krwi i preparatów krwiopochodnych', 'Transfusion'),
(19, 'niewłaściwe wskazania i odstąpienie od przetoczenia', 'Transfusion'),
(20, 'awarie sprzętu', 'Operational'),
(21, 'niewłaściwa identyfikacja pacjenta', 'Operational'),
(22, 'nieodpowiednia organizacja pracy', 'Operational');
