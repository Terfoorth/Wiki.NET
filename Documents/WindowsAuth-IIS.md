# Windows Anmeldung (IIS Integrated) - Betriebsstandard

## Zielbild

- Host: Windows Server, domain-joined
- Hosting: IIS mit ASP.NET Core Module
- Login-Start: manuell ueber Button `Windows Anmeldung`
- Fallback: lokaler Formular-Login bleibt aktiv

## IIS-Konfiguration

1. `Windows Authentication` = `Enabled`
2. `Anonymous Authentication` = `Enabled`
3. Provider-Reihenfolge in Windows Authentication:
   - `Negotiate`
   - `NTLM`

## AD/Kerberos Voraussetzungen

1. Passenden SPN fuer die produktive URL registrieren:
   - `HTTP/<fqdn>`
2. SPN auf dem verwendeten Dienstkonto korrekt pflegen.
3. Sicherstellen, dass der Application Pool mit dem vorgesehenen Konto laeuft.

## Browser/GPO Voraussetzungen

1. Ziel-URL in der Local-Intranet-Zone.
2. Richtlinie: `Automatic logon only in Intranet zone`.

## App-Konfiguration

Section: `Authentication:Windows`

- `Enabled = true`
- `AllowedDomain = <AD-FQDN>`
- `AutoProvision = true` (oder gem. Betriebsregel)
- `ProfileSyncMode = EveryLogin`
- `DirectoryServices:Enabled = true` (optional abschaltbar)
- `DirectoryServices:DomainController` und `Container` optional

## Laufzeitverhalten

1. Klick auf `Windows Anmeldung` startet genau einen Negotiate-Challenge.
2. Wenn danach keine integrierte Windows-Identitaet vorliegt:
   - kein weiterer Challenge-Loop,
   - Rueckkehr auf Login-Seite mit Fehlermeldung.
3. Bei gueltiger AD-Identitaet:
   - `samAccountName` wird erkannt,
   - lokaler User wird gefunden oder automatisch angelegt,
   - Profil wird bei jedem Login synchronisiert (`EveryLogin`),
   - Login-Mapping (`Windows`) und Rolle `User` werden sichergestellt.
