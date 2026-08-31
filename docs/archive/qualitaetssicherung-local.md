# Lokale Qualitätssicherung

Die Local Edition bleibt SQLite-basiert. Die folgenden Prüfungen verwenden ausschließlich temporäre Testdatenbanken oder isolierte Build-Verzeichnisse und überschreiben nicht `backend/Einsparungs.Api/einsparungen.db`.

## Backend

```powershell
dotnet restore backend/EinsparungsApp.sln
dotnet build backend/EinsparungsApp.sln --configuration Release --no-restore
dotnet test backend/EinsparungsApp.sln --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project backend/Einsparungs.Api --startup-project backend/Einsparungs.Api --context AppDbContext --configuration Release
dotnet ef migrations has-pending-model-changes --project backend/Einsparungs.Api --startup-project backend/Einsparungs.Api --context SqlServerAppDbContext --configuration Release
```

Der Test `SQLitePerformanceSmokeTests` erzeugt 10.000 synthetische Datensätze in einer In-Memory-SQLite-Datenbank und misst Pagination, Filter und Aggregation. Er ist ein reproduzierbarer Smoke-Test, kein Enterprise-SLA.

## Frontend

```powershell
npm ci
npm run test:ci
npm run build -- --configuration production
npm audit --audit-level=high
```

Wenn ein lokaler Angular-Prozess `node_modules` sperrt, `npm ci` nach dessen Beendigung wiederholen. Die CI führt stets einen sauberen Installationslauf aus.

## Playwright

Die Suite startet mit `HIMIFLOW_E2E_START_SERVERS=1` eine eigene SQLite-Datei unter `%TEMP%`, API-Port `55281` und Frontend-Port `55280`:

```powershell
$env:HIMIFLOW_E2E_START_SERVERS = '1'
$env:HIMIFLOW_DOTNET = 'C:\Pfad\zu\dotnet10\dotnet.exe' # falls dotnet auf PATH nicht 10.0.400 ist
npm run e2e
Remove-Item Env:HIMIFLOW_E2E_START_SERVERS,Env:HIMIFLOW_DOTNET -ErrorAction SilentlyContinue
```

Die E2E-Daten werden nicht in die Entwicklerdatenbank geschrieben. Die CI installiert Chromium und führt dieselben drei kritischen Flows aus.
