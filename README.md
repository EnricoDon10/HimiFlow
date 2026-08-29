# Einsparungsdatenbank

> **Reifegrad 4/5:** vorproduktions- und verkaufsvorbereitungsreife Local Edition mit SQLite, Cookie-/CSRF-Authentifizierung, lokaler Rollenverwaltung, Offline-Jahreslizenz mit 30-Tage-Grace-Period, HTTPS-Härtung, vereinheitlichter Fehlerbehandlung, täglichen integritätsgeprüften Backups, SBOM und Betriebs-/Datenschutzdokumentation. SQL Server, Kunden-PKI und Ziel-VM folgen ausschließlich in der Phase Inbetriebnahme.
---

> **Phase D:** Der lokale SQLite-Betrieb bleibt erhalten. Für eine kontrollierte Veröffentlichung sind getrennte `--migrate`/`--seed`-Schritte, Liveness-/Readiness-Endpunkte und Skripte unter [`deploy/`](deploy/) ergänzt. Die spätere SQL-Server-/Cluster-Migration wird erst in einer eigenen provider-spezifischen Phase durchgeführt.

> **Phase E:** KVNRs werden in CSV-/Excel-Exporten standardmäßig maskiert. Administrative Audit-Einträge können systemseitig paginiert abgefragt werden, ohne fachliche Snapshot-Werte offenzulegen. Exportantworten werden nicht im Browser-Cache gespeichert.

> **Phase F:** Die lokale Edition ist technisch abgenommen und als kontrollierter MVP-/Pilotbetrieb bewertet. Der vollständige Reifegrad und die bewussten Grenzen stehen im [Phase-F-Reifegrad- und Abschlussbericht](docs/phase-f-reifegrad-abschlussbericht.md).

> **Reifegrad 4:** Der maßgebliche aktuelle Stand ist im [Production-Gap-Abschlussbericht](docs/production-gap-abschlussbericht.md) dokumentiert. Maßgebliche Detailunterlagen: [Backend-Betrieb](docs/backend-betriebsdokumentation.md), [Login/Passwort](docs/login-und-passwortkonzept.md), [Backup/Restore](docs/backup-und-restore-konzept.md), [Datenschutz/Berechtigungen](docs/datenschutz-und-berechtigungskonzept.md) und [Recht/Vertrag](docs/rechtliche-und-vertragliche-checkliste.md).

## Lokaler Start unter Windows

Das Repository verwendet **.NET SDK 10.0.400** gemäß `global.json`. Ist dieses SDK noch nicht systemweit installiert, kann der vorhandene lokale Codex-SDK-Pfad direkt verwendet werden:

```powershell
cd C:\Users\enric\dev\GitHub\HimiFlow\backend\Einsparungs.Api
& "$env:USERPROFILE\.codex\tools\dotnet10\dotnet.exe" run --launch-profile http
```

Für den normalen dauerhaften Betrieb installierst du das .NET-10-SDK 10.0.400 systemweit. Danach funktioniert im selben Ordner wieder:

```powershell
dotnet run --launch-profile http
```

Das Frontend wird in einem zweiten PowerShell-Fenster gestartet:

```powershell
cd C:\Users\enric\dev\GitHub\HimiFlow\frontend
npm run start -- --port 4200
```

Anschließend ist die Anwendung unter `http://localhost:4200` erreichbar; die API läuft lokal auf `http://localhost:5281`.

## Projektbeschreibung

Die **Einsparungsdatenbank** ist ein Digitalisierungsprojekt zur strukturierten Erfassung, Verwaltung, Auswertung und Nachvollziehbarkeit von Einsparungen innerhalb eines fachlichen Krankenkassenprozesses. Ziel des Projekts ist es, einen bisher teilweise manuellen, dezentralen und historisch gewachsenen Prozess in eine moderne, zentrale und rollenbasierte Webanwendung zu überführen.

