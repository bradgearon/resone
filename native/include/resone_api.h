#pragma once
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif
/* ABI 1. UTF-8 event memory is borrowed until callback returns. Copy it immediately.
   close must be called off the callback and audio threads. After close returns no
   callbacks remain. NativeAOT modules stay loaded for the host process lifetime. */
typedef void (*resone_event_fn)(void *user, const uint8_t *utf8, int32_t length);
int32_t resone_abi_version(void);
/* Set the installation root before opening the first connection. */
void resone_set_home(const char *installation_root);
int64_t resone_open(const char *websocket_url, resone_event_fn callback, void *user);
int32_t resone_send(int64_t handle, const uint8_t *utf8, int32_t length);
void resone_close(int64_t handle);
#ifdef __cplusplus
}
#endif
