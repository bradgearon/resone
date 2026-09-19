// Maintain the lane's Resonator string. Existing server notation stays verbatim
// until notes change; the numeric timeline is only an editor/playback detail.
globalThis.LaneNotation = (() => {
    const ppq = 480, names = ['C','C#','D','D#','E','F','F#','G','G#','A','A#','B'];
    const pitchName = p => names[p % 12] + (Math.floor(p / 12) - 1);
    const signature = lane => JSON.stringify(lane.notes || []);
    function serialize(lane, tempo, meter) {
        const events = (lane.notes || []).map(n => ({
            start: Math.round(n.start * ppq), length: Math.max(1, Math.round(n.duration * ppq)),
            pitch: n.pitch, velocity: n.velocity ?? 96
        })).sort((a,b) => a.start-b.start || a.length-b.length || a.velocity-b.velocity || a.pitch-b.pitch);
        if (!events.length) return '';
        const out = [`tempo=${tempo} ${meter}`];
        if (lane.drums) out.push('mode=drums');
        let cursor = 0;
        function advance(ticks, symbol) {
            for (const [size,suffix] of [[480,''],[240,','],[120,'.']])
                while(ticks >= size) { out.push(symbol+suffix); ticks -= size; cursor += size; }
        }
        for (let i=0;i<events.length;) {
            const n = events[i++], pitches=[n.pitch];
            while(i<events.length && events[i].start===n.start && events[i].length===n.length &&
                  events[i].velocity===n.velocity && !pitches.includes(events[i].pitch)) pitches.push(events[i++].pitch);
            if(n.start>cursor) advance(Math.floor((n.start-cursor)/120)*120,'_');
            const nominal = Math.ceil(n.length/120)*120;
            const base = nominal>=480?480:nominal>=240?240:120;
            const tail = nominal-base;
            const gate = (n.length-tail)/base*100;
            let token = pitches.length===1?pitchName(pitches[0]):'['+pitches.map(pitchName).join(' ')+']';
            token += base===120?'.':base===240?',':'';
            const offset=n.start-cursor;
            if(offset) token+='@'+(offset>0?'+':'')+offset+'t';
            // Explicit gate also avoids the percussion profile shortening hits.
            if(gate!==100 || lane.drums) token+=':'+Number(gate.toFixed(8))+'%';
            token+=':v'+n.velocity;
            out.push(token); cursor+=base;
            advance(tail,'-');
        }
        return out.join(' ');
    }
    function sync(lane,tempo,meter) {
        const current=signature(lane);
        if(!lane.notation || (lane.notationSignature!==undefined && lane.notationSignature!==current))
            lane.notation=serialize(lane,tempo,meter);
        lane.notationSignature=current;
        return lane.notation;
    }
    function accept(lane) { lane.notationSignature=signature(lane); }
    return {serialize,sync,accept};
})();

// Standard MIDI File reader used for drag-in clips. It keeps source PPQ timing and
// converts note timing to quarter-note beats, which is the same unit used by the editor.
globalThis.MidiImport = (() => {
    const u16 = (v,o) => v.getUint16(o,false), u32 = (v,o) => v.getUint32(o,false);
    const ascii = (v,o,n) => String.fromCharCode(...new Uint8Array(v.buffer,v.byteOffset+o,n));
    function vlq(v,state,end) {
        let value=0,count=0;
        while(state.o<end && count++<4) {
            const b=v.getUint8(state.o++); value=(value<<7)|(b&0x7f);
            if(!(b&0x80)) return value;
        }
        throw Error('Invalid MIDI variable-length value.');
    }
    function parse(buffer) {
        const v=buffer instanceof DataView?buffer:new DataView(buffer.buffer||buffer,buffer.byteOffset||0,buffer.byteLength||buffer.byteLength);
        if(v.byteLength<14 || ascii(v,0,4)!=='MThd') throw Error('This is not a Standard MIDI file.');
        const hlen=u32(v,4); if(hlen<6 || 8+hlen>v.byteLength) throw Error('Invalid MIDI header.');
        const format=u16(v,8), trackCount=u16(v,10), division=u16(v,12);
        if(division&0x8000) throw Error('SMPTE-timed MIDI files are not supported yet.');
        const ppq=division; if(!ppq) throw Error('MIDI PPQ cannot be zero.');
        let pos=8+hlen, maxTick=0, tempoEvent=null, meterEvent=null, tempoChanges=0, meterChanges=0;
        const notes=[], channelCounts=Array(16).fill(0), programs=Array(16).fill(null);
        for(let ti=0;ti<trackCount;ti++) {
            if(pos+8>v.byteLength || ascii(v,pos,4)!=='MTrk') throw Error('Invalid MIDI track chunk.');
            const len=u32(v,pos+4), end=pos+8+len; if(end>v.byteLength) throw Error('Truncated MIDI track.');
            const st={o:pos+8}; let tick=0,running=0;
            const active=new Map();
            const closeNote=(ch,note,at)=>{
                const key=(ch<<8)|note, stack=active.get(key); if(!stack?.length) return;
                const started=stack.shift();
                notes.push({tick:started.tick,durationTicks:Math.max(1,at-started.tick),pitch:note,velocity:started.velocity,channel:ch,track:ti});
                channelCounts[ch]++;
            };
            while(st.o<end) {
                tick+=vlq(v,st,end); maxTick=Math.max(maxTick,tick);
                if(st.o>=end) break;
                let status=v.getUint8(st.o++), firstData=null;
                if(status<0x80) { if(!running) throw Error('Invalid MIDI running status.'); firstData=status; status=running; }
                else if(status<0xf0) running=status;
                if(status===0xff) {
                    running=0;
                    if(st.o>=end) break;
                    const type=v.getUint8(st.o++), n=vlq(v,st,end), start=st.o; if(start+n>end) throw Error('Truncated MIDI meta event.');
                    if(type===0x51 && n===3) {
                        const micros=(v.getUint8(start)<<16)|(v.getUint8(start+1)<<8)|v.getUint8(start+2);
                        const e={tick,bpm:60000000/micros}; tempoChanges++; if(!tempoEvent || tick<tempoEvent.tick) tempoEvent=e;
                    } else if(type===0x58 && n>=2) {
                        const e={tick,numerator:v.getUint8(start),denominator:1<<v.getUint8(start+1)};
                        meterChanges++; if(!meterEvent || tick<meterEvent.tick) meterEvent=e;
                    }
                    st.o+=n; continue;
                }
                if(status===0xf0 || status===0xf7) { running=0; st.o+=vlq(v,st,end); if(st.o>end) throw Error('Truncated MIDI SysEx.'); continue; }
                const kind=status&0xf0,ch=status&0x0f, need=(kind===0xc0||kind===0xd0)?1:2;
                let d1=firstData===null?(st.o<end?v.getUint8(st.o++):0):firstData, d2=0;
                if(need===2) { if(st.o>=end) throw Error('Truncated MIDI channel event.'); d2=v.getUint8(st.o++); }
                if(kind===0x90 && d2>0) {
                    const key=(ch<<8)|d1, stack=active.get(key)||[]; stack.push({tick,velocity:d2}); active.set(key,stack);
                } else if(kind===0x80 || (kind===0x90 && d2===0)) closeNote(ch,d1,tick);
                else if(kind===0xc0 && programs[ch]===null) programs[ch]=d1;
            }
            for(const [key,stack] of active) while(stack.length) {
                const started=stack.shift(), note=key&0xff, ch=(key>>8)&0xf;
                notes.push({tick:started.tick,durationTicks:Math.max(1,tick-started.tick),pitch:note,velocity:started.velocity,channel:ch,track:ti});
                channelCounts[ch]++;
            }
            maxTick=Math.max(maxTick,tick); pos=end;
        }
        return {format,trackCount,ppq,maxTick,notes,channelCounts,programs,
            tempo:tempoEvent?.bpm||120,meter:meterEvent?`${meterEvent.numerator}/${meterEvent.denominator}`:'4/4',
            tempoChanges,meterChanges,lengthBeats:maxTick/ppq};
    }
    function forLane(midi,drums) {
        let channels=[];
        for(let ch=0;ch<16;ch++) if(midi.channelCounts[ch] && (drums?ch===9:ch!==9)) channels.push(ch);
        if(!channels.length) for(let ch=0;ch<16;ch++) if(midi.channelCounts[ch]) channels.push(ch);
        if(!channels.length) return {notes:[],channel:null,lengthBeats:midi.lengthBeats};
        channels.sort((a,b)=>midi.channelCounts[b]-midi.channelCounts[a]);
        const channel=channels[0];
        const notes=midi.notes.filter(n=>n.channel===channel).map(n=>({
            start:n.tick/midi.ppq,duration:n.durationTicks/midi.ppq,pitch:n.pitch,velocity:n.velocity
        })).sort((a,b)=>a.start-b.start||a.pitch-b.pitch);
        return {notes,channel,lengthBeats:midi.lengthBeats,otherPlayableChannels:Math.max(0,channels.length-1),program:midi.programs[channel]};
    }
    return {parse,forLane};
})();

'use strict';
const $ = id => document.getElementById(id),
      colors = [ '#f58dc9', '#57c9dc', '#a292f3', '#ffad76', '#76dfa7', '#ffda7d' ];
const roles = [ 'Melody', 'Bass', 'Chords', 'Drums', 'Guitar', 'Strings', 'Vocals' ],
      glyphs = [ '♪', '𝄢', '≡', '▤', '✦', '△', '♬' ];
