# Audit-Aufbewahrung

HimiFlow kann technische Auditprotokolle optional zeitgesteuert bereinigen. Im Auslieferungszustand ist diese Funktion deaktiviert und `Audit:RetentionDays = 0` bedeutet ausdrücklich: keine automatische Löschung.

Eine Bereinigung wird erst aktiv, wenn sowohl `Audit:CleanupEnabled = true` als auch ein positiver Wert für `Audit:RetentionDays` konfiguriert wurden. Sie erfolgt in kleinen, transaktionalen Batches und wird im Anwendungslog protokolliert.

Die konkrete Frist darf nicht technisch geraten werden. Sie muss vor Aktivierung von Datenschutz, Informationssicherheit, Fachbereich und gegebenenfalls Rechtsberatung organisatorisch festgelegt und freigegeben werden.

Soft-gelöschte Einsparungen und Sicherungsdateien werden von diesem Audit-Prozess nicht berührt. Für Fachdaten ist vor jeder automatisierten Löschung ein separates fachliches Löschkonzept erforderlich. Backup-Aufbewahrung wird ausschließlich über die vorhandene `Backup`-Konfiguration gesteuert.
