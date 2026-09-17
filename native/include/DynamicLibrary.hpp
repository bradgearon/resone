#pragma once
#include <algorithm>
#include <filesystem>
#include <stdexcept>
#include <string>
#include <vector>
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

#ifdef _WIN32
    static std::string windowsError(DWORD code) {
        wchar_t *buffer = nullptr;
        FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                       nullptr, code, 0, reinterpret_cast<wchar_t *>(&buffer), 0, nullptr);
        if (!buffer) return "Windows error " + std::to_string(code);
        std::wstring wide(buffer);
        LocalFree(buffer);
        while (!wide.empty() && (wide.back() == L'\r' || wide.back() == L'\n' || wide.back() == L' '))
            wide.pop_back();
        const int needed = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), static_cast<int>(wide.size()),
                                                nullptr, 0, nullptr, nullptr);
        std::string result(static_cast<size_t>(std::max(0, needed)), '\0');
        if (needed > 0)
            WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), static_cast<int>(wide.size()), result.data(), needed,
                                nullptr, nullptr);
        return result;
    }
#endif

  public:
    explicit DynamicLibrary(const std::filesystem::path &path, bool pinned = false,
                            const std::vector<std::filesystem::path> &searchDirectories = {})
        : pinned_(pinned) {
#ifdef _WIN32
        using AddDllDirectoryFn = void *(WINAPI *)(PCWSTR);
        using RemoveDllDirectoryFn = BOOL(WINAPI *)(void *);
        auto kernel = GetModuleHandleW(L"kernel32.dll");
        auto addDllDirectory = kernel ? reinterpret_cast<AddDllDirectoryFn>(GetProcAddress(kernel, "AddDllDirectory")) : nullptr;
        auto removeDllDirectory = kernel ? reinterpret_cast<RemoveDllDirectoryFn>(GetProcAddress(kernel, "RemoveDllDirectory")) : nullptr;
        std::vector<void *> cookies;
        auto addSearch = [&](const std::filesystem::path &directory) {
            std::error_code ec;
            if (!addDllDirectory || directory.empty() || !std::filesystem::is_directory(directory, ec)) return;
            if (auto cookie = addDllDirectory(directory.c_str())) cookies.push_back(cookie);
        };
        addSearch(path.parent_path());
        for (const auto &directory : searchDirectories) addSearch(directory);

        SetLastError(ERROR_SUCCESS);
        handle_ = reinterpret_cast<void *>(LoadLibraryExW(
            path.c_str(), nullptr,
            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS));
        DWORD error = handle_ ? ERROR_SUCCESS : GetLastError();
        if (removeDllDirectory) for (auto cookie : cookies) removeDllDirectory(cookie);

        // Older Windows loader configurations may reject the modern search flags.
        // Preserve the previous fallback while still reporting the original failure.
        if (!handle_) {
            SetLastError(ERROR_SUCCESS);
            handle_ = reinterpret_cast<void *>(LoadLibraryExW(path.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH));
            if (!handle_ && error == ERROR_SUCCESS) error = GetLastError();
        }
        if (!handle_) {
            std::string searched;
            for (const auto &directory : searchDirectories) {
                if (!searched.empty()) searched += "; ";
                searched += directory.string();
            }
            throw std::runtime_error("Could not load " + path.string() + ": " + windowsError(error) +
                                     (searched.empty() ? "" : ". Dependency search: " + searched));
        }
#else
        handle_ = dlopen(path.c_str(), RTLD_NOW | RTLD_LOCAL);
        if (!handle_)
            throw std::runtime_error("Could not load " + path.string() + ": " + (dlerror() ? dlerror() : "unknown dlopen error"));
#endif
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
