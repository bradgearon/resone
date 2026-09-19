from pathlib import Path
s = Path("native/inference/resone_llama_bridge.cpp").read_text(encoding="utf-8")
h = Path("native/inference/llama_dynamic_abi.hpp").read_text(encoding="utf-8")
assert 'llama_model_meta_val_str' in h
assert 's.model_architecture == "gemma4" || is_gemma4_template(tmpl)' in s
assert 'prompt += "<|turn>";' in s
assert 'prompt += "<turn|>\\n";' in s
assert 'prompt += "<|turn>model\\n";' in s
assert 'prompt += "<|channel>thought' not in s
assert 'llama.cpp b9870 could not apply the model\'s chat template' in s
print('PASS Gemma 4 b9870 chat-template fallback')

log = Path("src/wds.resone.api/LlmRequestLog.cs").read_text(encoding="utf-8")
native = Path("src/wds.resone.api/NativeChatClient.cs").read_text(encoding="utf-8")
assert "string? endpoint = null" in log
assert "endpoint ?? settings.LlmUrl" in log
assert '"llama.cpp-in-process")' in native
print("PASS native request log endpoint reflects in-process transport")
