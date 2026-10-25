# Bolo

**Version 1.3**

Bolo is a free, fully offline speech-to-text app for Windows. Hold a hotkey, speak, and your words are typed directly into whatever application has focus: Word, your browser, Slack, anywhere you can type. No cloud, no subscription, no account, no telemetry.

The name comes from the Urdu word "bolo", meaning "speak".

## Features

- **System-wide dictation.** A global hotkey works from any application. Text is typed straight into the focused window. Choose hold-to-talk or tap-to-toggle.
- **Live preview.** A small overlay shows what Bolo is hearing while you speak, then the final text is injected when you stop. Press Esc during a take to throw it away.
- **Knows silence from speech.** A small voice-activity model checks every take before it reaches the speech model, so an accidental tap or a quiet room produces nothing instead of invented words.
- **Runs on your GPU.** Uses whisper.cpp with the Vulkan backend, so it works on AMD, NVIDIA, and Intel graphics cards. Falls back to CPU automatically.
- **Learns your vocabulary.** Add names, acronyms, and jargon to a personal dictionary, organized into clusters you can switch on and off. Enabled terms bias the speech recognition toward hearing them correctly.
- **Learns from corrections.** Edit any transcript in the History page and Bolo remembers the fix. The same mistake gets corrected automatically from then on.
- **Auto-learn.** Spell a word out loud ("I R C C") and Bolo types the acronym and adds it to the dictionary. You can also say "add to dictionary: [word]" as a voice command.
- **Snippets.** Say a trigger phrase and Bolo types the expansion. For example "my email" can expand to your full address.
- **Insights.** Words per day, average words per minute, day streak, and your most-corrected words. All computed locally from your own history.
- **Style transforms.** Optional rule-based cleanups: filler word removal and spoken-list-to-bullet-list conversion. No AI rewriting is involved.
- **Full control.** Settings for hotkey binding and mode, microphone, model size, GPU/CPU preference, overlay position/size/opacity, light/dark theme and a gold or red accent.
- **A proper desktop app.** Dark and light themes with the same care, a tray icon whose tile turns red while you speak, a floating pill with live level bars and the words as they arrive, and a window that stays out of your way until you want your history or insights.

## Privacy

- Audio is processed in memory and discarded. It is never written to disk.
- Only text is stored: transcripts, corrections, vocabulary, and settings, all as readable JSON in `%APPDATA%\Bolo` on your own machine.
- The app makes no network calls. The only download is the speech model itself, fetched once during setup.

## Requirements

- Windows 10 or 11, 64-bit
- About 2 GB of disk space for the default speech model
- A GPU with Vulkan support is recommended for fast transcription. Any reasonably modern AMD, NVIDIA, or Intel GPU qualifies. Without one, Bolo runs on CPU; a smaller model is recommended in that case.

## Getting started

See [INSTALL.md](INSTALL.md) for the setup guide.

## Getting the most out of it

1. During onboarding, seed the dictionary with names and terms you use often.
2. When Bolo mishears something, open History and use "Edit & learn" to fix it. This is how Bolo improves.
3. Organize your dictionary into clusters (work, technical, personal) and toggle them based on context.
4. Every few months, run an AI review of your data (see the next section). This is Bolo's most powerful improvement loop.

## Training Bolo with an AI assistant

Everything Bolo learns is stored as plain, readable JSON in `%APPDATA%\Bolo` on your machine. That is deliberate: it means any AI assistant that can read files can act as Bolo's "trainer" without any cloud service or model retraining being involved.

Every few months:

1. Open an AI coding assistant that can read local files (Claude Code works well; use the strongest model available to you, such as Opus, for the best analysis).
2. Point it at `%APPDATA%\Bolo` and ask it to review `corrections.json`, `vocabulary.json`, `snippets.json`, and the `history` folder, then propose:
   - new dictionary terms that keep appearing in your transcripts or corrections,
   - junk entries in the Auto-learned cluster to prune or fix,
   - correction pairs that look risky or stale,
   - repeated phrases that deserve to become snippets.
3. Review the proposals, let it edit the JSON files you approve, and restart Bolo.

The files are small and yours: nothing leaves your machine unless you choose to share it, and the assistant only needs read and write access to that one folder. Whisper itself is never modified; all learning is text-level, so this process cannot damage the speech model.

## Built with

- [whisper.cpp](https://github.com/ggml-org/whisper.cpp) via [Whisper.net](https://github.com/sandrohanea/whisper.net) for speech recognition
- [NAudio](https://github.com/naudio/NAudio) for audio capture
- .NET 8 / WPF for the desktop shell

## Credits

Made by EmAyeBee.
