# HimiFlow – Backend-Betriebsdokumentation

Stand: 28.08.2026 · Produktversion 1.0.0 · Reifegrad 4/5

## 1. Geltungsbereich

Diese Dokumentation beschreibt den maßgeblichen Backend-Stand der **HimiFlow Einsparungsdatenbank – Local Edition**. Frühere JWT-, Demo- und Prototyp-Anweisungen wurden entfernt. Der lokale Betrieb nutzt weiterhin SQLite. SQL Server, die Ziel-VM, das Kundenzertifikat und die konkrete Netzwerkfreigabe gehören zur späteren **Phase Inbetriebnahme**.

## 2. Architektur

- Angular-Frontend und ASP.NET-Core-10-API
- ASP.NET Core Identity mit serverseitig gesetzter `HimiFlow.Auth`-Cookie
- CSRF-Schutz für schreibende Aufrufe über `HimiFlow.Antiforgery` und `X-XSRF-TOKEN`
- Entity Framework Core; aktuell `Database:Provider=SQLite`
- lokale, signierte Offline-Jahreslizenz mit 30 Tagen Grace Period
- rollenbasierte Autorisierung und Audit-Metadaten
- tägliche, integritätsgeprüfte SQLite-Backups

Es gibt keinen JWT-Schlüssel und keinen GPT-/OpenAI-Schlüssel. Ein eventuell noch vorhandenes lokales User Secret `Jwt:Key` ist ein nicht mehr verwendeter Altbestand und kann mit `dotnet user-secrets remove "Jwt:Key"` gelöscht werden.

## 3. Rollen und gültiger lokaler Benutzerbestand

| Rolle | Aufgabe | Datenzugriff |
| --- | --- | --- |
| `SystemAdmin` | lokale Benutzer, Rollen, Lizenz, Backups, Audit | administrative Funktionen; bewusst kein Zugriff auf fachliche Einzelfälle |
| `FachAdmin` | fachliche Koordination | alle Einsparungen sehen und bearbeiten/exportieren |
| `Mitarbeiter` | Erfassung | globale aggregierte Statistik, aber nur eigene Einträge anzeigen/bearbeiten/löschen |

Der lokale Datenbestand wurde auf drei aktive Konten bereinigt:

- `admin` – IT Admin – `SystemAdmin`
- `marco.meyer` – Marco Meyer – `FachAdmin`
- `enrico.mancuso` – Enrico Mancuso – `Mitarbeiter`

Christian Schmitt, Daniel Beck und Michael Klemmer wurden deaktiviert und logisch gelöscht. Ihre historischen Einsparungen und Audit-Einträge bleiben zur Nachvollziehbarkeit erhalten. Es wurden keine Passwörter in das Repository aufgenommen.

## 4. Anmeldung und Passwortschutz

Die Anwendung verwendet lokale Konten; AD und SSO sind nicht Bestandteil dieser Edition.

- mindestens 14 Zeichen
- Groß- und Kleinbuchstabe, Ziffer und Sonderzeichen
- mindestens vier unterschiedliche Zeichen
- keine Produkt-/Standardbegriffe, Benutzer- oder längeren Namensbestandteile
- das aktuelle Passwort kann nicht erneut verwendet werden
- fünf Fehlversuche sperren das Konto für 15 Minuten
- Sitzungsablauf nach 30 Minuten Inaktivität, mit gleitender Verlängerung
- zufälliges Einmalpasswort bei Anlage oder Reset
- verpflichtender Passwortwechsel nach Einmalpasswort
- Deaktivierung, Rollenwechsel und Passwortreset invalidieren bestehende Sitzungen
- keine turnusmäßige Passwortänderung ohne Anlass; sofortiger Wechsel bei Verdacht oder Reset

Die Authentifizierungscookie ist `HttpOnly`, `SameSite=Strict` und im HTTPS-Betrieb `Secure`. Schreibende API-Aufrufe benötigen zusätzlich den CSRF-Token. Login-Aufrufe sind auf zehn Versuche pro IP und Minute begrenzt.

## 5. Start und Wartungsbefehle

Lokale Entwicklung:

```powershell
cd C:\Users\enric\dev\GitHub\HimiFlow\backend\Einsparungs.Api
dotnet run --launch-profile http
```

Frontend in einem zweiten Terminal:

```powershell
cd C:\Users\enric\dev\GitHub\HimiFlow\frontend
npm run start
```

Explizite SQLite-Migration beziehungsweise Initialisierung:

```powershell
dotnet run -- --migrate
dotnet run -- --seed
```

In Production erfolgen Migration und Seed **nicht** automatisch beim Prozessstart. Für eine Erstinstallation wird das temporäre Initialpasswort ausschließlich kurzzeitig über `InitialAdmin__TemporaryPassword` gesetzt; siehe [Deployment-Anleitung](../deploy/README.md).

