# HimiFlow – Code-Freeze- und Quality-Hardening-Bericht

Stand: 31.08.2026 · Local Edition · Produktversion 1.0.0 · Reifegrad 4/5

## Einordnung

Die Local Edition ist für einen kontrollierten Pilot- und Abnahmelauf eingefroren. Der Stand ist nicht als allgemein produktionsfertige Krankenkassen- oder Enterprise-Installation zu verstehen: Die kundenspezifische Phase Inbetriebnahme bleibt ein separates, noch ausstehendes Abnahme-Gate.

## Umgesetzte Härtung

- Business- und Audit-Schreibvorgänge für Stammdaten, Benutzerverwaltung, Passwortänderungen und Lizenzinstallation laufen transaktional.
- Die Datenbank erzwingt maximal eine Rolle je Benutzer; SQLite- und SQL-Server-Migrationen sind getrennt und enthalten den neuen Unique-Index.
- Production-Konfiguration wird vor Migration/Seed validiert: HTTPS, persistenter Data-Protection-Key-Ring, sichere Pfade außerhalb von `wwwroot`, SQLite-/SQL-Server-Provider, Backup-Parameter, Lizenz-Public-Key und deaktivierte Demo-Seeds.
- API-Validierungsfehler verwenden ein einheitliches `ProblemDetails`-Format mit Trace-ID und Fehlercode/Fehlerliste.
- Stammdaten-Normalisierung (Trim und Groß-/Kleinschreibung) ist zentralisiert.
- Veraltete Backup-Validierungs-/Restore-API-Endpunkte, der ungenutzte Validierungscache und die physische alte Produktgruppen-Komponente wurden entfernt; der Restore bleibt ein kontrollierter Betriebsschritt.
- Restore-Skript ist über einen echten Integrationstest für normales Restore, fehlende Zieldatenbank und beschädigte Quelle abgesichert.
- Angular verwendet `firstValueFrom` statt des abgekündigten `.toPromise()`; der einzige knappe Component-Style-Budgetwert wurde auf den gemessenen Bedarf von 4,1 kB angepasst.
- GitHub Actions verwenden aktuelle Node-24-kompatible Action-Major-Versionen und enthalten einen Windows-Job für den echten Restore-Skript-Test.
- Dokumentation ist in aktuelle Betriebs-/Security-/Lizenz-/Entwicklungsunterlagen und historische Berichte unter `docs/archive/` gegliedert.

## Verifizierte Gates

Die folgenden Ergebnisse wurden lokal mit .NET SDK 10.0.400 beziehungsweise npm 11.16.0 ermittelt:

| Gate | Ergebnis |
| --- | --- |
| Backend Release-Build | bestanden, 0 Warnungen, 0 Fehler |
| Backendtests | 114 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Frontendtests | 16 bestanden |
| Frontend Production-Build | bestanden, ohne Budgetwarnung |
| Kritische Playwright-Flows | 3/3 bestanden (Erstlogin/Passwortwechsel, FachAdmin-Fachfluss, Lizenz/Logout) |
| `npm audit --audit-level=high` | 0 Schwachstellen |
| Secret-Mustersuche | sauber |
| EF-Modellprüfung SQLite | keine ausstehenden Modelländerungen |
| EF-Modellprüfung SQL Server | keine ausstehenden Modelländerungen |
| Idempotentes SQL-Server-Skript | erfolgreich erzeugt |
| Restore-Skript-Integrationstest | bestanden: normal, fehlendes Ziel, beschädigte Quelle mit Rollback-Schutz |

Die GitHub-Workflows führen zusätzlich CodeQL, Gitleaks, Dependency Review und den Windows-Restore-Job aus. SBOM und Third-Party Notices werden reproduzierbar über das vorhandene Publish-Skript erzeugt und sind Bestandteil des Release-Prozesses. Ein Remote-Workflow-Lauf ist erst nach dem nächsten Push aussagekräftig; lokale Ergebnisse werden dadurch nicht als GitHub-Status ausgegeben.

## Bewusste Restpunkte

1. **Phase Inbetriebnahme:** reale SQL-Server-Datenbank, Datenübernahme, Firewall-/Proxyfreigabe, VM-/Containerinstallation, Kunden-PKI-Zertifikat, produktiver Secret Store sowie Monitoring und Abnahme müssen in der Viactiv-Zielumgebung durchgeführt und protokolliert werden.
2. **Backupziel:** SQLite-Backups benötigen im Pilotbetrieb ein getrenntes, zugriffsgeschütztes Ziel. Ein gemessener RPO/RTO-Nachweis und ein kundenseitiger Restore-Test stehen noch aus.
3. **Datenschutz und Vertrag:** Aufbewahrung, Löschfristen, TOMs, Impressum, Lizenz-/Supportvertrag und formale Freigaben sind kundenspezifisch rechtlich zu prüfen.
4. **Datenbestand vor Migration:** Vor einer SQL-Server-Übernahme muss die DBA bestehende Dubletten und die neue Ein-Rolle-pro-Benutzer-Regel fachlich prüfen.
5. **KI-Schlüssel:** HimiFlow verwendet aktuell keinen GPT-/OpenAI-Dienst. Sollte später ein solcher Dienst hinzukommen, sind eigener Secret-Store-Eintrag, Datenschutz-/Vertragsfreigabe und Rollenprüfung erforderlich; ein Schlüssel gehört nicht in das Repository oder Frontend.

## Freigabeempfehlung

Der Code kann als eingefrorener Reifegrad-4-Pilotstand weitergegeben und beim Kunden technisch vorinstalliert werden. Eine Freigabe als produktive Kundeninstallation erfolgt erst nach Abschluss der oben genannten Inbetriebnahme- und Rechtsgates. Für jede Auslieferung gilt der [Release-Prozess](development/release-process.md).
