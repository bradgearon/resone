#pragma once

// Exact minimal public ABI mirror for qwentts.cpp commit
// a8a7716b530e49fed537c57711247c12fbbb903c.
//
// Resone never builds qwentts.cpp. The implementation is supplied as a
// prebuilt platform engine pack and loaded dynamically. Keep this header in
// lockstep with that engine pack. Public ABI version at this commit: 4.

#include <stdbool.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#define QT_ABI_VERSION 4
#define QT_ABI_MIN_VERSION 4

struct qt_context;

struct qt_audio {
    float * samples;
    int n_samples;
    int sample_rate;
    int channels;
};

struct qt_init_params {
    int abi_version;
    const char * talker_path;
    const char * codec_path;
    bool use_fa;
    bool clamp_fp16;
    int max_batch;
    float codec_chunk_sec;
};

typedef bool (*qt_cancel_cb)(void * user_data);
typedef bool (*qt_audio_chunk_cb)(const float * samples, int n_samples, void * user_data);

enum qt_log_level {
    QT_LOG_DEBUG = 0,
    QT_LOG_INFO  = 1,
    QT_LOG_WARN  = 2,
    QT_LOG_ERROR = 3,
};

typedef void (*qt_log_cb)(enum qt_log_level level, const char * msg, void * user_data);

struct qt_tts_params {
    int abi_version;
    const char * text;
    const char * lang;
    const char * instruct;
    const char * speaker;
    const float * ref_audio_24k;
    int ref_n_samples;
    const char * ref_text;
    int64_t seed;
    int max_new_tokens;
    bool do_sample;
    float temperature;
    int top_k;
    float top_p;
    float repetition_penalty;
    bool subtalker_do_sample;
    float subtalker_temperature;
    int subtalker_top_k;
    float subtalker_top_p;
    const char * dump_dir;
    qt_cancel_cb cancel;
    void * cancel_user_data;
    qt_audio_chunk_cb on_chunk;
    void * on_chunk_user_data;
    const float * ref_spk_emb;
    int ref_spk_dim;
    const int32_t * ref_codes;
    int ref_T;
};

enum qt_status {
    QT_STATUS_OK              = 0,
    QT_STATUS_INVALID_PARAMS  = -1,
    QT_STATUS_MODE_INVALID    = -2,
    QT_STATUS_GENERATE_FAILED = -3,
    QT_STATUS_OOM             = -4,
    QT_STATUS_CANCELLED       = -5,
};

const char * qt_version(void);
const char * qt_last_error(void);
void qt_audio_free(struct qt_audio * a);
void qt_init_default_params(struct qt_init_params * p);
struct qt_context * qt_init(const struct qt_init_params * params);
void qt_free(struct qt_context * q);
void qt_log_set(qt_log_cb cb, void * user_data);
void qt_tts_default_params(struct qt_tts_params * p);
enum qt_status qt_synthesize(struct qt_context * q, const struct qt_tts_params * params, struct qt_audio * out);
int qt_duration_sec_to_tokens(const struct qt_context * q, float duration_sec);
int qt_n_speakers(const struct qt_context * q);
const char * qt_speaker_name(const struct qt_context * q, int i);

#ifdef __cplusplus
}
#endif
