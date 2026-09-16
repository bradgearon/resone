#pragma once
#include "json.hpp"
#include <algorithm>
#include <cmath>
#include <string>
#include <vector>
namespace resone {
using Json = nlohmann::json;
struct Note {
    double start{}, duration{};
    int pitch{}, velocity{96};
};
struct Lane {
    std::string id, name;
    int bank{}, program{};
    double volume{.8};
    bool muted{}, solo{}, drums{};
    std::vector<Note> notes;
};
struct Song {
    double tempo{120};
    double length{};
    std::vector<Lane> lanes;
};
inline Song parseSong(const Json &j) {
    Song s;
    s.tempo = j.at("tempo").get<double>();
    if (!std::isfinite(s.tempo) || s.tempo < 30 || s.tempo > 240)
        throw std::runtime_error("Tempo must be 30–240");
    if (j.at("lanes").size() > 16)
        throw std::runtime_error("At most 16 lanes");
    for (auto &v : j.at("lanes")) {
        Lane l;
        l.id = v.at("id");
        l.name = v.at("name");
        l.program = v.value("program", 0);
        l.bank = v.value("bank", 0);
        l.volume = v.value("volume", .8);
        l.muted = v.value("muted", false);
        l.solo = v.value("solo", false);
        l.drums = v.value("drums", false);
        if (l.program < 0 || l.program > 127 || l.bank < 0 || l.bank > 128 || !std::isfinite(l.volume) ||
            l.volume < 0 || l.volume > 1)
            throw std::runtime_error("Invalid instrument or volume");
        if (v.at("notes").size() > 8192)
            throw std::runtime_error("Too many notes");
        for (auto &n : v.at("notes")) {
            Note a{n.at("start"), n.at("duration"), n.at("pitch"), n.value("velocity", 96)};
            if (!std::isfinite(a.start) || !std::isfinite(a.duration) || a.start < 0 || a.duration <= 0 ||
                a.start + a.duration > 4096 || a.pitch < 0 || a.pitch > 127 || a.velocity < 1 ||
                a.velocity > 127)
                throw std::runtime_error("Invalid note");
            s.length = std::max(s.length, a.start + a.duration);
            l.notes.push_back(a);
        }
        s.lanes.push_back(std::move(l));
    }
    return s;
}
} // namespace resone
