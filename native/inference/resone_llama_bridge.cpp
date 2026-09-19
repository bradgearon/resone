#include "llama_dynamic_abi.hpp"
#include "json.hpp"

#include <algorithm>
#include <cctype>
#include <cstring>
#include <memory>
#include <mutex>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#define RESONE_API extern "C" __declspec(dllexport)
#else
#include <dlfcn.h>
#define RESONE_API extern "C" __attribute__((visibility("default")))
#endif

using json = nlohmann::json;
using emit_callback = int (*)(void *, const char *, int);
using cancel_callback = int (*)(void *);

namespace {

void write_error(char * out, int size, const std::string & text) {
    if (!out || size <= 0) return;
    std::strncpy(out, text.c_str(), static_cast<size_t>(size - 1));
    out[size - 1] = 0;
}

#ifdef _WIN32
using module_handle = HMODULE;

std::wstring widen(const std::string & value) {
    if (value.empty()) return {};
    int needed = MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), nullptr, 0);
    if (needed <= 0) throw std::runtime_error("Unable to convert llama engine path to UTF-16.");
    std::wstring result(static_cast<size_t>(needed), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), result.data(), needed);
    return result;
}

std::string windows_error(DWORD code) {
    wchar_t * buffer = nullptr;
    FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                   nullptr, code, 0, reinterpret_cast<wchar_t *>(&buffer), 0, nullptr);
    if (!buffer) return "Windows error " + std::to_string(code);
    std::wstring wide(buffer);
    LocalFree(buffer);
    while (!wide.empty() && (wide.back() == L'\r' || wide.back() == L'\n' || wide.back() == L' ')) wide.pop_back();
    int needed = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), static_cast<int>(wide.size()), nullptr, 0, nullptr, nullptr);
    std::string result(static_cast<size_t>(std::max(0, needed)), '\0');
    if (needed > 0) WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), static_cast<int>(wide.size()), result.data(), needed, nullptr, nullptr);
    return result;
}

module_handle load_module(const std::string & path) {
    const auto wide = widen(path);
    auto module = LoadLibraryExW(wide.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!module) throw std::runtime_error("Unable to load " + path + ": " + windows_error(GetLastError()));
    return module;
}

void * symbol(module_handle module, const char * name) {
    auto p = reinterpret_cast<void *>(GetProcAddress(module, name));
    if (!p) throw std::runtime_error(std::string("llama.cpp engine is missing required export '") + name + "'. The installed engine pack does not match Resone's llama ABI.");
    return p;
}

std::string join_path(const std::string & dir, const std::string & name) {
    if (dir.empty()) return name;
    const char tail = dir.back();
    return dir + ((tail == '\\' || tail == '/') ? "" : "\\") + name;
}
#else
using module_handle = void *;
module_handle load_module(const std::string & path) {
    auto module = dlopen(path.c_str(), RTLD_NOW | RTLD_LOCAL);
    if (!module) throw std::runtime_error("Unable to load " + path + ": " + (dlerror() ? dlerror() : "unknown dlopen error"));
    return module;
}
void * symbol(module_handle module, const char * name) {
    auto p = dlsym(module, name);
    if (!p) throw std::runtime_error(std::string("llama.cpp engine is missing required export '") + name + "'. The installed engine pack does not match Resone's llama ABI.");
    return p;
}
std::string join_path(const std::string & dir, const std::string & name) {
    if (dir.empty()) return name;
    return dir + (dir.back() == '/' ? "" : "/") + name;
}
#endif

template <typename T>
T load_symbol(module_handle module, const char * name) {
    return reinterpret_cast<T>(symbol(module, name));
}

struct llama_api {
    module_handle llama{};
    module_handle ggml{};
    std::string engine_dir;

