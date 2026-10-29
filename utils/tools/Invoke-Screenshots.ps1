#requires -Version 7.0
#requires -PSEdition Core

<#
.SYNOPSIS
    Opens Postclient and saves PNGs of the main screens.

.DESCRIPTION
    Applies the screenshot window layout, launches the UI with --screenshots,
    and writes one folder of PNGs plus manifest.json. Closes the app when the
    tour finishes. Unless -KeepLayout is set, settings.json is put back to the
    bytes from before this script ran, so the tour does not leave a different
    page or window size behind.

    Close Postclient first. The tour paints fictional mail in memory and does not
    connect to a mailbox. Copy is English unless -Language is set (en, it, fr, de, es).
    The saved language and mail profile are left unchanged. Column widths stay as saved.

    With no -Views, the tour saves the inbox, then Compose, the open message,
    Accounts, Features, Rules, Search, Retention, Spam marks, About, and Logs.
    An unknown id is recorded in manifest.json and skipped.

    Each view produces <id>.png for that window.
    folders.png is the folder column, saved with the inbox.

    PNGs are 96 DPI device pixels, so a 1600x900 window is a 1600x900 file.

.PARAMETER Mailbox
    Ignored. Screenshots use sample mail, not a saved mailbox.

.PARAMETER Language
    UI language for the tour. Default en. Also it, fr, de, es.

.PARAMETER Views
    Comma-separated view ids. Omit for the default tour.
    Ids: inbox, compose, message, accounts, features, rules, search, retention, spam, about, logs.

.PARAMETER OutDir
    Folder for the PNGs. Relative paths are under the repo root.
    Default assets/screenshots.

.PARAMETER SettleMs
    Pause after each view loads. Default 600.

.PARAMETER Exe
    Path to MaksIT.PostClient.UI.exe. Omit to use the newest build under src.

.PARAMETER Build
    Build the UI project before launching.

.PARAMETER SkipLayout
    Do not rewrite window size and pane sizes before launch.

.PARAMETER KeepLayout
    Leave settings.json as the tour finished. By default this script restores
    the file it copied at startup. Sample rules and retention are written to a temp file.

.EXAMPLE
    pwsh -File .\utils\tools\Invoke-Screenshots.ps1 -Language en

.EXAMPLE
    pwsh -File .\utils\tools\Invoke-Screenshots.ps1 -Views "inbox,compose,accounts"
#>

[CmdletBinding()]
param(
    [string]$Mailbox,
    [string]$Language = "en",
    [string]$Views,
    [string]$OutDir = "assets/screenshots",
    [int]$SettleMs = 600,
    [string]$Exe,
    [switch]$Build,
    [switch]$SkipLayout,
    [switch]$KeepLayout,
    [int]$Width = 1600,
    [int]$Height = 900
)

$ErrorActionPreference = "Stop"

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$project = Join-Path $repo "src\MaksIT.PostClient.UI\MaksIT.PostClient.UI.csproj"
$layoutScript = Join-Path $PSScriptRoot "Set-ScreenshotLayout.ps1"

function Get-PostclientSettingsPath([string]$ExePath) {
    if (-not [string]::IsNullOrWhiteSpace($env:POSTCLIENT_CONFIG)) {
        return (Join-Path $env:POSTCLIENT_CONFIG.Trim() "settings.json")
    }

    if (-not [string]::IsNullOrWhiteSpace($ExePath)) {
        $beside = Join-Path (Split-Path -Parent $ExePath) "settings.json"

        if (Test-Path -LiteralPath $beside) {
            try {
                $portable = Get-Content -LiteralPath $beside -Raw | ConvertFrom-Json

                if ($portable.Configuration.installType -eq "portable") {
                    return $beside
                }
            }
            catch {
            }
        }
    }

    if ($IsWindows) {
        return (Join-Path $env:LOCALAPPDATA "MaksIT\Postclient\settings.json")
    }

    if ($IsMacOS) {
        return (Join-Path $env:HOME "Library\Application Support\MaksIT\Postclient\settings.json")
    }

    $xdg = $env:XDG_CONFIG_HOME

    if ([string]::IsNullOrWhiteSpace($xdg)) {
        $xdg = Join-Path $env:HOME ".config"
    }

    return (Join-Path $xdg "MaksIT\postclient\settings.json")
}

