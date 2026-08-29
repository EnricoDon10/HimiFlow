# HimiFlow – technische Lizenzdurchsetzung

## Benutzerlimit

`MaxUsers` zählt alle Benutzer mit `IsActive=true` und `IsDeleted=false`, einschließlich Administratoren. Anlage und Reaktivierung werden beim erreichten Limit mit `409 LICENSE_MAX_USERS_REACHED` abgelehnt. Bestehende aktive Benutzer arbeiten weiter. Passwortreset und Deaktivierung bleiben möglich, damit der SystemAdmin Zugang wiederherstellen und einen Lizenzplatz freigeben kann.

Die Prüfung läuft zusammen mit Anlage beziehungsweise Aktivierung in einer serialisierbaren Datenbanktransaktion. Damit können parallele Administratoraktionen das Limit nicht stillschweigend überschreiten.

## Features

Der signierte Payload kann weiterhin `Features` enthalten. Aktuell gibt es keine freigegebenen Feature-Tiers; deshalb werden keine künstlichen Funktionsschalter eingeführt. Eine spätere Edition kann die bereits zentral validierten Feature-Namen im `LicenseService` prüfen, ohne das Tokenformat zu ändern.

## Installations-ID

Bei aktiver Lizenzdurchsetzung startet Production ohne konfigurierte `License:InstallationId` nicht. Development darf die Durchsetzung über `License:EnforcementEnabled=false` abschalten. Es gibt keine starre Hardwarebindung; der Anbieter kann eine geregelte Installations-ID auf eine Ersatzinstallation übertragen und dafür eine neu signierte Lizenz ausstellen.

## Zeitrückstellung

`LicenseInstallations.LastSuccessfulLicenseValidationUtc` speichert einen monotonen Prüfcheckpoint. Liegt die aktuelle UTC-Zeit mehr als `License:ClockRollbackToleranceMinutes` (Standard: fünf Minuten) dahinter, wird die Lizenz als `INVALID` behandelt und eine strukturierte Warnung protokolliert. Kleine NTP-Abweichungen bleiben zulässig. Der Checkpoint wird höchstens im konfigurierten Intervall fortgeschrieben, um unnötige Datenbankschreiblast zu vermeiden.

Dies ist eine Erkennung offensichtlicher Manipulation, kein unüberwindbarer DRM-Schutz. Der private RSA-Schlüssel bleibt ausschließlich beim Anbieter.

## Read-only-Matrix

Bei `EXPIRED`, `INVALID` oder nicht konfigurierter Lizenz bleiben Lesen, Anmeldung, Abmeldung, Passwortwechsel, Lizenzinstallation, Health und Backup möglich. Zusätzlich darf der SystemAdmin Passwörter zurücksetzen und Benutzer deaktivieren. Gesperrt werden insbesondere Benutzeranlage, Rollenänderung, Aktivierung, Benutzerlöschung, Fachstammdaten- und Savings-Schreibvorgänge.
