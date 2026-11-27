using System;
using System.Collections.Generic;

namespace TTSAPI.Tts;

public sealed class TtsSettings
{
    public const string PlaceholderVoice = "default";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8080";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string Voice { get; set; } = "";
    public string VoicesRaw { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 180;
    public float Speed { get; set; }
    public float Volume { get; set; } = 1f;
    public string Instructions { get; set; } = "";
    public string ExtraHeaders { get; set; } = "";

    /// <summary>Voice ids shown by CVR's TTS voice selector. Never empty.</summary>
    public IReadOnlyList<string> BuildVoiceList()
    {
        IReadOnlyList<string> configured = ParseConfiguredVoices();
        return configured.Count > 0 ? configured : new[] { PlaceholderVoice };
    }

    /// <summary>Maps CVR's selected voice to the request value; the synthetic placeholder means "server default".</summary>
    public string ResolveVoice(string currentVoice)
    {
        bool placeholderConfigured = ParseConfiguredVoices()
            .Any(v => string.Equals(v, PlaceholderVoice, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(currentVoice) &&
            (currentVoice.Trim() != PlaceholderVoice || placeholderConfigured))
            return currentVoice.Trim();

        return string.IsNullOrWhiteSpace(Voice) ? string.Empty : Voice.Trim();
    }

    private IReadOnlyList<string> ParseConfiguredVoices()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string part in (VoicesRaw ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = part.Trim();
            if (trimmed.Length == 0)
                continue;
            if (seen.Add(trimmed))
                result.Add(trimmed);
        }

        if (result.Count == 0 && !string.IsNullOrWhiteSpace(Voice))
            result.Add(Voice.Trim());

        return result;
    }

    /// <summary>Startup hints for values that would otherwise fail per message.</summary>
    public IReadOnlyList<string> Validate()
    {
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(Model))
            warnings.Add("Model is empty; audio.cpp requires models[].id (e.g. Model = \"zonos3\").");

        if (!Uri.TryCreate((BaseUrl ?? string.Empty).Trim(), UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            warnings.Add($"BaseUrl '{BaseUrl}' is not an absolute http(s) URL.");

        if (TimeoutSeconds < 1 || TimeoutSeconds > 3600)
            warnings.Add($"TimeoutSeconds {TimeoutSeconds} is outside 1..3600; requests will be clamped.");

        if (!float.IsFinite(Speed) || Speed < 0f)
            warnings.Add($"Speed {Speed} is not a finite non-negative number; it will be omitted.");

        if (!float.IsFinite(Volume) || Volume < 0f || Volume > 2f)
            warnings.Add($"Volume {Volume} is outside 0..2; it will be clamped.");

        return warnings;
    }

    /// <summary>Parses <c>Name: Value; Name2: Value2</c>; malformed entries are skipped.</summary>
    public IReadOnlyDictionary<string, string> ParseExtraHeaders()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string part in (ExtraHeaders ?? "").Split(';'))
        {
            int colon = part.IndexOf(':');
            if (colon <= 0)
                continue;

            string name = part.Substring(0, colon).Trim();
            string value = part.Substring(colon + 1).Trim();
            if (name.Length == 0)
                continue;

            headers[name] = value;
        }

        return headers;
    }
}
