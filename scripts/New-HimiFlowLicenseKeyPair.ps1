[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$directory = [System.IO.Path]::GetFullPath($OutputDirectory)
if ([string]::Equals([System.IO.Path]::GetPathRoot($directory), $directory, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Bitte ein eigenes Unterverzeichnis für die Lizenzschlüssel verwenden, nicht das Laufwerks-Root.'
}

New-Item -ItemType Directory -Path $directory -Force | Out-Null
$privatePath = Join-Path $directory 'himiflow-license-private.pem'
$publicPath = Join-Path $directory 'himiflow-license-public.pem'

if ((Test-Path -LiteralPath $privatePath -PathType Leaf) -or (Test-Path -LiteralPath $publicPath -PathType Leaf)) {
    throw "Im Zielverzeichnis existiert bereits ein Lizenzschlüssel. Bitte ein leeres Verzeichnis verwenden: $directory"
}

$rsa = [System.Security.Cryptography.RSA]::Create(3072)
try {
    [System.IO.File]::WriteAllText(
        $privatePath,
        $rsa.ExportPkcs8PrivateKeyPem(),
        [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText(
        $publicPath,
        $rsa.ExportSubjectPublicKeyInfoPem(),
        [System.Text.UTF8Encoding]::new($false))
}
finally {
    $rsa.Dispose()
}

Write-Output "PrivateKeyPath=$privatePath"
Write-Output "PublicKeyPath=$publicPath"
Write-Output 'Der private Schlüssel darf niemals in Git, ein Releasepaket oder eine Kundeninstallation gelangen.'
