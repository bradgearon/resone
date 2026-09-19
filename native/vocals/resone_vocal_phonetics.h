#pragma once

#include <string>
#include <vector>

namespace resone::vocals {

enum class ConsonantBehavior {
    None,
    Transient,
    SustainableNoise,
    SustainableVoiced,
    Glide,
    Aspirate,
    Mixed
};

struct TextConsonantUnit {
    std::string text;
    ConsonantBehavior behavior = ConsonantBehavior::None;
    double sustainWeight = 0.0;
};

struct TextConsonantCluster {
    std::string text;
    ConsonantBehavior behavior = ConsonantBehavior::None;
    double sustainWeight = 0.0;
    // Ordered spelling runs are retained for diagnostics and future forced alignment.
    // The acoustic cluster remains one source gesture unless real phoneme timestamps
    // are available, preventing duplicate consonant attacks.
    std::vector<TextConsonantUnit> units;
};

struct TextVowelNucleus {
    std::string text;
    // English singers normally hold the first element of a diphthong and move
    // into the off-glide only near the end.  Preserve that tail instead of
    // stretching it across the entire note.
    bool hasOffglide = false;
    // In American-style rhotic singing, a post-vocalic r is primarily a vowel
    // colour/formant transition.  Keeping it in the nucleus avoids a spoken
    // standalone "r" pasted between sung vowels.
    bool rhotic = false;
};

struct WordPhoneticPlan {
    int vowelNuclei = 1;
    std::vector<TextVowelNucleus> nuclei;
    // Always vowelNuclei + 1 entries: before the first vowel, between each
    // pair of vowels, and after the last vowel.
    std::vector<TextConsonantCluster> consonants;
};

WordPhoneticPlan planWordPhonetics(const std::string& word);

} // namespace resone::vocals
