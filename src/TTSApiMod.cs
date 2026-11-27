using MelonLoader;
using ABI_RC.Systems.Communications.Audio.TTS;
using TTSAPI.Tts;

namespace TTSAPI;

public sealed class TTSApiMod : MelonMod
{
    public const string ModuleId = "AUDIOCPP";

    internal static TTSApiMod Instance { get; private set; }

    public override void OnInitializeMelon()
    {
        Instance = this;

        ModConfig.Initialize();

        TtsSettings settings = ModConfig.GetSettings();
        string model = string.IsNullOrWhiteSpace(settings.Model) ? "(server default)" : settings.Model;
        LoggerInstance.Msg($"TTS-API ready. Server: {settings.BaseUrl} | model: {model}");

        foreach (string warning in settings.Validate())
            LoggerInstance.Warning("[TTSAPI] " + warning);

        MelonPreferences.Save();

        try
        {
            Comms_TTSHandler.AddModule<AudioCppTtsModule>(ModuleId, "audio.cpp (OpenAI-compatible)");
            LoggerInstance.Msg($"Registered TTS module '{ModuleId}'.");
        }
        catch (System.Exception ex)
        {
            LoggerInstance.Error($"Could not register TTS module '{ModuleId}': {ex.Message}");
        }

        try
        {
            HarmonyInstance.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            TtsChatBoxSync.Initialize();
            LoggerInstance.Msg($"Installed queue patches (queue={ModConfig.QueueMessages?.Value}, delay chatbox={ModConfig.DelayChatBoxUntilSpeech?.Value}).");
        }
        catch (System.Exception ex)
        {
            LoggerInstance.Error($"Could not install message-queue patches; messages will not be queued or delayed: {ex.Message}");
        }
    }

    public override void OnUpdate()
    {
        TtsMessageQueue.Update();
        TtsChatBoxSync.Update();
    }
}
