# Builds the shareable, self-contained Bolo app into publish\Bolo\ next to this script.
# Recipients need no .NET install; they just run Bolo.exe.
# Requires the .NET 8 SDK on the build machine.

param([switch]$Minor)
$ErrorActionPreference = 'Stop'
$src = Join-Path $PSScriptRoot 'src'
$out = Join-Path $PSScriptRoot 'publish\Bolo'

# Every release wave gets a new patch version automatically (1.1.0 -> 1.1.1 -> ...).
# A UI overhaul or feature wave is a minor bump: run publish.ps1 -Minor (1.1.3 -> 1.2.0). Major bumps stay manual.
$csproj = Join-Path $src 'Bolo.App\Bolo.App.csproj'
$projText = Get-Content $csproj -Raw
if ($projText -match '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    $newVersion = if ($Minor) { "$($Matches[1]).$([int]$Matches[2] + 1).0" } else { "$($Matches[1]).$($Matches[2]).$([int]$Matches[3] + 1)" }
    $projText -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$newVersion</Version>" |
        Set-Content $csproj -Encoding utf8 -NoNewline
    Write-Host "Version bumped to $newVersion"
}

dotnet publish (Join-Path $src 'Bolo.App') -c Release -r win-x64 --self-contained -o $out
if ($LASTEXITCODE -ne 0) {
    Write-Host "PUBLISH FAILED - nothing was released."
    exit 1
}

# Keep the README's version line in lockstep with the built exe — no manual bumps.
$exeVersion = (Get-Item (Join-Path $out 'Bolo.exe')).VersionInfo.FileVersion
$shortVersion = ($exeVersion -split '\.')[0..1] -join '.'
$readme = Join-Path $PSScriptRoot 'README.md'
(Get-Content $readme -Raw) -replace '\*\*Version [\d\.]+\*\*', "**Version $shortVersion**" |
    Set-Content $readme -Encoding utf8 -NoNewline

Write-Host ""
Write-Host "Published to $out (v$shortVersion, README stamped to match)"
Write-Host "Share this folder (README.md, INSTALL.md, download-model.ps1, publish\Bolo\)."
Write-Host "The model is NOT included; recipients run download-model.ps1 once."
