# HimiFlow – Build, Lieferung und Inbetriebnahme

Stand: Reifegrad 4 · SQLite Local Edition

## Voraussetzungen

- .NET 10 SDK zum Bauen; .NET 10 ASP.NET Core Runtime auf dem Zielsystem
- Node.js/npm passend zu `frontend/package.json`
- PowerShell 7 oder Windows PowerShell 5.1
- für Production: vom Kunden freigegebener Hostname, TLS-Zertifikat und Secret-/Konfigurationsweg

## Release-Paket erzeugen

Alle laufenden Angular-Entwicklungsserver vorher mit `Strg+C` beenden, da `npm ci` unter Windows sonst durch Dateisperren fehlschlagen kann.

```powershell
.\deploy\Publish-HimiFlow.ps1
```

Der Publish-Schritt erzeugt die SBOMs automatisch. `Generate-Sbom.ps1` kann zusätzlich separat für eine reine Lieferkettenprüfung ausgeführt werden.

Ergebnis:

- `artifacts/api`: veröffentlichte ASP.NET-Core-API
- `artifacts/frontend`: statische Angular-Dateien und `3rdpartylicenses.txt`
- `artifacts/sbom`: CycloneDX-SBOMs für Backend und Frontend
- `artifacts/documentation`: Betriebs-, Datenschutz-, Rechts- und Reifegradunterlagen
- `artifacts/THIRD-PARTY-NOTICES.md`: zentrale Drittanbieterhinweise
- `artifacts/LICENSE`: aktuell geltende Repository-Lizenz; vor proprietärer Kundenauslieferung rechtlich entscheiden

Das Frontend und die API sollen unter derselben HTTPS-Origin ausgeliefert werden. Die konkrete Zieltopologie wird mit der Kunden-IT gewählt. Für eine einzelne Windows-VM ist IIS als TLS-Reverse-Proxy plus ASP.NET-Core-Windows-Dienst meist einfacher als ein Container; Docker ist sinnvoll, wenn der Kunde bereits eine verbindliche Containerplattform und Betriebsstandards besitzt.

## SQLite-Initialisierung

Production migriert oder seedet beim normalen API-Start nicht automatisch.

Bestehende Datenbank migrieren:

```powershell
.\deploy\Apply-Migrations.ps1
```

Neue Installation mit zufälligem, nur kurzfristig vorhandenem Initialpasswort:

```powershell
$env:InitialAdmin__TemporaryPassword = "ein-individuelles-starkes-Einmalpasswort"
.\deploy\Initialize-HimiFlow.ps1
Remove-Item Env:\InitialAdmin__TemporaryPassword
```

Das Initialpasswort wird nicht in Git oder dauerhaft in einer Konfigurationsdatei gespeichert. Bei der ersten Anmeldung ist der Wechsel verpflichtend.

## Production-Konfiguration

Mindestens kundenspezifisch festlegen:

- `ASPNETCORE_ENVIRONMENT=Production`
- `AllowedHosts`
- `ConnectionStrings__DefaultConnection`
- `Database__Provider=SQLite` bis zur SQL-Server-Inbetriebnahme
- `Security__RequireHttps=true`
- `Security__HttpsPort=443`
- `ReverseProxy__Enabled` und ausschließlich konkrete `KnownProxies`
- `License__PublicKeyPem` und `License__InstallationId`
- `Legal__*`
- separates `Backup__Directory`

Private Schlüssel, Passwörter, produktive Verbindungszeichenfolgen und Lizenzsignaturschlüssel gehören nie in Git. Bei TLS-Terminierung am Reverse Proxy bleibt der Zertifikat-Private-Key ausschließlich im Zertifikatsspeicher beziehungsweise Secret-System des Kunden.

