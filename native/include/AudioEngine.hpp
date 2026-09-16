#pragma once
#include "Model.hpp"
#include <atomic>
#include <condition_variable>
#include <filesystem>
#include <functional>
#include <mutex>
#include <optional>
#include <thread>
namespace resone {
class AudioEngine {
    struct Frame {
        float left{}, right{};
        uint64_t generation{};
    };
    static constexpr size_t Capacity = 131072;
    std::vector<Frame> ring_ = std::vector<Frame>(Capacity);
    alignas(64) std::atomic<uint64_t> read_{}, write_{};
    std::atomic<uint64_t> generation_{1}, done_{};
    std::atomic<bool> playing_{}, paused_{}, quitting_{};
    std::atomic<int> requestedRate_{48000}, startFrames_{2400};
    std::atomic<uint64_t> consumed_{};
    std::mutex mutex_;
    std::atomic<uint64_t> wakeSerial_{};
    std::optional<Song> pending_;
    std::filesystem::path home_;
    std::function<void(Json)> emit_;
    std::thread worker_;
    void signal() noexcept {
        wakeSerial_.fetch_add(1, std::memory_order_release);
        wakeSerial_.notify_one();
    }
    void run();

  public:
    AudioEngine(std::filesystem::path home, std::function<void(Json)> emit);
    ~AudioEngine();
    void play(Song song);
    void stop();
    void pause(bool value);
    void sampleRate(int rate);
    // Called by the host audio callback. No locks, allocations, file I/O or network calls.
    void process(double **outputs, int channels, int count) noexcept;
    uint64_t position() const {
        return consumed_.load();
    }
};
} // namespace resone
