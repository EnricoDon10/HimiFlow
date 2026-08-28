# Frontend-Betriebsdokumentation HimiFlow

Stand: 28.08.2026

## 1. Zweck des Frontends

Das Frontend ist die browserbasierte Bedienoberflaeche von HimiFlow. Es stellt die fachlichen Funktionen der Einsparungsdatenbank fuer die Rollen Mitarbeiter, FachAdmin (Führungskraft) und SystemAdmin (IT-Administration) bereit.

> **Maßgeblicher Stand Reifegrad 4 (28.08.2026):** Das Frontend verwendet keine JWTs und keinen `localStorage`. Die Anmeldung erfolgt über die HttpOnly-Cookie des Backends; Angular sendet API-Aufrufe mit `withCredentials` und dem CSRF-Header `X-XSRF-TOKEN`. Die Rollen heißen `Mitarbeiter`, `FachAdmin` und `SystemAdmin`. Ergänzt sind Lizenzstatus/-verwaltung, Read-only-Hinweise, responsive Layout-Politur, zugängliche Passwortfelder mit Gedrückthalten-Anzeige und die öffentliche Produkt-/Anbieterseite `/legal`. Fach-Admin-Exporte werden backendseitig mit maskierter KVNR und `no-store` ausgeliefert. Maßgeblich ist der [Reifegrad-4-Abschlussbericht](reifegrad-4-abschlussbericht.md).

Das Frontend ermoeglicht:

- Anmeldung per Benutzername und Passwort
- rollenbasierte Navigation
- Erfassung neuer Einsparungsfaelle
- Anzeige eigener Einsparungen
- Anzeige aller Einsparungen fuer Fach-Admins (Führungskräfte)
- Bearbeiten und Loeschen von Einsparungen
- Statistikansichten
- CSV- und Excel-Export fuer berechtigte Rollen
- Benutzerverwaltung fuer System-Admins
- Produktgruppenverwaltung fuer Fach-Admins

## 2. Technischer Steckbrief

| Bereich | Wert |
| --- | --- |
| Projekt | `frontend` |
| Framework | Angular 22 |
| Sprache | TypeScript |
| Styling | SCSS |
| Formulare | Angular Forms / `FormsModule` |
| HTTP | Angular `HttpClient` |
| Routing | Angular Router |
| Authentifizierung | HttpOnly-Cookie + CSRF-Header |
| Package Manager | npm |
| Test-Setup | Angular Unit Test Builder mit Vitest-Typen |

Wichtige Abhaengigkeiten:

- `@angular/core`
- `@angular/router`
- `@angular/forms`
- `@angular/common`
- `rxjs`
- `zone.js`

## 3. Projektstruktur

```text
frontend/
  angular.json
  package.json
  tsconfig.json
  tsconfig.app.json
  tsconfig.spec.json
  public/
  src/
    main.ts
    styles.scss
    app/
      app.config.ts
      app.routes.ts
      core/
        config/
        guards/
        interceptors/
        models/
        services/
      features/
        admin/
          license/
          product-groups/
          user-management/
        dashboard/
        login/
        savings/
          all-savings/
          my-savings/
          savings-create/
        statistics/
      layout/
        app-layout/
```

## 4. Lokaler Betrieb

### Voraussetzungen

- Node.js passend zur Angular-Version
- npm
- installierte Pakete aus `package-lock.json`
- laufendes Backend unter `http://localhost:5281`

### Abhaengigkeiten installieren

```powershell
cd "C:\Users\enric\dev\GitHub\HimiFlow\frontend"
npm install
```

### Development-Server starten

```powershell
cd "C:\Users\enric\dev\GitHub\HimiFlow\frontend"
npm run start
```

Danach ist das Frontend erreichbar unter:

```text
http://localhost:4200
```

### Lizenzstatus und Passwortfelder

System-Admins finden unter **Lizenzverwaltung** den lokalen Lizenzstatus und können einen vom Anbieter signierten Jahreslizenzschlüssel installieren. Bei Grace-Period, Ablauf oder ungültiger Lizenz zeigt das Layout einen Hinweis; nach Ablauf bleiben Lesefunktionen verfügbar, Schreibaktionen werden deaktiviert und zusätzlich vom Backend abgewiesen.

In Login und Passwortänderung kann das Passwort durch Gedrückthalten des Augen-Symbols kurz eingeblendet werden. Das funktioniert mit Maus/Touch und mit der Leertaste; Enter schaltet die Anzeige für Tastaturbedienung um.

### Production-Build erstellen