    decltype(&llama_backend_init) backend_init{};
    decltype(&llama_model_default_params) model_default_params{};
    decltype(&llama_context_default_params) context_default_params{};
    decltype(&llama_sampler_chain_default_params) sampler_chain_default_params{};
    decltype(&llama_model_load_from_file) model_load_from_file{};
    decltype(&llama_model_free) model_free{};
    decltype(&llama_init_from_model) init_from_model{};
    decltype(&llama_free) context_free{};
    decltype(&llama_model_get_vocab) model_get_vocab{};
    decltype(&llama_model_meta_val_str) model_meta_val_str{};
    decltype(&llama_model_chat_template) model_chat_template{};
    decltype(&llama_chat_apply_template) chat_apply_template{};
    decltype(&llama_tokenize) tokenize{};
    decltype(&llama_token_to_piece) token_to_piece{};
    decltype(&llama_vocab_is_eog) vocab_is_eog{};
    decltype(&llama_get_memory) get_memory{};
    decltype(&llama_memory_clear) memory_clear{};
    decltype(&llama_memory_seq_rm) memory_seq_rm{};
    decltype(&llama_memory_seq_add) memory_seq_add{};
    decltype(&llama_set_abort_callback) set_abort_callback{};
    decltype(&llama_batch_get_one) batch_get_one{};
    decltype(&llama_decode) decode{};
    decltype(&llama_sampler_chain_init) sampler_chain_init{};
    decltype(&llama_sampler_chain_add) sampler_chain_add{};
    decltype(&llama_sampler_init_top_k) sampler_init_top_k{};
    decltype(&llama_sampler_init_top_p) sampler_init_top_p{};
    decltype(&llama_sampler_init_temp) sampler_init_temp{};
    decltype(&llama_sampler_init_dist) sampler_init_dist{};
    decltype(&llama_sampler_sample) sampler_sample{};
    decltype(&llama_sampler_free) sampler_free{};
    decltype(&llama_log_set) log_set{};
    const char * (*version)(){}; // optional; b9870 public header does not require this export
    decltype(&ggml_backend_load_all_from_path) backend_load_all_from_path{};
};

std::mutex runtime_lock;
std::unique_ptr<llama_api> runtime;

void silent_log(enum ggml_log_level, const char *, void *) {
    // Intentionally suppress llama.cpp stdout/stderr-style runtime chatter.
}

llama_api & load_runtime(const std::string & engine_dir) {
    std::lock_guard guard(runtime_lock);
    if (runtime) {
        if (runtime->engine_dir != engine_dir) throw std::runtime_error("Restart the Resone AI stack after changing the llama engine directory.");
        return *runtime;
    }

    auto api = std::make_unique<llama_api>();
    api->engine_dir = engine_dir;
#ifdef _WIN32
    api->ggml = load_module(join_path(engine_dir, "ggml.dll"));
    api->llama = load_module(join_path(engine_dir, "llama.dll"));
#elif defined(__APPLE__)
    api->ggml = load_module(join_path(engine_dir, "libggml.dylib"));
    api->llama = load_module(join_path(engine_dir, "libllama.dylib"));
#else
    api->ggml = load_module(join_path(engine_dir, "libggml.so"));
    api->llama = load_module(join_path(engine_dir, "libllama.so"));
#endif

#define LLAMA_FN(member, name) api->member = load_symbol<decltype(api->member)>(api->llama, name)
    LLAMA_FN(backend_init, "llama_backend_init");
    LLAMA_FN(model_default_params, "llama_model_default_params");
    LLAMA_FN(context_default_params, "llama_context_default_params");
    LLAMA_FN(sampler_chain_default_params, "llama_sampler_chain_default_params");
    LLAMA_FN(model_load_from_file, "llama_model_load_from_file");
    LLAMA_FN(model_free, "llama_model_free");
    LLAMA_FN(init_from_model, "llama_init_from_model");
    LLAMA_FN(context_free, "llama_free");
    LLAMA_FN(model_get_vocab, "llama_model_get_vocab");
    LLAMA_FN(model_meta_val_str, "llama_model_meta_val_str");
    LLAMA_FN(model_chat_template, "llama_model_chat_template");
    LLAMA_FN(chat_apply_template, "llama_chat_apply_template");
    LLAMA_FN(tokenize, "llama_tokenize");
    LLAMA_FN(token_to_piece, "llama_token_to_piece");
    LLAMA_FN(vocab_is_eog, "llama_vocab_is_eog");
    LLAMA_FN(get_memory, "llama_get_memory");
    LLAMA_FN(memory_clear, "llama_memory_clear");
    LLAMA_FN(memory_seq_rm, "llama_memory_seq_rm");
    LLAMA_FN(memory_seq_add, "llama_memory_seq_add");
    LLAMA_FN(set_abort_callback, "llama_set_abort_callback");
    LLAMA_FN(batch_get_one, "llama_batch_get_one");
    LLAMA_FN(decode, "llama_decode");
    LLAMA_FN(sampler_chain_init, "llama_sampler_chain_init");
    LLAMA_FN(sampler_chain_add, "llama_sampler_chain_add");
    LLAMA_FN(sampler_init_top_k, "llama_sampler_init_top_k");
    LLAMA_FN(sampler_init_top_p, "llama_sampler_init_top_p");
    LLAMA_FN(sampler_init_temp, "llama_sampler_init_temp");
    LLAMA_FN(sampler_init_dist, "llama_sampler_init_dist");
    LLAMA_FN(sampler_sample, "llama_sampler_sample");
    LLAMA_FN(sampler_free, "llama_sampler_free");
    LLAMA_FN(log_set, "llama_log_set");
    // llama_version() was not part of the b9870 public header contract.
    // Resolve it opportunistically for diagnostics only; never fail engine load if absent.
#ifdef _WIN32
    api->version = reinterpret_cast<const char * (*)()>(GetProcAddress(api->llama, "llama_version"));
#else
    api->version = reinterpret_cast<const char * (*)()>(dlsym(api->llama, "llama_version"));
#endif
#undef LLAMA_FN

    api->backend_load_all_from_path = load_symbol<decltype(api->backend_load_all_from_path)>(api->ggml, "ggml_backend_load_all_from_path");
    api->log_set(&silent_log, nullptr);
    api->backend_load_all_from_path(engine_dir.c_str());
    api->backend_init();
    runtime = std::move(api);
    return *runtime;
}

