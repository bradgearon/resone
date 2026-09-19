using System.Text;
using Wds.Resone.Api.VocalSinging;

namespace Wds.Resone.Api;
/// <summary>Local Whisper transcription and Qwen PCM streaming, isolated from composition.</summary>
public sealed class SpeechService : IAsyncDisposable
{
    private readonly HttpClient http;
    private readonly ResoneSettings settings;
    private readonly WhisperCppRuntime whisper;
    private readonly QwenTtsServiceManager ttsManager;

    public SpeechService(HttpClient http, ResoneSettings settings, QwenTtsServiceManager ttsManager)
    {
        this.http = http;
        this.settings = settings;
        this.ttsManager = ttsManager;
        whisper = new WhisperCppRuntime(http, settings);
    }

    /// <summary>Starts loading the bundled whisper.cpp model/runtime while microphone capture continues.</summary>
    public Task<string> WarmTranscriptionAsync(CancellationToken token) => whisper.WarmAsync(token);

    public Task<string> TranscribeAsync(byte[] wav, CancellationToken token) => whisper.TranscribeAsync(wav, token);

    public async Task SpeakAsync(string text, Func<byte[], Task> chunk, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 12000)
            throw new ArgumentException("Speech text must be 1–12000 characters.");
        if (!string.IsNullOrWhiteSpace(settings.TtsReferenceAudioPath))
            throw new NotSupportedException("Reference-audio voices are moving to Resone's local voice source. Use a named Qwen CustomVoice voice for now.");

        string temp = Path.Combine(Path.GetTempPath(), "Resone", "speech", Guid.NewGuid().ToString("N") + ".wav");
        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
        try
        {
            await ttsManager.SynthesizeDefaultWavAsync(text, settings.TtsVoice, temp, token).ConfigureAwait(false);
            byte[] wav = await File.ReadAllBytesAsync(temp, token).ConfigureAwait(false);
            int data = FindWaveData(wav, out int count, out int sampleRate, out short channels, out short bits);
            if (sampleRate != 24000 || channels != 1 || bits != 16)
                throw new InvalidDataException($"Qwen TTS returned unsupported PCM: {sampleRate} Hz, {channels} channels, {bits}-bit.");
            const int chunkBytes = 12000;
            for (int offset = 0; offset < count; offset += chunkBytes)
            {
                token.ThrowIfCancellationRequested();
                int n = Math.Min(chunkBytes, count - offset);
                if ((n & 1) != 0) n--;
                if (n > 0) await chunk(wav.AsSpan(data + offset, n).ToArray()).ConfigureAwait(false);
            }
        }
        finally { try { File.Delete(temp); } catch { } }
    }

    private static int FindWaveData(byte[] wav, out int count, out int sampleRate, out short channels, out short bits)
    {
        if (wav.Length < 44 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF" || Encoding.ASCII.GetString(wav, 8, 4) != "WAVE")
            throw new InvalidDataException("Qwen TTS did not return a WAV file.");
        sampleRate = 0; channels = 0; bits = 0; count = 0;
        int pos = 12;
        while (pos + 8 <= wav.Length)
        {
            string id = Encoding.ASCII.GetString(wav, pos, 4);
            int size = BitConverter.ToInt32(wav, pos + 4);
            int body = pos + 8;
            if (size < 0 || body + size > wav.Length) throw new InvalidDataException("Invalid WAV chunk.");
            if (id == "fmt " && size >= 16)
            {
                short format = BitConverter.ToInt16(wav, body);
                channels = BitConverter.ToInt16(wav, body + 2);
                sampleRate = BitConverter.ToInt32(wav, body + 4);
                bits = BitConverter.ToInt16(wav, body + 14);
                if (format != 1) throw new InvalidDataException("Qwen TTS WAV is not PCM.");
            }
            else if (id == "data")
            {
                count = size;
                if (sampleRate <= 0) throw new InvalidDataException("WAV data appeared before a valid fmt chunk.");
                return body;
            }
            pos = body + size + (size & 1);
        }
        throw new InvalidDataException("Qwen TTS WAV has no data chunk.");
    }
    public ValueTask DisposeAsync() => whisper.DisposeAsync();
}
