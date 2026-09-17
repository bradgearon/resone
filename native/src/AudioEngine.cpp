#include "AudioEngine.hpp"
#include "SoundFont.hpp"
#include <chrono>
#include <fstream>
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
}
AudioEngine::AudioEngine(std::filesystem::path home, std::function<void(Json)> emit)
    : home_(std::move(home)), emit_(std::move(emit)), worker_([this] { run(); }) {}
AudioEngine::~AudioEngine() {
    quitting_ = true;
    signal();
    worker_.join();
}
void AudioEngine::play(Song song) {
    std::lock_guard lock(mutex_);
    generation_.fetch_add(1);
    pending_ = std::move(song);
    playing_ = false;
    paused_ = false;
    consumed_ = 0;
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
        const bool developmentTree = std::filesystem::is_regular_file(home_ / "Wds.Resone.sln");
        std::vector<std::filesystem::path> candidates;
        if (developmentTree) {
            // A source-tree run should use the vcpkg FluidSynth build together
            // with the dependency DLLs that were installed beside it. A copied
            // root-level libfluidsynth can exist while its transitive DLLs do not.
            candidates = {vcpkgBin / "libfluidsynth-3.dll", vcpkgBin / "fluidsynth.dll",
                          home_ / "libfluidsynth-3.dll"};
        } else {
            candidates = {home_ / "libfluidsynth-3.dll", vcpkgBin / "libfluidsynth-3.dll",
                          vcpkgBin / "fluidsynth.dll"};
        }
        std::filesystem::path dll = candidates.front();
        for (const auto &candidate : candidates) {
            if (std::filesystem::is_regular_file(candidate)) { dll = candidate; break; }
        }
        std::vector<std::filesystem::path> dependencySearch{dll.parent_path(), home_, vcpkgBin};
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
            uint64_t gen;
            for (;;) {
                auto serial = wakeSerial_.load();
                {
                    std::lock_guard lock(mutex_);
                    if (quitting_)
                        return;
                    if (pending_) {
                        song = std::move(*pending_);
                        pending_.reset();
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
                bool solo = std::any_of(song.lanes.begin(), song.lanes.end(), [](auto &l) { return l.solo; });
                int channel = 0;
                for (auto &lane : song.lanes) {
                    int c = channel++;
                    if (lane.volume == 0 || lane.muted || (solo && !lane.solo))
                        continue;
                    font->program(c, lane.drums ? 128 : lane.bank, lane.program);
                    for (auto &n : lane.notes) {
                        events.push_back({llround(n.start * 60 / song.tempo * rate), c, n.pitch,
                                          std::clamp(int(n.velocity * lane.volume), 1, 127), true});
                        events.push_back(
                            {llround((n.start + n.duration) * 60 / song.tempo * rate), c, n.pitch, 0, false});
                    }
                }
                std::stable_sort(events.begin(), events.end(), [](auto &a, auto &b) {
                    return a.frame == b.frame ? a.on < b.on : a.frame < b.frame;
                });
                int64_t frame = 0, end = llround((song.length * 60 / song.tempo + 1.0) * rate);
                size_t next = 0;
                constexpr int block = 1024;
                float audio[block * 2];
                bool announced = false, lastPlaying = false, lastPaused = false;
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
                        if (write_ - read_ < static_cast<uint64_t>(rate / 2))
                            break;
                        wakeSerial_.wait(serial);
                    }
                    if (quitting_ || gen != generation_)
                        break;
                    report();
                    int n = static_cast<int>(std::min<int64_t>(block, end - frame)), offset = 0;
                    while (offset < n) {
                        while (next < events.size() && events[next].frame <= frame + offset) {
                            auto &e = events[next++];
                            if (e.on)
                                font->on(e.channel, e.pitch, e.velocity);
                            else
                                font->off(e.channel, e.pitch);
                        }
                        int part = n - offset;
                        if (next < events.size())
                            part = std::min(part, int(events[next].frame - frame - offset));
                        font->render(audio + offset * 2, part);
                        offset += part;
                    }
                    auto w = write_.load(std::memory_order_relaxed);
                    for (int i = 0; i < n; i++)
                        ring_[(w + i) % Capacity] = {audio[i * 2], audio[i * 2 + 1], gen};
                    write_.store(w + n, std::memory_order_release);
                    frame += n;
                    if (!announced) {
                        double seconds =
                            std::chrono::duration<double>(std::chrono::steady_clock::now() - began).count();
                        startFrames_ = int(rate * (seconds < .02 ? .05 : .25));
                        announced = true;
                        emit_({{"op", "transport"}, {"payload", {{"state", "buffering"}, {"seconds", 0}}}});
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
