# Creates a desktop shortcut for Bolo.
# Run this from the folder that contains the Bolo app folder (right-click > Run with PowerShell).

$ErrorActionPreference = 'Stop'

$exe = Join-Path $PSScriptRoot 'Bolo\Bolo.exe'
if (-not (Test-Path $exe)) {
    # Also handle being placed directly next to Bolo.exe.
    $exe = Join-Path $PSScriptRoot 'Bolo.exe'
}
if (-not (Test-Path $exe)) {
    Write-Host "Could not find Bolo.exe next to this script. Move this script into the folder that contains the Bolo app folder and run it again."
    exit 1
}

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut("$([Environment]::GetFolderPath('Desktop'))\Bolo.lnk")
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = Split-Path $exe
$lnk.IconLocation = "$exe,0"
$lnk.Description = 'Bolo - offline dictation'
$lnk.Save()

Write-Host "Desktop shortcut created."
Write-Host ""
Write-Host "To pin Bolo to the taskbar or Start menu: right-click the new desktop"
Write-Host "shortcut and choose 'Pin to taskbar' or 'Pin to Start'."
