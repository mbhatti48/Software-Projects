# Bolo

**Version 1.1.3**

Bolo is a free, fully offline speech-to-text app for Windows. Hold a hotkey, speak, and your words are typed directly into whatever application has focus: Word, your browser, Slack, anywhere you can type. No cloud, no subscription, no account, no telemetry.

The name comes from the Urdu word "bolo", meaning "speak".

## Why Bolo exists

Bolo is deliberately simple, and it will keep growing over time. It is free and always will be — no subscription, no account, no paywall. Good dictation shouldn't be locked behind a monthly fee, and everything Bolo learns stays on your own machine. If you want to make it better, the [Training Bolo with an AI assistant](#training-bolo-with-an-ai-assistant) section shows how to improve your own copy with an AI assistant — or you can fork it and build your own version (see [Contributing](#contributing)).

## Features

- **System-wide dictation.** A global hotkey works from any application. Text is typed straight into the focused window. Choose hold-to-talk or tap-to-toggle.
- **Live preview.** A small overlay shows what Bolo is hearing while you speak, then the final text is injected when you stop.
- **Runs on your GPU.** Uses whisper.cpp with the Vulkan backend, so it works on AMD, NVIDIA, and Intel graphics cards. Falls back to CPU automatically.
- **Learns your vocabulary.** Add names, acronyms, and jargon to a personal dictionary, organized into clusters you can switch on and off. Enabled terms bias the speech recognition toward hearing them correctly.
- **Learns from corrections.** Edit any transcript in the History page and Bolo remembers the fix. The same mistake gets corrected automatically from then on.
- **Auto-learn.** Spell a word out loud ("I R C C") and Bolo types the acronym and adds it to the dictionary. You can also say "add to dictionary: [word]" as a voice command.
- **Snippets.** Say a trigger phrase and Bolo types the expansion. For example "my email" can expand to your full address.
- **Insights.** Words per day, average words per minute, day streak, and your most-corrected words. All computed locally from your own history.
- **Style transforms.** Optional rule-based cleanups: filler word removal and spoken-list-to-bullet-list conversion. No AI rewriting is involved.
- **Full control.** Settings for hotkey binding and mode, microphone, model size, GPU/CPU preference, overlay position/size/opacity, and light/dark theme.

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

Everything Bolo learns is stored as plain, readable JSON in `%APPDATA%\Bolo` on your machine. That is deliberate: it means any AI assistant that can read files can act as Bolo's "trainer" without any cloud service or model retraining being involved. Whisper itself is never touched — all of Bolo's learning is text-level, so this process cannot damage the speech model.

### The copy-paste prompt

Open an AI assistant that can read local files — **Claude Code, Cursor, or the Claude desktop app with folder access all work well** (use the strongest model available to you, such as Claude Opus, for the best analysis). If your assistant can't read files directly, open the JSON files yourself and paste their contents into the chat instead.

Then give it this prompt:

```
You are helping me improve Bolo, my offline speech-to-text app. Bolo stores everything
it has learned as plain JSON in the folder %APPDATA%\Bolo on this machine. Nothing in
that folder ever leaves my computer, and I'm only asking you to work inside it.

Please read these files in %APPDATA%\Bolo:
  - vocabulary.json   — my dictionary clusters (terms that bias transcription)
  - corrections.json  — wrong -> right fixes Bolo auto-applies to every transcript
  - snippets.json     — trigger phrase -> expansion text
  - the history/ folder — my recent transcripts, one JSON file per day

Then analyse them together and propose:
  1. New dictionary terms — names, acronyms, or jargon that recur in my transcripts or
     corrections but aren't in vocabulary.json yet. Suggest which cluster each belongs in.
  2. Junk to prune — nonsense, misheard fragments, or one-off entries in the
     "Auto-learned" cluster that should be removed or fixed.
  3. Risky or stale corrections — correction pairs that could misfire on ordinary words
     (e.g. replacing a common word), or that I no longer seem to need.
  4. Snippet candidates — phrases I dictate often that would be worth turning into a
     snippet with a short trigger.

Show me all of your proposals as a numbered list first, and wait for my approval.
For anything I approve, edit the relevant JSON file directly, keeping the exact existing
format and valid JSON. Do NOT change settings.json, and do NOT modify the speech model —
all of Bolo's learning is text-level. When you've applied the approved edits, remind me
to restart Bolo so they take effect.
```

Review the proposals, approve the ones you want, let it edit the JSON, and restart Bolo. The files are small and yours: nothing leaves your machine unless you choose to share it, and the assistant only needs read and write access to that one folder.

## Contributing

Bolo is open to the community. You're free to fork it and build your own version — make it do whatever you like. If you'd like to contribute back, open a pull request and I'll review and merge good changes; bug reports and ideas are welcome as issues too. The goal is to keep Bolo simple, free, and useful.

## License

Bolo is released under the **GNU General Public License v3.0** (see [LICENSE](LICENSE)). In plain terms: it's free to use, study, modify, and share — and any version you distribute must also stay open-source under the same license. That's deliberate: it keeps Bolo, and anything built from it, free for everyone. Nobody can take it closed-source or lock it behind a subscription.

Copyright (C) 2026 EmAyeBee.

## Built with

- [whisper.cpp](https://github.com/ggml-org/whisper.cpp) via [Whisper.net](https://github.com/sandrohanea/whisper.net) for speech recognition
- [NAudio](https://github.com/naudio/NAudio) for audio capture
- .NET 8 / WPF for the desktop shell

## Credits

Made by EmAyeBee.
