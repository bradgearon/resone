const fs = require('fs');
const assert = require('assert');

const app = JSON.parse(fs.readFileSync('config/appsettings.json','utf8'));
assert(app.nativeInference === true || app.useLocalInference === true, 'source appsettings should opt into local inference');
assert(app.contextTokens === 16384, 'local llama context should match the existing 16K stack setting');
assert(app.gpuLayers === 99, 'local llama should request full GPU offload first');
assert(app.flashAttention === true, 'flash attention should be enabled');
assert(app.reasoningEnabled === false, 'reasoning must remain disabled');
assert(app.warmModelOnStackStart === true, 'model should warm when the AI stack starts');
assert(app.llamaEngineDirectories['win-x64'] === 'engines/llm/llama-cpp-dynamic-win-x64', 'win-x64 engine mapping missing');

const contracts = fs.readFileSync('src/wds.resone.api/Contracts.cs','utf8');
assert(contracts.includes('public bool UseLocalInference'), 'compatibility alias missing');
assert(contracts.includes('LocalInferenceEnabled => NativeInference || UseLocalInference'), 'normalized switch missing');
assert(contracts.includes('Dictionary<string, string> LlamaEngineDirectories'), 'platform engine directory map missing');
assert(!contracts.includes('LlamaBridgePath'), 'internal bridge should not be user-configurable');

const native = fs.readFileSync('src/wds.resone.api/NativeChatClient.cs','utf8');
assert(native.includes('resone_llama_bridge_abi'), 'native client is not using the internal llama ABI bridge');
assert(native.includes('resone_llama_open'), 'native client does not open llama in process');
assert(native.includes('resone_llama_generate'), 'native client does not generate through llama in process');
assert(native.includes('WarmupAsync'), 'native client model warmup missing');
assert(native.includes('["enable_thinking"] = false'), 'thinking-disabled request diagnostics missing');
assert(native.includes('VisibleOutputFilter'), 'reasoning-channel output filter missing');
assert(native.includes('"<|channel>thought"'), 'Gemma thought-channel suppression marker missing');
assert(native.includes('"<channel|>"'), 'Gemma thought-channel closing marker missing');

const bridge = fs.readFileSync('native/inference/resone_llama_bridge.cpp','utf8');
assert(bridge.includes('ggml_backend_load_all_from_path'), 'bridge does not load backend plugins from selected engine directory');
assert(bridge.includes('LLAMA_FLASH_ATTN_TYPE_ENABLED'), 'bridge does not enable flash attention');
assert(bridge.includes('llama_chat_apply_template'), 'bridge does not use the model chat template');
assert(bridge.includes('silent_log'), 'llama logging suppression missing');
assert(bridge.includes('#ifndef NOMINMAX'), 'Windows NOMINMAX definition should be guarded');
assert(!bridge.includes('char small['), 'bridge must not use Windows-reserved small token as a buffer identifier');
assert(bridge.includes('char piece_buffer[256]'), 'token decode buffer regression fix missing');
const abi = fs.readFileSync('native/inference/llama_dynamic_abi.hpp','utf8');
assert(abi.includes('ABI snapshot: llama.cpp commit'), 'vendored pinned llama C ABI declarations missing');

const forge = fs.readFileSync('src/wds.resone.api/ForgeEngine.cs','utf8');
const arranger = fs.readFileSync('src/wds.resone.api/ArrangementComposer.cs','utf8');
assert(forge.includes('settings.LocalInferenceEnabled ? new NativeChatClient'), 'Forge does not use normalized local inference switch');
assert(arranger.includes('settings.LocalInferenceEnabled ? new NativeChatClient'), 'Arranger does not use normalized local inference switch');

const worker = fs.readFileSync('src/wds.resone.launcher/WorkerHost.cs','utf8');
assert(worker.includes('LLM transport: llama.cpp in-process'), 'launcher llama transport diagnostic missing');
assert(worker.includes('llama={llamaLibrary}'), 'launcher diagnostic should identify the actual platform llama library');
assert(worker.includes('NativeChatClient.WarmupAsync'), 'worker does not warm the local model before readiness');

const build = fs.readFileSync('scripts/build-windows.ps1','utf8');
assert(!build.includes('ggml-org/llama.cpp.git'), 'Windows build must not clone llama.cpp');
assert(!build.includes('LLAMA_CPP_DIR'), 'Windows build must not require a llama.cpp source tree');
assert(build.includes('resone_llama_bridge'), 'build does not compile/stage the llama bridge');
assert(build.includes("'llamaEngineDirectories','nativeModelPath','contextTokens','gpuLayers','allowCpuFallback','flashAttention','reasoningEnabled','warmModelOnStackStart','temperature','topK','topP'"), 'installer does not synchronize llama inference parameters');
assert(build.includes("'manifestVersion','provisionAiRuntime','useExistingStack','requireHashes','logging','updates','dependencyPins','enginePacks','models','services'"), 'installer does not synchronize runtime/update provisioning fields');

const releaseRuntime = JSON.parse(fs.readFileSync('config/runtime.release.example.json','utf8'));
const packMatrix = releaseRuntime.enginePacks[0];
assert(packMatrix && packMatrix.cpu && packMatrix.cuda && packMatrix.vulkan, 'release runtime must define CPU/CUDA/Vulkan engine groups');
assert(packMatrix.cuda.llm.url.includes('bin-win-cuda-13.3-x64.zip'), 'CUDA llama pack missing');
assert(/^[A-F0-9]{64}$/.test(packMatrix.cuda.llm.sha256), 'CUDA llama hash missing');
assert(packMatrix.cpu.asr.url.endsWith('/whisper-bin-x64.zip'), 'CPU Whisper must use the CPU archive');
assert(packMatrix.cuda.asr.url.endsWith('/whisper-cublas-12.4.0-bin-x64.zip'), 'CUDA Whisper must use the cuBLAS archive');

const installer = fs.readFileSync('src/wds.resone.launcher/EnginePackInstaller.cs','utf8');
assert(installer.includes('Preparing {pack.Component} engine ({pack.EffectivePlatform})'), 'launcher engine-pack downloader missing');
assert(installer.includes('ValidateRequiredFiles'), 'launcher does not validate extracted engine pack contents');
assert(installer.includes('ExpandManifestPacks'), 'compact engine matrix normalization missing');
assert(installer.includes('AddCompactBackend(result, "cuda"'), 'CUDA engine matrix support missing');
assert(installer.includes('"nvidia" or "cuda" => "cuda"'), 'legacy NVIDIA to CUDA compatibility missing');
assert(installer.includes('"amd" or "intel" or "vulkan" => "vulkan"'), 'AMD/Intel Vulkan compatibility missing');
assert(installer.includes('PlatformEquals(p, "dynamic")'), 'dynamic engine pack selection missing');

console.log('PASS in-process llama.cpp selection, shared AI_ROOT provisioning, warm model, streaming bridge, and platform mapping');
