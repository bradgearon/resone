#include "AudioEngine.hpp"
#include "SoundFont.hpp"
#include <chrono>
#include <fstream>
#include <cstdint>
#include <cstring>
#include <iomanip>
#include <sstream>
namespace resone {
namespace {
void nativeAudioLog(const std::filesystem::path &home, const std::string &message) noexcept {
    try {
        const auto directory = home / "logs";
        std::filesystem::create_directories(directory);
        const auto now = std::chrono::system_clock::now();
        const auto tt = std::chrono::system_clock::to_time_t(now);
        std::tm tm{};
#ifdef _WIN32
        localtime_s(&tm, &tt);
#else
        localtime_r(&tt, &tm);
#endif
        std::ostringstream line;
        line << std::put_time(&tm, "%Y-%m-%d %H:%M:%S") << " " << message << "\n";
        std::ofstream(directory / "native-audio.log", std::ios::app) << line.str();
    } catch (...) {}
}
struct PcmWav {
    int sampleRate{};
    int channels{};
    std::vector<float> samples;
    double seconds() const { return sampleRate > 0 && channels > 0 ? samples.size() / double(sampleRate * channels) : 0; }
};
static uint16_t u16(const std::vector<uint8_t> &b, size_t p) {
    if (p + 2 > b.size()) throw std::runtime_error("Truncated WAV");
    return uint16_t(b[p]) | uint16_t(b[p + 1]) << 8;
}
static uint32_t u32(const std::vector<uint8_t> &b, size_t p) {
    if (p + 4 > b.size()) throw std::runtime_error("Truncated WAV");
    return uint32_t(b[p]) | uint32_t(b[p + 1]) << 8 | uint32_t(b[p + 2]) << 16 | uint32_t(b[p + 3]) << 24;
}
static PcmWav loadPcm16Wav(const std::filesystem::path &path) {
    std::ifstream f(path, std::ios::binary);
    if (!f) throw std::runtime_error("Could not open rendered vocal WAV: " + path.string());
    f.seekg(0, std::ios::end); auto length = f.tellg(); f.seekg(0);
    if (length < 44 || length > std::streamoff(512ll * 1024 * 1024)) throw std::runtime_error("Invalid vocal WAV size");
    std::vector<uint8_t> b(static_cast<size_t>(length)); f.read(reinterpret_cast<char*>(b.data()), b.size());
    if (!f || std::memcmp(b.data(), "RIFF", 4) || std::memcmp(b.data() + 8, "WAVE", 4)) throw std::runtime_error("Rendered vocal is not RIFF/WAVE");
    uint16_t format = 0, channels = 0, bits = 0; uint32_t rate = 0; size_t dataPos = 0, dataSize = 0;
    for (size_t p = 12; p + 8 <= b.size();) {
        uint32_t size = u32(b, p + 4); size_t body = p + 8;
        if (body + size > b.size()) throw std::runtime_error("Invalid WAV chunk length");
        if (!std::memcmp(b.data() + p, "fmt ", 4) && size >= 16) {
            format = u16(b, body); channels = u16(b, body + 2); rate = u32(b, body + 4); bits = u16(b, body + 14);
        } else if (!std::memcmp(b.data() + p, "data", 4)) { dataPos = body; dataSize = size; break; }
        p = body + size + (size & 1u);
    }
    if (format != 1 || (channels != 1 && channels != 2) || bits != 16 || rate < 8000 || rate > 192000 || !dataPos || dataSize % (2 * channels))
        throw std::runtime_error("Rendered vocal WAV must be 16-bit PCM mono/stereo");
    PcmWav out; out.sampleRate = int(rate); out.channels = int(channels); out.samples.resize(dataSize / 2);
    for (size_t i = 0; i < out.samples.size(); ++i) {
        int16_t sample = static_cast<int16_t>(u16(b, dataPos + i * 2)); out.samples[i] = sample / 32768.0f;
    }
    return out;
}
static float vocalSample(const PcmWav &wav, int channel, double sourceFrame) {
    if (sourceFrame < 0) return 0;
    size_t frames = wav.samples.size() / wav.channels;
    size_t a = static_cast<size_t>(sourceFrame);
    if (a >= frames) return 0;
    size_t b = std::min(a + 1, frames - 1); float frac = float(sourceFrame - a);
    int c = wav.channels == 1 ? 0 : std::min(channel, wav.channels - 1);
    float x = wav.samples[a * wav.channels + c], y = wav.samples[b * wav.channels + c];
    return x + (y - x) * frac;
}
}
AudioEngine::AudioEngine(std::filesystem::path home, std::function<void(Json)> emit)
    : home_(std::move(home)), emit_(std::move(emit)), worker_([this] { run(); }) {
    for (auto &gain : laneGain_) gain.store(1.0f);
}
AudioEngine::~AudioEngine() {
    quitting_ = true;
    signal();
    worker_.join();
}
void AudioEngine::play(Song song, double startSeconds, bool paused) {
    bool anySolo = std::any_of(song.lanes.begin(), song.lanes.end(), [](const auto &lane) { return lane.solo; });
    for (size_t i = 0; i < laneGain_.size(); ++i) {
        float gain = 0.0f;
        if (i < song.lanes.size()) {
            const auto &lane = song.lanes[i];
            gain = (!lane.muted && (!anySolo || lane.solo)) ? static_cast<float>(lane.volume) : 0.0f;
        }
        laneGain_[i].store(std::clamp(gain, 0.0f, 1.0f), std::memory_order_relaxed);
    }
    std::lock_guard lock(mutex_);
    generation_.fetch_add(1);
    pending_ = PlayRequest{std::move(song), std::max(0.0, startSeconds), paused};
    playing_ = false;
    paused_ = paused;
    consumed_ = static_cast<uint64_t>(std::llround(std::max(0.0, startSeconds) * requestedRate_.load()));
    signal();
}
void AudioEngine::mixer(const Json &state) {
    if (!state.is_object() || !state.contains("lanes") || !state.at("lanes").is_array()) return;
    const auto &lanes = state.at("lanes");
    bool anySolo = false;
    for (const auto &lane : lanes) anySolo = anySolo || lane.value("solo", false);
    for (size_t i = 0; i < laneGain_.size(); ++i) {
        float gain = 0.0f;
        if (i < lanes.size()) {
            const auto &lane = lanes[i];
            const bool audible = !lane.value("muted", false) && (!anySolo || lane.value("solo", false));
            gain = audible ? static_cast<float>(lane.value("volume", 0.8)) : 0.0f;
        }
        laneGain_[i].store(std::clamp(gain, 0.0f, 1.0f), std::memory_order_relaxed);
    }
    signal();
}
void AudioEngine::stop() {
    std::lock_guard lock(mutex_);
    generation_.fetch_add(1);
    pending_.reset();
    playing_ = false;
    consumed_ = 0;
    signal();
}
void AudioEngine::pause(bool value) {
    paused_ = value;
    signal();
}
void AudioEngine::sampleRate(int rate) {
    if (rate < 8000 || rate > 192000)
        return;
    requestedRate_ = rate;
    stop();
}
void AudioEngine::process(double **out, int channels, int count) noexcept {
    const auto gen = generation_.load();
    auto r = read_.load(std::memory_order_relaxed);
    const auto w = write_.load(std::memory_order_acquire);
    while (r < w && ring_[r % Capacity].generation != gen)
        ++r;
    if (!paused_ && !playing_ &&
        (w - r >= static_cast<uint64_t>(startFrames_.load()) || (done_ == gen && w > r)))
        playing_ = true;
    for (int i = 0; i < count; i++) {
        float l = 0, rr = 0;
        if (playing_ && !paused_) {
            if (r < w && ring_[r % Capacity].generation == gen) {
                auto &f = ring_[r++ % Capacity];
                l = f.left;
                rr = f.right;
                consumed_.fetch_add(1, std::memory_order_relaxed);
            } else
                playing_ = false;
        }
        for (int c = 0; c < channels; c++)
            out[c][i] = c == 0 ? l : c == 1 ? rr : 0;
    }
    read_.store(r, std::memory_order_release);
    signal();
}
void AudioEngine::run() {
    try {
#ifdef _WIN32
        const auto vcpkgBin = home_ / "third_party/vcpkg/installed/x64-windows/bin";
        const auto packagedFluid = home_ / "third_party/libfluidsynth";
        const bool developmentTree = std::filesystem::is_regular_file(home_ / "Wds.Resone.sln");
        std::vector<std::filesystem::path> candidates;
        if (developmentTree) {
            // Source-tree runs continue to use vcpkg directly so the development
            // dependency graph remains in one place. Installed builds use the
            // dedicated third_party/libfluidsynth payload instead.
            candidates = {vcpkgBin / "libfluidsynth-3.dll", vcpkgBin / "fluidsynth.dll",
                          packagedFluid / "libfluidsynth-3.dll", packagedFluid / "fluidsynth.dll",
                          home_ / "libfluidsynth-3.dll"};
        } else {
            candidates = {packagedFluid / "libfluidsynth-3.dll", packagedFluid / "fluidsynth.dll",
                          vcpkgBin / "libfluidsynth-3.dll", vcpkgBin / "fluidsynth.dll",
                          home_ / "libfluidsynth-3.dll"};
        }
        std::filesystem::path dll = candidates.front();
        for (const auto &candidate : candidates) {
            if (std::filesystem::is_regular_file(candidate)) { dll = candidate; break; }
        }
        std::vector<std::filesystem::path> dependencySearch{dll.parent_path(), packagedFluid, home_, vcpkgBin};
#else
        auto dll = home_ / "libfluidsynth.so.3";
        std::vector<std::filesystem::path> dependencySearch{dll.parent_path(), home_};
#endif
        int rate = requestedRate_;
        nativeAudioLog(home_, "FluidSynth startup. home=" + home_.string() + "; dll=" + dll.string() +
                                  "; exists=" + (std::filesystem::is_regular_file(dll) ? "true" : "false"));
        auto font = std::make_unique<SoundFont>(dll, home_ / "assets/GeneralUser-GS.sf2", rate, dependencySearch);
        nativeAudioLog(home_, "FluidSynth loaded successfully.");
        emit_({{"op", "instruments"}, {"payload", font->presets()}});
        while (!quitting_) {
            Song song;
            double startSeconds = 0.0;
            bool startPaused = false;
            uint64_t gen;
            for (;;) {
                auto serial = wakeSerial_.load();
                {
                    std::lock_guard lock(mutex_);
                    if (quitting_)
                        return;
                    if (pending_) {
                        auto request = std::move(*pending_);
                        pending_.reset();
                        song = std::move(request.song);
                        startSeconds = request.startSeconds;
                        startPaused = request.paused;
                        gen = generation_;
                        break;
                    }
                }
                wakeSerial_.wait(serial);
            }
            try {
                if (rate != requestedRate_) {
                    rate = requestedRate_;
                    font = std::make_unique<SoundFont>(dll, home_ / "assets/GeneralUser-GS.sf2", rate, dependencySearch);
                }
                font->reset();
                struct Event {
                    int64_t frame;
                    int channel, pitch, velocity;
                    bool on;
                };
                std::vector<Event> events;
                struct VocalTrack { PcmWav wav; int laneIndex{}; };
                std::vector<VocalTrack> vocalTracks;
                int channel = 0;
                for (auto &lane : song.lanes) {
                    int c = channel++;
                    bool renderedVocal = false;
                    if (lane.vocals && !lane.renderedVocalPath.empty()) {
                        try {
                            auto wav = loadPcm16Wav(std::filesystem::path(lane.renderedVocalPath));
                            nativeAudioLog(home_, "Loaded rendered vocal: " + lane.renderedVocalPath + "; seconds=" + std::to_string(wav.seconds()));
                            vocalTracks.push_back({std::move(wav), c});
                            renderedVocal = true;
                        } catch (const std::exception &e) {
                            nativeAudioLog(home_, std::string("Rendered vocal load failed; using MIDI preview: ") + e.what());
                        }
                    }
                    if (renderedVocal) continue;
                    font->program(c, lane.drums ? 128 : lane.bank, lane.program);
                    for (auto &n : lane.notes) {
                        events.push_back({llround(n.start * 60 / song.tempo * rate), c, n.pitch,
                                          std::clamp(n.velocity, 1, 127), true});
                        events.push_back(
                            {llround((n.start + n.duration) * 60 / song.tempo * rate), c, n.pitch, 0, false});
                    }
                }
                std::stable_sort(events.begin(), events.end(), [](auto &a, auto &b) {
                    return a.frame == b.frame ? a.on < b.on : a.frame < b.frame;
                });
                double vocalSeconds = 0;
                for (const auto &v : vocalTracks) vocalSeconds = std::max(vocalSeconds, v.wav.seconds());
                int64_t frame = 0, end = std::max<int64_t>(llround((song.length * 60 / song.tempo + 1.0) * rate), llround((vocalSeconds + .25) * rate));
                size_t next = 0;
                constexpr int block = 1024;
                float audio[block * 2];
                bool announced = false, lastPlaying = false, lastPaused = !startPaused;
                std::array<int, 16> appliedVolume{}; appliedVolume.fill(-1);
                auto applyMixer = [&] {
                    for (int c = 0; c < std::min<int>(channel, static_cast<int>(laneGain_.size())); ++c) {
                        const float gain = laneGain_[c].load(std::memory_order_relaxed);
                        const int midiVolume = std::clamp(static_cast<int>(std::lround(gain * 127.0f)), 0, 127);
                        if (appliedVolume[c] != midiVolume) { font->volume(c, gain); appliedVolume[c] = midiVolume; }
                    }
                };
                auto renderBlock = [&](int n, bool mixVocals) {
                    applyMixer();
                    int offset = 0;
                    while (offset < n) {
                        while (next < events.size() && events[next].frame <= frame + offset) {
                            auto &e = events[next++];
                            if (e.on) font->on(e.channel, e.pitch, e.velocity); else font->off(e.channel, e.pitch);
                        }
                        int part = n - offset;
                        if (next < events.size()) part = std::min(part, int(events[next].frame - frame - offset));
                        if (part <= 0) { offset++; continue; }
                        font->render(audio + offset * 2, part);
                        if (mixVocals && !vocalTracks.empty()) {
                            for (int k = 0; k < part; ++k) {
                                const int64_t outputFrame = frame + offset + k;
                                float vl = 0, vr = 0;
                                for (const auto &v : vocalTracks) {
                                    const double sourceFrame = outputFrame * (double(v.wav.sampleRate) / rate);
                                    const float gain = laneGain_[std::clamp(v.laneIndex, 0, int(laneGain_.size() - 1))].load(std::memory_order_relaxed);
                                    vl += vocalSample(v.wav, 0, sourceFrame) * gain;
                                    vr += vocalSample(v.wav, 1, sourceFrame) * gain;
                                }
                                audio[(offset + k) * 2] += vl;
                                audio[(offset + k) * 2 + 1] += vr;
                            }
                        }
                        offset += part;
                    }
                };
                const int64_t startFrame = std::clamp<int64_t>(llround(startSeconds * rate), 0, end);
                playing_ = false;
                paused_ = startPaused;
                // Fast-forward the synth state so notes that began before the seek point continue naturally.
                while (frame < startFrame && !quitting_ && gen == generation_) {
                    const int n = static_cast<int>(std::min<int64_t>(block, startFrame - frame));
                    renderBlock(n, false);
                    frame += n;
                }
                consumed_ = static_cast<uint64_t>(frame);
                auto began = std::chrono::steady_clock::now();
                auto report = [&] {
                    bool p = playing_, pause = paused_;
                    if (p != lastPlaying || pause != lastPaused) {
                        emit_({{"op", "transport"},
                               {"payload",
                                {{"state", pause ? "paused"
                                           : p   ? "playing"
                                                 : "buffering"},
                                 {"seconds", double(consumed_) / rate}}}});
                        lastPlaying = p;
                        lastPaused = pause;
                    }
                };
                while (frame < end && !quitting_ && gen == generation_) {
                    while (!quitting_ && gen == generation_) {
                        auto serial = wakeSerial_.load();
                        report();
                        if (write_ - read_ < static_cast<uint64_t>(std::max(1024, rate / 8))) break;
                        wakeSerial_.wait(serial);
                    }
                    if (quitting_ || gen != generation_) break;
                    report();
                    const int n = static_cast<int>(std::min<int64_t>(block, end - frame));
                    renderBlock(n, true);
                    auto w = write_.load(std::memory_order_relaxed);
                    for (int i = 0; i < n; i++) ring_[(w + i) % Capacity] = {audio[i * 2], audio[i * 2 + 1], gen};
                    write_.store(w + n, std::memory_order_release);
                    frame += n;
                    if (!announced) {
                        double seconds = std::chrono::duration<double>(std::chrono::steady_clock::now() - began).count();
                        startFrames_ = int(rate * (seconds < .02 ? .05 : .25));
                        announced = true;
                        emit_({{"op", "transport"}, {"payload", {{"state", startPaused ? "paused" : "buffering"}, {"seconds", double(consumed_) / rate}}}});
                    }
                }
                if (gen != generation_)
                    continue;
                done_ = gen;
                while (!quitting_ && gen == generation_) {
                    auto serial = wakeSerial_.load();
                    report();
                    if (read_ == write_)
                        break;
                    wakeSerial_.wait(serial);
                }
                if (gen == generation_ && !quitting_)
                    emit_({{"op", "transport"},
                           {"payload", {{"state", "stopped"}, {"seconds", double(consumed_) / rate}}}});
            } catch (const std::exception &e) {
                nativeAudioLog(home_, std::string("FluidSynth/render error: ") + e.what());
                playing_ = false;
                generation_.fetch_add(1);
                emit_({{"op", "error"}, {"payload", {{"message", e.what()}}}});
            }
        }
    } catch (const std::exception &e) {
        nativeAudioLog(home_, std::string("FluidSynth startup failed: ") + e.what());
        emit_({{"op", "error"}, {"payload", {{"message", e.what()}}}});
    }
}
} // namespace resone