struct session {
    llama_api * api{};
    llama_model * model{};
    llama_context * ctx{};
    std::mutex lock;
    int context_tokens{};
    std::string engine_version;
    std::string model_architecture;

    ~session() {
        if (ctx) api->context_free(ctx);
        if (model) api->model_free(model);
    }
};

std::string trim_copy(const std::string & value) {
    size_t first = 0;
    size_t last = value.size();
    while (first < last && std::isspace(static_cast<unsigned char>(value[first]))) ++first;
    while (last > first && std::isspace(static_cast<unsigned char>(value[last - 1]))) --last;
    return value.substr(first, last - first);
}

bool is_gemma4_template(const char * tmpl) {
    if (!tmpl) return false;
    const std::string_view view(tmpl);
    return view.find("<|turn>") != std::string_view::npos &&
           view.find("<turn|>") != std::string_view::npos;
}

// b9870's llama_chat_apply_template() only knows the older hard-coded Gemma
// format (<start_of_turn>...). Gemma 4 uses <|turn>role ... <turn|>, so the
// public b9870 formatter returns -1 for the template embedded in Gemma 4 GGUFs.
// Keep the normal llama formatter for supported models and only use this
// narrow fallback for a detected Gemma 4 template/architecture.
std::string format_gemma4_prompt(const std::vector<std::string> & roles,
                                 const std::vector<std::string> & contents) {
    std::string prompt;
    size_t reserve = 64;
    for (const auto & c : contents) reserve += c.size() + 32;
    prompt.reserve(reserve);

    for (size_t i = 0; i < roles.size(); ++i) {
        std::string role = roles[i];
        if (role == "assistant") role = "model";
        if (role == "developer") role = "system";
        if (role != "system" && role != "user" && role != "model")
            throw std::runtime_error("Unsupported Gemma 4 chat role: " + role);

        std::string content = trim_copy(contents[i]);
        // Thinking is opt-in for Gemma 4 through <|think|>. Resone explicitly
        // runs non-thinking inference, so never forward an accidental leading
        // opt-in token from a system instruction.
        if (role == "system" && content.rfind("<|think|>", 0) == 0)
            content = trim_copy(content.substr(std::strlen("<|think|>")));

        prompt += "<|turn>";
        prompt += role;
        prompt += '\n';
        prompt += content;
        prompt += "<turn|>\n";
    }

    // Gemma 4 E2B/E4B thinking-off generation prompt. Do not insert the empty
    // thought channel used by some of the larger Gemma 4 variants.
    prompt += "<|turn>model\n";
    return prompt;
}

std::string format_prompt(session & s, const char * messages_json) {
    const auto parsed = json::parse(messages_json ? messages_json : "[]");
    if (!parsed.is_array()) throw std::runtime_error("Native llama messages must be a JSON array.");

    std::vector<std::string> roles;
    std::vector<std::string> contents;
    roles.reserve(parsed.size());
    contents.reserve(parsed.size());
    for (const auto & item : parsed) {
        roles.push_back(item.value("role", "user"));
        contents.push_back(item.value("content", ""));
        if (roles.back() == "system" && contents.back().rfind("<|think|>", 0) == 0)
            contents.back().erase(0, std::strlen("<|think|>"));
    }

    std::vector<llama_chat_message> chat;
    chat.reserve(roles.size());
    for (size_t i = 0; i < roles.size(); ++i) chat.push_back({roles[i].c_str(), contents[i].c_str()});

    const char * tmpl = s.api->model_chat_template(s.model, nullptr);
    int32_t needed = s.api->chat_apply_template(tmpl, chat.data(), chat.size(), true, nullptr, 0);
    if (needed > 0) {
        std::string prompt(static_cast<size_t>(needed) + 1, '\0');
        int32_t written = s.api->chat_apply_template(tmpl, chat.data(), chat.size(), true,
                                                     prompt.data(), static_cast<int32_t>(prompt.size()));
        if (written < 0) throw std::runtime_error("llama.cpp chat-template formatting failed.");
        prompt.resize(static_cast<size_t>(written));
        return prompt;
    }

    if (s.model_architecture == "gemma4" || is_gemma4_template(tmpl))
        return format_gemma4_prompt(roles, contents);

    std::string detail = "llama.cpp b9870 could not apply the model's chat template";
    if (!s.model_architecture.empty()) detail += " (architecture=" + s.model_architecture + ")";
    detail += ". The embedded template is not supported by b9870's legacy formatter.";
    throw std::runtime_error(detail);
}

