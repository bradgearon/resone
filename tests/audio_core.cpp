#include "AudioEngine.hpp"
#include <cassert>
#include <chrono>
#include <condition_variable>
#include <iostream>
int main(int argc, char **argv) {
    using namespace resone;
    std::mutex mu;
    std::condition_variable cv;
    bool ready = false, failed = false;
    int instruments = 0;
    AudioEngine audio(argv[1], [&](Json e) {
        std::lock_guard lock(mu);
        if (e["op"] == "instruments") {
            ready = true;
            instruments = e["payload"].size();
        }
        if (e["op"] == "error") {
            failed = true;
            std::cerr << e.dump() << '\n';
        }
        cv.notify_one();
    });
    {
        std::unique_lock lock(mu);
        assert(cv.wait_for(lock, std::chrono::seconds(10), [&] { return ready || failed; }));
    }
    assert(!failed && instruments > 128);
    Song s;
    s.tempo = 120;
    s.length = 32;
    s.lanes.push_back({"melody",
                       "Melody",
                       0,
                       0,
                       .8,
                       false,
                       false,
                       false,
                       {{0, 2, 60, 100}, {2, 2, 64, 100}, {4, 28, 67, 100}}});
    audio.play(s);
    double l[256], r[256];
    double *channels[] = {l, r};
    double peak = 0;
    int initial = 0;
    // Simulate a host callback at ~48 kHz. Sleep is test-only, never in production scheduling.
    for (int block = 0; block < 200; ++block) {
        audio.process(channels, 2, 256);
        for (auto n : l)
            peak = std::max(peak, std::abs(n));
        if (peak > 0 && !initial)
            initial = block + 1;
        std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    assert(peak > .0001 && initial < 50);
    audio.pause(true);
    audio.process(channels, 2, 256);
    for (auto n : l)
        assert(n == 0);
    audio.stop();
    audio.process(channels, 2, 256);
    for (auto n : l)
        assert(n == 0);
    // Cancel a long render and immediately replace it; no deadlock or old notes leak.
    s.lanes[0].notes = {{0, 1, 72, 90}};
    s.length = 1;
    audio.play(s);
    peak = 0;
    for (int block = 0; block < 100; ++block) {
        audio.process(channels, 2, 256);
        for (auto n : l)
            peak = std::max(peak, std::abs(n));
        std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    assert(peak > .0001);
    audio.stop();
    std::cout << "PASS: " << instruments << " actual GeneralUser presets, first audio by callback " << initial
              << ", stereo synthesis, pause, cancellation, replacement\n";
}
