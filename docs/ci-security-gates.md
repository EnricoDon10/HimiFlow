# CI- und Security-Gates

Die Haupt-CI führt Release-Builds, Backend- und Frontendtests, NuGet-/npm-Schwachstellenprüfungen, beide EF-Migrationsmodellprüfungen, die Erzeugung eines idempotenten SQL-Server-Skripts und eine enge Geheimnissuche aus. NuGet-Warnungen der Stufen High und Critical (`NU1903`, `NU1904`) werden als Fehler behandelt; `npm audit --audit-level=high` setzt dieselbe Schwelle im Frontend.

CodeQL analysiert C# und JavaScript/TypeScript bei Pushes, Pull Requests und wöchentlich. Dependency Review blockiert in öffentlichen Repositories neue High-/Critical-Risiken. Für ein später privates Repository benötigt diese GitHub-Funktion gegebenenfalls GitHub Advanced Security; der Workflow überspringt den Schritt deshalb bei privaten Repositories ohne stillschweigend die normale CI zu umgehen.

Dependabot erstellt wöchentlich Update-Vorschläge für NuGet, npm und GitHub Actions. Updates werden nicht automatisch zusammengeführt, sondern müssen Build, Tests und Review bestehen.

Die lokale Mustersuche ersetzt GitHubs serverseitiges Secret Scanning nicht. Vor der kommerziellen Privatstellung sind in den Repository-Einstellungen Secret Scanning und Push Protection zu aktivieren, soweit der gewählte GitHub-Tarif dies unterstützt. Gefundene echte Schlüssel müssen widerrufen und ersetzt werden; bloßes Entfernen aus dem letzten Commit reicht nicht.
