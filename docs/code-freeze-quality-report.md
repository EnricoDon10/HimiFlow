# HimiFlow – Code-Freeze- und Quality-Hardening-Bericht

Stand: 31.08.2026 · Local Edition · Produktversion 0.9.0-rc.1 · Reifegrad 4/5

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

## Freeze-Patch dieser Runde

- **Unicode-Stammdaten:** Die Dublettenprüfung für Teams, Einspargründe und Produktgruppen vergleicht nach zentralem Trim, Unicode-Form-C-Normalisierung und invariantem Großschreiben in .NET. Damit hängt sie nicht mehr von `ToUpper()`/SQLite-Collation ab; die sichtbare Schreibweise bleibt unverändert. Die Fälle `Kompressionsartikel`, `Änderung`, `ÜBERNAHME`, Leerzeichen und deaktivierte Reaktivierung sind getestet.
- **Schema-sicheres Ein-Rolle-Preflight:** Vor jeder Migration wird ein bestehender Rollendoppelbestand direkt über das historisch verfügbare `UserRoles`-Schema identifiziert. Der Check materialisiert nicht das aktuelle Identity-Modell, ergänzt Benutzername/Anzeigename nur bei sicher vorhandenen Spalten und meldet andernfalls mindestens die Benutzer-ID. Das Upgrade bricht verständlich ab; es werden keine Daten gelöscht. Der Unique-Index `IX_UserRoles_AppUserId_Unique` bleibt die technische Durchsetzung.
- **npm/Version:** Nur die vier im Lockfile geprüften nativen Build-Scripts (`@parcel/watcher`, `esbuild`, `lmdb`, `msgpackr-extract`) sind versionsgenau per `allowScripts` freigegeben. Der Pilotstand lautet konsistent `0.9.0-rc.1`; `1.0.0` bleibt der späteren Kundenabnahme vorbehalten.
- **Anbieterangaben:** `/api/public/product-info` und `/legal` liefern nun konfigurationsgetrieben ME Digitale GbR Dirr & Mancuso, Kurzform, Anschrift, beide Gesellschafter, beide `tel:`-Telefonnummern, E-Mail und Inhaltsverantwortliche. Eine leere USt-ID wird vollständig ausgeblendet; Register-, Website- und Datenschutzwerte bleiben leer, solange sie nicht rechtlich freigegeben konfiguriert sind.
- **VIACTIV-Pilottest:** Die signierte Testlizenz `TEST-VIACTIV-2026-0908-01` wurde über den regulären SystemAdmin-Lizenzdialog installiert und als `ACTIVE` geprüft (Kunde `VIACTIV Krankenkasse`, `viactiv-test-01`, gültig bis 08.09.2026 23:59:59 UTC, Grace bis 09.09.2026 23:59:59 UTC, MaxUsers 50, Feature `core`). Schlüsseldateien liegen ausschließlich unter `.local-secrets/himiflow-test-licensing/`; weder vollständiger Lizenzschlüssel noch Private Key stehen in diesem Bericht oder Git.
- **Playwright-E2E-Stabilisierung:** Der rote CI-Flow entstand beim Erstellen des FachAdmins durch ein noch nicht abgeschlossenes Laden der Teams; der fehlende Teamwert führte anschließend nur zu einem irreführenden Fehler am temporären Passwort-Locator. Der Test wartet jetzt auf die echte Teams-API-Antwort, die sichtbare Benutzerverwaltungsseite und mindestens eine auswählbare Option, wählt ein Team explizit und prüft die Create-Response mit HTTP-Status und Response-Body. Da die drei seriellen Flows eine gemeinsame isolierte Datenbank und erzeugte Zugangsdaten verwenden, sind Retries auf `0` gesetzt, damit kein teilweiser Lauf einen Folgefehler mit falschen Zugangsdaten erzeugt.

## Historische SQLite-Migrationswarnung

Die Warnungen `SqlOperation` während des Rebuilds von `Users` und `PRAGMA foreign_keys = 0` stammen unverändert aus der historischen Migration `PhaseBIdentityAuthentication`. EF Core weist korrekt auf die nicht-transaktionale SQLite-Table-Rebuild-Sequenz hin. Eine Umschreibung dieser bereits ausgelieferten Migration wäre für Bestandsdaten riskanter als die Warnung und wurde deshalb bewusst nicht vorgenommen. Fresh-DB-Migration, Restore und das neue Preflight-Verhalten werden weiterhin automatisiert geprüft.

## Datensicherung: Ursache und Fix

Der Requestpfad wurde vollständig verfolgt. Ein nicht vorhandenes oder leeres Backup-Verzeichnis ist ein gültiger Zustand und liefert weiterhin ein Statusobjekt (kein Fehler). Der konkrete Fehlerpfad lag bei nicht lesbaren/ungültig konfigurierten Verzeichnissen: `Directory.EnumerateFiles` propagierte die Ausnahme, während das Frontend nur den pauschalen Fallbacktext zeigte. Der Status-Endpunkt gibt solche Betriebsfehler jetzt als `503 ProblemDetails` mit verständlichem Detail zurück; normale Zustände (`MISSING`, `CURRENT`, `OVERDUE`, `DISABLED`, externer Provider) bleiben `200 OK`. Die Oberfläche räumt beim erfolgreichen Laden alte Fehler auf, unterstützt Retry und lädt nach manueller Erstellung erneut. Dafür sind Evaluator-, HTTP-Integrations- und Frontend-Komponententests ergänzt.

