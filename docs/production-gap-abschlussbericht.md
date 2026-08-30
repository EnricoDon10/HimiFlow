# HimiFlow – Production-Gap-Abschlussbericht

Stand: 29.08.2026
Bewertung: **Reifegrad 4 von 5 – vorproduktions- und verkaufsvorbereitete Local Edition**

## 1. Ausgangslage

Die Anwendung war funktional, aber noch nicht belastbar genug für einen kontrollierten Pilot- oder Kundenbetrieb. Wesentliche Produktionslücken waren fehlende Konflikterkennung bei parallelen Änderungen, unlimitierte Listenabfragen, nicht optimierte Statistikabfragen, fehlende Datenbankindizes, kein getrennt vorbereiteter SQL-Server-Migrationsweg, unvollständige Lizenzgrenzen, uneinheitliche Team- und Rollenregeln, fehlende fachliche Änderungshistorie sowie fehlende echte HTTP- und Frontend-Tests.

Zusätzlich fehlten technische Leitplanken für Backup/Restore, Audit-Aufbewahrung, CI-Sicherheitsprüfungen, Release-Metadaten und die Abgrenzung zwischen technischem Audit und fachlichen Versichertendaten. SQLite bleibt für den lokalen Entwicklungs- und Demonstrationsbetrieb bewusst erhalten.

## 2. Durchgeführte Änderungen

| Phase | Dateien/Bereiche | Technische Änderung und Grund | Nachweis |
|---|---|---|---|
| 1 .NET 10 | `global.json`, Solution-Projekte, `Directory.Build.props`, `.config/dotnet-tools.json` | Einheitliche .NET-10-Basis, aktualisierte EF-/Identity-/Testpakete und reproduzierbare Tools. | Release-Build ohne Warnungen/Fehler |
| 2 Concurrency | `AppDbContext`, Savings-Controller/DTOs, Angular-Savings-Modelle und -Services | Optimistische Versionsprüfung mit HTTP 409 und verständlicher Konfliktmeldung verhindert stilles Überschreiben. | `SavingsConcurrencyTests`, HTTP-Integrationstest |
| 3 Pagination/Filter | `SavingsListDtos`, Savings-API, All-/My-Savings-Komponenten | Serverseitige Filter und begrenzte Seiten (`pageSize` maximal 100) schützen API und UI; vollständige Datenmengen werden über den vorhandenen Export bereitgestellt. | `SavingsPaginationTests`, Browser-Abnahme |
| 4 Statistik | Statistics-Controller/Queries | Aggregationen werden datenbankseitig statt über vollständige Datensatzlisten berechnet. | `StatisticsAggregationTests` |
| 5 Indizes | `AppDbContext`, SQLite- und SQL-Server-Migrationen | Composite-Indizes für die produktiven Filter- und Statistikpfade verkürzen Abfragen. | `DatabaseIndexMigrationTests`, Pending-Model-Checks |
| 6 SQL-Server-Weg | `SqlServerAppDbContext`, Factory, `Migrations/SqlServer`, Produktionskonfiguration | SQL Server ist als separater Provider mit eigenen Migrationen vorbereitet; SQLite bleibt lokal. TLS-Anforderungen und idempotentes Skript sind dokumentiert. | Provider-Trennung, beide Pending-Model-Checks, SQL-Skript-Generierung |
| 7 Lizenz | License-DTOs/Service/Middleware/Controller, Installation-Modell | Offline-Jahreslizenz mit Grace-Period, MaxUsers-Transaktionsgrenze, InstallationId- und Rollback-Schutz sowie definierte Read-only-Ausnahmen. | Lizenz-Unit- und HTTP-Integrationstests |
| 8 Team/Berechtigung | Savings-/User-Management-Controller, Angular-Erfassungsmaske | Mitarbeiter bleiben auf ihr Team beschränkt; FachAdmin darf fachlich erforderliche Teams wählen; SystemAdmin bleibt technische Administration. | `SavingsTeamAuthorizationTests`, HTTP-Rollenmatrix |
| 9 Fachhistorie | `SavingsHistoryDtos`, Savings-Controller, Angular-All-Savings | FachAdmin erhält eine getrennte Historie; geänderte Felder sind nachvollziehbar, KVNR wird in der Historie maskiert. SystemAdmin-Audit und Fachhistorie bleiben getrennt. | `SavingsHistoryTests`, Browser-Abnahme |
| 10 Audit/Retention | `AuditRetentionOptions`, Cleanup-Service/Background-Service, Migrationen | Sichere optionale Aufbewahrungsbereinigung in kleinen Transaktionen. `RetentionDays=0` löscht nie automatisch; standardmäßig ist Cleanup deaktiviert. | `AuditRetentionServiceTests` |
| 11 Backup/Restore | Operations-Controller, Backup-Evaluator/HealthCheck, `deploy/Restore-SqliteBackup.ps1` | Backup-Alter und Überfälligkeit sind sichtbar; Restore verlangt ein explizites Ziel, erstellt eine Sicherheitskopie und validiert die Quelldatei. | `BackupStatusEvaluatorTests`, Restore-Dokumentation |
| 12 API-Integration | `Einsparungs.Api.Tests` und Test-Factory | Reale HTTP-Pfade prüfen Authentifizierung, Cookies, CSRF, Lockout, Passwortwechsel, Rollen, Lizenzzustände und Concurrency in isolierten SQLite-Datenbanken. | 16 HTTP-Integrationstests |
| 13 Frontend | Angular-Services, Guards und `critical-security-flows.spec.ts` | Login, Passwortwechsel, Lizenz-Read-only, Benutzerverwaltung und Konfliktverhalten sind automatisiert abgesichert. | 14 Frontend-Tests |
| 14 CI Security | `.github/workflows/ci.yml`, CodeQL, Dependency Review, Dependabot, `scripts/check-secrets.ps1` | Build-, Test-, Schwachstellen-, Secret- und Code-Scanning-Gates; NuGet/npm-Abhängigkeiten werden geprüft. | CI-Konfiguration, lokale Scans sauber |
| 15 Release-Qualität | `frontend/src/index.html`, API-Projektmetadaten, `LICENSE`, `THIRD-PARTY-NOTICES.md` | Produktname, Sprache, Beschreibung, Anbieter-Metadaten und Drittanbieterhinweise sind konsistent. | Release-Build, npm-Audit, SBOM |
| 16 Commercial Source Check | `docs/commercial-source-code-check.md`, SBOM-Artefakte | Lizenz- und Paketprüfung, SBOM-Erzeugung für Backend/Frontend und klare Abgrenzung zur späteren Pro-Edition. | CycloneDX-SBOMs und Lizenzprüfung |

