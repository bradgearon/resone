const fs=require('node:fs');
function read(p){return fs.readFileSync(p,'utf8');}
function must(v,msg){if(!v)throw new Error(msg);}
const store=read('src/wds.resone.api/SongWorkspaceStore.cs');
const worker=read('src/wds.resone.launcher/WorkerHost.cs');
const ui=read('src/wds.resone.ui/Resone.cpp');
const js=read('src/wds.resone.ui/resources/web/app.js');
must(store.includes('Task<JsonObject> ListPayloadAsync')&&store.includes('Task<JsonObject> LoadPayloadAsync')&&store.includes('Task<JsonObject> SaveAsync')&&store.includes('Task<JsonObject> DeleteAsync'),'workspace APIs must be async');
must(store.includes('FileOptions.Asynchronous')&&store.includes('JsonSerializer.DeserializeAsync')&&store.includes('JsonSerializer.SerializeAsync'),'workspace disk I/O must use async streams');
must(!store.includes('File.ReadAllText(')&&!store.includes('File.WriteAllText('),'workspace store must not regress to blocking text I/O');
must(worker.includes('Channel.CreateUnbounded<Envelope>')&&worker.includes('localRequests.Writer.WriteAsync'),'local library operations must be detached from the websocket receive pump');
must(worker.includes('workspaceStore.SaveAsync')&&worker.includes('workspaceStore.LoadPayloadAsync'),'worker must await async workspace persistence');
must(!ui.includes('preferences() / "song.json"'),'standalone must not duplicate workspace persistence through synchronous song.json I/O');
must(js.includes('project:song')&&!js.includes('project:clone(song),history:workspaceHistoryPayload()'),'autosave should avoid redundant whole-project/history cloning');
must(js.includes('workspaceHistoryVersion !== workspaceHistorySavedVersion'),'history snapshots should only be sent when changed');
const ready=js.slice(js.indexOf("case 'connected':"),js.indexOf("case 'voiceLibrary':"));
must(ready.indexOf("send('workspaceList'")>=0&&!ready.includes("send('voiceLibraryList'"),'startup should request workspace before voice-library discovery');
must(js.includes("send('voiceLibraryList',{},crypto.randomUUID());"),'voice library should still load after workspace restoration');
console.log('PASS async workspace persistence, nonblocking request pump, sparse autosave, and song-first startup');
