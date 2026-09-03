# Baut den Rechtschreib-Trainer, kopiert ihn an einen festen Ort und
# richtet den Autostart mit Windows ein.
#
#   powershell -ExecutionPolicy Bypass -File scripts\install.ps1
#
# Deinstallieren:  scripts\install.ps1 -Uninstall

param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$repo    = Split-Path $PSScriptRoot -Parent
$target  = Join-Path $env:LOCALAPPDATA 'RechtschreibTrainer'
$exe     = Join-Path $target 'RechtschreibTrainer.exe'
$startup = [Environment]::GetFolderPath('Startup')
$lnk     = Join-Path $startup 'Rechtschreib-Trainer.lnk'

function Remove-Autostart {
    Get-Process RechtschreibTrainer -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-Item $lnk -ErrorAction SilentlyContinue
    Write-Host "Autostart-Verknuepfung entfernt."
}

if ($Uninstall) {
    Remove-Autostart
    Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Programm entfernt aus $target"
    Write-Host "Deine Daten in Dokumente\RechtschreibTrainer bleiben erhalten."
    return
}

Write-Host "Baue Release ..."
& dotnet publish (Join-Path $repo 'src\RechtschreibTrainer\RechtschreibTrainer.csproj') `
    -c Release -r win-x64 --self-contained false -o $target | Out-Null

if (-not (Test-Path $exe)) { throw "Build fehlgeschlagen: $exe nicht gefunden." }

Get-Process RechtschreibTrainer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1

$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = $target
$sc.Description = 'Rechtschreib-Trainer - Live-Korrektur'
$sc.Save()

Start-Process $exe
Write-Host ""
Write-Host "Fertig."
Write-Host "  Programm:  $exe"
Write-Host "  Autostart: $lnk"
Write-Host "Der Rechtschreib-Trainer laeuft jetzt und startet ab sofort mit Windows."
