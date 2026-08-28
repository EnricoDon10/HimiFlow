# Backend-Betriebsdokumentation  
## Einsparungsdatenbank

---

## 1. Zweck dieser Dokumentation

Diese Betriebsdokumentation beschreibt das Backend der Anwendung **Einsparungsdatenbank** aus technischer, fachlicher und administrativer Sicht.

Sie dient dazu, dass Administratoren, Entwickler, technische Betreuer und spätere Projektbeteiligte nachvollziehen können:

- wie das Backend aufgebaut ist
- welche Komponenten vorhanden sind
- welche fachlichen Anforderungen umgesetzt wurden
- welche API-Endpunkte existieren
- wie Authentifizierung und Berechtigungen funktionieren
- wie die lokale Datenbank betrieben wird
- wie Stammdaten erzeugt werden
- wie Einsparungsdatensätze verarbeitet werden
- wie Statistik und Export umgesetzt sind
- wie das Backend lokal gestartet, getestet und administriert wird
- welche Punkte bei einer späteren produktionsnahen Umgebung zu beachten sind

Die Dokumentation bezieht sich auf den aktuellen Entwicklungsstand des Backends im lokalen Prototyp.

> **Maßgeblicher Stand Phase F (28.08.2026):** Die Anwendung verwendet weiterhin lokale SQLite und ASP.NET Core Identity mit einer HttpOnly-Authentifizierungscookie (`HimiFlow.Auth`). Schreibende API-Aufrufe werden zusätzlich über einen CSRF-Token (`XSRF-TOKEN` / Header `X-XSRF-TOKEN`) geschützt. Die lokale Offline-Lizenz, der Health-Check, SQLite-Backups und die administrative Auditierung sind ergänzt. Migrationen können für einen kontrollierten Deployment-Schritt explizit mit `--migrate` bzw. `--seed` ausgeführt werden; im Produktionsprofil werden sie nicht automatisch beim API-Start angewendet. KVNRs werden in Exporten standardmäßig maskiert, Exportantworten tragen `Cache-Control: no-store`, und System-Admins erhalten Audit-Metadaten über `/api/admin/audit`, ohne Snapshot-Werte auszulesen. Der [Phase-F-Reifegrad- und Abschlussbericht](phase-f-reifegrad-abschlussbericht.md) bewertet die lokale Edition als kontrolliert pilotfähig und grenzt spätere Enterprise-Themen ab. Die älteren JWT- und Demo-Benutzer-Abschnitte weiter unten sind historische Projektnotizen und beschreiben nicht mehr den laufenden Code.

### Phase-C-Betrieb auf diesem Rechner

```powershell
cd "C:\Users\enric\dev\GitHub\HimiFlow\backend"
dotnet run --project .\Einsparungs.Api\Einsparungs.Api.csproj --launch-profile http
```

Beim ersten Start einer leeren Datenbank muss einmalig ein Initialpasswort als lokales User Secret gesetzt werden. Das Secret wird niemals in Git eingecheckt:

```powershell
dotnet user-secrets init --project .\Einsparungs.Api\Einsparungs.Api.csproj
dotnet user-secrets set "InitialAdmin:TemporaryPassword" "<eigenes-starkes-passwort>" --project .\Einsparungs.Api\Einsparungs.Api.csproj
```

Bestehende Benutzer werden bei der ersten Anmeldung zur Passwortänderung aufgefordert. Neue Benutzer erhalten bei der Anlage ein zufälliges Einmalpasswort, das nur einmal in der Benutzerverwaltung angezeigt wird. SSO und die spätere SQL-Server-/Cluster-Migration bleiben bewusst nachgelagerte Phasen.

Die für Phase C relevanten Authentifizierungs- und Betriebs-Endpunkte sind:

| Methode | Route | Zweck |
| --- | --- | --- |
| `GET` | `/api/auth/csrf` | CSRF-Cookie für den nächsten Schreibzugriff ausstellen |
| `POST` | `/api/auth/login` | lokale Anmeldung und HttpOnly-Cookie setzen |
| `GET` | `/api/auth/me` | aktuelle Sitzung prüfen |
| `POST` | `/api/auth/change-password` | Erstlogin abschließen oder Passwort ändern |
| `POST` | `/api/auth/logout` | Sitzung serverseitig beenden |
| `POST` | `/api/user-management/{id}/reset-password` | zufälliges Einmalpasswort erzeugen |
| `GET` | `/api/license/status` | lokalen Lizenzstatus prüfen |
| `POST` | `/api/admin/license` | signierten Offline-Jahreslizenzschlüssel installieren (SystemAdmin) |
| `GET` | `/api/health` | lokale API-/SQLite-Erreichbarkeit prüfen |
| `GET` | `/api/health/live` | Liveness-Prüfung des API-Prozesses |
| `GET` | `/api/health/ready` | Readiness-Prüfung inklusive Datenbankverbindung |
| `GET` | `/api/operations/backups` | vorhandene SQLite-Backups auflisten (SystemAdmin) |
| `POST` | `/api/operations/backups` | konsistentes SQLite-Backup erstellen (SystemAdmin) |
| `GET` | `/api/admin/audit` | paginierte technische Audit-Metadaten (SystemAdmin) |

Rollenmatrix: `Mitarbeiter` verwalten nur eigene Einsparungen und sehen globale Statistiken; `FachAdmin` verwaltet alle fachlichen Datensätze und Exporte; `SystemAdmin` verwaltet Benutzer und Rollen, erhält aber keinen Zugriff auf fachliche Einsparungsdaten.

### Phase-C-Betrieb: Lizenz und Read-only-Modus

Die Lizenz ist ein offline signiertes Token. Der Public-Key wird ausschließlich über Konfiguration/User Secrets hinterlegt; ein privater Signaturschlüssel gehört niemals in das Repository. Eine Jahreslizenz erhält automatisch höchstens 30 Tage Grace-Period. Während Grace bleibt der Betrieb schreibbar und es erscheint ein Warnbanner. Nach Ablauf oder bei ungültiger Lizenz blockiert das Backend schreibende Fach- und Stammdatenaufrufe mit HTTP 403 (`LICENSE_READ_ONLY`); Anmeldung, Lizenzverwaltung, Benutzerverwaltung und Backups bleiben für den `SystemAdmin` möglich.

Für die lokale Entwicklung bleibt die Durchsetzung deaktiviert:

```powershell
dotnet user-secrets set "License:EnforcementEnabled" "false" --project .\Einsparungs.Api\Einsparungs.Api.csproj
```

Für eine produktionsnahe lokale Installation werden `License:EnforcementEnabled=true`, `License:PublicKeyPem` und optional `License:InstallationId` als sichere Konfiguration gesetzt. Der Schlüssel wird anschließend in der Frontend-Seite **Lizenzverwaltung** installiert.

### Phase-C-Betrieb: Backups und Health

`POST /api/operations/backups` erstellt ein konsistentes Online-Backup über SQLite `WAL`-Checkpoint und `VACUUM INTO`. Standardziel ist der nicht versionierte Ordner `backend/Einsparungs.Api/backups`; über `Backup:Directory` kann ein anderes lokales Ziel konfiguriert werden. Der Restore bleibt absichtlich ein kontrollierter manueller Vorgang: Anwendung stoppen, aktuelle Datenbank sichern, gewünschte `.db`-Datei zurückkopieren und Anwendung wieder starten. Der Endpoint `/api/health` liefert `200 Healthy`, wenn SQLite erreichbar ist.

### Phase-D-Betrieb: kontrollierte Migrationen und Veröffentlichung

Im Produktionsprofil sind `Database:ApplyMigrationsOnStartup` und `Database:SeedOnStartup` standardmäßig deaktiviert. Dadurch kann ein Anwendungstart keine ungeplante Schemaänderung auslösen. Migrationen werden als separater, nachvollziehbarer Schritt ausgeführt:

```powershell
.\deploy\Apply-Migrations.ps1
```

Eine neue lokale Installation kann anschließend mit `--seed` die Rollen, Stammdaten und den initialen Systemadministrator anlegen. Das Initialpasswort wird ausschließlich über `InitialAdmin__TemporaryPassword` bzw. einen sicheren Secret-Provider übergeben. Für den vollständigen lokalen Publish-/Setup-Ablauf siehe [`deploy/README.md`](../deploy/README.md).

### Phase-E-Betrieb: Datenschutz und Audit

CSV- und Excel-Exporte enthalten standardmäßig nur eine maskierte KVNR (`A******789`). Die Einstellung `Privacy:MaskKvnrInExports` bleibt absichtlich aktiv und darf nur nach einer dokumentierten fachlichen Datenschutzfreigabe deaktiviert werden. Exportantworten werden mit `Cache-Control: no-store` und `Pragma: no-cache` ausgeliefert.

Der Endpunkt `/api/admin/audit` ist ausschließlich für `SystemAdmin` verfügbar. Er liefert Seiten mit Entität, Aktion, Zeitpunkt, auslösendem Benutzer, Client-Metadaten und geänderten Feldnamen. Die gespeicherten `OldValuesJson`-/`NewValuesJson`-Snapshots werden nicht ausgegeben; insbesondere gelangen keine fachlichen KVNR- oder Betragswerte in die technische Adminansicht. Die Aufbewahrungsdauer ist über `Audit:RetentionDays` dokumentiert, aber mit `0` standardmäßig ohne automatische Löschung. Eine konkrete Frist und ein Löschprozess benötigen eine fachlich-rechtliche Freigabe.

---

## 2. Projektkontext

Die Anwendung **Einsparungsdatenbank** digitalisiert einen fachlichen Krankenkassenprozess zur Erfassung und Auswertung von Einsparungen.

Der bisherige Prozess basiert auf historisch gewachsenen Strukturen wie:

