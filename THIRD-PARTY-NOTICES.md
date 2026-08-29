# HimiFlow – Hinweise zu Drittkomponenten

Stand: 29.08.2026

HimiFlow verwendet Open-Source-Komponenten. Dieses Dokument ist eine technische Übersicht und ersetzt keine abschließende lizenzrechtliche Prüfung des auszuliefernden Produktpakets.

## Backend-Laufzeit

| Komponente | Version | Lizenz |
| --- | --- | --- |
| ASP.NET Core Identity Entity Framework Core | 10.0.11 | MIT |
| Entity Framework Core SQLite | 10.0.11 | MIT |
| Entity Framework Core SQL Server | 10.0.11 | MIT |
| BCrypt.Net-Next | 4.2.0 | MIT |
| ClosedXML | 0.105.1 | MIT |
| Microsoft.AspNetCore.OpenApi | 10.0.11 | MIT |
| Swashbuckle.AspNetCore | 10.2.3 | MIT |

Transitive Abhängigkeiten werden durch den .NET-Publish-Prozess aufgelöst. Vor jeder kommerziellen Veröffentlichung muss die tatsächlich ausgelieferte Paketliste erneut erzeugt und gegen die jeweiligen Lizenztexte geprüft werden.

## Frontend-Laufzeit

| Komponente | Version | Lizenz |
| --- | --- | --- |
| Angular (Common, Compiler, Core, Forms, Platform Browser, Router) | 22.1.4 | MIT |
| RxJS | 7.8.2 | Apache-2.0 |
| tslib | 2.8.1 | 0BSD |
| zone.js | 0.16.2 | MIT |

Der Angular-Production-Build erzeugt zusätzlich `3rdpartylicenses.txt` mit den vollständigen Hinweisen des konkreten Frontend-Bundles. Das HimiFlow-Publish-Skript übernimmt diese Datei in das Auslieferungspaket.

## Veröffentlichungspflicht

Bei jeder Kundenauslieferung müssen mindestens folgende Dateien mitgeliefert werden:

- die endgültige HimiFlow-Lizenzvereinbarung
- dieses Dokument in aktualisierter Form
- die vom Angular-Build erzeugte `3rdpartylicenses.txt`
- weitere Lizenz- oder Copyright-Hinweise, die durch eine Abhängigkeit verlangt werden

Die derzeitige HimiFlow-Repository-Lizenz ist separat zu bewerten. Sie bestimmt die Rechte am eigenen HimiFlow-Quellcode und darf nicht mit den Lizenzen der Drittkomponenten verwechselt werden.