Im bisherigen Arbeitsablauf werden Einsparungsdaten über verschiedene Werkzeuge und Datenquellen gepflegt. Dazu gehören unter anderem Confluence-Formulare, Access-Datenbanken, Excel-Dateien oder manuell gepflegte Auswertungen. Diese Arbeitsweise führt langfristig zu mehreren fachlichen und technischen Herausforderungen: Daten liegen verteilt vor, Auswertungen sind nicht immer einheitlich, Berechtigungen lassen sich nur eingeschränkt sauber abbilden, Änderungen sind schwer nachvollziehbar und die Pflege hängt stark von manuellen Arbeitsschritten ab.

Die neue Anwendung soll diese Abhängigkeiten schrittweise ablösen und einen stabilen digitalen Prozess schaffen. Dabei steht nicht nur die reine Datenerfassung im Vordergrund, sondern auch die fachliche Qualität der Daten, die Nachvollziehbarkeit von Änderungen, eine saubere Rollensteuerung sowie die Möglichkeit, Auswertungen und Exporte zentral bereitzustellen.

---

## Ausgangssituation

Der bestehende Prozess basiert auf einer Erfassung von Einsparungen im Zusammenhang mit Krankenkassen-Vorgängen. Fachlich werden dabei unter anderem Informationen wie Monat, KVNR, ursprünglicher KV-Betrag, neuer KV-Betrag, Team, Einspargrund, Produktgruppe und Übermittlungsdatum dokumentiert.

Bisher erfolgt diese Erfassung über eine Lösung, die stark an bestehende Werkzeuge wie Confluence und Access angelehnt ist. Diese Werkzeuge erfüllen zwar grundsätzlich ihren Zweck, stoßen aber bei wachsendem Datenumfang, steigenden Anforderungen an Transparenz und zunehmendem Bedarf an Auswertbarkeit an ihre Grenzen.

Typische Probleme des bisherigen Prozesses sind:

* Daten werden nicht zentral in einer modernen Anwendung verwaltet.
* Fachliche Validierungen sind nur eingeschränkt technisch abgesichert.
* Die Berechnung von Einsparungen ist abhängig von Formularlogik oder manuellen Eingaben.
* Änderungen an Datensätzen sind nicht vollständig revisionssicher nachvollziehbar.
* Rollen und Berechtigungen lassen sich nur begrenzt sauber abbilden.
* Auswertungen und Exporte müssen teilweise manuell erzeugt oder nachbearbeitet werden.
* Access- und Confluence-Abhängigkeiten erschweren Wartung, Erweiterbarkeit und langfristige Ablösung.
* Eine spätere Integration in eine interne Server- oder Datenbanklandschaft ist mit der bisherigen Struktur nur eingeschränkt möglich.

Das Projekt adressiert genau diese Punkte und schafft die Grundlage für eine fachlich saubere, technisch erweiterbare und nachvollziehbare Anwendung.

---

## Fachlicher Zweck der Anwendung

Die Anwendung dient der digitalen Erfassung und Verwaltung von Einsparungsfällen. Ein Einsparungsfall beschreibt dabei einen Vorgang, bei dem durch fachliche Prüfung, Korrektur oder Anpassung eine Differenz zwischen einem ursprünglichen KV-Betrag und einem neuen KV-Betrag entsteht.

Die zentrale Berechnung lautet:

```text
Ersparnis = Alter KV-Betrag - Neuer KV-Betrag
```

Diese Berechnung erfolgt künftig automatisch im Backend der Anwendung. Dadurch wird verhindert, dass Ersparnisse manuell falsch berechnet oder uneinheitlich gepflegt werden.

Zu jedem Einsparungsdatensatz werden strukturierte Informationen gespeichert, unter anderem:

* Monat der Einsparung
* KVNR
* Alter KV-Betrag
* Neuer KV-Betrag
* Automatisch berechnete Ersparnis
* Team
* Einspargrund
* Produktgruppe
* Übermittlungsdatum
* Ersteller des Datensatzes
* Erstellungszeitpunkt
* Änderungsinformationen
* Versionsstand

Durch diese Struktur entsteht eine zentrale Datenbasis, die später für operative Auswertungen, Teamübersichten, Statistiken und Exporte genutzt werden kann.

---

## Zielsetzung des Projekts

Das Ziel des Projekts ist der Aufbau einer modernen Webanwendung, die den bestehenden Prozess digitalisiert und langfristig stabil ersetzt.