- Confluence-Formularen
- Access-Datenbanken
- Excel-Auswertungen
- manuellen Datenexporten
- dezentral gepflegten Datenquellen

Diese Strukturen sollen perspektivisch durch eine moderne Webanwendung ersetzt werden.

Das Backend bildet dafür die technische Grundlage. Es stellt alle zentralen Funktionen bereit:

- Login
- Benutzerrollen
- Stammdaten
- Einsparungsdatensätze
- Validierungen
- Auditierung
- Statistik
- Export

Das Angular-Frontend nutzt die hier dokumentierten Backend-Schnittstellen; die aktuelle Phase C umfasst insbesondere Cookie-Sitzung, lokale Benutzerverwaltung, Lizenzstatus, Health und Backups.

---

## 3. Technischer Überblick

Das Backend ist eine ASP.NET Core Web API.

### Eingesetzte Technologien

| Bereich | Technologie |
|---|---|
| Backend-Framework | ASP.NET Core 8 |
| Programmiersprache | C# |
| Datenzugriff | Entity Framework Core 8 |
| Lokale Datenbank | SQLite |
| Authentifizierung | ASP.NET Core Identity, HttpOnly-Cookie, CSRF-Schutz |
| Passwort-Hashing | Identity (Kompatibilität für vorhandene BCrypt-Hashes) |
| Excel-Export | ClosedXML |
| API-Dokumentation | Swagger / OpenAPI |
| Entwicklungsumgebung | Visual Studio Code |
| Shell | PowerShell |
| Versionsverwaltung | Git / GitHub |

---

## 4. Projektpfade

### Projekt-Root

```text
C:\Users\enric\dev\GitHub\HimiFlow
```

### Backend-Root

```text
C:\Users\enric\dev\GitHub\HimiFlow\backend\Einsparungs.Api
```

### Solution-Datei

```text
C:\Users\enric\dev\GitHub\HimiFlow\backend\EinsparungsApp.sln
```

### Lokale SQLite-Datenbank

```text
C:\Users\enric\dev\GitHub\HimiFlow\backend\Einsparungs.Api\einsparungen.db
```

Die Datenbankdatei wird nicht in Git versioniert.

---

## 5. Gesamtstruktur des Backends

Die Backend-Anwendung ist in mehrere fachliche und technische Ordner gegliedert.

```text
backend/
└── Einsparungs.Api/
    ├── Controllers/
    ├── Data/
    ├── DTOs/
    ├── Migrations/
    ├── Models/
    ├── Security/
    ├── appsettings.json
    ├── Program.cs
    └── Einsparungs.Api.csproj
```

---

## 6. Erklärung der Backend-Ordner

### 6.1 Controllers

Pfad:

```text
backend\Einsparungs.Api\Controllers
```

Der Ordner `Controllers` enthält die API-Endpunkte der Anwendung.

Jeder Controller stellt einen fachlichen Bereich über HTTP bereit.

Aktuell vorhandene Controller:

| Datei | Zweck |
|---|---|
| `AuthController.cs` | Login und aktuelle Benutzerinformationen |
| `MasterDataController.cs` | Stammdaten für Teams, Einspargründe und Produktgruppen |
| `SavingsController.cs` | Fach-API für Einsparungsdatensätze |
| `StatisticsController.cs` | Statistik- und Auswertungs-API |
| `ExportsController.cs` | CSV- und Excel-Export |
| `UserManagementController.cs` | Benutzer, Rollen und Einmalpasswörter |
| `LicenseController.cs` / `LicenseAdminController.cs` | Lizenzstatus und Lizenzinstallation |
| `OperationsController.cs` | Health-nahe Betriebsfunktionen und SQLite-Backups |

---

### 6.2 Data

Pfad:

```text
backend\Einsparungs.Api\Data
```

Der Ordner `Data` enthält alle datenbanknahen Komponenten.

Aktuell vorhandene Dateien:

| Datei | Zweck |
|---|---|
| `AppDbContext.cs` | Entity Framework Datenbankkontext |
| `DatabaseSeeder.cs` | Initiale Befüllung der Datenbank mit Rollen, Teams, Gründen und Produktgruppen; Initial-Admin nur über lokales Secret |

---

### 6.3 DTOs

Pfad:

```text
backend\Einsparungs.Api\DTOs
```

DTO steht für **Data Transfer Object**.

Diese Klassen definieren, welche Daten über die API angenommen oder zurückgegeben werden.

Die DTOs sind bewusst von den Datenbankmodellen getrennt. Dadurch wird verhindert, dass interne Datenbankstrukturen ungefiltert nach außen gegeben werden.

Aktuell vorhandene DTOs:

| Datei | Zweck |
|---|---|
| `LoginRequest.cs` | Eingabe für Login |
| `LoginResponse.cs` | Antwort nach erfolgreichem Login |
| `ChangePasswordRequest.cs` | Eingabe für Passwortwechsel |
| `SavingsEntryCreateRequest.cs` | Eingabe zum Erstellen eines Einsparungsdatensatzes |
| `SavingsEntryUpdateRequest.cs` | Eingabe zum Bearbeiten eines Einsparungsdatensatzes |
| `SavingsEntryResponse.cs` | Antwortmodell für Einsparungsdatensätze |
| `StatisticsOverviewResponse.cs` | Statistik-Gesamtübersicht |
| `MonthlySavingsStatisticResponse.cs` | Statistik nach Monat |
| `GroupedSavingsStatisticResponse.cs` | Gruppierte Statistik, zum Beispiel nach Team oder Einspargrund |

---

### 6.4 Models

Pfad:

```text
backend\Einsparungs.Api\Models
```

Der Ordner `Models` enthält die Entity-Klassen. Diese Klassen bilden die Tabellenstruktur der Datenbank ab.

Aktuell vorhandene Models:

| Datei | Zweck |
|---|---|
| `AppUser.cs` | Benutzer |
| `AppRole.cs` | Rolle |
| `AppUserRole.cs` | Zuordnung Benutzer zu Rolle |
| `Team.cs` | Team-Stammdaten |
| `SavingReason.cs` | Einspargrund-Stammdaten |
| `ProductGroup.cs` | Produktgruppen-Stammdaten |
| `SavingsEntry.cs` | Fachlicher Einsparungsdatensatz |
| `AuditLog.cs` | Änderungsprotokoll |

---

### 6.5 Security

Pfad:

```text
backend\Einsparungs.Api\Security
```

Der Ordner `Security` enthält sicherheitsrelevante Komponenten.

Aktuell vorhanden:

| Datei | Zweck |
|---|---|
| `LegacyCompatiblePasswordHasher.cs` | Prüft vorhandene BCrypt-Hashes und migriert sie bei erfolgreicher Anmeldung zu Identity |
| `ActiveUserCookieEvents.cs` | Validiert Sicherheitsstempel und aktive Benutzer bei jeder Sitzung |
| `PasswordChangeRequiredMiddleware.cs` | Sperrt Fach-API-Aufrufe bis zum Erstpasswortwechsel |
| `TemporaryPasswordGenerator.cs` | Erzeugt kryptografisch zufällige Einmalpasswörter |

---

### 6.6 Migrations

Pfad:

```text
backend\Einsparungs.Api\Migrations
```

Der Ordner `Migrations` enthält die Entity-Framework-Migrationen.

Migrationen beschreiben, wie die Datenbankstruktur erstellt oder verändert wird.

Im aktuellen Stand wurde eine Initialmigration für die erste vollständige Datenbankstruktur erstellt.

---

## 7. Zentrale Startdatei: Program.cs

Pfad:

```text
backend\Einsparungs.Api\Program.cs
```

Die Datei `Program.cs` ist der Einstiegspunkt der ASP.NET-Core-Anwendung.

Hier wird konfiguriert:

- Controller-Unterstützung
- SQLite-Datenbankverbindung
- Entity Framework Core
- ASP.NET-Core-Identity mit HttpOnly-Cookie und CSRF-Schutz
- Rollenbasierte Autorisierung
- Swagger/OpenAPI
- CORS für das spätere Angular-Frontend
- automatisches Datenbank-Seeding
- HTTP-Middleware-Pipeline

### Wichtige Aufgaben von Program.cs

#### 7.1 Controller registrieren

```csharp
builder.Services.AddControllersWithViews();
```

Dadurch werden alle Controller im Ordner `Controllers` als API-Endpunkte aktiviert.

---

#### 7.2 Datenbank registrieren

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});
```

Das Backend verwendet im lokalen Prototyp SQLite.

Die Verbindung wird aus `appsettings.json` gelesen.

---

#### 7.3 Identity und Sitzungsschutz registrieren

```csharp
builder.Services.AddIdentityCore<AppUser>()
    .AddSignInManager()
    .AddEntityFrameworkStores<AppDbContext>();
```

Die Anwendung setzt die Authentifizierungscookie `HimiFlow.Auth` mit `HttpOnly` und `SameSite=Strict`. Schreibende Anfragen benötigen den Header `X-XSRF-TOKEN`; der zugehörige lesbare Request-Token wird über `/api/auth/csrf` ausgegeben.

---

#### 7.4 Swagger konfigurieren

Swagger wird verwendet, um die API im Browser testen zu können.

Swagger dokumentiert die Routen weiterhin. Für geschützte Aufrufe muss eine Browser-Sitzung mit Cookie und CSRF-Token verwendet werden; ein Bearer-Token ist nicht mehr vorgesehen.

---

#### 7.5 Cookie-Authentifizierung konfigurieren

Die Anwendung verwendet die ASP.NET-Core-Identity-Cookie.

Dabei werden geprüft:

- aktive/deaktivierte Benutzer
- Sicherheitsstempel zur Sitzungswiderrufung
- Rollenclaims `Mitarbeiter`, `FachAdmin` und `SystemAdmin`
- erzwungener Passwortwechsel beim Erstlogin

Das SSO-/Active-Directory-Thema bleibt für eine spätere Erweiterung offen.

---

#### 7.6 CORS konfigurieren

Für das spätere Angular-Frontend wurde eine CORS-Regel eingerichtet.

Erlaubter Ursprung:

```text
http://localhost:4200
```

Das ist die Standardadresse einer lokalen Angular-Anwendung.

---

#### 7.7 Datenbank-Seeding beim Start

Beim Start der Anwendung wird automatisch der Seeder ausgeführt:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    await DatabaseSeeder.SeedAsync(db, userManager, app.Configuration);
}
```

