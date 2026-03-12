# Windows Kerberos Login (Kestrel)

## Zielbetrieb

- Host: Windows, domain-joined
- App: Kestrel (`dotnet run`)
- Login-Start: manuell ueber den Button `Login with Kerberos`

## Konfiguration

Section: `Authentication:Windows`

- `Enabled`: aktiviert Kerberos-Button und Kerberos-Endpunkte
- `AllowedDomain`: erlaubte AD-Domain
- `AutoProvision`: legt lokale Konten bei erstem Windows-Login an
- `ProfileSyncMode`: `FirstLoginOnly` oder `EveryLogin`
- `DirectoryServices:Enabled`: AD-Profillookup aktiv
- `DirectoryServices:DomainController`: optionaler DC-Servername
- `DirectoryServices:Container`: optionale LDAP-Search-Base

## Ablauf

1. Benutzer klickt auf `Login with Kerberos`.
2. Negotiate/Kerberos Challenge wird ausgefuehrt.
3. AD-Profil wird via DirectoryServices geladen.
4. Bei AD-Fehlern erfolgt Fallback auf Kerberos-Claims.
5. Konto wird gefunden oder automatisch provisioniert.
6. Windows-Login-Mapping, Rolle `User` und Favoritengruppe werden sichergestellt.

## Wichtige Hinweise

- Kerberos funktioniert nur mit korrekter Domaenen-/Browser-/SPN-Umgebung.
- Bei Domain-Mismatch wird Login blockiert.
- Bei `AutoProvision = false` koennen sich nur bereits vorhandene lokale Konten anmelden.
