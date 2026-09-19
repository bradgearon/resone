using NAudio.Wave;
using System.Text.Json;
using System.Text.Json.Nodes;
using Wds.Resone.Api;
using Wds.Resone.Api.VocalSinging;
using Wds.Resone.Resonator;

namespace Wds.Resone.VoiceDev;

internal static class Program
{
    private const string Lyrics = "We develop software";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || Has(args, "--help") || Has(args, "-h"))
            {
                PrintHelp();
                return args.Length == 0 ? 1 : 0;
            }

            if (args.Length == 1 && Eq(args[0], "--list"))
            {
                foreach (var listedMelody in VoicePreviewMelodies.All)
                    Console.WriteLine($"{listedMelody.Id,-14} {listedMelody.Name,-16} {listedMelody.Feeling}");
                return 0;
            }

            var store = new VoiceLibraryStore();
            if (args.Length == 1 && Eq(args[0], "--voices"))
            {
                foreach (var listedVoice in store.Load().Voices.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"{listedVoice.Name,-28} {listedVoice.Id}");
                return 0;
            }

            if (args.Length < 2)
                throw new ArgumentException("Usage: resone-voice <voice> <melody>. Example: resone-voice peppy amazing");

            string requestedVoice = args[0];
            string requestedMelody = args[1];
            bool refreshSource = Has(args, "--refresh-source");
            bool noCache = Has(args, "--no-cache");
            bool noPlay = Has(args, "--no-play");
            bool reprofile = Has(args, "--reprofile");
            string root = ResolveRoot(Value(args, "--resone-root"));
            string outputRoot = Path.GetFullPath(Value(args, "--out") ?? Path.Combine(AppContext.BaseDirectory, "out"));

            var melody = VoicePreviewMelodies.All.FirstOrDefault(x => Eq(x.Id, requestedMelody))
                ?? throw new KeyNotFoundException($"Unknown melody '{requestedMelody}'. Run resone-voice --list.");
            var voice = FindVoice(store.Load().Voices, requestedVoice);
            voice = reprofile ? store.ReanalyzePitchProfile(voice) : store.EnsurePitchProfile(voice);
            string samplePath = store.SamplePath(voice);
            if (!File.Exists(samplePath)) throw new FileNotFoundException("Saved voice reference WAV was not found.", samplePath);
            if (string.IsNullOrWhiteSpace(voice.Transcript)) throw new InvalidDataException($"Voice '{voice.Name}' has no reference transcription.");

            string slug = $"{Slug(voice.Name)}-{Slug(melody.Id)}";
            string runDir = Path.Combine(outputRoot, slug);
            string cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
            Directory.CreateDirectory(runDir);
            Directory.CreateDirectory(cacheDir);

            string midiPath = Path.Combine(runDir, "melody.mid");
            string sourcePath = Path.Combine(runDir, "source.wav");
            string singingPath = Path.Combine(runDir, "singing.wav");
            string cachedSource = Path.Combine(cacheDir, $"{voice.Id}-we-develop-software.wav");

            Console.WriteLine($"Voice   : {voice.Name}");
            Console.WriteLine($"Register: base {MidiName(voice.PitchProfile.BaseMidiNote)} / octave {voice.PitchProfile.BaseOctave}; singing {MidiName(voice.PitchProfile.SingingMinMidiNote)}-{MidiName(voice.PitchProfile.SingingMaxMidiNote)}");
            Console.WriteLine($"Melody  : {melody.Name} ({melody.Id})");
            Console.WriteLine($"Feeling : {melody.Feeling}");
            Console.WriteLine($"Root    : {root}");

            var midi = new ResonatorMidiGenerator().Generate(melody.Notation);
            await File.WriteAllBytesAsync(midiPath, midi.MidiBytes).ConfigureAwait(false);

            if (!refreshSource && !noCache && File.Exists(cachedSource))
            {
                File.Copy(cachedSource, sourcePath, true);
                Console.WriteLine("Source  : cached Qwen phrase");
            }
            else
            {
                Console.WriteLine("Source  : generating fluent Qwen phrase…");
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                var settings = LoadSettings(root);
                await using var tts = new QwenTtsServiceManager(settings, root, http);
                await tts.SynthesizeSavedVoiceWavAsync(
                    VocalSingingEngine.PrepareSourceSpeechText(Lyrics),
                    voice,
                    samplePath,
                    sourcePath,
                    CancellationToken.None,
                    message =>
                    {
                        Console.WriteLine(message);
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                if (!noCache) File.Copy(sourcePath, cachedSource, true);
            }

            Console.WriteLine("Render  : native/vocals/resone_vocals.cpp");
            await new VocalSingingEngine().RenderAsync(new VocalSingingRequest
            {
                InputSpeechWavPath = sourcePath,
                SpeechText = Lyrics,
                VocalMidiPath = midiPath,
                OutputSingingWavPath = singingPath,
                Guidance = Array.Empty<VocalGuidanceEvent>(),
                Options = VocalSingingEngine.OptionsForVoice(voice.PitchProfile)
            }).ConfigureAwait(false);

            var manifest = new JsonObject
            {
                ["voice"] = voice.Name,
                ["voiceId"] = voice.Id,
                ["referenceWav"] = samplePath,
                ["referenceTranscript"] = voice.Transcript,
                ["lyrics"] = Lyrics,
                ["melody"] = melody.Id,
                ["melodyName"] = melody.Name,
                ["melodyFeeling"] = melody.Feeling,
                ["notation"] = melody.Notation,
                ["voiceBasePitchHz"] = voice.PitchProfile.BasePitchHz,
                ["voiceBaseMidi"] = voice.PitchProfile.BaseMidiNote,
                ["voiceBaseOctave"] = voice.PitchProfile.BaseOctave,
                ["singingMinMidi"] = voice.PitchProfile.SingingMinMidiNote,
                ["singingMaxMidi"] = voice.PitchProfile.SingingMaxMidiNote,
                ["sourceWav"] = sourcePath,
                ["midi"] = midiPath,
                ["output"] = singingPath,
                ["utc"] = DateTimeOffset.UtcNow.ToString("O")
            };
            await File.WriteAllTextAsync(Path.Combine(runDir, "run.json"), manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);

            Console.WriteLine($"Done    : {singingPath}");
            if (!noPlay)
            {
                Console.WriteLine("Play    : DirectSound");
                await PlayDirectSoundAsync(singingPath).ConfigureAwait(false);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            if (Environment.GetEnvironmentVariable("RESONE_VOICE_STACK") == "1") Console.Error.WriteLine(ex);
            return 2;
        }
    }

    private static async Task PlayDirectSoundAsync(string wavPath)
    {
        using var reader = new AudioFileReader(wavPath);
        using var output = new DirectSoundOut(100);
        var stopped = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<StoppedEventArgs>? handler = null;
        handler = (_, e) =>
        {
            if (e.Exception is not null) stopped.TrySetException(e.Exception);
            else stopped.TrySetResult(null);
        };
        output.PlaybackStopped += handler;
        try
        {
            output.Init(reader);
            output.Play();
            await stopped.Task.ConfigureAwait(false);
        }
        finally
        {
            output.PlaybackStopped -= handler;
        }
    }

    private static ResoneSettings LoadSettings(string root)
    {
        string path = Path.Combine(root, "config", "appsettings.json");
        if (!File.Exists(path)) return new ResoneSettings();
        return JsonSerializer.Deserialize(File.ReadAllText(path), ResoneJson.Default.ResoneSettings) ?? new ResoneSettings();
    }

    private static SavedVoice FindVoice(IReadOnlyList<SavedVoice> voices, string requested)
    {
        var exact = voices.FirstOrDefault(v => Eq(v.Name, requested) || Eq(v.Id, requested));
        if (exact is not null) return exact;
        var prefix = voices.Where(v => v.Name.StartsWith(requested, StringComparison.OrdinalIgnoreCase)).ToArray();
        return prefix.Length switch
        {
            1 => prefix[0],
            > 1 => throw new InvalidOperationException($"Voice '{requested}' is ambiguous: {string.Join(", ", prefix.Select(x => x.Name))}"),
            _ => throw new KeyNotFoundException($"Saved Resone voice '{requested}' was not found. Run resone-voice --voices.")
        };
    }

    private static string ResolveRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot)) return ValidateRoot(explicitRoot);
        string? env = Environment.GetEnvironmentVariable("RESONE_ROOT");
        if (!string.IsNullOrWhiteSpace(env)) return ValidateRoot(env);

        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var cursor = new DirectoryInfo(Path.GetFullPath(start));
            for (int i = 0; i < 10 && cursor is not null; i++, cursor = cursor.Parent)
                if (LooksLikeRoot(cursor.FullName)) return cursor.FullName;
        }

        string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wds", "Resone");
        if (LooksLikeRoot(installed)) return installed;
        throw new DirectoryNotFoundException("Could not locate the Resone root. Run from build\\voice inside the Resone tree, pass --resone-root, or set RESONE_ROOT.");
    }

    private static string ValidateRoot(string path)
    {
        string full = Path.GetFullPath(path);
        if (!LooksLikeRoot(full)) throw new DirectoryNotFoundException($"'{full}' is not a Resone runtime/source root with config, engines/tts and models/tts.");
        return full;
    }

    private static bool LooksLikeRoot(string path) =>
        File.Exists(Path.Combine(path, "config", "appsettings.json")) &&
        Directory.Exists(Path.Combine(path, "engines", "tts")) &&
        Directory.Exists(Path.Combine(path, "models", "tts"));

    private static bool Has(string[] args, string value) => args.Any(x => Eq(x, value));
    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string? Value(string[] args, string name)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (Eq(args[i], name)) return args[i + 1];
        return null;
    }

    private static string Slug(string value)
    {
        var chars = value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string MidiName(int note)
    {
        if (note is < 0 or > 127) return "?";
        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        return names[note % 12] + (note / 12 - 1);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Resone vocal renderer development command");
        Console.WriteLine();
        Console.WriteLine("  .\\build\\voice\\resone-voice.exe peppy amazing");
        Console.WriteLine();
        Console.WriteLine("Uses the real Resone voice library, Qwen service manager, preview melodies,");
        Console.WriteLine("MIDI generator, and native/vocals/resone_vocals.cpp in the current source tree.");
        Console.WriteLine();
        Console.WriteLine("  --list             list the 20 development melodies");
        Console.WriteLine("  --voices           list saved Resone voices");
        Console.WriteLine("  --refresh-source   regenerate the Qwen source phrase instead of using cache");
        Console.WriteLine("  --no-cache         do not read or write the source phrase cache");
        Console.WriteLine("  --no-play          render without playing the result through DirectSound");
        Console.WriteLine("  --reprofile        force re-analysis of the saved voice register before rendering");
        Console.WriteLine("  --out <dir>        override output directory (default build\\voice\\out)");
        Console.WriteLine("  --resone-root <d>  explicitly select the Resone source/runtime root");
    }
}