## 6. HTTPS und Zertifikate

`Security:RequireHttps` ist in Production aktiv. HTTP wird mit Status 308 umgeleitet; HSTS wird für 180 Tage ausgegeben. Cookies sind im HTTPS-Betrieb ausschließlich über TLS übertragbar.

Für den Kundenbetrieb ist ein Zertifikat aus der **PKI des Kunden** die professionelle Zielvariante. Es ist in dessen Vertrauenskette eingebunden und kann durch die Kunden-IT erneuert und gesperrt werden. Private Schlüssel oder Zertifikate gehören nie in Git. Ein selbstsigniertes Zertifikat ist nur für lokale Entwicklung oder einen isolierten technischen Test geeignet.

Bei TLS-Terminierung an IIS oder einem anderen Reverse Proxy muss `ReverseProxy:Enabled=true` gesetzt werden. `ReverseProxy:KnownProxies` muss ausschließlich die konkrete Proxy-IP enthalten. Unbeschränktes Vertrauen in weitergeleitete Header ist nicht zulässig.

## 7. Konfiguration und Secrets

Nicht geheime Standardwerte stehen in `appsettings*.json`. Kundenspezifische Werte und Geheimnisse werden im Zielbetrieb über einen freigegebenen Secret-Mechanismus oder geschützte Umgebungsvariablen bereitgestellt.

Programmdateien, Datenbank und Backups liegen im Kundenbetrieb in getrennten Verzeichnissen. `artifacts/api` ist ausschließlich ein reproduzierbares Build-Ergebnis. Das Publish-Skript liefert keine Development-Konfiguration aus und bricht ab, falls im Ziel eine lokale Datenbank gefunden wird.

Relevante Gruppen:

- `ConnectionStrings:DefaultConnection`
- `Database:Provider`
- `AllowedHosts`
- `License:PublicKeyPem` und `License:InstallationId`
- `Security:*`, `ReverseProxy:*`, `Cors:AllowedOrigins`
- `Backup:*`, `Identity:*`, `Legal:*`

Die Ausstellung und Übergabe signierter Jahreslizenzen ist im [Lizenzgenerierungs- und Übergabekonzept](lizenzgenerierung-und-uebergabe.md) beschrieben. Die Anbieterwerkzeuge `scripts/New-HimiFlowLicenseKeyPair.ps1` und `scripts/New-HimiFlowLicense.ps1` werden ausschließlich außerhalb der Kundeninstallation verwendet.

Es gibt aktuell keinen externen KI-Dienst und deshalb keinen GPT-Key. Falls später ein KI-Dienst ergänzt wird, benötigt er eine eigene Datenschutz-, Vertrags-, Berechtigungs- und Secret-Management-Freigabe.

## 8. Datenbankprovider

SQLite bleibt der aktive und geprüfte lokale Provider:

```json
"Database": { "Provider": "SQLite" },
"ConnectionStrings": { "DefaultConnection": "Data Source=einsparungen.db" }
```

Für `SqlServer` existiert eine eigene Migrationshistorie unter `Migrations/SqlServer`. Der aktive Provider entscheidet, welche Migrationen `--migrate` und `--seed` verwenden. Productive SQL-Verbindungen müssen `Encrypt=True` und `TrustServerCertificate=False` verwenden. Details zu Initialisierung, Update und getrennten Migrations-/Laufzeitkonten stehen im [SQL-Server-Produktionsweg](sql-server-produktionsweg.md). Eine bloße Änderung der Verbindungszeichenfolge ersetzt weiterhin keine Datenübernahme und Kundenabnahme.

## 9. Backup und Restore

Bei SQLite erstellt der Hintergrunddienst standardmäßig alle 24 Stunden ein Backup. Nach jedem Backup läuft `PRAGMA integrity_check`. Standardaufbewahrung: 30 Tage, mindestens sieben Dateien bleiben erhalten. Der Dienst holt ein fälliges Backup beim nächsten Start nach und beendet die API bei einem einzelnen Backupfehler nicht; der Fehler wird protokolliert. `Backup:Directory` darf als absoluter Pfad außerhalb des Anwendungsverzeichnisses konfiguriert werden. `Backup:MaximumAgeHours` bestimmt, ab wann das letzte Backup als überfällig gilt.

Manuell:

```powershell
.\deploy\Backup-SqliteNow.ps1
dotnet run -- --validate-backup .\backups\einsparungen_....db
```

Restore nur bei gestoppter API:

```powershell
.\deploy\Restore-SqliteBackup.ps1 -BackupFile "C:\gesicherter-pfad\einsparungen_....db" -DatabaseFile "C:\ProgramData\HimiFlow\data\einsparungen.db"
```

