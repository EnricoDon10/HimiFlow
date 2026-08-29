# HimiFlow – Finalisierungsbericht Local Edition

Stand: 29.08.2026 · SQLite-Local-Edition · technische Bestandsaufnahme, keine Rechtsberatung

## 1. Umgesetzt

- FachAdmin-Stammdatenverwaltung für Organisationseinheiten, Einspargründe und Produktgruppen mit Erstellen, Bearbeiten und Löschen.
- Löschen wird als nachvollziehbares Soft-Delete umgesetzt: Der Wert verschwindet aus Verwaltung und Auswahl, historische Datensätze bleiben lesbar. Das Löschen einer Organisationseinheit mit aktiven Benutzern wird mit HTTP 409 (`TEAM_HAS_ACTIVE_USERS`) und Anzahl der Benutzer abgelehnt.
- Stammdatenänderungen werden mit Benutzer, Zeitpunkt, Aktion sowie alten/neuen Werten auditiert.
- Kundenspezifisches Demo-Seeding ist von technischen Rollen getrennt und in Production standardmäßig deaktiviert.
- Proprietäre Release-/Assembly-Metadaten auf ME Digitale GbR korrigiert; keine MIT-Deklaration für den eigenen Code.
- Konfigurierbarer Data-Protection-Key-Ring mit Production-Startprüfung, WebRoot-Schutz und Windows-DPAPI-Verschlüsselung vorbereitet.
- DELETE-Concurrency über `expectedVersion` geschlossen; veraltete Löschungen liefern HTTP 409.
- CodeQL JavaScript/TypeScript auf `build-mode: none` korrigiert; C# bleibt bei `autobuild`.
- Gitleaks als etablierter CI-Secret-Scanner ergänzt; der Workflow checkt mit vollständigem Git-Checkout.
- CSV-/Excel-Exporte gegen Spreadsheet-Formula-Injection neutralisiert; CSV schreibt zeilenweise und Exporte haben Filter, Limit und Auditdaten.
- `/api/savings/my` serverseitig paginiert; das Frontend zeigt Seite, Gesamtanzahl und Seitengröße einschließlich der expliziten Auswahl „Alle“.
- Fachhistorie speichert für neue Änderungen damalige Team-, Einspargrund- und Produktgruppenanzeigen; ältere IDs bleiben abwärtskompatibel.
- SQLite-Backup/Restore-Test, Monitoring-Dokumentation und reproduzierbarer 10.000-Zeilen-Performance-Smoke-Test ergänzt.
- Kleine Playwright-Suite für isolierte Erstlogin-, FachAdmin-, Stammdaten-, Einsparungs-, Lizenzseiten- und Logout-Flows ergänzt; eigener CI-Workflow vorhanden.

## 2. Tests

Im lokalen Prüfstand erfolgreich:

- Backend Release-Build: erfolgreich, 0 Fehler / 0 Warnungen.
- Backend Tests: **96 erfolgreich**, 0 fehlgeschlagen, 0 übersprungen.
- Frontend Production-Build: erfolgreich (Warnung: bestehendes My-Savings-SCSS überschreitet das 4-kB-Budget um 25 Bytes).
- Frontend Unit-/Component-Tests: **16 erfolgreich**.
- EF Core SQLite: keine ausstehenden Modelländerungen.
- EF Core SQL-Server-Kontext: keine ausstehenden Modelländerungen; idempotentes Skript wurde nur erzeugt, kein SQL Server gestartet.
- Export-Controller-Tests: Filter, Limit und Formula-Injection erfolgreich.
- Playwright-Konfiguration: 3 Tests werden erkannt (`playwright test --list`). Ein vollständiger Browserlauf ist lokal erst möglich, wenn ein .NET-10-SDK über `HIMIFLOW_DOTNET` verfügbar ist; die GitHub-CI richtet .NET 10 und Chromium selbst ein.
- `npm ci` im bestehenden Windows-Arbeitsverzeichnis war wegen eines laufenden `esbuild.exe`-Prozesses mit `EPERM` blockiert. Ein sauberer temporärer Installationslauf wurde für Build und Tests verwendet.
- Gitleaks ist lokal nicht installiert; der verbindliche Scan läuft im CI-Workflow.

## 3. Security

ASP.NET Core Identity, Cookie-/CSRF-Schutz, Rollenprüfung, Passwortwechsel, Lockout, Security-Stamp-Invalidierung, Lizenz-Read-only-Modus, Audit, KVNR-Masking, Concurrency und Rate-Limits bleiben erhalten. Keine Secrets, Passwörter oder Tokens werden in Audit-Snapshots geschrieben. Data-Protection-Schlüssel müssen außerhalb des WebRoot und mit restriktiven ACLs betrieben werden.

## 4. FachAdmin-Stammdaten

Nur `FachAdmin` darf Stammdaten schreiben. `SystemAdmin` bleibt technische Benutzer-/Lizenzadministration und erhält dadurch keine fachlichen Schreibrechte. Aktive Werte erscheinen unmittelbar in den Erfassungs-Dropdowns; gelöschte Werte werden für neue Datensätze ausgeblendet, bleiben aber in historischen Datensätzen lesbar.

## 5. Performance

Der Smoke-Test verwendet 10.000 synthetische SQLite-Datensätze ohne echte KVNR. Geprüft werden Pagination, Teamfilter und Statistikaggregation. Das Ergebnis dient der Regressionserkennung und ist kein Enterprise-Lasttest oder verbindliches SLA.

## 6. Noch manuell/offen

- Repository-Visibility bei GitHub endgültig auf privat prüfen.
- Kunden-PKI/Zertifikate, Windows-VM/Docker, Firewall und Infrastruktur-ACLs einrichten.
- Datenbank-, Arbeitsverzeichnis-, Backup- und Key-Ring-Verschlüsselung auf Kundensystemen freigeben.
- Produktives, getrenntes Backupziel und Kundenmonitoring konfigurieren sowie Restore-Übung durchführen.
- Anbieter-/Impressumsdaten, Datenschutzrollen, AVV/DSFA, Löschkonzept, EULA, SLA, Preis und Supportvertrag juristisch/kaufmännisch freigeben.
- SQL-Server-Migration erst in der Kunden-Inbetriebnahme durchführen.
- Playwright-Browserlauf in CI abwarten und bei Bedarf umgebungsspezifische Selektoren nachschärfen.

## 7. Reifegrad

Die Local Edition liegt technisch bei **Reifegrad 4 von 5**: sicherheitsgehärteter, getesteter SQLite-Betrieb mit produktisierten Stammdaten, Backup-/Monitoring-Schnittstelle und vorbereitetem Kundenpfad. Reifegrad 5 wird erst nach Kunden-Inbetriebnahme, Infrastrukturfreigaben, Restore-/Betriebsnachweis und formaler rechtlicher/organisatorischer Abnahme vergeben.
