# Windows Login mit Kestrel (Hinweis)

## Einordnung

- Produktionsstandard ist **IIS Integrated Windows Authentication**.
- Diese Notiz beschreibt nur den Kestrel-Fall fuer lokale Entwicklung oder Troubleshooting.
- Vollstaendige Produktionsvorgaben stehen in `Documents/WindowsAuth-IIS.md`.

## Kestrel-Besonderheiten

- Kestrel ist sensibel gegen Browser-/SPN-/Zone-Konfiguration.
- Wenn keine integrierte Windows-Identitaet geliefert wird, darf der neue Flow nur **einmal** challengen und faellt danach auf den lokalen Login zurueck.
- LDAP/DirectoryServices bleibt optional und faellt bei Fehlern auf Claims-basierte Daten zurueck.

## Konfigurationsschluessel

Section: `Authentication:Windows`

- `Enabled`
- `AllowedDomain`
- `AutoProvision`
- `ProfileSyncMode` (empfohlen: `EveryLogin`)
- `DirectoryServices:Enabled`
- `DirectoryServices:DomainController`
- `DirectoryServices:Container`