Dadurch wird sichergestellt, dass die lokale Datenbank die notwendigen Grunddaten enthält.

---

## 8. Konfigurationsdatei: appsettings.json

Pfad:

```text
backend\Einsparungs.Api\appsettings.json
```

Diese Datei enthält zentrale Konfigurationswerte.

### Aktuelle Inhalte

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=einsparungen.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "localhost;127.0.0.1"
}
```

### Bedeutung der Werte

| Abschnitt | Bedeutung |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQLite-Verbindung zur lokalen Datenbank |
| `InitialAdmin:TemporaryPassword` | lokales User Secret für den ersten System-Admin einer leeren Datenbank |
| `Logging` | Logging-Konfiguration |
| `AllowedHosts` | Host-Einschränkung für ASP.NET Core |

### Administrativer Hinweis

Das Initialpasswort des ersten System-Admins ist nicht Bestandteil von `appsettings.json` oder des Repositories. Im lokalen Entwicklungsbetrieb wird es über .NET User Secrets bereitgestellt. In anderen Umgebungen muss es über einen sicheren Konfigurationsanbieter gesetzt werden, zum Beispiel über:

- Umgebungsvariablen
- Secret Store
- Server-Konfiguration
- Key Vault oder vergleichbare interne Lösung

Ein lokales Initialpasswort kann mit einem eigenen starken Wert gesetzt werden:

```powershell
dotnet user-secrets set "InitialAdmin:TemporaryPassword" "<eigenes-starkes-passwort>" --project ".\backend\Einsparungs.Api\Einsparungs.Api.csproj"
```

---

## 9. Datenbank und Entity Framework

### 9.1 Datenbanktechnologie

Im lokalen Prototyp wird SQLite verwendet.

Vorteile für den Prototyp:

- keine separate Datenbankinstallation notwendig
- einfache lokale Entwicklung
- Datenbank liegt als Datei im Projektordner
- schnell zurücksetzbar
- gut für klickbare Prototypen geeignet

Langfristig ist eine Migration auf SQL Server vorgesehen.

---

### 9.2 Datenbankdatei

Die SQLite-Datenbank wird automatisch lokal erzeugt:

```text
einsparungen.db
```

Zusätzlich können temporäre SQLite-Dateien entstehen:

```text
einsparungen.db-shm
einsparungen.db-wal
```

Diese Dateien sind lokal und werden nicht versioniert.

---

### 9.3 Git-Ausschluss

Die Datenbankdateien sind über `.gitignore` ausgeschlossen.

Relevante Einträge:

```text
*.db
*.db-shm
*.db-wal
```

Dadurch wird verhindert, dass lokale Daten versehentlich in GitHub landen.

---

### 9.4 Migrationen

Migrationen wurden mit Entity Framework Core erstellt.

Typische Befehle:

```powershell
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Im aktuellen Betrieb wird die Datenbank beim Start zusätzlich über den Seeder migriert:

```csharp
await db.Database.MigrateAsync();
```

Das bedeutet:

- Wenn die Datenbank noch nicht existiert, wird sie erstellt.
- Wenn Migrationen fehlen, werden sie angewendet.
- Danach werden Stammdaten angelegt.

---

## 10. Datenmodell

Das Datenmodell besteht aus mehreren miteinander verbundenen Tabellen.

---

### 10.1 AppUser

Datei:

```text
Models\AppUser.cs
```

Zweck:

Speichert Benutzer der Anwendung.

Wichtige Felder:

| Feld | Bedeutung |
|---|---|
| `Id` | Eindeutige Benutzer-ID |
| `UserName` | Login-Name |
| `DisplayName` | Anzeigename |
| `PasswordHash` | ASP.NET-Core-Identity-Hash; vorhandene BCrypt-Hashes werden beim erfolgreichen Login kompatibel übernommen |
| `TeamId` | Optionales Team des Benutzers |
| `IsActive` | Gibt an, ob der Benutzer aktiv ist |
| `CreatedAt` | Erstellungszeitpunkt |

Beziehungen:

- Ein Benutzer kann einem Team zugeordnet sein.
- Ein Benutzer kann mehrere Rollen haben.
- Ein Benutzer kann Einsparungsdatensätze erstellen.

---

### 10.2 AppRole

Datei:

```text
Models\AppRole.cs
```

Zweck:

Speichert Rollen der Anwendung.

Aktuelle Rollen:

```text
Mitarbeiter
FachAdmin
SystemAdmin
```

`FachAdmin` entspricht der fachlichen Führungskraftrolle. `SystemAdmin` ist für IT-/Systemverwaltung vorgesehen.

---

### 10.3 AppUserRole

Datei:

```text
Models\AppUserRole.cs
```

Zweck:

Verknüpft Benutzer mit Rollen.

Ein Benutzer kann theoretisch mehrere Rollen besitzen.

Die Tabelle ist eine klassische n:m-Zwischentabelle.

---

### 10.4 Team

Datei:

```text
Models\Team.cs
```

Zweck:

Speichert die Team-Stammdaten.

Aktuelle Teams:

| Code | Name |
|---|---|
| 3410 | Bochum 1 |
| 3420 | Bochum 2 |
| 3430 | Bochum 3 |
| 3440 | Ruesselsheim |
| 3450 | Luebeck |

Feld `DisplayName` enthält die spätere Anzeigeform, zum Beispiel:

```text
Bochum 1 (3410)
```

---

### 10.5 SavingReason

Datei:

```text
Models\SavingReason.cs
```

Zweck:

Speichert die Einspargründe.

Aktuelle Einspargründe:

```text
vollstaendig keine med. Notwendigkeit
teilweise keine med. Notwendigkeit
Lagerversorgung
Kuerzung auf Vertragspreis
Kuerzung allgemein
Rabatt
Umversorgung auf anderes Himi
```

---

### 10.6 ProductGroup

Datei:

```text
Models\ProductGroup.cs
```

Zweck:

Speichert Produktgruppen.

Aktuell sind Demo-Produktgruppen hinterlegt.

Beispiel:

```text
18.50.03.0xxx, Aktivrollstuhl
```

Die Produktgruppen können später aus einer Excel-Datei oder anderen Quelle importiert werden.

---

### 10.7 SavingsEntry

Datei:

```text
Models\SavingsEntry.cs
```

Zweck:

Speichert den eigentlichen fachlichen Einsparungsdatensatz.

Wichtige Felder:

| Feld | Bedeutung |
|---|---|
| `Id` | Eindeutige Datensatz-ID |
| `Month` | Monat der Einsparung |
| `Kvnr` | KVNR |
| `OldKvAmount` | Alter KV-Betrag |
| `NewKvAmount` | Neuer KV-Betrag |
| `SavingAmount` | Automatisch berechnete Ersparnis |
| `TeamId` | Team |
| `SavingReasonId` | Einspargrund |
| `ProductGroupId` | Produktgruppe |
| `TransmissionDate` | Übermittlungsdatum |
| `CreatedByUserId` | Ersteller |
| `CreatedAt` | Erstellungszeitpunkt |
| `UpdatedByUserId` | Letzter Bearbeiter |
| `UpdatedAt` | Letzter Änderungszeitpunkt |
| `IsDeleted` | Soft-Delete-Kennzeichen |
| `DeletedByUserId` | Benutzer, der gelöscht hat |
| `DeletedAt` | Löschzeitpunkt |
| `Version` | Versionsnummer |

---

### 10.8 AuditLog

Datei:

```text
Models\AuditLog.cs
```

Zweck:

Speichert Änderungsprotokolle.

Auditiert werden:

- Erstellung
- Änderung
- Löschung

Wichtige Felder:

| Feld | Bedeutung |
|---|---|
| `EntityName` | Name der betroffenen Entität |
| `EntityId` | ID des betroffenen Datensatzes |
| `Action` | Aktion, zum Beispiel Created, Updated, Deleted |
| `ChangedByUserId` | Benutzer, der die Änderung durchgeführt hat |
| `ChangedAt` | Zeitpunkt der Änderung |
| `OldValuesJson` | Alte Werte |
| `NewValuesJson` | Neue Werte |
| `ChangedFieldsJson` | Optional für spätere Feldänderungen |
| `ClientIp` | IP-Adresse des Clients |
| `UserAgent` | Browser/User-Agent |

---

## 11. Datenbankkontext: AppDbContext

Datei:

```text
Data\AppDbContext.cs
```

Der `AppDbContext` ist die zentrale Verbindung zwischen C#-Code und Datenbank.

Er enthält die `DbSet`-Definitionen für alle Tabellen:

```csharp
public DbSet<AppUser> Users => Set<AppUser>();
public DbSet<AppRole> Roles => Set<AppRole>();
public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();
public DbSet<Team> Teams => Set<Team>();
public DbSet<SavingReason> SavingReasons => Set<SavingReason>();
public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
public DbSet<SavingsEntry> SavingsEntries => Set<SavingsEntry>();
public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
```

---

### 11.1 Wichtige Datenbankregeln

Im `OnModelCreating` werden technische Regeln definiert.

Dazu gehören:

