# HimiFlow – Phase-F-Reifegrad- und Abschlussbericht

> Historischer Abschlussstand Reifegrad 3. Der aktuelle, maßgebliche Stand ist der [Reifegrad-4-Abschlussbericht](reifegrad-4-abschlussbericht.md).

Stand: 28.08.2026  
Bewertung: lokaler SQLite-Betrieb auf dem Entwicklerrechner

## 1. Executive Summary

HimiFlow ist nach Abschluss der Phasen A bis F ein funktionsfähiger, technisch sauber strukturierter und kontrolliert testbarer MVP-/Pilotprototyp für die lokale Erfassung und Auswertung von Einsparungen.

Der aktuelle Reifestatus lautet:

> **Reifegrad 3 von 5 – pilotfähig im kontrollierten lokalen Betrieb**

Damit eignet sich HimiFlow für:

- fachliche Demos und Produktpräsentationen
- einen begrenzten lokalen Pilotbetrieb
- die Validierung des Arbeitsablaufs mit ausgewählten Benutzern
- die Vorbereitung eines Kundenangebots mit klar abgegrenztem Leistungsumfang

HimiFlow ist noch **keine allgemein produktionsfertige Krankenkassen- oder Enterprise-Lösung**. Dafür fehlen insbesondere eine rechtlich freigegebene Datenschutz-/Aufbewahrungsorganisation, Verschlüsselung der lokalen Datenbank und Backups at rest, ein abgesicherter HTTPS-Betrieb mit Secret-Management sowie ein formal geplanter Restore- und Notfalltest.

Active Directory/SSO, SIEM und SQL-Cluster sind ausdrücklich **nicht Bestandteil des aktuellen Produkts**. Sie bleiben optionale spätere Erweiterungen und sind keine Voraussetzung für die lokale Edition.

## 2. Was mit Phase F abgeschlossen wurde

Phase F ist die Abschluss- und Übergabephase für die lokale Edition. Sie umfasst:

- überprüften Start von Backend und Frontend
- reproduzierbare Release-/Publish-Schritte
- getrennte Migration- und Seed-Befehle für kontrollierte Installationen
- Liveness- und Readiness-Health-Checks
- lokale SQLite-Backups mit SystemAdmin-Berechtigung
- rollenbasierte lokale Benutzerverwaltung
- HttpOnly-Cookie-Authentifizierung und CSRF-Schutz
- Offline-Jahreslizenz mit Grace-Period und Read-only-Verhalten
- fachliche Validierung und Soft Delete für Einsparungen
- globale Statistiken bei eingeschränktem Datenzugriff für Mitarbeiter
- KVNR-Maskierung in CSV-/Excel-Exporten
- technische Audit-Metadaten für SystemAdmins ohne Snapshot-Werte
- Sicherheitsheader, Rate-Limit für Authentifizierung und zentrale Fehlerantworten
- automatisierte Tests, Frontend-Build und Dependency-Audit
- aktualisierte Betriebs- und Deployment-Dokumentation

## 3. Reifegrad nach Bereich

| Bereich | Status | Einordnung |
| --- | --- | --- |
| Fachliche Kernfunktion | Grün | Einsparungen, Stammdaten, Berechnung, Statistiken und Exporte funktionieren lokal. |
| Rollen und Berechtigungen | Grün | Mitarbeiter, FachAdmin und SystemAdmin sind getrennt umgesetzt. |
| Authentifizierung | Grün | ASP.NET Core Identity, HttpOnly-Cookie, CSRF, Lockout und Passwortwechsel sind vorhanden. |
| Lizenzierung | Grün | Offline-Jahreslizenz, 30-Tage-Grace-Period und Read-only-Modus sind vorhanden. |
| Datenschutz-Basisschutz | Gelb/Grün | Exporte maskieren KVNRs und werden nicht gecacht; die SQLite-Datei selbst ist noch nicht at-rest verschlüsselt. |
| Auditierbarkeit | Gelb/Grün | Schreibvorgänge und Exporte werden protokolliert; eine rechtlich freigegebene Aufbewahrungsfrist fehlt noch. |
| Betrieb und Deployment | Gelb/Grün | Publish-, Migrations-, Seed- und Health-Schritte sind dokumentiert und getestet. |
| Backup und Wiederherstellung | Gelb | Backup-Erstellung ist vorhanden; ein formal abgenommener Restore- und Notfalllauf steht noch aus. |
| Testbarkeit | Grün | 21 Backend-Tests, 2 Frontend-Tests, Builds und HTTP-Smoke-Checks bestanden. |
| Enterprise-Integration | Bewusst offen | SQL-Server/Cluster, Active Directory/SSO und SIEM sind spätere optionale Erweiterungen. |

## 4. Nachweis der technischen Abnahme

Am 28.08.2026 wurden lokal erfolgreich ausgeführt:

