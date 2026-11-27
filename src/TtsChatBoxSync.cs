using System;
using System.Reflection;
using System.Threading;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.ChatBox;
using ABI_RC.Systems.Communications.Audio.TTS;
using MelonLoader;
using TTSAPI.Tts;

namespace TTSAPI;

/// <summary>
/// Holds the chatbox text of a typed message until its speech starts playing, so other
/// players see the text at the same moment they hear the voice. Requires QueueMessages,
/// because without it a second message can abort the first and break text/audio pairing.
/// </summary>
internal static class TtsChatBoxSync
{
    private static readonly ChatBoxDelayQueue Queue = new();
    private static readonly MethodInfo SendMethod = typeof(ChatBoxManager).GetMethod(
        "SendMessageToNetwork", BindingFlags.Static | BindingFlags.NonPublic);

    private static bool _flushing;
    private static bool _wasPlaying;
    private static int _synthesisFailed;

    private static bool Enabled =>
        SendMethod != null &&
        ModConfig.DelayChatBoxUntilSpeech != null && ModConfig.DelayChatBoxUntilSpeech.Value &&
        ModConfig.QueueMessages != null && ModConfig.QueueMessages.Value;

    public static void Initialize()
    {
        if (SendMethod == null)
            MelonLogger.Error("[TTSAPI] Chatbox sync disabled: ChatBoxManager.SendMessageToNetwork not found.");
    }

    /// <summary>Returns true when the chatbox send was delayed and the original must be skipped.</summary>
    public static bool TryDelay(ChatBoxAPI.MessageSource source, string message, bool notification, bool showInBubble, bool showInHistory)
    {
        if (_flushing || !Enabled)
            return false;

        if (source != ChatBoxAPI.MessageSource.Internal)
            return false;

        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (!TextComms.SettingTTSEnabled || Comms_TTSHandler.Instance == null)
            return false;

        Queue.Enqueue(message, notification, showInBubble, showInHistory, Environment.TickCount);
        return true;
    }

    public static void Update()
    {
        Comms_TTSHandler handler = Comms_TTSHandler.Instance;
        if (handler == null)
        {
            Queue.Clear();
            _wasPlaying = false;
            return;
        }

        if (!Enabled)
        {
            FlushAll();
            _wasPlaying = false;
            return;
        }

        if (Interlocked.Exchange(ref _synthesisFailed, 0) == 1)
            FlushOne();

        bool playing = handler.IsPlaying;
        if (playing && !_wasPlaying)
            FlushOne();
        _wasPlaying = playing;

        if (!playing && !handler.IsProcessing && Queue.Peek(out PendingChatMessage head) &&
            unchecked((uint)(Environment.TickCount - head.EnqueuedAtMs)) > 30000)
            FlushOne();
    }

    /// <summary>Called by the module on a worker thread when no audio can be produced.</summary>
    public static void NotifySynthesisFailed() => Interlocked.Exchange(ref _synthesisFailed, 1);

    private static void FlushOne()
    {
        if (Queue.TryDequeue(out PendingChatMessage message))
            Send(message);
    }

    private static void FlushAll()
    {
        while (Queue.TryDequeue(out PendingChatMessage message))
            Send(message);
    }

    private static void Send(PendingChatMessage message)
    {
        _flushing = true;
        try
        {
            SendMethod.Invoke(null, new object[]
            {
                ChatBoxAPI.MessageSource.Internal,
                "",
                message.Message,
                message.Notification,
                message.ShowInBubble,
                message.ShowInHistory
            });
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[TTSAPI] Could not send delayed chatbox message: " + ex.Message);
        }
        finally
        {
            _flushing = false;
        }
    }
}
