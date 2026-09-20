using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Wds.Resone.Api.Music;

namespace Wds.Resone.Api.Ai;
public sealed record ChatMessage(string Role, string Content);
public interface ILocalChatModelClient
{
    Task<string> CompleteTextStreamingAsync(IReadOnlyList<ChatMessage> messages, string label, Action<string>? delta, CancellationToken token);
}

public sealed class InstructionLibrary(string root)
{
    public MusicCompositionInstructions LoadMusicCompositionInstructions() => MusicCompositionInstructions.Load(root);
    public string LoadIntervalEmotionGuide() => InstructionContent.Read(root, "interval_emotion_field_guide.md");
}

/// <summary>Scoped extraction of the Six Stars OpenAI SSE flow. Ignores reasoning deltas.</summary>
public sealed class LocalAiClient(HttpClient http, ResoneSettings settings) : ILocalChatModelClient
{
    public async Task<string> CompleteTextStreamingAsync(IReadOnlyList<ChatMessage> messages, string label, Action<string>? delta, CancellationToken token)
    {
        var wire = new JsonArray();
        foreach (var m in messages)
            wire.Add((JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
        var payload = new JsonObject
        {
            ["model"] = settings.LlmModel,
            ["messages"] = wire,
            ["stream"] = true,
            ["temperature"] = .7,
            ["chat_template_kwargs"] = new JsonObject
            {
                ["enable_thinking"] = false
            }
        };
        using var log = new LlmRequestLog(settings, label, payload);
        var result = new StringBuilder();
        try
        {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.LlmUrl)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        log.HttpStatus((int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
            log.Raw(await response.Content.ReadAsStringAsync(token));
        response.EnsureSuccessStatusCode();
        using var body = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(body);
        if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
        {
            string raw = await reader.ReadToEndAsync(token);
            log.Raw(raw);
            using var json = JsonDocument.Parse(raw);
            string content = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            log.Complete(content);
            return content;
        }

        while (await reader.ReadLineAsync(token)is { } line)
        {
            log.Raw(line);
            if (!line.StartsWith("data:"))
                continue;
            string data = line[5..].Trim();
            if (data == "[DONE]")
                break;
            if (data.Length == 0)
                continue;
            using var json = JsonDocument.Parse(data);
            if (!json.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                continue;
            if (choices[0].TryGetProperty("delta", out var d) && d.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
            {
                string text = c.GetString()!;
                result.Append(text);
                delta?.Invoke(text);
            }
        }

        log.Complete(result.ToString());
        return result.ToString();
        }
        catch (Exception error)
        {
            log.Fail(error, result.ToString());
            throw;
        }
    }
}
