#pragma once

// Resone's pinned llama.cpp C-ABI declarations.
//
// This is intentionally NOT a llama.cpp source checkout. Resone only needs the
// public ABI layout/signatures used by the tiny dynamic loader bridge. The
// actual llama.cpp implementation is supplied at runtime as a prebuilt engine
// pack (llama.dll / libllama.* plus GGML backend libraries).
//
// ABI snapshot: llama.cpp commit c6824a9e42ceeda5d58089fa274ddd816e59e68e
// Keep runtime packs compatible with this ABI when updating them.

#include <cstddef>
#include <cstdint>

#define LLAMA_DEFAULT_SEED 0xFFFFFFFFu

struct llama_vocab;
struct llama_model;
struct llama_context;
struct llama_sampler;
struct llama_memory_i;
struct ggml_tensor;

typedef llama_memory_i * llama_memory_t;
typedef int32_t llama_pos;
typedef int32_t llama_token;
typedef int32_t llama_seq_id;

// Opaque GGML pointer aliases. Only pointer size/layout matters to the parameter
// structs below; Resone never dereferences these objects.
typedef void * ggml_backend_dev_t;
typedef void * ggml_backend_buffer_type_t;

enum ggml_type : int {
    GGML_TYPE_F32 = 0,
};

enum ggml_log_level : int {
    GGML_LOG_LEVEL_NONE  = 0,
    GGML_LOG_LEVEL_DEBUG = 1,
    GGML_LOG_LEVEL_INFO  = 2,
    GGML_LOG_LEVEL_WARN  = 3,
    GGML_LOG_LEVEL_ERROR = 4,
    GGML_LOG_LEVEL_CONT  = 5,
};

using ggml_abort_callback = bool (*)(void * data);
using ggml_log_callback = void (*)(enum ggml_log_level level, const char * text, void * user_data);
// The bridge never calls/sets this callback directly; it only preserves the
// field returned by llama_context_default_params().
using ggml_backend_sched_eval_callback = bool (*)(struct ggml_tensor * t, bool ask, void * user_data);

enum llama_rope_scaling_type : int {
    LLAMA_ROPE_SCALING_TYPE_UNSPECIFIED = -1,
    LLAMA_ROPE_SCALING_TYPE_NONE        = 0,
    LLAMA_ROPE_SCALING_TYPE_LINEAR      = 1,
    LLAMA_ROPE_SCALING_TYPE_YARN        = 2,
    LLAMA_ROPE_SCALING_TYPE_LONGROPE    = 3,
};

enum llama_pooling_type : int {
    LLAMA_POOLING_TYPE_UNSPECIFIED = -1,
    LLAMA_POOLING_TYPE_NONE = 0,
    LLAMA_POOLING_TYPE_MEAN = 1,
    LLAMA_POOLING_TYPE_CLS = 2,
    LLAMA_POOLING_TYPE_LAST = 3,
    LLAMA_POOLING_TYPE_RANK = 4,
};

enum llama_attention_type : int {
    LLAMA_ATTENTION_TYPE_UNSPECIFIED = -1,
    LLAMA_ATTENTION_TYPE_CAUSAL = 0,
    LLAMA_ATTENTION_TYPE_NON_CAUSAL = 1,
};

enum llama_flash_attn_type : int {
    LLAMA_FLASH_ATTN_TYPE_AUTO = -1,
    LLAMA_FLASH_ATTN_TYPE_DISABLED = 0,
    LLAMA_FLASH_ATTN_TYPE_ENABLED = 1,
};

enum llama_split_mode : int {
    LLAMA_SPLIT_MODE_NONE = 0,
    LLAMA_SPLIT_MODE_LAYER = 1,
    LLAMA_SPLIT_MODE_ROW = 2,
    LLAMA_SPLIT_MODE_TENSOR = 3,
};

enum llama_load_mode : int {
    LLAMA_LOAD_MODE_AUTO = -1,
    LLAMA_LOAD_MODE_NONE = 0,
    LLAMA_LOAD_MODE_MMAP = 1,
    LLAMA_LOAD_MODE_MLOCK = 2,
    LLAMA_LOAD_MODE_MMAP_MLOCK = 3,
    LLAMA_LOAD_MODE_DIRECT_IO = 4,
};

enum llama_lazy_mode : int {
    LLAMA_LAZY_MODE_OFF = 0,
    LLAMA_LAZY_MODE_AUTO = 1,
    LLAMA_LAZY_MODE_ON = 2,
};

enum llama_context_type : int {
    LLAMA_CONTEXT_TYPE_DEFAULT = 0,
    LLAMA_CONTEXT_TYPE_MTP = 1,
};

using llama_progress_callback = bool (*)(float progress, void * user_data);

struct llama_batch {
    int32_t n_tokens;
    llama_token * token;
    float * embd;
    llama_pos * pos;
    int32_t * n_seq_id;
    llama_seq_id ** seq_id;
    int8_t * logits;
};

enum llama_model_kv_override_type : int {
    LLAMA_KV_OVERRIDE_TYPE_INT,
    LLAMA_KV_OVERRIDE_TYPE_FLOAT,
    LLAMA_KV_OVERRIDE_TYPE_BOOL,
    LLAMA_KV_OVERRIDE_TYPE_STR,
};