const newWorkspaceId = () => crypto.randomUUID().replaceAll('-', '');
const fresh = () => ({
    started : false,
    tempo : 120,
    meter : '4/4',
    bars : 16,
    lanes : roles.map((name, i) => ({
                          id : crypto.randomUUID(),
                          name,
                          bank : i === 3 ? 128 : 0,
                          program : [ 0, 32, 89, 0, 24, 48, 53 ][i],
                          volume : .8,
                          muted : false,
                          solo : false,
                          drums : i === 3,
                          vocals : i === 6,
                          includeInAi : i === 0,
                          lyrics : '',
                          voiceId : '',
                          renderedVocalPath : '',
                          renderedVocalSignature : '',
                          vocalGuidance : [],
                          notation : '',
                          originalBrief : '',
                          clipLengthBeats : 0,
                          importedMidiName : '',
                          notes : []
                      }))
});
let song = fresh(), selected = song.lanes[0].id, presets = [], history = [], undo = [], redo = [],
    pending = null, songRun = null, recording = false, transport = 'stopped', anchor = 0, anchorTime = 0, zoom = 28,
    drag = null, savedVoices = [], previewMelodies = [], workspaceSongs = [], workspaceId = newWorkspaceId(),
    workspaceTitle = 'Untitled Song', workspaceProducerDesign = '', workspaceBootstrapped = false, workspaceLoadRequest = '',
    workspaceView = 'history', workspaceSaveTimer = 0, workspaceHistoryVersion = 0, workspaceHistorySavedVersion = 0,
    workspaceProducerVersion = 0, workspaceProducerSavedVersion = 0, workspaceSaveRequests = new Map(), voicePreview = null, voiceAudio = null;
const DEFAULT_VOICE_SAMPLE_TEXT = "Thank you for using Resone by We Develop Software, I can't wait to hear what you create.";
function setVoiceLibrary(payload) {
    savedVoices = Array.isArray(payload?.voices) ? payload.voices.filter(v => v?.id && v?.name) : [];
    previewMelodies = Array.isArray(payload?.previewMelodies) ? payload.previewMelodies : previewMelodies;
    const select = $('vocalVoice');
    if (select) {
        const active = lane()?.voiceId || payload?.lastSelectedVoiceId || '';
        select.replaceChildren();
        if (!savedVoices.length) select.add(new Option('Create a voice…', ''));
        for (const v of savedVoices) select.add(new Option(v.name, v.id));
        const chosen = savedVoices.some(v => v.id === active) ? active
            : savedVoices.some(v => v.id === payload?.lastSelectedVoiceId) ? payload.lastSelectedVoiceId
            : savedVoices[0]?.id || '';
        if (chosen) select.value = chosen;
        for (const l of song.lanes) if (l.vocals && (!l.voiceId || l.voiceId === 'default' || !savedVoices.some(v => v.id === l.voiceId))) l.voiceId = chosen;
    }
    const melody = $('voicePreviewMelody');
    if (melody && previewMelodies.length) {
        const current = melody.value;
        melody.replaceChildren(...previewMelodies.map(v => new Option(v.name, v.id)));
        melody.value = previewMelodies.some(v => v.id === current) ? current : (previewMelodies[0]?.id || 'fun');
    }
    render();
}
const clone = x => JSON.parse(JSON.stringify(x)),
      lane = () => song.lanes.find(l => l.id === selected) || song.lanes[0];
