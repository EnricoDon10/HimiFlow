# HimiFlow – Abschlussbericht Final-Fixes / Recovery-Runde

Stand: 30.08.2026 · Local Edition auf SQLite

## 1. Umgesetzte Punkte

- Stammdaten-Lifecycle für Organisationseinheiten, Einspargründe und Produktgruppen auf Aktiv/Inaktiv mit Deaktivieren/Reaktivieren vereinheitlicht.
- Inaktive Dubletten werden mit `MASTER_DATA_INACTIVE_EXISTS` und direkter Reaktivierungsaktion gemeldet.
- SystemAdmin kann Mitarbeiter und FachAdmins einer anderen aktiven Organisationseinheit zuordnen; Security Stamp und Audit werden aktualisiert.
- Benutzeranlage und Reaktivierung akzeptieren nur aktive Organisationseinheiten.
- Pagination bleibt serverseitig auf 1–100 begrenzt; die UI bietet nur 25/50/100 und keinen „Alle“-Modus.
- DELETE-Concurrency liefert `409 CONCURRENCY_CONFLICT` statt einer 500-Antwort.
- Backup-Status und manuelles Backup stehen im SystemAdmin-Bereich bereit. Die Wiederherstellung bleibt ein kontrollierter Offline-/Wartungsmodus; es gibt keinen Live-Database-Swap und keinen Browser-Download.
- Restore-Skript akzeptiert auch eine fehlende Ziel-Datenbank, validiert temporär und nach dem Austausch, behandelt WAL/SHM, erstellt Safety-Backups und Recovery-Logs.
- Disaster-Recovery-Anleitung für fachliche Fehlbedienung, beschädigte/fehlende Datenbank und fehlendes Produktiv-Backup erstellt.
- Spreadsheet-/CSV-Formelneutralisierung berücksichtigt Formelzeichen nach führenden Leerzeichen, Tab, CR und LF.
- Alte Route `/admin/product-groups` leitet auf `/admin/master-data` um.
- `ChangedFieldsJson` wird aus tatsächlichen Snapshot-Differenzen erzeugt.

Für Einspargründe und Produktgruppen bleibt die providerübergreifende Duplikatprüfung bewusst in der API: Vor einer SQL-Server-Migration müssen vorhandene Daten einmalig auf Dubletten geprüft werden. Eine fragile, kollationsabhängige SQLite/SQL-Server-Migration wurde nicht erzeugt; dieser Abgleich ist als Migrations-Check dokumentiert.

## 2. Geänderte Dateien

Wesentliche Änderungen liegen in `backend/Einsparungs.Api/Controllers/MasterDataController.cs`, `UserManagementController.cs`, `SavingsController.cs`, `OperationsController.cs`, `Security/SqliteBackupService.cs`, `Security/LicenseReadOnlyMiddleware.cs`, den zugehörigen DTOs und Tests. Im Frontend kamen Backup-Service/-Modelle, die SystemAdmin-Seite, Teamwechsel in der Benutzerverwaltung, der Stammdaten-Statusfilter und die alte Routenweiterleitung hinzu. Deployment und Dokumentation wurden unter `deploy/` und `docs/` ergänzt.

## 3. Stammdaten-Lifecycle

Management-Listen enthalten aktive und inaktive Werte samt Status-Badge. Lookups für neue Einsparungen enthalten weiterhin nur aktive Werte; historische Einsparungen behalten ihre IDs und bleiben lesbar. Deaktivierung verändert keine historischen Datensätze.

## 4. Benutzer-/Team-Lifecycle

Teamwechsel ist eine SystemAdmin-Funktion für Mitarbeiter/FachAdmins. SystemAdmins haben kein Team. Inaktive Teams werden bei Anlage, Rollenwechsel und Reaktivierung eines Benutzers abgewiesen (`USER_TEAM_INACTIVE`).

## 5. Pagination und Concurrency

