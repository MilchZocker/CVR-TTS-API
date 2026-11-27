using System.Threading;

namespace TTSAPI.Tts;

public interface ITtsProvider
{
    /// <summary>Returns the raw response body (WAV for OpenAI-compatible servers). Throws <see cref="TtsProviderException"/> on failure.</summary>
    byte[] Synthesize(string text, string voice, CancellationToken cancellationToken);
}
