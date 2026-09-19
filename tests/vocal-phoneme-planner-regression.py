from pathlib import Path
root = Path(__file__).resolve().parents[1]
h = (root / "native/vocals/resone_vocal_phonetics.h").read_text(encoding="utf-8")
c = (root / "native/vocals/resone_vocal_phonetics.cpp").read_text(encoding="utf-8")
native = (root / "native/vocals/resone_vocals.cpp").read_text(encoding="utf-8")
cmake_ui = (root / "src/wds.resone.ui/CMakeLists.txt").read_text(encoding="utf-8")
cmake_cli = (root / "tools/resone-voice/native/CMakeLists.txt").read_text(encoding="utf-8")
articulation = (root / "native/vocals/resone_vocal_articulation.cpp").read_text(encoding="utf-8")
assert "stretchNoiseConsonantMonotonic" in articulation and "renderConsonantOnce" in articulation
assert "resampleLinearOnce(source, outputSamples)" in articulation and "repeating grains makes the ear hear" in (root / "native/vocals/resone_vocal_articulation.h").read_text(encoding="utf-8")
assert "renderAspirate" in articulation and "wanted = before * 2.20f" in articulation
assert "SustainableNoise" in h and "SustainableVoiced" in h and "Mixed" in h
assert "Glide" in h and "Aspirate" in h and "TextVowelNucleus" in h
assert "planWordPhonetics" in c
assert 't == "sh"' in c and 't == "th"' in c and 't == "ch"' in c and 't == "ng"' in c
assert "c == 'h'" in c and "c == 'y' || c == 'w' || c == 'r'" in c
assert "postVocalicRhotic" in c and "hasOffglide" in c
assert "fitVoicedNucleiToText" in native and "quietSplitPoint" in native
assert "appendConsonantPieces" in native and "stretchNoiseConsonantMonotonic" in native
assert "single-consumption events" in native and "may genuinely carry a sung fundamental" in native
assert "ConsonantGlide" in native and "ConsonantAspirate" in native and "VowelOffglide" in native
assert "minimumSourceWordSamples" in native and "makeWordRegionsContiguous" in native
assert "phraseGain" in native and "shaped=u*u*(3.0-2.0*u)" in native
assert "resone_vocal_phonetics.cpp" in cmake_ui and "resone_vocal_phonetics.cpp" in cmake_cli
assert "resone_vocal_articulation.cpp" in cmake_ui and "resone_vocal_articulation.cpp" in cmake_cli
assert "silentFinalE" in c
assert "ConsonantBehavior::Mixed" in c
assert "Raised-cosine, constant-sum crossfade" in native
print("PASS glides/aspirates/rhotics/diphthong off-glides, source-word preservation, single-consumption consonants, sung voiced continuants, smooth joins, pitch glide, and phrase-level dynamics")