Die API akzeptiert keine unbounded page size; der Export bleibt der Weg für vollständige Datenmengen. Aktualisieren und Löschen prüfen Versionen und behandeln zusätzlich `DbUpdateConcurrencyException` mit HTTP 409.

## 6. Backup- und Disaster-Recovery-Konzept

Das automatische SQLite-Backup bleibt aktiv und wird mit `integrity_check` geprüft. Die Wiederherstellung ist absichtlich ein Offline-/Wartungsmodus über PowerShell, damit kein Webprozess eine geöffnete Produktivdatei überschreibt. Die vollständige Ablaufbeschreibung steht in `docs/disaster-recovery.md`.

## 7. SystemAdmin-Backup-Oberfläche

`/admin/backup-recovery` zeigt Status, Alter/Anzahl/Aufbewahrung und erlaubt die manuelle Erstellung eines Backups. Die Wiederherstellung bleibt bewusst außerhalb der Oberfläche und wird ausschließlich im geplanten Wartungsmodus über die interne Betriebsanleitung durchgeführt. Die Oberfläche zeigt keine öffentlichen Dateipfade und bietet keinen Download.

## 8. Restore-Ablauf

Bei beschädigter oder vorhandener DB: Prozess stoppen, aktuelles Ziel sichern und validieren, Backup validieren, temporär kopieren, atomar ersetzen, Integrität erneut prüfen, starten und fachlich stichproben. Bei vollständig fehlender DB wird das Zielverzeichnis angelegt und die Datei aus dem temporären Backup erzeugt. Safety-Backup und Log bleiben auch bei einem Fehler erhalten.

## 9. Tests und tatsächliche Zahlen

Die bestehende Teststruktur wurde um Lifecycle-, Teamwechsel-, Pagination-, Spreadsheet- und Backup-Prüfungen erweitert. Der aktuelle Release-Build ist ohne Warnungen/Fehler durchgelaufen; die Backend-Suite steht bei **109/109** und die Frontend-Suite bei **16/16** erfolgreichen Tests. Der Production-Frontend-Build ist erfolgreich (mit einer bestehenden 25-Byte-Budgetwarnung im Stylesheet `my-savings.component.scss`).

## 10. E2E- und Security-Ergebnis

Die E2E-Konfiguration verwendet pro Lauf einen eindeutigen temporären Datenbankpfad und die korrekte Erstlogin-Zielroute `/admin/users`. Der echte Playwright-Lauf ist mit **3/3** Tests erfolgreich durchgelaufen (Erstlogin/Passwortwechsel/FachAdmin, Stammdaten-Lifecycle und Einsparung, Lizenzseite/Logout). Backup-Dateien werden nicht aus dem WebRoot bedient; Secrets und private Lizenzschlüssel bleiben außerhalb des Repositories.

## 11. Noch offene Punkte vor Pilot

- .NET 10 SDK und Playwright-Browser in der Pilotumgebung installieren und vollständige Build-/Test-/E2E-Läufe protokollieren.
- Off-host Backupziel, Aufbewahrung, RPO/RTO und eine dokumentierte Restore-Übung festlegen.
- Kundeninfrastruktur-HTTPS-Zertifikat, Reverse Proxy/Firewall und Betriebsmonitoring konfigurieren.
- Datenschutz-, Berechtigungs- und Betriebsfreigabe mit dem Kunden abnehmen.

## 12. Noch offene Punkte vor Echtdaten-Go-Live

Die SQL-Server-Migration, Windows-VM-/Containerinstallation, kundenseitige PKI, produktive Secrets, Firewall-Freischaltung und die formale Inbetriebnahme bleiben bewusst die separate Phase „Inbetriebnahme“. Reifegrad 5 wird nicht automatisch behauptet; dafür sind reale Kundeninfrastruktur, Backup-/Restore-Übung, TLS, Monitoring und Freigaben nachzuweisen.