function normalizedWorkspaceTitle(value) {
    const t = String(value || '').replace(/\s+/g, ' ').trim();
    return (t || 'Untitled Song').slice(0, 120);
}
function isGenericWorkspaceTitle(value) {
    const t=String(value||'').replace(/\s+/g,' ').trim().toLowerCase();
    return !t || t==='untitled' || t==='untitled song' || t==='new song' || t==='song' || t==='new composition';
}
function provisionalSongTitle(brief) {
    let t=String(brief||'').replace(/\s+/g,' ').trim();
    t=t.replace(/^(?:please\s+)?(?:make|create|write|compose|generate)\s+(?:me\s+)?(?:a|an|the)?\s*/i,'').trim();
    const words=t.split(' ').filter(Boolean);
    if(words.length>7)t=words.slice(0,7).join(' ');
    if(t.length>64)t=t.slice(0,64).trimEnd()+'…';
    return normalizedWorkspaceTitle(t);
}
function songDesignTitle(payload, brief) {
    const candidates=[payload?.title,payload?.state?.title];
    const design=String(payload?.design||'');
    const m=design.match(/^\s*(?:[-*]\s*)?(?:\*{1,2})?Song\s+title(?:\*{1,2})?\s*:\s*(?:\*{1,2})?([^\r\n]+)/im);
    if(m)candidates.push(m[1].replace(/^[\s*_`"'“”]+|[\s*_`"'“”]+$/g,''));
    for(const value of candidates) if(value && !isGenericWorkspaceTitle(value)) return normalizedWorkspaceTitle(value);
    return provisionalSongTitle(brief);
}
function workspaceHistoryPayload() {
    // History snapshots are already immutable clones when they are created. Avoid cloning
    // every saved song again on every autosave; JSON serialization in send() is enough.
    return history.slice(-40).map(h => ({brief:h.brief || '', laneId:h.laneId || '', createdUtc:h.createdUtc || new Date().toISOString(), song:h.song}));
}
function saveWorkspace(force=false) {
    if (!workspaceBootstrapped) return;
    if (!force && !song.started && !history.length && workspaceTitle === 'Untitled Song') return;
    clearTimeout(workspaceSaveTimer);
    const payload={id:workspaceId,title:workspaceTitle,project:song};
    const ack={historyVersion:null,producerVersion:null};
    if (workspaceHistoryVersion !== workspaceHistorySavedVersion) {
        payload.history=workspaceHistoryPayload(); ack.historyVersion=workspaceHistoryVersion;
    }
    if (workspaceProducerVersion !== workspaceProducerSavedVersion) {
        payload.producerDesign=workspaceProducerDesign; ack.producerVersion=workspaceProducerVersion;
    }
    const id=crypto.randomUUID(); workspaceSaveRequests.set(id,ack); send('workspaceSave',payload,id);
}
function scheduleWorkspaceSave() {
    if (!workspaceBootstrapped) return;
    clearTimeout(workspaceSaveTimer);
    workspaceSaveTimer = setTimeout(() => saveWorkspace(false), 350);
}
function updateSongIdentity() {
    if ($('songTitle') && document.activeElement !== $('songTitle')) $('songTitle').textContent = workspaceTitle;
}
function setWorkspaceSongs(values) {
    workspaceSongs = Array.isArray(values) ? values : [];
    renderSongList();
}
function renderSongList() {
    const root = $('songList'); if (!root) return;
    root.replaceChildren();
    if (!workspaceSongs.length) {
        const p=document.createElement('p'); p.className='empty'; p.textContent='Saved songs will appear here.'; root.append(p); return;
    }
    for (const item of workspaceSongs) {
        const b=document.createElement('button'); b.className='songListItem'+(item.id===workspaceId?' active':'');
        const text=document.createElement('span'); text.className='songListText';
        const strong=document.createElement('strong'); strong.textContent=item.title || 'Untitled Song';
        const small=document.createElement('small');
        const date=item.updatedUtc?new Date(item.updatedUtc):null; small.textContent=date&&!Number.isNaN(date.getTime())?'Updated '+date.toLocaleString():'Saved song';
        text.append(strong,small); b.append(text); b.onclick=()=>loadWorkspaceSong(item.id); root.append(b);
    }
}
function loadWorkspaceSong(id) {
    if (pending || !id || id===workspaceId) return;
    saveWorkspace(false);
    workspaceLoadRequest=crypto.randomUUID();
    send('workspaceLoad',{id},workspaceLoadRequest); status('Loading saved song…');
}
function createNewWorkspaceSong(forceSave=false) {
    if (forceSave) saveWorkspace(true);
    send('stop'); song=fresh(); selected=song.lanes[0].id; history=[]; undo=[]; redo=[]; songRun=null;
    workspaceId=newWorkspaceId(); workspaceTitle='Untitled Song'; workspaceProducerDesign='';
    workspaceHistoryVersion=workspaceHistorySavedVersion=0; workspaceProducerVersion=workspaceProducerSavedVersion=0; workspaceSaveRequests.clear();
    $('brief').value=''; updateSongIdentity(); renderHistory(); renderSongList(); commit();
    status('New song. Describe your first idea.');
}
function toggleLibraryView() {
    workspaceView = workspaceView === 'history' ? 'songs' : 'history';
    $('leftTrack')?.classList.toggle('showSongs', workspaceView === 'songs');
    if ($('leftPanelHeading')) $('leftPanelHeading').textContent = workspaceView === 'songs' ? 'Songs' : 'History';
    if ($('historyCount')) $('historyCount').textContent = workspaceView === 'songs' ? `${workspaceSongs.length} saved` : `${history.length} prompts`;
}
function vocalSignature(l) {
    return JSON.stringify({tempo:song.tempo,notes:l.notes||[],lyrics:l.lyrics||'',voiceId:l.voiceId||'',guidance:l.vocalGuidance||[]});
}
function invalidateVocalRenders() {
    for (const l of song.lanes) if (l.vocals && l.renderedVocalPath && l.renderedVocalSignature !== vocalSignature(l)) {
        l.renderedVocalPath = ''; l.renderedVocalSignature = '';
    }
}
function normalizeLaneAiDefaults(project) {
    if (!project?.lanes?.length) return project;
    project.lanes.forEach((l, i) => {
        // Preserve explicit saved choices. Older projects that predate the
        // checkbox receive the new default: Melody/first lane only.
        if (typeof l.includeInAi !== 'boolean') l.includeInAi = i === 0 || l.name === 'Melody';
        if (typeof l.vocals !== 'boolean') l.vocals = l.name === 'Vocals';
        if (typeof l.lyrics !== 'string') l.lyrics = '';
        if (typeof l.voiceId !== 'string') l.voiceId = '';
        if (typeof l.renderedVocalPath !== 'string') l.renderedVocalPath = '';
        if (typeof l.renderedVocalSignature !== 'string') l.renderedVocalSignature = '';
        if (!Array.isArray(l.vocalGuidance)) l.vocalGuidance = [];
    });
    return project;
}
function send(op, payload = {}, requestId = '') {
    if (typeof IPlugSendMsg !== 'function') {
        status('Native bridge unavailable. Open the standalone app or VST3.');
        return;
    }
    const bytes = new TextEncoder().encode(JSON.stringify({op, requestId, payload}));
    let s = '';
    for (const b of bytes)
        s += String.fromCharCode(b);
    IPlugSendMsg({msg : 'SAMFUI', msgTag : 1, ctrlTag : -1, data : btoa(s)});
}
function status(s) {
    $('status').textContent = s;
}
function commit() {
    for (const l of song.lanes) LaneNotation.sync(l, song.tempo, song.meter);
    send('project', song);
    render();
    scheduleWorkspaceSave();
}
function checkpoint() {
    undo.push(clone(song));
    if (undo.length > 40)
        undo.shift();
    redo = [];
}
function changed() {
    invalidateVocalRenders();
    send('stop');
    transport = 'stopped';
    commit();
}
function beatsPerBar() {
    let [n, d] = song.meter.split('/').map(Number);
    return n * 4 / d;
}
function duration() {
    return Math.max(0, ...song.lanes.flatMap(l => [Number(l.clipLengthBeats)||0, ...l.notes.map(n => n.start + n.duration)]));
}
function readFields() {
    const tempo = Number($('tempo').value), bars = Number($('bars').value), meter = $('meter').value.trim();
    if (!Number.isInteger(tempo) || tempo < 30 || tempo > 240)
        throw Error('Tempo must be a whole number from 30 to 240.');
    if (!Number.isInteger(bars) || bars < 1 || bars > 64)
        throw Error('Bars must be a whole number from 1 to 64.');
    if (!/^(?:[1-9]|1[0-2])\/(?:2|4|8|16)$/.test(meter))
        throw Error('Use a meter such as 4/4, 3/4 or 6/8.');
    song.tempo = tempo;
    song.bars = bars;
    song.meter = meter;
}
function busy() {
    const b = !!pending;
    $('send').disabled = b && !recording;
    $('voice').disabled = b && !recording;
    $('cancel').hidden = !b && !recording;
    $('voice').textContent = recording ? '➜ Send voice' : '●  Speak your idea';
    $('export').disabled = b;
    $('dragFullMidi').disabled = b || !duration();
    $('play').disabled = b;
    $('restart').disabled = b;
    $('clear').disabled = b || recording;
    if ($('saveNew')) $('saveNew').disabled = b || recording;
    if ($('newVoice')) $('newVoice').disabled = b || recording;
    if ($('deleteSong')) $('deleteSong').disabled = b || recording;
    $('songMode').disabled = b || recording;
    if ($('renderVocals')) $('renderVocals').disabled = b || recording || !lane()?.vocals || !(lane()?.notes?.length) || !lane()?.voiceId || !savedVoices.some(v=>v.id===lane().voiceId); 
    for (const control of document.querySelectorAll('.laneActions input')) control.disabled = b;
    for (const control of document.querySelectorAll('.laneActions button')) control.disabled = b || song.lanes.length === 1;
}
function requestMusic(text) {
    try {
        readFields();
        if (!text.trim())
            throw Error('Describe the music you want to create.');
        if (pending)
            return;
        const id = crypto.randomUUID();
        for (const l of song.lanes) LaneNotation.sync(l, song.tempo, song.meter);
        const previous = clone(song);
        const includedIds = [selected];
        if (!previous.lanes.some(l => l.id === selected)) throw Error('Select a lane to generate.');
        const requestProject = clone(previous);
        // Send all current lanes as context; only selected is a generation target.
        pending = {id, kind : 'compose', brief : text, laneId : selected, previous, includedIds};
        send('stop');
        transport = 'stopped';
        anchor = 0;
        // Keep the revision context in the request, but not in the active view/audio.
        for (const l of song.lanes) if (includedIds.includes(l.id)) Object.assign(l, {notes : [], notation : ''});
        render();
        send('compose', {project : requestProject, laneId : selected, description : text, useAhd : $('ahd').checked},
             id);
        const target = previous.lanes.find(l => l.id === selected);
        status((target.notes.length ? 'Updating ' : 'Creating ') + target.name + '…');
        busy();
    } catch (e) {
        status(e.message);
    }
}
function restorePendingComposition() {
    if (pending?.kind === 'compose' && pending.previous) {
        song = pending.previous;
        selected = pending.laneId;
        render();
    }
}
function restoreSongGeneration() {
    if (typeof songRun !== 'undefined' && songRun?.previous) {
        song = songRun.previous;
        selected = songRun.selectedLaneId || selected;
        songRun = null;
        render();
    }
}
function restorePendingWork() {
    if (pending?.kind === 'compose') restorePendingComposition();
    else if (pending?.kind === 'songDesign' || pending?.kind === 'songChunk') restoreSongGeneration();
}
function songModeEnabled() { return !!$('songMode').checked; }
function saveSongMode() {
    try { localStorage.setItem('resone.songMode', songModeEnabled() ? '1' : '0'); } catch {}
}
function selectedSongLaneIds() {
    return song.lanes.filter(l => l.includeInAi !== false).map(l => l.id);
}
function startSongGeneration(text) {
    try {
        readFields();
        if (!text.trim()) throw Error('Describe the song you want to create.');
        if (pending) return;
        for (const l of song.lanes) LaneNotation.sync(l, song.tempo, song.meter);
        const includedIds = selectedSongLaneIds();
        if (!includedIds.length) throw Error('Check at least one lane for song generation.');
        const previous = clone(song), id = crypto.randomUUID();
        // Give the workspace a useful identity immediately. The producer's
        // generated title replaces this when SongDesign returns, but autosave can
        // no longer create a run of "Untitled Song" entries while generation is active.
        if (isGenericWorkspaceTitle(workspaceTitle)) {
            workspaceTitle=provisionalSongTitle(text);
            updateSongIdentity(); renderSongList(); scheduleWorkspaceSave();
        }
        songRun = {
            brief : text.trim(), previous, includedIds, state : null, design : '',
            sectionIndex : 0, laneIndex : 0, selectedLaneId : selected,
            completedChunks : 0, failures : []
        };
        pending = {id, kind : 'songDesign', brief : text.trim(), previous, includedIds};
        send('stop'); transport = 'stopped'; anchor = 0;
        status('Song · Producer planning…'); busy();
        send('songDesign', {description : text.trim(), tempo : song.tempo, meter : song.meter,
            targetBars : song.bars, useAhd : $('ahd').checked}, id);
    } catch (e) { status(e.message); }
}
function sectionBounds(section) {
    const bpb = beatsPerBar(), start = Number(section.startBar || 0) * bpb,
          length = Math.max(1, Number(section.bars || 1)) * bpb;
    return {start, length, end : start + length};
}
function sectionProject(section) {
    const {start, length, end} = sectionBounds(section), keep = new Set(songRun.includedIds);
    const project = {tempo : song.tempo, meter : song.meter, bars : Math.max(1, Number(section.bars || 1)), lanes : []};
    for (const source of song.lanes) {
        if (!keep.has(source.id)) continue;
        const l = clone(source);
        l.notes = (source.notes || []).filter(n => n.start < end && n.start + n.duration > start).map(n => {
            const localStart = Math.max(0, n.start - start), localEnd = Math.min(length, n.start + n.duration - start);
            return {...n, start : localStart, duration : Math.max(.001, localEnd - localStart)};
        }).filter(n => n.start < length && n.duration > 0);
        l.clipLengthBeats = length;
        l.notation = '';
        delete l.notationSignature;
        LaneNotation.sync(l, project.tempo, project.meter); LaneNotation.accept(l);
        project.lanes.push(l);
    }
    return project;
}
function nextSongChunk() {
    if (!songRun?.state?.sections?.length) return finishSongGeneration();
    if (songRun.sectionIndex >= songRun.state.sections.length) return finishSongGeneration();
    const section = songRun.state.sections[songRun.sectionIndex];
    while (songRun.laneIndex < songRun.includedIds.length && !song.lanes.some(l => l.id === songRun.includedIds[songRun.laneIndex]))
        songRun.laneIndex++;
    if (songRun.laneIndex >= songRun.includedIds.length) {
        songRun.sectionIndex++; songRun.laneIndex = 0; return nextSongChunk();
    }
    const laneId = songRun.includedIds[songRun.laneIndex], target = song.lanes.find(l => l.id === laneId);
    if (!target) { songRun.laneIndex++; return nextSongChunk(); }
    const id = crypto.randomUUID(), project = sectionProject(section), bounds = sectionBounds(section);
    pending = {id, kind : 'songChunk', laneId, sectionId : section.id, sectionIndex : songRun.sectionIndex,
        laneIndex : songRun.laneIndex, startBeat : bounds.start, endBeat : bounds.end, sectionLength : bounds.length};
    status(`Song · Section ${songRun.sectionIndex + 1}/${songRun.state.sections.length} · ${target.name} · Queued…`);
    busy();
    send('songChunk', {project, songState : songRun.state, sectionId : section.id, laneId,
        description : section.plan || `${section.title} section`, useAhd : $('ahd').checked}, id);
}
function skipFailedSongChunk(message) {
    if (!songRun || pending?.kind !== 'songChunk') return false;
    const section = songRun.state?.sections?.[pending.sectionIndex], target = song.lanes.find(l => l.id === pending.laneId);
    const label = `${section?.title || pending.sectionId || 'Section'} · ${target?.name || pending.laneId}`;
    songRun.failures.push({sectionId : pending.sectionId, laneId : pending.laneId, message : String(message || 'Generation failed.')});
    songRun.laneIndex = pending.laneIndex + 1;
    pending = null;
    status(`Song · Skipped ${label}: ${message || 'generation failed'} · continuing…`);
    busy();
    nextSongChunk();
    return true;
}

function applySongChunk(payload) {
    if (!songRun || pending?.kind !== 'songChunk') throw Error('Song generation state was lost.');
    const tracks = payload.tracks;
    if (!Array.isArray(tracks) || tracks.length !== 1 || tracks[0].laneId !== pending.laneId || !Array.isArray(tracks[0].notes) || !tracks[0].notes.length)
        throw Error('Invalid song-section arrangement data.');
    const t = tracks[0], target = song.lanes.find(l => l.id === pending.laneId);
    if (!target) throw Error('Song lane disappeared during generation.');
    const start = pending.startBeat, end = pending.endBeat, length = pending.sectionLength;
    const kept = (target.notes || []).filter(n => n.start < start || n.start >= end);
    const shifted = t.notes.filter(n => Number.isFinite(n.start) && Number.isFinite(n.duration) && n.duration > 0 && n.start < length + 1e-6)
        .map(n => ({...n, start : start + Math.max(0, n.start), duration : Math.min(n.duration, Math.max(.001, end - (start + Math.max(0, n.start))))}));
    target.notes = [...kept, ...shifted].sort((a,b) => a.start-b.start || a.pitch-b.pitch);
    target.originalBrief = target.originalBrief || songRun.brief;
    target.prompts = [...(target.prompts || []), `[${songRun.state.sections[pending.sectionIndex].title}] ${songRun.brief}`];
    target.clipLengthBeats = Math.max(Number(target.clipLengthBeats)||0, ...songRun.state.sections.map(s => sectionBounds(s).end));
    target.notation = ''; delete target.notationSignature;
    if (target.vocals) { target.renderedVocalPath = ''; target.renderedVocalSignature = ''; }
    LaneNotation.sync(target, song.tempo, song.meter); LaneNotation.accept(target);
    songRun.state = payload.songState || songRun.state;
    song.started = true;
    songRun.completedChunks = (songRun.completedChunks || 0) + 1;
    songRun.laneIndex = pending.laneIndex + 1;
    pending = null;
    commit();
    nextSongChunk();
}
function finishSongGeneration() {
    if (!songRun) return;
    const run = songRun, completed = Number(run.completedChunks || 0), failures = run.failures || [];
    if (completed > 0) {
        undo.push(run.previous); if (undo.length > 40) undo.shift(); redo = [];
    }
    selected = run.selectedLaneId && song.lanes.some(l => l.id === run.selectedLaneId) ? run.selectedLaneId : song.lanes[0].id;
    if (completed > 0) {
        history.push({brief : run.brief, song : clone(song), laneId : selected, createdUtc:new Date().toISOString()}); if (history.length > 40) history.shift(); if (typeof workspaceHistoryVersion !== 'undefined') workspaceHistoryVersion++;
    }
    songRun = null; pending = null; commit(); renderHistory(); busy();
    if (failures.length)
        status(`Song complete with ${failures.length} skipped generation${failures.length === 1 ? '' : 's'}. ${completed} chunk${completed === 1 ? '' : 's'} completed.`);
    else
        status('Song complete. Generated every checked lane through every producer section.');
    if (duration()) send('play', song);
}
function submit() {
    if (recording) {
        send('voiceSend');
        status('Transcribing your idea…');
        return;
    }
    const text = $('brief').value;
    if (songModeEnabled()) startSongGeneration(text); else requestMusic(text);
}
$('send').onclick = submit;
$('brief').onkeyup = e => e.stopPropagation();
$('brief').onkeydown = e => {
    e.stopPropagation();
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter')
        submit();
};
$('voice').onclick = () => {
    if (recording) {
        submit();
        return;
    }
    try {
        readFields();
        const id = crypto.randomUUID();
        pending = {id, kind : 'voice', laneId : selected};
        // Start capture first so ASR initialization can never delay the microphone. Then warm the
        // bundled whisper.cpp runtime in parallel while the user is speaking.
        send('voiceStart', {}, id);
        send('asrWarm', {}, crypto.randomUUID());
        busy();
    } catch (e) {
        status(e.message);
    }
};
$('cancel').onclick = () => {
    if (recording)
        send('voiceCancel');
    else if (pending)
        send('cancel', {}, pending.id);
    restorePendingWork();
    pending = null;
    if (typeof songRun !== 'undefined') songRun = null;
    recording = false;
    busy();
    status('Cancelled.');
};
for (const b of document.querySelectorAll('.suggestions button'))
    b.onclick = () => { if (pending || recording) return; $('brief').value = b.textContent; submit(); };
$('settings').onclick = () => $('config').showModal();
$('saveSettings').onclick = e => {
    const url = $('apiUrl').value.trim();
    if (!/^wss?:\/\//.test(url)) {
        e.preventDefault();
        status('Use a ws:// or wss:// URL.');
        return;
    }
    send('settings', {apiUrl : url});
};
$('clear').onclick = () => { if (!pending) createNewWorkspaceSong(false); };
$('saveNew').onclick = () => { if (!pending) createNewWorkspaceSong(true); };
$('undo').onclick = () => {
    if (undo.length && !pending) {
        redo.push(clone(song));
        song = undo.pop();
        changed();
    }
};
$('redo').onclick = () => {
    if (redo.length && !pending) {
        undo.push(clone(song));
        song = redo.pop();
        changed();
    }
};
for (const id of ['tempo', 'meter', 'bars'])
    $(id).onchange = () => {
        const old = clone(song);
        try {
            readFields();
            undo.push(old);
            redo = [];
            changed();
        } catch (e) {
            status(e.message);
            render();
        }
    };
$('instrument').onchange = () => {
    const p = presets[Number($('instrument').value)];
    if (!p)
        return;
    checkpoint();
    Object.assign(lane(), {bank : p.bank, program : p.program});
    changed();
};
$('addLane').onclick = () => {
    if (song.lanes.length === 16) {
        status('All 16 lanes are in use.');
        return;
    }
    checkpoint();
    const l = {
        id : crypto.randomUUID(),
        name : 'Accent ' + (song.lanes.length - 3),
        bank : 0,
        program : 0,
        volume : .8,
        muted : false,
        solo : false,
        drums : false,
        vocals : false,
        includeInAi : false,
        lyrics : '',
        voiceId : '',
        renderedVocalPath : '',
        renderedVocalSignature : '',
        vocalGuidance : [],
        notation : '',
        originalBrief : '',
        clipLengthBeats : 0,
        importedMidiName : '',
        notes : []
    };
    song.lanes.push(l);
    selected = l.id;
    changed();
};
$('play').onclick = () => {
    if (transport === 'playing') {
        send('pause', true);
        return;
    }
    if (transport === 'paused') {
        send('pause', false);
        return;
    }
    try {
        readFields();
        if (!duration()) {
            status('Create some music first.');
            return;
        }
        send('play', song);
    } catch (e) {
        status(e.message);
    }
};
$('restart').onclick = () => {
    if (duration())
        send('play', song);
};
$('stop').onclick = () => send('stop');
$('export').onclick = () => {
    if (!duration()) {
        status('Create some music first.');
        return;
    }
    const id = crypto.randomUUID();
    pending = {id, kind : 'export'};
    send('export', song, id);
    busy();
};
$('dragFullMidi').onclick = e => e.preventDefault();
$('dragFullMidi').onpointerdown = e => {
    if (e.button !== 0 || pending || !duration()) return;
    e.preventDefault();
    status('Dragging full multitrack MIDI…');
    send('dragProjectMidi', {project : clone(song)});
};
$('zoom').oninput = () => {
    zoom = Number($('zoom').value);
    draw();
};
$('zoomIn').onclick = () => {
    zoom = Math.min(100, zoom + 8);
    $('zoom').value = zoom;
    draw();
};
$('zoomOut').onclick = () => {
    zoom = Math.max(12, zoom - 8);
    $('zoom').value = zoom;
    draw();
};
$('fit').onclick = () => {
    zoom = Math.max(4, ($('timeline').clientWidth - 20) / Math.max(duration(), song.bars * beatsPerBar()));
    $('zoom').value = zoom;
    draw();
};
function instrumentName(l) {
    return presets.find(p => p.bank === l.bank && p.program === l.program)?.name ||
           (l.drums ? 'Standard kit' : 'Program ' + (l.program + 1));
}
function renderHistory() {
    const root = $('history');
    root.replaceChildren();
    $('historyCount').textContent = workspaceView === 'songs' ? workspaceSongs.length + ' saved' : history.length + ' prompts';
    if (!history.length) {
        const p = document.createElement('p');
        p.className = 'empty';
        p.textContent = 'Your ideas start here. Every variation keeps a snapshot.';
        root.append(p);
    }
    for (const h of [...history].reverse()) {
        const b = document.createElement('button');
        b.className = 'historyItem';
        b.textContent = h.brief;
        b.title = h.brief;
        b.onclick = () => {
            if (pending)
                return;
            checkpoint();
            song = clone(h.song);
            selected = h.laneId;
            $('brief').value = h.brief;
            changed();
        };
        root.append(b);
    }
}
function render() {
    if (!song.lanes.some(l => l.id === selected))
        selected = song.lanes[0].id;
    for (const id of ['tempo', 'meter', 'bars'])
        $(id).value = song[id];
    $('selectedLabel').textContent = lane().name.toUpperCase();
    $('send').title = 'Generate or update ' + lane().name;
    $('targetLane').textContent = songModeEnabled()
        ? 'Song mode: producer plans sections; checked lanes generate one at a time · Editing: ' + lane().name
        : 'Editing: ' + lane().name + ' · Drop MIDI anywhere to import into this lane';
    updateSongIdentity();
    const vocal = lane(), vocalPanel = $('vocalPanel');
    vocalPanel.hidden = !vocal.vocals;
    if (vocal.vocals) {
        if (document.activeElement !== $('vocalLyrics')) $('vocalLyrics').value = vocal.lyrics || '';
        const voiceSelect=$('vocalVoice');
        if (voiceSelect) {
            const wanted=vocal.voiceId || '';
            if (wanted && [...voiceSelect.options].some(o=>o.value===wanted)) voiceSelect.value=wanted;
            else if (savedVoices.length) { vocal.voiceId=savedVoices[0].id; voiceSelect.value=vocal.voiceId; }
        }
        $('renderVocals').textContent = vocal.renderedVocalPath ? '♬ Re-render singing' : '♬ Render singing';
    }
    const seconds = duration() * 60 / song.tempo;
    $('duration').textContent =
        Math.floor(seconds / 60) + ':' + String(Math.floor(seconds % 60)).padStart(2, '0');
    const select = $('instrument');
    select.replaceChildren();
    presets.forEach((p, i) => {
        if ((p.bank === 128) !== lane().drums)
            return;
        const o = new Option(p.name, i);
        o.selected = p.bank === lane().bank && p.program === lane().program;
        select.add(o);
    });
    if (!select.options.length)
        select.add(new Option('Loading instruments…', ''));
    const lanes = $('lanes'), outputs = $('outputs');
    lanes.replaceChildren();
    outputs.replaceChildren();
    song.lanes.forEach((l, i) => {
        const row = document.createElement('div');
        row.className = 'lane' + (l.id === selected ? ' selected' : '') + (l.includeInAi === false ? ' songExcluded' : '');
        row.style.setProperty('--lane', colors[i % 6]);
        const title = document.createElement('div');
        title.className = 'laneTitle';
        const icon = document.createElement('span');
        icon.className = 'glyph';
        icon.textContent = l.vocals ? '♬' : glyphs[i % glyphs.length];
        const name = document.createElement('div');
        name.textContent = l.name;
        const sub = document.createElement('small');
        sub.textContent = instrumentName(l);
        name.append(sub);
        title.append(icon, name);
        row.append(title);
        const actions = document.createElement('div');
        actions.className = 'laneActions';
        const includeLabel = document.createElement('label');
        includeLabel.className = 'laneIncludeLabel';
        includeLabel.title = 'Include this lane in Song mode generation';
        const include = document.createElement('input');
        include.type = 'checkbox'; include.className = 'laneInclude'; include.checked = l.includeInAi !== false;
        include.onclick = e => e.stopPropagation();
        include.onchange = e => { e.stopPropagation(); checkpoint(); l.includeInAi = include.checked; changed(); };
        includeLabel.onclick = e => e.stopPropagation();
        includeLabel.append(include, document.createTextNode('Song'));
        actions.append(includeLabel);
        const remove = document.createElement('button');
        remove.textContent = '×';
        remove.title = 'Delete ' + l.name + ' lane';
        remove.setAttribute('aria-label', remove.title);
        remove.disabled = !!pending || song.lanes.length === 1;
        remove.onclick = e => {
            e.stopPropagation();
            if (pending || song.lanes.length === 1) return;
            checkpoint();
            song.lanes = song.lanes.filter(x => x.id !== l.id);
            if (selected === l.id) selected = song.lanes[0].id;
            changed();
        };
        actions.append(remove);
        row.append(actions);
        const controls = document.createElement('div');
        controls.className = 'laneControls';
        for (const [key, label] of [[ 'muted', 'M' ], [ 'solo', 'S' ]]) {
            const b = document.createElement('button');
            b.textContent = label;
            b.title = key;
            b.className = l[key] ? 'active' : '';
            b.onclick = e => {
                e.stopPropagation();
                checkpoint();
                l[key] = !l[key];
                changed();
            };
            controls.append(b);
        }
        const gain = document.createElement('input');
        gain.type = 'range';
        gain.min = 0;
        gain.max = 1;
        gain.step = .01;
        gain.value = l.volume;
        gain.title = 'Lane volume';
        gain.onpointerdown = e => {
            e.stopPropagation();
            checkpoint();
        };
        gain.onclick = e => e.stopPropagation();
        gain.onchange = () => {
            l.volume = Number(gain.value);
            changed();
        };
        controls.append(gain);
        row.append(controls);
        row.onclick = () => {
            selected = l.id;
            render();
        };
        lanes.append(row);
        const out = document.createElement('div');
        out.className = 'output';
        out.style.setProperty('--lane', colors[i % 6]);
        const g = document.createElement('span');
        g.className = 'glyph';
        g.textContent = l.vocals ? '♬' : glyphs[i % glyphs.length];
        const t = document.createElement('div');
        t.textContent = l.name + ' MIDI';
        const n = document.createElement('small');
        n.textContent = l.notes.length + ' notes · ' + (l.vocals && l.renderedVocalPath ? 'rendered vocal' : instrumentName(l));
        t.append(n);
        const drag = document.createElement('button');
        drag.textContent = '⠿ Drag MIDI';
        drag.className = 'midiDrag';
        drag.title = 'Drag this lane into your DAW or a folder';
        drag.disabled = !l.notes.length || !!pending;
        drag.onclick = e => e.stopPropagation();
        drag.onpointerdown = e => {
            if (e.button !== 0 || pending || !l.notes.length) return;
            // Start the native OLE drag while the mouse button is definitely still down.
            // Waiting for a later pointermove can let WebView2 consume/release capture before
            // Windows receives the file drag, which results in no file cursor at all.
            e.preventDefault();
            e.stopPropagation();
            status('Dragging ' + l.name + ' MIDI…');
            send('dragMidi', {laneId : l.id, project : clone(song)});
        };
        out.append(g, t, drag);
        out.onclick = () => {
            selected = l.id;
            render();
        };
        outputs.append(out);
    });
    draw();
    busy();
}
const svgNS = 'http://www.w3.org/2000/svg';
function svg(tag, attrs) {
    const e = document.createElementNS(svgNS, tag);
    for (const [k, v] of Object.entries(attrs))
        e.setAttribute(k, v);
    return e;
}
function pitchRange(l) {
    const values = l.notes.map(n => n.pitch);
    let lo = Math.min(48, ...values) - 2, hi = Math.max(72, ...values) + 2;
    return {lo, hi};
}
function noteY(l, n, i) {
    const {lo, hi} = pitchRange(l);
    return 26 + i * 78 + 8 + (hi - n.pitch) / (hi - lo) * 59;
}
function draw() {
    const root = $('piano');
    root.replaceChildren();
    const end = Math.max(duration(), song.bars * beatsPerBar()),
          width = Math.max($('timeline').clientWidth, end * zoom + 20), height = 26 + song.lanes.length * 78;
    root.setAttribute('width', width);
    root.setAttribute('height', height);
    root.append(svg('rect', {x : 0, y : 0, width, height, fill : '#11202b'}));
    for (let beat = 0; beat <= end; beat++) {
        const bar = beat % beatsPerBar() === 0;
        root.append(svg('line', {
            x1 : beat * zoom,
            x2 : beat * zoom,
            y1 : 26,
            y2 : height,
            stroke : bar ? '#344b5b' : '#203341',
            'stroke-width' : bar ? 1 : .6
        }));
        if (bar) {
            const t = svg('text', {x : beat * zoom + 5, y : 18, fill : '#809aaf', 'font-size' : 11});
            t.textContent = String(Math.floor(beat / beatsPerBar()) + 1);
            root.append(t);
        }
    }
    song.lanes.forEach((l, i) => {
        root.append(svg('rect', {
            x : 0,
            y : 26 + i * 78,
            width,
            height : 78,
            fill : l.id === selected ? '#789bbe0b' : 'transparent',
            stroke : '#344654',
            'stroke-width' : .6
        }));
        for (let j = 1; j < 4; j++)
            root.append(svg('line', {
                x1 : 0,
                x2 : width,
                y1 : 26 + i * 78 + j * 19.5,
                y2 : 26 + i * 78 + j * 19.5,
                stroke : '#20313e',
                'stroke-width' : .5
            }));
        l.notes.forEach((n, index) => {
            const r = svg('rect', {
                x : n.start * zoom,
                y : noteY(l, n, i),
                width : Math.max(3, n.duration * zoom - 2),
                height : 6,
                rx : 1,
                fill : colors[i % 6],
                class : 'note',
                'data-lane' : i,
                'data-note' : index
            });
            const t = svg('title', {});
            t.textContent = 'MIDI ' + n.pitch + ' · ' + n.duration + ' beats';
            r.append(t);
            root.append(r);
        });
    });
}
$('piano').onpointerdown = e => {
    const r = e.target.closest('.note');
    if (!r || e.button !== 0 || pending)
        return;
    const li = Number(r.dataset.lane), ni = Number(r.dataset.note), l = song.lanes[li], n = l.notes[ni];
    selected = l.id;
    checkpoint();
    const bounds = r.getBoundingClientRect();
    drag = {
        li,
        ni,
        x : e.clientX,
        y : e.clientY,
        start : n.start,
        pitch : n.pitch,
        duration : n.duration,
        resize : e.clientX > bounds.right - 7,
        range : pitchRange(l)
    };
    $('piano').setPointerCapture(e.pointerId);
};
$('piano').onpointermove = e => {
    if (!drag)
        return;
    const d = drag, n = song.lanes[d.li].notes[d.ni];
    if (d.resize)
        n.duration = Math.max(.25, Math.round((d.duration + (e.clientX - d.x) / zoom) * 4) / 4);
    else {
        n.start = Math.max(0, Math.round((d.start + (e.clientX - d.x) / zoom) * 4) / 4);
        n.pitch = Math.max(
            0, Math.min(127, d.pitch - Math.round((e.clientY - d.y) / 59 * (d.range.hi - d.range.lo))));
    }
    draw();
};
$('piano').onpointerup = () => {
    if (drag) {
        song.lanes[drag.li].notation = '';
        drag = null;
        changed();
    }
};
$('piano').ondblclick = e => {
    if (e.target.closest('.note') || pending)
        return;
    const r = $('piano').getBoundingClientRect(), i = Math.floor((e.clientY - r.top - 26) / 78);
    if (i < 0 || i >= song.lanes.length)
        return;
    const l = song.lanes[i], range = pitchRange(l);
    checkpoint();
    l.notes.push({
        start : Math.max(0, Math.round((e.clientX - r.left) / zoom * 4) / 4),
        duration : 1,
        pitch : Math.max(0, Math.min(127, Math.round(range.hi - (e.clientY - r.top - 26 - i * 78 - 8) / 59 *
                                                                    (range.hi - range.lo)))),
        velocity : 96
    });
    l.notes.sort((a, b) => a.start - b.start);
    l.notation = '';
    selected = l.id;
    changed();
};
$('piano').oncontextmenu = e => {
    e.preventDefault();
    const r = e.target.closest('.note');
    if (r && !pending) {
        checkpoint();
        const l = song.lanes[Number(r.dataset.lane)];
        l.notes.splice(Number(r.dataset.note), 1);
        l.notation = '';
        changed();
    }
};
async function importMidiFile(file) {
    if (pending || recording) throw Error('Finish the current request before importing MIDI.');
    if (!file || !/\.midi?$/i.test(file.name || '')) throw Error('Drop a .mid or .midi file.');
    const parsed = MidiImport.parse(await file.arrayBuffer());
    const target = lane(), imported = MidiImport.forLane(parsed, target.drums);
    if (!imported.notes.length) throw Error('No playable note events were found in that MIDI file.');
    checkpoint(); send('stop'); transport='stopped'; anchor=0;
    const bpm=Math.round(parsed.tempo);
    if (Number.isFinite(bpm) && bpm>=30 && bpm<=240) song.tempo=bpm;
    if (/^(?:[1-9]|1[0-2])\/(?:2|4|8|16)$/.test(parsed.meter)) song.meter=parsed.meter;
    const [num,den]=song.meter.split('/').map(Number), bpb=num*4/den;
    const importedBars=Math.max(1,Math.ceil(parsed.lengthBeats/bpb));
    song.bars=Math.min(64,importedBars);
    Object.assign(target, {
        notes: imported.notes,
        notation: '',
        clipLengthBeats: parsed.lengthBeats,
        importedMidiName: file.name,
        originalBrief: target.originalBrief || ('Imported MIDI clip: ' + file.name)
    });
    if (target.vocals) { target.renderedVocalPath=''; target.renderedVocalSignature=''; }
    LaneNotation.sync(target,song.tempo,song.meter); LaneNotation.accept(target);
    song.started=true;
    commit();
    const details=[`${imported.notes.length} notes`,`${parsed.lengthBeats.toFixed(2)} beats`,`${importedBars} bars`];
    if(imported.otherPlayableChannels) details.push(`used channel ${imported.channel+1} (${imported.otherPlayableChannels} other MIDI channel${imported.otherPlayableChannels===1?'':'s'} not imported)`);
    if(parsed.tempoChanges>1) details.push('initial tempo used');
    if(parsed.meterChanges>1) details.push('initial meter used');
    if(importedBars>64) details.push('generation form capped at 64 bars');
    status('Imported ' + file.name + ' into ' + target.name + ' · ' + details.join(' · '));
}
function midiDragActive(on) { document.body.classList.toggle('midiDropActive', !!on); }
window.addEventListener('dragenter', e => { if ([...(e.dataTransfer?.items||[])].some(x => x.kind==='file')) { e.preventDefault(); midiDragActive(true); } });
window.addEventListener('dragover', e => { if ([...(e.dataTransfer?.items||[])].some(x => x.kind==='file')) { e.preventDefault(); if(e.dataTransfer)e.dataTransfer.dropEffect='copy'; midiDragActive(true); } });
window.addEventListener('dragleave', e => { if (!e.relatedTarget) midiDragActive(false); });
window.addEventListener('drop', async e => {
    const file=[...(e.dataTransfer?.files||[])].find(f => /\.midi?$/i.test(f.name));
    if(!file) { midiDragActive(false); return; }
    e.preventDefault(); midiDragActive(false);
    try { await importMidiFile(file); } catch(err) { status(err.message || String(err)); }
});

function receive(j) {
    const p = j.payload ?? {}, matching = pending && j.requestId === pending.id;
    switch (j.op) {
    case 'instruments':
        presets = p;
        render();
        break;
    case 'licenseStatus':
        $('licenseStatus').textContent = p.message || ('Device released. Transfer available: ' + new Date(p.transferAvailableAt*1000).toLocaleString());
        $('licenseKey').value='';
        break;
    case 'settings':
        $('apiUrl').value = p.apiUrl;
        break;
    case 'project':
        if (pending) break; // A late restore must not replace an in-flight generation.
        song = normalizeLaneAiDefaults(p);
        render();
        break;
    case 'connected':
    case 'ready':
        if (p.logging) {
            $('runtimeDiagnostics').textContent = p.logging;
            status(p.logging);
        }
        $('connection').textContent = 'Host connected';
        $('connection').className = 'badge online';
        if (j.op === 'ready') {
            // Restore the user's song first. Voice-library discovery can scan preview
            // folders, so defer it until the workspace is visible instead of competing
            // with startup restoration.
            send('workspaceList',{},crypto.randomUUID());
        }
        break;
    case 'voiceLibrary':
    case 'voiceSelected':
        setVoiceLibrary(p);
        break;
    case 'workspaceList':
        setWorkspaceSongs(p.songs);
        if (!workspaceBootstrapped) {
            if (p.lastSongId) {
                workspaceLoadRequest=crypto.randomUUID();
                send('workspaceLoad',{id:p.lastSongId},workspaceLoadRequest);
            } else {
                workspaceBootstrapped=true;
                updateSongIdentity();
                renderSongList();
                send('voiceLibraryList',{},crypto.randomUUID());
            }
        }
        break;
    case 'workspaceLoaded':
        if (j.requestId !== workspaceLoadRequest && workspaceLoadRequest) break;
        workspaceLoadRequest='';
        song=normalizeLaneAiDefaults(p.project || fresh());
        history=Array.isArray(p.history)?p.history:[];
        workspaceId=p.id || newWorkspaceId();
        workspaceTitle=normalizedWorkspaceTitle(p.title);
        workspaceProducerDesign=p.producerDesign || '';
        workspaceHistoryVersion=workspaceHistorySavedVersion=0; workspaceProducerVersion=workspaceProducerSavedVersion=0; workspaceSaveRequests.clear();
        workspaceBootstrapped=true; undo=[]; redo=[]; songRun=null; pending=null;
        selected=song.lanes[0]?.id || selected;
        $('brief').value=history.at(-1)?.brief || '';
        setWorkspaceSongs(p.songs);
        send('project',song); render(); renderHistory(); renderSongList(); busy();
        send('voiceLibraryList',{},crypto.randomUUID());
        status('Loaded ' + workspaceTitle + '.');
        break;
    case 'workspaceSaved': { 
        const ack=workspaceSaveRequests.get(j.requestId); workspaceSaveRequests.delete(j.requestId);
        if (ack?.historyVersion != null) workspaceHistorySavedVersion=Math.max(workspaceHistorySavedVersion,ack.historyVersion);
        if (ack?.producerVersion != null) workspaceProducerSavedVersion=Math.max(workspaceProducerSavedVersion,ack.producerVersion);
        if (p.id === workspaceId) workspaceTitle=normalizedWorkspaceTitle(p.title || workspaceTitle);
        setWorkspaceSongs(p.songs); updateSongIdentity();
        break;
    }
    case 'workspaceDeleted':
        setWorkspaceSongs(p.songs);
        if (p.lastSongId) loadWorkspaceSong(p.lastSongId); else createNewWorkspaceSong(false);
        break;
    case 'disconnected':
        $('connection').textContent = 'Disconnected';
        $('connection').className = 'badge';
        restorePendingWork();
        pending = null;
        if (typeof songRun !== 'undefined') songRun = null;
        busy();
        break;
    case 'status':
        if (matching) {
            status(p.message);
            if (pending && ['voiceDesignPreview','voiceImportPreview','voiceSavePreview'].includes(pending.kind) && $('voiceDesigner')?.open)
                $('voicePreviewStatus').textContent = p.message || 'Working…';
        }
        break;
    case 'asrReady':
        if (recording) status('Listening… Transcription engine ready. Send voice when finished.');
        break;
    case 'asrWarmError':
        if (recording) status('Listening… transcription engine could not warm: ' + (p.message || 'unknown error'));
        break;
    case 'recording':
        recording = p;
        busy();
        if (p)
            status('Listening… Send voice to create music, or Cancel.');
        break;
    case 'transcript':
        if (matching) {
            const text = p.text;
            selected = pending.laneId;
            pending = null;
            $('brief').value = text;
            if (songModeEnabled()) startSongGeneration(text); else requestMusic(text);
        }
        break;
    case 'songDesign':
        if (matching && pending?.kind === 'songDesign') {
            if (!p.state?.sections?.length) {
                restoreSongGeneration(); pending = null; busy(); status('Producer did not return a usable section list.'); break;
            }
            songRun.state = p.state; songRun.design = p.design || '';
            if (typeof workspaceProducerDesign !== 'undefined' && p.design && p.design !== workspaceProducerDesign) { workspaceProducerDesign=p.design; workspaceProducerVersion++; }
            if (typeof workspaceTitle !== 'undefined') {
                workspaceTitle=songDesignTitle(p,songRun.brief);
                updateSongIdentity(); renderSongList();
            }
            if (typeof scheduleWorkspaceSave === 'function') scheduleWorkspaceSave();
            songRun.sectionIndex = 0; songRun.laneIndex = 0;
            pending = null;
            nextSongChunk();
        }
        break;
    case 'songChunk':
        if (matching && pending?.kind === 'songChunk') {
            try { applySongChunk(p); }
            catch (e) { skipFailedSongChunk(e.message); }
        }
        break;
    case 'composition':
        if (matching) {
            const {brief, laneId, previous, includedIds} = pending;
            const tracks = p.tracks;
            const ids = new Set();
            if (!Array.isArray(tracks) || tracks.length === 0 || tracks.some(t =>
                !includedIds.includes(t.laneId) || !song.lanes.some(l => l.id === t.laneId) || ids.has(t.laneId) ||
                !ids.add(t.laneId) || !Array.isArray(t.notes) || !t.notes.length ||
                t.notes.some(n => !Number.isFinite(n.start) || n.start < 0 ||
                    !Number.isFinite(n.duration) || n.duration <= 0 ||
                    !Number.isInteger(n.pitch) || n.pitch < 0 || n.pitch > 127))) {
                restorePendingComposition();
                pending = null;
                busy();
                status('Invalid arrangement data. Previous music restored; nothing replayed.');
                break;
            }
            undo.push(previous);
            if (undo.length > 40) undo.shift();
            redo = [];
            for (const t of tracks) {
                const l = song.lanes.find(l => l.id === t.laneId);
                const before = previous.lanes.find(x => x.id === t.laneId);
                const generatedLength = Math.max(0, ...t.notes.map(n => n.start + n.duration));
                Object.assign(l, {notation : t.notation, originalBrief : t.originalBrief || before.originalBrief || brief,
                    prompts : [...(before.prompts || []), brief], notes : clone(t.notes), clipLengthBeats : generatedLength});
                if (l.vocals) { l.renderedVocalPath=''; l.renderedVocalSignature=''; }
            }
            for (const t of tracks) LaneNotation.accept(song.lanes.find(l => l.id === t.laneId));
            song.started = true;
            selected = laneId;
            history.push({brief, song : clone(song), laneId : selected, createdUtc:new Date().toISOString()});
            if (history.length > 40)
                history.shift();
            if (typeof workspaceHistoryVersion !== 'undefined') workspaceHistoryVersion++;
            pending = null;
            commit();
            renderHistory();
            status('Ready. Updated ' + lane().name + '.' +
                (p.warnings?.length ? ' ' + p.warnings.join(' ') : ''));
            send('play', song);
        }
        break;
    case 'vocalsRendered':
        if (matching && pending?.kind === 'renderVocals') {
            const target = song.lanes.find(l => l.id === p.laneId);
            if (!target) { pending=null; busy(); status('Vocal lane no longer exists.'); break; }
            target.renderedVocalPath = p.wavPath || '';
            target.voiceId = p.voiceId || target.voiceId || '';
            target.renderedVocalSignature = vocalSignature(target);
            if (p.voiceLibrary) setVoiceLibrary(p.voiceLibrary);
            pending = null;
            commit();
            status('Ready. Rendered ' + target.name + (p.qwenModelType ? ' with local Qwen TTS (' + p.qwenModelType + ').' : '.'));
            send('play', song);
        }
        break;
    case 'voicePreview':
        if (matching && (pending?.kind === 'voiceDesignPreview' || pending?.kind === 'voiceImportPreview')) {
            voicePreview = p;
            pending=null; busy();
            $('voiceTranscript').value=p.transcript || '';
            $('voiceOk').disabled=!p.previewId;
            $('voicePlay').disabled=!p.wav;
            $('voicePreviewStatus').textContent=(p.source==='imported'?'Imported and transcribed — review the reference text below.':'Voice generated and transcribed — review the reference text below.') + (p.baseOctave>=0 ? ` Base ${midiVoiceNoteName(p.baseMidiNote)} (octave ${p.baseOctave}); singing range ${midiVoiceNoteName(p.singingMinMidiNote)}–${midiVoiceNoteName(p.singingMaxMidiNote)}.` : '') + (p.singingWav?' Singing preview ready.':'');
            playCurrentVoicePreview();
        }
        break;
    case 'voiceSaved':
        if (matching && pending?.kind === 'voiceSavePreview') {
            const savedId=p.savedVoiceId || '';
            pending=null; setVoiceLibrary(p);
            if (lane()?.vocals && savedId) lane().voiceId=savedId;
            voicePreview=null; closeVoiceDesigner(false); commit(); busy();
            status('Voice saved and selected for ' + (lane()?.name || 'Vocals') + '.');
        }
        break;
    case 'voiceDesignerReady':
        if ($('voiceDesigner')?.open && !voicePreview && !pending) $('voicePreviewStatus').textContent='VoiceDesign ready.';
        break;
    case 'voiceServiceReady':
    case 'voicePreviewDiscarded':
    case 'voiceDesignerClosed':
        break;
    case 'midiSaved':
        pending = null;
        busy();
        status('MIDI export complete.');
        break;
    case 'midiDragFinished':
        status('Ready.');
        break;
    case 'transport':
        transport = p.state;
        anchor = p.seconds || 0;
        anchorTime = performance.now();
        $('play').textContent = transport === 'playing' ? 'Ⅱ' : '▶';
        if (!pending && transport === 'buffering')
            status('Buffering audio…');
        else if (!pending && transport === 'playing')
            status('Playing');
        break;
    case 'cancelled':
        if (matching) {
            if (pending?.kind === 'songChunk' && skipFailedSongChunk(p.message || 'Song chunk cancelled or timed out.'))
                break;
            restorePendingWork();
            pending = null;
            if (typeof songRun !== 'undefined') songRun = null;
            busy();
            status(p.message);
        }
        break;
    case 'error': {
        // Background library/workspace commands use their own request ids and do not
        // own the current AI generation. Surface those errors without rolling back or
        // cancelling unrelated pending work.
        const message = p.message || j.message || 'An error occurred.';
        if (typeof workspaceSaveRequests !== 'undefined' && workspaceSaveRequests.has(j.requestId)) workspaceSaveRequests.delete(j.requestId);
        if (typeof workspaceLoadRequest !== 'undefined' && workspaceLoadRequest && j.requestId === workspaceLoadRequest) {
            // A stale/corrupt last-song pointer must not leave workspaceBootstrapped=false,
            // because that would silently disable every later autosave in this session.
            workspaceLoadRequest='';
            workspaceBootstrapped=true;
            updateSongIdentity();
            renderSongList();
            send('voiceLibraryList',{},crypto.randomUUID());
            status(message + ' Started a new local workspace instead.');
            break;
        }
        if (matching) {
            const failedKind = pending?.kind || '';
            if (pending?.kind === 'songChunk' && skipFailedSongChunk(message)) break;
            restorePendingWork();
            pending = null;
            if (typeof songRun !== 'undefined') songRun = null;
            busy();
            status(message);
            if (['voiceDesignPreview','voiceImportPreview','voiceSavePreview'].includes(failedKind) && $('voiceDesigner')?.open) {
                $('voicePreviewStatus').textContent = 'Error: ' + message;
                $('voicePlay').disabled = !voicePreview?.wav;
                $('voiceOk').disabled = !voicePreview?.previewId;
            }
        } else if (!j.requestId || !pending) {
            status(message);
            if (voicePreview === null && $('voiceDesigner')?.open) $('voicePreviewStatus').textContent = message;
        }
        break;
    }
    }
}
window.SAMFD = (tag, size, data) => {
    try {
        receive(JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(data), c => c.charCodeAt(0)))));
    } catch (e) {
        status(e.message);
    }
};
window.SPVFD = () => {};
window.SCVFD = () => {};
window.SCMFD = () => {};
window.SMMFD = () => {};
window.SSMFD = () => {};
function animate() {
    const head = $('playhead');
    head.style.display = [ 'playing', 'paused', 'buffering' ].includes(transport) ? 'block' : 'none';
    const seconds = anchor + (transport === 'playing' ? (performance.now() - anchorTime) / 1000 : 0);
    head.style.left = (seconds * song.tempo / 60 * zoom) + 'px';
    requestAnimationFrame(animate);
}
$('vocalLyrics').onchange = () => {
    if (!lane().vocals) return;
    checkpoint(); lane().lyrics = $('vocalLyrics').value; changed();
};
$('vocalVoice').onchange = () => {
    if (!lane().vocals) return;
    const id=$('vocalVoice').value.trim();
    if (!id) { openVoiceDesigner(); return; }
    checkpoint(); lane().voiceId=id; changed();
    send('voiceSelect',{id},crypto.randomUUID());
    // Warm the saved/reference-voice Qwen server while the user continues editing.
    send('voiceServiceActivity',{service:'custom-voice'},crypto.randomUUID());
};
$('renderVocals').onclick = () => {
    try {
        if (pending) return;
        readFields();
        const target = lane();
        if (!target.vocals) throw Error('Select the Vocals lane.');
        if (!target.notes?.length) throw Error('Create or import a vocal melody first.');
        const text = $('vocalLyrics').value.trim(), voiceId=$('vocalVoice').value.trim();
        if (!text) throw Error('Enter lyrics for the vocal lane.');
        if (!voiceId || !savedVoices.some(v=>v.id===voiceId)) throw Error('Create or select a saved voice first.');
        target.lyrics = text; target.voiceId = voiceId;
        LaneNotation.sync(target, song.tempo, song.meter);
        const id=crypto.randomUUID();
        pending={id,kind:'renderVocals',laneId:target.id};
        status('Vocals · Starting local voice render…'); busy();
        send('voiceServiceActivity',{service:'custom-voice'},crypto.randomUUID());
        send('renderVocals',{project:clone(song),laneId:target.id,text:target.lyrics,voiceId:target.voiceId},id);
    } catch(e) { status(e.message); }
};

