#pragma once
#include <filesystem>
#include <stdexcept>
#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#else
#include <dlfcn.h>
#endif
namespace resone {
class DynamicLibrary {
    void *handle_{};
    bool pinned_{};

  public:
    explicit DynamicLibrary(const std::filesystem::path &path, bool pinned = false) : pinned_(pinned) {
#ifdef _WIN32
        handle_ =
            reinterpret_cast<void *>(LoadLibraryExW(path.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH));
#else
        handle_ = dlopen(path.c_str(), RTLD_NOW | RTLD_LOCAL);
#endif
        if (!handle_)
            throw std::runtime_error("Could not load " + path.string());
    }
    DynamicLibrary(const DynamicLibrary &) = delete;
    ~DynamicLibrary() {
        if (handle_ && !pinned_) {
#ifdef _WIN32
            FreeLibrary(static_cast<HMODULE>(handle_));
#else
            dlclose(handle_);
#endif
        }
    }
    template <class T> T symbol(const char *name) {
#ifdef _WIN32
        auto address = GetProcAddress(static_cast<HMODULE>(handle_), name);
#else
        auto address = dlsym(handle_, name);
#endif
        if (!address)
            throw std::runtime_error(std::string("Missing native export: ") + name);
        return reinterpret_cast<T>(address);
    }
};
} // namespace resone
