# Bolo Setup Guide

Time required: about 10 minutes, most of it waiting for the model download.

## Step 1: Get the app

**Option A: prebuilt release.** If you received a `Bolo` folder or downloaded a release zip, extract it anywhere you like (for example `C:\Program Files\Bolo` or a folder on your desktop). No installer needs to run.

The app itself is **`Bolo\Bolo.exe`** (the other files in that folder are its libraries). Step 4 below creates a desktop shortcut so you never have to find it manually, and launching Bolo again while it is already running simply opens its window.

**Option B: build from source.** Requires the .NET 8 SDK.

```
cd src
dotnet publish Bolo.App -c Release -r win-x64 --self-contained -o "..\publish\Bolo"
```

## Step 2: Download the speech model

The speech model is a single file that is downloaded once. After this, Bolo is fully offline.

Right-click `download-model.ps1` in this folder and choose "Run with PowerShell". It downloads the recommended model (large-v3-turbo, about 1.6 GB) and the small voice-activity model (about 1 MB) into a `models` folder next to `Bolo.exe`.

To do it manually instead: download `ggml-large-v3-turbo.bin` from
`https://huggingface.co/ggerganov/whisper.cpp/tree/main`
and place it in a folder named `models` next to `Bolo.exe`.

Smaller and faster alternatives (worth trying if you have no GPU):

| Model file | Size | Notes |
|---|---|---|
| ggml-large-v3-turbo.bin | 1.6 GB | Recommended. Near-best accuracy, fast on GPU |
| ggml-medium.en.bin | 1.5 GB | English only |
| ggml-small.en.bin | 466 MB | Good CPU choice |
| ggml-base.en.bin | 142 MB | Fastest, lower accuracy |

You can put several models in the folder and switch between them in Settings.

## Step 3: First run

1. Launch `Bolo.exe`. A dark tile with a gold "B" appears in the system tray (check the tray overflow area behind the ^ arrow; you can drag the icon onto the taskbar to pin it).
2. The onboarding window opens:
   - Pick your microphone and run the 2-second level test.
   - Seed your dictionary with names, acronyms, and jargon you use often. You can add more later.
   - Try it: click in the practice box, hold the hotkey, say something, release. The words appear in the box.
3. Wait for the "Bolo ready" notification. The first model load takes a few seconds.

## Step 4: Desktop shortcut (optional, recommended)

Right-click `create-shortcut.ps1` in this folder and choose "Run with PowerShell". It puts a Bolo shortcut on your desktop so you never have to dig into the app folder to find `Bolo.exe`.

To pin Bolo to the taskbar or Start menu, right-click that new desktop shortcut and choose "Pin to taskbar" or "Pin to Start".

Tip: once Bolo is running, you rarely need to launch it again. It lives in the system tray (the dark tile with a gold B; the tile turns red while Bolo is listening). Double-click the tray icon to open the main window.

## Step 5: Dictate

1. Click into any text field.
2. Press and hold **F9** (the default hotkey), speak, and release.
3. Your words appear in the overlay while you speak, and the final text is typed into the field when you release.

Prefer tapping instead of holding? Open the main window (double-click the tray icon), go to Settings, and switch the hotkey mode to Toggle. You can also rebind the hotkey itself there: click the hotkey box and press the combination you want. Modifier-only combos work too (for example hold Ctrl+Shift together and release to bind them); a short activation delay keeps normal shortcuts like Ctrl+Shift+S in other apps from triggering dictation.

## The main window

Double-click the tray icon to open it.

- **Home**: start/stop button and usage stats (a single click on the tray icon shows your last three transcripts without opening the window)
- **History**: every transcript, with copy and "Edit & learn" (fixing a transcript teaches Bolo)
- **Dictionary**: your vocabulary clusters
- **Snippets**: trigger phrase to expansion text
- **Insights**: usage trends and most-corrected words
- **Style**: optional transforms (filler removal, spoken lists to bullets, auto-learn toggle)
- **Settings**: hotkey, microphone, injection method, model, overlay, theme and accent, reset

## Where your data lives

Everything Bolo knows is in `%APPDATA%\Bolo` as plain JSON files: settings, transcript history, dictionary, corrections, and snippets. Audio is never stored.

## Uninstall

1. Exit Bolo from the tray menu.
2. Delete the folder containing `Bolo.exe`.
3. Delete `%APPDATA%\Bolo` if you also want to remove your history and dictionary.

Nothing else is left behind. Bolo does not touch the registry or install services.

## Troubleshooting

- **Hotkey does nothing.** Another app may own that combination. Pick a different one in Settings; the hotkey box tells you if binding fails.
- **Transcription is slow.** Check Settings to confirm the engine says Vulkan. If it says CPU, switch to a smaller model, or update your graphics drivers and restart Bolo.
- **Text lands in the wrong place.** The text goes to whatever window has focus when transcription finishes. Click into your target field before or while speaking.
- **Wrong microphone.** Change it in Settings under Microphone.
- **Diagnostics.** A log of app events (no transcript content beyond lengths) is written to `%APPDATA%\Bolo\bolo.log`.
