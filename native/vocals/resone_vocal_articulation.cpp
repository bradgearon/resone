#include "resone_vocal_articulation.h"

#include <algorithm>
#include <cmath>
#include <cstddef>

namespace resone::vocals {
namespace {

std::vector<float> resampleLinearOnce(const std::vector<float>& source, int outputSamples) {
    if (outputSamples <= 0) return {};
    if (source.empty()) return std::vector<float>(outputSamples, 0.0f);
    if (source.size() == 1) return std::vector<float>(outputSamples, source.front());
    if (outputSamples == static_cast<int>(source.size())) return source;

    std::vector<float> output(outputSamples);
    const double scale = (source.size() - 1.0) / std::max(1, outputSamples - 1);
    for (int i = 0; i < outputSamples; ++i) {
        const double at = i * scale;
        const auto a = static_cast<std::size_t>(std::floor(at));
        const auto b = std::min(a + 1, source.size() - 1);
        const float t = static_cast<float>(at - a);
        output[i] = source[a] + (source[b] - source[a]) * t;
    }
    return output;
}

float rms(const std::vector<float>& x) {
    if (x.empty()) return 0.0f;
    double e = 0.0;
    for (float v : x) e += static_cast<double>(v) * v;
    return static_cast<float>(std::sqrt(e / x.size()));
}

void mildStretchBrightnessCompensation(std::vector<float>& x, double stretchRatio) {
    if (x.size() < 2 || stretchRatio <= 1.01) return;
    // Slowing a noise consonant by resampling lowers its spectral centroid. Mix a
    // small first-difference component back in. Keep this deliberately mild so
    // /s/ and /f/ do not become brittle.
    const float amount = static_cast<float>(std::clamp((stretchRatio - 1.0) * 0.42, 0.0, 0.24));
    float previous = x.front();
    for (std::size_t i = 1; i < x.size(); ++i) {
        const float current = x[i];
        const float high = current - previous;
        x[i] = current + amount * high;
        previous = current;
    }
}

void softLimit(std::vector<float>& x, float ceiling = 0.96f) {
    for (float& v : x) {
        if (std::abs(v) > ceiling) v = std::tanh(v / ceiling) * ceiling;
    }
}

} // namespace

std::vector<float> renderConsonantOnce(const std::vector<float>& source,
                                       int outputSamples) {
    return resampleLinearOnce(source, outputSamples);
}

std::vector<float> stretchNoiseConsonantMonotonic(const std::vector<float>& source,
                                                  int outputSamples,
                                                  int sampleRate) {
    (void)sampleRate;
    if (outputSamples <= 0) return {};
    if (source.empty()) return std::vector<float>(outputSamples, 0.0f);

    // A one-pass warp is intentionally preferred to grain repetition here. The
    // source attack, body and release are each traversed exactly once, so a held
    // consonant cannot become s-s-s/f-f-f. Musical duration is capped elsewhere,
    // keeping the spectral change small enough for this to remain natural.
    auto output = resampleLinearOnce(source, outputSamples);
    const double ratio = outputSamples / static_cast<double>(std::max<std::size_t>(1, source.size()));
    mildStretchBrightnessCompensation(output, ratio);
    softLimit(output);
    return output;
}

std::vector<float> renderGlideTransition(const std::vector<float>& source,
                                         int outputSamples,
                                         int sampleRate) {
    if (outputSamples <= 0) return {};
    if (source.empty()) return std::vector<float>(outputSamples, 0.0f);

    // A glide is articulation, not a separate sung note.  Preserve its natural
    // spectral movement in one pass; stretching is deliberately small and is
    // capped by the duration allocator.  This avoids PSOLA phase/formant
    // artifacts on /r/ while keeping /j/ (y) and /w/ audible.
    auto output = resampleLinearOnce(source, outputSamples);

    // Remove only sub-rumble that can become exaggerated by a short resample.
    // 45 Hz is below the practical singing range and does not thin the glide.
    if (output.size() > 1 && sampleRate > 0) {
        constexpr double pi = 3.14159265358979323846;
        const double rc = 1.0 / (2.0 * pi * 45.0);
        const double dt = 1.0 / sampleRate;
        const float alpha = static_cast<float>(rc / (rc + dt));
        float prevIn = output.front();
        float prevOut = 0.0f;
        for (std::size_t i = 1; i < output.size(); ++i) {
            const float in = output[i];
            const float hp = alpha * (prevOut + in - prevIn);
            output[i] = hp;
            prevIn = in;
            prevOut = hp;
        }
    }

    // A tiny presence lift keeps initial /j/ from vanishing under the vowel,
    // without turning /r/ into a nasal/gargled effect.
    for (float& v : output) v *= 1.10f;
    const int attack = std::min<int>(output.size(), std::max(1, static_cast<int>(0.0015 * sampleRate)));
    for (int i = 0; i < attack; ++i) {
        const float u = static_cast<float>(i + 1) / static_cast<float>(attack + 1);
        output[i] *= 0.78f + 0.22f * u;
    }
    softLimit(output);
    return output;
}

std::vector<float> renderAspirate(const std::vector<float>& source,
                                  int outputSamples,
                                  int sampleRate) {
    if (outputSamples <= 0) return {};
    if (source.empty()) return std::vector<float>(outputSamples, 0.0f);

    auto output = resampleLinearOnce(source, outputSamples);
    const float before = std::max(1e-6f, rms(output));

    // Preserve the breath itself but bring forward the noisy/air component. /h/
    // in a spoken reference can be 10+ dB below the following vowel; without
    // this treatment it disappears after the phrase mix.
    float previous = output.front();
    const float hp = sampleRate >= 32000 ? 0.72f : 0.62f;
    for (std::size_t i = 1; i < output.size(); ++i) {
        const float current = output[i];
        const float air = current - previous;
        output[i] = current * 1.10f + air * hp;
        previous = current;
    }

    const float after = std::max(1e-6f, rms(output));
    // About +5 dB of presence, but normalize relative to the original aspirate so
    // an already-loud /h/ is not blown up.
    const float wanted = before * 2.20f;
    const float gain = std::clamp(wanted / after, 0.90f, 2.80f);
    for (float& v : output) v *= gain;

    // Fast natural onset, slightly fuller release into the vowel. Do not fade h
    // to zero at its end; the following crossfade should feel like one breath
    // becoming a sung vowel.
    const int attack = std::min<int>(output.size(), std::max(1, static_cast<int>(0.004 * sampleRate)));
    for (int i = 0; i < attack; ++i) {
        const float u = static_cast<float>(i + 1) / static_cast<float>(attack + 1);
        output[i] *= 0.55f + 0.45f * u;
    }
    softLimit(output);
    return output;
}

} // namespace resone::vocals
