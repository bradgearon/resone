from pathlib import Path
root = Path(__file__).resolve().parents[1]
contracts = (root/'src/wds.resone.api/Contracts.cs').read_text()
processor = (root/'src/wds.resone.api/MusicPostProcessor.cs').read_text()
arranger = (root/'src/wds.resone.api/ArrangementComposer.cs').read_text()
forge = (root/'src/wds.resone.api/ForgeEngine.cs').read_text()
ui = (root/'src/wds.resone.ui/resources/web/app.js').read_text()
html = (root/'src/wds.resone.ui/resources/web/index.html').read_text()

for member in ['PostProcessingEnabled', 'PostProcessingInstructions', 'PostProcessingDeepMode', 'PostProcessingDeepInstructions']:
    assert member in contracts, f'missing persisted Lane.{member}'

assert 'Enable post processing' in html
assert 'Post-processing instructions' in html
assert 'id="postProcessingDeepMode"' in html
assert "Melody : 'Stay above E2 and below C6. Exceed octave 4 sparingly.'" in ui
assert "Chords : 'Stay above E2 and below C6. Exceed octave 4 sparingly.'" in ui
assert "Guitar : 'Stay above B1 and below C5. Exceed octave 3 sparingly.'" in ui
assert "Strings : 'Stay above E2 and below C5. Exceed octave 3 sparingly.'" in ui
assert "Vocals : 'Stay above E2 and below C5. Exceed octave 3 sparingly.'" in ui
assert "DEFAULT_GUITAR_DEEP_POST_PROCESSING = 'Stay above E0 and below C4. Exceed octave 2 sparingly.'" in ui
assert "postProcess.textContent = 'PP'" in ui
assert 'postProcessingEnabled : defaultPostProcessingEnabled(name)' in ui
assert 'target.postProcessingInstructions' in ui and 'target.postProcessingDeepInstructions' in ui

assert 'new MusicPostProcessor(assetsRoot).ApplyAsync' in arranger, 'Composer output is not routed through post processing'
for reference in ['interval_emotion_field_guide.md', 'anchored_harmonic_divergence.md', 'resonator_api_v0.1.md']:
    assert reference in processor, f'missing post-processing reference {reference}'
assert 'SongChunkPostProcessing' in processor and 'MusicPostProcessing' in processor

assert 'You are the post processing for music composed by Resone. Using the information from the composer, make the notes selected meet these requirements:' in processor
for forbidden in ['pitch classes and interval relationships', 'Prefer octave/register movement', 'do not "correct"', 'Apply ONLY the requested post-processing transformations']:
    assert forbidden not in processor, f'unrequested post-processing behavior returned: {forbidden}'
assert 'CURRENT COMPOSER CHUNK' in processor
assert 'Post processing for {target.Name} could not be applied' in processor
assert 'kept the original Composer chunk' in processor
assert 'Shifting octaves' in processor and 'Shifting octaves' in forge
assert 'DeepClone' in processor, 'successful post processing must replace only the generated track'
print('PASS persisted generic lane post processing, octave defaults/deep Guitar profile, current-chunk-only streamed transform, status, and fail-soft Composer preservation')
