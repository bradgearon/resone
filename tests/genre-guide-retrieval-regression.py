from pathlib import Path
import re

root=Path(__file__).resolve().parents[1]
song=(root/'assets/Instructions/Music/resone_song_design_genre_guide.md').read_text(encoding='utf-8')
drum=(root/'assets/Instructions/Music/resone_drums_genre_guide.md').read_text(encoding='utf-8')
resolver=(root/'src/wds.resone.api/GenreBriefResolver.cs').read_text(encoding='utf-8')
classifier=(root/'src/wds.resone.api/GenreIdentificationPass.cs').read_text(encoding='utf-8')
arranger=(root/'src/wds.resone.api/ArrangementComposer.cs').read_text(encoding='utf-8')
planner=(root/'src/wds.resone.api/MusicNarrativePlanner.cs').read_text(encoding='utf-8')
forge=(root/'src/wds.resone.api/ForgeEngine.cs').read_text(encoding='utf-8')
state=(root/'src/wds.resone.api/SongGeneration.cs').read_text(encoding='utf-8')
producer=(root/'src/wds.resone.api/SongCompositionDesigner.cs').read_text(encoding='utf-8')
composer=(root/'src/wds.resone.api/SongComposerDesignPass.cs').read_text(encoding='utf-8')
packer=(root/'scripts/pack-instructions.mjs').read_text(encoding='utf-8')
local_client=(root/'src/wds.resone.api/AiClient.cs').read_text(encoding='utf-8')
native_client=(root/'src/wds.resone.api/NativeChatClient.cs').read_text(encoding='utf-8')

for canonical in ['progressive_metal','polyrhythmic_progressive_metal','uplifting_trance','romantic_classical','trap','cinematic_orchestral']:
    assert f'# {canonical}' in song, canonical
    assert f'# {canonical}' in drum, canonical

for canonical in ['drum_core','edm','house','deep_house','trance','classic_trance','uplifting_trance','goa_trance','psytrance',
                  'techno','jungle','uk_garage','rock','grunge','metal','nu_metal','hip_hop_rap','classical','romantic_classical',
                  'late_romantic','cinematic_orchestral']:
    assert f'# {canonical}' in drum, canonical
assert 'Source appendix — merged Drum & Percussion Grammar' in drum
assert 'Do not merge unrelated grooves merely because fuzzy matching found them.' in drum
core=re.search(r'(?ms)^# drum_core\n.*?(?=^# rock\s*$)', drum).group(0)
assert len(core) < 2600, len(core)
for phrase in ['Resone kit:', 'Before writing, decide:', 'Core atoms:', 'Energy should usually change', 'genre is a hierarchy of rhythmic expectations']:
    assert phrase in core, phrase

assert '**Drum parent:** `edm`' in re.search(r'(?ms)^# trance\n.*?(?=^# )', drum).group(0)
assert '**Drum parent:** `trance`' in re.search(r'(?ms)^# uplifting_trance\n.*?(?=^# )', drum).group(0)
assert '**Drum parent:** `house`' in re.search(r'(?ms)^# deep_house\n.*?(?=^# )', drum).group(0)
assert '**Drum parent:** `classical`' in re.search(r'(?ms)^# romantic_classical\n.*?(?=^# )', drum).group(0)

for token in ['Levenshtein','return Selection.Empty','ParentGenre','trap metal','cinematic_orchestral',
              'drum_core','GetFamilyChain','ExtractParent','drumCandidates','SongPromptContext','DrumPromptContext']:
    assert token in resolver, token
assert 'result.Reverse(); // broad fundamentals first, specific override last.' in resolver
assert 'new List<string> { "drum_core" }' in resolver

# Song mode: producer resolves identity once; packets split song vs drum grammar and only drum lanes get drum grammar.
assert 'GenreBriefResolver.Resolve(assetsRoot, description)' in forge
assert 'ExtractOverallIdentity(design)' in forge
assert 'GenreBriefResolver.Resolve(assetsRoot, identity, description)' in forge
assert 'if (!selectedGenre.HasMatch) selectedGenre = producerGenre;' in forge
assert 'selectedGenre.PromptContext' in forge
assert 'BuildPacket(state, sectionId, lane.Name, lane.Drums)' in forge
assert 'GenreSongContext' in state and 'GenreDrumContext' in state
assert 'if (drumLane && !string.IsNullOrWhiteSpace(state.GenreDrumContext))' in state
assert 'MaxGenreContextChars' not in state
assert 'Clip(state.GenreDrumContext' not in state

# Ordinary Melody-mode drum lane: one tiny AI classifier, then deterministic fuzzy retrieval. No extra call for Song mode/non-drums.
assert 'Return ONLY one short genre/subgenre string' in classifier
assert 'DrumGenreIdentification' in classifier
assert 'if (songContext is null && target.Drums)' in arranger
assert 'GenreIdentificationPass.IdentifyAsync' in arranger
assert 'GenreBriefResolver.Resolve(assetsRoot, identifiedGenre)' in arranger
assert 'GenreBriefResolver.Resolve(assetsRoot, description)' in arranger  # fail-soft fallback
assert 'laneGenreContext = genre.DrumPromptContext' in arranger
assert 'DRUM GENRE GUIDANCE — CURRENT LANE ONLY' in arranger
assert 'LANE GENRE GUIDANCE' in planner

# Genre/context is not arbitrarily character-clipped; backend model context/EOS is the real limit.
assert 'genreContext.Length > 16000' not in producer
assert 'genreContext.Length > 16000' not in composer
assert 'Model notation exceeds 65536 characters.' not in local_client
assert "Model output exceeds Resone's 65536-character safety limit." not in native_client

assert 'RETRIEVED GENRE GUIDANCE' in producer
assert 'RETRIEVED GENRE GUIDANCE' in composer
assert 'resone_song_design_genre_guide.md' in packer
assert 'resone_drums_genre_guide.md' in packer
print('PASS genre guides: compact drum inheritance + song-mode lane scoping + Melody-mode drum genre classifier + fail-soft retrieval + no artificial genre/model-output clipping')