struct abort_state {
    cancel_callback fn{};
    void * user{};
};

bool abort_eval(void * opaque) {
    auto * state = static_cast<abort_state *>(opaque);
    return state && state->fn && state->fn(state->user) != 0;
}

} // namespace

// Bridge ABI 3 removes application-level output-token budgets. Generation now
// continues until EOS/cancellation, shifting the b9870 KV context when needed.
// The configured context size is the model's active window, not an output cap.
RESONE_API int resone_llama_bridge_abi() { return 3; }

RESONE_API const char * resone_llama_bridge_build_id() {
    return "resone-llama-bridge/3 llama.cpp-b9870 unlimited-output";
}

RESONE_API void * resone_llama_open(const char * engine_dir,
                                    const char * model_path,
                                    int context_tokens,
                                    int gpu_layers,
                                    int threads,
                                    int flash_attention,
                                    char * err,
                                    int err_size) {
    try {
        if (!engine_dir || !*engine_dir) throw std::runtime_error("llama engine directory is empty.");
        if (!model_path || !*model_path) throw std::runtime_error("GGUF model path is empty.");
        auto & api = load_runtime(engine_dir);
        auto s = std::make_unique<session>();
        s->api = &api;
        s->context_tokens = context_tokens;
        s->engine_version = api.version ? api.version() : "b9870";

        auto mp = api.model_default_params();
        mp.n_gpu_layers = gpu_layers;
        s->model = api.model_load_from_file(model_path, mp);
        if (!s->model) throw std::runtime_error("llama.cpp could not load the GGUF model. Check model path, engine/backend compatibility, VRAM and RAM.");

        char architecture[128]{};
        if (api.model_meta_val_str(s->model, "general.architecture", architecture, sizeof(architecture)) >= 0)
            s->model_architecture = architecture;

        auto cp = api.context_default_params();
        cp.n_ctx = static_cast<uint32_t>(context_tokens);
        cp.n_batch = std::min<uint32_t>(512, cp.n_ctx);
        cp.n_ubatch = std::min<uint32_t>(512, cp.n_batch);
        cp.n_threads = std::max(1, threads);
        cp.n_threads_batch = std::max(1, threads);
        cp.flash_attn_type = flash_attention ? LLAMA_FLASH_ATTN_TYPE_ENABLED : LLAMA_FLASH_ATTN_TYPE_DISABLED;
        s->ctx = api.init_from_model(s->model, cp);
        if (!s->ctx) throw std::runtime_error("llama.cpp could not allocate the inference context.");
        return s.release();
    } catch (const std::exception & e) {
        write_error(err, err_size, e.what());
        return nullptr;
    } catch (...) {
        write_error(err, err_size, "Native llama.cpp model initialization failed.");
        return nullptr;
    }
}

RESONE_API void resone_llama_close(void * handle) {
    delete static_cast<session *>(handle);
}

