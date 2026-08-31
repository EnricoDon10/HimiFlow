# Backup- und Restore-Konzept

Stand: 28.08.2026

## Zweck und Schutzziele

Backups begrenzen Datenverlust und ermöglichen die Wiederherstellung nach Fehlbedienung, Defekt oder beschädigter SQLite-Datei. Ein Backup im selben Verzeichnis und auf demselben Datenträger schützt nicht vor Geräteverlust oder Ransomware; deshalb muss der Betreiber die erzeugten Dateien zusätzlich auf ein getrenntes, zugriffsgeschütztes Sicherungsziel übertragen.

## Aktueller SQLite-Betrieb

| Parameter | Standard |
| --- | --- |
| Intervall | 24 Stunden |
| Prüfung | `PRAGMA integrity_check` nach jeder Erstellung |
| Aufbewahrung | 30 Tage |
| Mindestbestand | sieben Backups |
| Speicherort | `backend/Einsparungs.Api/backups` beziehungsweise konfiguriertes `Backup:Directory` |

Der Hintergrunddienst prüft alle 15 Minuten, ob ein Backup fällig ist. Fehlt ein fälliges Backup beim Prozessstart, wird es nachgeholt. Fehler werden protokolliert. Der `SystemAdmin` kann Status, Bestand und manuelle Erstellung über die API kontrollieren.

Empfohlenes, noch mit dem Kunden zu vereinbarendes Ziel: **RPO höchstens 24 Stunden**. Ein verbindliches RTO wird erst nach Messung auf der Zielinfrastruktur und einer Wiederherstellungsübung festgelegt.

## Betrieb

Manuelles Backup:

```powershell
.\deploy\Backup-SqliteNow.ps1
```

Integrität einer konkreten Datei:

```powershell
dotnet run --project .\backend\Einsparungs.Api\Einsparungs.Api.csproj -- --validate-backup "C:\Backup\einsparungen_....db"
```

Der Betreiber überwacht täglich, ob ein gültiges Backup innerhalb des Sollintervalls vorhanden ist. Mindestens eine Kopie wird automatisiert auf ein separates, verschlüsseltes Kundensicherungsziel übertragen. Zugriffe auf Sicherungen sind auf den Betriebskreis zu beschränken; Sicherungen enthalten dieselben personenbezogenen Daten wie die Produktivdatenbank. Auf dem lokalen Windows-Laptop müssen Geräte-/Volumeverschlüsselung (zum Beispiel BitLocker) und gesicherte Zugangsdaten den fehlenden dateiinternen SQLite-Verschlüsselungsschutz abdecken.

## Restore-Ablauf

1. Störung dokumentieren und Änderungsstopp ausrufen.
2. API vollständig stoppen; nicht nur den Browser schließen.
3. Gewünschtes Backup und dessen Integritätsprüfung auswählen.
4. Restore ausführen:

   ```powershell
   .\deploy\Restore-SqliteBackup.ps1 -BackupFile "C:\Backup\einsparungen_....db"
   ```

5. Das Skript erstellt eine Sicherheitskopie der bisherigen Datenbank, ersetzt die Zieldatei und validiert das Ergebnis.
6. API starten und `/api/health/ready` prüfen.
7. Anmeldung, Benutzerbestand, jüngste Datensätze und Stichproben prüfen.
8. Ergebnis, Zeitpunkt, Backup-ID, Datenlücke und Freigabe dokumentieren.

## Prüfungen und Verantwortungen

- Monatlich: Backupstatus und Stichprobe der Integritätsprüfung kontrollieren.
- Mindestens jährlich sowie vor einer größeren Migration: Restore in einer getrennten Testumgebung vollständig üben.
- Nach jedem Restore-Test: tatsächliches RPO/RTO und Abweichungen dokumentieren.
- Betreiber: Sicherungsziel, Monitoring, Schlüssel/Zugriffe, Aufbewahrung und Löschung.
- Hersteller/Support: Skriptpflege und Unterstützung nach Vertrag, aber kein ungeplanter Zugriff auf Kundendaten.

## Monitoring-Schnittstelle

Ein externer Check kann ohne kundenspezifische Monitoringsoftware die folgenden Endpunkte abfragen:

| Endpunkt | Zweck | Erwartung bei gesundem Betrieb |
| --- | --- | --- |
| `/api/health/live` | Prozess lebt | HTTP 200 |
| `/api/health/ready` | Datenbank ist bereit | HTTP 200 |
| `/api/health/operations` | Backup-/Betriebschecks | HTTP 200; bei `MISSING`, `OVERDUE` oder Fehler HTTP 503 |
| `/api/operations/backup-status` | Detailstatus für SystemAdmin | `status=CURRENT`, `lastSuccessfulBackupUtc` gesetzt |

Der Operations-Status liefert außerdem Alter, Überfälligkeit, Anzahl der Sicherungen und den konfigurierten Backupzielpfad (ohne Datenbankinhalte). Der Kunde kann daraus einen HTTP-, Windows-Service- oder PowerShell-Check bauen. Das externe Ziel, die Alarmierung und Eskalationswege bleiben kundenseitig konfigurierbar. Die Oberfläche stellt nur Status und eine manuelle Backup-Erstellung bereit; Datei-Auswahl, Validierung und Restore erfolgen ausschließlich über die freigegebenen Betriebs-/PowerShell-Schritte.

## SQL Server in der Phase Inbetriebnahme

Die SQLite-Automatik wird bei `Database:Provider=SqlServer` deaktiviert. Dann gilt ausschließlich das vom SQL-Server-Betrieb freigegebene Konzept, zum Beispiel Voll-/Differenz-/Transaktionsprotokollsicherungen, Verschlüsselung, getrennte Medien, regelmäßige Restore-Tests und kundenseitiges Monitoring. Erst ein erfolgreich getesteter Restore erfüllt das Sicherungsziel.