```text
dotnet build backend/EinsparungsApp.sln -c Release --no-restore
  0 Warnungen, 0 Fehler

dotnet test backend/EinsparungsApp.sln -c Release --no-build
  21 Tests bestanden

npm run build
  Angular-Production-Build erfolgreich

npm test -- --watch=false
  2 Tests bestanden

npm audit --audit-level=high
  0 Schwachstellen
```

Die laufenden lokalen Smoke-Checks lieferten:

| Prüfung | Ergebnis |
| --- | --- |
| Frontend `http://localhost:4200/` | HTTP 200 |
| API `/api/health/live` | HTTP 200 |
| API `/api/health/ready` | HTTP 200 |
| API `/api/auth/csrf` | HTTP 204 |
| `/api/admin/audit` ohne Anmeldung | HTTP 401, erwartet |
| `/api/exports/savings.csv` ohne Anmeldung | HTTP 401, erwartet |
| Sicherheitsheader | `nosniff`, `DENY`, `no-referrer` vorhanden |

## 5. Produktstatus für ein Verkaufsgespräch

### Jetzt vertretbar

HimiFlow kann als **lokale Pilot-/MVP-Edition** angeboten werden. Im Angebot sollten die folgenden Punkte ausdrücklich genannt werden:

- Betrieb auf einem lokalen Windows-Rechner oder einer einzelnen kontrollierten VM
- SQLite als lokale Datenbank
- lokale Benutzer- und Rollenverwaltung
- Fach-Admin-Export mit standardmäßig maskierter KVNR
- lokale Lizenzverwaltung mit Jahreslizenz und Grace-Period
- lokaler Backup-Prozess
- definierter Funktionsumfang der Einsparungserfassung

### Noch nicht als pauschales Produktionsversprechen formulieren

Nicht behaupten sollten Angebot oder Marketing derzeit:

- „vollständig DSGVO-zertifiziert“
- „revisionssicher im rechtlichen Sinn“ ohne Prozess- und Organisationsfreigabe
- „hochverfügbar“ oder „clusterfähig“ in der lokalen SQLite-Edition
- „verschlüsselte Datenbank at rest“
- „integriert in Active Directory, SSO oder SIEM“

Das sind keine Mängel des lokalen MVPs, sondern klare Produktgrenzen. Sie sollten im Angebot als Abgrenzung und als optionale spätere Ausbaustufen geführt werden.

## 6. Priorisierte Restpunkte vor einem echten Kunden-Produktivbetrieb

Diese Punkte sind wichtiger als große Enterprise-Integrationen:

1. Datenschutz- und Aufbewahrungskonzept fachlich/rechtlich freigeben lassen.
2. Sicheren Installationsweg für Secrets und HTTPS-Zertifikate definieren.
3. SQLite-Datei und Backups gegen unbefugtes Lesen schützen, mindestens über Betriebssystemrechte und verschlüsselte Backup-Ziele.
4. Einen Restore-Test mit dokumentierter Wiederanlaufzeit durchführen.
5. Verantwortliche für Benutzer, Lizenz, Backup und Incident-Meldung benennen.
6. Einen kleinen Pilot mit echten Rollen und anonymisierten Testdaten abnehmen.
7. Erst danach entscheiden, ob für einen konkreten Kunden SQL Server, SSO oder zentrale Protokollierung wirtschaftlich erforderlich sind.

## 7. Bewusste Nicht-Ziele der Phase F

Nicht umgesetzt und aktuell auch nicht erforderlich sind:

- Active Directory oder Entra ID
- SSO und externe Identity Provider
- SIEM-Anbindung
- SQL-Server-/Cluster-Migration
- Multi-Node-High-Availability
- Cloudbetrieb
- automatische Audit-Löschung ohne Aufbewahrungsfreigabe

Diese Punkte würden den aktuellen lokalen Produktumfang deutlich vergrößern, ohne den Pilotnutzen proportional zu erhöhen.

## 8. Schlussbewertung

HimiFlow ist technisch deutlich über einem einfachen Demo-Prototyp: Die Anwendung besitzt Kernfunktion, Rollenmodell, Authentifizierung, Lizenzlogik, Betriebswerkzeuge, Datenschutz-Basisschutz, Auditierung und automatisierte Prüfungen.

Für einen kontrollierten lokalen Piloten ist der Stand ausreichend. Für einen uneingeschränkten produktiven Einsatz bei einer Krankenkasse fehlen noch organisatorische und infrastrukturelle Nachweise, vor allem Datenschutzfreigabe, Daten-/Backup-Schutz und Restore-Abnahme.

**Phase F kann damit als abgeschlossen gelten.** Der nächste sinnvolle Schritt ist nicht mehr eine weitere große Technikphase, sondern die gemeinsame Erstellung der konkreten Betriebs-, Verkaufs- und Verantwortlichkeitsdokumentation für dich als Anbieter und Betreiber.