Der bestehende SystemAdmin-Playwright-Flow ruft zusätzlich `/admin/backup-recovery` auf und prüft Überschrift, Statuskarte, einen zulässigen Betriebsstatus (`MISSING`, `CURRENT`, `OVERDUE`, `DISABLED` oder `EXTERNAL_PROVIDER`) sowie das Ausbleiben einer sichtbaren roten Fehlermeldung. Ein Restore wird im Browser weiterhin nicht ausgeführt. Der vollständige Flow wurde danach in drei aufeinanderfolgenden lokalen Läufen mit jeweils neuer temporärer SQLite-Datenbank erfolgreich ausgeführt.

## Verifizierte Gates

Die folgenden Ergebnisse wurden lokal mit .NET SDK 10.0.400 beziehungsweise npm 11.16.0 ermittelt:

| Gate | Ergebnis |
| --- | --- |
| Backend Release-Build | bestanden, 0 Warnungen, 0 Fehler |
| Backendtests | 126 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Frontendtests | 20 bestanden |
| Frontend Production-Build | bestanden, ohne Budgetwarnung |
| Kritische Playwright-Flows | 3/3 bestanden (Erstlogin/Passwortwechsel mit Backup-Smoke, FachAdmin-Fachfluss, Lizenz/Logout) |
| Playwright-Mehrfachlauf | 3 aufeinanderfolgende lokale Läufe jeweils 3/3 bestanden, neue isolierte SQLite-DB je Lauf |
| `npm audit --audit-level=high` | 0 Schwachstellen |
| Secret-Mustersuche | sauber |
| Gitleaks lokal | nicht installiert; Gitleaks bleibt im GitHub-Workflow aktiviert |
| EF-Modellprüfung SQLite | keine ausstehenden Modelländerungen |
| EF-Modellprüfung SQL Server | keine ausstehenden Modelländerungen |
| Idempotentes SQL-Server-Skript | erfolgreich erzeugt |
| Restore-Skript-Integrationstest | bestanden: normal, fehlendes Ziel, beschädigte Quelle mit Rollback-Schutz |
| Fresh SQLite / historische Preflight-Schemata / Ein-Rollen-Preflight / Unicode | bestanden |
| npm `ci` / Script-Policy | bestanden, keine unbewerteten Install-Scripts, 0 Schwachstellen |
| Öffentliche Anbieterinformationen | bestanden: Felder, Telefonnummern, Verantwortliche, leere USt-ID ausgeblendet |

Die GitHub-Workflows führen zusätzlich CodeQL, Gitleaks, Dependency Review und den Windows-Restore-Job aus. SBOM und Third-Party Notices werden reproduzierbar über das vorhandene Publish-Skript erzeugt und sind Bestandteil des Release-Prozesses. Für den finalen Commit `26a603b` sind [HimiFlow CI #26](https://github.com/EnricoDon10/HimiFlow/actions/runs/33419591845) einschließlich Geheimnisprüfung/Restore, [HimiFlow Browser E2E #13](https://github.com/EnricoDon10/HimiFlow/actions/runs/33419591890) und [HimiFlow CodeQL #24](https://github.com/EnricoDon10/HimiFlow/actions/runs/33419591871) erfolgreich abgeschlossen.

## Bewusste Restpunkte

1. **Phase Inbetriebnahme:** reale SQL-Server-Datenbank, Datenübernahme, Firewall-/Proxyfreigabe, VM-/Containerinstallation, Kunden-PKI-Zertifikat, produktiver Secret Store sowie Monitoring und Abnahme müssen in der Viactiv-Zielumgebung durchgeführt und protokolliert werden.
2. **Backupziel:** SQLite-Backups benötigen im Pilotbetrieb ein getrenntes, zugriffsgeschütztes Ziel. Ein gemessener RPO/RTO-Nachweis und ein kundenseitiger Restore-Test stehen noch aus.
3. **Datenschutz und Vertrag:** Aufbewahrung, Löschfristen, TOMs, Impressum, Lizenz-/Supportvertrag und formale Freigaben sind kundenspezifisch rechtlich zu prüfen.
4. **Datenbestand vor Migration:** Vor einer SQL-Server-Übernahme muss die DBA bestehende Dubletten und die neue Ein-Rolle-pro-Benutzer-Regel fachlich prüfen.
5. **KI-Schlüssel:** HimiFlow verwendet aktuell keinen GPT-/OpenAI-Dienst. Sollte später ein solcher Dienst hinzukommen, sind eigener Secret-Store-Eintrag, Datenschutz-/Vertragsfreigabe und Rollenprüfung erforderlich; ein Schlüssel gehört nicht in das Repository oder Frontend.

## Freigabeempfehlung

Der Code kann als eingefrorener Reifegrad-4-Pilotstand weitergegeben und beim Kunden technisch vorinstalliert werden. Eine Freigabe als produktive Kundeninstallation erfolgt erst nach Abschluss der oben genannten Inbetriebnahme- und Rechtsgates. Für jede Auslieferung gilt der [Release-Prozess](development/release-process.md).
