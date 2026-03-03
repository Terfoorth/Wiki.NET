-- Datenkorrektur für Legacy-Daten mit fehlender AuthorId.
-- Ziel:
--   Wenn AuthorId leer ist, wird sie aus OwnerId nachgezogen,
--   damit Autor-Informationen in der UI konsistent angezeigt werden können.

-- 1) Prüfung: betroffene Datensätze anzeigen
SELECT Id, Title, OwnerId, AuthorId
FROM WikiPages
WHERE (AuthorId IS NULL OR LTRIM(RTRIM(AuthorId)) = '')
  AND OwnerId IS NOT NULL
  AND LTRIM(RTRIM(OwnerId)) <> '';

BEGIN TRANSACTION;

-- 2) Korrektur: fehlende AuthorId aus OwnerId übernehmen
UPDATE WikiPages
SET AuthorId = OwnerId
WHERE (AuthorId IS NULL OR LTRIM(RTRIM(AuthorId)) = '')
  AND OwnerId IS NOT NULL
  AND LTRIM(RTRIM(OwnerId)) <> '';

COMMIT TRANSACTION;
