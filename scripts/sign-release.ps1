param(
    [string]$FilePath = ".\dist\Vellwick Extractor.exe",
    [string]$CertificateThumbprint = $env:VELLWICK_SIGN_CERT_THUMBPRINT,
    [string]$PfxPath = $env:VELLWICK_SIGN_PFX_PATH,
    [string]$PfxPassword = $env:VELLWICK_SIGN_PFX_PASSWORD,
    [string]$TimestampUrl = "http://timestamp.acs.microsoft.com",
    [string]$SignToolPath = $env:SIGNTOOL_PATH,
    [switch]$UseLocalMachineStore
)

$ErrorActionPreference = "Stop"

function Resolve-SignTool {
    param([string]$ExplicitPath)

    if ($ExplicitPath -and (Test-Path -LiteralPath $ExplicitPath)) {
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $fromPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $kitRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path -LiteralPath $kitRoot) {
        $candidate = Get-ChildItem -LiteralPath $kitRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "Could not find signtool.exe. Install the Windows SDK Build Tools or set SIGNTOOL_PATH."
}

$resolvedFile = Resolve-Path -LiteralPath $FilePath
$signtool = Resolve-SignTool -ExplicitPath $SignToolPath

if ($PfxPath) {
    $resolvedPfx = Resolve-Path -LiteralPath $PfxPath
    $args = @(
        "sign",
        "/v",
        "/fd", "SHA256",
        "/tr", $TimestampUrl,
        "/td", "SHA256",
        "/d", "Vellwick",
        "/du", "https://github.com/AlexanderTrysMine/vellwick-extractor",
        "/f", $resolvedPfx.Path
    )

    if ($PfxPassword) {
        $args += @("/p", $PfxPassword)
    }

    $args += $resolvedFile.Path
} elseif ($CertificateThumbprint) {
    $args = @(
        "sign",
        "/v",
        "/fd", "SHA256",
        "/tr", $TimestampUrl,
        "/td", "SHA256",
        "/d", "Vellwick",
        "/du", "https://github.com/AlexanderTrysMine/vellwick-extractor",
        "/sha1", $CertificateThumbprint
    )

    if ($UseLocalMachineStore) {
        $args += "/sm"
    }

    $args += $resolvedFile.Path
} else {
    throw "Provide VELLWICK_SIGN_CERT_THUMBPRINT or VELLWICK_SIGN_PFX_PATH before signing."
}

& $signtool @args
if ($LASTEXITCODE -ne 0) {
    throw "SignTool signing failed with exit code $LASTEXITCODE."
}

& $signtool verify /pa /v $resolvedFile.Path
if ($LASTEXITCODE -ne 0) {
    throw "SignTool verification failed with exit code $LASTEXITCODE."
}

Write-Host "Signed and verified $($resolvedFile.Path)"