Das Restore-Skript prüft das Quellbackup, verlangt einen exklusiven Zugriff auf die explizit angegebene Zieldatenbank, erstellt von genau dieser Datei zuerst eine Sicherheitskopie und validiert anschließend die wiederhergestellte Datenbank. Ein Backup auf derselben Festplatte ist kein vollständiges Disaster-Recovery-Konzept. Die Detailvorgaben stehen im [Backup- und Restore-Konzept](backup-und-restore-konzept.md).

## 10. Health, Fehler und Diagnose

`/api/health/operations` meldet fehlende, deaktivierte oder gemäß `MaximumAgeHours` überfällige SQLite-Backups als `Degraded`. Diese betriebliche Warnung ist bewusst von `/api/health/ready` getrennt und nimmt die Anwendung nicht automatisch aus dem Dienst.

- `GET /api/health/live`: Prozess antwortet
- `GET /api/health/ready`: Datenbank ist erreichbar
- `GET /api/health`: kombinierter Status
- `GET /api/public/product-info`: Produktversion und konfigurierbare Anbieterangaben

Validierungs-, Berechtigungs-, Rate-Limit- und unerwartete Fehler werden als einheitliche RFC-7807-`ProblemDetails` zurückgegeben. Jede Antwort enthält eine `traceId`; `X-Correlation-ID` kann zur Vorgangszuordnung verwendet werden. Unerwartete Fehler werden serverseitig protokolliert, ohne Stacktrace oder lokale Dateipfade an den Browser auszugeben.

Production setzt zusätzlich `nosniff`, `DENY`, `no-referrer`, eine eingeschränkte Permissions Policy, API-CSP und deaktiviert den Kestrel-Serverheader. Die maximale Requestgröße beträgt standardmäßig 1 MiB.

## 11. Datenschutz, Export und Audit

- KVNRs werden in Exporten standardmäßig maskiert.
- Exportantworten werden mit `no-store`/`no-cache` ausgeliefert.
- Mitarbeiter erhalten globale aggregierte Statistiken, aber keinen Reiter mit fremden Einzelfällen.
- fachliche Änderungen, Benutzerverwaltung, Anmeldung, Passwortwechsel und Backuperstellung erzeugen Audit-Metadaten.
- nur `SystemAdmin` kann die administrative Auditansicht aufrufen; alte/neue fachliche Snapshotwerte werden dort nicht ausgegeben.
- Die optionale Audit-Bereinigung ist standardmäßig deaktiviert; `Audit:RetentionDays=0` bedeutet keine automatische Löschung. Die konkrete Aufbewahrungs- und Löschfrist muss der Kunde rechtlich/fachlich festlegen.

Details und offene Betreiberentscheidungen stehen im [Datenschutz- und Berechtigungskonzept](datenschutz-und-berechtigungskonzept.md).

## 12. Produktinformation und Drittkomponenten

Die Route `/legal` ist ohne Anmeldung erreichbar und lädt die Angaben aus `Legal:*`. Solange Name, Anschrift und E-Mail fehlen, zeigt sie deutlich „nicht konfiguriert“. Vor Auslieferung müssen die rechtlich freigegebenen Anbieter-/Betreiberangaben eingetragen werden.

`deploy/Generate-Sbom.ps1` erzeugt CycloneDX-SBOMs für Backend und Frontend. Lizenzhinweise stehen in `THIRD-PARTY-NOTICES.md`; das Frontend erzeugt zusätzlich `3rdpartylicenses.txt`.

Die aktuelle Repository-Datei `LICENSE` ist proprietär für die ME Digitale GbR. Vor Kundenüberlassung sind die Rechtekette, frühere Lizenzstände und der Endkundenvertrag trotzdem juristisch zu prüfen; die technische Dokumentation ersetzt diese Entscheidung nicht.

## 13. Phase Inbetriebnahme

Vor echtem Kundenbetrieb bleiben kundenspezifisch:

1. Zieltopologie festlegen: Windows-Dienst/IIS oder kundenseitig standardisierte Containerplattform.
2. Kunden-PKI-Zertifikat und DNS/Hostname einbinden.
3. Firewall-, Proxy- und `AllowedHosts`-Werte festlegen.
4. SQL-Server-Datenbank, Dienstkonto, Verschlüsselung und Least-Privilege-Zugriff bereitstellen.
5. Vorbereitete SQL-Server-Migrationen auf der Kundeninstanz anwenden; Datenübernahme, Performance, Backup und Restore testen.
6. Lizenzschlüssel, Anbieterangaben und produktive Konfiguration einspielen.
7. Datenschutzfreigabe, gegebenenfalls DSFA, Vertrag, Abnahme und Betriebsverantwortung abschließen.
8. Monitoring, Patchprozess, Recovery-Test und Support-Eskalation in die Kundenprozesse integrieren.

Die Anwendung ist damit technisch auf die Inbetriebnahme vorbereitet, aber ohne diese kundenspezifische Abnahme noch kein freigegebener Produktivbetrieb.
