$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $projectRoot "Program.cs"
$vellwickLogo = Join-Path $projectRoot "assets\vellwick-mark-dark.png"
$githubLogo = Join-Path $projectRoot "assets\github-mark.png"
$appIcon = Join-Path $projectRoot "assets\vellwick-extractor.ico"
$dist = Join-Path $projectRoot "dist"
$output = Join-Path $dist "Vellwick Extractor.exe"
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "Could not find the .NET Framework C# compiler."
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:$appIcon /out:$output `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    /resource:$vellwickLogo,VellwickExtractor.Assets.VellwickMarkDark.png `
    /resource:$githubLogo,VellwickExtractor.Assets.GitHubMark.png `
    $source

Write-Host "Built $output"
