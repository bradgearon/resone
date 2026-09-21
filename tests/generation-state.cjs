// Exercise production generation handlers without WebView/native audio.
const fs = require('node:fs'), vm = require('node:vm');
const source = fs.readFileSync(require('node:path').join(__dirname, '../src/wds.resone.ui/resources/web/app.js'), 'utf8');
const handlers = source.slice(source.indexOf('function requestMusic('), source.indexOf('function submit(')) +
    source.slice(source.indexOf('function receive('), source.indexOf('window.SAMFD ='));
vm.runInNewContext(`
let song={lanes:[{id:'a',name:'Melody',notes:[{pitch:60,start:0,duration:1}],notation:'C4'}]},
 selected='a',pending=null,transport='playing',anchor=5,undo=[],redo=[],history=[],recording=false;
const clone=x=>JSON.parse(JSON.stringify(x)),lane=()=>song.lanes.find(l=>l.id===selected),sent=[],
 send=(op,payload,requestId)=>sent.push({op,payload:payload?clone(payload):payload,requestId}),
 status=()=>{},render=()=>{},busy=()=>{},readFields=()=>{},renderHistory=()=>{},
 commit=()=>send('project',song),$=()=>({checked:true}),crypto={randomUUID:()=>String(++serial)};
let serial=0;
${source.slice(0,source.indexOf('})();')+5)}
${handlers}
function assert(x){if(!x)throw Error('Generation regression');}
requestMusic('new');
assert(sent[0].op==='stop' && song.lanes[0].notes.length===0);
assert(sent[1].payload.project.lanes[0].notes[0].pitch===60);
let id=pending.id;
receive({op:'composition',requestId:'obsolete',payload:{}});
assert(pending.id===id);
receive({op:'composition',requestId:id,payload:{tracks:[{notes:[{pitch:72,start:0,duration:1}],notation:'C5'}]}});
assert(song.lanes[0].notes[0].pitch===72 && sent.at(-1).payload.lanes[0].notes[0].pitch===72);
assert(undo[0].lanes[0].notes[0].pitch===60);
requestMusic('another');
id=pending.id;
const plays=sent.filter(x=>x.op==='play').length;
receive({op:'composition',requestId:id,payload:{tracks:[{notes:[{pitch:50,start:0,duration:1}]}]}});
assert(song.lanes[0].notes[0].pitch===50 && sent.filter(x=>x.op==='play').length===plays+1);
requestMusic('third');
receive({op:'error',requestId:pending.id,payload:{message:'bad notation'}});
assert(song.lanes[0].notes[0].pitch===50 && pending===null);
song.lanes.push({id:'b',name:'Bass',notes:[{pitch:40,start:0,duration:1}],notation:'E2'});
requestMusic('partial');
receive({op:'composition',requestId:pending.id,payload:{tracks:[{notes:[{pitch:65,start:0,duration:1}],notation:'F4'}]}});
assert(song.lanes[0].notes[0].pitch===65 && song.lanes[1].notes[0].pitch===40 && sent.at(-1).op==='play');
song.lanes[1].notes=[{pitch:43,start:2,duration:.5}];
song.lanes[1].includeInAi=false;
song.lanes[0].notes[0].pitch=67;
requestMusic('edited');
const req=sent.at(-1);
assert(req.payload.project.lanes.length===2 && req.payload.project.lanes[0].notes[0].pitch===67);
assert(song.lanes[1].notes[0].pitch===43);
receive({op:'composition',requestId:pending.id,payload:{tracks:[{notes:[{pitch:69,start:0,duration:1}],notation:'A4'}]}});
assert(song.lanes[1].notes[0].pitch===43 && song.started===true);
assert(song.lanes[0].prompts.includes('edited'));
selected='b';
requestMusic('make bass move');
assert(pending.laneId==='b' && sent.at(-1).payload.laneId==='b');
assert(sent.at(-1).payload.project.lanes[0].prompts.includes('edited'));
assert(song.lanes[0].notes[0].pitch===69 && song.lanes[1].notes.length===0);
receive({op:'composition',requestId:pending.id,payload:{tracks:[{notes:[{pitch:45,start:0,duration:1}],notation:'A2'}]}});
assert(song.lanes[0].notes[0].pitch===69 && song.lanes[1].notes[0].pitch===45);
assert(song.lanes[1].prompts[0]==='make bass move');

`);
// Exercise the actual suggestion handler: it must submit, not just fill the box.
const chipCode = source.slice(source.indexOf('for (const b of document.querySelectorAll'), source.indexOf("$('settings').onclick"));
const chip={textContent:'More energy'}, input={value:''}; let submissions=0;
vm.runInNewContext(chipCode,{document:{querySelectorAll:()=>[chip]},pending:null,recording:false,$:()=>input,submit:()=>submissions++});
chip.onclick();
if(input.value!=='More energy'||submissions!==1)throw Error('Suggestion did not submit');
console.log('PASS: selected-lane replacement, context/manual edits/prompts, other lanes preserved, autoplay, undo, stale replies, failures, suggestion submit');
