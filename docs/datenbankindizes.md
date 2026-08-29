# HimiFlow – Datenbankindizes

Die Indizes orientieren sich an den tatsächlich verwendeten Listen-, Filter- und Statistikabfragen. Ein alleiniger Index auf `IsDeleted` wird bewusst vermieden, weil dieses boolesche Feld nur geringe Selektivität besitzt.

| Index | Zweck |
| --- | --- |
| `IX_SavingsEntries_ActiveMonthCreatedAt` | Aktive Gesamtliste, Pagination, Export und Monatsstatistik in der Standardsortierung. |
| `IX_SavingsEntries_UserActiveMonthCreatedAt` | „Meine Einsparungen“ sowie der optionale Benutzerfilter. |
| `IX_SavingsEntries_TeamActiveMonth` | Teamfilter und teambezogene Statistik. |
| `IX_SavingsEntries_ReasonActiveMonth` | Filter und Statistik nach Einspargrund. |
| `IX_SavingsEntries_ProductGroupActiveMonth` | Filter und Statistik nach Produktgruppe. |

Weitere Indizes sollen erst nach Messung realer Produktionsabfragen ergänzt werden. Jeder zusätzliche Index erhöht Speicherbedarf und Schreibaufwand.
