#pragma once
#include "DynamicLibrary.hpp"
#include "resone_api.h"
#include "Model.hpp"
#include <vector>
#include <functional>
#include <string>
namespace resone {
class ApiClient {
    DynamicLibrary lib_;
    decltype(&resone_open) open_;
    decltype(&resone_close) close_;
    decltype(&resone_send) send_;
    decltype(&resone_render_midi) renderMidi_;
    decltype(&resone_export_project_midi) exportProjectMidi_;
    decltype(&resone_export_lane_midi) exportLaneMidi_;
    decltype(&resone_last_error) lastError_;
    decltype(&resone_free_buffer) freeBuffer_;
    int64_t handle_{};
    std::function<void(std::string)> receive_;
    static void callback(void *user, const uint8_t *data, int32_t size) noexcept {
        try {
            static_cast<ApiClient *>(user)->receive_(std::string(reinterpret_cast<const char *>(data), size));
        } catch (...) {
        }
    }
    std::string localError() const {
        int32_t length = 0;
        auto *data = lastError_(&length);
        if (!data || length <= 0) {
            if (data) freeBuffer_(data);
            return "Local Resonator operation failed";
        }
        std::string message(reinterpret_cast<const char *>(data), static_cast<size_t>(length));
        freeBuffer_(data);
        return message;
    }
    template <class Invoke> std::vector<uint8_t> localMidi(Invoke invoke) const {
        int32_t length = 0;
        auto *data = invoke(&length);
        if (!data || length <= 0) {
            if (data) freeBuffer_(data);
            throw std::runtime_error(localError());
        }
        std::vector<uint8_t> result(data, data + length);
        freeBuffer_(data);
        return result;
    }

  public:
    ApiClient(const std::filesystem::path &path, const std::filesystem::path &runtimeRoot,
              std::function<void(std::string)> receive)
        : lib_(path, true), receive_(std::move(receive)) {
        if (lib_.symbol<decltype(&resone_abi_version)>("resone_abi_version")() != 2)
            throw std::runtime_error("Unsupported Resone API ABI");
        using SetHome = void(*)(const char*);
        // The API DLL can live in build/api during development. Pass the actual
        // runtime/source root, not the DLL's parent directory, so launcher,
        // config, engines and models resolve from the same tree.
        lib_.symbol<SetHome>("resone_set_home")(runtimeRoot.string().c_str());
        open_ = lib_.symbol<decltype(open_)>("resone_open");
        close_ = lib_.symbol<decltype(close_)>("resone_close");
        send_ = lib_.symbol<decltype(send_)>("resone_send");
        renderMidi_ = lib_.symbol<decltype(renderMidi_)>("resone_render_midi");
        exportProjectMidi_ = lib_.symbol<decltype(exportProjectMidi_)>("resone_export_project_midi");
        exportLaneMidi_ = lib_.symbol<decltype(exportLaneMidi_)>("resone_export_lane_midi");
        lastError_ = lib_.symbol<decltype(lastError_)>("resone_last_error");
        freeBuffer_ = lib_.symbol<decltype(freeBuffer_)>("resone_free_buffer");
    }
    ~ApiClient() {
        disconnect();
    }
    void disconnect() {
        if (handle_) {
            close_(handle_);
            handle_ = 0;
        }
    }
    void connect(const std::string &url) {
        disconnect();
        handle_ = open_(url.c_str(), callback, this);
        if (!handle_)
            throw std::runtime_error("Could not connect to the Resone host");
    }
    void send(const std::string &value) {
        if (!handle_ || !send_(handle_, reinterpret_cast<const uint8_t *>(value.data()),
                               static_cast<int32_t>(value.size())))
            throw std::runtime_error("Host is disconnected or its request queue is full");
    }
    std::vector<uint8_t> renderMidi(const std::string &notation) const {
        return localMidi([&](int32_t *length) {
            return renderMidi_(reinterpret_cast<const uint8_t *>(notation.data()),
                               static_cast<int32_t>(notation.size()), length);
        });
    }
    std::vector<uint8_t> exportProjectMidi(const Json &project) const {
        const auto json = project.dump();
        return localMidi([&](int32_t *length) {
            return exportProjectMidi_(reinterpret_cast<const uint8_t *>(json.data()),
                                      static_cast<int32_t>(json.size()), length);
        });
    }
    std::vector<uint8_t> exportLaneMidi(const Json &project, const std::string &laneId) const {
        const auto json = project.dump();
        return localMidi([&](int32_t *length) {
            return exportLaneMidi_(reinterpret_cast<const uint8_t *>(json.data()),
                                   static_cast<int32_t>(json.size()),
                                   reinterpret_cast<const uint8_t *>(laneId.data()),
                                   static_cast<int32_t>(laneId.size()), length);
        });
    }
};
} // namespace resone
