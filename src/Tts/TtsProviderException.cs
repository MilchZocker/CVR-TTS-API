using System;

namespace TTSAPI.Tts;

public sealed class TtsProviderException : Exception
{
    public TtsProviderException(string message) : base(message)
    {
    }
}
