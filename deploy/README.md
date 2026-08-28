# HimiFlow – lokales Deployment (Phase F)

Phase F bündelt den reproduzierbaren Phase-D-Betrieb und die Datenschutz-/Audit-Schutzmaßnahmen aus Phase E. Die SQLite-Datenbank bleibt weiterhin lokal; eine SQL-Server-/Cluster-Migration erfolgt erst in einer späteren provider-spezifischen Phase.

Die Gesamtbewertung der lokalen Edition steht im [Phase-F-Reifegrad- und Abschlussbericht](../docs/phase-f-reifegrad-abschlussbericht.md).

## Voraussetzungen

- .NET 8 SDK/Runtime
- Node.js und npm passend zu `frontend/package.json`
- ein konfigurierter Produktions-Secret-Provider für die aktuellen Identity-/Lizenz-Secrets
- für die Erstinstallation: `InitialAdmin__TemporaryPassword`

## Veröffentlichung erzeugen

Aus dem Repository-Stamm:

```powershell
.\deploy\Publish-HimiFlow.ps1
```

Das Skript erzeugt `artifacts/api` und `artifacts/frontend`. Das Frontend ist für die Auslieferung über einen Reverse Proxy oder einen statischen Webserver gedacht; die API und das Frontend sollten im späteren Betrieb unter derselben Origin erreichbar sein.

Während des Publish-Schritts darf kein laufender Angular-Entwicklungsserver auf `frontend/node_modules` zugreifen. Falls `npm ci` unter Windows mit `EPERM` abbricht, zuerst `npm run start` mit `Strg+C` beenden und das Publish-Skript erneut ausführen.

## Datenbank vorbereiten

Migrationen werden in Production nicht automatisch beim API-Start ausgeführt. Verwende dafür das vorbereitete Skript (es setzt das Arbeitsverzeichnis korrekt auf die veröffentlichte API):

```powershell
.\deploy\Apply-Migrations.ps1
```

Bei einer neuen Installation wird zuerst ein temporäres Initialpasswort nur für den Setup-Prozess gesetzt und anschließend geseedet:

```powershell
$env:InitialAdmin__TemporaryPassword = "Ein-lokales-Setup-Passwort!2026"
.\deploy\Initialize-HimiFlow.ps1
Remove-Item Env:\InitialAdmin__TemporaryPassword
```

Das Passwort wird nicht in das Repository geschrieben. Der Benutzer wird nach der ersten Anmeldung zum Passwortwechsel gezwungen.

## Betriebsprüfungen

- `GET /api/health/live` prüft nur, ob der Prozess antwortet.
- `GET /api/health/ready` prüft die Datenbankverbindung.
- `GET /api/health` bleibt als kombinierter Health-Endpunkt erhalten.

Für den aktuellen lokalen Betrieb bleiben `Database:Provider=SQLite`, `einsparungen.db` und die lokale Backup-Funktion aktiv. Die spätere SQL-Server-/Cluster-Migration benötigt eine eigene, provider-spezifische Migrations- und Abnahmephase.

## Datenschutz und Audit (Phase E)

- CSV- und Excel-Exporte maskieren KVNRs standardmäßig (z. B. `A******789`). Die Einstellung `Privacy:MaskKvnrInExports` bleibt standardmäßig aktiviert.
- Exportantworten verwenden `Cache-Control: no-store` und `Pragma: no-cache`, damit sensible Dateien nicht im Browser-Cache verbleiben.
- `GET /api/admin/audit` ist ausschließlich für `SystemAdmin` verfügbar und liefert technische Audit-Metadaten ohne alte/neue fachliche Snapshot-Werte.
- `Audit:RetentionDays=0` bedeutet aktuell: keine automatische Löschung. Eine konkrete Aufbewahrungsfrist und ein Löschprozess werden erst nach fachlich-rechtlicher Freigabe aktiviert.
