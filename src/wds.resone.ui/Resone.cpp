#include "Resone.h"
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
    try {
        std::ifstream in(resone::preferences() / "song.json");
        if (in) {
            in >> project_;
            resone::parseSong(project_);
            state_ = project_.dump();
        }
    } catch (...) {
        project_ = nullptr;
    }
#endif
    mEditorInitFunc = [this] {
        ready_ = false;
        dispatcher_->attach();
        auto index = (home_ / "web/index.html").string();
        LoadFile(index.c_str(), nullptr);
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
    auto view = iplug::WebViewEditorDelegate::OpenWindow(parent);
    RECT client{};
    if (GetClientRect(static_cast<HWND>(parent), &client))
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
    EditorResizeFromUI(width, height, false);
}
void Resone::CloseWindow() {
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
    api_ = std::make_unique<resone::ApiClient>(home_ / "wds.resone.api.dll", [this](std::string s) {
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
            resone::saveMidi(j.at("payload").at("data"));
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
        } else if (op == "settings") {
            auto url = p.at("apiUrl").get<std::string>();
            if (url.rfind("ws://", 0) != 0 && url.rfind("wss://", 0) != 0)
                throw std::runtime_error("API URL must start with ws:// or wss://");
            settings_ = {{"apiUrl", url}};
            resone::saveJson(resone::preferences() / "settings.json", settings_);
            connect();
        } else if (op == "dragMidi") {
            resone::dragLane(p.at("project"),p.at("laneId").get<std::string>());
        } else if (op == "play") {
            project_ = p;
            audio_->play(resone::parseSong(p));
        } else if (op == "pause")
            audio_->pause(p.get<bool>());
        else if (op == "stop") {
            audio_->stop();
            emit({{"op", "transport"}, {"payload", {{"state", "stopped"}, {"seconds", 0}}}});
        } else if (op == "project") {
            resone::parseSong(p);
            project_ = p;
            std::lock_guard lock(stateMutex_);
            state_ = p.dump();
#ifdef APP_API
            resone::saveJson(resone::preferences() / "song.json", p);
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
