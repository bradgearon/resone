#include "resone_vocal_phonetics.h"

#include <algorithm>
#include <cctype>

namespace resone::vocals {
namespace {

bool asciiLetter(unsigned char c) { return c < 0x80 && std::isalpha(c); }
char lowerAscii(unsigned char c) { return static_cast<char>(std::tolower(c)); }

bool baseVowel(char c) {
    c = lowerAscii(static_cast<unsigned char>(c));
    return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
}

bool silentFinalE(const std::string& letters, size_t i) {
    return letters[i] == 'e' && i + 1 == letters.size() && letters.size() > 2;
}

TextConsonantUnit classifyToken(std::string token) {
    TextConsonantUnit out;
    out.text = std::move(token);
    if (out.text.empty()) return out;

    const std::string t = out.text;
    if (t == "sh") {
        out.behavior = ConsonantBehavior::SustainableNoise;
        out.sustainWeight = 0.36;
        return out;
    }
    if (t == "th") {
        // English spelling is ambiguous (think/this). Acoustic voicing in the
        // renderer gets the final say, so keep the text-side expectation noisy.
        out.behavior = ConsonantBehavior::SustainableNoise;
        out.sustainWeight = 0.30;
        return out;
    }
    if (t == "ng" || t == "zh") {
        out.behavior = ConsonantBehavior::SustainableVoiced;
        out.sustainWeight = 0.38;
        return out;
    }
    if (t == "ch") {
        out.behavior = ConsonantBehavior::Transient;
        return out;
    }

    char c = t.front();
    if (c == 'h') {
        // /h/ is not just another fricative.  It is a breathy transition into
        // the following vowel and needs its own gain/envelope treatment.
        out.behavior = ConsonantBehavior::Aspirate;
        out.sustainWeight = 0.18;
    } else if (c == 'y' || c == 'w' || c == 'r') {
        // Initial y/w/r are voiced glides.  Treating y in "you" as a stop was
        // the reason the word could appear to vanish before its vowel arrived.
        out.behavior = ConsonantBehavior::Glide;
        out.sustainWeight = 0.22;
    } else if (c == 's' || c == 'f' || c == 'x') {
        out.behavior = ConsonantBehavior::SustainableNoise;
        out.sustainWeight = 0.34;
    } else if (c == 'z' || c == 'v' || c == 'm' || c == 'n' || c == 'l') {
        out.behavior = ConsonantBehavior::SustainableVoiced;
        out.sustainWeight = 0.40;
    } else {
        // p/b/t/d/k/g plus c/q/j and other stop/affricate-like spellings.
        out.behavior = ConsonantBehavior::Transient;
    }
    return out;
}

std::vector<TextConsonantUnit> tokenizeConsonants(const std::string& text) {
    std::vector<TextConsonantUnit> tokens;
    for (size_t i = 0; i < text.size();) {
        std::string token(1, text[i]);
        if (i + 1 < text.size()) {
            std::string pair{text[i], text[i + 1]};
            if (pair == "sh" || pair == "th" || pair == "ch" || pair == "ng" || pair == "zh") {
                token = pair;
                i += 2;
            } else {
                ++i;
            }
        } else {
            ++i;
        }
        tokens.push_back(classifyToken(std::move(token)));
    }

    // Collapse only truly equivalent adjacent behavior. A double p becomes one
    // stop gesture; a long "ss" becomes one sustainable-noise gesture. This
    // prevents spelling from inventing repeated consonant attacks.
    std::vector<TextConsonantUnit> runs;
    for (auto& token : tokens) {
        if (!runs.empty() && runs.back().behavior == token.behavior) {
            runs.back().text += token.text;
            runs.back().sustainWeight = std::max(runs.back().sustainWeight, token.sustainWeight);
        } else {
            runs.push_back(std::move(token));
        }
    }
    return runs;
}

TextConsonantCluster classify(std::string text) {
    TextConsonantCluster out;
    out.text = std::move(text);
    if (out.text.empty()) return out;
    out.units = tokenizeConsonants(out.text);

    bool anyTransient = false, anyNoise = false, anyVoiced = false, anyGlide = false, anyAspirate = false;
    for (const auto& unit : out.units) {
        anyTransient |= unit.behavior == ConsonantBehavior::Transient;
        anyNoise |= unit.behavior == ConsonantBehavior::SustainableNoise;
        anyVoiced |= unit.behavior == ConsonantBehavior::SustainableVoiced;
        anyGlide |= unit.behavior == ConsonantBehavior::Glide;
        anyAspirate |= unit.behavior == ConsonantBehavior::Aspirate;
        out.sustainWeight = std::max(out.sustainWeight, unit.sustainWeight);
    }

    const int kinds = (anyTransient ? 1 : 0) + (anyNoise ? 1 : 0) + (anyVoiced ? 1 : 0) +
                      (anyGlide ? 1 : 0) + (anyAspirate ? 1 : 0);
    if (kinds > 1) out.behavior = ConsonantBehavior::Mixed;
    else if (anyGlide) out.behavior = ConsonantBehavior::Glide;
    else if (anyAspirate) out.behavior = ConsonantBehavior::Aspirate;
    else if (anyVoiced) out.behavior = ConsonantBehavior::SustainableVoiced;
    else if (anyNoise) out.behavior = ConsonantBehavior::SustainableNoise;
    else if (anyTransient) out.behavior = ConsonantBehavior::Transient;
    return out;
}

bool startsVowelAt(const std::string& letters, size_t i) {
    if (i >= letters.size() || silentFinalE(letters, i)) return false;
    if (baseVowel(letters[i])) return true;
    // A non-initial y can be syllabic (happy, pretty). Initial y is /j/.
    return letters[i] == 'y' && i > 0;
}

bool postVocalicRhotic(const std::string& letters, size_t i, bool inVowel) {
    if (!inVowel || letters[i] != 'r') return false;
    if (i + 1 >= letters.size()) return true;
    if (silentFinalE(letters, i + 1)) return true;
    // r before another written consonant colours the preceding nucleus in
    // American English (birthday, hard, world) more than it behaves like a
    // standalone consonant event.
    return !startsVowelAt(letters, i + 1);
}

} // namespace

WordPhoneticPlan planWordPhonetics(const std::string& word) {
    std::string letters;
    letters.reserve(word.size());
    for (unsigned char c : word) if (asciiLetter(c)) letters.push_back(lowerAscii(c));

    WordPhoneticPlan plan;
    if (letters.empty()) {
        plan.vowelNuclei = 1;
        plan.nuclei = {TextVowelNucleus{}};
        plan.consonants = {TextConsonantCluster{}, TextConsonantCluster{}};
        return plan;
    }

    std::vector<std::string> clusters;
    std::string consonants;
    std::vector<TextVowelNucleus> nuclei;
    bool inVowel = false;

    for (size_t i = 0; i < letters.size(); ++i) {
        if (silentFinalE(letters, i)) {
            if (inVowel) inVowel = false;
            consonants.push_back(letters[i]);
            continue;
        }

        // Initial y/w are glides, not vowel nuclei. A y following an already
        // active vowel is an off-glide (day, birthday) and belongs to that same
        // sung nucleus rather than becoming an extra consonant attack.
        bool yOffglide = letters[i] == 'y' && inVowel;
        bool vowel = startsVowelAt(letters, i) || yOffglide;
        bool rhotic = postVocalicRhotic(letters, i, inVowel);

        if (rhotic) {
            nuclei.back().text.push_back('r');
            nuclei.back().rhotic = true;
            continue;
        }

        if (vowel) {
            if (!inVowel) {
                clusters.push_back(consonants);
                consonants.clear();
                nuclei.push_back({});
            }
            inVowel = true;
            nuclei.back().text.push_back(letters[i]);
            if (letters[i] == 'y' && nuclei.back().text.size() > 1) nuclei.back().hasOffglide = true;
        } else {
            if (inVowel) inVowel = false;
            consonants.push_back(letters[i]);
        }
    }

    if (nuclei.empty()) {
        plan.vowelNuclei = 1;
        plan.nuclei = {TextVowelNucleus{}};
        auto whole = classify(letters);
        if (whole.behavior == ConsonantBehavior::Transient || whole.behavior == ConsonantBehavior::Mixed) {
            whole.behavior = ConsonantBehavior::SustainableNoise;
            whole.sustainWeight = std::max(whole.sustainWeight, 0.18);
        }
        plan.consonants = {whole, TextConsonantCluster{}};
        return plan;
    }

    clusters.push_back(consonants);
    while (clusters.size() < nuclei.size() + 1) clusters.push_back({});
    if (clusters.size() > nuclei.size() + 1) clusters.resize(nuclei.size() + 1);

    plan.vowelNuclei = static_cast<int>(nuclei.size());
    plan.nuclei = std::move(nuclei);
    plan.consonants.reserve(clusters.size());
    for (auto& cluster : clusters) plan.consonants.push_back(classify(std::move(cluster)));
    return plan;
}

} // namespace resone::vocals
