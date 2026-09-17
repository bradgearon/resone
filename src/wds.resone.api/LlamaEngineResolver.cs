using System.Runtime.InteropServices;

namespace Wds.Resone.Api.Ai;

public static class LlamaEngineResolver
{
    public static string Rid
    {
        get
        {
            string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
            string arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
            };
            return os + "-" + arch;
        }
    }

    public static string Root => Path.GetFullPath(LauncherBootstrap.InstallRoot ?? ResoneRoot.Resolve());

    public static string ResolveUnderRoot(string path)
        => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Root, path));

    public static string EngineDirectory(ResoneSettings settings)
    {
        if (!settings.LlamaEngineDirectories.TryGetValue(Rid, out string? relative) || string.IsNullOrWhiteSpace(relative))
            throw new PlatformNotSupportedException($"No llama.cpp engine directory is configured for {Rid}.");
        string full = ResolveUnderRoot(relative);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"llama.cpp engine directory for {Rid} was not found: {full}");
        return full;
    }

    public static string Bridge(ResoneSettings settings)
    {
        string name = OperatingSystem.IsWindows() ? "resone_llama_bridge.dll" :
            OperatingSystem.IsMacOS() ? "libresone_llama_bridge.dylib" : "libresone_llama_bridge.so";
        string[] candidates =
        [
            ResolveUnderRoot(name),
            ResolveUnderRoot(Path.Combine("build", "ui", "out", "Release", name)),
            ResolveUnderRoot(Path.Combine("build", "inference", "Release", name))
        ];
        string? full = candidates.FirstOrDefault(File.Exists);
        if (full is null)
            throw new FileNotFoundException(
                "Resone's internal llama.cpp ABI bridge is missing. This is not the llama engine; " +
                "the real engine is selected from llamaEngineDirectories. Rebuild Resone so the bridge is produced.",
                candidates[0]);
        return full;
    }

    public static string LlamaLibrary(ResoneSettings settings)
    {
        string engine = EngineDirectory(settings);
        string file = OperatingSystem.IsWindows() ? "llama.dll" : OperatingSystem.IsMacOS() ? "libllama.dylib" : "libllama.so";
        string full = Path.Combine(engine, file);
        if (!File.Exists(full))
            throw new FileNotFoundException($"Configured llama.cpp engine directory for {Rid} does not contain {file}.", full);
        return full;
    }

    public static string Model(ResoneSettings settings)
    {
        string full = ResolveUnderRoot(settings.NativeModelPath);
        if (!File.Exists(full))
            throw new FileNotFoundException("Configured GGUF model was not found.", full);
        return full;
    }
}
