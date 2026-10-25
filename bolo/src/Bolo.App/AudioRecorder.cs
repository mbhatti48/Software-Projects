using System.IO;
using NAudio.Utils;
using NAudio.Wave;

namespace Bolo.App;

/// <summary>
/// Captures microphone audio as 16kHz 16-bit mono PCM into a memory buffer.
/// Supports mid-recording snapshots (for live partial transcription) and a final
/// WAV on stop. Audio never touches disk.
/// </summary>
public sealed class AudioRecorder : IDisposable
{
    private static readonly WaveFormat Format = new(16000, 16, 1);
    private readonly object _gate = new();
    private WaveInEvent? _waveIn;
    private MemoryStream? _pcm;

    public bool IsRecording { get; private set; }

    /// <summary>NAudio input device index; -1 = Windows default microphone.</summary>
    public int DeviceIndex { get; set; } = -1;

    public void Start()
    {
        if (IsRecording)
            return;
        _pcm = new MemoryStream();
        _waveIn = new WaveInEvent { WaveFormat = Format, DeviceNumber = DeviceIndex };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        IsRecording = true;
    }

    /// <summary>Peak input level of the latest buffer, 0..1, for the overlay's level bars.</summary>
    public float Level { get; private set; }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_gate)
        {
            _pcm?.Write(e.Buffer, 0, e.BytesRecorded);
        }
        var peak = 0;
        for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            var sample = Math.Abs((int)BitConverter.ToInt16(e.Buffer, i));
            if (sample > peak) peak = sample;
        }
        Level = peak / 32768f;
    }

    public double BufferedSeconds
    {
        get
        {
            lock (_gate)
            {
                return (_pcm?.Length ?? 0) / (double)Format.AverageBytesPerSecond;
            }
        }
    }

    /// <summary>
    /// Copy of recorded audio as a WAV stream, or null if empty. When
    /// <paramref name="lastSeconds"/> is set, only the most recent tail is returned —
    /// keeps live-preview transcription cost flat on long dictations.
    /// </summary>
    public MemoryStream? SnapshotWav(double? lastSeconds = null)
    {
        byte[] bytes;
        lock (_gate)
        {
            if (_pcm is null || _pcm.Length == 0)
                return null;
            var all = _pcm.GetBuffer();
            var length = (int)_pcm.Length;
            var start = 0;
            if (lastSeconds is not null)
            {
                var tailBytes = (int)(lastSeconds.Value * Format.AverageBytesPerSecond) & ~1; // sample-aligned
                if (tailBytes < length)
                    start = length - tailBytes;
            }
            bytes = new byte[length - start];
            Array.Copy(all, start, bytes, 0, bytes.Length);
        }
        return BuildWav(bytes);
    }

    /// <summary>Stops capture and returns the finished WAV, positioned at 0.</summary>
    public MemoryStream Stop()
    {
        if (!IsRecording || _pcm is null)
            throw new InvalidOperationException("Not recording.");

        _waveIn!.DataAvailable -= OnDataAvailable;
        _waveIn.StopRecording();
        _waveIn.Dispose();
        _waveIn = null;
        IsRecording = false;

        byte[] bytes;
        lock (_gate)
        {
            bytes = _pcm.ToArray();
            _pcm.Dispose();
            _pcm = null;
        }
        return BuildWav(bytes);
    }

    private static MemoryStream BuildWav(byte[] pcm)
    {
        var wav = new MemoryStream();
        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(wav), Format))
            writer.Write(pcm, 0, pcm.Length);
        wav.Position = 0;
        return wav;
    }

    public void Dispose()
    {
        _waveIn?.Dispose();
        lock (_gate)
        {
            _pcm?.Dispose();
        }
    }
}