Anwendungsdateien und Laufzeitdaten werden getrennt: Das veröffentlichte `artifacts/api` ist ein austauschbares Build-Ergebnis und kein dauerhaftes Datenverzeichnis. Im Kundenbetrieb zeigt `ConnectionStrings__DefaultConnection` auf einen geschützten absoluten Pfad außerhalb des Programm-/Publish-Verzeichnisses (beispielsweise unter einem freigegebenen `ProgramData`-Bereich); `Backup__Directory` zeigt auf ein separates Sicherungsverzeichnis. Der Publish-Schritt bricht vorsorglich ab, sobald er im Ziel eine SQLite-Datei findet.

## HTTPS

Production erzwingt HTTPS und HSTS. Empfohlen ist ein Serverzertifikat aus der Kunden-PKI. Self-signed-Zertifikate sind nur für Entwicklung oder einen isolierten Abnahmetest gedacht.

Wenn IIS/Proxy TLS beendet, muss er `X-Forwarded-Proto` und `X-Forwarded-For` setzen. HimiFlow akzeptiert diese Header nur von den explizit konfigurierten Proxy-IPs. Zusätzlich sind `AllowedHosts`, DNS und Firewall des tatsächlichen Kundenhostnamens zu setzen.

## Backups

Die API erstellt im SQLite-Betrieb standardmäßig alle 24 Stunden ein validiertes Backup und bewahrt Dateien 30 Tage, mindestens jedoch sieben Backups auf. Der Betreiber muss diese Dateien zusätzlich auf ein getrenntes Sicherungsziel übertragen und deren Alter überwachen.

Manuell aus dem veröffentlichten Paket:

```powershell
.\deploy\Backup-SqliteNow.ps1
```

Restore nur bei vollständig gestoppter API:

```powershell
.\deploy\Restore-SqliteBackup.ps1 -BackupFile "C:\Backup\einsparungen_....db" -DatabaseFile "C:\ProgramData\HimiFlow\data\einsparungen.db"
```

Das Skript validiert das Backup, prüft den exklusiven Datenbankzugriff, erstellt eine Sicherheitskopie und validiert das Restore-Ergebnis. Siehe [Backup- und Restore-Konzept](../docs/backup-und-restore-konzept.md).

## Betriebsprüfungen

- `GET /api/health/live`: Prozess
- `GET /api/health/ready`: Datenbank
- `GET /api/health`: grundlegender Anwendungs-/Datenbankstatus
- `GET /api/health/operations`: betrieblicher Backupstatus; `Degraded` bei fehlendem oder überfälligem SQLite-Backup
- `GET /api/public/product-info`: Version und Anbieterangaben
- `GET /api/operations/backup-status`: Backupstatus, nur `SystemAdmin`

Unangemeldete Admin-Aufrufe müssen 401, fachlich unberechtigte Aufrufe 403 liefern. Fehlerantworten enthalten eine `traceId`, mit der der Vorgang im Serverprotokoll zugeordnet werden kann.

## SQL Server

Für SQL Server existiert eine getrennte, reproduzierbare Migrationshistorie. `--migrate` und `--seed` wählen anhand von `Database__Provider` automatisch den richtigen Provider. In Production erzwingt HimiFlow für SQL Server `Encrypt=True` und `TrustServerCertificate=False`.

Initialisierung, Updates, idempotentes DBA-Prüfskript und Least-Privilege-Vorgaben stehen im [SQL-Server-Produktionsweg](../docs/sql-server-produktionsweg.md). Die reale Datenübernahme, Kundenverbindung, Backup-/Restore-Abnahme und Performanceprüfung bleiben Bestandteil der Inbetriebnahme.

## Release-Checkliste

1. Backend-Tests, Frontend-Tests und Production-Build erfolgreich.
2. `npm audit` ohne High/Critical-Befund.
3. SBOM und Drittanbieterhinweise im Paket.
4. Versionsnummer, Release Notes und Git-Tag festgelegt.
5. Anbieter-/Lizenz-/Datenschutzunterlagen freigegeben.
6. Backup erstellt und Restore in getrennter Umgebung getestet.
7. Kundenspezifische Inbetriebnahme- und Rückfallplanung abgenommen.
