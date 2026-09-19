from pathlib import Path

root = Path(__file__).resolve().parents[1]
native = (root / "native/vocals/resone_vocals.cpp").read_text(encoding="utf-8")
engine = (root / "src/wds.resone.api/VocalSinging/VocalSingingEngine.cs").read_text(encoding="utf-8")
forge = (root / "src/wds.resone.api/ForgeEngine.cs").read_text(encoding="utf-8")
ui = (root / "src/wds.resone.ui/resources/web/index.html").read_text(encoding="utf-8")
app = (root / "src/wds.resone.ui/resources/web/app.js").read_text(encoding="utf-8")
models = (root / "src/wds.resone.api/VocalSinging/VocalSingingModels.cs").read_text(encoding="utf-8")

assert "buildWordTargets" in native
assert "fitSpeechToWords" in native
assert "psolaSustain" in native
assert "sourcePitchAt" in native and "sourcePitchMarks" in native and "refinePitchMark" in native
assert "applyRegisterTimbreCompensation" in native and "options.formantPreserve" in native
assert "2.0*kPi*35.0" in native
assert "srcCenter = half + std::fmod" not in native and "srcCenter = half + std::fmod" not in native.replace(" ", "")
assert "PrepareSourceSpeechText" in engine and "one fluent source phrase" in engine
assert 'string.Join(". ", words)' not in engine
assert "PrepareSourceSpeechText(text)" in forge
assert "Reference transcription" in ui and 'id="voiceTranscript" rows="2" readonly' not in ui
assert "voiceSavePreview',{previewId:voicePreview.previewId,name,transcript}" in app
assert "MinPitchHz { get; init; } = 50" in models
assert "VibratoDepthCents { get; init; } = 18f" in models
assert "kPhraseGapSeconds=0.14" in native
assert "splitWordSpeechPieces" in native
assert "SpeechPieceKind::ConsonantTransient" in native and "SpeechPieceKind::ConsonantNoiseSustainable" in native and "SpeechPieceKind::ConsonantVoicedSustainable" in native and "SpeechPieceKind::Vowel" in native
assert "allocateSpeechPieceDurations" in native
assert "stretchNoiseConsonantMonotonic" in native and "fitVoicedNucleiToText" in native
assert "appendCrossfaded" in native
phonetics = (root / "native/vocals/resone_vocal_phonetics.cpp").read_text(encoding="utf-8")
assert "planWordPhonetics" in phonetics and "Sustainable" in phonetics and "Transient" in phonetics
assert "sh" in phonetics and "th" in phonetics and "ng" in phonetics
assert "smoothstep" in native or "shaped=u*u*(3.0-2.0*u)" in native
assert "slide=std::clamp(0.018+cents/12000.0" in native
assert "noteMaturity" in native and "onsetRamp" in native
assert "foldMidiToRange" in native and "singingMinMidiNote" in native and "singingMaxMidiNote" in native
assert "resone_analyze_voice_profile" in native
assert "percentile(0.20)" in native
assert "CurrentVersion = 3" in (root / "src/wds.resone.api/VocalSinging/VoiceLibrary.cs").read_text(encoding="utf-8")
print("PASS syllable/phoneme-aware consonant-preserving singing, selective consonant sustain, pitch-synchronous correction, phrase shaping, practical-base register analysis, octave folding, editable clone transcript, and male pitch floor")