RESONE_API int resone_llama_generate(void * handle,
                                     const char * messages_json,
                                     float temperature,
                                     int top_k,
                                     float top_p,
                                     emit_callback emit,
                                     cancel_callback cancel,
                                     void * user,
                                     char * err,
                                     int err_size) {
    try {
        if (!handle) throw std::runtime_error("Native llama model is not loaded.");
        auto & s = *static_cast<session *>(handle);
        std::lock_guard guard(s.lock);
        auto & api = *s.api;

        const std::string prompt = format_prompt(s, messages_json);
        const auto * vocab = api.model_get_vocab(s.model);
        int token_count = -api.tokenize(vocab, prompt.c_str(), static_cast<int32_t>(prompt.size()), nullptr, 0, true, true);
        if (token_count <= 0) throw std::runtime_error("llama.cpp tokenization failed.");
        // The context size is a physical rolling window, not a pre-reserved output
        // budget. A prompt only fails when the prompt itself cannot fit. Generated
        // output is allowed to continue until EOS; if the window fills, shift the
        // middle/oldest portion while preserving the beginning and newest material.
        if (token_count >= s.context_tokens - 4)
            throw std::runtime_error("The formatted prompt itself exceeds the configured Resone context (" + std::to_string(s.context_tokens) + " tokens). Increase contextTokens or reduce the actual prompt material.");

        std::vector<llama_token> tokens(static_cast<size_t>(token_count));
        if (api.tokenize(vocab, prompt.c_str(), static_cast<int32_t>(prompt.size()), tokens.data(), token_count, true, true) < 0)
            throw std::runtime_error("llama.cpp tokenization failed.");

        api.memory_clear(api.get_memory(s.ctx), true);
        abort_state state{cancel, user};
        api.set_abort_callback(s.ctx, &abort_eval, &state);
        struct abort_reset {
            llama_api & api;
            llama_context * ctx;
            ~abort_reset() { api.set_abort_callback(ctx, nullptr, nullptr); }
        } reset{api, s.ctx};

        constexpr int prompt_batch = 512;
        for (int pos = 0; pos < token_count; pos += prompt_batch) {
            if (cancel && cancel(user)) return 1;
            const int count = std::min(prompt_batch, token_count - pos);
            auto batch = api.batch_get_one(tokens.data() + pos, count);
            const int status = api.decode(s.ctx, batch);
            if (status != 0) {
                if (cancel && cancel(user)) return 1;
                throw std::runtime_error("llama.cpp prompt evaluation failed.");
            }
        }

        auto * sampler = api.sampler_chain_init(api.sampler_chain_default_params());
        if (!sampler) throw std::runtime_error("llama.cpp sampler initialization failed.");
        struct sampler_guard {
            llama_api & api;
            llama_sampler * sampler;
            ~sampler_guard() { if (sampler) api.sampler_free(sampler); }
        } free_sampler{api, sampler};
        api.sampler_chain_add(sampler, api.sampler_init_top_k(top_k));
        api.sampler_chain_add(sampler, api.sampler_init_top_p(top_p, 1));
        api.sampler_chain_add(sampler, api.sampler_init_temp(temperature));
        api.sampler_chain_add(sampler, api.sampler_init_dist(LLAMA_DEFAULT_SEED));

        int n_past = token_count;
        const int n_keep = std::min(token_count, std::max(1, s.context_tokens / 4));
        while (true) {
            if (cancel && cancel(user)) return 1;
            const llama_token token = api.sampler_sample(sampler, s.ctx, -1);
            if (api.vocab_is_eog(vocab, token)) return 0;

            char piece_buffer[256];
            int count = api.token_to_piece(vocab, token, piece_buffer, sizeof(piece_buffer), 0, false);
            std::string piece;
            if (count < 0) {
                std::vector<char> large(static_cast<size_t>(-count));
                count = api.token_to_piece(vocab, token, large.data(), static_cast<int32_t>(large.size()), 0, false);
                if (count < 0) throw std::runtime_error("llama.cpp token decoding failed.");
                piece.assign(large.data(), static_cast<size_t>(count));
            } else {
                piece.assign(piece_buffer, static_cast<size_t>(count));
            }
            if (!piece.empty() && emit && emit(user, piece.data(), static_cast<int>(piece.size())) != 0) return 1;

            // Infinite/uncapped generation using llama.cpp b9870's own context-shift
            // strategy: preserve the first quarter (system/identity), discard half of
            // the older middle, and retain the newest generated material.
            if (n_past + 1 >= s.context_tokens) {
                const int n_left = n_past - n_keep;
                const int n_discard = std::max(1, n_left / 2);
                auto mem = api.get_memory(s.ctx);
                if (n_left <= 0 || !api.memory_seq_rm(mem, 0, n_keep, n_keep + n_discard))
                    throw std::runtime_error("llama.cpp context is full and could not shift its rolling context window.");
                api.memory_seq_add(mem, 0, n_keep + n_discard, n_past, -n_discard);
                n_past -= n_discard;
            }

            auto batch = api.batch_get_one(const_cast<llama_token *>(&token), 1);
            const int status = api.decode(s.ctx, batch);
            if (status != 0) {
                if (cancel && cancel(user)) return 1;
                throw std::runtime_error("llama.cpp token evaluation failed.");
            }
            ++n_past;
        }
    } catch (const std::exception & e) {
        write_error(err, err_size, e.what());
        return -1;
    } catch (...) {
        write_error(err, err_size, "Native llama.cpp generation failed.");
        return -1;
    }
}
