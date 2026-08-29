# Data-Protection-Key-Ring

HimiFlow verwendet ASP.NET Core Data Protection für Authentifizierungs-Cookies und verwandte Sicherheitsfunktionen. Der Key-Ring wird nie aus dem WebRoot ausgeliefert und darf nicht in Git liegen.

## Lokale Entwicklung und Tests

Ohne `DataProtection:KeyRingPath` läuft Development/Testing mit dem ASP.NET-Core-Standardverzeichnis. Das ist für den lokalen Prototyp und isolierte Tests ausreichend; bei einem Neustart kann der Entwicklungs-Key-Ring neu erzeugt werden.

## Kundenbetrieb

Für Production muss ein persistenter, nicht öffentlicher Pfad konfiguriert werden, zum Beispiel als Umgebungsvariable:

```powershell
$env:DataProtection__KeyRingPath = 'C:\ProgramData\HimiFlow\keys'
```

Der Production-Start bricht bei fehlendem Pfad kontrolliert ab. Der Dienstbenutzer benötigt dort Lesen, Schreiben und Erstellen, andere Benutzer möglichst keinen Zugriff. Der Ordner darf nicht unter `wwwroot` liegen und sollte auf einem verschlüsselten Volume liegen.

Unter Windows schützt HimiFlow die Dateien zusätzlich mit dem integrierten DPAPI-Mechanismus (`LocalMachine`). Der Dienst muss daher auf derselben Windows-Installation betrieben werden; bei einem geplanten Server-/VM-Umzug ist der Key-Ring gemäß dem Kunden-Betriebskonzept geschützt zu übertragen. In Linux-/Container-Umgebungen ist stattdessen der vom Kunden freigegebene Secret-/Volume-Schutz zu verwenden.

Die konkrete Kunden-PKI, Volumeverschlüsselung, ACL-Freigabe und ein geplanter Umzug gehören zur Inbetriebnahme und werden nicht durch eigene Kryptografie ersetzt.
