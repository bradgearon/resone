const fs=require('fs');
function read(p){return fs.readFileSync(p,'utf8');}
function must(v,msg){if(!v)throw new Error(msg);}
const html=read('src/wds.resone.ui/resources/web/index.html');
const js=read('src/wds.resone.ui/resources/web/app.js');
const service=read('src/wds.resone.api/VocalSinging/VoiceService.cs');
const manager=read('src/wds.resone.api/VocalSinging/QwenTtsServiceManager.cs');
const store=read('src/wds.resone.api/VocalSinging/VoiceLibrary.cs');
const melodies=read('src/wds.resone.api/VocalSinging/VoicePreviewMelodies.cs');
const settings=read('src/wds.resone.api/Contracts.cs');
for(const id of ['instrument','soundSourceLabel','vocalLyrics','renderVocals','voiceDesigner','voiceDesignPrompt','voiceSampleText','voiceDrop','voiceSingingPreview','voicePreviewMelody','voiceGenerate','voicePlay','voiceName','voiceOk','voiceCancel'])
  must(html.includes(`id="${id}"`),`missing voice UI ${id}`);
must(html.includes("Thank you for using Resone by We Develop Software, I can't wait to hear what you create."),'default sample speech missing');
must(!html.includes('id="vocalVoice"') && !html.includes('id="newVoice"'),'legacy cramped voice controls should not remain in the lyrics panel');
must(js.includes("VOCAL_SOURCE_OOHS") && js.includes("VOCAL_SOURCE_NEW"),'header vocal source selector modes missing');
must(js.includes("sourceLabel.textContent = 'Voice'") && js.includes("new Option('Oohs'") && js.includes("new Option('＋ New voice…'"),'voice header selector options missing');
must(js.includes("target.renderedVocalPath = ''") && js.includes("target.program = VOCAL_OOHS_PROGRAM"),'switching back to Oohs must disable rendered vocal playback');
for(const op of ['voiceLibraryList','voiceSelect','voiceDesignOpen','voiceDesignPreview','voiceImportPreview','voiceSavePreview','voiceDiscardPreview','voiceDesignClose','voiceServiceActivity','renderVocals'])
  must(js.includes(`'${op}'`)||js.includes(`"${op}"`),`missing app voice operation ${op}`);
must(js.includes("ondrop") && js.includes('importVoiceWav'),'WAV drag/drop import missing');
must(js.includes('localStorage') || store.includes('LastSelectedVoiceId'),'voice preference persistence missing');
must(settings.includes('QwenTtsVoiceDesignTalkerPath'),'separate lazy VoiceDesign checkpoint missing');
must(manager.includes('QwenTtsServiceKind.VoiceDesign') && manager.includes('SetScopeActiveAsync'),'lazy/releasable VoiceDesign server missing');
must(manager.includes('/v1/audio/voices') && manager.includes('wav_b64'),'server-side reference voice registration missing');
must(service.includes('SynthesizeVoiceDesignWavAsync'),'VoiceDesign task missing');
must(service.includes('SavePreviewAsVoice(previewId, name)'),'voice save should persist sample/transcript without DLL latent extraction');
must(service.includes('TranscribeValidatedAsync'),'voice transcription validation missing');
must(store.includes('user", "voices') || store.includes('"user", "voices"'),'user voice library path missing');
must(store.includes('library.json') && store.includes('audio'),'voice asset persistence layout missing');
const entries=[...melodies.matchAll(/new VoicePreviewMelody\(/g)].length;
must(entries===20,`expected 20 voice preview melodies, got ${entries}`);
const notation=[...melodies.matchAll(/new VoicePreviewMelody\([^\n]+?"(tempo=[^"]+)"\)/g)].map(m=>m[1]);
must(notation.length===20,'could not inspect all preview notation strings');
for(const n of notation){
  const body=n.split('|').slice(1).join(' ');
  const tokens=body.replaceAll('/',' ').replaceAll('_',' ').trim().split(/\s+/).filter(t=>/^[A-G](?:#|b)?-?\d/.test(t));
  must(tokens.length===6,`singing preview must contain six notes: ${n} -> ${tokens.length}`);
}
must(service.includes('We develop software'),'six-syllable singing preview text missing');
must(manager.includes('ProcessStartInfo') && manager.includes('CreateNoWindow = true'),'managed Qwen server process launch missing');
must(manager.includes('ScheduleIdleStopLocked'),'managed Qwen idle unload missing');
console.log('PASS voice library, managed Qwen services, WAV import, six-syllable previews, and persistent voice selection');
