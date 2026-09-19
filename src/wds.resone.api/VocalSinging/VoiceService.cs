using System.Text.Json;
using System.Text.Json.Nodes;
using Wds.Resone.Resonator;

namespace Wds.Resone.Api.VocalSinging;

public sealed class VoiceService(HttpClient http, ResoneSettings settings, SpeechService speechService, QwenTtsServiceManager ttsManager)
{
    public const string DefaultSampleText = "Thank you for using Resone by We Develop Software, I can't wait to hear what you create.";
    private const string SingingPreviewText = "We develop software";
    private readonly VoiceLibraryStore store = new();

    public JsonObject Library() => store.ToPayload();

    public async Task<JsonObject> DesignPreviewAsync(JsonElement payload, CancellationToken token, Func<string, Task>? progress = null)
    {
        string description = payload.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        string sampleText = payload.TryGetProperty("sampleText", out var s) ? s.GetString() ?? DefaultSampleText : DefaultSampleText;
        bool singing = payload.TryGetProperty("renderSinging", out var rs) && rs.GetBoolean();
        string melodyId = payload.TryGetProperty("melodyId", out var mi) ? mi.GetString() ?? "fun" : "fun";
        if (description.Trim().Length is < 3 or > 2000) throw new ArgumentException("Describe the voice in 3–2000 characters.");
        ValidateSampleText(sampleText);

        string id = Guid.NewGuid().ToString("N"), dir = store.NewPreviewDirectory(id), sample = Path.Combine(dir, "sample.wav");
        if (progress is not null) await progress("Voice Designer · Generating voice sample…").ConfigureAwait(false);
        await ttsManager.SynthesizeVoiceDesignWavAsync(sampleText.Trim(), description.Trim(), sample, token, progress).ConfigureAwait(false);
        string transcript = await TranscribeValidatedAsync(sample, token, progress).ConfigureAwait(false);
        var pitchProfile = await AnalyzePitchProfileAsync(sample, token, progress).ConfigureAwait(false);

        string singingFile = "";
        if (singing)
        {
            singingFile = "singing.wav";
            await RenderSingingPreviewAsync(sample, transcript, pitchProfile, Path.Combine(dir, singingFile), melodyId, token, progress).ConfigureAwait(false);
        }
        var preview = new VoicePreviewRecord
        {
            Id = id, Source = "designed", SampleFile = "sample.wav", Transcript = transcript,
            DesignPrompt = description.Trim(), SampleText = sampleText.Trim(), SingingFile = singingFile, MelodyId = VoicePreviewMelodies.Get(melodyId).Id, PitchProfile = pitchProfile
        };
        store.SavePreview(preview);
        return PreviewPayload(preview, dir);
    }

    public async Task<JsonObject> ImportPreviewAsync(JsonElement payload, CancellationToken token, Func<string, Task>? progress = null)
    {
        string b64 = payload.TryGetProperty("wav", out var w) ? w.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(b64)) throw new ArgumentException("Choose or drop a WAV file first.");
        byte[] wav;
        try { wav = Convert.FromBase64String(b64); } catch { throw new ArgumentException("Imported voice WAV data is invalid."); }
        if (wav.Length > 10 * 1024 * 1024) throw new ArgumentException("Imported voice WAV must be 10 MiB or smaller.");
        bool singing = payload.TryGetProperty("renderSinging", out var rs) && rs.GetBoolean();
        string melodyId = payload.TryGetProperty("melodyId", out var mi) ? mi.GetString() ?? "fun" : "fun";

