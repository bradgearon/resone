const fs=require('fs');
function assert(v,m){if(!v)throw new Error(m)}
const win=fs.readFileSync('native/include/WindowsSupport.hpp','utf8');
const api=fs.readFileSync('native/include/ApiClient.hpp','utf8');
const boot=fs.readFileSync('src/wds.resone.api/LauncherBootstrap.cs','utf8');
const roots=fs.readFileSync('src/wds.resone.api/ResoneRoot.cs','utf8');
const llama=fs.readFileSync('src/wds.resone.api/LlamaEngineResolver.cs','utf8');
const build=fs.readFileSync('scripts/build-windows.ps1','utf8');
const runtime=[win,api,boot,roots,llama,
 fs.readFileSync('src/wds.resone.api/NativeChatClient.cs','utf8'),
 fs.readFileSync('config/appsettings.json','utf8')].join('\n');
assert(!runtime.includes('resone_inference.dll'), 'legacy resone_inference.dll reference remains in runtime/config source');
assert(win.includes('build" / L"api" / L"wds.resone.api.dll'), 'build-tree API lookup missing');
assert(win.includes('findResoneRoot'), 'native build-tree root discovery missing');
assert(api.includes('runtimeRoot.string()'), 'ApiClient does not pass actual runtime root to API DLL');
assert(boot.includes('ResoneRoot.LauncherExecutable(root)'), 'launcher dev-tree resolver missing');
assert(boot.includes('start.Environment["RESONE_HOME"]=root'), 'launcher is not given resolved runtime root');
assert(roots.includes('build", "launcher"'), 'managed dev-tree launcher lookup missing');
assert(llama.includes('build", "ui", "out", "Release"'), 'dev-tree llama bridge lookup missing');
assert(build.includes('Remove-Item "$InstallDir/resone_inference.dll"'), 'installer does not clean stale inference shim');
console.log('PASS: build-folder runtime resolves local API/launcher/config/llama bridge and rejects legacy resone_inference.dll');