## 3. Security

- **Auth:** ASP.NET Core Identity mit Cookie-Anmeldung, Lockout bei Fehlversuchen und Security-Stamp-Invalidierung.
- **CSRF:** Antiforgery-Token über `XSRF-TOKEN`/`X-XSRF-TOKEN`; Zustandsänderungen werden geschützt.
- **Rollen:** `SystemAdmin`, `FachAdmin` und `Mitarbeiter` sind serverseitig getrennt. Fachliche Datenzugriffe werden nicht nur im Frontend verborgen.
- **Passwort:** Temporäre Passwörter werden serverseitig zufällig erzeugt, nur einmal angezeigt und erzwingen beim Erstlogin einen Wechsel. Die Mindestlänge beträgt 14 Zeichen; die produktive Passwortübermittlung erfolgt außerhalb der Anwendung über einen sicheren Kanal.
- **Lizenz:** Offline-Jahreslizenz, sichtbare Grace-Period, Read-only-Modus bei Ablauf und Schutz vor Benutzer-/Rollenaufwertung.
- **HTTPS:** In Produktion ist HTTPS verpflichtend vorgesehen. Für einen Kundenbetrieb ist das Zertifikat der Kunden-PKI bzw. des freigegebenen Reverse Proxy zu verwenden; ein selbstsigniertes Zertifikat ist nur für lokale Entwicklung oder einen isolierten Test geeignet.
- **Secrets:** JWT-/Cookie-/Lizenzschlüssel gehören in User Secrets, Umgebungsvariablen oder den Secret Store der Zielumgebung. Sie sind nicht im Repository. Ein GPT-/LLM-Key ist aktuell nicht Bestandteil des lokalen Betriebs und darf nur über den Ziel-Secret-Store ergänzt werden.
- **Datenschutz:** System-Audit enthält keine vollständigen fachlichen Werte; die Fachhistorie maskiert KVNR. SQLite-Datei und Backups besitzen jedoch keine anwendungsinterne Verschlüsselung und müssen vor echten Kundendaten infrastrukturell geschützt werden.

## 4. Datenbank

- **SQLite Development:** Bleibt die Standardbasis für den lokalen Laptop-Betrieb. Migrationen werden beim Start angewendet; der bestehende lokale Workflow bleibt erhalten.
- **SQL Server Production:** `SqlServerAppDbContext` und separate `Migrations/SqlServer` sind vorbereitet. Der Providerwechsel erfolgt erst in der Phase Inbetriebnahme mit Kunden-Connection-String und Firewallfreigabe.
- **Migration:** Der idempotente SQL-Server-Skriptweg wurde erfolgreich generiert; ein kundenspezifischer Datenimport ist noch nicht ausgeführt.
- **Indizes:** Filter- und Statistikpfade besitzen explizite Composite-Indizes.
- **Concurrency:** Einsparungen verwenden eine erwartete Version; bei Paralleländerungen liefert die API einen kontrollierten 409-Konflikt.

## 5. Betrieb