Die Anwendung soll insbesondere folgende Ziele erfüllen:

1. **Ablösung manueller und dezentraler Strukturen**
   Die bisherige Abhängigkeit von Confluence-Formularen, Access-Datenbanken und manuellen Excel-Auswertungen soll reduziert werden. Stattdessen entsteht eine zentrale Anwendung mit einheitlicher Datenhaltung.

2. **Zentrale und strukturierte Datenerfassung**
   Einsparungsdaten sollen nicht mehr verteilt oder unstrukturiert gepflegt werden, sondern über eine einheitliche Eingabemaske mit klar definierten Feldern und Regeln.

3. **Technische Absicherung fachlicher Regeln**
   Pflichtfelder, Betragsregeln, KVNR-Länge und die automatische Berechnung der Ersparnis werden serverseitig geprüft. Dadurch wird die Datenqualität verbessert.

4. **Rollenbasierte Berechtigungen**
   Mitarbeiter, Fach-Admins (Führungskräfte) und System-Admins (IT-Administration) erhalten klar getrennte Rechte. Dadurch sieht jeder Benutzer nur die Daten und Funktionen, die für seine Aufgabe erforderlich sind.

5. **Nachvollziehbarkeit von Änderungen**
   Änderungen an Einsparungsdatensätzen werden historisiert. Dadurch kann nachvollzogen werden, wann ein Datensatz erstellt, geändert oder gelöscht wurde und welcher Benutzer die Änderung vorgenommen hat.

6. **Zentrale Statistiken**
   Die Anwendung stellt globale Auswertungen bereit, beispielsweise nach Monat, Team, Einspargrund oder Produktgruppe.

7. **Kontrollierter Export**
   CSV- und Excel-Exporte stehen ausschließlich Fach-Admins zur Verfügung. Normale Mitarbeiter und System-Admins erhalten keinen fachlichen Exportzugriff.

8. **Vorbereitung auf spätere produktionsnahe Nutzung**
   Der aktuelle Prototyp läuft lokal mit SQLite. Die Architektur ist aber so aufgebaut, dass später eine Migration auf SQL Server und ein Betrieb auf einer internen VM oder Serverumgebung möglich ist.

---

## Digitalisierter Krankenkassenprozess

Der digitalisierte Prozess beginnt mit der Anmeldung eines Benutzers. Nach erfolgreichem Login entscheidet die Rolle des Benutzers darüber, welche Funktionen verfügbar sind.

Ein Mitarbeiter kann eigene Einsparungsfälle erfassen und verwalten. Dabei wählt er die relevanten Stammdaten wie Team, Einspargrund und Produktgruppe aus und gibt die fachlichen Beträge ein. Die Anwendung prüft automatisch, ob die Eingaben gültig sind. Insbesondere wird geprüft, dass der neue KV-Betrag nicht größer als der alte KV-Betrag ist und dass keine negativen Beträge erfasst werden.

Fach-Admins (Führungskräfte) erhalten einen erweiterten Blick auf alle Datensätze. Sie können alle Einsparungsfälle einsehen, bearbeiten, löschen und exportieren. System-Admins verwalten dagegen Benutzer und Rollen, sehen aber keine fachlichen Einsparungsdaten.

Die Statistikfunktion ist für alle angemeldeten Benutzer sichtbar. Damit erhalten auch Mitarbeiter Transparenz über globale Einsparungsentwicklungen, ohne dass sie Zugriff auf Exportfunktionen erhalten.

---

## Rollen- und Berechtigungskonzept

Die Anwendung unterscheidet aktuell drei Rollen:

### Mitarbeiter

Mitarbeiter sind die regulären Anwender der Anwendung. Sie können eigene Einsparungsdatensätze erfassen und verwalten.

Mitarbeiter dürfen:

* sich anmelden
* eigene Einsparungsdatensätze anlegen
* eigene Einsparungsdatensätze anzeigen
* eigene Einsparungsdatensätze bearbeiten
* eigene Einsparungsdatensätze löschen
* globale Statistiken anzeigen

Mitarbeiter dürfen nicht:

* alle Datensätze anderer Benutzer anzeigen
* CSV-Exporte durchführen
* Excel-Exporte durchführen
* administrative Funktionen ausführen

