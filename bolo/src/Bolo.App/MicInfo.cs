using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Bolo.App;

/// <summary>
/// Names the chosen input and reads its Windows mute switch (v1.3.1). A muted input still
/// "records": capture delivers silence with no error, which is how a muted webcam mic made
/// every take vanish without a word. WaveIn and the Windows audio endpoints share no id, so
/// the WaveIn product name (cut at 31 characters) is matched against the endpoint names.
/// </summary>
public static class MicInfo
{
    /// <summary>Names of the WaveIn inputs in their current order.</summary>
    public static List<string> DeviceNames()
    {
        var names = new List<string>();
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            names.Add(WaveInEvent.GetCapabilities(i).ProductName);
        return names;
    }

    /// <summary>
    /// The chosen mic's index right now (v1.3.2). Windows lists the default input first, so
    /// making a mic the default renumbers the rest: a saved index alone drifts onto another mic.
    /// </summary>
    public static int Resolve(Bolo.Core.Storage.AppSettings settings)
    {
        var index = Bolo.Core.Storage.AppSettings.ResolveMic(settings.MicDeviceName, settings.MicDeviceIndex, DeviceNames());
        if (index < 0 && settings.MicDeviceName != "")
            Bolo.Core.Storage.BoloLog.Write($"mic \"{settings.MicDeviceName}\" not found: using the System default");
        return index;
    }

    /// <summary>Saves a pick from a mic list whose first entry is "System default".</summary>
    public static void Choose(Bolo.Core.Storage.AppSettings settings, IReadOnlyList<string> pickerItems, int pickerIndex)
    {
        settings.MicDeviceIndex = pickerIndex - 1; // "System default" = -1
        settings.MicDeviceName = pickerIndex > 0 ? pickerItems[pickerIndex] : "";
        settings.Save();
    }

    /// <summary>Short display name: "Microphone (Brio 101)" → "Brio 101".</summary>
    public static string Name(int deviceIndex)
    {
        try
        {
            var name = deviceIndex < 0 ? DefaultEndpoint()?.FriendlyName ?? "the default microphone"
                : WaveInEvent.GetCapabilities(deviceIndex).ProductName;
            const string prefix = "Microphone (";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                name = name[prefix.Length..].TrimEnd(')');
            return name;
        }
        catch
        {
            return "the microphone";
        }
    }

    /// <summary>True when Windows has the input muted. False when unknown.</summary>
    public static bool IsMuted(int deviceIndex)
    {
        try
        {
            var device = deviceIndex < 0 ? DefaultEndpoint() : FindEndpoint(WaveInEvent.GetCapabilities(deviceIndex).ProductName);
            return device?.AudioEndpointVolume.Mute ?? false;
        }
        catch
        {
            return false;
        }
    }

    private static MMDevice? DefaultEndpoint()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
    }

    private static MMDevice? FindEndpoint(string waveInName)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(d => d.FriendlyName.StartsWith(waveInName, StringComparison.OrdinalIgnoreCase));
    }
}