function Resolve-PostclientExe {
    if (-not [string]::IsNullOrWhiteSpace($Exe)) {
        if (-not (Test-Path -LiteralPath $Exe)) {
            Write-Error "Exe not found: $Exe"
        }

        return (Resolve-Path -LiteralPath $Exe).Path
    }

    $bin = Join-Path $repo "src\MaksIT.PostClient.UI\bin"
    $found = @()

    if (Test-Path -LiteralPath $bin) {
        $found = @(Get-ChildItem -Path $bin -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq "MaksIT.PostClient.UI.exe" -or $_.Name -eq "MaksIT.PostClient.UI" })
    }

    $newest = $found | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $stale = $newest -and (Test-UiSourcesNewer $newest.FullName)

    if ($stale) {
        Write-Host "UI sources are newer than $($newest.FullName). Rebuilding."
    }

    if ($Build -or $found.Count -eq 0 -or $stale) {
        & dotnet build $project -c Debug --nologo | Out-Host

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed."
        }

        $found = @(Get-ChildItem -Path $bin -Recurse -File |
            Where-Object { $_.Name -eq "MaksIT.PostClient.UI.exe" -or $_.Name -eq "MaksIT.PostClient.UI" })
    }

    if ($found.Count -eq 0) {
        Write-Error "Could not find MaksIT.PostClient.UI. Pass -Build or -Exe."
    }

    return ($found | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

function Test-UiSourcesNewer([string]$exePath) {
    $exeTime = (Get-Item -LiteralPath $exePath).LastWriteTimeUtc
    $src = Join-Path $repo "src"

    foreach ($file in (Get-ChildItem -Path $src -Recurse -File)) {
        if ($file.FullName -match '\\(bin|obj)\\') {
            continue
        }

        if ($file.Extension -notin ".cs", ".axaml") {
            continue
        }

        if ($file.LastWriteTimeUtc -gt $exeTime) {
            return $true
        }
    }

    return $false
}

if ($SettleMs -lt 0) {
    Write-Error "SettleMs must be zero or greater."
}

if (-not [System.IO.Path]::IsPathRooted($OutDir)) {
    $OutDir = Join-Path $repo $OutDir
}

$OutDir = [System.IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$exePath = Resolve-PostclientExe
$settingsPath = Get-PostclientSettingsPath $exePath
$snapshot = $null

if ((Test-Path -LiteralPath $settingsPath) -and -not $KeepLayout) {
    $snapshot = Join-Path ([System.IO.Path]::GetTempPath()) ("postclient-settings-" + [guid]::NewGuid().ToString("n") + ".json")
    Copy-Item -LiteralPath $settingsPath -Destination $snapshot
}

$code = 1

try {
    if (-not $SkipLayout) {
        & $layoutScript -Width $Width -Height $Height -SettingsPath $settingsPath
    }

    $argList = @("--screenshots", $OutDir, "--settle-ms", "$SettleMs", "--language", $Language)

    if (-not [string]::IsNullOrWhiteSpace($Views)) {
        $argList += @("--views", $Views)
    }

    Write-Host "Launching $exePath"
    Write-Host "Saving screenshots to $OutDir"
    $proc = Start-Process -FilePath $exePath -ArgumentList $argList -Wait -PassThru
    $code = $proc.ExitCode
}
finally {
    if ($snapshot -and (Test-Path -LiteralPath $snapshot)) {
        $running = Get-Process -Name "MaksIT.PostClient.UI" -ErrorAction SilentlyContinue

        if (-not $running) {
            Copy-Item -LiteralPath $snapshot -Destination $settingsPath -Force
            Remove-Item -LiteralPath $snapshot -Force
            Write-Host "Restored $settingsPath"
        }
        else {
            Write-Warning "Postclient is still running, so settings were left in place. Snapshot: $snapshot"
        }
    }
}

$manifestPath = Join-Path $OutDir "manifest.json"

if (Test-Path -LiteralPath $manifestPath) {
    Write-Host (Get-Content -LiteralPath $manifestPath -Raw)
}

if ($code -ne 0) {
    Write-Error "Postclient exited with code $code."
}

Write-Host "Screenshots are in $OutDir"
