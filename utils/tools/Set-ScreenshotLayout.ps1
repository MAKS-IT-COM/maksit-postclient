#requires -Version 7.0
#requires -PSEdition Core

<#
.SYNOPSIS
    Pins Postclient to one window size and shell proportions for screenshots.

.DESCRIPTION
    Writes WindowWidth, WindowHeight, position, WindowState, FolderWidth,
    ListWidth, and ListHeight in the Postclient settings.json.

    The app reads that file when it opens and writes it again when it closes.
    Close Postclient before running this script. The first run keeps a
    backup next to settings.json. -Restore copies that backup back.

    Sizes are Avalonia device-independent pixels. At 100% Windows display scale
    they match physical pixels. A higher scale makes the captured bitmap larger.

    Column widths stay as saved. Leave the splitters alone while shooting so
    every frame keeps the same split.

    For an automatic tour of the main screens, run Invoke-Screenshots.ps1.
    That script applies this layout, launches the app, and restores your settings.

    Settings live in %LocalAppData%\MaksIT\Postclient\settings.json on Windows.
    POSTCLIENT_CONFIG overrides that directory. Pass -SettingsPath for a portable copy.

.PARAMETER Width
    Window width. Default 1600.

.PARAMETER Height
    Window height. Default 900.

.PARAMETER FolderWidth
    Left folder pane. Default 248.

.PARAMETER ListWidth
    Message list width in the wide layout. Default 420.

.PARAMETER ListHeight
    Message list height in the stacked layout. Default 320.

.PARAMETER X
    Window left edge in screen pixels. Default 80.

.PARAMETER Y
    Window top edge in screen pixels. Default 48.

.PARAMETER SettingsPath
    settings.json to edit. Omit to use the profile path.

.PARAMETER Restore
    Put the backup back and remove it.

.EXAMPLE
    pwsh -File .\utils\tools\Set-ScreenshotLayout.ps1

.EXAMPLE
    pwsh -File .\utils\tools\Set-ScreenshotLayout.ps1 -Restore
#>

[CmdletBinding()]
param(
    [int]$Width = 1600,
    [int]$Height = 900,
    [int]$FolderWidth = 248,
    [int]$ListWidth = 420,
    [int]$ListHeight = 320,
    [int]$X = 80,
    [int]$Y = 48,
    [string]$SettingsPath,
    [switch]$Restore
)

$ErrorActionPreference = "Stop"

function Get-PostclientSettingsPath {
    if (-not [string]::IsNullOrWhiteSpace($SettingsPath)) {
        return $SettingsPath
    }

    if (-not [string]::IsNullOrWhiteSpace($env:POSTCLIENT_CONFIG)) {
        return (Join-Path $env:POSTCLIENT_CONFIG.Trim() "settings.json")
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

$settingsPath = Get-PostclientSettingsPath
$settingsDir = Split-Path $settingsPath -Parent
$backupPath = Join-Path $settingsDir "settings.screenshot-backup.json"

function Assert-AppClosed {
    $running = Get-Process -Name "MaksIT.PostClient.UI" -ErrorAction SilentlyContinue

    if ($running) {
        Write-Error "Close Postclient first. It overwrites settings.json when it exits."
    }
}

function Write-Settings([System.Text.Json.Nodes.JsonNode]$Root) {
    $options = [System.Text.Json.JsonSerializerOptions]::new()
    $options.WriteIndented = $true
    $json = $Root.ToJsonString($options) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($settingsPath, $json)
}

Assert-AppClosed

if ($Restore) {
    if (-not (Test-Path -LiteralPath $backupPath)) {
        Write-Error "No screenshot backup at $backupPath"
    }

    Copy-Item -LiteralPath $backupPath -Destination $settingsPath -Force
    Remove-Item -LiteralPath $backupPath -Force
    Write-Host "Restored $settingsPath"
    exit 0
}

if ($Width -lt 960 -or $Height -lt 640) {
    Write-Error "Window must be at least 960 by 640."
}

if ($FolderWidth -lt 160 -or $FolderWidth -gt 900) {
    Write-Error "Folder width must be from 160 to 900."
}

if ($ListWidth -lt 220 -or $ListWidth -gt 1600) {
    Write-Error "List width must be from 220 to 1600."
}

if ($ListHeight -lt 120 -or $ListHeight -gt 5000) {
    Write-Error "List height must be from 120 to 5000."
}

if (-not (Test-Path -LiteralPath $settingsPath)) {
    Write-Error "Settings file not found: $settingsPath. Start Postclient once, then close it and run this script."
}

if (-not (Test-Path -LiteralPath $backupPath)) {
    Copy-Item -LiteralPath $settingsPath -Destination $backupPath
    Write-Host "Backup: $backupPath"
}

$root = [System.Text.Json.Nodes.JsonNode]::Parse([System.IO.File]::ReadAllText($settingsPath))
$config = $root["Configuration"]

if ($null -eq $config) {
    Write-Error "settings.json has no Configuration object."
}

$configuration = $config.AsObject()

if ($null -eq $configuration["Layout"]) {
    $configuration["Layout"] = [System.Text.Json.Nodes.JsonObject]::new()
}

$layout = $configuration["Layout"].AsObject()
$layout["WindowWidth"] = [System.Text.Json.Nodes.JsonValue]::Create([double]$Width)
$layout["WindowHeight"] = [System.Text.Json.Nodes.JsonValue]::Create([double]$Height)
$layout["WindowX"] = [System.Text.Json.Nodes.JsonValue]::Create($X)
$layout["WindowY"] = [System.Text.Json.Nodes.JsonValue]::Create($Y)
$layout["WindowState"] = [System.Text.Json.Nodes.JsonValue]::Create("Normal")
$layout["FolderWidth"] = [System.Text.Json.Nodes.JsonValue]::Create([double]$FolderWidth)
$layout["ListWidth"] = [System.Text.Json.Nodes.JsonValue]::Create([double]$ListWidth)
$layout["ListHeight"] = [System.Text.Json.Nodes.JsonValue]::Create([double]$ListHeight)

Write-Settings $root

Write-Host "Screenshot layout written to $settingsPath"
Write-Host "  Window ${Width}x${Height} at ${X},${Y} (Normal)"
Write-Host "  Folders $FolderWidth, list width $ListWidth, list height $ListHeight"
Write-Host "Start Postclient and take the shots. Do not resize the window or the splitters."
Write-Host "When you are done, close the app and run this script with -Restore."
