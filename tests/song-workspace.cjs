const fs=require('fs');
function read(p){return fs.readFileSync(p,'utf8');}
function must(v,msg){if(!v)throw new Error(msg);}
const html=read('src/wds.resone.ui/resources/web/index.html');
const js=read('src/wds.resone.ui/resources/web/app.js');
const producer=read('src/wds.resone.api/SongCompositionDesigner.cs');
const store=read('src/wds.resone.api/SongWorkspaceStore.cs');
const worker=read('src/wds.resone.launcher/WorkerHost.cs');
for(const id of ['libraryToggle','leftTrack','songList','songIdentity','songTitle','songNotesToggle','songNotesPanel','producerNotesText','composerNotesText','deleteSong','saveNew'])
  must(html.includes(`id="${id}"`)||html.includes(`class="${id}"`),`missing song workspace UI ${id}`);
for(const op of ['workspaceList','workspaceLoad','workspaceSave','workspaceDelete'])
  must(js.includes(`'${op}'`)||js.includes(`"${op}"`),`missing browser workspace operation ${op}`);
for(const op of ['workspaceList','workspaceLoad','workspaceSave','workspaceDelete'])
  must(worker.includes(`"${op}"`),`missing worker workspace operation ${op}`);
must(worker.includes('localLibraryOp') && worker.includes('local-request-failed'),'local workspace errors should not close the WebSocket');
must(producer.includes('Song title: ...'),'producer title contract missing');
must(producer.includes('ExtractTitle'),'producer title extraction missing');
must(js.includes('p.title') && js.includes('workspaceTitle'),'producer title is not applied to workspace');
must(js.includes("contentEditable='true'") && js.includes('saveWorkspace(true)'),'click-to-edit title persistence missing');
must(js.includes('confirm(`Delete'),'delete confirmation missing');
must(store.includes('index.json') && store.includes('project.json') && store.includes('history.json') && store.includes('meta.json'),'song JSON storage layout missing');
must(store.includes('TakeLast(40)'),'song history retention bound missing');
must(js.includes("workspaceView === 'history' ? 'songs' : 'history'"),'History/Songs sliding view missing');

must(js.includes("const newWorkspaceId = () => crypto.randomUUID().replaceAll('-', '')"),'browser workspace ids must use compact GUID format');
must(store.includes('Guid.TryParseExact(id, "N"') && store.includes('Guid.TryParseExact(id, "D"'),'workspace store must accept both compact and browser UUID formats');
must(js.includes("typeof workspaceLoadRequest !== 'undefined' && workspaceLoadRequest && j.requestId === workspaceLoadRequest") && js.includes('workspaceBootstrapped=true'),'failed last-workspace load must not disable later autosaves');
console.log('PASS JSON song workspaces, history navigation, producer titles, rename/delete, and Save & New');

must(js.includes('function renderSongNotes()') && js.includes('workspaceProducerDesign') && js.includes('workspaceComposerOverview'),'piece notes drawer must render saved producer and composer context');
must(js.includes("$('songNotesToggle').onclick") && js.includes('aria-expanded'),'piece notes document toggle wiring missing');
