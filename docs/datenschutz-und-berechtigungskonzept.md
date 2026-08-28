# Datenschutz- und Berechtigungskonzept

Stand: 28.08.2026

Dieses technische Konzept ist eine Grundlage für die Freigabe durch Datenschutz, Informationssicherheit und Fachbereich des Kunden. Es ersetzt weder die kundenseitige Datenschutzdokumentation noch Rechtsberatung.

## Verarbeitung und Schutzbedarf

HimiFlow verarbeitet lokale Benutzerstammdaten, Team-/Rollenzuordnungen, Einsparungsfälle, eine verkürzt benannte KVNR, Beträge, Zeitpunkte sowie technische Auditdaten. Eine KVNR ist ein personenbezogenes Identifikationsmerkmal; der fachliche Kontext kann zudem Rückschlüsse auf Gesundheits- oder Sozialdaten ermöglichen. Der Betreiber muss deshalb prüfen und dokumentieren, ob besondere Kategorien personenbezogener Daten nach Art. 9 DSGVO betroffen sind.

Zweck ist ausschließlich die Erfassung, Bearbeitung, Auswertung und revisionsnahe Nachvollziehbarkeit von Einsparungen. Eine Nutzung für Leistungs-/Verhaltenskontrolle, externe KI-Dienste oder andere Zwecke ist nicht implementiert und bedürfte einer eigenen Freigabe.

## Rollenmatrix

| Funktion | Mitarbeiter | FachAdmin | SystemAdmin |
| --- | ---: | ---: | ---: |
| Eigene Einsparungen anlegen/sehen/ändern/löschen | ja | ja | nein |
| Fremde Einzelfälle sehen/ändern | nein | ja | nein |
| Globale aggregierte Statistik | ja | ja | nein |
| CSV-/Excel-Gesamtexport | nein | ja | nein |
| Produktgruppen verwalten | nein | ja | nein |
| Benutzer/Rollen/Lizenz/Backups/Audit verwalten | nein | nein | ja |

Die technische Trennung von Fach- und Systemadministration folgt dem Prinzip der minimalen Berechtigung. Alle API-Endpunkte erzwingen Rollen serverseitig; das Ausblenden von Frontend-Menüs ist nur eine zusätzliche Bedienhilfe.

## Umgesetzte technische und organisatorische Maßnahmen

- individuelle Konten, Rollen und Teamzuordnung
- starke lokale Passwörter, Lockout, Einmalpasswörter und Sitzungsinvalidierung
- HTTPS in Production; Kundenzertifikat aus der Kunden-PKI empfohlen
- HttpOnly-/Secure-/SameSite-Cookies und CSRF-Schutz
- keine öffentliche Registrierung und kein Passwortversand durch die Anwendung
- maskierte KVNR in Exporten und Audit-Snapshots
- `no-store`/`no-cache` für Exporte
- Audit-Metadaten für fachliche und administrative Änderungen
- konsistente Fehlerantworten ohne Stacktrace oder interne Pfade
- tägliche integritätsgeprüfte Backups mit beschränktem Zugriff
- keine Telemetrie, kein externer KI-Dienst und kein SSO in der Local Edition
- SBOM und Drittanbieter-Lizenzhinweise für die Lieferkette

## Betreiberentscheidungen vor Produktivbetrieb

Der Kunde ist bei Eigenbetrieb typischerweise Verantwortlicher im Sinne der DSGVO. Ob der Hersteller bei Support, Fernwartung oder Hosting Auftragsverarbeiter wird, hängt vom tatsächlichen Zugriff und Vertrag ab. Vor Inbetriebnahme sind mindestens zu entscheiden:

1. Rechtsgrundlage, Zwecke, Datenkategorien und betroffene Personen dokumentieren.
2. Informationspflichten und Verzeichnis der Verarbeitungstätigkeiten ergänzen.
3. Lösch- und Aufbewahrungsfristen für Fachdaten, Audit, Exporte und Backups festlegen.
4. Berechtigungsgenehmigung, Rezertifizierung, Eintritt/Wechsel/Austritt und Notfallzugriff regeln.
5. Prüfen, ob wegen Umfang, Kontext und Gesundheits-/Sozialdaten eine Datenschutz-Folgenabschätzung erforderlich ist; die [BfDI-Liste nach Art. 35 Abs. 4 DSGVO](https://www.bfdi.bund.de/SharedDocs/Downloads/DE/Muster/Liste_VerarbeitungsvorgaengeArt35.pdf?__blob=publicationFile&v=7) ist hierfür ein relevanter Ausgangspunkt.
6. Falls Support Zugriff auf personenbezogene Daten erhält: Auftragsverarbeitungsvertrag, Weisungen, Vertraulichkeit, Unterauftragnehmer, Löschung und technische Maßnahmen regeln.
7. Protokollzugriff, Vorfallsprozess und Meldewege des Kunden festlegen.
8. Prüfen, ob eine vollständige KVNR überhaupt erforderlich ist; wenn fachlich möglich, das Datenmodell auf eine Vorgangs-/Referenz-ID reduzieren.

SQLite verschlüsselt die Datenbankdatei nicht eigenständig. Für den aktuellen Laptopbetrieb sind deshalb Windows-Geräteverschlüsselung/BitLocker, ein geschütztes Benutzerkonto und ein verschlüsseltes getrenntes Backupziel erforderlich. In der Kundenumgebung müssen Volume-/Backupverschlüsselung und später die SQL-Server-Verschlüsselung durch die Kunden-IT nach Schutzbedarfsfreigabe umgesetzt werden.

Maßgebliche Grundsätze sind insbesondere Datenminimierung und Integrität/Vertraulichkeit (Art. 5), Datenschutz durch Technikgestaltung (Art. 25), Auftragsverarbeitung (Art. 28), Sicherheit (Art. 32) und gegebenenfalls DSFA (Art. 35) der [DSGVO](https://eur-lex.europa.eu/legal-content/DE/TXT/?uri=CELEX:32016R0679).

## Offene Retention

`Audit:RetentionDays=0` bedeutet technisch „keine automatische Löschung“. Das ist keine datenschutzrechtliche Freigabe für unbegrenzte Speicherung. Eine automatische Löschung darf erst implementiert werden, nachdem Fachbereich, Revision und Datenschutz eine konkrete Frist sowie Löschsperren für laufende Prüfungen freigegeben haben.
