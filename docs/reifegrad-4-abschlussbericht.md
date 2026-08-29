# HimiFlow – Reifegrad-4-Abschlussbericht

Stand: 28.08.2026 · Version 1.0.0

## Ergebnis

HimiFlow erreicht nach der technischen Härtung **Reifegrad 4 von 5**: vorproduktions- und verkaufsvorbereitungsreif. Der lokale SQLite-Betrieb ist reproduzierbar, abgesichert, dokumentiert und durch automatisierte Tests überprüfbar. Die Anwendung ist bewusst noch nicht als kundenseitig produktiv freigegeben, weil SQL Server, Kunden-PKI, Zielinfrastruktur und formale rechtliche/organisatorische Freigaben erst in der Phase Inbetriebnahme feststehen können.

## Was Reifegrad 4 umfasst

| Bereich | Stand |
| --- | --- |
| Authentifizierung | lokale Identity-Cookie, CSRF, Lockout, Rate-Limit, Sitzungsinvalidierung |
| Passwörter | 14 Zeichen, Komplexität, Begriff-/Namensprüfung, kein aktuelles Passwort, Einmalpasswort |
| Rollen | SystemAdmin, FachAdmin und Mitarbeiter serverseitig getrennt |
| Benutzerbestand | auf IT Admin, Marco Meyer und Enrico Mancuso bereinigt |
| HTTPS | Production erzwingt HTTPS/HSTS; Kunden-PKI und vertrauenswürdiger Proxy vorbereitet |
| Fehler | einheitliche RFC-7807-Antworten mit Trace-ID, keine internen Details |
| SQLite | aktive lokale Datenbank, explizite Migration/Initialisierung |
| SQL Server | Provider und getrennte Migrationen vorbereitet; reale Kundenverbindung und Datenübernahme bleiben Inbetriebnahme |
| Backup | automatisch alle 24 h, Integritätsprüfung, Retention, Restore mit Sicherheitskopie |
| Datenschutz | KVNR-Maskierung, kein Browsercache für Exporte, Least Privilege, Audit |
| Lieferkette | CI, Tests, CycloneDX-SBOM und Drittanbieterhinweise |
| Produktseite | Version und konfigurierbare Anbieterinformationen unter `/legal` |
| Dokumentation | Betrieb, Login, Backup/Restore, Datenschutz, Recht/Vertrag und Inbetriebnahme |

Die Datenbereinigung war nicht destruktiv: entfernte Demo-Konten wurden deaktiviert und logisch gelöscht; historische Fach- und Auditbezüge bleiben erhalten. Vor der Bereinigung wurde ein integritätsgeprüftes SQLite-Backup angelegt.

## Reifegraddefinition

- **1 – Konzept:** Idee und Einzelbausteine
- **2 – Prototyp:** Kernabläufe funktionieren lokal
- **3 – Pilot:** Rollen, Lizenz, Health, Audit und kontrollierte Abläufe vorhanden
- **4 – Vorproduktion:** gehärtet, testbar, liefer- und inbetriebnahmebereit; Kundenkonfiguration offen
- **5 – Produktiv abgenommen:** auf Zielinfrastruktur migriert, überwacht, rechtlich/organisatorisch freigegeben und vom Kunden abgenommen

## Bewusste Grenzen

Nicht Bestandteil der jetzigen Edition: AD/SSO, SIEM, Cloudbetrieb, externe KI, SQL-Cluster und eigenbetriebene PKI. Diese Punkte sind für den vereinbarten lokalen Benutzerbetrieb nicht erforderlich. Containerisierung wird erst nach Auswahl der Kundentopologie entschieden; auf einer Windows-VM ist auch IIS plus ASP.NET-Core-Windows-Dienst eine professionelle und häufig einfachere Option.

SQLite besitzt keine anwendungsinterne Verschlüsselung. Bis zur SQL-Server-Inbetriebnahme muss der Betreiber deshalb die lokale Festplatte und das getrennte Backupziel auf Betriebssystem-/Infrastrukturebene verschlüsseln. Dieser organisatorische Infrastrukturpunkt verhindert Reifegrad 4 nicht, ist aber vor Verarbeitung echter Kundendaten zwingend freizugeben.

## Weg zu Reifegrad 5 – Phase Inbetriebnahme

1. Betreiber-, Datenschutz-, Lizenz-, Support- und SLA-Unterlagen freigeben.
2. Zielarchitektur und Betriebsverantwortung mit der Kunden-IT festlegen.
3. Hostname und Zertifikat der Kunden-PKI bereitstellen; TLS/Proxy/Firewall abnehmen.
4. SQL Server, Dienstkonto und Berechtigungen einrichten.
5. separate SQL-Server-Migrationen und kontrollierte Datenübernahme durchführen.
6. Performance-, Rollen-, Sicherheits-, Lizenz-, Backup- und Restore-Abnahme auf der Zielumgebung.
7. Monitoring, Update-/Vulnerability-Prozess und Supportwege aktivieren.
8. Go-live- und Rückfallplan ausführen und formale Kundenabnahme dokumentieren.

## Noch benötigte Angaben des Produktinhabers

- rechtlicher Name/Firma, Rechtsform, Anschrift, E-Mail, Telefon, Website
- gegebenenfalls Registergericht/-nummer und Umsatzsteuer-ID
- Datenschutzkontakt
- gewünschte proprietäre Lizenz- und Preissystematik
- Supportzeiten, Reaktionsziele und zugesagter Pflegezeitraum

Mit diesen Angaben kann anschließend das Verkaufs-, Preis- und Vertragsmodell belastbar ausgearbeitet werden.
