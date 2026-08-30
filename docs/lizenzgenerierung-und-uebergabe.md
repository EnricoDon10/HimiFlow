# HimiFlow – Lizenzgenerierung und Kundenübergabe

## Zweck

HimiFlow verwendet eine offline prüfbare Jahreslizenz. Nach Zahlungseingang erstellt der Anbieter einen signierten Lizenzschlüssel und übergibt ihn dem Kunden. Die Anwendung prüft beim Installieren:

- Signatur und Produkt (`HimiFlow`)
- Lizenz-ID und Kundenname
- Gültigkeitszeitraum und Grace-Period
- optionale Installations-ID
- maximales Benutzerlimit

Bei Erfolg speichert HimiFlow den Schlüssel in der lokalen `LicenseInstallations`-Tabelle. Der SystemAdmin sieht danach Status, Kunde, Lizenz-ID, Laufzeit, Grace-Ende und Benutzerlimit.

Der private Signaturschlüssel bleibt ausschließlich beim Anbieter. In die Kundenumgebung gelangen nur der passende Public-Key und der signierte Lizenzschlüssel.

## Einmalige Einrichtung beim Anbieter

Die Schlüssel werden außerhalb des Repositorys erzeugt und verwahrt:

```powershell
cd C:\Pfad\zu\HimiFlow
pwsh -File .\scripts\New-HimiFlowLicenseKeyPair.ps1 `
  -OutputDirectory 'C:\Secure\HimiFlow-Licensing'
```

Den privaten Schlüssel (`himiflow-license-private.pem`) verschlüsselt und mit restriktiven ACLs ablegen, zum Beispiel in einem Unternehmens-Passwortsafe oder HSM. Der Public-Key wird für jede Kundeninstallation über den freigegebenen Secret-Mechanismus hinterlegt.

Beispiel für eine lokale Development-Installation:

```powershell
dotnet user-secrets set 'License:PublicKeyPem' `
  (Get-Content -Raw 'C:\Secure\HimiFlow-Licensing\himiflow-license-public.pem') `
  --project .\backend\Einsparungs.Api
dotnet user-secrets set 'License:InstallationId' 'viactiv-prod-01' `
  --project .\backend\Einsparungs.Api
```

In Production gehören dieselben Werte in den Secret Store beziehungsweise als Umgebungsvariablen `License__PublicKeyPem`, `License__InstallationId` und `License__EnforcementEnabled=true`. Nach jeder Konfigurationsänderung den API-Prozess neu starten.

## Ausstellung nach bezahlter Rechnung

1. Rechnung und Vertrag prüfen und eine eindeutige interne Auftragsnummer vergeben.
2. Eine eindeutige `LicenseId` vergeben, zum Beispiel `LIC-VIACTIV-2027-0001`.
3. Den exakten Kundennamen und die Installations-ID aus der Zielumgebung übernehmen. Die Installations-ID ist keine starre Hardwarebindung; bei einem geregelten Umzug wird eine neue Lizenz für die neue ID ausgestellt.
4. Laufzeit in UTC festlegen. Für eine Jahreslizenz ist das Ende typischerweise `23:59:59Z` am letzten Gültigkeitstag. Die Grace-Period darf höchstens 30 Tage umfassen.
5. Das vereinbarte Benutzerlimit eintragen. Gezählt werden alle aktiven, nicht gelöschten Benutzer einschließlich Administratoren.
6. Lizenz mit dem Anbieter-Privatschlüssel erzeugen:

```powershell
$from = [DateTime]::Parse('2026-09-01T00:00:00Z')
$until = [DateTime]::Parse('2027-08-31T23:59:59Z')
$grace = [DateTime]::Parse('2027-09-30T23:59:59Z')

pwsh -File .\scripts\New-HimiFlowLicense.ps1 `
  -PrivateKeyPath 'C:\Secure\HimiFlow-Licensing\himiflow-license-private.pem' `
  -LicenseId 'LIC-VIACTIV-2027-0001' `
  -CustomerName 'Viactiv Krankenkasse' `
  -InstallationId 'viactiv-prod-01' `
  -ValidFromUtc $from `
  -ValidUntilUtc $until `
  -GraceUntilUtc $grace `
  -MaxUsers 50 `
  -OutputPath 'C:\Secure\HimiFlow-Licensing\LIC-VIACTIV-2027-0001.txt'
