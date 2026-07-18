using System.Diagnostics;
using System.Text;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Bolo.Core.Engine;

/// <summary>
/// Keeps a whisper.cpp model resident in memory (Vulkan GPU when available, CPU otherwise)
/// and transcribes 16kHz mono WAV streams. Audio only ever exists in memory — this class
/// never writes it anywhere.
/// </summary>
public sealed class WhisperTranscriber : IDisposable
{
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;

    // A WhisperProcessor cannot run two inferences at once; live partial transcription
    // and the final pass must take turns.
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);

    public bool IsLoaded => _processor is not null;
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
        var builder = _factory!.CreateBuilder().WithLanguage(_language);
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

    public void Unload()
    {
        _processor?.Dispose();
        _processor = null;
        _factory?.Dispose();
        _factory = null;
    }

    public void Dispose() => Unload();
}
