using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wds.Resone.Launcher;
public sealed class RuntimeConfig
{
    public string Backend { get; set; } = "auto";
    public bool RequireHashes { get; set; }
    public List<EnginePack> EnginePacks { get; set; } = [];
    public bool UseExistingStack { get; set; } = true;
    public List<ModelFile> Models { get; set; } = [];
    public List<ServiceSpec> Services { get; set; } = [];
}

public sealed class ModelFile
{
    public string Path { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class ServiceSpec
{
    public string Name { get; set; } = "";
    public string Executable { get; set; } = "";
    public List<string> Arguments { get; set; } = [];
    public bool Enabled { get; set; } = true;
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RuntimeConfig))]
internal partial class RuntimeJson : JsonSerializerContext;
/// <summary>Manifest-based download/start/stop extraction from Six Stars. No health polling.</summary>
public sealed class ServiceSupervisor(string root, HttpClient http, Action<string>? onStatus = null) : IDisposable
{
    private readonly List<Process> _owned = [];
    private void Report(string message){Console.WriteLine(message);onStatus?.Invoke(message); }
    public string SelectedBackend { get; private set; }="cpu";
    public async Task StartAsync(CancellationToken token)
    {
        string path = System.IO.Path.Combine(root, "config", "runtime.json");
        if (!File.Exists(path))
            return;
        var config = JsonSerializer.Deserialize(await File.ReadAllTextAsync(path, token), RuntimeJson.Default.RuntimeConfig)!;
        if (config.UseExistingStack)
        {
            Report("Using the existing AI stack; Resone will not download models or start/stop AI services.");
            return;
        }
        var backend=EnginePackInstaller.Backend(config);SelectedBackend=backend;
        var packs=config.EnginePacks.Where(p=>p.Rid==EnginePackInstaller.Rid).GroupBy(p=>p.Directory)
            .Select(group=>group.FirstOrDefault(p=>p.Backend==backend)??group.FirstOrDefault(p=>p.Backend=="cpu")??throw new InvalidDataException("No compatible engine pack for "+group.Key));
        foreach(var pack in packs)
            await EnginePackInstaller.InstallAsync(root,pack,http,Report,token);
        foreach(var model in config.Models.Where(m=>m.Enabled))
            if(config.RequireHashes && (model.Sha256.Length!=64 || !model.Sha256.All(Uri.IsHexDigit))) throw new InvalidDataException("Model hash required: "+model.Path);
        foreach (var model in config.Models.Where(m => m.Enabled))
            await DownloadAsync(model, token);
        var appSettingsPath=Path.Combine(root,"config","appsettings.json");
        var native=File.Exists(appSettingsPath) && (JsonSerializer.Deserialize(await File.ReadAllTextAsync(appSettingsPath,token),Wds.Resone.Api.ResoneJson.Default.ResoneSettings)?.NativeInference??false);
#if RESONE_CUSTOMER_RELEASE
        native=true;
#endif
        foreach (var spec in config.Services.Where(s => s.Enabled && !(native && s.Name.Equals("LLM",StringComparison.OrdinalIgnoreCase))))
        {
            token.ThrowIfCancellationRequested();
            var start = new ProcessStartInfo(Full(spec.Executable))
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var arg in spec.Arguments)
                start.ArgumentList.Add(arg.Replace("{root}", root));
            if (!File.Exists(start.FileName))
                throw new FileNotFoundException("Install the native runtime: " + start.FileName);
            var process = new Process
            {
                StartInfo = start,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    Console.WriteLine($"[{spec.Name}] {e.Data}");
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    Console.WriteLine($"[{spec.Name}] {e.Data}");
            };
            process.Exited += (_, _) => Report($"[{spec.Name}] exited");
            process.Start();
            _owned.Add(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
    }

    private string Full(string relative)
    {
        string value = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
        if (!value.StartsWith(System.IO.Path.GetFullPath(root) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Runtime paths must remain beneath Resone.");
        return value;
    }

    private async Task DownloadAsync(ModelFile model, CancellationToken token)
    {
        string destination = Full(model.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            try{await Verify(destination, model.Sha256, token);return;}
            catch(InvalidDataException){Report("Replacing a model that failed its configured hash: "+model.Path);}
        }

        string partial = destination + ".partial";
        long offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        using var request = new HttpRequestMessage(HttpMethod.Get, model.Url);
        if (offset > 0)
            request.Headers.Range = new RangeHeaderValue(offset, null);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        bool resume = offset > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (resume && response.Content.Headers.ContentRange?.From != offset)
            throw new InvalidDataException("Model range mismatch.");
        Report("Downloading " + model.Path);
        await using (var output = new FileStream(partial, resume ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
            await response.Content.CopyToAsync(output, token);
        if (new FileInfo(partial).Length == 0)
            throw new InvalidDataException("Empty model download.");
        try{await Verify(partial, model.Sha256, token);}catch(InvalidDataException){File.Delete(partial);throw;}
        File.Move(partial, destination, true);
    }

    private static async Task Verify(string path, string sha, CancellationToken token)
    {
        if (sha.Length == 0)
            return;
        await using var f = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(f, token));
        if (!actual.Equals(sha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Model SHA256 mismatch: " + path);
    }

    public void Dispose()
    {
        foreach (var p in _owned)
        {
            try
            {
                if (!p.HasExited)
                    p.Kill(true);
            }
            catch
            {
            }

            p.Dispose();
        }
    }
}
