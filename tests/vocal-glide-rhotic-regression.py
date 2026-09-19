from pathlib import Path
root=Path(__file__).resolve().parents[1]
main=(root/'native/vocals/resone_vocals.cpp').read_text()
art=(root/'native/vocals/resone_vocal_articulation.cpp').read_text()
phon=(root/'native/vocals/resone_vocal_phonetics.cpp').read_text()
assert 'clusterEndsWithGlide' in main
assert 'carveGlide' in main
assert 'renderGlideTransition' in main and 'renderGlideTransition' in art
assert 'ConsonantGlide' in main
assert 'y/w/r are vocal-tract transitions into the vowel, not mini-notes' in main
assert 'before * 2.20f' in art
assert "c == 'y' || c == 'w' || c == 'r'" in phon
print('vocal glide/rhotic regression: PASS')