### Fach-Admin (Führungskraft)

Führungskräfte besitzen erweiterte Rechte zur fachlichen Steuerung und Kontrolle.

Führungskräfte dürfen:

* sich anmelden
* alle Einsparungsdatensätze anzeigen
* alle Einsparungsdatensätze bearbeiten
* alle Einsparungsdatensätze löschen
* globale Statistiken anzeigen
* CSV-Exporte aller Datensätze durchführen
* Excel-Exporte aller Datensätze durchführen

### System-Admin (IT-Admin)

System-Admins besitzen ausschließlich technische und administrative Rechte.

System-Admins dürfen:

* sich anmelden
* Benutzer anlegen, aktivieren, deaktivieren und löschen
* Rollen vergeben und Passwörter zurücksetzen

System-Admins dürfen nicht:

* fachliche Einsparungsdatensätze anzeigen oder exportieren

---

## Fachliche Validierungen

Ein wesentlicher Bestandteil der Anwendung ist die technische Absicherung fachlicher Regeln. Diese Regeln werden nicht nur im späteren Frontend geprüft, sondern bereits verbindlich im Backend.

Aktuell gelten folgende Regeln:

```text
KVNR muss genau 10 Zeichen lang sein.
Alter KV-Betrag darf nicht negativ sein.
Neuer KV-Betrag darf nicht negativ sein.
Neuer KV-Betrag darf nicht größer als alter KV-Betrag sein.
Die Ersparnis wird automatisch berechnet.
Team muss gültig sein.
Einspargrund muss gültig sein.
Produktgruppe muss gültig sein.
```

Damit wird sichergestellt, dass fehlerhafte oder unvollständige Datensätze nicht dauerhaft in der Datenbank gespeichert werden.

---

## Auditierung und Nachvollziehbarkeit

Ein wichtiger Unterschied zum bisherigen Prozess ist die geplante Nachvollziehbarkeit von Änderungen. Jeder relevante Schreibvorgang an einem Einsparungsdatensatz wird protokolliert.

Auditiert werden:

* Erstellung eines Datensatzes
* Änderung eines Datensatzes
* Löschung eines Datensatzes

Dabei werden unter anderem folgende Informationen gespeichert:

* betroffene Entität
* betroffene Datensatz-ID
* Art der Änderung
* Benutzer, der die Änderung durchgeführt hat
* Zeitpunkt der Änderung
* alte Werte
* neue Werte
* technische Zusatzinformationen wie Client-IP und User-Agent

Löschungen erfolgen als sogenannte Soft Deletes. Das bedeutet, dass ein Datensatz nicht physisch aus der Datenbank entfernt wird. Stattdessen wird er als gelöscht markiert. Dadurch bleiben Informationen grundsätzlich nachvollziehbar und können bei Bedarf ausgewertet oder geprüft werden.

---

## Statistiken und Auswertungen

Die Anwendung stellt eine Statistik-API bereit, die globale Auswertungen über alle nicht gelöschten Einsparungsdatensätze ermöglicht.

Aktuell werden unter anderem folgende Auswertungen bereitgestellt:

* Gesamtanzahl der Einsparungsdatensätze
* Gesamtsumme der Einsparungen
* Durchschnittliche Einsparung
* Höchste Einsparung
* Niedrigste Einsparung
* Einsparungen nach Monat
* Einsparungen nach Team
* Einsparungen nach Einspargrund
* Einsparungen nach Produktgruppe

Diese Auswertungen bilden die Grundlage für eine spätere Statistikseite im Angular-Frontend.

---

## Exportfunktion

Die Anwendung enthält eine Exportfunktion für CSV und Excel. Diese Funktion ist bewusst eingeschränkt.

Exportberechtigt sind ausschließlich:

```text
Fach-Admins (Führungskräfte)
```

Nicht exportberechtigt sind:

```text
Mitarbeiter
```

Dadurch wird verhindert, dass reguläre Mitarbeiter Datenbestände exportieren können. Fach-Admins können Exporte für Auswertung und Qualitätssicherung erzeugen; KVNRs sind darin standardmäßig maskiert und die Antworten werden nicht im Browser-Cache gespeichert.

