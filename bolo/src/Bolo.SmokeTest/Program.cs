using System.Diagnostics;
using Bolo.Core.Learning;
using Whisper.net;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

// Bolo smoke tests.
// Usage: Bolo.SmokeTest <model.bin> <audio.wav> [cpu]   — engine test (Phase 0)
//        Bolo.SmokeTest selftest                        — learning-layer + speech-gate checks
//        Bolo.SmokeTest vad <ggml-silero.bin> <audio.wav> — VAD check: speech in the clip, none in 2 s of silence (v1.3.0)

if (args.Length == 1 && args[0] == "selftest")
    return RunSelfTest();

if (args.Length == 3 && args[0] == "vad")
    return await RunVadCheck(args[1], args[2]);

if (args.Length < 2)
{
    Console.WriteLine("Usage: Bolo.SmokeTest <model.bin> <audio.wav> [cpu] | selftest");
    return 1;
}

static int RunSelfTest()
{
    var failures = 0;
    void Check(string name, bool ok)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}");
        if (!ok) failures++;
    }

    // Whole-word, case-insensitive replacement.
    Check("replace basic",
        SubstitutionEngine.Replace("the fukan backend", "fukan", "Vulkan") == "the Vulkan backend");
    Check("replace is whole-word only",
        SubstitutionEngine.Replace("scanning", "can", "could") == "scanning");
    Check("replace multi-word phrase",
        SubstitutionEngine.Replace("open the twelve or studios site", "twelve or studios", "Twelver Studios")
            == "open the Twelver Studios site");

    // Correction harvesting from an edit.
    var pairs = SubstitutionEngine.ExtractCorrections(
        "It runs on the radian 6950 XT using the Fukan backend.",
        "It runs on the Radeon 6950 XT using the Vulkan backend.");
    Check("diff finds two corrections", pairs.Count == 2);
    Check("diff pair 1", pairs.Count > 0 && pairs[0] == ("radian", "Radeon"));
    Check("diff pair 2", pairs.Count > 1 && pairs[1] == ("Fukan", "Vulkan"));

    // Identical text produces nothing.
    Check("diff no-op", SubstitutionEngine.ExtractCorrections("same text here", "same text here").Count == 0);

    // Pure insertion is ignored (edit of intent, not a mishearing).
    Check("diff ignores insertion",
        SubstitutionEngine.ExtractCorrections("send the report", "send the full report").Count == 0);

    // Spelled-out acronym collapse.
    var (collapsed1, found1) = Transforms.CollapseSpelledAcronyms("I applied through I R C C yesterday.");
    Check("acronym collapse", collapsed1 == "I applied through IRCC yesterday." && found1.SequenceEqual(["IRCC"]));

    var (collapsed2, found2) = Transforms.CollapseSpelledAcronyms("Use the A-P-I key.");
    Check("acronym collapse hyphens", collapsed2 == "Use the API key." && found2.SequenceEqual(["API"]));

    var (collapsed3, found3) = Transforms.CollapseSpelledAcronyms("This is a normal sentence.");
    Check("acronym no false positive", collapsed3 == "This is a normal sentence." && found3.Count == 0);

    var (_, found4) = Transforms.CollapseSpelledAcronyms("Say A B only.");
    Check("acronym needs 3+ letters", found4.Count == 0);

    // '$' in an expansion must be literal, never a regex group reference.
    Check("replace with dollar sign",
        SubstitutionEngine.Replace("the price is my price", "my price", "$100") == "the price is $100");
    Check("replace with $0 pattern",
        SubstitutionEngine.Replace("send my rate", "my rate", "$0.50/word") == "send $0.50/word");

    // Speech gate on top of the VAD (v1.3.0): the decision rule, pure and pinned.
    Check("speech seconds sums segments",
        Math.Abs(Bolo.Core.Engine.SpeechGate.SpeechSeconds([(TimeSpan.Zero, TimeSpan.FromSeconds(0.5)), (TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.4))]) - 0.9) < 1e-9);
    Check("speech gate passes without a VAD", Bolo.Core.Engine.SpeechGate.HasSpeech(null));
    Check("speech gate rejects a silent take", !Bolo.Core.Engine.SpeechGate.HasSpeech(0.1));
    Check("speech gate accepts a short word", Bolo.Core.Engine.SpeechGate.HasSpeech(0.3));

    Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");
    return failures == 0 ? 0 : 1;
}

