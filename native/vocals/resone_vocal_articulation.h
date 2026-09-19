#pragma once

#include <vector>

namespace resone::vocals {

// Hard consonants (p/b/t/d/k/g, affricates, and mixed clusters without a
// trustworthy phoneme boundary) must be consumed exactly once. Small duration
// corrections are allowed, but the renderer never loops an attack/release.
std::vector<float> renderConsonantOnce(const std::vector<float>& source,
                                       int outputSamples);

// Unvoiced sustainable consonants (s/f/sh/th...) may be held musically, but
// repeating grains makes the ear hear s-s-s. This uses a strictly monotonic
// one-pass time warp plus mild spectral compensation, so every source sample is
// traversed in order and the consonant attack occurs exactly once.
std::vector<float> stretchNoiseConsonantMonotonic(const std::vector<float>& source,
                                                  int outputSamples,
                                                  int sampleRate);

// /h/ is a breathy onset into the following vowel. It is naturally much quieter
// than a vowel, so a plain stretcher makes it disappear. Keep it single-pass,
// brighten the aspiration, and give it a controlled presence boost without
// turning it into a repeated hiss.
std::vector<float> renderAspirate(const std::vector<float>& source,
                                  int outputSamples,
                                  int sampleRate);

// Preserve y/w/r as a short formant transition into the vowel. Glides are
// voiced, but pitch-correcting them as independent mini-notes produces growly
// or gargled artifacts. Traverse the source once and let the following vowel
// establish the sung F0.
std::vector<float> renderGlideTransition(const std::vector<float>& source,
                                         int outputSamples,
                                         int sampleRate);

} // namespace resone::vocals
