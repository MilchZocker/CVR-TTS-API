using System;
using System.Threading;

namespace TTSAPI.Tts;

public sealed class TtsSynthesisService
{
    private readonly Func<TtsSettings> _settingsProvider;
    private readonly Func<TtsSettings, ITtsProvider> _providerFactory;
    private readonly Action<string> _log;

    public TtsSynthesisService(Func<TtsSettings> settingsProvider, Action<string> log)
        : this(settingsProvider, settings => new OpenAiSpeechProvider(settings), log)
    {
    }

    public TtsSynthesisService(Func<TtsSettings> settingsProvider, Func<TtsSettings, ITtsProvider> providerFactory, Action<string> log)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _log = log ?? (_ => { });
    }

    /// <summary>Returns parsed audio, or <c>null</c> after logging why nothing can be spoken.</summary>
    public TtsAudio Synthesize(string text, string voice)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _log("Empty message; nothing to speak.");
            return null;
        }

        try
        {
            TtsSettings settings = _settingsProvider();
            ITtsProvider provider = _providerFactory(settings);
            byte[] body = provider.Synthesize(text, voice ?? string.Empty, CancellationToken.None);

            if (!WavParser.TryParse(body, out TtsAudio audio, out string error))
            {
                _log($"TTS response was not usable audio: {error} (body starts with: {Preview(body)})");
                return null;
            }

            return audio;
        }
        catch (TtsProviderException ex)
        {
            _log($"TTS request failed: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            _log($"TTS failed unexpectedly: {ex.Message}");
            return null;
        }
    }

    private static string Preview(byte[] body)
    {
        if (body == null || body.Length == 0)
            return "(empty)";

        int count = Math.Min(body.Length, 60);
        var sb = new System.Text.StringBuilder(count);
        for (int i = 0; i < count; i++)
        {
            char c = (char)body[i];
            sb.Append(c >= 0x20 && c < 0x7F ? c : '.');
        }

        return sb.ToString();
    }
}