static async Task<int> RunVadCheck(string vadModel, string wavPath)
{
    using var engine = new Bolo.Core.Engine.WhisperTranscriber();
    engine.LoadVad(vadModel);
    using var clip = File.OpenRead(wavPath);
    var speech = await engine.DetectSpeechSecondsAsync(clip);
    var clipSeconds = (new FileInfo(wavPath).Length - 44.0) / 32000;
    Console.WriteLine($"clip: {clipSeconds:F1} s audio, {speech:F2} s speech -> {(Bolo.Core.Engine.SpeechGate.HasSpeech(speech) ? "speech" : "no speech")}");
    // Two seconds of digital silence as a 16 kHz mono 16-bit WAV, built in memory.
    var silence = new MemoryStream();
    using (var w = new BinaryWriter(silence, System.Text.Encoding.ASCII, leaveOpen: true))
    {
        var data = 16000 * 2 * 2;
        w.Write("RIFF"u8); w.Write(36 + data); w.Write("WAVE"u8); w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(16000); w.Write(32000); w.Write((short)2); w.Write((short)16); w.Write("data"u8); w.Write(data); w.Write(new byte[data]);
    }
    silence.Position = 0;
    var silent = await engine.DetectSpeechSecondsAsync(silence);
    Console.WriteLine($"silence: 2.0 s audio, {silent:F2} s speech -> {(Bolo.Core.Engine.SpeechGate.HasSpeech(silent) ? "speech" : "no speech")}");
    var ok = Bolo.Core.Engine.SpeechGate.HasSpeech(speech) && !Bolo.Core.Engine.SpeechGate.HasSpeech(silent);
    Console.WriteLine(ok ? "VAD PASS" : "VAD FAIL");
    return ok ? 0 : 1;
}

var modelPath = args[0];
var wavPath = args[1];
var forceCpu = args.Length > 2 && args[2].Equals("cpu", StringComparison.OrdinalIgnoreCase);

RuntimeOptions.RuntimeLibraryOrder = forceCpu
    ? [RuntimeLibrary.Cpu]
    : [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];

// Surface native whisper.cpp logs — this is where Vulkan device enumeration/selection shows up.
using var logger = LogProvider.AddConsoleLogging(minLevel: WhisperLogLevel.Info);

Console.WriteLine($"Model: {modelPath}");
Console.WriteLine($"Audio: {wavPath}");
Console.WriteLine($"Requested runtime order: {(forceCpu ? "CPU only" : "Vulkan -> CPU")}");
Console.WriteLine(new string('-', 60));

var loadTimer = Stopwatch.StartNew();
using var factory = WhisperFactory.FromPath(modelPath);
using var processor = factory.CreateBuilder()
    .WithLanguage("en")
    .Build();
loadTimer.Stop();
Console.WriteLine($"Loaded runtime: {RuntimeOptions.LoadedLibrary}");
Console.WriteLine($"Model load time: {loadTimer.Elapsed.TotalSeconds:F2}s");
Console.WriteLine(new string('-', 60));

using var wavStream = File.OpenRead(wavPath);
var transcribeTimer = Stopwatch.StartNew();
var transcript = new List<string>();
await foreach (var segment in processor.ProcessAsync(wavStream))
{
    transcript.Add(segment.Text);
    Console.WriteLine($"[{segment.Start:mm\\:ss\\.ff} -> {segment.End:mm\\:ss\\.ff}] {segment.Text}");
}
transcribeTimer.Stop();

Console.WriteLine(new string('-', 60));
Console.WriteLine($"Transcription time: {transcribeTimer.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"Full text: {string.Concat(transcript).Trim()}");
return 0;
