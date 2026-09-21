const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const source=fs.readFileSync(path.join(__dirname,'../src/wds.resone.ui/resources/web/app.js'),'utf8');
const laneNotation=source.slice(0,source.indexOf('})();')+5);
const helpers=source.slice(source.indexOf('function requestMusic('),source.indexOf('function submit('));
const receive=source.slice(source.indexOf('function receive('),source.indexOf('window.SAMFD ='));
vm.runInNewContext(`
${laneNotation}
let workspaceTitle='Untitled Song',workspaceProducerDesign='',workspaceProducerVersion=0;
let song={started:false,tempo:120,meter:'4/4',bars:8,lanes:[
 {id:'a',name:'Melody',bank:0,program:0,volume:.8,muted:false,solo:false,drums:false,includeInAi:true,notation:'',originalBrief:'',clipLengthBeats:0,prompts:[],notes:[]},
 {id:'b',name:'Bass',bank:0,program:32,volume:.8,muted:false,solo:false,drums:false,includeInAi:true,notation:'',originalBrief:'',clipLengthBeats:0,prompts:[],notes:[]},
 {id:'c',name:'Strings',bank:0,program:48,volume:.8,muted:false,solo:false,drums:false,includeInAi:false,notation:'',originalBrief:'',clipLengthBeats:0,prompts:[],notes:[]}
]},selected='a',pending=null,songRun=null,transport='stopped',anchor=0,undo=[],redo=[],history=[],recording=false;
let serial=0;const sent=[];const clone=x=>JSON.parse(JSON.stringify(x)),lane=()=>song.lanes.find(l=>l.id===selected),
 send=(op,payload,requestId)=>sent.push({op,payload:payload?clone(payload):payload,requestId}),status=()=>{},render=()=>{},busy=()=>{},renderHistory=()=>{},
 commit=()=>send('project',song),readFields=()=>{},duration=()=>8*4,beatsPerBar=()=>4,
 updateSongIdentity=()=>{},renderSongList=()=>{},scheduleWorkspaceSave=()=>{},
 isGenericWorkspaceTitle=t=>!t||/^(untitled song|untitled|new song|song)$/i.test(t),provisionalSongTitle=t=>t||'Song',songDesignTitle=(p,b)=>p.title||p.state?.title||provisionalSongTitle(b),
 $=id=>({checked:id==='songMode'||id==='ahd',value:'',disabled:false,textContent:'',hidden:false}),crypto={randomUUID:()=>String(++serial)};
${helpers}
${receive}
function assert(x,m='song mode regression'){if(!x)throw Error(m)}
startSongGeneration('epic battle song');
assert(sent.at(-1).op==='songDesign','producer not called first');
let id=pending.id;
const state={brief:'epic battle song',design:'Producer notes',tempo:120,meter:'4/4',targetBars:8,currentSectionIndex:0,musicalMemories:[],sections:[
 {id:'intro',title:'Intro',plan:'SECTION 1 [intro] — Intro\\nBars: 4',bars:4,startBar:0,memoryNotes:''},
 {id:'battle',title:'Battle',plan:'SECTION 2 [battle] — Battle\\nBars: 4',bars:4,startBar:4,memoryNotes:''}
]};
receive({op:'songDesign',requestId:id,payload:{design:'Producer notes',state}});
assert(song.bars===8,'producer-decided actual song length was not applied');
assert(sent.at(-1).op==='songChunk'&&sent.at(-1).payload.sectionId==='intro'&&sent.at(-1).payload.laneId==='a','first section/lane not queued');
assert(sent.at(-1).payload.project.lanes.length===2,'unchecked lane was submitted');
id=pending.id;
receive({op:'songChunk',requestId:id,payload:{tracks:[{notation:'C4',notes:[{pitch:60,start:0,duration:1}]}],songState:state}});
assert(song.lanes[0].notes[0].start===0,'section 1 offset wrong');
assert(sent.at(-1).op==='songChunk'&&sent.at(-1).payload.laneId==='b','second checked lane not queued');
id=pending.id;
receive({op:'songChunk',requestId:id,payload:{tracks:[{notation:'C2',notes:[{pitch:36,start:1,duration:1}]}],songState:state}});
assert(sent.at(-1).op==='songChunk'&&sent.at(-1).payload.sectionId==='battle'&&sent.at(-1).payload.laneId==='a','section 2 did not begin');
assert(sent.at(-1).payload.project.bars===4,'section project length wrong');
id=pending.id;
receive({op:'songChunk',requestId:id,payload:{tracks:[{notation:'G4',notes:[{pitch:67,start:0,duration:1}]}],songState:state}});
assert(song.lanes[0].notes.some(n=>n.pitch===67&&n.start===16),'section 2 was not shifted to full-song position');
id=pending.id;
receive({op:'songChunk',requestId:id,payload:{tracks:[{notation:'G2',notes:[{pitch:43,start:0,duration:1}]}],songState:state}});
assert(pending===null&&songRun===null,'song loop did not finish');
assert(song.lanes[2].notes.length===0,'unchecked lane changed');
assert(undo.length===1,'whole song should be one undo checkpoint');
console.log('PASS producer-once, checked-lane filtering, section/lane loop, section offsets, whole-song undo');
`);
