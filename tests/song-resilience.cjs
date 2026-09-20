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
const state={brief:'battle',design:'Producer notes',tempo:120,meter:'4/4',targetBars:8,currentSectionIndex:0,musicalMemories:[],sections:[
 {id:'intro',title:'Intro',plan:'SECTION 1 [intro] — Intro\\nBars: 4',bars:4,startBar:0,memoryNotes:''},
 {id:'battle',title:'Battle',plan:'SECTION 2 [battle] — Battle\\nBars: 4',bars:4,startBar:4,memoryNotes:''}
]};

// A server/composer error must preserve this section and continue to the next lane without retrying.
startSongGeneration('battle'); let id=pending.id;
receive({op:'songDesign',requestId:id,payload:{design:'Producer notes',state}});
id=pending.id; const firstRequestCount=sent.filter(x=>x.op==='songChunk').length;
receive({op:'error',requestId:id,payload:{message:'Invalid arrangement: no playable notes'}});
assert(songRun&&pending&&pending.sectionId==='intro'&&pending.laneId==='b','failed chunk did not continue to next planned lane');
assert(sent.filter(x=>x.op==='songChunk').length===firstRequestCount+1,'failed chunk was retried instead of advancing once');
assert(songRun.emptyChunks===1,'failed chunk was not recorded as preserved/empty');
assert(songRun.recoveryWarnings.some(x=>/preserved and generation continued/.test(x)),'failure recovery warning missing');

// An explicit empty track is also accepted and advances, preserving preexisting notes.
song.lanes[1].notes=[{pitch:40,start:0,duration:1}];
id=pending.id;
receive({op:'songChunk',requestId:id,payload:{tracks:[{laneId:'b',notation:'',notes:[]}],warnings:['No playable MIDI could be recovered for Bass; preserved the section as existing music/silence and continued.'],songState:state}});
assert(song.lanes[1].notes.some(n=>n.pitch===40),'empty composer result erased existing section music');
assert(songRun&&pending&&pending.sectionId==='battle'&&pending.laneId==='a','empty track did not continue through plan');

// A partially recovered payload keeps every valid note and advances normally.
id=pending.id;
receive({op:'songChunk',requestId:id,payload:{tracks:[{laneId:'a',notation:'C4 BAD D4',notes:[{pitch:60,start:0,duration:1},{pitch:62,start:2,duration:1}]}],warnings:['Recovered Melody: Skipped invalid event BAD'],songState:state}});
assert(song.lanes[0].notes.some(n=>n.pitch===60)&&song.lanes[0].notes.some(n=>n.pitch===62),'best-effort notes were not kept');
assert(songRun&&pending&&pending.sectionId==='battle'&&pending.laneId==='b','recovered chunk did not advance exactly once');
console.log('PASS song chunks salvage/continue without retrying or aborting');
`);

const parser=fs.readFileSync(path.join(__dirname,'../src/wds.resone.resonator/ResonatorParser.cs'),'utf8');
if(!/ParseSingleEventTolerant/.test(parser)) throw Error('missing tolerant event parser');
if(!/Skipped invalid event/.test(parser)||!/Recovered invalid token/.test(parser)) throw Error('missing best-effort parser warnings');
const composer=fs.readFileSync(path.join(__dirname,'../src/wds.resone.api/ArrangementComposer.cs'),'utf8');
if(!/ExtractPlayableFallback/.test(composer)) throw Error('missing last-resort explicit note/chord salvage');
if(!/allowEmptyTracks: songContext is not null/.test(composer)) throw Error('song chunks do not allow preserved empty sections');
if(!/preserved the section as existing music\/silence and continued/.test(composer)) throw Error('empty song-section continuation warning missing');