```powershell
npm run build
```

Der Build wird geschrieben nach:

```text
frontend/dist/frontend
```

## 5. npm-Skripte

| Befehl | Zweck |
| --- | --- |
| `npm run start` | startet Angular Development Server |
| `npm run build` | erstellt Production-Build |
| `npm run watch` | baut im Watch-Modus mit Development-Konfiguration |
| `npm run test` | startet Unit Tests |

## 6. Angular-Konfiguration

Die zentrale Angular-Konfiguration liegt in:

```text
frontend/angular.json
```

Wichtige Punkte:

- Anwendungstyp: `application`
- Einstiegspunkt: `src/main.ts`
- Styles: `src/styles.scss`
- Assets: `public`
- Standard-Build-Konfiguration: `production`
- Development-Serve-Konfiguration: `development`

Production-Budgets:

- initiales Bundle: Warnung ab 500 kB, Fehler ab 1 MB
- Component Style: Warnung ab 4 kB, Fehler ab 8 kB

## 7. API-Konfiguration

Die Backend-Basisadresse ist zentral definiert in:

```text
frontend/src/app/core/config/api.config.ts
```

Aktueller Wert:

```ts
export const API_CONFIG = {
  baseUrl: ''
};
```

Das Frontend verwendet damit relative `/api`-Aufrufe. Im lokalen Entwicklungsbetrieb leitet `proxy.conf.json` diese Aufrufe an `http://localhost:5281` weiter. Nach Änderungen an der Proxy-Konfiguration muss `npm run start` neu gestartet werden. Für spätere Deployments können Frontend und API unter derselben Origin betrieben werden, ohne eine Backend-Adresse in den Browser-Build einzubetten.

## 8. App-Initialisierung

Die App-Konfiguration liegt in:

```text
frontend/src/app/app.config.ts
```

Registrierte Provider:

- globale Browser-Fehlerlistener
- Zone Change Detection mit Event Coalescing
- Angular Router mit `app.routes.ts`
- `HttpClient` mit `authInterceptor`

Der `authInterceptor` sorgt dafür, dass API-Aufrufe mit der Authentifizierungscookie an das Backend gesendet werden. Angular liest den XSRF-Request-Token aus dem `XSRF-TOKEN`-Cookie und setzt den Header automatisch.

## 9. Routing und Navigation

Die Routen sind definiert in:

```text
frontend/src/app/app.routes.ts
```

| Route | Komponente | Zugriff |
| --- | --- | --- |
| `/login` | `LoginComponent` | nur Gaeste |
| `/dashboard` | `DashboardComponent` | angemeldet |
| `/savings/new` | `SavingsCreateComponent` | angemeldet |
| `/savings/my` | `MySavingsComponent` | angemeldet |
| `/savings/all` | `AllSavingsComponent` | Fuehrungskraft, Admin |
| `/statistics` | `StatisticsComponent` | angemeldet |
| `/admin/product-groups` | `ProductGroupsComponent` | Fuehrungskraft, Admin |
| `/admin/users` | `UserManagementComponent` | Admin |

Unbekannte Routen werden auf `/login` umgeleitet.

Die Hauptnavigation liegt in:

```text
frontend/src/app/layout/app-layout/app-layout.component.html
```

Die Anzeige einzelner Menuepunkte wird rollenbasiert gesteuert.

## 10. Authentifizierung im Frontend

Die Authentifizierungslogik liegt in:

```text
frontend/src/app/core/services/auth.service.ts
```

Nach erfolgreichem Login speichert das Frontend keine Zugangsdaten im Browser. Die Authentifizierung übernimmt die HttpOnly-Cookie `HimiFlow.Auth` des Backends. Der aktuelle Benutzer wird nur im Laufzeitzustand der Angular-Anwendung gehalten und bei `/api/auth/me` erneut geladen.

### Login-Ablauf

1. Benutzer oeffnet `/login`.
2. Loginformular sendet Benutzername und Passwort an `/api/auth/login`.
3. Backend setzt die HttpOnly-Cookie und liefert Benutzer- und Rolleninformationen.
4. Bei einem neuen Browser-Tab lädt das Frontend den Benutzer über `/api/auth/me`.
5. Benutzer wird in den geschuetzten Bereich weitergeleitet.

### Token-Verwendung

Der Interceptor in:

```text
frontend/src/app/core/interceptors/auth.interceptor.ts
```

setzt für API-Aufrufe `withCredentials: true` und überlässt dem Browser die Cookie-Übertragung. Für schreibende Aufrufe wird der Angular-XSRF-Header gesetzt:

