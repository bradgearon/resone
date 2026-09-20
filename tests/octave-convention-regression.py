from pathlib import Path
root=Path(__file__).resolve().parents[1]
pitch=(root/'src/wds.resone.resonator/Pitch.cs').read_text()
models=(root/'src/wds.resone.resonator/Models.cs').read_text()
chords=(root/'src/wds.resone.resonator/ChordSymbolExpander.cs').read_text()
ctx=(root/'src/wds.resone.api/NoteNameContext.cs').read_text()
ui=(root/'src/wds.resone.ui/resources/web/app.js').read_text()
spec=(root/'assets/Instructions/Music/resonator_api_v0.1.md').read_text()
assert 'int midiNoteForC0 = 24' in pitch
assert 'MidiNoteForC0 { get; set; } = 24' in models
assert 'int midiNoteForC0 = 24' in chords
assert '(pitch / 12 - 2)' in ctx
assert 'Math.floor(pitch / 12) - 2' in ui
assert 'Math.floor(p / 12) - 2' in ui
assert 'Math.floor(note/12)-2' in ui
assert 'C0 = MIDI 24 and C3 = MIDI 60' in spec
print('PASS C0=MIDI24 / C3=MIDI60 convention is consistent across parsing and labels')
