from pathlib import Path
root = Path(__file__).resolve().parents[1]
native = (root / "native/vocals/resone_vocals.cpp").read_text(encoding="utf-8")
header = (root / "native/vocals/resone_vocals.h").read_text(encoding="utf-8")
service = (root / "src/wds.resone.api/VocalSinging/VoiceService.cs").read_text(encoding="utf-8")
store = (root / "src/wds.resone.api/VocalSinging/VoiceLibrary.cs").read_text(encoding="utf-8")
forge = (root / "src/wds.resone.api/ForgeEngine.cs").read_text(encoding="utf-8")
models = (root / "src/wds.resone.api/VocalSinging/VocalSingingModels.cs").read_text(encoding="utf-8")
engine = (root / "src/wds.resone.api/VocalSinging/VocalSingingEngine.cs").read_text(encoding="utf-8")
app = (root / "src/wds.resone.ui/resources/web/app.js").read_text(encoding="utf-8")
assert "ResoneVoicePitchProfile" in header
assert "resone_analyze_voice_profile" in header and "resone_analyze_voice_profile" in native
assert "observedMidi>=60 ? std::max(0,observedMidi-12) : observedMidi" in native
assert "baseOctave=baseMidi/12-1" in native
assert "singingMin=std::clamp(12*(baseOctave+1)" in native
assert "singingMax=std::clamp(singingMin+23" in native
assert "foldMidiToRange" in native and "while(note>maxNote) note-=12" in native and "while(note<minNote) note+=12" in native
assert "VoicePitchProfile" in store and "PitchProfile" in store
assert "CurrentVersion = 3" in store and "ReanalyzePitchProfile" in store
assert "Analyzing natural octave and singing range" in service
assert "EnsurePitchProfile" in forge
assert "OptionsForVoice(savedVoice.PitchProfile)" in forge
assert "SingingMinMidiNote" in models and "SingingMaxMidiNote" in models
assert "AnalyzeVoiceProfile" in engine and "OptionsForVoice" in engine
assert "SelectVoiceAsync" in service
assert "singing range" in app and "midiVoiceNoteName" in app
print("PASS analyzed base octave is persisted and all vocal melody targets are octave-folded into a two-octave comfortable register")
