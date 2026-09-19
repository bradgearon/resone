#pragma once
#include "DynamicLibrary.hpp"
#include "Model.hpp"
#include <array>
#include <commdlg.h>
#include <commctrl.h>
#include <deque>
#include <fstream>
#include <functional>
#include <mmsystem.h>
#include <mutex>
#include <shlobj.h>
#include <string_view>
namespace resone {
// Load a Win32 icon from the module that actually contains Resone's code.
// This matters for VST3: GetModuleHandle(nullptr) returns the DAW executable,
// not the plug-in DLL, so resource lookups against nullptr can silently fall
// back to the host/default icon.
inline HMODULE currentModule() {
    HMODULE module{};
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCWSTR>(&currentModule), &module);
    return module;
}
inline bool setWindowIcon(HWND window, int resourceId) {
    if (!window || !IsWindow(window))
        return false;
    const HMODULE module = currentModule();
    if (!module)
        return false;

    const auto load = [&](int width, int height) -> HICON {
        return reinterpret_cast<HICON>(LoadImageW(module, MAKEINTRESOURCEW(resourceId), IMAGE_ICON,
                                                  width, height, LR_DEFAULTCOLOR | LR_SHARED));
    };
    HICON smallIcon = load(GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON));
    HICON largeIcon = load(GetSystemMetrics(SM_CXICON), GetSystemMetrics(SM_CYICON));
    if (!smallIcon && !largeIcon)
        return false;
    if (!smallIcon)
        smallIcon = largeIcon;
    if (!largeIcon)
        largeIcon = smallIcon;

    // WM_SETICON controls the caption icon and the icon Windows uses for the
    // taskbar button. Updating the class icons as well prevents later window
    // recreation/theme changes from falling back to IDI_APPLICATION.
    SendMessageW(window, WM_SETICON, ICON_SMALL, reinterpret_cast<LPARAM>(smallIcon));
    SendMessageW(window, WM_SETICON, ICON_BIG, reinterpret_cast<LPARAM>(largeIcon));
    SetClassLongPtrW(window, GCLP_HICONSM, reinterpret_cast<LONG_PTR>(smallIcon));
    SetClassLongPtrW(window, GCLP_HICON, reinterpret_cast<LONG_PTR>(largeIcon));
    return true;
}
inline bool isResoneRuntimeRoot(const std::filesystem::path &root) {
    std::error_code ec;
    if (root.empty()) return false;
    // Installed/runtime layout.
    if (std::filesystem::is_regular_file(root / L"config" / L"appsettings.json", ec) &&
        (std::filesystem::is_regular_file(root / L"wds.resone.api.dll", ec) ||
         std::filesystem::is_regular_file(root / L"build" / L"api" / L"wds.resone.api.dll", ec)))
        return true;
    return false;
}
inline std::filesystem::path findResoneRoot(std::filesystem::path start) {
    std::error_code ec;
    start = std::filesystem::absolute(start, ec);
    if (ec) return {};
    for (int i = 0; i < 10 && !start.empty(); ++i) {
        if (isResoneRuntimeRoot(start)) return start;
        auto parent = start.parent_path();
        if (parent == start) break;
        start = std::move(parent);
    }
    return {};
}
inline std::filesystem::path home() {
    wchar_t value[32768]{};
    auto n = GetEnvironmentVariableW(L"RESONE_HOME", value, 32768);
    if (n && n < 32768)
        return std::filesystem::path(value);

    // Development/build-folder runs must use the source/build tree they were
    // launched from instead of silently loading an older installed API/config
    // from LocalAppData. Search from the actual module and current directory.
    HMODULE module{};
    if (GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCWSTR>(&home), &module) &&
        GetModuleFileNameW(module, value, 32768)) {
        if (auto root = findResoneRoot(std::filesystem::path(value).parent_path()); !root.empty())
            return root;
    }
    if (auto root = findResoneRoot(std::filesystem::current_path()); !root.empty())
        return root;

    // Installed application fallback. User preferences remain in AppData even
    // when the runtime itself is discovered beside/in the build tree.
    PWSTR local{};
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local))) {
        auto root = std::filesystem::path(local) / "Wds/Resone";
        CoTaskMemFree(local);
        return root;
    }
    return std::filesystem::current_path();
}
inline std::filesystem::path apiLibrary(const std::filesystem::path &root) {
    const std::array candidates{
        root / L"wds.resone.api.dll",
        root / L"build" / L"api" / L"wds.resone.api.dll"
    };
    for (const auto &candidate : candidates)
        if (std::filesystem::is_regular_file(candidate)) return candidate;
    throw std::runtime_error("wds.resone.api.dll was not found under the active Resone runtime root: " + root.string());
}
inline std::filesystem::path fluidSynthLibrary(const std::filesystem::path &root) {
#ifdef _WIN32
    const std::array candidates{
        root / L"libfluidsynth-3.dll",
        root / L"third_party" / L"vcpkg" / L"installed" / L"x64-windows" / L"bin" / L"libfluidsynth-3.dll",
        root / L"third_party" / L"vcpkg" / L"installed" / L"x64-windows" / L"bin" / L"fluidsynth.dll"
    };
#else
    const std::array candidates{root / L"libfluidsynth.so.3"};
#endif
    for (const auto &candidate : candidates)
        if (std::filesystem::is_regular_file(candidate)) return candidate;
    // Preserve the installed-layout error path for a useful loader error.
#ifdef _WIN32
    return root / L"libfluidsynth-3.dll";
#else
    return root / L"libfluidsynth.so.3";
#endif
}
inline std::string utf8Path(const std::filesystem::path &path) {
    auto wide = std::filesystem::absolute(path).wstring();
    if (wide.empty())
        return {};
    const int size = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, wide.data(),
                                         static_cast<int>(wide.size()), nullptr, 0, nullptr, nullptr);
    if (size <= 0)
        throw std::runtime_error("Could not convert the Resone UI path to UTF-8");
    std::string value(static_cast<size_t>(size), '\0');
    if (WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, wide.data(), static_cast<int>(wide.size()),
                            value.data(), size, nullptr, nullptr) != size)
        throw std::runtime_error("Could not convert the Resone UI path to UTF-8");
    return value;
}
inline std::filesystem::path webIndex(const std::filesystem::path &root) {
    std::vector<std::filesystem::path> candidates;
    candidates.push_back(root / L"web" / L"index.html");

    wchar_t modulePath[32768]{};
    HMODULE module{};
    if (GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCWSTR>(&webIndex), &module) &&
        GetModuleFileNameW(module, modulePath, 32768)) {
        auto cursor = std::filesystem::path(modulePath).parent_path();
        for (int i = 0; i < 6 && !cursor.empty(); ++i) {
            candidates.push_back(cursor / L"web" / L"index.html");
            candidates.push_back(cursor / L"src" / L"wds.resone.ui" / L"resources" / L"web" / L"index.html");
            cursor = cursor.parent_path();
        }
    }

    PWSTR local{};
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local))) {
        candidates.push_back(std::filesystem::path(local) / L"Wds" / L"Resone" / L"web" / L"index.html");
        CoTaskMemFree(local);
    }

    std::error_code ec;
    for (auto &candidate : candidates) {
        auto absolute = std::filesystem::absolute(candidate, ec);
        if (ec) {
            ec.clear();
            continue;
        }
        if (std::filesystem::is_regular_file(absolute, ec) && !ec)
            return absolute;
        ec.clear();
    }
    return {};
}
inline std::filesystem::path dailyLogPath() {
    wchar_t configured[32768]{};
    std::filesystem::path directory;
    const auto n = GetEnvironmentVariableW(L"RESONE_LOG_DIR", configured, 32768);
    if (n && n < 32768) {
        directory = std::filesystem::path(configured);
    } else {
        PWSTR local{};
        if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local))) {
            directory = std::filesystem::path(local) / L"Wds" / L"Logs" / L"Resone";
            CoTaskMemFree(local);
        } else {
            directory = std::filesystem::temp_directory_path() / L"Wds" / L"Logs" / L"Resone";
        }
    }
    std::filesystem::create_directories(directory);
    SYSTEMTIME now{};
    GetLocalTime(&now);
    wchar_t fileName[64]{};
    swprintf_s(fileName, L"resone-%04u-%02u-%02u.log", now.wYear, now.wMonth, now.wDay);
    return directory / fileName;
}
inline void appendDailyLog(std::string_view area, std::string_view message) noexcept {
    HANDLE gate = nullptr;
    bool held = false;
    try {
        gate = CreateMutexW(nullptr, FALSE, L"Local\\Wds.Resone.Log");
        if (gate) {
            const auto wait = WaitForSingleObject(gate, 5000);
            held = wait == WAIT_OBJECT_0 || wait == WAIT_ABANDONED;
        }
        SYSTEMTIME now{};
        GetLocalTime(&now);
        char stamp[64]{};
        sprintf_s(stamp, "%04u-%02u-%02uT%02u:%02u:%02u.%03u",
                  now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute, now.wSecond, now.wMilliseconds);
        std::ofstream out(dailyLogPath(), std::ios::app | std::ios::binary);
        if (out) out << stamp << '\t' << GetCurrentProcessId() << '\t' << area << '\t' << message << "\r\n";
    } catch (...) {
    }
    if (held && gate) ReleaseMutex(gate);
    if (gate) CloseHandle(gate);
}

