# Downloads the recommended Bolo speech model (one-time, ~1.6 GB).
# Run from the folder containing Bolo.exe, or from the folder next to it that contains this script.

$ErrorActionPreference = 'Stop'
$model = 'ggml-large-v3-turbo.bin'
$url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$model"

# Put the model next to Bolo.exe if we can find it, otherwise next to this script.
$candidates = @(
    (Join-Path $PSScriptRoot 'Bolo'),
    $PSScriptRoot
)
$target = $candidates | Where-Object { Test-Path (Join-Path $_ 'Bolo.exe') } | Select-Object -First 1
if (-not $target) { $target = $PSScriptRoot }
$modelsDir = Join-Path $target 'models'
New-Item -ItemType Directory -Force $modelsDir | Out-Null
$dest = Join-Path $modelsDir $model
# Since v1.3.0 Bolo also wants the small Silero voice-activity model beside it (about 1 MB).
$vadModel = "ggml-silero-v6.2.0.bin"
$vadUrl = "https://huggingface.co/sandrohanea/whisper.net/resolve/v5/vad/$vadModel"
$vadDest = Join-Path $modelsDir $vadModel
if (-not (Test-Path $vadDest)) { Invoke-WebRequest -Uri $vadUrl -OutFile $vadDest; Write-Host "Voice-activity model saved to $vadDest" }

if (Test-Path $dest) {
    Write-Host "Model already present: $dest"
    exit 0
}

Write-Host "Downloading $model (about 1.6 GB) to $modelsDir ..."
Write-Host "This is a one-time download. Bolo runs fully offline afterwards."
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest -Uri $url -OutFile $dest
Write-Host "Done. Launch Bolo.exe to get started."