- eindeutiger Benutzername
- eindeutiger Rollenname
- eindeutiger Team-Code
- Beziehungen zwischen Tabellen
- Löschverhalten
- Check Constraints

---

### 11.2 Check Constraints

Für Einsparungsdatensätze wurden Datenbankregeln hinterlegt:

```text
OldKvAmount >= 0
NewKvAmount >= 0
NewKvAmount <= OldKvAmount
length(Kvnr) = 10
```

Das bedeutet:

Selbst wenn fehlerhafte Daten an der API vorbei in die Datenbank geschrieben werden sollten, schützt die Datenbank zusätzlich vor ungültigen Werten.

---

## 12. Initialdaten: DatabaseSeeder

Datei:

```text
Data\DatabaseSeeder.cs
```

Der Seeder befüllt die Datenbank beim Start automatisch mit Grunddaten.

### 12.1 Seeder-Ablauf

Beim Start wird ausgeführt:

```csharp
await DatabaseSeeder.SeedAsync(db);
```

Diese Methode führt aus:

```text
1. Migrationen anwenden
2. Rollen anlegen
3. Teams anlegen
4. Einspargründe anlegen
5. Produktgruppen anlegen
6. Demo-Benutzer anlegen
```

---

### 12.2 Angelegte Rollen

```text
Mitarbeiter
Fuehrungskraft
Admin
```

---

### 12.3 Angelegte Teams

```text
3410 Bochum 1
3420 Bochum 2
3430 Bochum 3
3440 Ruesselsheim
3450 Luebeck
```

---

### 12.4 Angelegte Demo-Benutzer

| Benutzername | Passwort | Rolle |
|---|---|---|
| `mitarbeiter1` | `Demo123!` | Mitarbeiter |
| `mitarbeiter2` | `Demo123!` | Mitarbeiter |
| `teamleiter` | `Demo123!` | Fuehrungskraft |
| `admin` | `Demo123!` | Admin |

Die Passwörter werden nicht im Klartext gespeichert, sondern mit BCrypt gehasht.

---

### 12.5 Administrativer Hinweis zu Demo-Benutzern

Die Demo-Benutzer sind nur für den lokalen Prototyp vorgesehen.

Für eine produktionsnahe Umgebung müssen Demo-Benutzer entfernt oder durch ein echtes Benutzerkonzept ersetzt werden.

---

## 13. Authentifizierung

### 13.1 Prinzip

Die Anwendung verwendet JWT Bearer Authentication.

Ablauf:

```text
1. Benutzer sendet Benutzername und Passwort an /api/auth/login
2. Backend prüft Benutzer und Passwort
3. Backend erzeugt JWT-Token
4. Client speichert Token
5. Client sendet Token bei geschützten API-Aufrufen im Authorization Header
```

Header-Beispiel:

```text
Authorization: Bearer <token>
```

---

### 13.2 Passwortprüfung

Passwörter werden mit BCrypt geprüft:

```csharp
BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)
```

Das Klartextpasswort wird nie gespeichert.

---

### 13.3 JWT-Inhalte

Das Token enthält unter anderem:

- Benutzer-ID
- Benutzername
- Anzeigename
- Rolle

Wichtige Claims:

```text
userId
userName
displayName
role
ClaimTypes.Role
```

Die Rollenclaims werden für `[Authorize(Roles = "...")]` verwendet.

---

## 14. Autorisierung und Rollenlogik

Die Anwendung unterscheidet aktuell drei Rollen:

```text
Mitarbeiter
Fuehrungskraft
Admin
```

### 14.1 Mitarbeiter

Mitarbeiter dürfen:

- Login durchführen
- eigene Einsparungen sehen
- eigene Einsparungen anlegen
- eigene Einsparungen bearbeiten
- eigene Einsparungen löschen
- globale Statistiken sehen

Mitarbeiter dürfen nicht:

- alle Datensätze sehen
- CSV exportieren
- Excel exportieren

---

### 14.2 Fuehrungskraft

Führungskräfte dürfen:

- alle Einsparungsdatensätze sehen
- alle Einsparungsdatensätze bearbeiten
- alle Einsparungsdatensätze löschen
- globale Statistiken sehen
- CSV exportieren
- Excel exportieren

---

### 14.3 Admin

Admins dürfen:

- alle Einsparungsdatensätze sehen
- alle Einsparungsdatensätze bearbeiten
- alle Einsparungsdatensätze löschen
- globale Statistiken sehen
- CSV exportieren
- Excel exportieren

---

## 15. AuthController

Datei:

```text
Controllers\AuthController.cs
```

Zweck:

Der `AuthController` stellt Login und Benutzerinformationen bereit.

### 15.1 Endpunkte

```text
POST /api/auth/login
GET  /api/auth/me
```

---

### 15.2 POST /api/auth/login

Zweck:

Meldet einen Benutzer an.

Request-Beispiel:

```json
{
  "userName": "mitarbeiter1",
  "password": "Demo123!"
}
```

Antwort-Beispiel:

```json
{
  "token": "...",
  "userId": "...",
  "userName": "mitarbeiter1",
  "displayName": "Mitarbeiter Eins",
  "roles": [
    "Mitarbeiter"
  ],
  "expiresAt": "2026-07-02T05:15:16Z"
}
```

---

### 15.3 GET /api/auth/me

Zweck:

Gibt Informationen zum aktuell angemeldeten Benutzer zurück.

Benötigt JWT-Token.

Antwort-Beispiel:

```json
{
  "userId": "...",
  "userName": "teamleiter",
  "displayName": "Teamleiter Demo",
  "roles": [
    "Fuehrungskraft"
  ]
}
```

---

## 16. JwtTokenService

Datei:

```text
Security\JwtTokenService.cs
```

Zweck:

Der `JwtTokenService` erzeugt JWT-Tokens nach erfolgreichem Login.

### 16.1 Aufgaben

- liest JWT-Konfiguration aus `appsettings.json`
- erstellt Claims
- fügt Rollenclaims hinzu
- signiert Token mit HMAC SHA256
- setzt Ablaufzeit

### 16.2 Aktuelle Token-Laufzeit

```text
8 Stunden
```

Die Laufzeit wird im Code gesetzt:

```csharp
DateTime.UtcNow.AddHours(8)
```

---

## 17. MasterDataController

Datei:

```text
Controllers\MasterDataController.cs
```

Zweck:

Liefert Stammdaten für die spätere Eingabemaske.

### 17.1 Endpunkte

```text
GET /api/master-data/teams
GET /api/master-data/saving-reasons
GET /api/master-data/product-groups
```

---

### 17.2 GET /api/master-data/teams

Gibt aktive Teams zurück.

Beispielantwort:

```json
[
  {
    "id": 1,
    "code": "3410",
    "name": "Bochum 1",
    "displayName": "Bochum 1 (3410)"
  }
]
```

---

### 17.3 GET /api/master-data/saving-reasons

Gibt aktive Einspargründe zurück.

---

### 17.4 GET /api/master-data/product-groups

Gibt aktive Produktgruppen zurück.

Optionaler Suchparameter:

```text
?search=Rollstuhl
```

Beispiel:

```text
GET /api/master-data/product-groups?search=Rollstuhl
```

---

## 18. SavingsController

Datei:

```text
Controllers\SavingsController.cs
```

Zweck:

Der `SavingsController` ist die zentrale Fach-API für Einsparungsdatensätze.

Der Controller ist geschützt:

```csharp
[Authorize]
```

Das bedeutet:

Alle Endpunkte erfordern einen gültigen Login.

---

### 18.1 Endpunkte

```text
GET    /api/savings/my
GET    /api/savings/all
GET    /api/savings/{id}
POST   /api/savings
PUT    /api/savings/{id}
DELETE /api/savings/{id}
```

---

### 18.2 GET /api/savings/my

Zweck:

Gibt nur die eigenen Einsparungsdatensätze des angemeldeten Benutzers zurück.

Berechtigung:

```text
Alle eingeloggten Benutzer
```

Fachliche Logik:

```text
CreatedByUserId == aktueller Benutzer
```

---

### 18.3 GET /api/savings/all

Zweck:

Gibt alle nicht gelöschten Einsparungsdatensätze zurück.

Berechtigung:

```text
Nur Fuehrungskraft und Admin
```

Technische Absicherung:

```csharp
[Authorize(Roles = "Fuehrungskraft,Admin")]
```

---

### 18.4 GET /api/savings/{id}

Zweck:

Gibt einen einzelnen Einsparungsdatensatz zurück.

Berechtigung:

- Mitarbeiter: nur eigene Datensätze
- Fuehrungskraft/Admin: alle Datensätze

---

### 18.5 POST /api/savings

Zweck:

Erstellt einen neuen Einsparungsdatensatz.

Berechtigung:

```text
Alle eingeloggten Benutzer
```

Fachliche Verarbeitung:

```text
1. Benutzer-ID aus JWT lesen
2. Eingaben validieren
3. Beträge runden
4. Monat normalisieren
5. Ersparnis berechnen
6. Datensatz speichern
7. AuditLog schreiben
8. Antwort zurückgeben
```

Beispielrequest:

```json
{
  "month": "2026-06-01T00:00:00",
  "kvnr": "A123456789",
  "oldKvAmount": 3011.01,
  "newKvAmount": 0,
  "teamId": 3,
  "savingReasonId": 1,
  "productGroupId": 1
}
```

---

### 18.6 PUT /api/savings/{id}

Zweck:

Bearbeitet einen bestehenden Einsparungsdatensatz.

Berechtigung:

- Mitarbeiter: nur eigene Datensätze
- Fuehrungskraft/Admin: alle Datensätze

Bei Änderung wird:

- alter Zustand als Audit-Snapshot gespeichert
- neuer Zustand gespeichert
- `UpdatedByUserId` gesetzt
- `UpdatedAt` gesetzt
- `Version` erhöht