- **Backup:** SQLite-Backups werden integritätsgeprüft, erhalten Zeitstempel und werden über Operations-Health-Informationen bewertet. Das externe Backupziel ist konfigurierbar.
- **Restore:** Der Restore ist absichtlich explizit und sicherheitsorientiert: Quelle und `DatabaseFile` müssen angegeben werden, vor dem Überschreiben wird eine Safety-Kopie erstellt.
- **Health:** Readiness/Liveness und der separate Operations-Health-Endpunkt trennen Anwendbarkeit von Backup-Betriebszustand.
- **Logging:** Korrelation-IDs, strukturierte Fehlerantworten und Audit-Ereignisse erleichtern Support und Nachvollziehbarkeit; Passwörter, Tokens und technische Secrets werden nicht geloggt.
- **CI:** Build, Backend-Tests, Frontend-Tests, Vulnerability-Checks, Secret-Scan, CodeQL/Dependency-Review-Konfiguration und SBOM-Schritte sind vorbereitet.
- **SBOM:** Backend- und Frontend-CycloneDX-Dateien wurden lokal erzeugt; Angular-Drittanbieterhinweise sind ebenfalls vorhanden.

## 6. Tests

| Testart | Ergebnis |
|---|---:|
| Backend-Tests gesamt | 76 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Davon echte HTTP-Integrationstests | 16 bestanden |
| Frontend-Tests | 14 bestanden |
| Backend Release-Build | erfolgreich, 0 Warnungen, 0 Fehler |
| Frontend Production-Build | erfolgreich |
| npm Audit | 0 Schwachstellen |
| NuGet Vulnerability Check | keine Schwachstellen |
| Secret-Scan | sauber |
| SQL-Server Pending-Model-Check | sauber |
| Browser-Abnahme | Login, Rollen, Speicherung/Berechnung, Filter, Historie, Mobile ohne horizontalen Überlauf; Konsole ohne Fehler/Warnungen |

## 7. Noch offene Punkte

### Vor dem Pilotbetrieb

- Verantwortliche für Support, Incident-Meldung und fachliche Freigaben benennen.
- Aufbewahrungsdauer und Aktivierung des Audit-Cleanups mit dem Datenschutzverantwortlichen entscheiden.
- Regelmäßigen lokalen Backup-Lauf und eine dokumentierte Rücksicherung einmal praktisch abnehmen.

### Vor der Verarbeitung echter Kundendaten

- SQLite-Datei, Arbeitsverzeichnis und Backupziel mit Betriebssystem-/Datenträgerverschlüsselung und restriktiven Dateirechten absichern.
- Datenschutzprüfung, Verzeichnis der Verarbeitung, TOM/Schutzbedarf, Rollenfreigabe und gegebenenfalls DSFA/AVV mit dem Kunden abschließen.
- Persistenz und Rotation der ASP.NET-Data-Protection-Schlüssel für die Zielumgebung festlegen.

### Vor Go-Live / Phase Inbetriebnahme

- Kunden-VM bzw. Windows-Dienst/Container, feste Ports, Reverse Proxy und `AllowedHosts` festlegen.
- Kunden-PKI-Zertifikat installieren und HTTPS-Ende-zu-Ende testen.
- SQL-Server-Connection-String, Firewall, Login/Berechtigungen, Migration und gegebenenfalls Datenimport durchführen.
- Produktive JWT-, Cookie-, Data-Protection- und Lizenzschlüssel über den freigegebenen Secret Store hinterlegen.
- Echte `InstallationId`, signierte Jahreslizenz und Lizenz-Erneuerungsprozess einrichten.
- Externes Backupziel, Aufbewahrung, Monitoring/Alarmierung und einen dokumentierten Restore-Test abnehmen.
- Kundenakzeptanz-, Last- und Betriebsübergabetest durchführen.

### Optionale Enterprise-Funktionen

Active Directory/SSO, MFA, SIEM-Anbindung, Redis, Cloudmigration, Hochverfügbarkeit und SQL-Cluster sind bewusst nicht eingebaut. Sie können später als kundenspezifische Erweiterungen bewertet werden und sind keine Voraussetzung für die lokale Edition.

## 8. Neue Reifegradbewertung

HimiFlow ist **Reifegrad 4 von 5**: Der Anwendungskern ist vorproduktions- und verkaufsvorbereitet, lokal reproduzierbar, sicherheitstechnisch gehärtet, getestet und dokumentiert. Die verbleibenden Punkte sind überwiegend zielumgebungs-, kunden- und organisationsabhängig.

Reifegrad 5 wird ausdrücklich noch nicht vergeben. Dafür fehlen die konkrete Kundeninfrastruktur, ein tatsächlich migrierter SQL Server, das Kunden-PKI-Zertifikat, ein produktives externes Backupziel mit Restore-Nachweis, die produktive Secret-Verwaltung und die formalen Datenschutz-/Betriebsfreigaben. Diese Punkte bilden die klar abgegrenzte **Phase Inbetriebnahme**; sie erfordern keine Rückkehr zu einer offenen Demo-Architektur, sondern die konkrete Installation und Abnahme beim Kunden.
