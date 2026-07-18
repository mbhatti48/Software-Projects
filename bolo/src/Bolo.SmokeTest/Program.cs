using System.Diagnostics;
using Bolo.Core.Learning;
using Whisper.net;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

// Bolo smoke tests.
// Usage: Bolo.SmokeTest <model.bin> <audio.wav> [cpu]   — engine test (Phase 0)
//        Bolo.SmokeTest selftest                        — learning-layer checks (Phase 2)

if (args.Length == 1 && args[0] == "selftest")
    return RunSelfTest();

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

    Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");
    return failures == 0 ? 0 : 1;
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