---

### 18.7 DELETE /api/savings/{id}

Zweck:

Löscht einen Datensatz logisch.

Es handelt sich um einen Soft Delete.

Das bedeutet:

```text
Der Datensatz bleibt in der Datenbank.
IsDeleted wird auf true gesetzt.
DeletedByUserId wird gesetzt.
DeletedAt wird gesetzt.
```

Berechtigung:

- Mitarbeiter: nur eigene Datensätze
- Fuehrungskraft/Admin: alle Datensätze

---

## 19. Fachliche Validierung im SavingsController

Die Methode `ValidateSavingsRequestAsync` prüft die fachlichen Regeln.

Aktuelle Regeln:

```text
Monat ist Pflichtfeld.
KVNR ist Pflichtfeld.
KVNR muss genau 10 Zeichen haben.
Alter KV darf nicht kleiner als 0 sein.
Neuer KV darf nicht kleiner als 0 sein.
Neuer KV muss kleiner oder gleich alter KV sein.
Team muss existieren und aktiv sein.
Einspargrund muss existieren und aktiv sein.
Produktgruppe muss existieren und aktiv sein.
```

Wenn eine Regel verletzt wird, gibt die API `400 Bad Request` zurück.

Beispiel:

```json
{
  "errors": [
    "KVNR muss genau 10 Zeichen haben.",
    "Neuer KV muss kleiner oder gleich alter KV sein."
  ]
}
```

---

## 20. Ersparnisberechnung

Die Ersparnis wird im Backend berechnet.

Formel:

```text
Ersparnis = Alter KV-Betrag - Neuer KV-Betrag
```

Beispiel:

```text
Alter KV: 3011,01
Neuer KV: 0,00
Ersparnis: 3011,01
```

Die Ersparnis wird nicht vom Client übernommen, sondern serverseitig gesetzt.

Das verhindert Manipulation oder fehlerhafte Berechnung im Frontend.

---

## 21. Monatsnormalisierung

Der Monat wird normalisiert:

```csharp
new DateTime(month.Year, month.Month, 1)
```

Das bedeutet:

Wenn ein Benutzer ein Datum im Monat Juni 2026 sendet, wird gespeichert:

```text
01.06.2026
```

Damit können Monatsstatistiken zuverlässig gruppiert werden.

---

## 22. Audit-Logging

Audit-Logging ist im `SavingsController` integriert.

Bei folgenden Aktionen wird ein AuditLog geschrieben:

```text
Created
Updated
Deleted
```

### 22.1 Was wird gespeichert?

- Entitätsname
- Entitäts-ID
- Aktion
- Benutzer-ID
- Zeitpunkt
- alte Werte als JSON
- neue Werte als JSON
- Client-IP
- User-Agent

### 22.2 Beispiel für EntityName

```text
SavingsEntry
```

### 22.3 Nutzen

AuditLogs ermöglichen später:

- Nachvollziehbarkeit
- Prüfung von Änderungen
- Fehleranalyse
- Transparenz bei Datensatzänderungen
- mögliche Revisionsauswertungen

---

## 23. Soft Delete

Löschungen erfolgen logisch.

Das bedeutet:

Datensätze werden nicht physisch gelöscht.

Stattdessen werden folgende Felder gesetzt:

```text
IsDeleted = true
DeletedByUserId = aktueller Benutzer
DeletedAt = aktueller Zeitpunkt
Version = Version + 1
```

Alle normalen Abfragen filtern gelöschte Datensätze aus:

```csharp
.Where(x => !x.IsDeleted)
```

---

## 24. StatisticsController

Datei:

```text
Controllers\StatisticsController.cs
```

Zweck:

Stellt globale Statistiken über Einsparungsdaten bereit.

Der Controller ist geschützt:

```csharp
[Authorize]
```

Das bedeutet:

Alle eingeloggten Benutzer dürfen Statistiken sehen.

---

### 24.1 Endpunkte

```text
GET /api/statistics/overview
GET /api/statistics/monthly
GET /api/statistics/by-team
GET /api/statistics/by-saving-reason
GET /api/statistics/by-product-group
```

---

### 24.2 GET /api/statistics/overview

Gibt eine Gesamtübersicht zurück.

Werte:

- Anzahl Datensätze
- Gesamtersparnis
- Durchschnittliche Ersparnis
- Höchste Ersparnis
- Niedrigste Ersparnis

Beispielantwort:

```json
{
  "entryCount": 1,
  "totalSavingAmount": 3011.01,
  "averageSavingAmount": 3011.01,
  "highestSavingAmount": 3011.01,
  "lowestSavingAmount": 3011.01
}
```

---

### 24.3 GET /api/statistics/monthly

Gruppiert Einsparungen nach Monat.

---

### 24.4 GET /api/statistics/by-team

Gruppiert Einsparungen nach Team.

---

### 24.5 GET /api/statistics/by-saving-reason

Gruppiert Einsparungen nach Einspargrund.

---

### 24.6 GET /api/statistics/by-product-group

Gruppiert Einsparungen nach Produktgruppe.

---

## 25. ExportsController

Datei:

```text
Controllers\ExportsController.cs
```

Zweck:

Stellt Exportfunktionen bereit.

Der gesamte Controller ist rollenbasiert geschützt:

```csharp
[Authorize(Roles = "Fuehrungskraft,Admin")]
```

Das bedeutet:

Nur Führungskräfte und Admins dürfen exportieren.

Mitarbeiter erhalten:

```text
403 Forbidden
```

---

### 25.1 Endpunkte

```text
GET /api/exports/savings.csv
GET /api/exports/savings.xlsx
```

---

### 25.2 CSV-Export

Endpunkt:

```text
GET /api/exports/savings.csv
```

Exportiert alle nicht gelöschten Einsparungsdatensätze als CSV.

Format:

- Trennzeichen: Semikolon
- Textwerte: in Anführungszeichen
- Encoding: UTF-8 mit BOM
- Beträge: deutsches Zahlenformat
- Monat: MM.yyyy

CSV-Spalten:

```text
Id
Monat
KVNR
Alter KV
Neuer KV
Ersparnis
Team
Einspargrund
Produktgruppe
Uebermittlungsdatum
Erstellt von
Erstellt am
Version
```

---

### 25.3 Excel-Export

Endpunkt:

```text
GET /api/exports/savings.xlsx
```

Exportiert alle nicht gelöschten Einsparungsdatensätze als Excel-Datei.

Verwendete Bibliothek:

```text
ClosedXML
```

Excel-Details:

- Arbeitsblattname: Einsparungen
- Kopfzeile fett
- Kopfzeile grau hinterlegt
- Datumsformatierung
- Zahlenformatierung für Beträge
- automatische Spaltenbreite

---

### 25.4 Export-Berechtigung

| Rolle | CSV | Excel |
|---|---:|---:|
| Mitarbeiter | Nein | Nein |
| Fuehrungskraft | Ja | Ja |
| Admin | Ja | Ja |

---

## 26. API-Gesamtübersicht

### 26.1 Auth

```text
POST /api/auth/login
GET  /api/auth/me
```

### 26.2 Master Data

```text
GET /api/master-data/teams
GET /api/master-data/saving-reasons
GET /api/master-data/product-groups
```

### 26.3 Savings

```text
GET    /api/savings/my
GET    /api/savings/all
GET    /api/savings/{id}
POST   /api/savings
PUT    /api/savings/{id}
DELETE /api/savings/{id}
```

### 26.4 Statistics

```text
GET /api/statistics/overview
GET /api/statistics/monthly
GET /api/statistics/by-team
GET /api/statistics/by-saving-reason
GET /api/statistics/by-product-group
```

### 26.5 Exports

```text
GET /api/exports/savings.csv
GET /api/exports/savings.xlsx
```

---

## 27. Lokaler Betrieb

### 27.1 Voraussetzungen

Auf dem lokalen Entwicklungsrechner müssen vorhanden sein:

```text
.NET SDK 8
Visual Studio Code
PowerShell
Git
```

Für das spätere Frontend zusätzlich:

```text
Node.js
npm
Angular CLI
```

---

### 27.2 Backend starten

In PowerShell:

```powershell
cd C:\Users\enric\OneDrive\Dokumente\GitHub\First-Procect\backend\Einsparungs.Api
dotnet run
```

---

### 27.3 Backend stoppen

Im laufenden Terminal:

```text
STRG + C
```

---

### 27.4 Backend-Adresse

Die API läuft lokal unter:

```text
http://localhost:5281
```

Root-Test:

```text
http://localhost:5281
```

Erwartete Antwort:

```text
Einsparungs API laeuft.
```

---

### 27.5 Swagger öffnen

```text
http://localhost:5281/swagger
```

Swagger dient zum Testen der API-Endpunkte.

---

## 28. Lokale Tests mit Swagger

### 28.1 Login testen

Endpunkt:

```text
POST /api/auth/login
```

Body:

```json
{
  "userName": "mitarbeiter1",
  "password": "Demo123!"
}
```

Nach erfolgreichem Login wird ein Token zurückgegeben.

---

### 28.2 Swagger autorisieren

Oben rechts auf **Authorize** klicken.

Einfügen:

```text
Bearer <token>
```

Wichtig:

Die Schreibweise muss exakt sein:

```text
Bearer
```

Nicht:

```text
Baerer
```

---

### 28.3 Eigene Einsparungen testen

```text
GET /api/savings/my
```

Mit Mitarbeiter-Token erlaubt.

---

### 28.4 Alle Einsparungen testen

```text
GET /api/savings/all
```

Erwartung:

| Benutzer | Ergebnis |
|---|---|
| mitarbeiter1 | 403 Forbidden |
| teamleiter | 200 OK |
| admin | 200 OK |

---

### 28.5 Export testen

