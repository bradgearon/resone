const fs=require('node:fs'), vm=require('node:vm'), assert=require('node:assert/strict');
const src=fs.readFileSync(__dirname+'/../src/wds.resone.ui/resources/web/app.js','utf8');
const scope={};vm.runInNewContext(src.slice(0,src.indexOf('})();')+5),scope);
const N=scope.LaneNotation;
// Small decoder of the emitted subset, following Resonator's 480-PPQ
// cursor/offset/gate/hold rules. Checks sound events, not spelling.
function decode(s) {
 let cursor=0,last=[],notes=[];
 for(const token of s.match(/\[[^\]]+\][^\s]*|[^\s]+/g)||[]) {
  if(/^(tempo=|mode=|\d+\/)/.test(token))continue;
  const m=token.match(/^(\[[^\]]+\]|[A-G]#?-?\d+|_|-)([,.]?)(?:@([+-]\d+)t)?(?::([\d.]+)%)?(?::v(\d+))?$/);
  assert.ok(m,token);
  const base=m[2]==='.'?120:m[2]===','?240:480;
  if(m[1]==='_'){last=[];cursor+=base;continue;}
  if(m[1]==='-'){last.forEach(n=>n.length+=base);cursor+=base;continue;}
  last=[];
  for(const pitch of m[1].replace(/[\[\]]/g,'').split(' ')) {
   const p=pitch.match(/^([A-G])(#?)(-?\d+)$/);
   const n={start:cursor+Number(m[3]||0),length:Math.max(1,Math.round(base*Number(m[4]||100)/100)),pitch:(Number(p[3])+1)*12+{C:0,D:2,E:4,F:5,G:7,A:9,B:11}[p[1]]+(p[2]?1:0),velocity:Number(m[5]||96)};
   notes.push(n);last.push(n);
  }
  cursor+=base;
 }
 return notes.sort((a,b)=>a.start-b.start||a.pitch-b.pitch||a.length-b.length);
}
const cases=[
 [{start:0,duration:1,pitch:60,velocity:96},{start:2,duration:.5,pitch:62,velocity:80}],
 [{start:0,duration:2.5,pitch:60,velocity:96},{start:0,duration:2.5,pitch:64,velocity:96},{start:.5,duration:.25,pitch:67,velocity:70}],
 [{start:1/480,duration:1/480,pitch:0,velocity:1},{start:.5,duration:3.03125,pitch:127,velocity:127}],
 [{start:0,duration:.125,pitch:36,velocity:100},{start:0,duration:.125,pitch:42,velocity:100},{start:.5,duration:.125,pitch:38,velocity:80}]
];
for(const notes of cases) {
 const result=N.serialize({notes,drums:notes===cases[3]},120,'4/4');
 const expected=notes.map(n=>({start:Math.round(n.start*480),length:Math.max(1,Math.round(n.duration*480)),pitch:n.pitch,velocity:n.velocity})).sort((a,b)=>a.start-b.start||a.pitch-b.pitch||a.length-b.length);
 assert.deepEqual(decode(result),expected);
}
const lane={notes:cases[0],notation:'tempo=120 4/4 C4^ _ D4,'};
assert.equal(N.sync(lane,120,'4/4'),lane.notation);
const old=lane.notation;lane.notes[0]={...lane.notes[0],pitch:65};N.sync(lane,120,'4/4');assert.notEqual(lane.notation,old);assert.match(lane.notation,/F4/);
lane.notes=[];N.sync(lane,120,'4/4');assert.equal(lane.notation,'');
console.log('PASS saved-string retention, edit refresh, deletion, chord/rest/overlap/velocity/tick round trips');