```http
X-XSRF-TOKEN: <request-token>
```

## 11. Guards und Rollensteuerung

Die Zugriffskontrolle im Frontend erfolgt ueber Guards:

| Guard | Datei | Zweck |
| --- | --- | --- |
| `authGuard` | `core/guards/auth.guard.ts` | schuetzt angemeldete Bereiche |
| `guestGuard` | `core/guards/guest.guard.ts` | verhindert Loginseite fuer bereits angemeldete Benutzer |
| `roleGuard` | `core/guards/role.guard.ts` | prueft erlaubte Rollen aus Routendaten |

Wichtig: Die Frontend-Guards verbessern Bedienung und Navigation, ersetzen aber keine Backend-Autorisierung. Die verbindliche Rechtepruefung erfolgt im Backend.

## 12. Rollen und sichtbare Funktionen

| Rolle | Sichtbare Funktionen im Frontend |
| --- | --- |
| Mitarbeiter | Dashboard, Einsparung erfassen, Meine Einsparungen, globale Statistik |
| FachAdmin | Mitarbeiter-Funktionen plus Alle Einsparungen, Export, Produktgruppen verwalten |
| SystemAdmin | Benutzerverwaltung und Systemzugriff; keine fachlichen Einsparungsdaten |

Die Rollen werden aus der LoginResponse und dem aktuellen Laufzeitzustand der Anwendung gelesen. Die verbindliche Prüfung erfolgt serverseitig.

## 13. Fachliche Seiten

### Login

Pfad:

```text
/login
```

Zweck:

- Anmeldung an der Anwendung
- Speichern von Token und Benutzerinformationen
- Weiterleitung in den geschuetzten Bereich

### Dashboard

Pfad:

```text
/dashboard
```

Zweck:

- Startseite nach Login
- Uebersicht fuer den angemeldeten Benutzer

### Einsparung erfassen

Pfad:

```text
/savings/new
```

Komponente:

```text
features/savings/savings-create
```

Felder:

- Monat
- KVNR
- Alter KV
- Neuer KV
- Team
- Einspargrund
- Produktgruppe

Frontend-Verhalten:

- KVNR-Eingabe wird auf einen Grossbuchstaben plus neun Ziffern begrenzt.
- Ersparnis wird waehrend der Eingabe berechnet.
- Produktgruppen werden aus der aktiven Stammdatenliste geladen.
- Produktgruppen koennen ueber ein Suchfeld gefiltert werden.

Backend bleibt die massgebliche Validierungsinstanz.

### Meine Einsparungen

Pfad:

```text
/savings/my
```

Zweck:

- Anzeige der eigenen Einsparungsdatensaetze
- Bearbeiten eigener Datensaetze
- Loeschen eigener Datensaetze

### Alle Einsparungen

Pfad:

```text
/savings/all
```

Zugriff:

- Fuehrungskraft
- Admin

Zweck:

- Anzeige aller Einsparungen
- Bearbeiten und Loeschen berechtigter Datensaetze
- Exportfunktionen, sofern in der Komponente angeboten

### Statistik

Pfad:

```text
/statistics
```

Zweck:

- Uebersicht ueber Einsparungen
- Monatsstatistik
- Gruppierungen nach Team, Einspargrund und Produktgruppe

Genutzte API-Endpunkte:

- `/api/statistics/overview`
- `/api/statistics/monthly`
- `/api/statistics/by-team`
- `/api/statistics/by-saving-reason`
- `/api/statistics/by-product-group`

### Produktgruppen verwalten

Pfad:

```text
/admin/product-groups
```

Zugriff:

- Fuehrungskraft
- Admin

Zweck:

- Produktgruppe erfassen
- bestehende Produktgruppe bearbeiten
- Produktgruppe loeschen
- aktive Produktgruppen tabellarisch anzeigen

Wichtiges fachliches Verhalten:

- Diese Produktgruppen sind die Quelle fuer das Dropdown in der Einsparungserfassung.
- Loeschen entfernt Produktgruppen aus der aktiven Liste, nicht zwingend physisch aus der Datenbank.
- Doppelte Produktgruppen werden vom Backend verhindert.

### Benutzerverwaltung

Pfad:

```text
/admin/users
```

Zugriff:

- Admin

Zweck:

- Benutzer anlegen
- Rolle zuweisen
- Team zuweisen
- temporäres Einmalpasswort erzeugen und nach der ersten Anmeldung ändern
- Benutzer loeschen

## 14. Services und API-Kommunikation

