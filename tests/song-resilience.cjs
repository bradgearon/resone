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
 {id:'b',name:'Bass',bank:0,program:32,volume:.8,muted:false,solo:false,drums:false,includeInAi:true,notation:'',originalBrief:'',clipLengthBeats:0,prompts:[],notes:[]}
]},selected='a',pending=null,songRun=null,transport='stopped',anchor=0,undo=[],redo=[],history=[],recording=false;
let serial=0,lastStatus='';const sent=[];const clone=x=>JSON.parse(JSON.stringify(x)),lane=()=>song.lanes.find(l=>l.id===selected),
 send=(op,payload,requestId)=>sent.push({op,payload:payload?clone(payload):payload,requestId}),status=s=>{lastStatus=s},render=()=>{},busy=()=>{},renderHistory=()=>{},
 commit=()=>send('project',song),readFields=()=>{},duration=()=>8*4,beatsPerBar=()=>4,
 updateSongIdentity=()=>{},renderSongList=()=>{},scheduleWorkspaceSave=()=>{},
 isGenericWorkspaceTitle=t=>!t||/^(untitled song|untitled|new song|song)$/i.test(t),provisionalSongTitle=t=>t||'Song',songDesignTitle=(p,b)=>p.title||p.state?.title||provisionalSongTitle(b),
 $=id=>({checked:id==='songMode'||id==='ahd',value:'',disabled:false,textContent:'',hidden:false}),crypto={randomUUID:()=>String(++serial)};
${helpers}
${receive}
function assert(x,m='song resilience regression'){if(!x)throw Error(m)}
startSongGeneration('battle');
let id=pending.id;
const state={brief:'battle',design:'Producer notes',tempo:120,meter:'4/4',targetBars:8,currentSectionIndex:0,musicalMemories:[],sections:[
 {id:'intro',title:'Intro',plan:'SECTION 1 [intro] — Intro\\nBars: 4',bars:4,startBar:0,memoryNotes:''},
 {id:'battle',title:'Battle',plan:'SECTION 2 [battle] — Battle\\nBars: 4',bars:4,startBar:4,memoryNotes:''}
]};
receive({op:'songDesign',requestId:id,payload:{design:'Producer notes',state}});
// intro melody succeeds
id=pending.id; receive({op:'songChunk',requestId:id,payload:{tracks:[{laneId:'a',notation:'C4',notes:[{pitch:60,start:0,duration:1}]}],songState:state}});
assert(song.lanes[0].notes.some(n=>n.pitch===60&&n.start===0),'successful first chunk missing');
// intro bass fails at worker/API level: should move to battle melody, not rollback song.
id=pending.id; receive({op:'error',requestId:id,payload:{message:'Invalid arrangement: No playable tracks.'}});
assert(songRun && pending && pending.kind==='songChunk','song run died after recoverable chunk error');
assert(pending.sectionId==='battle'&&pending.laneId==='a','did not advance after failed chunk');
assert(song.lanes[0].notes.some(n=>n.pitch===60),'earlier successful chunk was rolled back');
// battle melody returns malformed payload: should also skip and continue to battle bass.
id=pending.id; receive({op:'songChunk',requestId:id,payload:{tracks:[],songState:state}});
assert(songRun && pending && pending.sectionId==='battle'&&pending.laneId==='b','invalid chunk payload killed song instead of skipping');
// last chunk succeeds and song finishes with partial results retained.
id=pending.id; receive({op:'songChunk',requestId:id,payload:{tracks:[{laneId:'b',notation:'G2',notes:[{pitch:43,start:0,duration:1}]}],songState:state}});
assert(songRun===null&&pending===null,'song did not finish after recoverable failures');
assert(song.lanes[0].notes.some(n=>n.pitch===60&&n.start===0),'first successful chunk lost at finish');
assert(song.lanes[1].notes.some(n=>n.pitch===43&&n.start===16),'later successful chunk not retained');
assert(/2 skipped generations/.test(lastStatus),'final status did not report skipped chunks');
console.log('PASS failed song chunks are skipped while successful chunks remain');
`);

// Static safety checks for the C# notation parser. The concrete parser behavior is
// compiled/tested by the Windows build; these assertions prevent the tolerant path
// and standard ppp/fff dynamics from being accidentally removed in source edits.
const parser=fs.readFileSync(path.join(__dirname,'../src/wds.resone.resonator/ResonatorParser.cs'),'utf8');
if(!/ParseSingleEventTolerant/.test(parser)) throw Error('missing tolerant event parser');
if(!/Skipped invalid event/.test(parser)) throw Error('missing invalid-token warning');
if(!/"ppp"\s*=>\s*28/.test(parser)||!/"fff"\s*=>\s*127/.test(parser)) throw Error('ppp/fff dynamics missing');
const composer=fs.readFileSync(path.join(__dirname,'../src/wds.resone.api/ArrangementComposer.cs'),'utf8');
if(!/generated\.Warnings/.test(composer)||!/Recovered /.test(composer)) throw Error('parser warnings are not surfaced');
