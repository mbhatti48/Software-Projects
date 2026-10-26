using System.IO;
using System.Diagnostics;
using Bolo.Core.Engine;
using Bolo.Core.Learning;
using Bolo.Core.Storage;

namespace Bolo.App;

public enum DictationState { Loading, Idle, Recording, Transcribing }

/// <summary>
/// Orchestrates the core loop: hotkey toggle → record to RAM → transcribe → inject
/// into the focused app → append to history → discard audio.
/// </summary>
public sealed class DictationController : IDisposable
{
    private readonly AppSettings _settings;
    private readonly WhisperTranscriber _transcriber = new();
    private readonly AudioRecorder _recorder = new();

    // Exposed for the UI pages (History, Dictionary, Snippets, Insights).
    public HistoryStore History { get; } = new();
    public VocabularyStore Vocabulary { get; } = VocabularyStore.Load();
    public CorrectionStore Corrections { get; } = CorrectionStore.Load();
    public SnippetStore Snippets { get; } = SnippetStore.Load();

    public DictationState State { get; private set; } = DictationState.Loading;
    public string LoadedRuntime => _transcriber.LoadedRuntime;
    /// <summary>Microphone peak level while recording, 0..1 (drives the overlay's bars).</summary>
    public float InputLevel => _recorder.IsRecording ? _recorder.Level : 0;

    public event Action<DictationState>? StateChanged;
    public event Action<string>? TranscriptReady;
    /// <summary>Live provisional transcript of the audio captured so far, while recording.</summary>
    public event Action<string>? PartialTranscript;
    /// <summary>Informational messages worth surfacing (auto-learned words, commands).</summary>
    public event Action<string>? Notice;
    public event Action<string>? Error;
    /// <summary>A take produced no text (silence, a muted mic): the reason, for the pill (v1.3.1).</summary>
    public event Action<string>? TakeDropped;

    private CancellationTokenSource? _partialCts;

    // Holding the hotkey triggers keyboard auto-repeat (~30ms interval), which would
    // rapid-toggle recording. Ignore presses that arrive too soon after the last one.
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(400);
    private DateTime _lastToggle = DateTime.MinValue;

    // Whisper hallucinates on near-silence ("*sad music*" etc.) — skip clips too short
    // to contain intentional speech.
    private const double MinAudioSeconds = 0.6;

    // Hard ceiling for a single take — protects against stuck/forgotten recordings.
    private const double MaxRecordingSeconds = 300;

