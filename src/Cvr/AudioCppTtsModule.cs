using ABI_RC.Systems.Communications.TTS;
using MelonLoader;
using TTSAPI.Tts;

namespace TTSAPI;

public sealed class AudioCppTtsModule : Comms_TTSModule
{
    private TtsSynthesisService _service;

    public override void Initialize()
    {
        _service = new TtsSynthesisService(ModConfig.GetSettings, message => MelonLogger.Warning("[TTSAPI] " + message));

        SampleRate = PcmConverter.TargetSampleRate;
        Channels = 1;

        Voices.Clear();
        foreach (string voice in ModConfig.GetSettings().BuildVoiceList())
            Voices[voice] = voice;
    }

    public override short[] Process(string msg)
    {
        if (_service == null)
            Initialize();

        TtsSettings settings = ModConfig.GetSettings();
        TtsAudio audio = _service.Synthesize(msg, settings.ResolveVoice(CurrentVoice));
        if (audio == null)
        {
            TtsChatBoxSync.NotifySynthesisFailed();
            return null;
        }

        short[] pcm = PcmConverter.ApplyGain(PcmConverter.ToMono48k(audio), settings.Volume);
        return PcmConverter.WithTail(pcm);
    }
}
