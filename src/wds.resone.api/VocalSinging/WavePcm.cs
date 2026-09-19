using System.Text;

namespace Wds.Resone.Api.VocalSinging;

internal static class WavePcm
{
    public sealed record Audio(float[] Samples, int SampleRate);

    public static Audio ReadMono24k(string path) => ReadMono24k(File.ReadAllBytes(path));

    public static Audio ReadMono24k(byte[] wav)
    {
        if (wav.Length < 44 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF" || Encoding.ASCII.GetString(wav, 8, 4) != "WAVE")
            throw new InvalidDataException("Voice sample must be a RIFF/WAVE file.");

        int sampleRate = 0, channels = 0, bits = 0, format = 0, dataOffset = 0, dataBytes = 0;
        int pos = 12;
        while (pos + 8 <= wav.Length)
        {
            string id = Encoding.ASCII.GetString(wav, pos, 4);
            int size = BitConverter.ToInt32(wav, pos + 4);
            int body = pos + 8;
            if (size < 0 || body + size > wav.Length) throw new InvalidDataException("Invalid WAV chunk length.");
            if (id == "fmt " && size >= 16)
            {
                format = BitConverter.ToInt16(wav, body);
                channels = BitConverter.ToInt16(wav, body + 2);
                sampleRate = BitConverter.ToInt32(wav, body + 4);
                bits = BitConverter.ToInt16(wav, body + 14);
            }
            else if (id == "data")
            {
                dataOffset = body; dataBytes = size;
            }
            pos = body + size + (size & 1);
        }
        if (dataOffset == 0 || sampleRate < 8000 || sampleRate > 192000 || channels is < 1 or > 8)
            throw new InvalidDataException("WAV is missing supported PCM audio data.");
        if (!((format == 1 && bits is 16 or 24 or 32) || (format == 3 && bits == 32)))
            throw new InvalidDataException($"Unsupported WAV format: format={format}, bits={bits}. Use PCM16/24/32 or float32 WAV.");

        int bytesPerSample = bits / 8;
        int frameBytes = checked(bytesPerSample * channels);
        int frames = dataBytes / frameBytes;
        if (frames <= 0) throw new InvalidDataException("WAV contains no audio frames.");
        var mono = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            double sum = 0;
            int baseOffset = dataOffset + frame * frameBytes;
            for (int ch = 0; ch < channels; ch++)
            {
                int o = baseOffset + ch * bytesPerSample;
                float v;
                if (format == 3) v = BitConverter.ToSingle(wav, o);
                else if (bits == 16) v = BitConverter.ToInt16(wav, o) / 32768f;
                else if (bits == 24)
                {
                    int raw = wav[o] | (wav[o + 1] << 8) | (wav[o + 2] << 16);
                    if ((raw & 0x800000) != 0) raw |= unchecked((int)0xff000000);
                    v = raw / 8388608f;
                }
                else v = BitConverter.ToInt32(wav, o) / 2147483648f;
                sum += Math.Clamp(v, -1f, 1f);
            }
            mono[frame] = (float)(sum / channels);
        }

        if (sampleRate == 24000) return new Audio(mono, 24000);
        int outputFrames = Math.Max(1, (int)Math.Round(mono.Length * 24000.0 / sampleRate));
        var resampled = new float[outputFrames];
        double ratio = sampleRate / 24000.0;
        for (int i = 0; i < outputFrames; i++)
        {
            double source = i * ratio;
            int a = Math.Min(mono.Length - 1, (int)source);
            int b = Math.Min(mono.Length - 1, a + 1);
            float f = (float)(source - a);
            resampled[i] = mono[a] + (mono[b] - mono[a]) * f;
        }
        return new Audio(resampled, 24000);
    }

    public static byte[] WritePcm16Wav(float[] samples, int sampleRate = 24000)
    {
        using var stream = new MemoryStream(44 + samples.Length * 2);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        int dataBytes = checked(samples.Length * 2);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataBytes); writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataBytes);
        foreach (float sample in samples)
        {
            short value = (short)Math.Clamp((int)Math.Round(Math.Clamp(sample, -1f, 1f) * 32767f), short.MinValue, short.MaxValue);
            writer.Write(value);
        }
        writer.Flush();
        return stream.ToArray();
    }

    public static void NormalizeToFile(byte[] wav, string outputPath)
    {
        var audio = ReadMono24k(wav);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllBytes(outputPath, WritePcm16Wav(audio.Samples, audio.SampleRate));
    }
}