    public DictationController(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task LoadModelAsync()
    {
        WhisperTranscriber.ConfigureRuntime(_settings.ForceCpu);
        var prompt = Vocabulary.BuildPrompt();
        await Task.Run(() => _transcriber.Load(_settings.ModelPath, _settings.Language, prompt));
        // The Silero VAD model (ggml-silero-*.bin) lives beside the Whisper model. Optional:
        // without it the duration gate alone applies, as before v1.3.0.
        var modelsDir = Path.GetDirectoryName(_settings.ModelPath) ?? "";
        var vadModel = Directory.Exists(modelsDir)
            ? Directory.GetFiles(modelsDir, "ggml-silero*.bin").OrderDescending().FirstOrDefault()
            : null;
        if (vadModel is not null)
        {
            try
            {
                await Task.Run(() => _transcriber.LoadVad(vadModel));
                BoloLog.Write($"VAD loaded: {Path.GetFileName(vadModel)}");
            }
            catch (Exception ex)
            {
                BoloLog.Write($"VAD load failed, duration gate only: {ex.Message}");
            }
        }
        else
        {
            BoloLog.Write("no VAD model beside the Whisper model: duration gate only");
        }
        SetState(DictationState.Idle);
    }

    public bool HasVad => _transcriber.HasVad;

    /// <summary>
    /// Esc while recording: throw the take away. No transcription, nothing injected,
    /// nothing stored. Ignored once a stop is already in flight.
    /// </summary>
    public void CancelDictation()
    {
        if (State != DictationState.Recording)
            return;
        if (Interlocked.Exchange(ref _stopInFlight, 1) == 1)
            return;
        try
        {
            _partialCts?.Cancel();
            _partialCts = null;
            try { _recorder.Stop().Dispose(); } catch { /* already stopped */ }
            BoloLog.Write("recording cancelled (Esc)");
            SetState(DictationState.Idle);
        }
        finally
        {
            Volatile.Write(ref _stopInFlight, 0);
        }
    }

    /// <summary>Hotkey handler for toggle mode: tap to start, tap again to stop.</summary>
    public void Toggle()
    {
        var now = DateTime.UtcNow;
        if (now - _lastToggle < DebounceWindow)
            return;
        _lastToggle = now;

        if (State == DictationState.Idle)
            StartDictation();
        else if (State == DictationState.Recording)
            StopDictation();
        // Loading or Transcribing: ignore the press rather than queue confusion.
    }

    /// <summary>
    /// Starts capturing audio the instant a modifier combo goes down, before it's
    /// confirmed — so no speech is lost to the confirmation delay. Silent: no state
    /// change, no overlay. Confirmed → StartDictation adopts the running capture;
    /// cancelled → CancelPrewarm discards it.
    /// </summary>
    public void PrewarmCapture()
    {
        if (State != DictationState.Idle || _recorder.IsRecording)
            return;
        try
        {
            _recorder.DeviceIndex = MicInfo.Resolve(_settings);
            _recorder.Start();
            BoloLog.Write("prewarm: capture started");
        }
        catch (Exception ex)
        {
            BoloLog.Write($"prewarm failed: {ex.Message}");
        }
    }

    /// <summary>Discards a prewarmed capture whose combo turned out to be a shortcut.</summary>
    public void CancelPrewarm()
    {
        if (State != DictationState.Idle || !_recorder.IsRecording)
            return;
        try
        {
            _recorder.Stop().Dispose();
            BoloLog.Write("prewarm: discarded");
        }
        catch { /* nothing worth surfacing */ }
    }

    /// <summary>Starts recording if idle. No debounce — hold-to-talk calls this directly.</summary>
    public void StartDictation()
    {
        if (State != DictationState.Idle)
            return;
        try
        {
            if (!_recorder.IsRecording) // not prewarmed (button/tray/registered-hotkey path)
            {
                _recorder.DeviceIndex = MicInfo.Resolve(_settings);
                _recorder.Start();
            }
            SetState(DictationState.Recording);
            _partialCts = new CancellationTokenSource();
            _ = RunPartialLoopAsync(_partialCts.Token);
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Could not start recording: {ex.Message}");
        }
    }

    /// <summary>Stops recording and transcribes. No debounce — hold-to-talk calls this directly.</summary>
    public async void StopDictation()
    {
        if (State != DictationState.Recording)
            return;
        // Two stoppers can race on different threads (hold-release watcher vs the
        // 5-minute cap, or hotkey vs tray): only the first may stop the recorder, or the
        // second throws "Not recording" and flips the state to Idle mid-transcription.
        if (Interlocked.Exchange(ref _stopInFlight, 1) == 1)
            return;
        try
        {
            await StopAndTranscribeAsync();
        }
        finally
        {
            Volatile.Write(ref _stopInFlight, 0);
        }
    }

    private int _stopInFlight;

    /// <summary>
    /// While recording, periodically re-transcribes the growing buffer so the overlay
    /// can show live ghost text. The final pass on stop is unchanged and authoritative.
    /// </summary>
    private async Task RunPartialLoopAsync(CancellationToken ct)
    {
        try
        {
            var firstPass = true;
            while (!ct.IsCancellationRequested)
            {
                // First ghost text as early as possible — it's the user's cue that
                // Bolo is hearing them; later refreshes can be slower.
                await Task.Delay(firstPass ? 500 : 1200, ct);
                firstPass = false;

                // Safety net: never record unbounded (a stuck hold or forgotten toggle).
                if (_recorder.BufferedSeconds > MaxRecordingSeconds)
                {
                    Notice?.Invoke("Recording hit the 5-minute limit — transcribing what you said.");
                    StopDictation();
                    return;
                }

                // Ghost text only needs the recent tail; a full re-transcription would get
                // slower and slower as a long dictation grows.
                var snapshot = _recorder.SnapshotWav(lastSeconds: 20);
                if (snapshot is null || snapshot.Length < 44 + (long)(16000 * 2 * MinAudioSeconds))
                {
                    snapshot?.Dispose();
                    continue;
                }
                using (snapshot)
                {
                    // No speech in the tail yet (VAD): skip the pass instead of letting
                    // Whisper invent ghost text from room noise.
                    if (!SpeechGate.HasSpeech(await _transcriber.DetectSpeechSecondsAsync(snapshot, ct)))
                        continue;
                    var text = await _transcriber.TranscribeAsync(snapshot, ct);
                    if (!ct.IsCancellationRequested && State == DictationState.Recording
                        && !string.IsNullOrWhiteSpace(text))
                        PartialTranscript?.Invoke(text);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // recording stopped — expected
        }
        catch (Exception ex)
        {
            BoloLog.Write($"partial transcription loop error: {ex.Message}");
        }
    }

    private async Task StopAndTranscribeAsync()
    {
        _partialCts?.Cancel();
        _partialCts = null;
        SetState(DictationState.Transcribing);
        try
        {
            using var wav = _recorder.Stop();
            var audioSeconds = Math.Max(0, (wav.Length - 44.0) / (16000 * 2)); // 16kHz 16-bit mono
            if (audioSeconds < MinAudioSeconds)
                return; // too short to be intentional speech — avoids silence hallucinations
            // Voice activity check (v1.3.0): a take with no detected speech is discarded
            // before Whisper can hallucinate "*sad music*" from it.
            var speechSeconds = await _transcriber.DetectSpeechSecondsAsync(wav);
            if (!SpeechGate.HasSpeech(speechSeconds))
            {
                var muted = MicInfo.IsMuted(_recorder.DeviceIndex);
                var mic = MicInfo.Name(_recorder.DeviceIndex);
                BoloLog.Write($"no speech detected ({audioSeconds:F1}s audio, {speechSeconds:F2}s speech, peak {_recorder.TakePeak:P1}, mic={mic}, muted={muted}): take discarded");
                TakeDropped?.Invoke(SpeechGate.DroppedReason(mic, muted, _recorder.TakePeak));
                return;
            }
            var timer = Stopwatch.StartNew();
            var text = await _transcriber.TranscribeAsync(wav);
            timer.Stop();

            // Voice command: "add to dictionary <term>" — consumed, never injected.
            if (_settings.AutoLearn && TryHandleDictionaryCommand(text))
                return;

            // The learning pipeline: corrections, snippets, auto-learn, style transforms.
            text = Corrections.Apply(text);
            text = Snippets.Apply(text);
            if (_settings.AutoLearn)
                text = AutoLearnSpelledAcronyms(text);
            if (_settings.RemoveFillerWords)
                text = Transforms.RemoveFillers(text);
            if (_settings.ConvertSpokenLists)
                text = Transforms.SpokenListToBullets(text);

            if (!string.IsNullOrWhiteSpace(text))
            {
                History.Append(new TranscriptEntry(
                    DateTimeOffset.Now, text, audioSeconds,
                    timer.Elapsed.TotalSeconds, _transcriber.LoadedRuntime,
                    ForegroundApp.GetName()));
                TranscriptReady?.Invoke(text);
            }
            else
            {
                BoloLog.Write($"empty transcript ({audioSeconds:F1}s audio, peak {_recorder.TakePeak:P1}): nothing typed");
                TakeDropped?.Invoke(SpeechGate.DroppedReason(MicInfo.Name(_recorder.DeviceIndex),
                    MicInfo.IsMuted(_recorder.DeviceIndex), _recorder.TakePeak));
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Transcription failed: {ex.Message}");
        }
        finally
        {
            SetState(DictationState.Idle);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex DictionaryCommandPattern = new(
        @"^(?:bolo[,.]?\s+)?add to (?:the\s+)?dictionary[:,.]?\s+(?<term>.+?)[\s\.]*$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// If the whole utterance is an "add to dictionary" command, adds the term to the
    /// Auto-learned cluster and reports true (the text is consumed, not injected).
    /// </summary>
    private bool TryHandleDictionaryCommand(string text)
    {
        var match = DictionaryCommandPattern.Match(text.Trim());
        if (!match.Success)
            return false;

        var term = match.Groups["term"].Value.Trim();
        // If the term itself was spelled out ("I R C C"), collapse it first.
        (term, _) = Transforms.CollapseSpelledAcronyms(term) is var (collapsed, found) && found.Count > 0
            ? (collapsed, found)
            : (term, []);

        AddToAutoLearnedCluster([term]);
        BoloLog.Write($"voice command: added \"{term}\" to dictionary");
        Notice?.Invoke($"Added \"{term}\" to the dictionary.");
        return true;
    }

    /// <summary>Collapses spelled-out acronyms and auto-adds them to the dictionary.</summary>
    private string AutoLearnSpelledAcronyms(string text)
    {
        var (result, acronyms) = Transforms.CollapseSpelledAcronyms(text);
        if (acronyms.Count > 0)
        {
            AddToAutoLearnedCluster(acronyms);
            BoloLog.Write($"auto-learned: {string.Join(", ", acronyms)}");
            Notice?.Invoke($"Learned: {string.Join(", ", acronyms)} — see the Dictionary page.");
        }
        return result;
    }

    private void AddToAutoLearnedCluster(IEnumerable<string> terms)
    {
        var cluster = Vocabulary.Clusters.FirstOrDefault(c => c.Name == "Auto-learned");
        if (cluster is null)
        {
            cluster = new VocabCluster { Name = "Auto-learned" };
            Vocabulary.Clusters.Add(cluster);
        }
        cluster.Terms = cluster.Terms.Union(terms, StringComparer.OrdinalIgnoreCase).ToList();
        Vocabulary.Save();
        _ = RefreshVocabularyPromptAsync();
    }

    public TranscriptEntry? LatestTranscript() => History.GetLatest();

    /// <summary>Rebuilds the Whisper prompt after the Dictionary page changes vocabulary.</summary>
    public Task RefreshVocabularyPromptAsync() => _transcriber.RepromptAsync(Vocabulary.BuildPrompt());

    /// <summary>Reloads the model (after a model-size or GPU/CPU settings change).</summary>
    public async Task ReloadModelAsync()
    {
        SetState(DictationState.Loading);
        await LoadModelAsync();
    }

    /// <summary>
    /// Correction harvesting for any history entry (the History page): learns
    /// (wrong → right) pairs and updates the stored entry.
    /// </summary>
    public int CorrectEntry(TranscriptEntry entry, string editedText)
    {
        if (string.Equals(entry.Text, editedText, StringComparison.Ordinal))
            return 0;
        var pairs = SubstitutionEngine.ExtractCorrections(entry.Text, editedText)
            .Where(p => p.Wrong.Split(' ').Length <= 6 && p.Right.Split(' ').Length <= 6)
            .ToList();
        foreach (var (wrong, right) in pairs)
            Corrections.AddOrReinforce(wrong, right);
        if (pairs.Count > 0)
            Corrections.Save();
        History.UpdateEntryText(entry.Timestamp, editedText);
        return pairs.Count;
    }

    /// <summary>
    /// Harvests (wrong → right) pairs from an edited version of the last transcript,
    /// stores them for future auto-application, and fixes the history entry.
    /// Returns how many pairs were learned.
    /// </summary>
    public int CorrectLastTranscript(string editedText)
    {
        var last = History.GetLatest();
        if (last is null || string.Equals(last.Text, editedText, StringComparison.Ordinal))
            return 0;

        var pairs = SubstitutionEngine.ExtractCorrections(last.Text, editedText)
            // Very long runs are rewrites of intent, not mishearings — don't learn them.
            .Where(p => p.Wrong.Split(' ').Length <= 6 && p.Right.Split(' ').Length <= 6)
            .ToList();
        foreach (var (wrong, right) in pairs)
            Corrections.AddOrReinforce(wrong, right);
        if (pairs.Count > 0)
            Corrections.Save();

        History.UpdateLatestText(editedText);
        return pairs.Count;
    }

    private void SetState(DictationState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public void Dispose()
    {
        _recorder.Dispose();
        _transcriber.Dispose();
    }
}
