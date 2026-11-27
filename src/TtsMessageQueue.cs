using System;
using System.Reflection;
using ABI_RC.Systems.Communications.Audio.TTS;
using MelonLoader;
using TTSAPI.Tts;

namespace TTSAPI;

/// <summary>
/// Holds chat messages until the previous one finished generating and playing.
/// CVR calls ProcessMessage for every send and stops the current audio first, so the
/// only way to speak messages in order is to intercept that call and replay it later.
/// </summary>
internal static class TtsMessageQueue
{
    private const int Capacity = 32;

    private static readonly TtsQueue Queue = new(Capacity);
    private static readonly MethodInfo ProcessMessageMethod = typeof(Comms_TTSHandler).GetMethod(
        nameof(Comms_TTSHandler.ProcessMessage), BindingFlags.Instance | BindingFlags.Public);

    private static bool _invoking;

    private static bool Enabled => ModConfig.QueueMessages != null && ModConfig.QueueMessages.Value;

    /// <summary>Returns true when the message was queued and CVR's immediate processing must be skipped.</summary>
    public static bool TryIntercept(Comms_TTSHandler handler, string message)
    {
        if (!Enabled)
        {
            Queue.Clear();
            return false;
        }

        if (_invoking)
            return false;

        if (string.IsNullOrWhiteSpace(message))
        {
            Queue.Clear();
            return false;
        }

        Queue.Enqueue(message);
        return true;
    }

    public static void Update()
    {
        if (!Enabled)
        {
            Queue.Clear();
            return;
        }

        Comms_TTSHandler handler = Comms_TTSHandler.Instance;
        if (handler == null)
        {
            Queue.Clear();
            return;
        }

        if (_invoking || handler.IsProcessing || handler.IsPlaying)
            return;

        if (!Queue.TryDequeue(true, out string message))
            return;

        if (ProcessMessageMethod == null)
        {
            Queue.Clear();
            MelonLogger.Error("[TTSAPI] Queue is enabled but Comms_TTSHandler.ProcessMessage was not found.");
            return;
        }

        _invoking = true;
        try
        {
            ProcessMessageMethod.Invoke(handler, new object[] { message });
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[TTSAPI] Queued message failed: " + ex.Message);
        }
        finally
        {
            _invoking = false;
        }
    }
}
