using ABI_RC.Systems.Communications.Audio.TTS;
using HarmonyLib;

namespace TTSAPI;

[HarmonyPatch(typeof(Comms_TTSHandler), nameof(Comms_TTSHandler.ProcessMessage))]
internal static class ProcessMessagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Comms_TTSHandler __instance, string msg)
        => !TtsMessageQueue.TryIntercept(__instance, msg);
}