function midiVoiceNoteName(note) {
    if (!Number.isFinite(note) || note < 0 || note > 127) return '?';
    const names=['C','C#','D','D#','E','F','F#','G','G#','A','A#','B'];
    return names[note%12] + (Math.floor(note/12)-1);
}

function bytesToBase64(bytes) {
    let out=''; const step=0x8000;
    for(let i=0;i<bytes.length;i+=step) out+=String.fromCharCode(...bytes.subarray(i,Math.min(bytes.length,i+step)));
    return btoa(out);
}
function openVoiceDesigner() {
    if (pending) return;
    voicePreview=null;
    if (voiceAudio) { try{voiceAudio.pause();}catch{} voiceAudio=null; }
    $('voiceDesignPrompt').value=''; $('voiceSampleText').value=DEFAULT_VOICE_SAMPLE_TEXT; $('voiceTranscript').value=''; $('voiceName').value='';
    $('voiceSingingPreview').checked=false; $('voicePreviewStatus').textContent='Loading VoiceDesign…'; $('voicePlay').disabled=true; $('voiceOk').disabled=true;
    if (previewMelodies.length) setVoiceLibrary({voices:savedVoices,previewMelodies,lastSelectedVoiceId:lane()?.voiceId||''});
    $('voiceDesigner').showModal();
    // Opening the window is VoiceDesign activity: start/load qwen-server immediately
    // and keep it resident until the dialog closes.
    send('voiceDesignOpen',{},crypto.randomUUID());
}
function closeVoiceDesigner(discard=true) {
    if (voiceAudio) { try{voiceAudio.pause();}catch{} voiceAudio=null; }
    if (discard && voicePreview?.previewId) send('voiceDiscardPreview',{previewId:voicePreview.previewId},crypto.randomUUID());
    voicePreview=null;
    if ($('voiceDesigner').open) $('voiceDesigner').close();
    send('voiceDesignClose',{},crypto.randomUUID());
}
function playCurrentVoicePreview() {
    if (!voicePreview) return;
    const b64=$('voiceSingingPreview').checked && voicePreview.singingWav ? voicePreview.singingWav : voicePreview.wav;
    if (!b64) return;
    if (voiceAudio) { try{voiceAudio.pause();}catch{} }
    voiceAudio=new Audio('data:audio/wav;base64,'+b64);
    voiceAudio.play().catch(()=>{});
}
function discardCurrentPreview() {
    if (voicePreview?.previewId) send('voiceDiscardPreview',{previewId:voicePreview.previewId},crypto.randomUUID());
    voicePreview=null; $('voiceTranscript').value=''; $('voicePlay').disabled=true; $('voiceOk').disabled=true;
}
function requestVoiceDesignPreview() {
    try {
        if (pending) return;
        const description=$('voiceDesignPrompt').value.trim(), sampleText=$('voiceSampleText').value.trim();
        if (!description) throw Error('Describe how you want the voice to sound.');
        discardCurrentPreview();
        const id=crypto.randomUUID(); pending={id,kind:'voiceDesignPreview'}; busy(); $('voicePreviewStatus').textContent='Generating voice…';
        send('voiceDesignPreview',{description,sampleText,renderSinging:$('voiceSingingPreview').checked,melodyId:$('voicePreviewMelody').value||'fun'},id);
    } catch(e) { status(e.message); $('voicePreviewStatus').textContent=e.message; }
}
async function importVoiceWav(file) {
    try {
        if (pending) return;
        if (!file || !/\.wav$/i.test(file.name)) throw Error('Choose a WAV file.');
        if (file.size>10*1024*1024) throw Error('Voice WAV must be 10 MiB or smaller.');
        discardCurrentPreview();
        const bytes=new Uint8Array(await file.arrayBuffer()), id=crypto.randomUUID();
        pending={id,kind:'voiceImportPreview'}; busy(); $('voicePreviewStatus').textContent='Importing and transcribing WAV…';
        send('voiceImportPreview',{wav:bytesToBase64(bytes),renderSinging:$('voiceSingingPreview').checked,melodyId:$('voicePreviewMelody').value||'fun'},id);
    } catch(e) { status(e.message); $('voicePreviewStatus').textContent=e.message; }
}
$('newVoice').onclick=openVoiceDesigner;
$('voiceGenerate').onclick=requestVoiceDesignPreview;
$('voicePlay').onclick=playCurrentVoicePreview;
$('voiceBrowse').onclick=()=>$('voiceWavFile').click();
$('voiceWavFile').onchange=()=>{ const f=$('voiceWavFile').files?.[0]; if(f) importVoiceWav(f); $('voiceWavFile').value=''; };
$('voiceDrop').ondragenter=$('voiceDrop').ondragover=e=>{e.preventDefault();e.stopPropagation();$('voiceDrop').classList.add('dragging'); if(e.dataTransfer)e.dataTransfer.dropEffect='copy';};
$('voiceDrop').ondragleave=e=>{e.preventDefault();e.stopPropagation();$('voiceDrop').classList.remove('dragging');};
$('voiceDrop').ondrop=e=>{e.preventDefault();e.stopPropagation();$('voiceDrop').classList.remove('dragging'); const f=[...(e.dataTransfer?.files||[])].find(x=>/\.wav$/i.test(x.name)); if(f) importVoiceWav(f);};
$('voiceCancel').onclick=()=>{
    if (pending && (pending.kind==='voiceDesignPreview'||pending.kind==='voiceImportPreview'||pending.kind==='voiceSavePreview')) { send('cancel',{},pending.id); pending=null; busy(); }
    closeVoiceDesigner(true);
};
$('voiceOk').onclick=()=>{
    try {
        if (pending) return;
        if (!voicePreview?.previewId) throw Error('Generate or import a voice first.');
        const name=$('voiceName').value.trim(); if(!name) throw Error('Name the voice before saving it.');
        const id=crypto.randomUUID(); pending={id,kind:'voiceSavePreview'}; busy(); $('voicePreviewStatus').textContent='Saving reusable voice reference…';
        const transcript=$('voiceTranscript').value.trim(); if(!transcript) throw Error('Enter the reference transcription before saving.');
        send('voiceSavePreview',{previewId:voicePreview.previewId,name,transcript},id);
    } catch(e) { status(e.message); $('voicePreviewStatus').textContent=e.message; }
};
$('voiceSingingPreview').onchange=()=>{ if (voicePreview) $('voicePreviewStatus').textContent=$('voiceSingingPreview').checked && !voicePreview.singingWav?'Run Generate/Import again to create the singing preview.':'Preview ready.'; };

