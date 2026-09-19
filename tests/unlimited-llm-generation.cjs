const fs = require('fs');
const assert = require('assert');

const ai = fs.readFileSync('src/wds.resone.api/AiClient.cs','utf8');
const native = fs.readFileSync('src/wds.resone.api/NativeChatClient.cs','utf8');
const narrative = fs.readFileSync('src/wds.resone.api/MusicNarrativePlanner.cs','utf8');
const designer = fs.readFileSync('src/wds.resone.api/SongCompositionDesigner.cs','utf8');
const arranger = fs.readFileSync('src/wds.resone.api/ArrangementComposer.cs','utf8');
const composition = fs.readFileSync('src/wds.resone.api/MusicComposition.cs','utf8');
const bridge = fs.readFileSync('native/inference/resone_llama_bridge.cpp','utf8');

assert(!ai.includes('maxTokens'), 'chat client interface must not expose an application output-token budget');
assert(!ai.includes('["max_tokens"]'), 'HTTP fallback must not send an application max_tokens cap');
assert(!native.includes('int? maxTokens'), 'native client must not accept a maxTokens cap');
assert(!native.includes('int budget'), 'native client must not synthesize a default output budget');
assert(native.includes('["output_limit"] = "none (EOS/context-window only)"'), 'native diagnostics should state uncapped output');
assert(!narrative.includes('MaxNarrativeTokens'), 'narrative planner token cap must be removed');
assert(!designer.includes('MaxTokens'), 'song designer token cap must be removed');
assert(!arranger.includes('CompleteTextStreamingAsync(messages, 16384'), 'arranger must not reserve the entire 16K context as output');
assert(!composition.includes('CompleteTextStreamingAsync(messages, 8192'), 'legacy composition must not impose an 8K output cap');
assert(!bridge.includes('Prompt plus output budget exceeds'), 'bridge must not reject prompt + theoretical output budget');
assert(!bridge.includes('Output token limit reached before completion'), 'bridge must not stop on an application output-token cap');
assert(bridge.includes('while (true)'), 'native generation should continue until EOS/cancellation');
assert(bridge.includes('llama_memory_seq_rm') && bridge.includes('llama_memory_seq_add'), 'native generation should shift the b9870 rolling context instead of imposing an output cap');
assert(bridge.includes('resone_llama_bridge_abi() { return 3; }'), 'bridge ABI must bump so stale capped binaries are rejected');
console.log('PASS uncapped LLM output uses context window only, with b9870 rolling context shift');
