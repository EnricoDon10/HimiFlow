# HimiFlow – SQL-Server-Produktionsweg

## Geltungsbereich

HimiFlow besitzt zwei voneinander getrennte EF-Core-Migrationshistorien:

- `AppDbContext` und `Migrations/` für SQLite (lokale Entwicklung und Demo)
- `SqlServerAppDbContext` und `Migrations/SqlServer/` für SQL Server (Produktion)

Die vorhandene SQLite-Datenbank und ihre Migrationen werden dadurch nicht verändert oder auf SQL Server angewendet. Zugangsdaten und echte Servernamen werden nicht im Repository gespeichert.

## Sichere Verbindungsparameter

Production akzeptiert SQL Server nur mit verschlüsselter Verbindung und ohne Umgehung der Zertifikatsprüfung:

```text
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=Server=<server>;Database=<datenbank>;Integrated Security=True;Encrypt=True;TrustServerCertificate=False
```

Das SQL-Server-Zertifikat muss vom Anwendungsserver als vertrauenswürdig validiert werden. Benutzername und Passwort dürfen nur über den freigegebenen Secret-Mechanismus des Kunden gesetzt werden. Windows-/Dienstkonto-Authentifizierung ist zu bevorzugen, wenn die Kundenumgebung dies unterstützt.

## Neue Datenbank initialisieren

1. Die DBA legt eine leere Datenbank und ein zeitlich begrenztes Migrationskonto an.
2. Provider und Connection String werden nur für das Wartungsfenster gesetzt.
3. Schema und Referenzdaten werden angelegt:

```powershell
$env:Database__Provider = "SqlServer"
$env:ConnectionStrings__DefaultConnection = "Server=<server>;Database=<datenbank>;Integrated Security=True;Encrypt=True;TrustServerCertificate=False"
$env:InitialAdmin__TemporaryPassword = "<individuelles-Einmalpasswort>"
dotnet .\Einsparungs.Api.dll --migrate --seed
Remove-Item Env:\InitialAdmin__TemporaryPassword
```

Nach erfolgreicher Initialisierung wird das Migrationskonto deaktiviert beziehungsweise dessen Berechtigung entzogen. Das normale App-Dienstkonto erhält ausschließlich die für Lesen und Schreiben benötigten Rechte, insbesondere keine `db_owner`- oder allgemeinen DDL-Rechte.

## Bestehende Installation aktualisieren

Vor dem Update werden SQL-Backup, Rückfallplan und Wartungsfenster bestätigt. Danach führt das getrennte Migrationskonto aus:

```powershell
$env:Database__Provider = "SqlServer"
$env:ConnectionStrings__DefaultConnection = "<geschützter-Connection-String-des-Migrationskontos>"
dotnet .\Einsparungs.Api.dll --migrate
```

Anschließend startet die Anwendung wieder mit dem Least-Privilege-Laufzeitkonto. Automatische Production-Migrationen beim normalen Prozessstart bleiben deaktiviert.

## DBA-Prüfskript erzeugen

Ein idempotentes SQL-Skript kann vorab geprüft und kontrolliert durch die DBA ausgeführt werden:

```powershell
dotnet tool restore
dotnet ef migrations script --idempotent `
  --project .\backend\Einsparungs.Api\Einsparungs.Api.csproj `
  --startup-project .\backend\Einsparungs.Api\Einsparungs.Api.csproj `
  --context SqlServerAppDbContext `
  --output .\artifacts\sql\himiflow-idempotent.sql
```

## Schema- und Modellprüfung

```powershell
dotnet ef migrations has-pending-model-changes --context AppDbContext `
  --project .\backend\Einsparungs.Api\Einsparungs.Api.csproj `
  --startup-project .\backend\Einsparungs.Api\Einsparungs.Api.csproj

dotnet ef migrations has-pending-model-changes --context SqlServerAppDbContext `
  --project .\backend\Einsparungs.Api\Einsparungs.Api.csproj `
  --startup-project .\backend\Einsparungs.Api\Einsparungs.Api.csproj
```

Die SQL-Migration enthält Identity-Tabellen, FKs, Check Constraints, `decimal(18,2)`, `datetime2`, Lizenzinstallation, Audit und die produktionsrelevanten Indizes. Die feste Lizenzzeile `Id=1` wird auf SQL Server bewusst ohne Identity-Spalte modelliert.

## Noch kundenspezifisch abzunehmen

- echte SQL-Server-Version und Hochverfügbarkeitsvorgaben
- Zertifikatskette, DNS und Firewall
- konkrete DBA-/Dienstkonten und Berechtigungen
- Übernahme bestehender SQLite-Daten mit Mengen- und Summenabgleich
- SQL-Backup, Restore-Test, Monitoring und Performance-Test

Diese Punkte benötigen reale Kundeninfrastruktur und bleiben Teil der Inbetriebnahme; der reproduzierbare Schemaweg ist bereits vorbereitet.
