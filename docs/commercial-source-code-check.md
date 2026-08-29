# Commercial Source Code Check

Stand: 29.08.2026 · technische Bestandsaufnahme, keine Rechtsberatung

## Aktueller Lizenzstand

Im Repository liegt aktuell eine MIT-Lizenz mit `EnricoDon10` als Copyright-Inhaber. Der Quellcode ist damit im gegenwärtigen Stand nicht allein durch die Repository-Datei als proprietär ausgewiesen. Bereits unter MIT veröffentlichte oder überlassene Fassungen behalten die damals eingeräumten Rechte; eine spätere andere Lizenzierung entzieht diese Rechte nicht rückwirkend.

Eine künftige geschlossene kommerzielle Edition ist technisch möglich, setzt aber eine rechtlich geprüfte Rechtekette, einen eigenen Endkunden-/Softwarelizenzvertrag und eine bewusste Entscheidung voraus, welche Quellcodefassung unter welchen Bedingungen angeboten wird. Historische Commits werden nicht automatisiert verändert.

## Technische Lieferbestandteile

Der Releaseprozess liefert beziehungsweise erzeugt:

- die im Release gültige `LICENSE`
- `THIRD-PARTY-NOTICES.md`
- Angular `3rdpartylicenses.txt`
- CycloneDX-SBOMs für Backend und Frontend
- eine reproduzierbare Liste direkter und transitiver Pakete über Lockdateien und .NET Restore

Die Drittanbieter-Lizenzen bleiben unabhängig von der Lizenz des eigenen HimiFlow-Codes einzuhalten. Vor jeder kommerziellen Version müssen SBOM und Notices neu erzeugt und geprüft werden.

## Empfohlene organisatorische Schritte

1. Repository nach Abschluss der notwendigen Analyse-/Entwicklungszugriffe privat betreiben.
2. Rechte an sämtlichen eigenen und fremden Beiträgen prüfen und dokumentieren.
3. Kommerzielle EULA, Wartungs-/Supportbedingungen und Quellcodezugang juristisch festlegen.
4. Release-Tag, Binärpaket, SBOM, Notices und vereinbarte Lizenz zusammen versionieren und unverändert archivieren.
5. Keine privaten Lizenzsignaturschlüssel oder produktiven Kundengeheimnisse in Repository oder Releasepaket aufnehmen.

Der technische Check ersetzt weder die lizenzrechtliche Beurteilung früherer Veröffentlichungen noch eine Vertragsprüfung.
