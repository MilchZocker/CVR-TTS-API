using System;

namespace TTSAPI.Tts;

/// <summary>
/// Converts arbitrary WAV output into the fixed format CVR's TTS pipeline expects.
/// CVR's own resampler only handles integer ratios (44.1 kHz would play fast), so the
/// mod does the conversion itself and always publishes 48 kHz mono.
/// </summary>
public static class PcmConverter
{
    public const int TargetSampleRate = 48000;

    public static short[] ToMono48k(TtsAudio audio)
    {
        if (audio?.Samples == null || audio.Samples.Length == 0)
            return Array.Empty<short>();

        int channels = Math.Max(1, audio.Channels);
        short[] mono = channels == 1 ? audio.Samples : Downmix(audio.Samples, channels);

        if (audio.SampleRate == TargetSampleRate)
            return mono;

        return Resample(mono, Math.Max(1, audio.SampleRate), TargetSampleRate);
    }

    private static short[] Downmix(short[] samples, int channels)
    {
        int frames = samples.Length / channels;
        var mono = new short[frames];

        for (int frame = 0; frame < frames; frame++)
        {
            int sum = 0;
            int index = frame * channels;
            for (int channel = 0; channel < channels; channel++)
                sum += samples[index + channel];

            mono[frame] = (short)(sum / channels);
        }

        return mono;
    }

    /// <summary>Applies a playback gain (0..2) and clamps to 16-bit full scale.</summary>
    public static short[] ApplyGain(short[] samples, double gain)
    {
        if (samples == null || samples.Length == 0)
            return Array.Empty<short>();

        double clamped = Math.Clamp(gain, 0.0, 2.0);
        if (Math.Abs(clamped - 1.0) < 1e-9)
            return samples;

        var output = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            double value = samples[i] * clamped;
            if (value > 32767d)
                value = 32767d;
            if (value < -32768d)
                value = -32768d;
            output[i] = (short)Math.Round(value);
        }

        return output;
    }

    /// <summary>
    /// Fades the last few milliseconds out and appends silence so the message does not
    /// end mid-waveform. The final sample is a 1-LSB marker: CVR's <c>Trim</c> cuts
    /// everything after the last positive sample, which would otherwise delete the tail.
    /// </summary>
    public static short[] WithTail(short[] samples, int fadeMs = 15, int silenceMs = 200)
    {
        if (samples == null || samples.Length == 0)
            return Array.Empty<short>();

        int fade = Math.Min(Math.Max(0, fadeMs) * TargetSampleRate / 1000, samples.Length);
        int silence = Math.Max(0, silenceMs) * TargetSampleRate / 1000;

        var output = new short[samples.Length + silence];
        Array.Copy(samples, output, samples.Length);

        for (int i = 0; i < fade; i++)
        {
            int index = samples.Length - fade + i;
            double gain = 1.0 - (double)(i + 1) / fade;
            output[index] = (short)Math.Round(samples[index] * gain);
        }

        if (silence > 0)
            output[output.Length - 1] = 1;

        return output;
    }

    private static short[] Resample(short[] input, int sourceRate, int targetRate)    {
        int length = (int)Math.Round(input.Length * (double)targetRate / sourceRate);
        if (length <= 0)
            return Array.Empty<short>();

        var output = new short[length];
        double step = (double)sourceRate / targetRate;

        for (int i = 0; i < length; i++)
        {
            double position = i * step;
            int index = (int)position;
            double fraction = position - index;

            int a = input[Math.Min(index, input.Length - 1)];
            int b = input[Math.Min(index + 1, input.Length - 1)];
            output[i] = (short)Math.Round(a + (b - a) * fraction);
        }

        return output;
    }
}
