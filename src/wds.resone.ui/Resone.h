#pragma once
#include "ApiClient.hpp"
#include "AudioEngine.hpp"
#include "IPlug_include_in_plug_hdr.h"
#include "WindowsSupport.hpp"
class Resone final : public iplug::Plugin {
    std::filesystem::path home_;
    resone::Json settings_{{"apiUrl", "ws://127.0.0.1:8078/ws"}}, project_, presets_ = resone::Json::array();
    std::unique_ptr<resone::Dispatcher> dispatcher_;
    std::unique_ptr<resone::ApiClient> api_;
    std::unique_ptr<resone::AudioEngine> audio_;
    resone::VoiceCapture voice_;
    void* editorParent_{};
    bool ready_{};
    uint64_t recordingId_{};
    std::string voiceRequest_;
    mutable std::mutex stateMutex_;
    resone::Json lastError_, connection_;
    std::string state_;
    void emit(resone::Json event);
    void event(resone::Json event);
    void connect();
    void finishVoice(bool keep);

  public:
    explicit Resone(const iplug::InstanceInfo &info);
    ~Resone() override;
    void* OpenWindow(void* parent) override;
    void OnParentWindowResize(int width, int height) override;
    void CloseWindow() override;
    void ProcessBlock(iplug::sample **inputs, iplug::sample **outputs, int nFrames) override;
    void OnReset() override;
    bool OnMessage(int msgTag, int ctrlTag, int size, const void *data) override;
    bool SerializeState(iplug::IByteChunk &chunk) const override;
    int UnserializeState(const iplug::IByteChunk &chunk, int position) override;
};
