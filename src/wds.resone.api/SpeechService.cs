using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wds.Resone.Api;
/// <summary>Whisper file submission and Qwen PCM streaming, isolated from composition.</summary>
public sealed class SpeechService(HttpClient http, ResoneSettings settings)
{
    public async Task<string> TranscribeAsync(byte[] wav, CancellationToken token)
    {
        if (wav.Length < 44 || wav.Length > 6000000)
            throw new ArgumentException("Voice recording must be a WAV of at most 90 seconds.");
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(wav);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "file", "recording.wav");
        form.Add(new StringContent("json"), "response_format");
        using var response = await http.PostAsync(settings.SttUrl, form, token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return json.RootElement.GetProperty("text").GetString()?.Trim() ?? "";
    }

    public async Task SpeakAsync(string text, Func<byte[], Task> chunk, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 12000)
            throw new ArgumentException("Speech text must be 1–12000 characters.");
        if (!string.IsNullOrWhiteSpace(settings.TtsReferenceAudioPath))
        {
            var path = Path.IsPathRooted(settings.TtsReferenceAudioPath) ? settings.TtsReferenceAudioPath : Path.Combine(Environment.GetEnvironmentVariable("RESONE_HOME") ?? AppContext.BaseDirectory, settings.TtsReferenceAudioPath);
            if (new FileInfo(path).Length > 10 * 1024 * 1024)
                throw new ArgumentException("TTS reference WAV exceeds 10 MiB.");
            var registration = new JsonObject
            {
                ["name"] = settings.TtsVoice,
                ["wav_b64"] = Convert.ToBase64String(await File.ReadAllBytesAsync(path, token))
            };
            if (!string.IsNullOrWhiteSpace(settings.TtsReferenceText))
                registration["ref_text"] = settings.TtsReferenceText;
            var endpoint = new Uri(new Uri(settings.TtsUrl), "/v1/audio/voices");
            using var voice = await http.PostAsync(endpoint, new StringContent(registration.ToJsonString(), Encoding.UTF8, "application/json"), token);
            voice.EnsureSuccessStatusCode();
        }

        var data = new JsonObject
        {
            ["model"] = "qwen3-tts-base",
            ["input"] = text,
            ["voice"] = settings.TtsVoice,
            ["response_format"] = "pcm"
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.TtsUrl)
        {
            Content = new StringContent(data.ToJsonString(), Encoding.UTF8, "application/json")
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = new byte[12000];
        int count = 0;
        while (true)
        {
            int n = await stream.ReadAsync(bytes.AsMemory(count), token);
            if (n == 0)
                break;
            count += n;
            if (count == bytes.Length)
            {
                await chunk(bytes.ToArray());
                count = 0;
            }
        }

        if (count % 2 != 0)
            throw new InvalidDataException("Truncated PCM sample from TTS.");
        if (count > 0)
            await chunk(bytes[..count]);
    }
}