```text
GET /api/exports/savings.csv
GET /api/exports/savings.xlsx
```

Erwartung:

| Benutzer | Ergebnis |
|---|---|
| mitarbeiter1 | 403 Forbidden |
| teamleiter | Datei wird erzeugt |
| admin | Datei wird erzeugt |

---

## 29. Lokale Tests mit PowerShell

### 29.1 Login

```powershell
$loginBody = @{
    userName = "teamleiter"
    password = "Demo123!"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod `
    -Uri "http://localhost:5281/api/auth/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body $loginBody

$loginResponse
```

---

### 29.2 Header mit Token bauen

```powershell
$headers = @{
    Authorization = "Bearer $($loginResponse.token)"
}
```

---

### 29.3 Aktuellen Benutzer testen

```powershell
Invoke-RestMethod `
    -Uri "http://localhost:5281/api/auth/me" `
    -Method Get `
    -Headers $headers
```

---

### 29.4 Alle Einsparungen abrufen

```powershell
Invoke-RestMethod `
    -Uri "http://localhost:5281/api/savings/all" `
    -Method Get `
    -Headers $headers
```

---

## 30. Typische HTTP-Statuscodes

| Statuscode | Bedeutung |
|---|---|
| 200 OK | Anfrage erfolgreich |
| 201 Created | Datensatz wurde erstellt |
| 204 No Content | Löschung erfolgreich |
| 400 Bad Request | Validierungsfehler |
| 401 Unauthorized | Kein oder ungültiger Token |
| 403 Forbidden | Benutzer ist angemeldet, aber nicht berechtigt |
| 404 Not Found | Datensatz nicht gefunden |
| 500 Internal Server Error | Unerwarteter Serverfehler |

---

## 31. Häufige Fehler und Lösungen

### 31.1 Swagger zeigt 401 Unauthorized

Ursache:

- Kein Token gesetzt
- Token abgelaufen
- Token falsch eingefügt
- `Bearer` fehlt

Lösung:

Neu einloggen und in Swagger einfügen:

```text
Bearer <token>
```

---

### 31.2 Swagger zeigt 403 Forbidden

Ursache:

Der Benutzer ist angemeldet, hat aber nicht die erforderliche Rolle.

Beispiel:

```text
mitarbeiter1 ruft /api/savings/all auf
```

Das ist fachlich korrekt verboten.

---

### 31.3 Build-Fehler wegen fehlendem Namespace

Mögliche Ursache:

- Datei leer
- Datei nicht gespeichert
- falscher Namespace
- VS Code hat alten Stand überschrieben

Lösung:

Datei schließen, nicht speichern, neu öffnen und mit `dotnet build` prüfen.

---

### 31.4 SQLite-Datenbank enthält alte Daten

Die lokale Datenbank kann zurückgesetzt werden.

Backend stoppen und ausführen:

```powershell
Remove-Item .\einsparungen.db -Force -ErrorAction SilentlyContinue
Remove-Item .\einsparungen.db-shm -Force -ErrorAction SilentlyContinue
Remove-Item .\einsparungen.db-wal -Force -ErrorAction SilentlyContinue
dotnet run
```

Beim nächsten Start wird die Datenbank neu erstellt und erneut geseedet.

---

### 31.5 Port ist belegt

Wenn `dotnet run` nicht starten kann, weil der Port belegt ist:

- prüfen, ob ein altes Backend-Terminal noch läuft
- dort `STRG + C` drücken
- danach erneut starten

---

## 32. Git-Betrieb

### 32.1 Entwicklungsbranch

Aktiver Entwicklungsbranch:

```text
Testumgebung
```

### 32.2 Stabiler Branch

Stabiler Demo-Branch:

```text
main
```

---

### 32.3 Status prüfen

```powershell
git status
```

---

### 32.4 Änderungen committen

```powershell
git add .
git commit -m "Beschreibung der Änderung"
git push origin Testumgebung
```

---

### 32.5 Wichtiger Hinweis

Vor jedem Commit prüfen, dass keine lokalen Datenbankdateien aufgenommen werden:

```powershell
git status --short
```

Folgende Dateien dürfen nicht committed werden:

```text
einsparungen.db
einsparungen.db-shm
einsparungen.db-wal
bin/
obj/
```

---

## 33. Sicherheitsbewertung des aktuellen Prototyps

Der aktuelle Stand ist ein lokaler Prototyp.

Er ist funktional, aber noch nicht produktionsreif.

### 33.1 Für lokale Entwicklung geeignet

Geeignet für:

- lokale Tests
- Demo
- fachliche Validierung
- Frontend-Entwicklung
- Prototyping

### 33.2 Nicht produktiv verwenden ohne Anpassung

Vor produktionsnaher Nutzung müssen angepasst werden:

- JWT-Key sicher verwalten
- Demo-Benutzer entfernen
- Passwortkonzept finalisieren
- Benutzerverwaltung erweitern
- HTTPS erzwingen
- Logging-Konzept definieren
- Fehlerbehandlung vereinheitlichen
- Datenbank auf SQL Server migrieren
- Backup- und Restore-Konzept erstellen
- Datenschutz- und Berechtigungskonzept final prüfen

---

## 34. Aktuelle fachliche Umsetzung

Im Backend sind aktuell folgende fachliche Anforderungen umgesetzt:

```text
Benutzer können sich anmelden.
Benutzer erhalten JWT-Token.
Benutzer haben Rollen.
Mitarbeiter sehen eigene Datensätze.
Führungskräfte und Admins sehen alle Datensätze.
Mitarbeiter dürfen keine Exporte durchführen.
Führungskräfte und Admins dürfen CSV und Excel exportieren.
Statistik ist für alle angemeldeten Benutzer sichtbar.
Ersparnis wird automatisch berechnet.
KVNR muss genau 10 Zeichen haben.
Neuer KV darf nicht größer als alter KV sein.
Negative Beträge sind nicht erlaubt.
Änderungen werden auditiert.
Löschungen erfolgen als Soft Delete.
```

---

## 35. Aktueller Backend-Fertigstellungsstand

Der Backend-Kern ist abgeschlossen.

Fertig umgesetzt:

- Projektstruktur
- ASP.NET Core API
- SQLite-Datenbank
- Entity Framework Core
- Migrationen
- Stammdaten-Seeding
- Demo-Benutzer
- Login
- JWT
- Rollenprüfung
- Stammdaten-API
- Einsparungs-Fach-API
- Statistik-API
- CSV-Export
- Excel-Export
- Exportberechtigung
- AuditLog
- Soft Delete
- Swagger-Testbarkeit

---

## 36. Noch offene Punkte für spätere Ausbaustufen

Nicht Bestandteil des aktuellen Backend-Stands, aber perspektivisch relevant:

- echte Benutzerverwaltung
- Passwort ändern
- Benutzer deaktivieren
- Rollenverwaltung über Adminoberfläche
- Produktgruppenimport aus Excel
- Filterparameter für Statistik
- Filterparameter für Export
- Zeitraumfilter
- Teamfilter
- Pseudonymisierung oder Maskierung je nach Datenschutzanforderung
- SQL-Server-Migration
- produktionsnahes Logging
- Health Checks
- zentrale Fehlerbehandlung
- Deployment-Dokumentation
- Backup/Restore-Dokumentation
- Frontend-Anbindung
- automatisierte Tests

---

## 37. Vorbereitung für SQL Server

Aktuell wird SQLite verwendet:

```json
"DefaultConnection": "Data Source=einsparungen.db"
```

Für SQL Server müsste perspektivisch umgestellt werden auf einen SQL-Server-Connection-String.

Beispielhaft:

```json
"DefaultConnection": "Server=<SERVERNAME>;Database=<DATENBANK>;Trusted_Connection=True;TrustServerCertificate=True;"
```

Zusätzlich müsste in `Program.cs` statt SQLite SQL Server verwendet werden.

Aktuell:

```csharp
options.UseSqlite(...)
```

Später:

```csharp
options.UseSqlServer(...)
```

Dafür wäre zusätzlich das passende EF-Core-SQL-Server-Paket erforderlich.

---

## 38. Betriebliches Zielbild

Das spätere Zielbild ist:

```text
Angular Frontend
        |
        v
ASP.NET Core Backend API
        |
        v
SQL Server Datenbank
```

Möglicher interner Betrieb:

- Frontend und Backend auf interner VM
- Datenbank auf internem SQL Server
- Zugriff nur aus internem Netz
- Firewall-Regeln für Datenbankzugriff
- zentrale Sicherung der Datenbank
- kontrollierte Benutzer- und Rollenverwaltung

---

## 39. Zusammenfassung für Administratoren

Das Backend ist der zentrale technische Kern der Einsparungsdatenbank.

Es übernimmt:

- Authentifizierung
- Autorisierung
- fachliche Validierung
- Datenhaltung
- Änderungshistorie
- Statistik
- Export

Die wichtigsten Administrationspunkte sind:

```text
Backend starten:
dotnet run

Swagger öffnen:
http://localhost:5281/swagger

Datenbank zurücksetzen:
einsparungen.db löschen und Backend neu starten

Benutzer testen:
mitarbeiter1 / Demo123!
teamleiter / Demo123!
admin / Demo123!

Exportrechte prüfen:
Mitarbeiter = verboten
Fuehrungskraft/Admin = erlaubt

