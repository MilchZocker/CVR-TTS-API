using System;
using System.Text;

namespace TTSAPI.Tts;

public static class WavParser
{
    public static bool TryParse(byte[] wav, out TtsAudio audio, out string error)
    {
        audio = null;
        error = null;

        if (wav == null || wav.Length < 12)
        {
            error = "not a WAV file (too short)";
            return false;
        }

        if (!HasTag(wav, 0, "RIFF") || !HasTag(wav, 8, "WAVE"))
        {
            error = "not a RIFF/WAVE file";
            return false;
        }

        int format = -1;
        int channels = 0;
        int sampleRate = 0;
        int bits = 0;
        int dataOffset = -1;
        int dataLength = 0;

        int pos = 12;
        while (pos + 8 <= wav.Length)
        {
            string id = Encoding.ASCII.GetString(wav, pos, 4);
            int size = BitConverter.ToInt32(wav, pos + 4);
            int body = pos + 8;

            if (size < 0)
            {
                error = "invalid chunk size";
                return false;
            }

            if (id == "fmt ")
            {
                if (body + 16 > wav.Length)
                {
                    error = "truncated fmt chunk";
                    return false;
                }

                format = BitConverter.ToUInt16(wav, body);
                channels = BitConverter.ToUInt16(wav, body + 2);
                sampleRate = BitConverter.ToInt32(wav, body + 4);
                bits = BitConverter.ToUInt16(wav, body + 14);

                if (format == 0xFFFE)
                {
                    if (body + 26 > wav.Length)
                    {
                        error = "truncated extensible fmt chunk";
                        return false;
                    }

                    format = BitConverter.ToUInt16(wav, body + 24);
                }
            }
            else if (id == "data")
            {
                dataOffset = body;
                dataLength = Math.Max(0, Math.Min(size, wav.Length - body));
                break;
            }

            int advance = size + (size & 1);
            if (advance <= 0 || body + advance > wav.Length)
            {
                error = "truncated chunk";
                return false;
            }

            pos = body + advance;
        }

        if (format < 0)
        {
            error = "missing fmt chunk";
            return false;
        }

        if (dataOffset < 0)
        {
            error = "missing data chunk";
            return false;
        }

        if (format != 1 && format != 3)
        {
            error = $"unsupported audio format {format}";
            return false;
        }

        if (format == 3 && bits != 32)
        {
            error = $"unsupported float bit depth {bits}";
            return false;
        }

        if (channels < 1)
        {
            error = "invalid channel count";
            return false;
        }

        if (sampleRate < 1)
        {
            error = "invalid sample rate";
            return false;
        }

        if (bits != 8 && bits != 16 && bits != 24 && bits != 32)
        {
            error = $"unsupported bit depth {bits}";
            return false;
        }

        int bytesPerSample = bits / 8;
        int frameBytes = bytesPerSample * channels;
        int frames = dataLength / frameBytes;

        if (frames <= 0)
        {
            error = "empty audio data";
            return false;
        }

        var samples = new short[frames * channels];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = ReadSample(wav, dataOffset + i * bytesPerSample, format, bits);
        }

        audio = new TtsAudio
        {
            Samples = samples,
            SampleRate = sampleRate,
            Channels = channels
        };
        return true;
    }

    private static short ReadSample(byte[] data, int offset, int format, int bits)
    {
        if (format == 3)
            return FloatToPcm16(BitConverter.ToSingle(data, offset));

        switch (bits)
        {
            case 8:
                return (short)((data[offset] - 128) << 8);

            case 16:
                return BitConverter.ToInt16(data, offset);

            case 24:
                int v = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
                if ((v & 0x800000) != 0)
                    v |= unchecked((int)0xFF000000);
                return (short)(v >> 8);

            default:
                return (short)(BitConverter.ToInt32(data, offset) >> 16);
        }
    }

    private static short FloatToPcm16(float value)
    {
        double scaled = Math.Round(Math.Clamp(value, -1f, 1f) * 32768d);
        if (scaled > 32767d)
            scaled = 32767d;
        if (scaled < -32768d)
            scaled = -32768d;
        return (short)scaled;
    }

    private static bool HasTag(byte[] data, int offset, string tag)
    {
        if (offset + tag.Length > data.Length)
            return false;

        for (int i = 0; i < tag.Length; i++)
        {
            if (data[offset + i] != tag[i])
                return false;
        }

        return true;
    }
}