```

   Das Werkzeug verifiziert die Signatur vor der Ausgabe. Die Datei enthält genau einen kopierbaren Lizenzschlüssel. Der Privatschlüssel wird nicht ausgegeben.

7. Lizenz-Metadaten (Kunde, Auftrag, Lizenz-ID, Laufzeit, Installations-ID, Benutzerlimit und Ausgabedatum) im Anbieterarchiv dokumentieren. Den privaten Schlüssel niemals an den Kunden senden.
8. Den Lizenzschlüssel zusammen mit der Rechnung beziehungsweise dem vereinbarten Übergabedokument über einen freigegebenen, nachvollziehbaren Kanal übermitteln.

## Installation beim Kunden

Der Kunde startet die passende HimiFlow-Version, meldet sich als `SystemAdmin` an und öffnet **Lizenzverwaltung**. Dort wird der vollständige Schlüssel eingefügt und **Lizenz installieren** gewählt.

Nach erfolgreicher Installation muss der Status `ACTIVE` anzeigen:

- Kunde
- Lizenz-ID
- Gültig bis
- Grace-Ende
- Aktive Benutzer erlaubt

Die Installation wird zusätzlich als Audit-Ereignis protokolliert. Bei einer abgelaufenen Lizenz bleibt der Lesezugriff erhalten; schreibende Vorgänge werden blockiert. Die Grace-Period erlaubt den geregelten Rechnung-/Erneuerungsprozess.

## Verlängerung, Austausch und Transfer

- Für jedes Lizenzjahr einen neuen Schlüssel mit neuer Lizenz-ID ausstellen; nicht direkt in der Datenbank editieren.
- Bei geänderter Benutzerzahl einen neuen signierten Schlüssel ausstellen.
- Bei Umzug auf eine neue VM eine neue Installations-ID vereinbaren und einen neuen Schlüssel signieren.
- Bei kompromittiertem Anbieter-Privatschlüssel muss das Schlüsselpaar ersetzt, der neue Public-Key ausgerollt und anschließend eine neue Lizenz ausgestellt werden.
- Das Offline-Modell besitzt keine zentrale Sofort-Widerrufsliste. Vertrags-, Support- und Sperrprozesse müssen deshalb organisatorisch geregelt werden.

## Fehlerbilder

| Meldung | Bedeutung | Maßnahme |
|---|---|---|
| `Der Lizenz-Public-Key ist nicht konfiguriert.` | API kennt den Public-Key nicht. | Secret/Umgebungsvariable prüfen und API neu starten. |
| `Die Lizenzsignatur ist ungültig.` | Schlüssel beschädigt, unvollständig kopiert oder mit anderem Schlüssel signiert. | Originaldatei unverändert erneut übermitteln. |
| `Die Lizenz gehört nicht zu dieser Installations-ID.` | Payload und Kundeninstallation stimmen nicht überein. | Installations-ID prüfen oder Transferlizenz ausstellen. |
| `Die Lizenz ist noch nicht gültig.` | Systemzeit liegt vor `ValidFrom`. | Zeitsynchronisation und Lizenzdaten prüfen. |
| `NOT_CONFIGURED` | Es ist noch kein Lizenzschlüssel in der Kundendatenbank installiert. | Schlüssel im SystemAdmin-Bereich installieren. |

## Test- und Produktionsabgrenzung

Die bisherige Viactiv-Lizenz ist ausschließlich ein lokaler Testschlüssel mit kurzem Laufzeitfenster. Für echte Kunden immer ein separates Anbieter-Schlüsselpaar beziehungsweise eine kontrollierte Produktionsschlüsselverwaltung verwenden. Test-Private-Keys gehören nicht in das Repository und nicht in eine Kundeninstallation.
