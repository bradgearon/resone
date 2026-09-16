#pragma once
#include "DynamicLibrary.hpp"
#include "resone_api.h"
#include <functional>
#include <string>
namespace resone {
class ApiClient {
    DynamicLibrary lib_;
    decltype(&resone_open) open_;
    decltype(&resone_close) close_;
    decltype(&resone_send) send_;
    int64_t handle_{};
    std::function<void(std::string)> receive_;
    static void callback(void *user, const uint8_t *data, int32_t size) noexcept {
        try {
            static_cast<ApiClient *>(user)->receive_(std::string(reinterpret_cast<const char *>(data), size));
        } catch (...) {
        }
    }

  public:
    ApiClient(const std::filesystem::path &path, std::function<void(std::string)> receive)
        : lib_(path, true), receive_(std::move(receive)) {
        if (lib_.symbol<decltype(&resone_abi_version)>("resone_abi_version")() != 1)
            throw std::runtime_error("Unsupported Resone API ABI");
        using SetHome = void(*)(const char*);
        lib_.symbol<SetHome>("resone_set_home")(path.parent_path().string().c_str());
        open_ = lib_.symbol<decltype(open_)>("resone_open");
        close_ = lib_.symbol<decltype(close_)>("resone_close");
        send_ = lib_.symbol<decltype(send_)>("resone_send");
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
};
} // namespace resone
