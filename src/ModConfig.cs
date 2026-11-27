using MelonLoader;
using TTSAPI.Tts;

namespace TTSAPI;

public static class ModConfig
{
    private static MelonPreferences_Category _category;
    private static MelonPreferences_Entry<string> _baseUrl;
    private static MelonPreferences_Entry<string> _apiKey;
    private static MelonPreferences_Entry<string> _model;
    private static MelonPreferences_Entry<string> _voice;
    private static MelonPreferences_Entry<string> _voices;
    private static MelonPreferences_Entry<int> _timeoutSeconds;
    private static MelonPreferences_Entry<float> _speed;
    private static MelonPreferences_Entry<string> _instructions;
    private static MelonPreferences_Entry<string> _extraHeaders;
    private static MelonPreferences_Entry<float> _volume;
    private static MelonPreferences_Entry<bool> _queueMessages;
    private static MelonPreferences_Entry<bool> _delayChatBox;

    public static MelonPreferences_Entry<bool> QueueMessages => _queueMessages;
    public static MelonPreferences_Entry<bool> DelayChatBoxUntilSpeech => _delayChatBox;

    public static void Initialize()
    {
        _category = MelonPreferences.CreateCategory("TTSAPI", "CVR TTS-API");

        _baseUrl = _category.CreateEntry("BaseUrl", "http://127.0.0.1:8080",
            "Server base URL", "Root of the audio server; the mod appends /v1/audio/speech.");
        _apiKey = _category.CreateEntry("ApiKey", "",
            "API key", "Sent as 'Authorization: Bearer <key>' when set. Leave empty for a local server.");
        _model = _category.CreateEntry("Model", "",
            "Model id", "Required by audio.cpp: must match the server's models[].id (e.g. zonos3).");
        _voice = _category.CreateEntry("Voice", "",
            "Default voice", "Voice id sent when the in-game voice selection is empty.");
        _voices = _category.CreateEntry("Voices", "",
            "Voice list", "Comma-separated voice ids shown in CVR's TTS voice selector. Empty means the server's default voice preset.");
        _timeoutSeconds = _category.CreateEntry("TimeoutSeconds", 180,
            "Request timeout (seconds)", "Covers a cold model load on the server.");
        _speed = _category.CreateEntry("Speed", 0f,
            "Speaking speed", "0 = let the server decide; ignored by servers without speed support.");
        _instructions = _category.CreateEntry("Instructions", "",
            "Instructions", "Optional OpenAI-style 'instructions' field, for servers/models that accept it.");
        _extraHeaders = _category.CreateEntry("ExtraHeaders", "",
            "Extra headers", "Semicolon-separated 'Name: Value' pairs sent with every request.");
        _volume = _category.CreateEntry("Volume", 1f,
            "Volume", "Playback gain for generated speech (0 = mute, 1 = as generated, 2 = max). Also affects the voice other players hear.");
        _queueMessages = _category.CreateEntry("QueueMessages", true,
            "Queue messages", "Speak messages in order: while one is generating or playing, later ones wait. Off = CVR default (a new message interrupts the current one).");
        _delayChatBox = _category.CreateEntry("DelayChatBoxUntilSpeech", true,
            "Delay chatbox text", "Hold the chatbox text until its speech starts, so text and audio appear together. Requires Queue messages.");
    }

    public static TtsSettings GetSettings()
    {
        return new TtsSettings
        {
            BaseUrl = _baseUrl.Value,
            ApiKey = _apiKey.Value,
            Model = _model.Value,
            Voice = _voice.Value,
            VoicesRaw = _voices.Value,
            TimeoutSeconds = _timeoutSeconds.Value,
            Speed = _speed.Value,
            Instructions = _instructions.Value,
            ExtraHeaders = _extraHeaders.Value,
            Volume = _volume.Value
        };
    }
}
