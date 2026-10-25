using System.Diagnostics;
using System.Text;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Bolo.Core.Engine;

/// <summary>
/// Keeps a whisper.cpp model resident in memory (Vulkan GPU when available, CPU otherwise)
/// and transcribes 16kHz mono WAV streams. Audio only ever exists in memory — this class
/// never writes it anywhere. Since v1.3.0 it also hosts the Silero VAD (voice activity
/// detector) that ships with Whisper.net: a 1 MB model that says where speech is, used to
/// drop silent takes before Whisper can hallucinate on them.
/// </summary>
public sealed class WhisperTranscriber : IDisposable
{
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private WhisperVadFactory? _vadFactory;
    private WhisperVadProcessor? _vad;

    // A WhisperProcessor cannot run two inferences at once; live partial transcription
    // and the final pass must take turns. The VAD has its own gate for the same reason.
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);
    private readonly SemaphoreSlim _vadGate = new(1, 1);

    public bool IsLoaded => _processor is not null;
    public bool HasVad => _vad is not null;
    public string LoadedRuntime { get; private set; } = "none";
    public TimeSpan LoadTime { get; private set; }

    /// <summary>
    /// Runtime order must be set before the first native library load in the process;
    /// switching between GPU and CPU afterwards requires an app restart.
    /// </summary>
    public static void ConfigureRuntime(bool forceCpu)
    {
        RuntimeOptions.RuntimeLibraryOrder = forceCpu
            ? [RuntimeLibrary.Cpu]
            : [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];
    }

    private string _language = "en";

    public void Load(string modelPath, string language, string prompt = "")
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Whisper model not found: {modelPath}");

        Unload();
        _language = language;
        var timer = Stopwatch.StartNew();
        _factory = WhisperFactory.FromPath(modelPath);
        _processor = BuildProcessor(prompt);
        timer.Stop();
        LoadTime = timer.Elapsed;
        LoadedRuntime = RuntimeOptions.LoadedLibrary?.ToString() ?? "unknown";
    }

    /// <summary>Loads the Silero VAD model (ggml-silero-*.bin). Optional: without it the duration gate alone applies.</summary>
    public void LoadVad(string vadModelPath)
    {
        if (!File.Exists(vadModelPath))
            throw new FileNotFoundException($"VAD model not found: {vadModelPath}");
        _vad?.Dispose();
        _vadFactory?.Dispose();
        _vadFactory = WhisperVadFactory.FromPath(vadModelPath);
        _vad = _vadFactory.CreateBuilder()
            .WithThreshold(0.5f)
            .WithMinSpeechDuration(TimeSpan.FromMilliseconds(250))
            .WithMinSilenceDuration(TimeSpan.FromMilliseconds(100))
            .Build();
    }

    /// <summary>Rebuilds the processor with a new vocabulary prompt — the model stays loaded.</summary>
    public async Task RepromptAsync(string prompt)
    {
        if (_factory is null)
            return;
        await _inferenceGate.WaitAsync();
        try
        {
            _processor?.Dispose();
            _processor = BuildProcessor(prompt);
        }
        finally
        {
            _inferenceGate.Release();
        }
    }

    private WhisperProcessor BuildProcessor(string prompt)
    {
        var builder = _factory!.CreateBuilder()
            .WithLanguage(_language)
            // Hallucination guard, made explicit (these are whisper.cpp's own defaults): a
            // segment whose no-speech probability passes 0.6 while its average log
            // probability falls under -1.0 is treated as silence instead of text.
            .WithNoSpeechThreshold(0.6f)
            .WithLogProbThreshold(-1.0f);
        if (!string.IsNullOrWhiteSpace(prompt))
            builder = builder.WithPrompt(prompt); // vocabulary bias — text-level, no retraining
        return builder.Build();
    }

    public async Task<string> TranscribeAsync(Stream wavStream, CancellationToken ct = default)
    {
        if (_processor is null)
            throw new InvalidOperationException("Model not loaded.");

        await _inferenceGate.WaitAsync(ct);
        try
        {
            var text = new StringBuilder();
            await foreach (var segment in _processor.ProcessAsync(wavStream, ct))
                text.Append(segment.Text);
            return text.ToString().Trim();
        }
        finally
        {
            _inferenceGate.Release();
        }
    }

    /// <summary>
    /// Seconds of detected speech in a 16kHz mono WAV stream, or null when no VAD model is
    /// loaded. The stream is left at position 0 so the caller can transcribe it next.
    /// </summary>
    public async Task<double?> DetectSpeechSecondsAsync(Stream wavStream, CancellationToken ct = default)
    {
        if (_vad is null)
            return null;
        await _vadGate.WaitAsync(ct);
        try
        {
            wavStream.Position = 0;
            var segments = await _vad.DetectSpeechAsync(wavStream, ct);
            return SpeechGate.SpeechSeconds(segments.Select(s => (s.Start, s.End)));
        }
        finally
        {
            if (wavStream.CanSeek)
                wavStream.Position = 0;
            _vadGate.Release();
        }
    }

    public void Unload()
    {
        _processor?.Dispose();
        _processor = null;
        _factory?.Dispose();
        _factory = null;
    }

    public void Dispose()
    {
        Unload();
        _vad?.Dispose();
        _vadFactory?.Dispose();
    }
}

/// <summary>
/// The decision rule on top of the VAD, kept pure so the self-tests can pin it down:
/// a take counts as speech when its detected speech adds up to at least
/// <see cref="MinSpeechSeconds"/>.
/// </summary>
public static class SpeechGate
{
    /// <summary>Shorter than this and Whisper is more likely to invent text than to hear a word.</summary>
    public const double MinSpeechSeconds = 0.3;

    public static double SpeechSeconds(IEnumerable<(TimeSpan Start, TimeSpan End)> segments) =>
        segments.Sum(s => Math.Max(0, (s.End - s.Start).TotalSeconds));

    public static bool HasSpeech(double? detectedSpeechSeconds) =>
        detectedSpeechSeconds is null || detectedSpeechSeconds.Value >= MinSpeechSeconds;
}