Die API-Kommunikation ist in `core/services` gekapselt.

| Service | Zweck |
| --- | --- |
| `AuthService` | Login, Logout, aktueller Benutzer, Rollenpruefung |
| `SavingsService` | Einsparungen erstellen, lesen, bearbeiten, loeschen |
| `MasterDataService` | Teams, Einspargruende, Produktgruppen, Produktgruppenverwaltung |
| `StatisticsService` | Statistikdaten laden |
| `ExportsService` | CSV- und Excel-Downloads |
| `UserManagementService` | Admin-Benutzerverwaltung |

## 15. Datenmodelle

Die TypeScript-Modelle liegen in:

```text
frontend/src/app/core/models
```

Wichtige Modelle:

- `LoginRequest`
- `LoginResponse`
- `CurrentUser`
- `SavingsEntryCreateRequest`
- `SavingsEntryUpdateRequest`
- `SavingsEntryResponse`
- `Team`
- `SavingReason`
- `ProductGroup`
- `ProductGroupSaveRequest`
- `StatisticsOverview`
- `GroupedStatisticsItem`
- `MonthlyStatisticsItem`
- `UserManagementUser`
- `CreateUserRequest`
- `ResetPasswordResponse`

Die Modelle spiegeln die API-Vertraege des Backends wider. Bei API-Aenderungen sollten die passenden TypeScript-Modelle direkt mit angepasst werden.

## 16. Build- und TypeScript-Konfiguration

Relevante Dateien:

```text
tsconfig.json
tsconfig.app.json
tsconfig.spec.json
```

Wichtiger aktueller Punkt:

In `tsconfig.spec.json` ist `rootDir` explizit gesetzt:

```json
"rootDir": "./src"
```

Das ist fuer die aktuelle TypeScript-/Angular-Konfiguration wichtig, damit VS Code und TypeScript die Test-Konfiguration korrekt interpretieren.

## 17. Betrieb mit Backend

Fuer den lokalen Gesamtbetrieb muessen Backend und Frontend parallel laufen.

Backend:

```powershell
cd "C:\Users\enric\dev\GitHub\HimiFlow\backend"
dotnet run --project .\Einsparungs.Api\Einsparungs.Api.csproj --launch-profile http
```

Frontend:

```powershell
cd "C:\Users\enric\dev\GitHub\HimiFlow\frontend"
npm run start
```

Erwartete URLs:

| Anwendung | URL |
| --- | --- |
| Frontend | `http://localhost:4200` |
| Backend | `http://localhost:5281` |
| Swagger | `http://localhost:5281/swagger` |

## 18. Typische Fehlerbilder

| Fehlerbild | Ursache | Massnahme |
| --- | --- | --- |
| Login schlaegt fehl | Backend laeuft nicht oder Zugangsdaten falsch | Backend starten und Benutzerkonto pruefen |
| API-Aufruf liefert 401 | Cookie fehlt/abgelaufen oder Benutzer deaktiviert | `/login` neu laden und anmelden |
| API-Aufruf liefert 403 | Rolle reicht nicht aus | Benutzerrolle pruefen |
| CORS-Fehler im Browser | Backend erlaubt Frontend-Origin nicht | Backend-CORS in `Program.cs` pruefen |
| Produktgruppen fehlen im Dropdown | keine aktiven Produktgruppen oder Backend nicht erreichbar | Produktgruppenverwaltung und API pruefen |
| VS Code meldet `rootDir` bei `tsconfig.spec.json` | `rootDir` fehlt in Spec-Konfiguration | `rootDir: "./src"` setzen |
| Build bricht wegen Bundle-Budget ab | Bundle zu gross | Abhaengigkeiten und Lazy Loading pruefen |

## 19. Wartung und Weiterentwicklung

Bei neuen Features sollte folgendes Schema eingehalten werden:

1. API-Vertrag im Backend klaeren.
2. TypeScript-Modell in `core/models` ergaenzen.
3. HTTP-Zugriff in `core/services` kapseln.
4. Route in `app.routes.ts` ergaenzen.
5. Rollen in Route und Navigation pruefen.
6. Komponente unter `features` erstellen.
7. Build ausfuehren.
8. Betriebsdokumentation aktualisieren, wenn sich Betrieb, Rechte, Routen oder API-Vertraege aendern.

## 20. Pruefbefehle

Frontend bauen:

```powershell
cd "C:\Users\enric\dev\GitHub\HimiFlow\frontend"
npm run build
```

Frontend starten:

```powershell
npm run start
```

Git-Status pruefen:

```powershell
git status
```
