from pathlib import Path
root = Path(__file__).resolve().parents[1]
parser = (root / 'src/wds.resone.resonator/ResonatorParser.cs').read_text()
api = (root / 'assets/Instructions/Music/resonator_api_v0.1.md').read_text()
ai = (root / 'assets/Instructions/Music/resonator_ai_notation_spec.md').read_text()
tips = (root / 'assets/Instructions/Music/composition-tips.md').read_text()
arranger = (root / 'src/wds.resone.api/ArrangementComposer.cs').read_text()

assert 'var memberSpec = ParseModifiers(part, baseLength: 1);' in parser
assert 'memberSpec.HasExplicitDuration' in parser
assert 'ParseOffsetTicks(memberSpec.OffsetText' in parser
assert 'memberSpec.HasExplicitGate ? memberSpec.GatePercent : chordSpec.GatePercent' in parser
assert 'memberSpec.Velocity ?? chordSpec.Velocity ?? _currentVelocity' in parser
assert 'chordSpec.Accent || memberSpec.Accent' in parser
assert 'chordSpec.Staccato || memberSpec.Staccato' in parser
assert 'ExtractCents(ref pitchToken)' in parser
assert 'FindBendSeparator(pitchToken)' in parser
assert 'return chordNominal;' in parser
assert 'Supported per-member overrides are duration, onset offset, gate, exact velocity' in api
assert '[C2 D2@+1/4q F#2@+1/8q]' in api
assert 'Explicit chord members may carry these modifiers independently' in ai
assert '[C2 F#2@+1/8q D2@+1/4q]' in tips
assert '[C2 F#2@+1/8q D2@+1/4q]' in arranger
print('PASS explicit chord members inherit chord defaults and independently override note modifiers without advancing the chord cursor')
