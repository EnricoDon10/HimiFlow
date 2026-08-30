# HimiFlow – Disaster-Recovery-Anleitung (SQLite Local Edition)

Diese Anleitung beschreibt die Wiederherstellung der lokalen SQLite-Datenbank. Ein Restore ist immer ein geplanter Wartungsvorgang: HimiFlow wird vollständig beendet und anschließend außerhalb des laufenden Webprozesses wieder gestartet. Ein Browser-Request ersetzt niemals eine geöffnete Produktivdatenbank.

## Verantwortlichkeiten und Zielwerte

Der Betreiber legt RPO (maximal akzeptierter Datenverlust) und RTO (maximal akzeptierte Wiederanlaufzeit) passend zum Vertrag fest und dokumentiert sie je Installation. HimiFlow erfindet dafür keine pauschalen Werte. Das automatische Backup ist nur ein lokales Zwischenziel; mindestens eine Kopie muss regelmäßig auf einem getrennten/off-host Speicher liegen.

## Szenario A – Fachliche Fehlbedienung

1. Betroffene Einsparung und Änderungsverlauf prüfen.
2. Wenn möglich die fachliche Korrektur über die Anwendung durchführen; keinen vollständigen Datenbank-Restore nur wegen eines einzelnen falschen Werts starten.
3. Bei Unsicherheit Audit-Log, FachAdmin und Betreiber abstimmen. Ein Restore kann Änderungen anderer Benutzer seit dem gewählten Backup zurücksetzen.

## Szenario B – SQLite beschädigt

1. Benutzer informieren und Schreibzugriffe beenden.
2. HimiFlow vollständig stoppen (API und Frontend/Reverse Proxy).
3. Die aktuelle Datenbank inklusive eventuell vorhandener `-wal`/`-shm`-Dateien unverändert sichern.
4. Ein Backup aus dem geschützten Backup-Verzeichnis auswählen und mit `Einsparungs.Api.dll --validate-backup <backup.db>` prüfen.
5. `deploy/Restore-SqliteBackup.ps1` als lokaler Operator ausführen. Das Skript erstellt vor dem Überschreiben ein validiertes Sicherheitsbackup, kopiert zunächst in eine temporäre Datei und führt die Integritätsprüfung nach dem Austausch erneut aus.
6. Bei Fehlern bleibt das Sicherheitsbackup erhalten; die temporäre Datei wird entfernt. Den Recovery-Log unter `restore-logs` sichern.
7. HimiFlow starten, Health-Endpunkte prüfen und eine fachliche Stichprobe (Login, Stammdaten, aktuelle Einsparungen und Audit) durchführen.

Beispiel (PowerShell, aus dem Deployment-Verzeichnis):

```powershell
& .\Restore-SqliteBackup.ps1 `
  -PublishRoot 'C:\HimiFlow\api' `
  -BackupFile 'D:\HimiFlow-Backups\einsparungen_20260830_120000.db' `
  -DatabaseFile 'C:\HimiFlow\data\einsparungen.db'
```

## Szenario C – Zieldatenbank vollständig verloren

1. HimiFlow beendet lassen und den Datenbankordner anlegen bzw. prüfen.
2. Ein validiertes Backup aus dem Off-host-Ziel verwenden.
3. Das gleiche Restore-Skript mit dem fehlenden Zielpfad ausführen. Eine vorhandene Zieldatenbank ist nicht erforderlich; das Skript legt die Datei kontrolliert aus einer temporären Kopie an.
4. Integritätsprüfung, Start, Health-Check und fachliche Stichprobe wie in Szenario B durchführen.

Wenn keine Benutzer mehr in der Datenbank vorhanden sind, wird die Wiederherstellung nicht über die Weboberfläche durchgeführt. Der lokale Betreiber nutzt die Deployment-/Recovery-Anleitung und stellt danach die Anmeldung wieder her.

## Szenario D – Kein Backup auf dem Produktivsystem

Ein lokales Backup schützt nicht gegen Verlust des gesamten Systems. Fehlt auch das lokale Backup, ist eine Wiederherstellung nur aus dem externen/off-host Backupziel möglich. Deshalb müssen Backups regelmäßig auf einen getrennten, zugriffsgeschützten Speicher kopiert, ihre Integrität geprüft und die Aufbewahrung dokumentiert werden. Ohne eine solche Kopie kann HimiFlow keine Daten wiederherstellen.

## Sicherheitsregeln

- Backup-Dateien enthalten Benutzer-, Audit- und Fachdaten. Sie gehören nicht in `wwwroot` und werden nicht über einen anonymen Download-Endpunkt ausgeliefert.
- Vor jedem Restore muss HimiFlow beendet sein. Das Skript verweigert geöffnete Zieldateien, ungültige Dateinamen und Pfade im öffentlichen WebRoot.
- Safety-Backups und Recovery-Logs bleiben bis zur dokumentierten Aufbewahrungsfrist erhalten; Logs enthalten keine Passwörter oder Secrets.
- Nach einem Restore sind Lizenzstatus, Benutzerrollen, Audit und die fachlichen Werte zu prüfen. Änderungen nach dem Backup können bewusst fehlen.
