const fs=require('fs');
function read(p){return fs.readFileSync(p,'utf8')}
const nativeExports=read('src/wds.resone.api/NativeExports.cs');
const api=read('native/include/ApiClient.hpp');
const ui=read('src/wds.resone.ui/Resone.cpp');
const drag=read('native/include/MidiDrag.hpp');
const header=read('native/include/resone_api.h');
function ok(v,m){if(!v)throw Error(m)}
ok(/Version\(\)\s*=>\s*2/.test(nativeExports),'ABI must be version 2');
for(const name of ['resone_render_midi','resone_export_project_midi','resone_export_lane_midi','resone_last_error','resone_free_buffer']){
  ok(nativeExports.includes(`EntryPoint = "${name}"`),`missing C# export ${name}`);
  ok(header.includes(name),`missing C header symbol ${name}`);
  ok(api.includes(name),`ApiClient does not load ${name}`);
}
ok(nativeExports.includes('new ResonatorMidiGenerator().Generate(text).MidiBytes'),'render must use local ResonatorMidiGenerator');
ok(nativeExports.includes('ForgeEngine.Export(project)'),'project export must use local ForgeEngine/Resonator writer');
ok(nativeExports.includes('ForgeEngine.ExportLane(project, id)'),'lane export must use local ForgeEngine/Resonator writer');
ok(ui.includes('api_->exportProjectMidi(p)'),'normal export must bypass WebSocket');
ok(ui.includes('api_->renderMidi(notation)'),'render must bypass WebSocket');
ok(ui.includes('api_->exportLaneMidi(project, laneId)'),'lane drag must bypass WebSocket');
ok(ui.includes('api_->exportProjectMidi(p.at("project"))'),'full MIDI drag must bypass WebSocket');
ok(ui.includes('j.at("payload").at("data").get<std::string>()'),'legacy midi event must explicitly decode base64 string before overloaded saveMidi call');
ok(!drag.includes('MidiExport.hpp'),'runtime MIDI drag must not use duplicate C++ MIDI builder');
ok(!drag.includes('laneMidi(')&&!drag.includes('projectMidi('),'runtime MIDI drag must receive Resonator-produced bytes');
console.log('PASS direct local Resonator render/export/drag bridge');