$('libraryToggle').onclick=toggleLibraryView;
$('songTitle').onclick=()=>{ if(pending)return; $('songTitle').contentEditable='true'; $('songTitle').focus(); const r=document.createRange();r.selectNodeContents($('songTitle'));const sel=getSelection();sel.removeAllRanges();sel.addRange(r); };
$('songTitle').onkeydown=e=>{ if(e.key==='Enter'){e.preventDefault();$('songTitle').blur();} if(e.key==='Escape'){e.preventDefault();$('songTitle').textContent=workspaceTitle;$('songTitle').blur();} };
$('songTitle').onblur=()=>{ if($('songTitle').contentEditable==='true'){ workspaceTitle=normalizedWorkspaceTitle($('songTitle').textContent); $('songTitle').contentEditable='false'; updateSongIdentity(); renderSongList(); saveWorkspace(true); } };
$('deleteSong').onclick=()=>{
    if(pending)return;
    if(!confirm(`Delete "${workspaceTitle}" from your Resone song library?`)) return;
    send('workspaceDelete',{id:workspaceId},crypto.randomUUID());
};

try { $('songMode').checked = localStorage.getItem('resone.songMode') === '1'; } catch { $('songMode').checked = false; }
$('songMode').onchange = () => { saveSongMode(); render(); };
render();
renderHistory();
requestAnimationFrame(animate);
send('ready');

$('activateLicense').onclick = () => {
    const key=$('licenseKey').value.trim();
    if(key) { $('licenseStatus').textContent='Activating…'; send('licenseActivate',{key},crypto.randomUUID()); }
};
$('releaseLicense').onclick = () => {
    $('licenseStatus').textContent='Releasing device…'; send('licenseRelease',{},crypto.randomUUID());
};
