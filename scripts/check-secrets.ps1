[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$patterns = @(
    "-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----",
    "sk-[A-Za-z0-9_-]{20,}",
    "gh[pousr]_[A-Za-z0-9]{30,}",
    "(Password|Pwd)=[^;[:space:]]{4,}"
)
$findings = @()

foreach ($pattern in $patterns) {
    $result = & git grep -n -I -E -- $pattern -- . ":(exclude)scripts/check-secrets.ps1" ":(exclude)docs/production-gap-abschlussbericht.md" 2>$null
    if ($LASTEXITCODE -eq 0) {
        $findings += $result
    }
    elseif ($LASTEXITCODE -ne 1) {
        throw "git grep konnte die Geheimnisprüfung nicht ausführen."
    }
}

if ($findings.Count -gt 0) {
    Write-Error ("Mögliche Geheimnisse im Repository gefunden:`n" + ($findings -join "`n"))
}

Write-Host "Keine typischen fest codierten Geheimnisse gefunden."
exit 0
