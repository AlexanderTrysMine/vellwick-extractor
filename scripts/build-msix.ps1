param(
    [string]$IdentityName = "Vellwick.VellwickExtractor",
    [string]$Publisher = "CN=Vellwick",
    [string]$PublisherDisplayName = "Vellwick",
    [string]$PackageDisplayName = "Vellwick Extractor",
    [string]$Version = "",
    [string]$MakeAppxPath = $env:MAKEAPPX_PATH
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $projectRoot "dist"
$layout = Join-Path $projectRoot "obj\msix-layout"
$sourceExe = Join-Path $dist "Vellwick Extractor.exe"
$logoSource = Join-Path $projectRoot "assets\vellwick-mark-dark.png"
$outputPackage = Join-Path $dist "VellwickExtractor.msix"

function Resolve-MakeAppx {
    param([string]$ExplicitPath)

    if ($ExplicitPath -and (Test-Path -LiteralPath $ExplicitPath)) {
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $fromPath = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $wingetRoot = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    if (Test-Path -LiteralPath $wingetRoot) {
        $candidate = Get-ChildItem -LiteralPath $wingetRoot -Recurse -Filter MakeAppx.exe -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }
    }

    $kitRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path -LiteralPath $kitRoot) {
        $candidate = Get-ChildItem -LiteralPath $kitRoot -Recurse -Filter MakeAppx.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\x64\\MakeAppx\.exe$" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "Could not find MakeAppx.exe. Install Microsoft.MSIX-Toolkit or the Windows SDK."
}

function Reset-SafeDirectory {
    param([string]$Path)

    $workspace = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd('\') + '\'
    $target = [System.IO.Path]::GetFullPath($Path).TrimEnd('\') + '\'
    if (-not $target.StartsWith($workspace, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a directory outside the project: $Path"
    }

    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $target | Out-Null
}

function Get-AppVersion {
    if ($Version) {
        return $Version
    }

    $program = Get-Content -LiteralPath (Join-Path $projectRoot "Program.cs") -Raw
    if ($program -match 'AssemblyVersion\("([^"]+)"\)') {
        return $matches[1]
    }

    throw "Could not determine the app version from Program.cs."
}

function New-PackageImage {
    param(
        [string]$Path,
        [int]$Width,
        [int]$Height
    )

    Add-Type -AssemblyName System.Drawing
    $source = [System.Drawing.Image]::FromFile($logoSource)
    try {
        $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.Clear([System.Drawing.Color]::FromArgb(2, 6, 23))

                $maxWidth = $Width * 0.72
                $maxHeight = $Height * 0.72
                $scale = [Math]::Min($maxWidth / $source.Width, $maxHeight / $source.Height)
                $drawWidth = [Math]::Max(1, [int]($source.Width * $scale))
                $drawHeight = [Math]::Max(1, [int]($source.Height * $scale))
                $x = [int](($Width - $drawWidth) / 2)
                $y = [int](($Height - $drawHeight) / 2)
                $graphics.DrawImage($source, $x, $y, $drawWidth, $drawHeight)
            }
            finally {
                $graphics.Dispose()
            }

            $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bitmap.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

function Escape-Xml {
    param([string]$Value)
    return [System.Security.SecurityElement]::Escape($Value)
}

$makeAppx = Resolve-MakeAppx -ExplicitPath $MakeAppxPath
$appVersion = Get-AppVersion

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot "build.ps1")
if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Build output was not found: $sourceExe"
}

Reset-SafeDirectory -Path $layout
New-Item -ItemType Directory -Force -Path (Join-Path $layout "Assets") | Out-Null

Copy-Item -LiteralPath $sourceExe -Destination (Join-Path $layout "Vellwick Extractor.exe") -Force

New-PackageImage -Path (Join-Path $layout "Assets\StoreLogo.png") -Width 50 -Height 50
New-PackageImage -Path (Join-Path $layout "Assets\Square44x44Logo.png") -Width 44 -Height 44
New-PackageImage -Path (Join-Path $layout "Assets\Square71x71Logo.png") -Width 71 -Height 71
New-PackageImage -Path (Join-Path $layout "Assets\Square150x150Logo.png") -Width 150 -Height 150
New-PackageImage -Path (Join-Path $layout "Assets\Square310x310Logo.png") -Width 310 -Height 310
New-PackageImage -Path (Join-Path $layout "Assets\Wide310x150Logo.png") -Width 310 -Height 150

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap uap10 rescap">
  <Identity
    Name="$(Escape-Xml $IdentityName)"
    Publisher="$(Escape-Xml $Publisher)"
    Version="$(Escape-Xml $appVersion)"
    ProcessorArchitecture="neutral" />
  <Properties>
    <DisplayName>$(Escape-Xml $PackageDisplayName)</DisplayName>
    <PublisherDisplayName>$(Escape-Xml $PublisherDisplayName)</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Applications>
    <Application
      Id="App"
      Executable="Vellwick Extractor.exe"
      EntryPoint="Windows.FullTrustApplication"
      uap10:RuntimeBehavior="packagedClassicApp"
      uap10:TrustLevel="mediumIL">
      <uap:VisualElements
        DisplayName="$(Escape-Xml $PackageDisplayName)"
        Description="Extract zip files from a selected folder."
        BackgroundColor="#020617"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile
          Wide310x150Logo="Assets\Wide310x150Logo.png"
          Square310x310Logo="Assets\Square310x310Logo.png"
          Square71x71Logo="Assets\Square71x71Logo.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@

Set-Content -LiteralPath (Join-Path $layout "AppxManifest.xml") -Value $manifest -Encoding UTF8

New-Item -ItemType Directory -Force -Path $dist | Out-Null
if (Test-Path -LiteralPath $outputPackage) {
    Remove-Item -LiteralPath $outputPackage -Force
}

& $makeAppx pack /d $layout /p $outputPackage /o /v
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx failed with exit code $LASTEXITCODE."
}

Write-Host "Built $outputPackage"
Write-Host "Store note: replace IdentityName and Publisher with the exact Partner Center values before final submission."
