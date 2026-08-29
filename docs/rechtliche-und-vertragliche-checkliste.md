# Rechtliche und vertragliche Checkliste für den Verkauf

Stand: 29.08.2026

Diese Checkliste kennzeichnet Produkt- und Vertragsentscheidungen. Sie ist keine Rechtsberatung. Vor Angebot, Vertrag und Auslieferung sollten ein auf IT-/Datenschutzrecht spezialisierter Rechtsanwalt sowie Datenschutz und IT-Sicherheit des Kunden die Unterlagen freigeben.

## 1. Anbieterangaben und Produktseite

HimiFlow besitzt die ohne Anmeldung erreichbare Seite `/legal`. Die Werte werden über `Legal:*` konfiguriert. Benötigt werden mindestens:

- vollständiger Name/Firma und Rechtsform
- ladungsfähige Anschrift
- E-Mail und geeigneter unmittelbarer Kontakt
- soweit vorhanden Registergericht/-nummer und Umsatzsteuer-ID
- freigegebener Datenschutzkontakt
- Klärung, ob Hersteller, Lizenzgeber und/oder kundeninterner Betreiber angezeigt werden

§ 5 DDG verlangt für geschäftsmäßige digitale Dienste leicht erkennbare, unmittelbar erreichbare und ständig verfügbare Informationen; die konkrete Einordnung der intern betriebenen Kundenanwendung muss rechtlich geprüft werden. Quelle: [§ 5 Digitale-Dienste-Gesetz](https://www.gesetze-im-internet.de/ddg/__5.html).

Aktueller Status: Anbieterangaben fehlen bewusst. Die UI zeigt deshalb „nicht konfiguriert“ und verhindert so ein scheinbar fertiges Impressum mit Fantasiedaten.

## 2. Datenschutzpaket

Vor Kundeneinsatz werden benötigt:

- Rollenfestlegung: Kunde als Verantwortlicher; Hersteller nur dann Auftragsverarbeiter, wenn tatsächlicher Zugriff/Verarbeitung im Auftrag besteht
- Verzeichnis der Verarbeitungstätigkeiten und Rechtsgrundlage des Kunden
- Datenschutzinformationen für Beschäftigte/Nutzer und gegebenenfalls Betroffene
- Auftragsverarbeitungsvertrag nach Art. 28 DSGVO, falls Support/Hosting Kundendaten zugänglich macht
- dokumentierte technische und organisatorische Maßnahmen
- Lösch-/Aufbewahrungskonzept einschließlich Backups und Exporte
- Entscheidung und gegebenenfalls Durchführung einer DSFA
- Incident-/Datenschutzverletzungsprozess und Ansprechpartner
- Regelung von Supportzugriff, Fernwartung, Protokollen und Datenexport am Vertragsende

Relevante Normen: [DSGVO, insbesondere Art. 5, 9, 13, 25, 28, 32 und 35](https://eur-lex.europa.eu/legal-content/DE/TXT/?uri=CELEX:32016R0679).

## 3. Software- und Lizenzvertrag

Der Vertrag sollte mindestens eindeutig regeln:

- Vertragsprodukt, Edition, Version und Lieferumfang
- Nutzungsrecht: Kunde, Gesellschaften, Standorte, Installationen und Nutzerzahl
- Jahreslizenz, Beginn/Ende, 30-Tage-Grace-Period, Erneuerung und Folgen des Ablaufs
- Installations-ID und geregelter Transfer ohne starre Hardwarebindung
- Preis, Zahlung, Preisanpassung und Steuern
- Installation, Migration, Abnahmekriterien und Verantwortungsmatrix
- Pflege, Sicherheitsupdates, Funktionsupdates und definierter Supportzeitraum
- Supportzeiten, Prioritäten, Reaktions-/Wiederherstellungsziele und Eskalation
- Gewährleistung, Haftung, Mitwirkungspflichten und höhere Gewalt
- Vertraulichkeit, Datenschutz, Supportzugriff und Unterauftragnehmer
- Dateneigentum, Export, Rückgabe/Löschung und Vertragsende
- Schutzrechte, Quellcodezugang, Escrow nur falls vereinbart
- Drittkomponenten und Lizenzhinweise
- anwendbares Recht, Gerichtsstand und Textformregelungen

Die Anwendung kann den Jahreslizenzstatus technisch abbilden; sie ersetzt kein vertragliches Lizenzdokument.

## 4. Aktuelle proprietäre Lizenz – Rechtekette prüfen

Die aktuelle `LICENSE` ist proprietär für die **ME Digitale GbR** und die Assembly-Metadaten deklarieren keine MIT-Lizenz mehr. Für die Verkaufsfreigabe bleibt trotzdem die Rechtekette zu prüfen:

- Bereits wirksam unter früheren Lizenzen erhaltene Kopien behalten grundsätzlich die eingeräumten Rechte.
- Künftige Versionen können nur dann sicher proprietär lizenziert werden, wenn die notwendige Rechteinhaberschaft an sämtlichen Beiträgen geklärt ist.
- Vor dem ersten Kundenvertrag sind Copyright-Inhaber, proprietärer Lizenztext und Bedingungen juristisch festzulegen.
- `THIRD-PARTY-NOTICES.md`, Frontend-Lizenzdatei und SBOM bleiben unabhängig davon Bestandteil der Lieferung.

Die technische Lizenzkonsistenz ist damit hergestellt; die formale Freigabe und Prüfung älterer Veröffentlichungen bleiben ein kaufmännisch-rechtlicher Punkt.

Die technische Bestandsaufnahme und die unverändert geltenden Handlungsempfehlungen stehen ergänzend im [Commercial Source Code Check](commercial-source-code-check.md).

## 5. Cyber Resilience Act (CRA)

Kommerziell bereitgestellte Software kann ein „Produkt mit digitalen Elementen“ nach der EU-Verordnung 2024/2847 sein. Die konkrete Klassifizierung und Konformitätsroute muss vor Marktbereitstellung bewertet werden. Die Hauptpflichten gelten ab **11. Dezember 2027**; Meldepflichten für aktiv ausgenutzte Schwachstellen und schwere Vorfälle beginnen am **11. September 2026**. Quellen: [EU-Kommission – CRA-Überblick](https://digital-strategy.ec.europa.eu/en/policies/cra-summary), [Verordnung (EU) 2024/2847](https://eur-lex.europa.eu/eli/reg/2024/2847/oj/eng).

Vor Verkauf ist ein CRA-Arbeitspaket anzulegen:

- Produktklassifizierung und Rollen des Wirtschaftsakteurs
- dokumentierte Cybersecurity-Risikoanalyse über den Lebenszyklus
- Secure-by-design/default und Sicherheitsanforderungen
- Supportzeitraum und Sicherheitsupdateprozess
- Schwachstellenannahme, Koordination, Behebung und Veröffentlichung
- SBOM und Abhängigkeits-/Vulnerability-Monitoring
- Meldungsprozess und Verantwortliche
- technische Dokumentation, Konformitätsbewertung, EU-Konformitätserklärung und gegebenenfalls CE-Kennzeichnung

HimiFlow liefert dafür bereits CI, Tests, SBOM, Security Defaults und Betriebsdokumentation, ist aber noch nicht formal CRA-konformitätsbewertet.

## 6. Verkaufsfreigabe-Gates

| Gate | Status |
| --- | --- |
| Technischer Reifegrad 4, SQLite-Lokalbetrieb | umgesetzt und geprüft |
| Anbieter-/Unternehmensdaten | vom Inhaber zu liefern |
| Proprietäre Lizenzstrategie / historische Lizenzstände | Rechtsberatung erforderlich |
| Datenschutzrollen, DSFA-/AVV-Entscheidung | mit Kunde erforderlich |
| Preis, SLA, Pflege und Supportzeitraum | kaufmännisch festzulegen |
| CRA-Klassifizierung/Konformitätsplan | vor Marktbereitstellung erforderlich |
| SQL Server, Kunden-PKI, VM/Netzwerk | Phase Inbetriebnahme |
| Produktivabnahme und Restore-Test | Phase Inbetriebnahme |

Damit ist das Produkt **verkaufsvorbereitungsreif**, aber ein rechtsverbindliches Angebot sollte erst nach Freigabe dieser Gates versendet werden.
