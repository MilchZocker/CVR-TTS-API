using System;
using System.Collections.Generic;

namespace TTSAPI.Tts;

/// <summary>Bounded FIFO of chat messages waiting to be spoken. Drops the oldest at capacity.</summary>
public sealed class TtsQueue
{
    private const int DuplicateWindowMs = 1000;

    private readonly Queue<string> _items = new();
    private readonly int _capacity;

    private string _lastText;
    private long _lastAt = long.MinValue / 2;

    public TtsQueue(int capacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    public int Count => _items.Count;

    public bool Enqueue(string message) => Enqueue(message, Environment.TickCount);

    /// <summary>Enqueues a message; identical text arriving within a second is treated as one send.</summary>
    public bool Enqueue(string message, long nowMs)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (_lastText == message && nowMs - _lastAt <= DuplicateWindowMs)
        {
            _lastAt = nowMs;
            return false;
        }

        if (_items.Count >= _capacity)
            _items.Dequeue();

        _items.Enqueue(message);
        _lastText = message;
        _lastAt = nowMs;
        return true;
    }

    public bool TryDequeue(bool canPlay, out string message)
    {
        message = null;

        if (!canPlay || _items.Count == 0)
            return false;

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
