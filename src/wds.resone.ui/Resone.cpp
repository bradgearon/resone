#include "Resone.h"
#include "resources/resource.h"
#include "MidiDrag.hpp"
#include "IPlug_include_in_plug_src.h"
using resone::Json;
Resone::Resone(const iplug::InstanceInfo &info)
    : iplug::Plugin(info, iplug::MakeConfig(0, 1)), home_(resone::home()) {
    SetMaxJSStringLength(24 * 1024 * 1024);
    dispatcher_ = std::make_unique<resone::Dispatcher>([this](Json j) {
        try {
            event(std::move(j));
        } catch (const std::exception &e) {
            emit({{"op", "error"}, {"payload", {{"message", e.what()}}}});
        }
    });
    try {
        std::ifstream in(resone::preferences() / "settings.json");
        if (in)
            in >> settings_;
    } catch (...) {
        settings_ = {{"apiUrl", "ws://127.0.0.1:8078/ws"}};
    }
    audio_ =
        std::make_unique<resone::AudioEngine>(home_, [this](Json j) { dispatcher_->post(std::move(j)); });
#ifdef APP_API
    // Standalone song persistence belongs to SongWorkspaceStore. Do not synchronously
    // read a second project copy here: it caused the WebView to render an obsolete
    // native project and then render the saved workspace again during startup.
    project_ = nullptr;
#endif
    mEditorInitFunc = [this] {
        ready_ = false;
        dispatcher_->attach();
        const auto index = resone::webIndex(home_);
        if (index.empty()) {
            LoadHTML("<html><body style=\"font-family:sans-serif;background:#16151a;color:#eee;padding:24px\">"
                     "<h2>Resone UI files were not found.</h2>"
                     "<p>Re-run the Resone build/install so the <code>web</code> folder is staged beside the launcher.</p>"
                     "</body></html>");
        } else {
            const auto path = resone::utf8Path(index);
            LoadFile(path.c_str());
        }
        EnableScroll(false);
    };
}
Resone::~Resone() {
    voice_.stop(false);
    api_.reset();
    audio_.reset();
    dispatcher_.reset();
}
void* Resone::OpenWindow(void* parent) {
    editorParent_ = parent;
    const HWND editorWindow = static_cast<HWND>(parent);

    // iPlug2 creates the Win32 window, but it does not reliably attach the
    // plug-in's icon resource to that HWND. Do it explicitly so the caption
    // and taskbar use Resone instead of the generic application icon.
    resone::setWindowIcon(editorWindow, IDI_ICON1);
#ifdef APP_API
    // In the standalone build the HWND handed to the editor may be a child of
    // the actual top-level frame. The taskbar button belongs to that root HWND.
    // Apply the same icon there as well.
    if (HWND root = GetAncestor(editorWindow, GA_ROOT); root && root != editorWindow)
        resone::setWindowIcon(root, IDI_ICON1);
#endif

    auto view = iplug::WebViewEditorDelegate::OpenWindow(parent);

    // Some hosts/framework paths recreate or subclass the editor HWND while
    // opening the WebView. Reassert the icon after OpenWindow as a safeguard.
    resone::setWindowIcon(editorWindow, IDI_ICON1);
#ifdef APP_API
    if (HWND root = GetAncestor(editorWindow, GA_ROOT); root) {
        if (root != editorWindow)
            resone::setWindowIcon(root, IDI_ICON1);
        resone::trackStandaloneWindowBounds(root);
    }
#endif

    RECT client{};
    if (GetClientRect(editorWindow, &client))
        OnParentWindowResize(client.right, client.bottom);
    return view;
}
void Resone::OnParentWindowResize(int width, int height) {
    if (!editorParent_ || width <= 0 || height <= 0)
        return;
    // APP WM_SIZE and VST3 onSize supply physical pixels. iPlug2's
    // SetWebViewBounds applies the HWND DPI scale, so convert exactly once.
    const float scale = GetScaleForHWND(static_cast<HWND>(editorParent_));
    SetWebViewBounds(0, 0, width / scale, height / scale);

    // This callback is parent/OS -> editor. Do not send a resize request
    // back toward the host here: that API is for the opposite direction and in the
    // standalone APP creates a resize feedback path that can reapply the
    // compile-time default dimensions after restored window bounds. Keep
    // iPlug's editor bookkeeping in sync without requesting another resize.
    SetEditorSize(width, height);
}
void Resone::CloseWindow() {
#ifdef APP_API
    if (editorParent_)
        resone::saveStandaloneWindowBounds(static_cast<HWND>(editorParent_));
#endif
    ready_ = false;
    voice_.stop(false);
    ++recordingId_;
    dispatcher_->detach();
    iplug::WebViewEditorDelegate::CloseWindow();
    editorParent_ = nullptr;
}
void Resone::emit(Json j) {
    if (ready_) {
        auto data = j.dump();
        SendArbitraryMsgFromDelegate(1, static_cast<int>(data.size()), data.data());
    }
}
void Resone::connect() {
    if (!api_) api_ = std::make_unique<resone::ApiClient>(resone::apiLibrary(home_), home_, [this](std::string s) {
        try {
            dispatcher_->post(Json::parse(s));
        } catch (...) {
        }
    });
    api_->connect(settings_.value("apiUrl", "ws://127.0.0.1:8078/ws"));
}
void Resone::event(Json j) {
    auto op = j.value("op", "");
    if (op == "error")
        lastError_ = j;
    if (op == "connected" || op == "disconnected")
        connection_ = j;
    if (op == "instruments")
        presets_ = j.at("payload");
    if (op == "voiceFinished") {
        if (j.at("payload").get<uint64_t>() == recordingId_ && voice_.recording())
            finishVoice(true);
        return;
    }
    if (op == "restore") {
        project_ = j.at("payload");
        emit({{"op", "project"}, {"payload", project_}});
        return;
    }
    if (op == "midi") {
        try {
            resone::saveMidi(j.at("payload").at("data").get<std::string>());
            emit({{"op", "midiSaved"}});
        } catch (const std::exception &e) {
            emit({{"op", "error"}, {"payload", {{"message", e.what()}}}});
        }
        return;
    }
    emit(std::move(j));
}
void Resone::finishVoice(bool keep) {
    auto wav = voice_.stop(keep);
    ++recordingId_;
    emit({{"op", "recording"}, {"payload", false}});
    if (keep && !wav.empty()) {
        if (!api_)
            throw std::runtime_error("Connect to the host first");
        api_->send(Json{
            {"op", "transcribe"}, {"requestId", voiceRequest_}, {"payload", {{"wav", resone::base64(wav)}}}}
                       .dump());
    }
}
bool Resone::OnMessage(int tag, int, int size, const void *data) {
    if (tag != 1 || size < 1 || size > 16 * 1024 * 1024)
        return false;
    try {
        auto j = Json::parse(static_cast<const char *>(data), static_cast<const char *>(data) + size);
        auto op = j.at("op").get<std::string>();
        auto p = j.value("payload", Json::object());
        if (op == "uiDiagnostic") {
            const auto message = p.value("message", std::string("UI diagnostic"));
            const auto stack = p.value("stack", std::string{});
            resone::appendDailyLog("UI", stack.empty() ? message : message + " | " + stack);
            return true;
        }
        if (op == "ready") {
            ready_ = true;
            emit({{"op", "settings"}, {"payload", settings_}});
            emit({{"op", "instruments"}, {"payload", presets_}});
            if (!lastError_.is_null())
                emit(lastError_);
            if (!connection_.is_null())
                emit(connection_);
            {
                std::lock_guard lock(stateMutex_);
                if (!state_.empty())
                    project_ = Json::parse(state_);
            }
            if (!project_.is_null())
                emit({{"op", "project"}, {"payload", project_}});
            if (!api_)
                connect();
        } else if (op == "export") {
            if (!api_) connect();
            auto midi = api_->exportProjectMidi(p);
            resone::saveMidi(midi);
            emit({{"op", "midiSaved"}, {"requestId", j.value("requestId", "")}});
        } else if (op == "render") {
            if (!api_) connect();
            auto notation = p.at("notation").get<std::string>();
            auto midi = api_->renderMidi(notation);
            resone::saveMidi(midi);
            emit({{"op", "midiSaved"}, {"requestId", j.value("requestId", "")}});
        } else if (op == "settings") {
            auto url = p.at("apiUrl").get<std::string>();
            if (url.rfind("ws://", 0) != 0 && url.rfind("wss://", 0) != 0)
                throw std::runtime_error("API URL must start with ws:// or wss://");
            settings_ = {{"apiUrl", url}};
            resone::saveJson(resone::preferences() / "settings.json", settings_);
            connect();
        } else if (op == "dragMidi") {
            if (!api_) connect();
            auto project = p.at("project");
            auto laneId = p.at("laneId").get<std::string>();
            resone::dragMidiBytes(api_->exportLaneMidi(project, laneId), false);
            emit({{"op", "midiDragFinished"}});
        } else if (op == "dragProjectMidi") {
            if (!api_) connect();
            resone::dragMidiBytes(api_->exportProjectMidi(p.at("project")), true);
            emit({{"op", "midiDragFinished"}});
        } else if (op == "play") {
            const bool wrapped = p.is_object() && p.contains("project");
            project_ = wrapped ? p.at("project") : p;
            const double startSeconds = wrapped ? p.value("startSeconds", 0.0) : 0.0;
            const bool paused = wrapped ? p.value("paused", false) : false;
            audio_->play(resone::parseSong(project_), startSeconds, paused);
        } else if (op == "seek") {
            if (p.is_object() && p.contains("project")) project_ = p.at("project");
            if (project_.is_null()) throw std::runtime_error("Nothing is loaded to seek.");
            audio_->play(resone::parseSong(project_), p.value("startSeconds", 0.0), p.value("paused", false));
        } else if (op == "mixer") {
            audio_->mixer(p);
        } else if (op == "pause")
            audio_->pause(p.get<bool>());
        else if (op == "stop") {
            audio_->stop();
            emit({{"op", "transport"}, {"payload", {{"state", "stopped"}, {"seconds", 0}}}});
        } else if (op == "project") {
#ifdef APP_API
            // The browser/workspace store owns durable standalone state. Keep the latest
            // project in memory only; validating + dumping + write-through JSON on every
            // edit was synchronous work on the native editor thread.
            project_ = p;
#else
            resone::parseSong(p);
            project_ = p;
            std::lock_guard lock(stateMutex_);
            state_ = p.dump();
#endif
        } else if (op == "voiceStart") {
            audio_->stop();
            voiceRequest_ = j.value("requestId", "");
            auto id = ++recordingId_;
            voice_.start([this, id] { dispatcher_->post({{"op", "voiceFinished"}, {"payload", id}}); });
            emit({{"op", "recording"}, {"payload", true}});
        } else if (op == "voiceSend")
            finishVoice(true);
        else if (op == "voiceCancel")
            finishVoice(false);
        else {
            if (!api_)
                throw std::runtime_error("Host is disconnected. Check Settings and reconnect.");
            api_->send(j.dump());
        }
    } catch (const std::exception &e) {
        emit({{"op", "error"}, {"payload", {{"message", e.what()}}}});
    }
    return true;
}
void Resone::ProcessBlock(iplug::sample **, iplug::sample **out, int count) {
    audio_->process(out, NOutChansConnected(), count);
}
void Resone::OnReset() {
    audio_->sampleRate(static_cast<int>(GetSampleRate()));
}
bool Resone::SerializeState(iplug::IByteChunk &chunk) const {
    std::lock_guard lock(stateMutex_);
    WDL_String s(state_.c_str());
    return chunk.PutStr(s.Get()) >= 0;
}
int Resone::UnserializeState(const iplug::IByteChunk &chunk, int pos) {
    WDL_String text;
    auto next = chunk.GetStr(text, pos);
    if (next < 0)
        return next;
    try {
        auto j = Json::parse(text.Get());
        resone::parseSong(j);
        {
            std::lock_guard lock(stateMutex_);
            state_ = text.Get();
        }
        dispatcher_->post({{"op", "restore"}, {"payload", j}});
    } catch (...) {
        return -1;
    }
    return next;
}
