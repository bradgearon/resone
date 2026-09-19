from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
web = root / 'src' / 'wds.resone.ui' / 'resources' / 'web'
app = (web / 'app.js').read_text(encoding='utf-8')
html = (web / 'index.html').read_text(encoding='utf-8')

refs = set(re.findall(r"\$\('([^']+)'\)", app))
ids = set(re.findall(r'id=["\']([^"\']+)["\']', html))
missing = sorted(refs - ids)
assert not missing, f'app.js references missing DOM ids: {missing}'
assert 'id="rollContent"' in html
assert "send('ready')" in app
assert 'reportUiFault' in app
print(f'UI DOM contract OK: {len(refs)} referenced ids are present.')
