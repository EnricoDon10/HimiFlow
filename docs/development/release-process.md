# Release-Prozess

Dieser Prozess gilt für die Local Edition und trennt reproduzierbare Softwareauslieferung von der späteren kundenspezifischen Inbetriebnahme.

## Versionen

- `0.9.x`: Pilot- und Abnahmestände, solange noch keine produktive Kundenumgebung freigegeben ist.
- `1.0.0`: erste vertraglich freigegebene Kundeninstallation nach dokumentierter Abnahme.
- Patch-Releases erhöhen nur die dritte Stelle und enthalten ausschließlich rückwärtskompatible Fehler- oder Sicherheitskorrekturen.

## Vor jedem Release

1. Änderungsumfang, Migrationen und offene Risiken im Release-Eintrag dokumentieren.
2. Backend in Release-Konfiguration bauen und alle Backendtests ausführen.
3. Frontend mit Produktionskonfiguration bauen, Frontendtests und `npm audit --audit-level=high` ausführen.
4. Beide EF-Kontexte auf ausstehende Modelländerungen prüfen und das idempotente SQL-Server-Skript erzeugen.
5. Restore-Integrationstest, Geheimnissuche, Abhängigkeitsprüfung und SBOM erfolgreich ausführen.
6. Deployment-Dateien, Konfigurationsreferenz, Drittanbieterhinweise und Prüfsummen aktualisieren.
7. Einen annotierten Git-Tag mit Release Notes erstellen; automatische Veröffentlichung ist nicht vorgesehen.

## Übergabe

Die Übergabe enthält Version, Git-Commit/Tag, Artefakte, Prüfsummen, Konfigurationsvorlage, Migrationshinweise, bekannte Einschränkungen und den Support-Kontakt. Kunden-PKI, SQL-Server, Firewall, Backupziel und fachliche Abnahme werden erst im separaten Inbetriebnahme-Protokoll bestätigt.