inline std::filesystem::path preferences() {
    PWSTR local{};
    SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local);
    auto p = std::filesystem::path(local) / "Wds/Resone/user";
    CoTaskMemFree(local);
    std::filesystem::create_directories(p);
    return p;
}
inline void saveJson(const std::filesystem::path &path, const Json &j) {
    auto tmp = path;
    tmp += ".tmp";
    {
        std::ofstream out(tmp);
        if (!(out << j.dump(2)))
            throw std::runtime_error("Could not save settings");
    }
    if (!MoveFileExW(tmp.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
        throw std::runtime_error("Could not replace settings file");
}

inline HWND rootWindow(HWND window) {
    if (!window || !IsWindow(window)) return nullptr;
    if (HWND root = GetAncestor(window, GA_ROOT)) return root;
    return window;
}
inline double visibleWindowFraction(const RECT &candidate) {
    const long width = std::max<LONG>(0, candidate.right - candidate.left);
    const long height = std::max<LONG>(0, candidate.bottom - candidate.top);
    const double area = double(width) * double(height);
    if (area <= 0) return 0;
    // Sum visibility across all connected monitor work areas. A deliberately
    // wide window straddling two displays should not be rejected merely
    // because neither individual monitor contains 35% of it by itself.
    struct State { RECT candidate; double visible{}; } state{candidate, 0};
    EnumDisplayMonitors(nullptr, nullptr,
        [](HMONITOR monitor, HDC, LPRECT, LPARAM value) -> BOOL {
            auto *s = reinterpret_cast<State *>(value);
            MONITORINFO info{sizeof(info)};
            if (!GetMonitorInfoW(monitor, &info)) return TRUE;
            RECT overlap{};
            if (IntersectRect(&overlap, &s->candidate, &info.rcWork)) {
                const double w = std::max<LONG>(0, overlap.right - overlap.left);
                const double h = std::max<LONG>(0, overlap.bottom - overlap.top);
                s->visible += w * h;
            }
            return TRUE;
        }, reinterpret_cast<LPARAM>(&state));
    return std::min(1.0, state.visible / area);
}
inline void saveStandaloneWindowBounds(HWND window) {
    window = rootWindow(window);
    if (!window || !IsWindow(window) || IsIconic(window)) return;

    WINDOWPLACEMENT placement{sizeof(placement)};
    if (!GetWindowPlacement(window, &placement)) return;

    // For a normal window, persist the actual outer frame that the user sees.
    // rcNormalPosition is useful while maximized, but some APP/framework paths
    // keep stale/default normal bounds while a normal window is being resized.
    RECT r{};
    const bool maximized = IsZoomed(window) != FALSE;
    if (maximized) {
        r = placement.rcNormalPosition;
    } else if (!GetWindowRect(window, &r)) {
        return;
    }

    const int width = r.right - r.left, height = r.bottom - r.top;
    if (width < 300 || height < 200) return;
    Json bounds{{"x",r.left},{"y",r.top},{"width",width},{"height",height},
                {"maximized",maximized}};
    try { saveJson(preferences() / "window.json", bounds); } catch (...) {}
}
inline bool restoreStandaloneWindowBounds(HWND window) {
    window = rootWindow(window);
    if (!window || !IsWindow(window)) return false;
    try {
        std::ifstream in(preferences() / "window.json");
        Json bounds; if (!in || !(in >> bounds)) return false;
        const int x=bounds.value("x",0), y=bounds.value("y",0), width=bounds.value("width",0), height=bounds.value("height",0);
        if (width < 300 || height < 200 || width > 10000 || height > 10000) return false;
        RECT candidate{LONG(x),LONG(y),LONG(x+width),LONG(y+height)};
        if (visibleWindowFraction(candidate) < .35) return false;

        // Apply the saved *outer frame* directly. This avoids relying on the
        // framework's editor-size bookkeeping, which may still contain the
        // compile-time PLUG_WIDTH/PLUG_HEIGHT during APP startup.
        if (!SetWindowPos(window, nullptr, x, y, width, height,
                          SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED))
            return false;

        if (bounds.value("maximized", false))
            ShowWindow(window, SW_MAXIMIZE);
        return true;
    } catch (...) { return false; }
}

// Track the actual standalone top-level HWND instead of relying only on the
// WebView editor lifecycle. The iPlug APP wrapper can perform another default
// sizing pass shortly after OpenWindow(), so restoration is intentionally
// delayed until the frame has settled. User-driven move/resize completion and
// close/destroy persist the actual outer frame.
inline constexpr UINT_PTR kStandaloneWindowSubclassId = 0x5245534F; // 'RESO'
inline constexpr UINT_PTR kStandaloneWindowRestoreTimerId = 0x5253; // 'RS'
inline constexpr UINT kStandaloneWindowRestoreDelayMs = 250;
struct StandaloneWindowTrackerState { bool restored{}; bool inMoveSize{}; };
inline LRESULT CALLBACK standaloneWindowTrackerProc(HWND window, UINT message, WPARAM wp, LPARAM lp,
                                                    UINT_PTR subclassId, DWORD_PTR refData) {
    auto *state = reinterpret_cast<StandaloneWindowTrackerState *>(refData);
    switch (message) {
    case WM_ENTERSIZEMOVE:
        if (state) state->inMoveSize = true;
        break;
    case WM_EXITSIZEMOVE:
        if (state) state->inMoveSize = false;
        if (state && state->restored)
            saveStandaloneWindowBounds(window);
        break;
    case WM_SIZE:
        // Save maximize immediately. A normal interactive resize is saved on
        // WM_EXITSIZEMOVE. Deliberately do not save SIZE_RESTORED here: APP
        // startup can emit a late default-size WM_SIZE and overwrite the user's
        // restored dimensions before they ever touch the window.
        if (state && state->restored && wp == SIZE_MAXIMIZED)
            saveStandaloneWindowBounds(window);
        break;
    case WM_TIMER:
        if (wp == kStandaloneWindowRestoreTimerId && state && !state->restored) {
            KillTimer(window, kStandaloneWindowRestoreTimerId);
            restoreStandaloneWindowBounds(window);
            state->restored = true;
            return 0;
        }
        break;
    case WM_CLOSE:
        if (state && state->restored)
            saveStandaloneWindowBounds(window);
        break;
    case WM_NCDESTROY:
        KillTimer(window, kStandaloneWindowRestoreTimerId);
        if (state && state->restored)
            saveStandaloneWindowBounds(window);
        RemoveWindowSubclass(window, standaloneWindowTrackerProc, subclassId);
        delete state;
        return DefSubclassProc(window, message, wp, lp);
    default:
        break;
    }
    return DefSubclassProc(window, message, wp, lp);
}
inline void trackStandaloneWindowBounds(HWND window) {
    window = rootWindow(window);
    if (!window || !IsWindow(window)) return;
    DWORD_PTR existing{};
    if (GetWindowSubclass(window, standaloneWindowTrackerProc, kStandaloneWindowSubclassId, &existing))
        return;

    auto *state = new StandaloneWindowTrackerState{};
    if (!SetWindowSubclass(window, standaloneWindowTrackerProc, kStandaloneWindowSubclassId,
                           reinterpret_cast<DWORD_PTR>(state))) {
        delete state;
        return;
    }

    // Delay past iPlug APP's initial/default sizing messages. Once this fires,
    // later normal WM_SIZE messages no longer write over the saved dimensions.
    if (!SetTimer(window, kStandaloneWindowRestoreTimerId, kStandaloneWindowRestoreDelayMs, nullptr)) {
        // Timer allocation is extremely unlikely to fail; restore immediately
        // as a safe fallback rather than abandoning persistence altogether.
        restoreStandaloneWindowBounds(window);
        state->restored = true;
    }
}
inline std::string base64(const std::vector<uint8_t> &b) {
    static constexpr char abc[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    std::string s;
    for (size_t i = 0; i < b.size(); i += 3) {
        unsigned v = b[i] << 16;
        if (i + 1 < b.size())
            v |= b[i + 1] << 8;
        if (i + 2 < b.size())
            v |= b[i + 2];
        s += abc[(v >> 18) & 63];
        s += abc[(v >> 12) & 63];
        s += i + 1 < b.size() ? abc[(v >> 6) & 63] : '=';
        s += i + 2 < b.size() ? abc[v & 63] : '=';
    }
    return s;
}
inline std::vector<uint8_t> unbase64(const std::string &s) {
    std::vector<uint8_t> b;
    unsigned v = 0;
    int bits = 0;
    std::string abc = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    for (auto c : s) {
        if (c == '=')
            break;
        auto n = abc.find(c);
        if (n == std::string::npos)
            throw std::runtime_error("Invalid encoded data");
        v = (v << 6) | unsigned(n);
        bits += 6;
        if (bits >= 8) {
            bits -= 8;
            b.push_back(uint8_t(v >> bits));
        }
    }
    return b;
}
// A Win32 message queue marshals network and render events onto the editor thread.
// No timer, OnIdle polling or web status polling is used.
class Dispatcher {
    HWND window_{};
    std::wstring className_;
    std::mutex mutex_;
    std::deque<Json> queue_;
    std::function<void(Json)> consume_;
    static LRESULT CALLBACK proc(HWND w, UINT m, WPARAM wp, LPARAM lp) {
        auto *self = reinterpret_cast<Dispatcher *>(GetWindowLongPtrW(w, GWLP_USERDATA));
        if (m == WM_NCCREATE) {
            self = static_cast<Dispatcher *>(reinterpret_cast<CREATESTRUCTW *>(lp)->lpCreateParams);
            SetWindowLongPtrW(w, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
        }
        if (m == WM_APP + 37 && self) {
            std::deque<Json> q;
            {
                std::lock_guard lock(self->mutex_);
                q.swap(self->queue_);
            }
            for (auto &j : q) {
                try {
                    self->consume_(std::move(j));
                } catch (...) {
                }
            }
            return 0;
        }
        return DefWindowProcW(w, m, wp, lp);
    }

  public:
    explicit Dispatcher(std::function<void(Json)> fn)
        : className_(L"Wds.Resone.Dispatcher." + std::to_wstring(reinterpret_cast<uintptr_t>(this))),
          consume_(std::move(fn)) {}
    void attach() {
        std::lock_guard lock(mutex_);
        if (window_)
            return;
        WNDCLASSW wc{};
        wc.lpfnWndProc = proc;
        wc.hInstance = GetModuleHandleW(nullptr);
        wc.lpszClassName = className_.c_str();
        RegisterClassW(&wc);
        window_ =
            CreateWindowW(wc.lpszClassName, L"", 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, wc.hInstance, this);
        if (!window_)
            throw std::runtime_error("Could not create event dispatcher");
        PostMessageW(window_, WM_APP + 37, 0, 0);
    }
    ~Dispatcher() {
        detach();
    }
    void detach() {
        HWND old;
        {
            std::lock_guard lock(mutex_);
            old = window_;
            window_ = nullptr;
        }
        if (old) {
            DestroyWindow(old);
            UnregisterClassW(className_.c_str(), GetModuleHandleW(nullptr));
        }
    }
    void post(Json event) {
        std::lock_guard lock(mutex_);
        queue_.push_back(std::move(event));
        if (window_)
            PostMessageW(window_, WM_APP + 37, 0, 0);
    }
};
// One bounded WAV buffer. WinMM captures asynchronously; stop/reset returns its
// actual length. The callback only signals completion via the dispatcher.
class VoiceCapture {
    HWAVEIN input_{};
    WAVEHDR header_{};
    std::vector<uint8_t> pcm_;
    std::function<void()> complete_;
    static void CALLBACK done(HWAVEIN, UINT m, DWORD_PTR user, DWORD_PTR, DWORD_PTR) {
        if (m == WIM_DATA) {
            auto *s = reinterpret_cast<VoiceCapture *>(user);
            try {
                s->complete_();
            } catch (...) {
            }
        }
    }

  public:
    ~VoiceCapture() {
        stop(false);
    }
    bool recording() const {
        return input_ != nullptr;
    }
    void start(std::function<void()> complete) {
        if (input_)
            return;
        complete_ = std::move(complete);
        pcm_.assign(16000 * 2 * 90, 0);
        header_ = {};
        header_.lpData = reinterpret_cast<char *>(pcm_.data());
        header_.dwBufferLength = static_cast<DWORD>(pcm_.size());
        WAVEFORMATEX f{WAVE_FORMAT_PCM, 1, 16000, 32000, 2, 16, 0};
        if (waveInOpen(&input_, WAVE_MAPPER, &f, reinterpret_cast<DWORD_PTR>(&done),
                       reinterpret_cast<DWORD_PTR>(this), CALLBACK_FUNCTION) != MMSYSERR_NOERROR) {
            input_ = nullptr;
            throw std::runtime_error(
                "Could not open the default microphone. Check Windows microphone permissions.");
        }
        if (waveInPrepareHeader(input_, &header_, sizeof(header_)) ||
            waveInAddBuffer(input_, &header_, sizeof(header_)) || waveInStart(input_)) {
            stop(false);
            throw std::runtime_error("Could not start recording");
        }
    }
    std::vector<uint8_t> stop(bool keep) {
        if (!input_)
            return {};
        waveInStop(input_);
        waveInReset(input_);
        auto size = header_.dwBytesRecorded;
        waveInUnprepareHeader(input_, &header_, sizeof(header_));
        waveInClose(input_);
        input_ = nullptr;
        if (!keep)
            return {};
        std::vector<uint8_t> wav;
        auto text = [&](const char *s) { wav.insert(wav.end(), s, s + 4); };
        auto u16 = [&](unsigned n) {
            wav.push_back(n & 255);
            wav.push_back((n >> 8) & 255);
        };
        auto u32 = [&](unsigned n) {
            u16(n & 65535);
            u16(n >> 16);
        };
        text("RIFF");
        u32(36 + size);
        text("WAVE");
        text("fmt ");
        u32(16);
        u16(1);
        u16(1);
        u32(16000);
        u32(32000);
        u16(2);
        u16(16);
        text("data");
        u32(size);
        wav.insert(wav.end(), pcm_.begin(), pcm_.begin() + size);
        return wav;
    }
};
inline void saveMidi(const std::vector<uint8_t> &bytes) {
    wchar_t path[MAX_PATH] = L"Resone.mid";
    OPENFILENAMEW dlg{};
    dlg.lStructSize = sizeof(dlg);
    dlg.lpstrFilter = L"MIDI files\0*.mid\0";
    dlg.lpstrFile = path;
    dlg.nMaxFile = MAX_PATH;
    dlg.lpstrDefExt = L"mid";
    dlg.Flags = OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST;
    if (GetSaveFileNameW(&dlg)) {
        std::ofstream out(std::filesystem::path(path), std::ios::binary);
        out.write(reinterpret_cast<const char *>(bytes.data()), bytes.size());
        if (!out)
            throw std::runtime_error("Could not save MIDI");
    }
}
inline void saveMidi(const std::string &data) { saveMidi(unbase64(data)); }
} // namespace resone
