-- Datenbereinigung für Legacy-Daten:
-- Ziel:
--   1) OwnerId ist führend für Besitz- und Benutzerfilter.
--   2) Pro Wiki-Seite und Assignee existiert genau eine Assignment-Zeile.
--
-- Empfohlen: zuerst die SELECT-Abfragen prüfen, dann Updates/Deletes ausführen.

-- 1) Prüfung: inkonsistente OwnerId/AuthorId-Werte
SELECT Id, Title, OwnerId, AuthorId
FROM WikiPages
WHERE (OwnerId IS NULL OR LTRIM(RTRIM(OwnerId)) = '')
   OR (AuthorId IS NOT NULL AND LTRIM(RTRIM(AuthorId)) <> ''
       AND OwnerId IS NOT NULL AND LTRIM(RTRIM(OwnerId)) <> ''
       AND OwnerId <> AuthorId);

-- 2) Prüfung: doppelte Assignments pro (WikiPageId, AssigneeId)
SELECT WikiPageId, AssigneeId, COUNT(*) AS DuplicateCount
FROM WikiAssignments
GROUP BY WikiPageId, AssigneeId
HAVING COUNT(*) > 1;

BEGIN TRANSACTION;

-- OwnerId aus AuthorId übernehmen, wenn OwnerId leer ist.
UPDATE WikiPages
SET OwnerId = AuthorId
WHERE (OwnerId IS NULL OR LTRIM(RTRIM(OwnerId)) = '')
  AND AuthorId IS NOT NULL
  AND LTRIM(RTRIM(AuthorId)) <> '';

-- Bei Konflikt OwnerId als führend behandeln und AuthorId angleichen.
UPDATE WikiPages
SET AuthorId = OwnerId
WHERE AuthorId IS NOT NULL
  AND LTRIM(RTRIM(AuthorId)) <> ''
  AND OwnerId IS NOT NULL
  AND LTRIM(RTRIM(OwnerId)) <> ''
  AND AuthorId <> OwnerId;

-- Doppelte Assignments löschen: je (WikiPageId, AssigneeId) bleibt der neueste Eintrag (höchste Id) erhalten.
;WITH DuplicateAssignments AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY WikiPageId, AssigneeId
               ORDER BY Id DESC
           ) AS RowNumber
    FROM WikiAssignments
)
DELETE FROM WikiAssignments
WHERE Id IN (
    SELECT Id
    FROM DuplicateAssignments
    WHERE RowNumber > 1
);

COMMIT TRANSACTION;

-- Optional: technische Absicherung in SQL Server (einmalig ausführen, falls Index noch fehlt)
-- CREATE UNIQUE INDEX IX_WikiAssignments_WikiPageId_AssigneeId_UQ
-- ON WikiAssignments(WikiPageId, AssigneeId);
