using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Wds.Resone.Api;

public sealed class SongWorkspaceSummary
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled Song";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SongWorkspaceIndex
{
    public int Version { get; set; } = 1;
    public string LastSongId { get; set; } = "";
    public List<SongWorkspaceSummary> Songs { get; set; } = [];
}

public sealed class SongWorkspaceMeta
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled Song";
    public string ProducerDesign { get; set; } = "";
    public string ComposerDesign { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SongWorkspaceHistoryEntry
{
    public string Brief { get; set; } = "";
    public string LaneId { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public SongProject Song { get; set; } = new();
}

/// <summary>
/// Durable local song workspace. Each song gets its own folder with project.json,
/// history.json and meta.json; index.json only contains lightweight navigation metadata.
/// Disk access is asynchronous and serialized through an async gate so workspace I/O never
/// blocks the WebSocket request pump or the native editor/UI thread.
/// </summary>
public sealed class SongWorkspaceStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string root;
    private readonly string indexPath;

    public SongWorkspaceStore()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        root = Path.Combine(local, "Wds", "Resone", "user", "songs");
        indexPath = Path.Combine(root, "index.json");
        Directory.CreateDirectory(root);
    }

    public async Task<JsonObject> ListPayloadAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(token).ConfigureAwait(false);
            if (await RepairGenericTitlesAsync(index, token).ConfigureAwait(false))
                await SaveIndexAsync(index, token).ConfigureAwait(false);
            return new JsonObject
            {
                ["lastSongId"] = index.LastSongId,
                ["songs"] = new JsonArray(index.Songs.OrderByDescending(x => x.UpdatedUtc).Select(ToSummaryNode).ToArray())
            };
        }
        finally { gate.Release(); }
    }

    public async Task<JsonObject> LoadPayloadAsync(string id, CancellationToken token = default)
    {
        ValidateId(id);
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            string dir = SongDir(id), projectPath = Path.Combine(dir, "project.json"), metaPath = Path.Combine(dir, "meta.json"), historyPath = Path.Combine(dir, "history.json");
            if (!File.Exists(projectPath) || !File.Exists(metaPath)) throw new FileNotFoundException("Saved song was not found.");
            var project = await ReadJsonAsync(projectPath, ResoneJson.Default.SongProject, token).ConfigureAwait(false)
                ?? throw new InvalidDataException("Saved song project is invalid.");
            var meta = await ReadJsonAsync(metaPath, ResoneJson.Default.SongWorkspaceMeta, token).ConfigureAwait(false)
                ?? throw new InvalidDataException("Saved song metadata is invalid.");
            var history = File.Exists(historyPath)
                ? await ReadJsonAsync(historyPath, ResoneJson.Default.ListSongWorkspaceHistoryEntry, token).ConfigureAwait(false) ?? []
                : [];
            var index = await LoadIndexAsync(token).ConfigureAwait(false);
            index.LastSongId = id;
            await SaveIndexAsync(index, token).ConfigureAwait(false);
            return new JsonObject
            {
                ["id"] = id,
                ["title"] = meta.Title,
                ["producerDesign"] = meta.ProducerDesign,
                ["composerDesign"] = meta.ComposerDesign,
                ["project"] = JsonSerializer.SerializeToNode(project, ResoneJson.Default.SongProject),
                ["history"] = JsonSerializer.SerializeToNode(history, ResoneJson.Default.ListSongWorkspaceHistoryEntry),
                ["songs"] = new JsonArray(index.Songs.OrderByDescending(x => x.UpdatedUtc).Select(ToSummaryNode).ToArray())
            };
        }
        finally { gate.Release(); }
    }

    public async Task<JsonObject> SaveAsync(JsonElement payload, CancellationToken token = default)
    {
        string id = payload.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");
        ValidateId(id);
        string title = NormalizeTitle(payload.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "");
        string? producerDesign = payload.TryGetProperty("producerDesign", out var pd) ? pd.GetString() ?? "" : null;
        string? composerDesign = payload.TryGetProperty("composerDesign", out var cd) ? cd.GetString() ?? "" : null;
        var project = payload.GetProperty("project").Deserialize(ResoneJson.Default.SongProject) ?? throw new ArgumentException("Missing workspace project.");
        List<SongWorkspaceHistoryEntry>? history = null;
        if (payload.TryGetProperty("history", out var h) && h.ValueKind == JsonValueKind.Array)
        {
            history = h.Deserialize(ResoneJson.Default.ListSongWorkspaceHistoryEntry) ?? [];
            if (history.Count > 40) history = history.TakeLast(40).ToList();
        }

        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(token).ConfigureAwait(false);
            var summary = index.Songs.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (summary is null)
            {
                summary = new SongWorkspaceSummary { Id = id, Title = title, CreatedUtc = now, UpdatedUtc = now };
                index.Songs.Add(summary);
            }
            else { summary.Title = title; summary.UpdatedUtc = now; }

            string dir = SongDir(id);
            Directory.CreateDirectory(dir);
            SongWorkspaceMeta meta;
            string metaPath = Path.Combine(dir, "meta.json");
            if (File.Exists(metaPath))
            {
                try { meta = await ReadJsonAsync(metaPath, ResoneJson.Default.SongWorkspaceMeta, token).ConfigureAwait(false) ?? new(); }
                catch (OperationCanceledException) { throw; }
                catch { meta = new(); }
            }
            else meta = new SongWorkspaceMeta { Id = id, CreatedUtc = summary.CreatedUtc };

            meta.Id = id;
            meta.Title = title;
            if (producerDesign is not null)
                meta.ProducerDesign = producerDesign.Length > 24000 ? producerDesign[..24000] : producerDesign;
            if (composerDesign is not null)
                meta.ComposerDesign = composerDesign.Length > 24000 ? composerDesign[..24000] : composerDesign;
            meta.UpdatedUtc = now;

            var writes = new List<Task>
            {
                AtomicJsonAsync(Path.Combine(dir, "project.json"), project, ResoneJson.Default.SongProject, token),
                AtomicJsonAsync(metaPath, meta, ResoneJson.Default.SongWorkspaceMeta, token)
            };
            // History snapshots are much larger than the current project. The browser only
            // sends them when history actually changed, so routine autosaves remain cheap.
            if (history is not null)
                writes.Add(AtomicJsonAsync(Path.Combine(dir, "history.json"), history, ResoneJson.Default.ListSongWorkspaceHistoryEntry, token));
            await Task.WhenAll(writes).ConfigureAwait(false);

            index.LastSongId = id;
            await SaveIndexAsync(index, token).ConfigureAwait(false);
            return new JsonObject
            {
                ["id"] = id,
                ["title"] = title,
                ["songs"] = new JsonArray(index.Songs.OrderByDescending(x => x.UpdatedUtc).Select(ToSummaryNode).ToArray())
            };
        }
        finally { gate.Release(); }
    }

    public async Task<JsonObject> DeleteAsync(string id, CancellationToken token = default)
    {
        ValidateId(id);
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(token).ConfigureAwait(false);
            index.Songs.RemoveAll(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            string dir = SongDir(id);
            if (Directory.Exists(dir))
            {
                try { await Task.Run(() => Directory.Delete(dir, true), token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
            if (string.Equals(index.LastSongId, id, StringComparison.OrdinalIgnoreCase))
                index.LastSongId = index.Songs.OrderByDescending(x => x.UpdatedUtc).FirstOrDefault()?.Id ?? "";
            await SaveIndexAsync(index, token).ConfigureAwait(false);
            return new JsonObject
            {
                ["lastSongId"] = index.LastSongId,
                ["songs"] = new JsonArray(index.Songs.OrderByDescending(x => x.UpdatedUtc).Select(ToSummaryNode).ToArray())
            };
        }
        finally { gate.Release(); }
    }

    private async Task<bool> RepairGenericTitlesAsync(SongWorkspaceIndex index, CancellationToken token)
    {
        bool changed = false;
        foreach (var summary in index.Songs.Where(x => IsGenericTitle(x.Title)))
        {
            token.ThrowIfCancellationRequested();
            string dir = SongDir(summary.Id);
            string metaPath = Path.Combine(dir, "meta.json");
            if (!File.Exists(metaPath)) continue;

            SongWorkspaceMeta? meta;
            try { meta = await ReadJsonAsync(metaPath, ResoneJson.Default.SongWorkspaceMeta, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { continue; }
            if (meta is null) continue;

            string brief = "";
            string historyPath = Path.Combine(dir, "history.json");
            if (File.Exists(historyPath))
            {
                try
                {
                    var history = await ReadJsonAsync(historyPath, ResoneJson.Default.ListSongWorkspaceHistoryEntry, token).ConfigureAwait(false) ?? [];
                    brief = history.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.Brief))?.Brief ?? "";
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            string repaired = SongCompositionDesigner.ExtractTitle(meta.ProducerDesign, brief);
            if (IsGenericTitle(repaired)) continue;
            summary.Title = repaired;
            meta.Title = repaired;
            meta.UpdatedUtc = DateTimeOffset.UtcNow;
            summary.UpdatedUtc = meta.UpdatedUtc;
            await AtomicJsonAsync(metaPath, meta, ResoneJson.Default.SongWorkspaceMeta, token).ConfigureAwait(false);
            changed = true;
        }
        return changed;
    }

    private static bool IsGenericTitle(string? title)
    {
        string normalized = string.Join(' ', (title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim().ToLowerInvariant();
        return normalized is "" or "untitled" or "untitled song" or "new song" or "song" or "new composition";
    }

    private async Task<SongWorkspaceIndex> LoadIndexAsync(CancellationToken token)
    {
        if (!File.Exists(indexPath)) return new();
        try { return await ReadJsonAsync(indexPath, ResoneJson.Default.SongWorkspaceIndex, token).ConfigureAwait(false) ?? new(); }
        catch (OperationCanceledException) { throw; }
        catch { return new(); }
    }

    private Task SaveIndexAsync(SongWorkspaceIndex index, CancellationToken token) =>
        AtomicJsonAsync(indexPath, index, ResoneJson.Default.SongWorkspaceIndex, token);

    private string SongDir(string id) => Path.Combine(root, id);
    private static JsonNode ToSummaryNode(SongWorkspaceSummary x) => new JsonObject { ["id"] = x.Id, ["title"] = x.Title, ["createdUtc"] = x.CreatedUtc.ToString("O"), ["updatedUtc"] = x.UpdatedUtc.ToString("O") };
    private static string NormalizeTitle(string title)
    {
        title = string.Join(' ', (title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (title.Length == 0) title = "Untitled Song";
        if (title.Length > 120) title = title[..120].Trim();
        return title;
    }
    private static void ValidateId(string id) { if (!Guid.TryParseExact(id, "N", out _) && !Guid.TryParseExact(id, "D", out _)) throw new ArgumentException("Invalid song workspace id."); }

    private static async Task<T?> ReadJsonAsync<T>(string path, JsonTypeInfo<T> info, CancellationToken token)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync(input, info, token).ConfigureAwait(false);
    }

    private static async Task AtomicJsonAsync<T>(string path, T value, JsonTypeInfo<T> info, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        try
        {
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(output, value, info, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { }
            }
        }
    }
}
