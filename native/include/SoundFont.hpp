#pragma once
#include "DynamicLibrary.hpp"
#include "Model.hpp"
namespace resone {
/// FluidSynth is dynamically linked, retaining its LGPL replacement boundary.
class SoundFont {
    DynamicLibrary lib_;
    void *settings_{};
    void *synth_{};
    int bankId_{};
    using NewSettings = void *(*)();
    using Delete = void (*)(void *);
    Delete deleteSettings_{}, deleteSynth_{};
    int (*setNum_)(void *, const char *, double){};
    int (*setInt_)(void *, const char *, int){};
    int (*program_)(void *, int, int, int, int){};
    int (*on_)(void *, int, int, int){};
    int (*off_)(void *, int, int){};
    int (*write_)(void *, int, void *, int, int, void *, int, int){};
    int (*allOff_)(void *, int){};

  public:
    SoundFont(const std::filesystem::path &dll, const std::filesystem::path &sf2, double rate) : lib_(dll) {
        deleteSettings_ = lib_.symbol<Delete>("delete_fluid_settings");
        deleteSynth_ = lib_.symbol<Delete>("delete_fluid_synth");
        setNum_ = lib_.symbol<decltype(setNum_)>("fluid_settings_setnum");
        setInt_ = lib_.symbol<decltype(setInt_)>("fluid_settings_setint");
        settings_ = lib_.symbol<NewSettings>("new_fluid_settings")();
        if (!settings_)
            throw std::runtime_error("FluidSynth settings failed");
        try {
            setNum_(settings_, "synth.sample-rate", rate);
            setNum_(settings_, "synth.gain", .5);
            setNum_(settings_, "synth.reverb.room-size", .5);
            setNum_(settings_, "synth.reverb.damp", .3);
            setNum_(settings_, "synth.reverb.level", .7);
            setInt_(settings_, "synth.polyphony", 256);
            setInt_(settings_, "synth.midi-channels", 32);
            setInt_(settings_, "synth.threadsafe-api", 0);
            synth_ = lib_.symbol<void *(*)(void *)>("new_fluid_synth")(settings_);
            if (!synth_)
                throw std::runtime_error("FluidSynth creation failed");
            bankId_ = lib_.symbol<int (*)(void *, const char *, int)>("fluid_synth_sfload")(
                synth_, sf2.string().c_str(), 1);
            if (bankId_ < 0)
                throw std::runtime_error("Could not load GeneralUser GS SoundFont");
            program_ = lib_.symbol<decltype(program_)>("fluid_synth_program_select");
            on_ = lib_.symbol<decltype(on_)>("fluid_synth_noteon");
            off_ = lib_.symbol<decltype(off_)>("fluid_synth_noteoff");
            write_ = lib_.symbol<decltype(write_)>("fluid_synth_write_float");
            allOff_ = lib_.symbol<decltype(allOff_)>("fluid_synth_all_sounds_off");
        } catch (...) {
            if (synth_)
                deleteSynth_(synth_);
            deleteSettings_(settings_);
            throw;
        }
    }
    ~SoundFont() {
        deleteSynth_(synth_);
        deleteSettings_(settings_);
    }
    Json presets() {
        Json list = Json::array();
        auto *font = lib_.symbol<void *(*)(void *, int)>("fluid_synth_get_sfont_by_id")(synth_, bankId_);
        auto begin = lib_.symbol<void (*)(void *)>("fluid_sfont_iteration_start");
        auto next = lib_.symbol<void *(*)(void *)>("fluid_sfont_iteration_next");
        auto name = lib_.symbol<const char *(*)(void *)>("fluid_preset_get_name");
        auto bank = lib_.symbol<int (*)(void *)>("fluid_preset_get_banknum");
        auto program = lib_.symbol<int (*)(void *)>("fluid_preset_get_num");
        begin(font);
        while (auto *p = next(font))
            list.push_back({{"name", name(p)}, {"bank", bank(p)}, {"program", program(p)}});
        return list;
    }
    void reset() {
        for (int c = 0; c < 32; c++)
            allOff_(synth_, c);
    }
    void program(int channel, int bank, int program) {
        if (program_(synth_, channel, bankId_, bank, program) != 0)
            throw std::runtime_error("SoundFont instrument is unavailable");
    }
    void on(int c, int n, int v) {
        on_(synth_, c, n, v);
    }
    void off(int c, int n) {
        off_(synth_, c, n);
    }
    void render(float *interleaved, int frames) {
        if (write_(synth_, frames, interleaved, 0, 2, interleaved, 1, 2) != 0)
            throw std::runtime_error("SoundFont rendering failed");
    }
};
} // namespace resone
