const fs=require('fs');
function read(p){return fs.readFileSync(p,'utf8');}
function must(v,msg){if(!v)throw new Error(msg);}
const html=read('src/wds.resone.ui/resources/web/index.html');
const css=read('src/wds.resone.ui/resources/web/style.css');
const js=read('src/wds.resone.ui/resources/web/app.js');
must(html.includes('id="soundSourceLabel"') && html.includes('id="instrument"'),'shared header source selector missing');
must(!html.includes('id="vocalVoice"') && !html.includes('id="newVoice"'),'voice controls should not crowd lyrics panel');
must(/class="vocalFields"[\s\S]*id="vocalLyrics"[\s\S]*id="renderVocals"/.test(html),'compact lyrics + render vocal panel missing');
must(css.includes('grid-template-columns:minmax(0,1fr) auto'),'lyrics field should own remaining vocal panel width');
must(css.includes('.top:has(.vocalPanel:not([hidden]))'),'vocal editor should receive extra vertical room');
for(const token of ["VOCAL_SOURCE_OOHS", "VOCAL_SOURCE_NEW", "sourceLabel.textContent = 'Voice'", "new Option('Oohs'", "'voice:' + v.id", "new Option('＋ New voice…'", "target.program = VOCAL_OOHS_PROGRAM", "target.renderedVocalPath = ''", "openVoiceDesigner()"])
  must(js.includes(token),`missing source-selector behavior: ${token}`);
must(js.includes("sourceLabel.textContent = 'Instrument'"),'non-vocal lanes must restore Instrument label');
must(js.includes("status('Vocals · Using Oohs MIDI preview.')"),'Oohs selection status missing');
console.log('PASS shared Instrument/Voice selector and unclipped lyrics layout');