Branch:
Testumgebung
```

---

## 40. Schlussbemerkung

Mit dem aktuellen Backend-Stand ist eine stabile technische Grundlage für die weitere Entwicklung geschaffen.

Das Backend kann lokal betrieben, über Swagger getestet und durch das geplante Angular-Frontend angebunden werden.

Die wichtigsten fachlichen Anforderungen des Einsparungsprozesses sind bereits serverseitig umgesetzt:

- strukturierte Erfassung
- automatische Berechnung
- Rollenrechte
- Auditierung
- Statistik
- kontrollierter Export
# Backend-Betriebsdokumentation HimiFlow

Stand: 03.07.2026

## 1. Zweck des Backends

Das Backend stellt die zentrale REST-API fuer HimiFlow bereit. HimiFlow dient der Erfassung, Verwaltung, Auswertung und dem Export von Einsparungsfaellen im Hilfsmittelumfeld.

Das Backend uebernimmt insbesondere:

- Authentifizierung per Benutzername und Passwort
- Ausgabe und Validierung von JWT-Tokens
- rollenbasierte Autorisierung fuer Mitarbeiter, Fuehrungskraft und Admin
- Verwaltung von Einsparungsdatensaetzen
- Verwaltung von Stammdaten wie Teams, Einspargruenden und Produktgruppen
- Benutzerverwaltung fuer Admins
- Statistik-Endpunkte fuer Auswertungen
- CSV- und Excel-Export fuer Fuehrungskraft und Admin
- automatische Datenbankmigration und initiales Seedings beim Start

## 2. Technischer Steckbrief

| Bereich | Wert |
| --- | --- |
| Projekt | `backend/Einsparungs.Api` |
| Solution | `backend/EinsparungsApp.sln` |
| Framework | .NET 8 |
| API-Typ | ASP.NET Core Web API |
| Datenzugriff | Entity Framework Core |
| Datenbank | SQLite |
| Authentifizierung | JWT Bearer Token |
| Passwort-Hashing | BCrypt |
| API-Dokumentation lokal | Swagger im Development-Profil |
| Export | CSV und XLSX |

Wichtige NuGet-Pakete:

- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Swashbuckle.AspNetCore`
- `BCrypt.Net-Next`
- `ClosedXML`

## 3. Projektstruktur

```text
backend/
  EinsparungsApp.sln
  Einsparungs.Api/
    Controllers/
      AuthController.cs
      SavingsController.cs
      MasterDataController.cs
      StatisticsController.cs
      ExportsController.cs
      UserManagementController.cs
    Data/
      AppDbContext.cs
      DatabaseSeeder.cs
    DTOs/
    Migrations/
    Models/
    Security/
      JwtTokenService.cs
    Program.cs
    appsettings.json
    appsettings.Development.json
```

## 4. Lokaler Betrieb

### Voraussetzungen

- .NET SDK 8
- Zugriff auf das Projektverzeichnis
- Freier Port `5281` fuer HTTP oder `7013` fuer HTTPS

### Startbefehl

```powershell
cd "C:\Users\enric\OneDrive\Dokumente\GitHub\First-Procect\backend"
dotnet run --project .\Einsparungs.Api\Einsparungs.Api.csproj --launch-profile http
```

Die API ist danach lokal unter folgender Adresse erreichbar:

```text
http://localhost:5281
```

Im Development-Profil steht Swagger zur Verfuegung:

```text
http://localhost:5281/swagger
```

### Build

```powershell
cd "C:\Users\enric\OneDrive\Dokumente\GitHub\First-Procect\backend"
dotnet build .\EinsparungsApp.sln
```

## 5. Konfiguration

Die zentrale Konfiguration liegt in:

```text
backend/Einsparungs.Api/appsettings.json
```

Aktuelle Werte:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=einsparungen.db"
  },
  "Jwt": {
    "Issuer": "EinsparungsApp",
    "Audience": "EinsparungsApp"
  }
}
```

Wichtig fuer produktionsnahe Umgebungen:

- Der JWT-Key steht nicht im Repository und wird lokal über .NET User Secrets bereitgestellt.
- Der SQLite-Dateiname `einsparungen.db` ist relativ zum Ausfuehrungsverzeichnis.
- Fuer produktive Nutzung sollte ein sicherer Secret-Mechanismus verwendet werden, zum Beispiel Umgebungsvariablen, Secret Store oder ein Deployment-spezifischer Konfigurationsanbieter.

## 6. Startverhalten und Middleware

Die Anwendung wird in `Program.cs` konfiguriert.

Beim Start passiert Folgendes:

1. Controller werden registriert.
2. `AppDbContext` wird mit SQLite eingerichtet.
3. `JwtTokenService` wird registriert.
4. Swagger wird inklusive Bearer-Token-Schema vorbereitet.
5. JWT-Konfiguration wird aus `appsettings.json` gelesen.
6. Authentifizierung und Autorisierung werden aktiviert.
7. CORS erlaubt den Angular-Development-Client unter `http://localhost:4200`.
8. Beim Start wird `DatabaseSeeder.SeedAsync(db)` ausgefuehrt.
9. Im Development-Modus wird Swagger aktiviert.
10. Controller-Routen werden gemappt.

Der Root-Endpunkt:

```http
GET /
```

liefert:

```text
Einsparungs API laeuft.
```

## 7. Datenbank und Migrationen

Das Backend nutzt Entity Framework Core mit SQLite. Der Datenbankkontext liegt in:

```text
backend/Einsparungs.Api/Data/AppDbContext.cs
```

Beim Start wird automatisch ausgefuehrt:

```csharp
await db.Database.MigrateAsync();
```

Dadurch werden vorhandene EF-Core-Migrationen auf die SQLite-Datenbank angewendet.

Aktuell vorhandene Migrationen:

- `20260701173459_InitialCreate`
- `20260702224257_AddUserSoftDelete`

### Neue Migration erzeugen

```powershell
cd "C:\Users\enric\OneDrive\Dokumente\GitHub\First-Procect\backend\Einsparungs.Api"
dotnet ef migrations add NameDerMigration
```

### Datenbank aktualisieren

```powershell
dotnet ef database update
```

Im normalen lokalen Betrieb ist ein explizites `database update` meistens nicht noetig, da die Migrationen beim API-Start angewendet werden.

## 8. Datenmodell

### AppUser

Benutzerkonto fuer die Anmeldung. Wichtige Felder:

- `Id`
- `UserName`
- `DisplayName`
- `PasswordHash`
- `TeamId`
- `IsActive`
- `IsDeleted`
- `DeletedAt`

Benutzer koennen ueber `UserManagementController` soft-geloescht werden. Beim Loeschen wird `IsDeleted` gesetzt und der Benutzername technisch erweitert, damit derselbe Benutzername spaeter erneut verwendet werden kann.

### AppRole

Rollenmodell fuer Berechtigungen.

Aktuelle Rollen:

- `Mitarbeiter`
- `Fuehrungskraft`
- `Admin`

### AppUserRole

Zuordnung zwischen Benutzern und Rollen.

### Team

Stammdatensatz fuer Teams. Wird bei Einsparungsdatensaetzen referenziert.

### SavingReason

Stammdatensatz fuer Einspargruende.

### ProductGroup

Stammdatensatz fuer Produktgruppen. Produktgruppen werden im Einsparungsformular als Dropdown angeboten und koennen inzwischen ueber die Anwendung verwaltet werden.

Wichtige Felder:

- `Id`
- `DisplayValue`
- `IsActive`
- `ImportedAt`
- `ImportedBy`

Loeschen erfolgt als Soft-Delete ueber `IsActive = false`.

### SavingsEntry

Fachlicher Einsparungsdatensatz. Wichtige Felder:

- `Id`
- `Month`
- `Kvnr`
- `OldKvAmount`
- `NewKvAmount`
- `SavingAmount`
- `TeamId`
- `SavingReasonId`
- `ProductGroupId`
- `TransmissionDate`
- `CreatedByUserId`
- `CreatedAt`
- `UpdatedByUserId`
- `UpdatedAt`
- `DeletedByUserId`
- `DeletedAt`
- `IsDeleted`
- `Version`

Die Ersparnis wird im Backend berechnet:

```text
SavingAmount = OldKvAmount - NewKvAmount
```

### AuditLog

Audit-Protokoll fuer Einsparungsdatensaetze.

Bei Erstellen, Bearbeiten und Loeschen von Einsparungen wird ein Audit-Eintrag geschrieben. Gespeichert werden unter anderem:

- Entitaetsname
- Entitaets-ID
- Aktion
- Benutzer
- Zeitpunkt
- alte Werte
- neue Werte
- Client-IP
- User-Agent

## 9. Initiale Seed-Daten

Die Seed-Logik liegt in:

```text
backend/Einsparungs.Api/Data/DatabaseSeeder.cs
```

Beim ersten Start werden angelegt:

- Rollen: `Mitarbeiter`, `Fuehrungskraft`, `Admin`
- Teams: Bochum 1, Bochum 2, Bochum 3, Ruesselsheim, Luebeck
- Einspargruende
- einige initiale Produktgruppen
- Demobenutzer

Demopasswort:

```text
Demo123!
```

Initiale Benutzer:

| Benutzername | Rolle | Hinweis |
| --- | --- | --- |
| `mitarbeiter1` | Mitarbeiter | Team Bochum 1 |
| `mitarbeiter2` | Mitarbeiter | Team Bochum 2 |
| `teamleiter` | Fuehrungskraft | Team Bochum 3 |
| `admin` | Admin | ohne Team |

## 10. Authentifizierung

Die Authentifizierung erfolgt ueber:

```http
POST /api/auth/login
```

Request:

```json
{
  "userName": "admin",
  "password": "Demo123!"
}
```

Bei gueltigen Zugangsdaten liefert das Backend:

- JWT-Token
- Ablaufzeitpunkt
- Benutzer-ID
- Benutzername
- Anzeigename
- Rollenliste

Der Token wird vom Frontend im Header mitgesendet:

```http
Authorization: Bearer <token>
```

### Aktueller Benutzer

```http
GET /api/auth/me
```

Liefert Informationen aus dem aktuellen Token.

## 11. Rollen und Berechtigungen

