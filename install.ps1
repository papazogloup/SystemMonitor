# install.ps1 - System Monitor Installer
# Run as Administrator!
# Installs SystemMonitor to Program Files and sets up auto-start via Task Scheduler

param(
    [string]$InstallPath = "C:\Program Files\SystemMonitor",
    [string]$TaskName = "SystemMonitor",
    [string]$Username = $env:USERNAME
)

# Check admin
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Run this script as Administrator!"
    exit 1
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceExe = Join-Path $ScriptDir "publish\win-x64\SystemMonitor.exe"

if (-not (Test-Path $SourceExe)) {
    Write-Error "SystemMonitor.exe not found at: $SourceExe"
    Write-Host "Run 'dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\win-x64' first."
    exit 1
}

Write-Host "Installing System Monitor..." -ForegroundColor Cyan

# 1. Create install directory
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# 2. Copy exe
$DestExe = Join-Path $InstallPath "SystemMonitor.exe"
Copy-Item $SourceExe $DestExe -Force
Write-Host "  Copied to: $DestExe" -ForegroundColor Green

# 3. Create Task Scheduler task only if it doesn't already exist
$existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue

if ($existingTask) {
    Write-Host "  Task Scheduler task '$TaskName' already exists - skipping." -ForegroundColor Yellow
} else {
    $Action   = New-ScheduledTaskAction -Execute $DestExe -WorkingDirectory $InstallPath
    $Trigger  = New-ScheduledTaskTrigger -AtLogOn -User $Username
    $Settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit 0 `
        -MultipleInstances IgnoreNew

    $Principal = New-ScheduledTaskPrincipal `
        -UserId $Username `
        -LogonType Interactive `
        -RunLevel Highest   # "Run with highest privileges" = no UAC popup

    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $Action `
        -Trigger $Trigger `
        -Settings $Settings `
        -Principal $Principal `
        -Description "System Monitor - CPU/RAM/Network/Temperature tray utility" | Out-Null

    Write-Host "  Task Scheduler task created: '$TaskName'" -ForegroundColor Green
}
Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host "SystemMonitor will start automatically at next login (no UAC prompt)." -ForegroundColor Cyan
Write-Host ""
Write-Host "To start now:" -ForegroundColor Yellow
Write-Host "  Start-ScheduledTask -TaskName '$TaskName'"
Write-Host "  -- or --"
Write-Host "  Start-Process '$DestExe'"