        string id = Guid.NewGuid().ToString("N"), dir = store.NewPreviewDirectory(id), sample = Path.Combine(dir, "sample.wav");
        if (progress is not null) await progress("Voice Designer · Normalizing imported WAV…").ConfigureAwait(false);
        WavePcm.NormalizeToFile(wav, sample);
        string transcript = await TranscribeValidatedAsync(sample, token, progress).ConfigureAwait(false);
        var pitchProfile = await AnalyzePitchProfileAsync(sample, token, progress).ConfigureAwait(false);
        string singingFile = "";
        if (singing)
        {
            singingFile = "singing.wav";
            await RenderSingingPreviewAsync(sample, transcript, pitchProfile, Path.Combine(dir, singingFile), melodyId, token, progress).ConfigureAwait(false);
        }
        var preview = new VoicePreviewRecord { Id = id, Source = "imported", SampleFile = "sample.wav", Transcript = transcript, SampleText = transcript, SingingFile = singingFile, MelodyId = VoicePreviewMelodies.Get(melodyId).Id, PitchProfile = pitchProfile };
        store.SavePreview(preview);
        return PreviewPayload(preview, dir);
    }

    public async Task<JsonObject> SavePreviewAsync(JsonElement payload, CancellationToken token, Func<string, Task>? progress = null)
    {
        string previewId = payload.GetProperty("previewId").GetString() ?? "";
        string name = payload.GetProperty("name").GetString() ?? "";
        var preview = store.LoadPreview(previewId);
        string correctedTranscript = payload.TryGetProperty("transcript", out var transcriptValue)
            ? transcriptValue.GetString() ?? ""
            : preview.Transcript;
        correctedTranscript = NormalizeReferenceTranscript(correctedTranscript);
        if (!string.Equals(correctedTranscript, preview.Transcript, StringComparison.Ordinal))
        {
            preview.Transcript = correctedTranscript;
            if (string.Equals(preview.Source, "imported", StringComparison.OrdinalIgnoreCase)) preview.SampleText = correctedTranscript;
            store.SavePreview(preview);
        }
        string sample = store.PreviewFile(previewId, preview.SampleFile);
        if (progress is not null) await progress("Voice Designer · Saving reusable reference voice…").ConfigureAwait(false);
        // qwen-server extracts and caches the voice reference when this saved WAV is first
        // used by the custom-voice service. No shared qwen.dll is required.
        var saved = store.SavePreviewAsVoice(previewId, name);
        if (progress is not null) await progress("Voice Designer · Voice saved.").ConfigureAwait(false);
        var result = store.ToPayload(); result["savedVoiceId"] = saved.Id; return result;
    }

    public void DiscardPreview(string previewId) => store.DiscardPreview(previewId);
    public async Task SelectVoiceAsync(string id, CancellationToken token = default)
    {
        store.Select(id);
        var voice = store.Get(id);
        if (voice.PitchProfile?.IsValid != true)
            await Task.Run(() => store.ReanalyzePitchProfile(voice), token).ConfigureAwait(false);
    }

    public Task SetDesignerOpenAsync(bool open, CancellationToken token = default, Func<string, Task>? progress = null)
        => ttsManager.SetScopeActiveAsync(QwenTtsServiceKind.VoiceDesign, open, token, progress);

    private async Task<string> TranscribeValidatedAsync(string samplePath, CancellationToken token, Func<string, Task>? progress)
    {
        if (progress is not null) await progress("Voice Designer · Transcribing the generated/reference sample…").ConfigureAwait(false);
        byte[] wav = await File.ReadAllBytesAsync(samplePath, token).ConfigureAwait(false);
        string transcript = (await speechService.TranscribeAsync(wav, token).ConfigureAwait(false)).Trim();
        int words = transcript.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Count(x => x.Any(char.IsLetterOrDigit));
        if (words < 4) throw new InvalidDataException("The voice sample transcription contained fewer than four words. Use a clearer/longer sample so Resone can clone the voice reliably.");
        return transcript;
    }

    private static async Task<VoicePitchProfile> AnalyzePitchProfileAsync(string samplePath, CancellationToken token, Func<string, Task>? progress)
    {
        if (progress is not null) await progress("Voice Designer · Analyzing natural octave and singing range…").ConfigureAwait(false);
        return await Task.Run(() => VocalSingingEngine.AnalyzeVoiceProfile(samplePath), token).ConfigureAwait(false);
    }

    private async Task RenderSingingPreviewAsync(string referenceWav, string transcript, VoicePitchProfile pitchProfile, string output, string melodyId, CancellationToken token, Func<string, Task>? progress)
    {
        var melody = VoicePreviewMelodies.Get(melodyId);
        if (progress is not null) await progress($"Voice Designer · Rendering {melody.Name.ToLowerInvariant()} singing preview…").ConfigureAwait(false);
        string dir = Path.GetDirectoryName(output)!;
        string speech = Path.Combine(dir, "singing-source.wav"), midi = Path.Combine(dir, "singing-preview.mid");
        var tempVoice = new SavedVoice
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Voice preview",
            Transcript = transcript,
            SampleFile = referenceWav,
            PitchProfile = pitchProfile
        };
        await ttsManager.SynthesizeSavedVoiceWavAsync(VocalSingingEngine.PrepareSourceSpeechText(SingingPreviewText), tempVoice, referenceWav, speech, token, progress).ConfigureAwait(false);
        var generated = new ResonatorMidiGenerator().Generate(melody.Notation);
        await File.WriteAllBytesAsync(midi, generated.MidiBytes, token).ConfigureAwait(false);
        await new VocalSingingEngine().RenderAsync(new VocalSingingRequest
        {
            InputSpeechWavPath = speech, SpeechText = SingingPreviewText, VocalMidiPath = midi, OutputSingingWavPath = output,
            Guidance = Array.Empty<VocalGuidanceEvent>(),
            Options = VocalSingingEngine.OptionsForVoice(pitchProfile)
        }, token).ConfigureAwait(false);
    }

    private static JsonObject PreviewPayload(VoicePreviewRecord preview, string dir)
    {
        string sample = Path.Combine(dir, preview.SampleFile);
        var result = new JsonObject
        {
            ["previewId"] = preview.Id, ["source"] = preview.Source, ["transcript"] = preview.Transcript,
            ["sampleText"] = preview.SampleText, ["designPrompt"] = preview.DesignPrompt,
            ["wav"] = Convert.ToBase64String(File.ReadAllBytes(sample)), ["melodyId"] = preview.MelodyId,
            ["basePitchHz"] = preview.PitchProfile?.BasePitchHz ?? 0, ["baseMidiNote"] = preview.PitchProfile?.BaseMidiNote ?? -1,
            ["baseOctave"] = preview.PitchProfile?.BaseOctave ?? -1, ["singingMinMidiNote"] = preview.PitchProfile?.SingingMinMidiNote ?? -1,
            ["singingMaxMidiNote"] = preview.PitchProfile?.SingingMaxMidiNote ?? -1
        };
        if (!string.IsNullOrWhiteSpace(preview.SingingFile))
        {
            string singing = Path.Combine(dir, preview.SingingFile);
            if (File.Exists(singing)) result["singingWav"] = Convert.ToBase64String(File.ReadAllBytes(singing));
        }
        return result;
    }


    private static string NormalizeReferenceTranscript(string text)
    {
        string normalized = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        int words = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Count(x => x.Any(char.IsLetterOrDigit));
        if (words < 4) throw new ArgumentException("Reference transcription must contain at least four words. Correct the detected text so it matches the WAV before saving.");
        if (normalized.Length > 4000) throw new ArgumentException("Reference transcription is too long. Use a shorter voice sample (4000 characters or fewer).");
        return normalized;
    }

    private static void ValidateSampleText(string text)
    {
        text = (text ?? "").Trim();
        int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Count(x => x.Any(char.IsLetterOrDigit));
        if (text.Length is < 8 or > 800 || words < 4) throw new ArgumentException("Voice sample text must contain at least four words and be no more than 800 characters.");
    }
}
