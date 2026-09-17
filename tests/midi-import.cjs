const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const src=fs.readFileSync(__dirname+'/../src/wds.resone.ui/resources/web/app.js','utf8');
const end=src.indexOf("'use strict';"); const scope={DataView,Uint8Array,ArrayBuffer,Math};
vm.runInNewContext(src.slice(0,end),scope);
const M=scope.MidiImport;
function be16(n){return[(n>>8)&255,n&255]} function be32(n){return[(n>>>24)&255,(n>>>16)&255,(n>>>8)&255,n&255]}
function vlq(n){let a=[n&127];while(n>>=7)a.unshift((n&127)|128);return a}
// Type 0, 480 PPQ, 100 BPM, 3/4. C4 from beat .5 to 1.75, then E4 to beat 3. EOT at 4 beats.
const ev=[
 ...vlq(0),0xff,0x51,3,0x09,0x27,0xc0,
 ...vlq(0),0xff,0x58,4,3,2,24,8,
 ...vlq(240),0x90,60,100,
 ...vlq(600),0x80,60,0,
 ...vlq(0),0x90,64,90,
 ...vlq(600),0x80,64,0,
 ...vlq(480),0xff,0x2f,0
];
const bytes=Uint8Array.from([...Buffer.from('MThd'),...be32(6),...be16(0),...be16(1),...be16(480),...Buffer.from('MTrk'),...be32(ev.length),...ev]);
const p=M.parse(bytes.buffer), lane=M.forLane(p,false);
assert.equal(Math.round(p.tempo),100);assert.equal(p.meter,'3/4');assert.equal(p.lengthBeats,4);
assert.equal(lane.notes.length,2);assert.equal(lane.notes[0].start,.5);assert.equal(lane.notes[0].duration,1.25);assert.equal(lane.notes[1].pitch,64);
console.log('PASS MIDI import tempo, meter, PPQ timing, duration, EOT length');