| Rolle | Rechte |
| --- | --- |
| Mitarbeiter | eigene Einsparungen erfassen, anzeigen, bearbeiten und loeschen |
| Fuehrungskraft | alle Einsparungen sehen, exportieren, Statistiken sehen, Produktgruppen verwalten |
| Admin | alle Fuehrungskraft-Rechte plus Benutzerverwaltung |

Wichtige technische Regeln:

- `SavingsController` ist grundsaetzlich authentifizierungspflichtig.
- `GET /api/savings/all` ist nur fuer `Fuehrungskraft` und `Admin`.
- `ExportsController` ist nur fuer `Fuehrungskraft` und `Admin`.
- `UserManagementController` ist nur fuer `Admin`.
- Produktgruppenverwaltung ist fuer `Fuehrungskraft` und `Admin`.

## 12. API-Endpunkte

### Auth

| Methode | Pfad | Zugriff | Zweck |
| --- | --- | --- | --- |
| POST | `/api/auth/login` | anonym | Anmeldung |
| GET | `/api/auth/me` | Token | aktuellen Benutzer lesen |

### Einsparungen

| Methode | Pfad | Zugriff | Zweck |
| --- | --- | --- | --- |
| GET | `/api/savings/my` | Token | eigene Einsparungen lesen |
| GET | `/api/savings/all` | Fuehrungskraft, Admin | alle Einsparungen lesen |
| GET | `/api/savings/{id}` | Token | einzelne Einsparung lesen |
| POST | `/api/savings` | Token | Einsparung erstellen |
| PUT | `/api/savings/{id}` | Token | Einsparung aktualisieren |
| DELETE | `/api/savings/{id}` | Token | Einsparung soft-loeschen |

Mitarbeiter duerfen nur eigene Datensaetze bearbeiten oder loeschen. Fuehrungskraft und Admin duerfen alle Datensaetze verwalten.

Validierungen:

- Monat muss gesetzt sein.
- KVNR muss aus einem Grossbuchstaben und genau neun Ziffern bestehen.
- `OldKvAmount` und `NewKvAmount` duerfen nicht negativ sein.
- `NewKvAmount` darf nicht groesser als `OldKvAmount` sein.
- Team, Einspargrund und Produktgruppe muessen existieren und aktiv sein.

### Stammdaten

| Methode | Pfad | Zugriff | Zweck |
| --- | --- | --- | --- |
| GET | `/api/master-data/teams` | aktuell anonym erreichbar | aktive Teams |
| GET | `/api/master-data/saving-reasons` | aktuell anonym erreichbar | aktive Einspargruende |
| GET | `/api/master-data/product-groups` | aktuell anonym erreichbar | aktive Produktgruppen fuer Dropdown |
| GET | `/api/master-data/product-groups?search=...` | aktuell anonym erreichbar | aktive Produktgruppen gefiltert |
| GET | `/api/master-data/product-groups/manage` | Fuehrungskraft, Admin | Produktgruppen fuer Verwaltung |
| POST | `/api/master-data/product-groups` | Fuehrungskraft, Admin | Produktgruppe anlegen |
| PUT | `/api/master-data/product-groups/{id}` | Fuehrungskraft, Admin | Produktgruppe bearbeiten |
| DELETE | `/api/master-data/product-groups/{id}` | Fuehrungskraft, Admin | Produktgruppe deaktivieren |

Hinweis: Die allgemeinen Stammdaten-Endpunkte sind aktuell nicht mit `[Authorize]` versehen. Fuer den lokalen Prototyp ist das funktional, fuer produktive Umgebungen sollte geprueft werden, ob auch diese Endpunkte tokenpflichtig sein sollen.

### Statistiken

| Methode | Pfad | Zugriff | Zweck |
| --- | --- | --- | --- |
| GET | `/api/statistics/overview` | Token | Kennzahlenuebersicht |
| GET | `/api/statistics/monthly` | Token | Monatsstatistik |
| GET | `/api/statistics/by-team` | Token | Gruppierung nach Team |
| GET | `/api/statistics/by-saving-reason` | Token | Gruppierung nach Einspargrund |
| GET | `/api/statistics/by-product-group` | Token | Gruppierung nach Produktgruppe |

Die Statistik beruecksichtigt nur nicht geloeschte Einsparungen.

### Exporte

| Methode | Pfad | Zugriff | Zweck |
| --- | --- | --- | --- |
| GET | `/api/exports/savings.csv` | Fuehrungskraft, Admin | CSV-Export |
| GET | `/api/exports/savings.xlsx` | Fuehrungskraft, Admin | Excel-Export |

Exportdateien enthalten:

- Id
- Monat
- KVNR
- Alter KV
- Neuer KV
- Ersparnis
- Team
- Einspargrund
- Produktgruppe
- Uebermittlungsdatum
- Erstellt von
- Erstellt am
- Version

### Benutzerverwaltung

| Methode | Pfad | Zugriff | Zweck |
| --- | --- | --- | --- |
| GET | `/api/user-management` | Admin | Benutzerliste |
| POST | `/api/user-management` | Admin | Benutzer anlegen |
| POST | `/api/user-management/{id}/reset-password` | Admin | Passwort auf `Demo123!` zuruecksetzen |
| DELETE | `/api/user-management/{id}` | Admin | Benutzer soft-loeschen |

Schutzregeln:

- Der aktuell angemeldete Admin kann sich nicht selbst loeschen.
- Der letzte aktive Admin darf nicht geloescht werden.
- Benutzername muss eindeutig sein.
- Passwort muss mindestens sechs Zeichen lang sein.
- Rolle muss existieren.

## 13. Produktgruppenverwaltung

Die Produktgruppenverwaltung wurde als neues Feature ergaenzt.

Fachliches Verhalten:

- Fuehrungskraft und Admin koennen Produktgruppen anlegen.
- Bestehende Produktgruppen koennen umbenannt werden.
- Produktgruppen koennen geloescht werden.
- Geloeschte Produktgruppen werden technisch deaktiviert und nicht hart aus der Datenbank entfernt.
- Das Einsparungsformular bezieht die Dropdown-Werte aus der aktiven Produktgruppenliste.

Technische Details:

- Tabelle: `ProductGroups`
- Feld fuer Anzeige: `DisplayValue`
- Soft-Delete: `IsActive = false`
- Maximale Laenge: 500 Zeichen
- Doppelte aktive Produktgruppen werden verhindert.

## 14. Logging und Fehlerbehandlung

Das Standardlogging wird ueber ASP.NET Core konfiguriert.

Aktuelle Log-Level:

```json
{
  "Default": "Information",
  "Microsoft.AspNetCore": "Warning"
}
```

Validierungsfehler werden meistens als `400 Bad Request` mit folgender Struktur zurueckgegeben:

```json
{
  "errors": [
    "Fehlermeldung"
  ]
}
```

Weitere wichtige Statuscodes:

| Status | Bedeutung |
| --- | --- |
| 200 | Erfolgreich |
| 201 | Ressource erstellt |
| 204 | Erfolgreich ohne Rueckgabe |
| 400 | Validierungsfehler |
| 401 | nicht authentifiziert |
| 403 | nicht berechtigt |
| 404 | Ressource nicht gefunden |

## 15. Betrieb und Wartung

### Health Check manuell

```powershell
Invoke-WebRequest -Uri "http://localhost:5281/" -UseBasicParsing
```

Erwartete Antwort:

```text
Einsparungs API laeuft.
```

### Swagger pruefen

```text
http://localhost:5281/swagger
```

### Datenbankdatei sichern

Bei SQLite ist die Datenbank eine Datei. Der aktuelle Connection String verwendet:

```text
einsparungen.db
```

Die Datei sollte fuer Sicherungen kopiert werden, wenn die API nicht gerade aktiv schreibt.

### Typische Fehlerbilder

| Fehlerbild | Ursache | Massnahme |
| --- | --- | --- |
| Frontend bekommt CORS-Fehler | Frontend laeuft nicht auf `http://localhost:4200` | CORS-Policy in `Program.cs` erweitern oder richtigen Port nutzen |
| 401 bei API-Aufruf | Token fehlt oder ist abgelaufen | neu anmelden |
| 403 bei Export oder Benutzerverwaltung | Rolle reicht nicht aus | Rolle pruefen |
| Datenbankfehler beim Start | Migration oder SQLite-Datei problematisch | Datenbankdatei, Migrationen und Schreibrechte pruefen |
| Swagger nicht sichtbar | Umgebung nicht `Development` | `ASPNETCORE_ENVIRONMENT=Development` setzen |

## 16. Sicherheits- und Produktivhinweise

Vor produktionsnaher Nutzung sollten mindestens folgende Punkte geprueft werden:

- JWT-Key aus `appsettings.json` entfernen und sicher konfigurieren.
- HTTPS erzwingen.
- CORS restriktiv auf die echte Frontend-Domain setzen.
- Demo-Passwort und Demo-Benutzer entfernen oder ersetzen.
- Passwort-Reset nicht dauerhaft auf ein bekanntes Standardpasswort setzen.
- Stammdaten-Endpunkte ggf. ebenfalls mit `[Authorize]` absichern.
- Backup-Strategie fuer SQLite oder Wechsel auf einen zentralen Datenbankserver definieren.
- Logging und Monitoring fuer Fehlerfaelle ergaenzen.

## 17. Relevante Pruefbefehle

Backend bauen:

```powershell
dotnet build "C:\Users\enric\OneDrive\Dokumente\GitHub\First-Procect\backend\EinsparungsApp.sln"
```

Backend starten:

```powershell
dotnet run --project "C:\Users\enric\OneDrive\Dokumente\GitHub\First-Procect\backend\Einsparungs.Api\Einsparungs.Api.csproj" --launch-profile http
```

Git-Status pruefen:

```powershell
git status
```
