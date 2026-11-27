using System;
using System.Collections.Generic;

namespace TTSAPI.Tts;

public sealed class PendingChatMessage
{
    public string Message;
    public bool Notification;
    public bool ShowInBubble;
    public bool ShowInHistory;
    public long EnqueuedAtMs;
}

/// <summary>
/// Chatbox messages held back until their speech starts, so text and audio appear together.
/// Identical text within a second is treated as one send.
/// </summary>
public sealed class ChatBoxDelayQueue
{
    private const int DuplicateWindowMs = 1000;

    private readonly Queue<PendingChatMessage> _items = new();

    private string _lastText;
    private long _lastAt = long.MinValue / 2;

    public int Count => _items.Count;

    public bool Enqueue(string message, bool notification, bool showInBubble, bool showInHistory, long nowMs)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (_lastText == message && nowMs - _lastAt <= DuplicateWindowMs)
        {
            _lastAt = nowMs;
            return false;
        }

        _items.Enqueue(new PendingChatMessage
        {
            Message = message,
            Notification = notification,
            ShowInBubble = showInBubble,
            ShowInHistory = showInHistory,
            EnqueuedAtMs = nowMs
        });

        _lastText = message;
        _lastAt = nowMs;
        return true;
    }

    public bool Peek(out PendingChatMessage message)
    {
        if (_items.Count == 0)
        {
            message = null;
            return false;
        }

        message = _items.Peek();
        return true;
    }

    public bool TryDequeue(out PendingChatMessage message)
    {
        if (_items.Count == 0)
        {
            message = null;
            return false;
        }

        message = _items.Dequeue();
        return true;
    }

    public void Clear()
    {
        _items.Clear();
        _lastText = null;
        _lastAt = long.MinValue / 2;
    }
}
