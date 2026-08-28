# Login- und Passwortkonzept

Stand: 28.08.2026

## Zielbild

HimiFlow verwendet in der Local Edition ausschließlich lokale Benutzerkonten. Active Directory, SSO, externe Identitätsprovider und eine Anmeldung über Drittdienste sind bewusst nicht enthalten.

## Konto-Lebenszyklus

1. Ein `SystemAdmin` legt ein Konto mit Name, Benutzername, Rolle und gegebenenfalls Team an.
2. Das System erzeugt kryptografisch zufällig ein Einmalpasswort und zeigt es genau im Administrationsvorgang an.
3. Die Übermittlung erfolgt getrennt vom Benutzernamen über einen vom Kunden freigegebenen Kanal.
4. Nach der ersten Anmeldung ist ausschließlich der Passwortwechsel zulässig.
5. Der Benutzer setzt ein persönliches Passwort; danach beginnt die reguläre Sitzung.
6. Bei Verdacht oder Verlust setzt der `SystemAdmin` das Passwort zurück. Alle bestehenden Sitzungen werden dadurch ungültig.
7. Bei Austritt wird das Konto sofort deaktiviert oder logisch gelöscht. Historische Fach- und Auditbezüge bleiben erhalten.

## Technische Regeln

- Mindestlänge 14 Zeichen
- Großbuchstabe, Kleinbuchstabe, Ziffer, Sonderzeichen
- mindestens vier unterschiedliche Zeichen
- kein leicht erratbarer Standard-/Produktbegriff
- kein Benutzername oder längerer Bestandteil des Anzeigenamens
- kein Wiederverwenden des aktuellen Passworts
- fünf Fehlversuche: 15 Minuten Kontosperre
- zehn Login-Anfragen je IP/Minute als zusätzliche Drosselung
- 30 Minuten Inaktivität bis zum Sitzungsablauf; gleitende Verlängerung
- keine Speicherung oder Protokollierung von Klartextpasswörtern

Passwörter werden über ASP.NET Core Identity gehasht. Das Einmalpasswort wird nicht in Git oder in einer Konfigurationsdatei gespeichert.

## Sitzungs- und CSRF-Schutz

- `HimiFlow.Auth`: `HttpOnly`, `SameSite=Strict`, bei HTTPS `Secure`
- `HimiFlow.Antiforgery`: `HttpOnly`, `SameSite=Strict`, bei HTTPS `Secure`
- schreibende Aufrufe benötigen zusätzlich den Header `X-XSRF-TOKEN`
- Deaktivierung, Rollenwechsel und Passwortreset ändern den Security Stamp und beenden damit bestehende Berechtigungen
- Login, Logout und Passwortwechsel werden als Audit-Metadaten festgehalten

## Betriebliche Regeln

- Keine regelmäßige Passwortänderung nur nach Kalender. Ein Wechsel erfolgt nach Reset, bei Kompromittierungsverdacht oder auf begründete Anweisung.
- Keine gemeinsamen Konten außer einem eindeutig zugeordneten Notfallkonto nach separater Kundenregelung.
- Einmalpasswörter niemals per offenem Ticket, Quellcode, Screenshot-Sammlung oder gemeinsam mit dem Benutzernamen versenden.
- Adminrechte werden nur für die konkrete Aufgabe vergeben; mindestens ein aktiver `SystemAdmin` muss erhalten bleiben.
- Konto- und Rollenbestand mindestens vierteljährlich durch den Betreiber prüfen.

Diese Regeln orientieren sich an den Empfehlungen des [BSI zur Regelung des Passwortgebrauchs](https://www.bsi.bund.de/SharedDocs/Downloads/DE/BSI/Grundschutz/IT-GS-Kompendium_Einzel_PDFs_2023/02_ORP_Organisation_und_Personal/ORP_4_Identitaets_und_Berechtigungsmanagement_Edition_2023.pdf?__blob=publicationFile&v=3). Kundenspezifische Sicherheitsvorgaben können strengere Werte verlangen.
