using ABI_RC.Systems.ChatBox;
using HarmonyLib;

namespace TTSAPI;

[HarmonyPatch(typeof(ChatBoxManager), "SendMessageToNetwork")]
internal static class ChatBoxSendPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ChatBoxAPI.MessageSource source, string message, bool notification, bool showInBubble, bool showInHistory)
        => !TtsChatBoxSync.TryDelay(source, message, notification, showInBubble, showInHistory);
}