---

## Technisches Zielbild

Der aktuelle Projektstand ist eine gehärtete Local Edition auf Reifegrad 4. Das Backend wurde mit ASP.NET Core umgesetzt und verwendet bewusst SQLite, damit die Anwendung ohne zusätzliche Datenbankinstallation lokal betrieben und geprüft werden kann.

In der Phase Inbetriebnahme wird die Anwendung in die Kundeninfrastruktur überführt. SQL-Server-Provider und getrennte SQL-Server-Migrationshistorie sind im Code vorbereitet; die reale Datenübernahme, Kunden-PKI, Firewall-/Proxy-Konfiguration und Abnahme erfolgen bewusst erst in der tatsächlichen Zielumgebung.

Das geplante Zielbild ist eine interne Webanwendung mit:

* Angular-Frontend
* ASP.NET Core Backend
* zentraler SQL-Datenbank
* rollenbasierter Anmeldung
* strukturierter Datenerfassung
* Statistiken
* Exporten
* Auditierung
* späterer Erweiterbarkeit für weitere fachliche Anforderungen

---

## Nutzen des Projekts

Die Einsparungsdatenbank schafft einen klaren fachlichen und technischen Mehrwert.

Fachlicher Nutzen:

* einheitlicher digitaler Erfassungsprozess
* bessere Datenqualität
* weniger manuelle Nachbearbeitung
* transparente Einsparungsberechnung
* zentrale Auswertbarkeit
* nachvollziehbare Änderungen
* klare Rollen und Rechte

Technischer Nutzen:

* Ablösung von Access-Abhängigkeiten
* Reduzierung von Confluence-Formularlogik
* moderne API-basierte Architektur
* spätere Erweiterbarkeit
* vorbereitete Migration auf SQL Server
* klare Trennung von Backend und Frontend
* versionierbarer und wartbarer Quellcode

Organisatorischer Nutzen:

* bessere Nachvollziehbarkeit für Führungskräfte
* einheitliche Datenbasis für Auswertungen
* kontrollierte Exportmöglichkeiten
* geringeres Risiko durch manuelle Prozesse
* Grundlage für spätere Automatisierung und Integration

---

## Projektstatus

Der aktuelle Projektstand ist die gehärtete Local Edition auf **Reifegrad 4 von 5**. Backend und Angular-Frontend sind vorproduktions- und inbetriebnahmebereit; die vollständige Bewertung steht im [Reifegrad-4-Abschlussbericht](docs/reifegrad-4-abschlussbericht.md).

Umgesetzt sind:

* Datenmodell
* lokale SQLite-Datenbank
* Entity-Framework-Migrationen
* automatische Stammdatenanlage
* lokale Benutzerverwaltung mit SystemAdmin, FachAdmin und Mitarbeiter
* Login mit ASP.NET Core Identity, HttpOnly-Cookie und CSRF-Schutz
* Rollenprüfung
* Stammdaten-API
* Einsparungs-Fach-API
* Statistik-API
* CSV- und Excel-Export
* Exportbeschränkung auf Fach-Admins
* AuditLogging bei Erstellung, Änderung, Löschung und Exporten
* KVNR-Maskierung in Exporten und technische Audit-Metadaten ohne Snapshot-Werte
* reproduzierbare Deployment-, Health-, Backup- und Restore-Schritte
* automatische tägliche SQLite-Backups mit Integritätsprüfung und Retention
* HTTPS-/Reverse-Proxy-Härtung für ein Zertifikat aus der Kunden-PKI
* einheitliche ProblemDetails-Fehlerantworten mit Trace-ID
* vorbereiteter SQL-Server-Provider mit sicherer Sperre der SQLite-Migrationen
* CI, automatisierte Tests, CycloneDX-SBOM und Drittanbieterhinweise
* öffentliche, konfigurierbare Produkt-/Anbieterseite

Die Local Edition ist damit verkaufsvorbereitungs- und vorproduktionsreif. Rechtliche Lizenz-/Vertragsfreigabe und die kundenspezifische SQL-Server-, PKI-, Zielsystem- und Produktivabnahme bleiben klar abgegrenzte Gates zur Stufe 5.