struct llama_model_kv_override {
    enum llama_model_kv_override_type tag;
    char key[128];
    union {
        int64_t val_i64;
        double val_f64;
        bool val_bool;
        char val_str[128];
    };
};

struct llama_model_tensor_buft_override {
    const char * pattern;
    ggml_backend_buffer_type_t buft;
};

struct llama_model_params {
    ggml_backend_dev_t * devices;
    const struct llama_model_tensor_buft_override * tensor_buft_overrides;
    int32_t n_gpu_layers;
    enum llama_split_mode split_mode;
    enum llama_load_mode load_mode;
    enum llama_lazy_mode lazy_mode;
    int32_t main_gpu;
    const float * tensor_split;
    llama_progress_callback progress_callback;
    void * progress_callback_user_data;
    const struct llama_model_kv_override * kv_overrides;
    bool vocab_only;
    bool check_tensors;
    bool use_extra_bufts;
    bool no_host;
    bool no_alloc;
    bool load_mtp;
};

struct llama_sampler_seq_config {
    llama_seq_id seq_id;
    struct llama_sampler * sampler;
};

struct llama_context_params {
    uint32_t n_ctx;
    uint32_t n_batch;
    uint32_t n_ubatch;
    uint32_t n_seq_max;
    uint32_t n_rs_seq;
    uint32_t n_outputs_max;
    uint32_t n_outputs_max_per_seq;
    int32_t n_threads;
    int32_t n_threads_batch;
    enum llama_context_type ctx_type;
    enum llama_rope_scaling_type rope_scaling_type;
    enum llama_pooling_type pooling_type;
    enum llama_attention_type attention_type;
    enum llama_flash_attn_type flash_attn_type;
    float rope_freq_base;
    float rope_freq_scale;
    float yarn_ext_factor;
    float yarn_attn_factor;
    float yarn_beta_fast;
    float yarn_beta_slow;
    uint32_t yarn_orig_ctx;
    float defrag_thold;
    ggml_backend_sched_eval_callback cb_eval;
    void * cb_eval_user_data;
    enum ggml_type type_k;
    enum ggml_type type_v;
    ggml_abort_callback abort_callback;
    void * abort_callback_data;
    bool embeddings;
    bool offload_kqv;
    bool no_perf;
    bool op_offload;
    bool swa_full;
    bool kv_unified;
    struct llama_sampler_seq_config * samplers;
    size_t n_samplers;
    struct llama_context * ctx_other;
};

struct llama_sampler_chain_params {
    bool no_perf;
};

struct llama_chat_message {
    const char * role;
    const char * content;
};

// Function declarations are used only for compile-time function-pointer types.
// The bridge resolves every symbol dynamically from the downloaded llama engine.
extern "C" {
void llama_backend_init(void);
struct llama_model_params llama_model_default_params(void);
struct llama_context_params llama_context_default_params(void);
struct llama_sampler_chain_params llama_sampler_chain_default_params(void);
struct llama_model * llama_model_load_from_file(const char * path_model, struct llama_model_params params);
void llama_model_free(struct llama_model * model);
struct llama_context * llama_init_from_model(struct llama_model * model, struct llama_context_params params);
void llama_free(struct llama_context * ctx);
const struct llama_vocab * llama_model_get_vocab(const struct llama_model * model);
const char * llama_model_chat_template(const struct llama_model * model, const char * name);
int32_t llama_chat_apply_template(const char * tmpl, const struct llama_chat_message * chat, size_t n_msg, bool add_ass, char * buf, int32_t length);
int32_t llama_tokenize(const struct llama_vocab * vocab, const char * text, int32_t text_len, llama_token * tokens, int32_t n_tokens_max, bool add_special, bool parse_special);
int32_t llama_token_to_piece(const struct llama_vocab * vocab, llama_token token, char * buf, int32_t length, int32_t lstrip, bool special);
bool llama_vocab_is_eog(const struct llama_vocab * vocab, llama_token token);
llama_memory_t llama_get_memory(const struct llama_context * ctx);
void llama_memory_clear(llama_memory_t mem, bool data);
void llama_set_abort_callback(struct llama_context * ctx, ggml_abort_callback abort_callback, void * abort_callback_data);
struct llama_batch llama_batch_get_one(llama_token * tokens, int32_t n_tokens);
int32_t llama_decode(struct llama_context * ctx, struct llama_batch batch);
struct llama_sampler * llama_sampler_chain_init(struct llama_sampler_chain_params params);
void llama_sampler_chain_add(struct llama_sampler * chain, struct llama_sampler * smpl);
struct llama_sampler * llama_sampler_init_top_k(int32_t k);
struct llama_sampler * llama_sampler_init_top_p(float p, size_t min_keep);
struct llama_sampler * llama_sampler_init_temp(float t);
struct llama_sampler * llama_sampler_init_dist(uint32_t seed);
llama_token llama_sampler_sample(struct llama_sampler * smpl, struct llama_context * ctx, int32_t idx);
void llama_sampler_free(struct llama_sampler * smpl);
void llama_log_set(ggml_log_callback log_callback, void * user_data);
const char * llama_version(void);
void ggml_backend_load_all_from_path(const char * dir_path);
}
