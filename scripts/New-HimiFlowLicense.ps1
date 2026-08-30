[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PrivateKeyPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$LicenseId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$CustomerName,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$InstallationId,

    [Parameter(Mandatory = $true)]
    [DateTime]$ValidFromUtc,

    [Parameter(Mandatory = $true)]
    [DateTime]$ValidUntilUtc,

    [Parameter(Mandatory = $true)]
    [DateTime]$GraceUntilUtc,

    [ValidateRange(1, 1000000)]
    [int]$MaxUsers = 100,

    [string[]]$Features = @('core'),

    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-ToBase64Url {
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)

    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Convert-ToUtcIso {
    param([Parameter(Mandatory = $true)][DateTime]$Value)

    return $Value.ToUniversalTime().ToString(
        'yyyy-MM-ddTHH:mm:ssZ',
        [Globalization.CultureInfo]::InvariantCulture)
}

$privatePath = [System.IO.Path]::GetFullPath($PrivateKeyPath)
if (-not (Test-Path -LiteralPath $privatePath -PathType Leaf)) {
    throw "Privater Lizenzschlüssel wurde nicht gefunden: $privatePath"
}

$validFrom = $ValidFromUtc.ToUniversalTime()
$validUntil = $ValidUntilUtc.ToUniversalTime()
$graceUntil = $GraceUntilUtc.ToUniversalTime()

if ($validUntil -le $validFrom) {
    throw 'ValidUntilUtc muss nach ValidFromUtc liegen.'
}
if ($graceUntil -lt $validUntil -or $graceUntil -gt $validUntil.AddDays(30)) {
    throw 'GraceUntilUtc muss zwischen ValidUntilUtc und maximal 30 Tagen danach liegen.'
}

$privatePem = [System.IO.File]::ReadAllText($privatePath)
$rsa = [System.Security.Cryptography.RSA]::Create()
try {
    $rsa.ImportFromPem($privatePem)

    $payload = [ordered]@{
        licenseId = $LicenseId.Trim()
        customerName = $CustomerName.Trim()
        product = 'HimiFlow'
        validFrom = Convert-ToUtcIso $validFrom
        validUntil = Convert-ToUtcIso $validUntil
        graceUntil = Convert-ToUtcIso $graceUntil
        maxUsers = $MaxUsers
        features = @($Features | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
        installationId = $InstallationId.Trim()
    }

    $payloadJson = $payload | ConvertTo-Json -Compress
    $payloadSegment = Convert-ToBase64Url ([Text.Encoding]::UTF8.GetBytes($payloadJson))
    $payloadBytes = [Text.Encoding]::UTF8.GetBytes($payloadSegment)
    $signature = $rsa.SignData(
        $payloadBytes,
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $signatureSegment = Convert-ToBase64Url $signature

    if (-not $rsa.VerifyData(
            $payloadBytes,
            $signature,
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)) {
        throw 'Die erzeugte Lizenzsignatur konnte nicht lokal verifiziert werden.'
    }

    $licenseKey = "HIMIFLOW-LICENSE-V1.$payloadSegment.$signatureSegment"
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $output = [System.IO.Path]::GetFullPath($OutputPath)
        $parent = [System.IO.Path]::GetDirectoryName($output)
        if ([string]::IsNullOrWhiteSpace($parent) -or -not (Test-Path -LiteralPath $parent -PathType Container)) {
            throw "Ausgabeverzeichnis wurde nicht gefunden: $parent"
        }
        [System.IO.File]::WriteAllText($output, $licenseKey, [System.Text.UTF8Encoding]::new($false))
        Write-Output "LicenseKeyPath=$output"
    }

    Write-Output $licenseKey
}
finally {
    $rsa.Dispose()
}
