using Wds.Resone.Api;

namespace Wds.Resone.Launcher;

internal static class AiRuntimeLocation
{
    public static string Configure(string[] args)
    {
        string? commandLine = ReadArgument(args, "--ai-root");
        string? environment = Environment.GetEnvironmentVariable(AiRuntimeRoot.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(environment))
            environment = Environment.GetEnvironmentVariable(AiRuntimeRoot.LegacyEnvironmentVariable);

        string root;
        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            root = Normalize(commandLine);
            Persist(root);
        }
        else if (!string.IsNullOrWhiteSpace(environment))
        {
            root = Normalize(environment);
        }
        else
        {
            root = AiRuntimeRoot.Resolve();
        }

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "engines"));
        Directory.CreateDirectory(Path.Combine(root, "models"));
        Directory.CreateDirectory(Path.Combine(root, "work"));
        Environment.SetEnvironmentVariable(AiRuntimeRoot.EnvironmentVariable, root);
        return root;
    }

    private static string? ReadArgument(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string value = args[i];
            if (value.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    throw new ArgumentException(name + " requires a path.");
                return args[i + 1];
            }
            string prefix = name + "=";
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return value[prefix.Length..];
        }
        return null;
    }

    private static string Normalize(string value)
        => Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')));

    private static void Persist(string root)
    {
        string file = AiRuntimeRoot.PreferenceFile;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string temp = file + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, root);
        File.Move(temp, file, true);
    }
}
