# Wiki.Forms Implementierungsplan

1. **Datenmodell erweitern**
   - `WikiPage` um Typ (`Standard`/`Form`), Edit-Lock und Formularschema erweitern.
   - `WikiCategory` um Kennzeichen `IsFormCategory` erweitern.
2. **Service-Ebene erweitern**
   - Form-Kategorien separat laden.
   - Form-Einträge separat laden/öffnen (`GetWikiFormsAsync`, `GetWikiFormForUserAsync`).
   - Standard-Wiki-Abfragen auf `EntryType == Standard` einschränken.
3. **Navigation und Kategorien**
   - Sidebar um `Wiki.Forms` erweitern.
   - Kategorienverwaltung um `Wiki.Forms Kategorie`-Eigenschaft ergänzen.
4. **Editor für Wiki.Forms**
   - Neue Seite `WikiFormEditor` (Erstellen/Bearbeiten).
   - Felddefinitionen: Textfeld, Checkbox, Label, Default-Wert.
   - Speichern als neuer `WikiPage`-Eintrag vom Typ `Form`.
5. **Öffnen-Ansicht für Wiki.Forms**
   - Dynamisches Formular-Rendering aus dem gespeicherten Schema.
   - Eingaben erfassen und als interaktive PDF exportieren.
6. **Integration + UX**
   - Edit-Lock berücksichtigen.
   - Form-Einträge von klassischen Wiki-Listen isolieren.
7. **Tests / Checks**
   - Build/Tests lokal ausführen (falls Tooling vorhanden).
   - Konsistenzchecks (`git diff --check`).
