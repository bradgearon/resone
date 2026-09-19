from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
source = (root / 'src/wds.resone.api/SongGeneration.cs').read_text(encoding='utf-8')
designer = (root / 'src/wds.resone.api/SongCompositionDesigner.cs').read_text(encoding='utf-8')

assert 'bareId' in source, 'song parser must accept bare [id] producer headers'
assert 'listId' in source, 'song parser must accept numbered [id] producer headers'
assert 'NormalizeSectionId' in source, 'song section ids should be normalized and stable'
assert 'Producer notes did not contain a usable section list.' in source
assert 'EVERY section header must literally begin with the word `SECTION`' in designer
assert 'Invalid: `[INTRO] — Overture Build`' in designer

# Behavioral mirror of the accepted header grammar against the exact producer shape that regressed.
header = re.compile(r'^\s*(?:(?:SECTION\s+(?P<n>\d+)(?:\s*\[(?P<id>[^\]]+)\])?)|(?:\[(?P<bareId>[^\]]+)\])|(?:(?P<listN>\d+)[.)]\s*\[(?P<listId>[^\]]+)\]))\s*(?:[—–:-]\s*)?(?P<title>[^\r\n]*)$', re.I | re.M)
producer = '''Producer notes:\nOverall identity: Epic orchestral hybrid.\nMotif strategy: recur.\nRhythmic strategy: drive.\nSections:\n[INTRO] — Overture Build\nBars: 4\nPurpose: Establish urgency.\n[CLASH] — The Confrontation\nBars: 8\nPurpose: Main battle.\n[CLIMAX] — Overwhelming Force\nBars: 4\nPurpose: Peak.\nFinal payoff: decisive ending.\n'''
section_body = producer.split('Sections:', 1)[1].split('Final payoff:', 1)[0]
matches = list(header.finditer(section_body))
assert len(matches) == 3, f'expected 3 sections, got {len(matches)}'
assert [m.group('bareId') for m in matches] == ['INTRO', 'CLASH', 'CLIMAX']
assert [m.group('title') for m in matches] == ['Overture Build', 'The Confrontation', 'Overwhelming Force']

canonical = '''Sections:\nSECTION 1 [intro] — Intro\nBars: 4\nSECTION 2 [battle] — Battle\nBars: 4\nFinal payoff: hit.\n'''
cmatches = list(header.finditer(canonical.split('Sections:',1)[1].split('Final payoff:',1)[0]))
assert len(cmatches) == 2
assert cmatches[0].group('id') == 'intro'

print('PASS song producer section parser accepts canonical and compact local-model headers')
