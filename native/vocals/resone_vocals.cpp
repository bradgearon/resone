#include "resone_vocals.h"
#include "resone_vocal_phonetics.h"
#include "resone_vocal_articulation.h"
#include <algorithm>
#include <array>
#include <cctype>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <fstream>
#include <limits>
#include <numeric>
#include <stdexcept>
#include <string>
#include <tuple>
#include <vector>

namespace {

constexpr double kPi = 3.14159265358979323846;

struct Audio {
    int sampleRate = 0;
    std::vector<float> samples; // mono
};

struct MidiNote {
    int note = 60;
    double start = 0.0;
    double end = 0.5;
    int velocity = 100;
};

struct PitchFrame {
    int sample = 0;
    float f0 = 0.0f;
    float rms = 0.0f;
    bool voiced = false;
};

struct Region {
    int start = 0;
    int end = 0;
};

static void setError(char* buf, int len, const std::string& s) {
    if (!buf || len <= 0) return;
    const int n = std::min<int>(len - 1, static_cast<int>(s.size()));
    std::memcpy(buf, s.data(), n);
    buf[n] = '\0';
}

static uint16_t readU16(std::istream& s) {
    uint8_t b[2]{}; s.read(reinterpret_cast<char*>(b), 2);
    return static_cast<uint16_t>(b[0] | (b[1] << 8));
}
static uint32_t readU32(std::istream& s) {
    uint8_t b[4]{}; s.read(reinterpret_cast<char*>(b), 4);
    return static_cast<uint32_t>(b[0] | (b[1]<<8) | (b[2]<<16) | (b[3]<<24));
}
static uint16_t readBE16(std::istream& s) {
    uint8_t b[2]{}; s.read(reinterpret_cast<char*>(b), 2);
    return static_cast<uint16_t>((b[0]<<8) | b[1]);
}
static uint32_t readBE32(std::istream& s) {
    uint8_t b[4]{}; s.read(reinterpret_cast<char*>(b), 4);
    return static_cast<uint32_t>((b[0]<<24) | (b[1]<<16) | (b[2]<<8) | b[3]);
}

static Audio readWav(const std::string& path) {
    std::ifstream f(path, std::ios::binary);
    if (!f) throw std::runtime_error("Could not open input WAV: " + path);
    char riff[4]; f.read(riff,4);
    if (std::strncmp(riff,"RIFF",4)!=0) throw std::runtime_error("Input is not RIFF WAV");
    (void)readU32(f);
    char wave[4]; f.read(wave,4);
    if (std::strncmp(wave,"WAVE",4)!=0) throw std::runtime_error("Input is not WAVE");

    uint16_t audioFormat=0, channels=0, bits=0;
    uint32_t sampleRate=0;
    std::vector<uint8_t> data;
    while (f && (!sampleRate || data.empty())) {
        char id[4]; f.read(id,4); if (!f) break;
        uint32_t size = readU32(f);
        std::string chunk(id,4);
        if (chunk == "fmt ") {
            audioFormat = readU16(f);
            channels = readU16(f);
            sampleRate = readU32(f);
            (void)readU32(f); (void)readU16(f);
            bits = readU16(f);
            if (size > 16) f.seekg(size - 16, std::ios::cur);
        } else if (chunk == "data") {
            data.resize(size); f.read(reinterpret_cast<char*>(data.data()), size);
        } else {
            f.seekg(size, std::ios::cur);
        }
        if (size & 1) f.seekg(1, std::ios::cur);
    }
    if (!sampleRate || data.empty()) throw std::runtime_error("WAV missing fmt/data chunks");
    if (audioFormat != 1 || bits != 16) throw std::runtime_error("Prototype expects 16-bit PCM WAV");
    if (channels < 1 || channels > 2) throw std::runtime_error("Prototype expects mono or stereo WAV");

    size_t frames = data.size() / (channels * 2);
    Audio out; out.sampleRate = static_cast<int>(sampleRate); out.samples.resize(frames);
    auto* p = reinterpret_cast<const int16_t*>(data.data());
    for (size_t i=0;i<frames;i++) {
        float v=0;
        for (int c=0;c<channels;c++) v += p[i*channels+c] / 32768.0f;
        out.samples[i] = v / channels;
    }
    return out;
}

static void writeWav(const std::string& path, const Audio& a) {
    std::ofstream f(path, std::ios::binary);
    if (!f) throw std::runtime_error("Could not create output WAV: " + path);
    const uint16_t channels=1, bits=16;
    const uint32_t dataBytes = static_cast<uint32_t>(a.samples.size()*2);
    const uint32_t riffSize = 36 + dataBytes;
    f.write("RIFF",4); f.write(reinterpret_cast<const char*>(&riffSize),4); f.write("WAVE",4);
    f.write("fmt ",4); uint32_t fmtSize=16; f.write(reinterpret_cast<const char*>(&fmtSize),4);
    uint16_t format=1; f.write(reinterpret_cast<const char*>(&format),2); f.write(reinterpret_cast<const char*>(&channels),2);
    uint32_t sr=a.sampleRate; f.write(reinterpret_cast<const char*>(&sr),4);
    uint32_t byteRate=sr*channels*bits/8; f.write(reinterpret_cast<const char*>(&byteRate),4);
    uint16_t blockAlign=channels*bits/8; f.write(reinterpret_cast<const char*>(&blockAlign),2);
    f.write(reinterpret_cast<const char*>(&bits),2);
    f.write("data",4); f.write(reinterpret_cast<const char*>(&dataBytes),4);
    for (float x : a.samples) {
        x = std::max(-1.0f,std::min(1.0f,x));
        int16_t s = static_cast<int16_t>(std::lrint(x*32767.0f));
        f.write(reinterpret_cast<const char*>(&s),2);
    }
}

static uint32_t readVar(const std::vector<uint8_t>& d, size_t& p) {
    uint32_t v=0; uint8_t b=0;
    do { if (p>=d.size()) throw std::runtime_error("Invalid MIDI varint"); b=d[p++]; v=(v<<7)|(b&0x7f); } while (b&0x80);
    return v;
}

static std::vector<MidiNote> readMidi(const std::string& path) {
    std::ifstream f(path, std::ios::binary);
    if (!f) throw std::runtime_error("Could not open MIDI: " + path);
    char h[4]; f.read(h,4); if (std::strncmp(h,"MThd",4)!=0) throw std::runtime_error("Invalid MIDI header");
    uint32_t hlen=readBE32(f); uint16_t format=readBE16(f), tracks=readBE16(f), division=readBE16(f);
    if (hlen>6) f.seekg(hlen-6,std::ios::cur);
    if (division & 0x8000) throw std::runtime_error("SMPTE MIDI division is not supported");
    const int tpq=division;

    struct Tempo { uint64_t tick; uint32_t usq; };
    std::vector<Tempo> tempos{{0,500000}};
    struct RawNote { int note; int vel; uint64_t st, en; };
    std::vector<RawNote> raw;

    for (int tr=0; tr<tracks; ++tr) {
        f.read(h,4); if (std::strncmp(h,"MTrk",4)!=0) throw std::runtime_error("Invalid MIDI track");
        uint32_t len=readBE32(f); std::vector<uint8_t> d(len); f.read(reinterpret_cast<char*>(d.data()),len);
        size_t p=0; uint64_t tick=0; uint8_t running=0;
        std::array<std::vector<std::pair<uint64_t,int>>,128> active;
        while (p<d.size()) {
            tick += readVar(d,p);
            if (p>=d.size()) break;
            uint8_t status=d[p++];
            if (status<0x80) { if (!running) throw std::runtime_error("Invalid MIDI running status"); --p; status=running; }
            else if (status<0xF0) running=status;
            if (status==0xFF) {
                if (p>=d.size()) break; uint8_t type=d[p++]; uint32_t n=readVar(d,p);
                if (type==0x51 && n==3 && p+3<=d.size()) {
                    uint32_t usq=(d[p]<<16)|(d[p+1]<<8)|d[p+2]; tempos.push_back({tick,usq});
                }
                p += n; continue;
            }
            if (status==0xF0 || status==0xF7) { uint32_t n=readVar(d,p); p+=n; continue; }
            uint8_t hi=status&0xF0;
            auto next=[&](){ if (p>=d.size()) throw std::runtime_error("Truncated MIDI event"); return d[p++]; };
            if (hi==0x90 || hi==0x80) {
                int note=next(), vel=next();
                bool on=(hi==0x90 && vel>0);
                if (on) active[note].push_back({tick,vel});
                else if (!active[note].empty()) {
                    auto st=active[note].front(); active[note].erase(active[note].begin());
                    raw.push_back({note,st.second,st.first,tick});
                }
            } else if (hi==0xA0 || hi==0xB0 || hi==0xE0) { next(); next(); }
            else if (hi==0xC0 || hi==0xD0) { next(); }
            else throw std::runtime_error("Unsupported MIDI event");
        }
    }
    std::sort(tempos.begin(),tempos.end(),[](auto&a,auto&b){return a.tick<b.tick;});
    tempos.erase(std::unique(tempos.begin(),tempos.end(),[](auto&a,auto&b){return a.tick==b.tick;}),tempos.end());

    auto tickToSec=[&](uint64_t tick){
        double sec=0; uint64_t prev=0; uint32_t usq=500000;
        for (const auto& t:tempos) {
            if (t.tick>tick) break;
            if (t.tick>prev) sec += (t.tick-prev)*(usq/1e6)/tpq;
            prev=t.tick; usq=t.usq;
        }
        if (tick>prev) sec += (tick-prev)*(usq/1e6)/tpq;
        return sec;
    };

    std::vector<MidiNote> notes;
    for (auto&r:raw) if (r.en>r.st) notes.push_back({r.note,tickToSec(r.st),tickToSec(r.en),r.vel});
    std::sort(notes.begin(),notes.end(),[](auto&a,auto&b){ return a.start==b.start ? a.note>b.note : a.start<b.start; });
    // If polyphonic, choose the highest note starting at a given instant as the vocal melody.
    std::vector<MidiNote> mono;
    for (size_t i=0;i<notes.size();) {
        size_t j=i+1; MidiNote best=notes[i];
        while (j<notes.size() && std::abs(notes[j].start-notes[i].start)<1e-5) { if (notes[j].note>best.note) best=notes[j]; ++j; }
        mono.push_back(best); i=j;
    }
    if (mono.empty()) throw std::runtime_error("No note events found in MIDI");
    return mono;
}

static float rms(const float* x, int n) {
    double s=0; for(int i=0;i<n;i++) s += x[i]*x[i]; return static_cast<float>(std::sqrt(s/std::max(1,n)));
}

// YIN-style F0 estimate. Returns 0 if no reliable period.
static float yinPitch(const float* x, int n, int sr, int minHz, int maxHz) {
    int minTau=std::max(2,sr/std::max(1,maxHz));
    int maxTau=std::min(n/2,sr/std::max(1,minHz));
    if (maxTau<=minTau) return 0;
    std::vector<double> diff(maxTau+1,0), cmnd(maxTau+1,1);
    for(int tau=minTau;tau<=maxTau;tau++) {
        double d=0; for(int i=0;i<n-tau;i++){ double z=x[i]-x[i+tau]; d+=z*z; } diff[tau]=d;
    }
    double run=0; int best=0;
    for(int tau=1;tau<=maxTau;tau++) {
        run+=diff[tau]; cmnd[tau]=run>0 ? diff[tau]*tau/run : 1.0;
        if (tau>=minTau && cmnd[tau]<0.15) { best=tau; while(best+1<=maxTau && cmnd[best+1]<cmnd[best]) ++best; break; }
    }
    if (!best) {
        double v=1.0; for(int tau=minTau;tau<=maxTau;tau++) if(cmnd[tau]<v){v=cmnd[tau];best=tau;}
        if (v>0.30) return 0;
    }
    return static_cast<float>(sr/static_cast<double>(best));
}

static std::vector<PitchFrame> analyze(const Audio& a, int minHz, int maxHz) {
    int win=std::max(256,static_cast<int>(0.040*a.sampleRate));
    int hop=std::max(64,static_cast<int>(0.010*a.sampleRate));
    std::vector<PitchFrame> out;
    for(int s=0;s+win<static_cast<int>(a.samples.size());s+=hop) {
        float r=rms(a.samples.data()+s,win);
        float f = r>0.008f ? yinPitch(a.samples.data()+s,win,a.sampleRate,minHz,maxHz) : 0.0f;
        out.push_back({s,f,r,f>0 && r>0.008f});
    }
    return out;
}

static std::vector<Region> voicedRegions(const std::vector<PitchFrame>& frames, int sr, int totalSamples) {
    std::vector<Region> r; if(frames.empty()) return r;
    int hop = frames.size()>1 ? frames[1].sample-frames[0].sample : sr/100;
    int maxGapFrames=4; int i=0;
    while(i<(int)frames.size()) {
        while(i<(int)frames.size() && !frames[i].voiced) ++i; if(i>=frames.size()) break;
        int start=i, last=i, gap=0; ++i;
        while(i<(int)frames.size()) {
            if(frames[i].voiced){last=i;gap=0;} else if(++gap>maxGapFrames) break;
            ++i;
        }
        int a=std::max(0,frames[start].sample-hop);
        int b=std::min(totalSamples,frames[last].sample+3*hop);
        if (b-a > sr*0.045) r.push_back({a,b});
    }
    return r;
}

static int countTextVowelGroups(const std::string& text) {
    // Keep word weighting consistent with the phonetic planner. This matters for
    // initial y in "you" (a glide, not its own vowel) and rhotic/diphthong words.
    return std::max(1,resone::vocals::planWordPhonetics(text).vowelNuclei);
}

struct LyricWord {
    std::string text;
    int vowelGroups = 1;
};

struct TargetSegment {
    double start = 0;
    double end = 0;
    int note = 60;
    int velocity = 100;
    ResoneVocalGuidanceEvent guidance{};
};

struct WordTarget {
    std::vector<TargetSegment> segments;
    double soundingDuration = 0;
};

struct PitchSegment {
    int startSample = 0;
    int endSample = 0;
    double hz = 140;
    double previousHz = 0;
    double slideSeconds = 0;
    float gain = 1;
};

static bool lyricWordByte(unsigned char c) {
    return std::isalnum(c) || c >= 0x80;
}

static std::vector<LyricWord> lyricWords(const std::string& text) {
    std::vector<LyricWord> words;
    std::string current;
    auto flush=[&](){
        if(current.empty()) return;
        words.push_back({current,countTextVowelGroups(current)});
        current.clear();
    };
    for(size_t i=0;i<text.size();++i) {
        unsigned char c=static_cast<unsigned char>(text[i]);
        bool joiner=(c=='\'' || c=='-') && !current.empty() && i+1<text.size() && lyricWordByte(static_cast<unsigned char>(text[i+1]));
        if(lyricWordByte(c) || joiner) current.push_back(static_cast<char>(c));
        else flush();
    }
    flush();
    return words;
}

static double medianPitch(const std::vector<PitchFrame>& f, int a, int b) {
    std::vector<float> p; for(auto&x:f) if(x.sample>=a&&x.sample<=b&&x.voiced) p.push_back(x.f0);
    if(p.empty()) return 0; std::sort(p.begin(),p.end()); return p[p.size()/2];
}

static double normalizePitchOctave(double hz, double reference) {
    if(hz<40 || reference<40) return hz;
    while(hz>reference*1.65) hz*=0.5;
    while(hz<reference/1.65) hz*=2.0;
    return hz;
}

static float midiHz(int note) { return 440.0f*std::pow(2.0f,(note-69)/12.0f); }

static int hzToMidi(double hz) {
    if(hz<1) return -1;
    return (int)std::lround(69.0 + 12.0*std::log2(hz/440.0));
}

static int foldMidiToRange(int note, int minNote, int maxNote) {
    if(minNote<0 || maxNote<minNote) return note;
    // Preserve pitch class and use octave transposition only. High melodies are
    // brought down, low melodies are brought up, until the singer stays inside
    // the analyzed comfortable register.
    while(note>maxNote) note-=12;
    while(note<minNote) note+=12;
    return std::clamp(note,minNote,maxNote);
}

static double foldMidiFloatToRange(double note, int minNote, int maxNote) {
    if(minNote<0 || maxNote<minNote) return note;
    while(note>maxNote) note-=12.0;
    while(note<minNote) note+=12.0;
    return std::clamp(note,(double)minNote,(double)maxNote);
}

static std::vector<float> hann(int n) {
    std::vector<float>w(n); for(int i=0;i<n;i++) w[i]=0.5f-0.5f*std::cos(2.0*kPi*i/std::max(1,n-1)); return w;
}

static ResoneVocalGuidanceEvent guidanceAt(const ResoneVocalGuidanceEvent* g, int n, double t) {
    ResoneVocalGuidanceEvent d{t,0,1,1,0,0,0,1.25f,0.7f};
    if(!g||n<=0) return d;
    int best=-1; double bd=1e100;
    for(int i=0;i<n;i++){ double x=std::abs(g[i].timeSeconds-t); if(x<bd){bd=x;best=i;} }
    if(best>=0) return g[best]; return d;
}

static Region trimSpeechRegion(const Audio& a, Region r) {
    r.start=std::clamp(r.start,0,(int)a.samples.size());
    r.end=std::clamp(r.end,r.start,(int)a.samples.size());
    if(r.end-r.start < a.sampleRate/100) return r;
    int hop=std::max(32,a.sampleRate/400); // 2.5 ms
    int win=std::max(hop,a.sampleRate/100); // 10 ms
    float peak=0;
    for(int s=r.start;s<r.end;s+=hop) {
        int n=std::min(win,r.end-s); if(n>0) peak=std::max(peak,rms(a.samples.data()+s,n));
    }
    float threshold=std::max(0.0008f,peak*0.025f);
    int first=r.start,last=r.end;
    bool found=false;
    for(int s=r.start;s<r.end;s+=hop) {
        int n=std::min(win,r.end-s);
        if(n>0 && rms(a.samples.data()+s,n)>=threshold){ first=s; found=true; break; }
    }
    if(!found) return r;
    for(int s=r.end-win;s>=r.start;s-=hop) {
        int ss=std::max(r.start,s), n=std::min(win,r.end-ss);
        if(n>0 && rms(a.samples.data()+ss,n)>=threshold){ last=ss+n; break; }
    }
    int pad=(int)std::lround(0.012*a.sampleRate);
    return {std::max(r.start,first-pad),std::min(r.end,last+pad)};
}

static std::vector<Region> speechActivityRegions(const Audio& a) {
    std::vector<Region> regions;
    if(a.samples.empty()) return regions;
    int hop=std::max(64,(int)std::lround(0.010*a.sampleRate));
    int win=std::max(hop,(int)std::lround(0.020*a.sampleRate));
    std::vector<float> energy;
    for(int s=0;s<(int)a.samples.size();s+=hop) {
        int n=std::min(win,(int)a.samples.size()-s);
        energy.push_back(n>0?rms(a.samples.data()+s,n):0);
    }
    float peak=0; for(float e:energy) peak=std::max(peak,e);
    if(peak<0.001f) return regions;
    float threshold=std::max(0.0012f,peak*0.035f);
    int maxGap=std::max(2,(int)std::lround(0.070*a.sampleRate/hop));
    int i=0;
    while(i<(int)energy.size()) {
        while(i<(int)energy.size() && energy[i]<threshold) ++i;
        if(i>=(int)energy.size()) break;
        int first=i,last=i,gap=0; ++i;
        while(i<(int)energy.size()) {
            if(energy[i]>=threshold){last=i;gap=0;}
            else if(++gap>maxGap) break;
            ++i;
        }
        int pad=(int)std::lround(0.015*a.sampleRate);
        Region r{std::max(0,first*hop-pad),std::min((int)a.samples.size(),last*hop+win+pad)};
        if(r.end-r.start >= (int)(0.030*a.sampleRate)) regions.push_back(trimSpeechRegion(a,r));
    }
    return regions;
}

static float localEnergy(const Audio& a, int center) {
    int half=std::max(16,(int)std::lround(0.008*a.sampleRate));
    int s=std::max(0,center-half), e=std::min((int)a.samples.size(),center+half);
    return e>s?rms(a.samples.data()+s,e-s):0;
}

static double wordWeight(const LyricWord& w) {
    int letters=0; for(unsigned char c:w.text) if(std::isalnum(c) || c>=0x80) ++letters;
    return 0.70 + 0.90*std::max(1,w.vowelGroups) + 0.025*letters;
}

static int minimumSourceWordSamples(const LyricWord& word, int sr) {
    // A source word shorter than this is almost always a bad activity split. In
    // particular, a quiet initial glide (/j/ in "you") can otherwise be cut away
    // and leave the final word with too little acoustic identity to sing.
    const double seconds=0.060 + 0.020*std::max(0,word.vowelGroups-1);
    return std::max(1,(int)std::lround(seconds*sr));
}

static bool plausibleWordRegions(const std::vector<Region>& regions,
                                 const std::vector<LyricWord>& words,
                                 int sr) {
    if(regions.size()!=words.size()) return false;
    for(size_t i=0;i<regions.size();++i)
        if(regions[i].end-regions[i].start<minimumSourceWordSamples(words[i],sr)) return false;
    return true;
}

static std::vector<Region> makeWordRegionsContiguous(std::vector<Region> regions) {
    if(regions.empty()) return regions;
    for(size_t i=0;i+1<regions.size();++i) {
        // Preserve the low-energy material between speech islands instead of
        // throwing it away.  That gap frequently contains /h/, /j/, /w/, stop
        // closures, or breath that is essential to the next word.
        const int boundary=(regions[i].end+regions[i+1].start)/2;
        regions[i].end=boundary;
        regions[i+1].start=boundary;
    }
    return regions;
}

static void enforceMinimumWordRegions(std::vector<Region>& out,
                                      const std::vector<LyricWord>& words,
                                      int first,
                                      int last,
                                      int sr) {
    if(out.size()!=words.size() || out.empty()) return;
    out.front().start=first;
    out.back().end=last;

    // Forward and backward passes move only shared boundaries. No sample is
    // duplicated and no gap is created.
    for(size_t i=0;i+1<out.size();++i) {
        const int need=minimumSourceWordSamples(words[i],sr);
        const int nextNeed=minimumSourceWordSamples(words[i+1],sr);
        if(out[i].end-out[i].start>=need) continue;
        const int wanted=out[i].start+need;
        const int maxBoundary=out[i+1].end-nextNeed;
        const int boundary=std::min(wanted,maxBoundary);
        if(boundary>out[i].start){out[i].end=boundary;out[i+1].start=boundary;}
    }
    for(size_t i=out.size()-1;i>0;--i) {
        const int need=minimumSourceWordSamples(words[i],sr);
        const int prevNeed=minimumSourceWordSamples(words[i-1],sr);
        if(out[i].end-out[i].start>=need) continue;
        const int wanted=out[i].end-need;
        const int minBoundary=out[i-1].start+prevNeed;
        const int boundary=std::max(wanted,minBoundary);
        if(boundary<out[i].end){out[i-1].end=boundary;out[i].start=boundary;}
    }
}

static std::vector<Region> fitSpeechToWords(const Audio& a, const std::vector<LyricWord>& words) {
    if(words.empty()) return {};
    auto regions=speechActivityRegions(a);
    if(regions.empty()) return {{0,(int)a.samples.size()}};

    // A deliberately articulated source utterance often yields one island per
    // word. Merge detector fragments, then convert the island gaps to shared
    // boundaries so quiet consonants are not discarded.
    while(regions.size()>words.size()) {
        size_t best=0; int bestGap=std::numeric_limits<int>::max();
        for(size_t i=0;i+1<regions.size();++i) {
            int gap=std::max(0,regions[i+1].start-regions[i].end);
            if(gap<bestGap){bestGap=gap;best=i;}
        }
        regions[best].end=regions[best+1].end;
        regions.erase(regions.begin()+best+1);
    }
    if(regions.size()==words.size()) {
        auto contiguous=makeWordRegionsContiguous(regions);
        enforceMinimumWordRegions(contiguous,words,regions.front().start,regions.back().end,a.sampleRate);
        if(plausibleWordRegions(contiguous,words,a.sampleRate)) return contiguous;
    }

    // Fallback: cut the complete speech envelope near expected text-weight positions,
    // snapping each cut to the quietest nearby point. This stays monotonic and never
    // duplicates a word even if the TTS engine did not leave a clean pause.
    const int first=regions.front().start,last=regions.back().end;
    double totalWeight=0; for(const auto&w:words) totalWeight+=wordWeight(w);
    std::vector<Region> out; out.reserve(words.size());
    int previous=first;
    double cumulative=0;
    const int average=std::max(1,(last-first)/(int)words.size());
    for(size_t i=0;i+1<words.size();++i) {
        cumulative+=wordWeight(words[i]);
        const int expected=first+(int)std::lround((last-first)*(cumulative/totalWeight));
        const int radius=std::max((int)(0.025*a.sampleRate),
            std::min((int)(0.075*a.sampleRate),(int)(average*0.28)));
        const int currentMin=minimumSourceWordSamples(words[i],a.sampleRate);
        int futureMin=0;
        for(size_t j=i+1;j<words.size();++j) futureMin+=minimumSourceWordSamples(words[j],a.sampleRate);
        const int lo=std::max(previous+currentMin,expected-radius);
        const int hi=std::min(last-futureMin,expected+radius);
        int boundary=std::clamp(expected,lo,std::max(lo,hi));
        float best=std::numeric_limits<float>::max();
        const int step=std::max(8,a.sampleRate/500);
        for(int at=lo;at<=hi;at+=step) {
            const float e=localEnergy(a,at);
            if(e<best){best=e;boundary=at;}
        }
        out.push_back({previous,boundary});
        previous=boundary;
    }
    out.push_back({previous,last});
    enforceMinimumWordRegions(out,words,first,last,a.sampleRate);
    return out;
}

static std::vector<WordTarget> buildWordTargets(const std::vector<MidiNote>& inputNotes,
                                                const std::vector<LyricWord>& words,
                                                const ResoneVocalGuidanceEvent* guidance,
                                                int guidanceCount,
                                                const ResoneVocalRenderOptions& options) {
    std::vector<WordTarget> targets(words.size());
    if(words.empty()||inputNotes.empty()) return targets;
    std::vector<TargetSegment> melody;
    melody.reserve(inputNotes.size());
    for(size_t i=0;i<inputNotes.size();++i) {
        const auto& n=inputNotes[i];
        double end=n.end;
        if(i+1<inputNotes.size() && inputNotes[i+1].start>n.start && end>inputNotes[i+1].start)
            end=inputNotes[i+1].start;
        if(end-n.start<0.012) continue;
        int mappedNote=foldMidiToRange(n.note,options.singingMinMidiNote,options.singingMaxMidiNote);
        melody.push_back({n.start,end,mappedNote,n.velocity,guidanceAt(guidance,guidanceCount,n.start)});
    }
    if(melody.empty()) return targets;
    double total=0; for(auto&s:melody) total+=s.end-s.start;
    double weightTotal=0; for(auto&w:words) weightTotal+=wordWeight(w);
    std::vector<double> desired(words.size());
    for(size_t i=0;i<words.size();++i) desired[i]=total*wordWeight(words[i])/std::max(1e-9,weightTotal);

    size_t mi=0; double cursor=melody[0].start, used=0;
    for(size_t wi=0;wi<words.size() && mi<melody.size();++wi) {
        double need=(wi+1==words.size())?std::max(0.0,total-used):desired[wi];
        while(need>1e-7 && mi<melody.size()) {
            const auto& src=melody[mi];
            cursor=std::max(cursor,src.start);
            double available=src.end-cursor;
            if(available<=1e-8){ ++mi; if(mi<melody.size()) cursor=melody[mi].start; continue; }
            double take=std::min(need,available);
            TargetSegment part=src; part.start=cursor; part.end=cursor+take;
            targets[wi].segments.push_back(part);
            targets[wi].soundingDuration+=take;
            cursor+=take; need-=take; used+=take;
            if(cursor>=src.end-1e-8){ ++mi; if(mi<melody.size()) cursor=melody[mi].start; }
        }
    }
    return targets;
}

static std::vector<float> resampleLinear(const std::vector<float>& src, int outSamples) {
    if(outSamples<=0) return {};
    if(src.empty()) return std::vector<float>(outSamples,0);
    if(src.size()==1) return std::vector<float>(outSamples,src[0]);
    std::vector<float> out(outSamples);
    for(int i=0;i<outSamples;++i) {
        double pos=i*(src.size()-1.0)/std::max(1,outSamples-1);
        int j=(int)pos; double t=pos-j;
        out[i]=src[j]+(src[std::min<int>(j+1,(int)src.size()-1)]-src[j])*(float)t;
    }
    return out;
}

static Region longestVoicedInside(const std::vector<PitchFrame>& frames, Region word, int sr) {
    Region best{0,0},current{0,0};
    int hop=frames.size()>1?frames[1].sample-frames[0].sample:std::max(1,sr/100);
    int gap=0,maxGap=3;
    bool active=false;
    auto finish=[&](){
        if(active && current.end-current.start>best.end-best.start) best=current;
        active=false; gap=0;
    };
    for(const auto&f:frames) {
        if(f.sample<word.start) continue;
        if(f.sample>word.end){finish();break;}
        if(f.voiced) {
            if(!active){current.start=std::max(word.start,f.sample-hop);active=true;}
            current.end=std::min(word.end,f.sample+2*hop);gap=0;
        } else if(active && ++gap>maxGap) finish();
    }
    finish();
    return best;
}

static double pitchAt(const std::vector<PitchSegment>& contour, int sample, int sr,
                      float vibratoCents, float vibratoHz) {
    if(contour.empty()) return 140.0;
    const PitchSegment* seg=&contour.back();
    for(const auto&s:contour) if(sample<s.endSample){seg=&s;break;}
    double hz=seg->hz;
    int local=std::max(0,sample-seg->startSample);
    if(seg->slideSeconds>0 && seg->previousHz>40 && local<seg->slideSeconds*sr) {
        double u=std::clamp(local/(seg->slideSeconds*sr),0.0,1.0);
        // Singers glide in pitch space (cents), not as a linear Hz sweep. A
        // smoothstep keeps the vowel connected while still settling decisively
        // onto the note center.
        double shaped=u*u*(3.0-2.0*u);
        hz=seg->previousHz*std::pow(seg->hz/seg->previousHz,shaped);
    }
    if(vibratoCents>0 && vibratoHz>0) {
        // Establish the note cleanly before vibrato blooms. Fast notes stay nearly
        // straight; sustained notes acquire a gentle singing vibrato.
        double noteSeconds=(seg->endSample-seg->startSample)/(double)sr;
        double localSeconds=local/(double)sr;
        double noteMaturity=std::clamp((noteSeconds-0.16)/0.24,0.0,1.0);
        double onsetRamp=std::clamp((localSeconds-0.14)/0.12,0.0,1.0);
        double depth=vibratoCents*noteMaturity*onsetRamp;
        double t=sample/(double)sr;
        hz*=std::pow(2.0,(depth*std::sin(2*kPi*vibratoHz*t))/1200.0);
    }
    return std::max(40.0,hz);
}

// Look up the source F0 at an absolute sample. Spoken TTS has real pitch drift,
// so a single median F0 is not enough for pitch-synchronous resynthesis. Prefer
// nearby voiced analysis frames and octave-normalize them against the word/voice
// reference to reject the common half/double-frequency YIN errors.
static double sourcePitchAt(const std::vector<PitchFrame>& frames,
                            int absoluteSample,
                            double fallbackHz,
                            double referenceHz) {
    if(frames.empty()) return fallbackHz;
    const PitchFrame* best=nullptr;
    int bestDistance=std::numeric_limits<int>::max();
    for(const auto&f:frames) {
        if(!f.voiced || f.f0<40) continue;
        int d=std::abs(f.sample-absoluteSample);
        if(d<bestDistance){bestDistance=d;best=&f;}
        if(f.sample>absoluteSample && d>bestDistance) break;
    }
    double hz=best?best->f0:fallbackHz;
    hz=normalizePitchOctave(hz,referenceHz>=40?referenceHz:fallbackHz);
    return hz>=40?hz:fallbackHz;
}

// Refine a predicted source pitch mark by matching one period of waveform around
// the previous mark. This is inexpensive for the short per-word vowels and keeps
// grains phase coherent instead of cutting them at arbitrary points.
static int refinePitchMark(const std::vector<float>& src,
                           int previous,
                           int predicted,
                           int period) {
    if(src.empty()) return 0;
    period=std::max(12,period);
    int radius=std::max(2,period/5);
    int half=std::max(6,period/3);
    int lo=std::max(previous+std::max(4,period/2),predicted-radius);
    int hi=std::min((int)src.size()-half-1,predicted+radius);
    if(lo>hi) return std::clamp(predicted,0,(int)src.size()-1);
    double best=-2.0; int bestAt=lo;
    for(int cand=lo;cand<=hi;++cand) {
        double xy=0,xx=0,yy=0;
        for(int k=-half;k<=half;++k) {
            int a=previous+k,b=cand+k;
            if(a<0||a>=(int)src.size()||b<0||b>=(int)src.size()) continue;
            double x=src[a],y=src[b]; xy+=x*y; xx+=x*x; yy+=y*y;
        }
        double score=xy/std::sqrt(std::max(1e-12,xx*yy));
        if(score>best){best=score;bestAt=cand;}
    }
    return bestAt;
}

// Build actual source pitch marks by following the measured source F0. This is
// the "auto-correct first" half of the singer: every grain comes from a coherent
// source cycle even when the spoken vowel wandered sharp/flat while Qwen spoke it.
static std::vector<int> sourcePitchMarks(const std::vector<float>& src,
                                         int absoluteStart,
                                         const std::vector<PitchFrame>& frames,
                                         int sr,
                                         double fallbackHz,
                                         double referenceHz) {
    std::vector<int> marks;
    if(src.size()<32) return marks;
    double firstHz=sourcePitchAt(frames,absoluteStart,fallbackHz,referenceHz);
    int firstPeriod=std::max(12,(int)std::lround(sr/std::max(40.0,firstHz)));
    int scanEnd=std::min((int)src.size()-1,std::max(firstPeriod*2,24));
    int first=std::min((int)src.size()-1,std::max(0,firstPeriod/2));
    float peak=0;
    for(int i=std::max(0,firstPeriod/3);i<=scanEnd;++i) {
        float v=std::abs(src[i]); if(v>peak){peak=v;first=i;}
    }
    marks.push_back(first);
    int guard=0;
    while(marks.back()<(int)src.size()-16 && ++guard<20000) {
        int prev=marks.back();
        double hz=sourcePitchAt(frames,absoluteStart+prev,fallbackHz,referenceHz);
        int period=std::max(12,(int)std::lround(sr/std::max(40.0,hz)));
        int predicted=prev+period;
        if(predicted>=(int)src.size()-4) break;
        int next=refinePitchMark(src,prev,predicted,period);
        if(next<=prev+3) next=predicted;
        if(next>=(int)src.size()) break;
        marks.push_back(next);
    }
    if(marks.size()<2) {
        marks.clear();
        for(int p=first;p<(int)src.size();p+=firstPeriod) marks.push_back(p);
    }
    return marks;
}

static int sourcePeriodAtMark(const std::vector<int>& marks,size_t i,int fallbackPeriod) {
    if(marks.size()<2) return fallbackPeriod;
    int a=i>0?marks[i]-marks[i-1]:marks[1]-marks[0];
    int b=i+1<marks.size()?marks[i+1]-marks[i]:a;
    return std::max(12,(a+b)/2);
}

// Pitch-synchronous time/pitch mapping for the vowel nucleus. Source grain
// centers follow the *measured source periods*; destination grain centers follow
// the MIDI target periods. That explicitly removes the spoken F0 drift before the
// melody pitch is imposed, while retaining the source spectral envelope/formants.
static std::vector<float> psolaSustain(const std::vector<float>& src,
                                       int srcAbsoluteStart,
                                       const std::vector<PitchFrame>& frames,
                                       int sr,
                                       double srcF0,
                                       double voiceReferenceHz,
                                       int outSamples,
                                       int wordOffsetSamples,
                                       const std::vector<PitchSegment>& contour,
                                       float vibratoCents,
                                       float vibratoHz,
                                       float formantPreserve) {
    if(src.empty()||outSamples<=0) return {};
    if(srcF0<40) return resampleLinear(src,outSamples);
    auto marks=sourcePitchMarks(src,srcAbsoluteStart,frames,sr,srcF0,voiceReferenceHz);
    if(marks.empty()) return resampleLinear(src,outSamples);

    int fallbackPeriod=std::max(12,(int)std::lround(sr/srcF0));
    int pad=std::max(256,fallbackPeriod*3);
    std::vector<float> out(outSamples+2*pad,0),weight(out.size(),0);
    double dstCenter=0.0;
    int grains=0;
    while(dstCenter<outSamples && ++grains<outSamples/2+24000) {
        double progress=std::clamp(dstCenter/std::max(1.0,outSamples-1.0),0.0,1.0);
        size_t mi=(size_t)std::clamp<long long>((long long)std::llround(progress*(marks.size()-1)),0,(long long)marks.size()-1);
        int sc=marks[mi];
        int sourcePeriod=sourcePeriodAtMark(marks,mi,fallbackPeriod);

        int fullSample=wordOffsetSamples+std::clamp((int)std::lround(dstCenter),0,std::max(0,outSamples-1));
        double targetHz=pitchAt(contour,fullSample,sr,vibratoCents,vibratoHz);
        double dstPeriod=sr/std::max(40.0,targetHz);

        // Keep the analysis window source-sized for formant preservation. For big
        // downward shifts, widen only enough to avoid holes; formantPreserve controls
        // how strongly we resist that widening.
        double preserve=std::clamp((double)formantPreserve,0.0,1.0);
        double desiredHalf=std::max((double)sourcePeriod,0.58*dstPeriod);
        double halfD=sourcePeriod+(desiredHalf-sourcePeriod)*(1.0-preserve*0.85);
        int half=std::clamp((int)std::lround(halfD),16,std::max(24,pad-2));
        int winN=2*half+1;
        auto window=hann(winN);
        int dc=(int)std::lround(dstCenter)+pad;
        for(int k=-half;k<=half;++k) {
            int si=std::clamp(sc+k,0,(int)src.size()-1),di=dc+k;
            if(di<0||di>=(int)out.size()) continue;
            float ww=window[k+half];
            out[di]+=src[si]*ww;
            weight[di]+=ww;
        }
        dstCenter+=dstPeriod;
    }
    std::vector<float> trimmed(outSamples);
    for(int i=0;i<outSamples;++i) {
        float w=weight[i+pad];
        trimmed[i]=w>1e-5f?out[i+pad]/w:0.0f;
    }
    return trimmed;
}

// A pitch-synchronous shifter preserves the source spectral envelope by design.
// That is desirable for identity, but a large upward transposition of a low male
// voice can retain more low-mid weight than a singer would naturally produce at
// the destination register. Apply only a *small* post-pitch spectral tilt; this is
// intentionally not another resampling pitch shift. formantPreserve=1 keeps the
// original envelope untouched, while lower values allow a little register-aware
// brightness compensation.
static void applyRegisterTimbreCompensation(std::vector<float>& x,
                                            double sourceHz,
                                            const std::vector<PitchSegment>& contour,
                                            float formantPreserve) {
    if(x.size()<3 || sourceHz<40 || contour.empty()) return;
    double logSum=0,weightSum=0;
    for(const auto&s:contour) {
        double w=std::max(1,s.endSample-s.startSample);
        logSum+=std::log(std::max(40.0,s.hz))*w; weightSum+=w;
    }
    if(weightSum<=0) return;
    double targetHz=std::exp(logSum/weightSum);
    double octaves=std::log2(targetHz/sourceHz);
    if(octaves<=0.10) return; // never make downward shifts darker/boomier
    double preserve=std::clamp((double)formantPreserve,0.0,1.0);
    double a=std::clamp(octaves*(1.0-preserve*0.72)*0.18,0.0,0.24);
    if(a<0.005) return;
    double before=0; for(float v:x) before+=v*v;
    float previous=x.front();
    for(size_t i=1;i<x.size();++i) {
        float current=x[i];
        x[i]=(float)(current-a*previous);
        previous=current;
    }
    double after=0; for(float v:x) after+=v*v;
    if(before>1e-12 && after>1e-12) {
        float g=(float)std::sqrt(before/after);
        g=std::clamp(g,0.75f,1.35f);
        for(float&v:x) v*=g;
    }
}

// -----------------------------------------------------------------------------
// Speech-preserving word renderer
// -----------------------------------------------------------------------------
// A sung word is not one vowel with everything else squeezed around it.  Real
// words can contain several vowel nuclei separated by plosives/fricatives.  The
// renderer therefore keeps an ordered set of voiced and consonant regions.
// Consonant regions are emitted once at close to their natural duration; only
// voiced regions absorb the extra musical time and receive pitch-synchronous
// resynthesis.

enum class SpeechPieceKind {
    Vowel,
    VowelOffglide,
    ConsonantTransient,
    ConsonantNoiseSustainable,
    ConsonantVoicedSustainable,
    ConsonantGlide,
    ConsonantAspirate,
    ConsonantMixed
};

struct SpeechPiece {
    Region source{};
    SpeechPieceKind kind = SpeechPieceKind::ConsonantTransient;
    std::string textHint;
    double sustainWeight = 0.0;
    int outputSamples = 0;
};

struct RenderedWord {
    // Exactly the word's assigned melodic sounding duration.  Consonants and
    // vowel nuclei are already ordered/crossfaded inside this buffer.
    std::vector<float> body;
};

static double targetStart(const WordTarget& t) { return t.segments.empty()?0.0:t.segments.front().start; }
static double targetEnd(const WordTarget& t) { return t.segments.empty()?0.0:t.segments.back().end; }

static std::vector<Region> voicedIslandsInside(const std::vector<PitchFrame>& frames,
                                                Region word,
                                                int sr) {
    std::vector<Region> islands;
    if(frames.empty() || word.end<=word.start) return islands;

    const int hop=frames.size()>1 ? frames[1].sample-frames[0].sample : std::max(1,sr/100);
    const int analysisCenter=(int)std::lround(0.020*sr);
    const int maxVoicingGapFrames=1;
    const int minVoicedSamples=(int)std::lround(0.024*sr);

    bool active=false;
    int first=-1,last=-1,gap=0;
    auto finish=[&](){
        if(!active) return;
        int a=frames[first].sample+analysisCenter-hop/2;
        int b=frames[last].sample+analysisCenter+hop/2;
        a=std::clamp(a,word.start,word.end);
        b=std::clamp(b,a,word.end);
        if(b-a>=minVoicedSamples) islands.push_back({a,b});
        active=false; first=last=-1; gap=0;
    };

    for(int i=0;i<(int)frames.size();++i) {
        int center=frames[i].sample+analysisCenter;
        if(center<word.start) continue;
        if(center>word.end){finish();break;}
        if(frames[i].voiced) {
            if(!active){active=true;first=i;}
            last=i; gap=0;
        } else if(active && ++gap>maxVoicingGapFrames) {
            finish();
        }
    }
    finish();

    const int mergeGap=(int)std::lround(0.009*sr);
    std::vector<Region> merged;
    for(const auto&r:islands) {
        if(!merged.empty() && r.start-merged.back().end<=mergeGap) merged.back().end=r.end;
        else merged.push_back(r);
    }
    return merged;
}

static int quietSplitPoint(const Audio& a, Region r, int sr) {
    int margin=std::max((int)std::lround(0.020*sr),(r.end-r.start)/6);
    int lo=r.start+margin, hi=r.end-margin;
    if(lo>=hi) return (r.start+r.end)/2;
    int step=std::max(4,sr/1000); // ~1 ms
    float best=std::numeric_limits<float>::max();
    int bestAt=(lo+hi)/2;
    for(int s=lo;s<=hi;s+=step) {
        float e=localEnergy(a,s);
        if(e<best){best=e;bestAt=s;}
    }
    return bestAt;
}

// Text tells us how many vowel nuclei a word should have.  The acoustic pitch
// detector tells us where the stable voiced islands are.  Reconcile the two so
// words like "happy" and "birthday" retain multiple syllables instead of being
// treated as one giant vowel with all consonants compressed around it.
static std::vector<Region> fitVoicedNucleiToText(const Audio& a,
                                                  const std::vector<PitchFrame>& frames,
                                                  Region word,
                                                  int sr,
                                                  int expectedNuclei) {
    expectedNuclei=std::max(1,expectedNuclei);
    auto nuclei=voicedIslandsInside(frames,word,sr);

    if(nuclei.empty()) {
        int span=word.end-word.start;
        int pad=std::min(span/4,std::max(1,(int)std::lround(0.020*sr)));
        nuclei.push_back({word.start+pad,std::max(word.start+pad+1,word.end-pad)});
    }

    // Too many pitch islands usually means detector dropout. Merge the closest
    // pair first; a real consonant gap tends to be larger than detector flicker.
    while((int)nuclei.size()>expectedNuclei) {
        size_t best=0; int bestGap=std::numeric_limits<int>::max();
        for(size_t i=0;i+1<nuclei.size();++i) {
            int gap=std::max(0,nuclei[i+1].start-nuclei[i].end);
            if(gap<bestGap){bestGap=gap;best=i;}
        }
        nuclei[best].end=nuclei[best+1].end;
        nuclei.erase(nuclei.begin()+best+1);
    }

    // Too few islands means voicing ran through an internal sonorant or the pitch
    // detector never dropped. Split the longest island at its quietest interior
    // point and reserve a small acoustic slot for the inter-syllable consonant.
    const int splitGap=std::max(2,(int)std::lround(0.010*sr));
    const int minNucleus=std::max(4,(int)std::lround(0.024*sr));
    while((int)nuclei.size()<expectedNuclei) {
        size_t best=nuclei.size(); int bestLen=0;
        for(size_t i=0;i<nuclei.size();++i) {
            int len=nuclei[i].end-nuclei[i].start;
            if(len>bestLen && len>=2*minNucleus+splitGap){best=i;bestLen=len;}
        }
        if(best==nuclei.size()) break;
        Region original=nuclei[best];
        int split=quietSplitPoint(a,original,sr);
        int leftEnd=std::clamp(split-splitGap/2,original.start+minNucleus,original.end-minNucleus);
        int rightStart=std::clamp(split+splitGap/2,leftEnd,original.end-minNucleus);
        Region left{original.start,leftEnd};
        Region right{rightStart,original.end};
        nuclei[best]=left;
        nuclei.insert(nuclei.begin()+best+1,right);
    }

    // Last-resort proportional subdivision if the source is extremely smooth.
    while((int)nuclei.size()<expectedNuclei) {
        size_t best=0; int bestLen=-1;
        for(size_t i=0;i<nuclei.size();++i) {
            int len=nuclei[i].end-nuclei[i].start;
            if(len>bestLen){best=i;bestLen=len;}
        }
        Region original=nuclei[best];
        int split=(original.start+original.end)/2;
        if(split<=original.start+2 || split>=original.end-2) break;
        nuclei[best].end=split;
        nuclei.insert(nuclei.begin()+best+1,{split,original.end});
    }
    return nuclei;
}

static SpeechPieceKind consonantKind(const resone::vocals::TextConsonantCluster& cluster) {
    using resone::vocals::ConsonantBehavior;
    switch (cluster.behavior) {
        case ConsonantBehavior::SustainableNoise: return SpeechPieceKind::ConsonantNoiseSustainable;
        case ConsonantBehavior::SustainableVoiced: return SpeechPieceKind::ConsonantVoicedSustainable;
        case ConsonantBehavior::Glide: return SpeechPieceKind::ConsonantGlide;
        case ConsonantBehavior::Aspirate: return SpeechPieceKind::ConsonantAspirate;
        case ConsonantBehavior::Mixed: return SpeechPieceKind::ConsonantMixed;
        case ConsonantBehavior::Transient: return SpeechPieceKind::ConsonantTransient;
        default: return SpeechPieceKind::ConsonantTransient;
    }
}

static bool clusterEndsWithGlide(const resone::vocals::TextConsonantCluster& cluster) {
    using resone::vocals::ConsonantBehavior;
    return !cluster.units.empty() && cluster.units.back().behavior == ConsonantBehavior::Glide;
}

static SpeechPieceKind clusterKindWithoutTrailingGlide(const resone::vocals::TextConsonantCluster& cluster) {
    using resone::vocals::ConsonantBehavior;
    bool anyTransient=false, anyNoise=false, anyVoiced=false, anyAspirate=false;
    const size_t count=clusterEndsWithGlide(cluster) && !cluster.units.empty() ? cluster.units.size()-1 : cluster.units.size();
    for(size_t i=0;i<count;++i) {
        const auto b=cluster.units[i].behavior;
        anyTransient |= b==ConsonantBehavior::Transient;
        anyNoise |= b==ConsonantBehavior::SustainableNoise;
        anyVoiced |= b==ConsonantBehavior::SustainableVoiced;
        anyAspirate |= b==ConsonantBehavior::Aspirate;
    }
    const int kinds=(anyTransient?1:0)+(anyNoise?1:0)+(anyVoiced?1:0)+(anyAspirate?1:0);
    if(kinds>1) return SpeechPieceKind::ConsonantMixed;
    if(anyAspirate) return SpeechPieceKind::ConsonantAspirate;
    if(anyVoiced) return SpeechPieceKind::ConsonantVoicedSustainable;
    if(anyNoise) return SpeechPieceKind::ConsonantNoiseSustainable;
    return SpeechPieceKind::ConsonantTransient;
}

static void appendConsonantPieces(std::vector<SpeechPiece>& pieces,
                                  Region source,
                                  const resone::vocals::TextConsonantCluster& cluster) {
    if(source.end<=source.start) return;
    // Keep the entire acoustic consonant cluster intact. Text spelling tells us
    // whether a cluster is generally stretchable, but without forced phoneme
    // timestamps it is unsafe to subdivide the actual waveform by character
    // count. That previous proportional split caused discontinuities in mixed
    // clusters even though articulation itself improved.
    pieces.push_back({source,consonantKind(cluster),cluster.text,cluster.sustainWeight,0});
}

static std::vector<SpeechPiece> splitWordSpeechPieces(const Audio& a,
                                                       const std::vector<PitchFrame>& frames,
                                                       Region word,
                                                       int sr,
                                                       const LyricWord& lyric) {
    const auto plan=resone::vocals::planWordPhonetics(lyric.text);
    auto nuclei=fitVoicedNucleiToText(a,frames,word,sr,plan.vowelNuclei);
    if(nuclei.empty()) return {{{word.start,word.end},SpeechPieceKind::ConsonantNoiseSustainable,lyric.text,0.20,0}};

    std::vector<SpeechPiece> pieces;
    pieces.reserve(nuclei.size()*2+1);
    int cursor=word.start;
    for(size_t i=0;i<nuclei.size();++i) {
        Region v=nuclei[i];
        v.start=std::clamp(v.start,cursor,word.end);
        v.end=std::clamp(v.end,v.start,word.end);
        const auto& cluster=plan.consonants[std::min(i,plan.consonants.size()-1)];

        // y/w/r are voiced and are commonly swallowed by the pitch detector
        // into the same voiced island as the vowel.  When text says a vowel is
        // preceded by a glide, explicitly reserve a short source slice from the
        // front of that island.  For a mixed onset such as "br", the pre-island
        // material remains the stop/fricative and only the last voiced transition
        // becomes the glide.  No source sample is consumed twice.
        const bool carveGlide=clusterEndsWithGlide(cluster) &&
            v.end-v.start >= std::max(2,(int)std::lround(0.060*sr));
        if(v.start>cursor) {
            if(carveGlide && cluster.behavior==resone::vocals::ConsonantBehavior::Mixed) {
                pieces.push_back({{cursor,v.start},clusterKindWithoutTrailingGlide(cluster),cluster.text,0.0,0});
            } else if(!carveGlide || cluster.behavior!=resone::vocals::ConsonantBehavior::Glide) {
                appendConsonantPieces(pieces,{cursor,v.start},cluster);
            }
        }
        if(carveGlide) {
            const int desired=std::clamp((int)std::lround(0.030*sr),
                std::max(1,(int)std::lround(0.018*sr)),
                std::max(1,(int)std::lround(0.042*sr)));
            const int glideLen=std::min(desired,std::max(1,(v.end-v.start)/4));
            pieces.push_back({{v.start,v.start+glideLen},SpeechPieceKind::ConsonantGlide,cluster.text,0.10,0});
            v.start+=glideLen;
        }
        if(v.end>v.start) {
            const auto& nucleus = plan.nuclei[std::min(i, plan.nuclei.size()-1)];
            if(nucleus.hasOffglide && v.end-v.start >= (int)std::lround(0.070*sr)) {
                // Hold the main vowel; reserve the final diphthong movement (the
                // y in day/birthday) for the end instead of smearing it across
                // the whole note.  The source split is intentionally late.
                const int minTail=std::max(1,(int)std::lround(0.032*sr));
                const int maxTail=std::max(minTail,(int)std::lround(0.075*sr));
                const int desired=std::clamp((v.end-v.start)/4,minTail,maxTail);
                const int split=std::clamp(v.end-desired,v.start+1,v.end-1);
                pieces.push_back({{v.start,split},SpeechPieceKind::Vowel,nucleus.rhotic?"[rhotic-vowel]":"[vowel]",1.0,0});
                pieces.push_back({{split,v.end},SpeechPieceKind::VowelOffglide,"[offglide]",0.18,0});
            } else {
                pieces.push_back({v,SpeechPieceKind::Vowel,nucleus.rhotic?"[rhotic-vowel]":"[vowel]",1.0,0});
            }
        }
        cursor=v.end;
    }
    const auto& trailing=plan.consonants.back();
    if(cursor<word.end) appendConsonantPieces(pieces,{cursor,word.end},trailing);

    // Preserve even very short consonant transients.  Only merge pathological
    // sub-2 ms detector slivers; plosive releases live in exactly the short-time
    // region that previous versions accidentally deleted.
    const int tiny=std::max(1,(int)std::lround(0.002*sr));
    for(size_t i=0;i<pieces.size();) {
        int n=pieces[i].source.end-pieces[i].source.start;
        if(n>=tiny || pieces.size()==1){++i;continue;}
        if(i>0) {
            pieces[i-1].source.end=pieces[i].source.end;
            pieces.erase(pieces.begin()+i);
        } else {
            pieces[1].source.start=pieces[0].source.start;
            pieces.erase(pieces.begin());
        }
    }
    return pieces;
}

static bool isVowelPiece(SpeechPieceKind kind) {
    return kind==SpeechPieceKind::Vowel || kind==SpeechPieceKind::VowelOffglide;
}

static bool isHardConsonant(SpeechPieceKind kind) {
    return kind==SpeechPieceKind::ConsonantTransient || kind==SpeechPieceKind::ConsonantMixed;
}

static bool isSustainableConsonant(SpeechPieceKind kind) {
    return kind==SpeechPieceKind::ConsonantNoiseSustainable ||
           kind==SpeechPieceKind::ConsonantVoicedSustainable ||
           kind==SpeechPieceKind::ConsonantGlide ||
           kind==SpeechPieceKind::ConsonantAspirate;
}

static void allocateSpeechPieceDurations(std::vector<SpeechPiece>& pieces,
                                         int totalTarget,
                                         int sr,
                                         float consonantDrive,
                                         float vowelHold) {
    if(pieces.empty() || totalTarget<=0) return;

    struct Budget { double base=0, weight=0, minimum=0; double maxStretch=0; };
    std::vector<Budget> budgets(pieces.size());
    double baseTotal=0;
    const double drive=std::clamp((double)consonantDrive,0.0,1.0);
    for(size_t i=0;i<pieces.size();++i) {
        const auto& p=pieces[i];
        const double src=std::max(1,p.source.end-p.source.start);
        auto& b=budgets[i];
        if(p.kind==SpeechPieceKind::Vowel) {
            b.minimum=std::min(src,std::max(1.0,0.018*sr));
            b.base=b.minimum;
            b.weight=std::max(src,0.030*sr)*std::clamp((double)vowelHold,0.5,2.0);
        } else if(p.kind==SpeechPieceKind::VowelOffglide) {
            // Diphthong tails should be audible but late.  Do not spend the whole
            // note on the y/ɪ glide; hold the primary vowel and move through the
            // off-glide near the end like a singer does.
            b.minimum=std::min(src,std::max(1.0,0.020*sr));
            b.base=std::clamp(src*0.95,b.minimum,std::max(b.minimum,0.080*sr));
            b.weight=std::max(src,0.018*sr)*0.10;
            b.maxStretch=1.20;
        } else if(p.kind==SpeechPieceKind::ConsonantVoicedSustainable) {
            b.minimum=std::min(src,std::max(1.0,0.010*sr));
            b.base=std::max(b.minimum,src*(0.96+0.08*drive));
            b.weight=std::max(src,0.020*sr)*p.sustainWeight*drive;
            b.maxStretch=2.00;
        } else if(p.kind==SpeechPieceKind::ConsonantNoiseSustainable) {
            b.minimum=std::min(src,std::max(1.0,0.010*sr));
            b.base=std::max(b.minimum,src*(0.98+0.04*drive));
            b.weight=std::max(src,0.018*sr)*p.sustainWeight*drive;
            b.maxStretch=1.45;
        } else if(p.kind==SpeechPieceKind::ConsonantGlide) {
            // y/w/initial-r are musical transitions. Keep enough duration to hear
            // the articulation, but let the following vowel carry most of the note.
            b.minimum=std::min(src,std::max(1.0,0.014*sr));
            b.base=std::max(b.minimum,src*(0.98+0.04*drive));
            b.weight=std::max(src,0.018*sr)*0.06*drive;
            b.maxStretch=1.15;
        } else if(p.kind==SpeechPieceKind::ConsonantAspirate) {
            // /h/ needs presence more than duration. Long h stretches sound like
            // an effect; the dedicated renderer will make the natural aspirate audible.
            b.minimum=std::min(src,std::max(1.0,0.014*sr));
            b.base=std::max(b.minimum,src*(1.00+0.05*drive));
            b.weight=std::max(src,0.016*sr)*0.08*drive;
            b.maxStretch=1.35;
        } else {
            // Stops/affricates carry identity in their closure/release transient.
            // Keep them nearly natural; never use them as the reservoir for a long note.
            const double naturalScale=0.94+0.06*drive;
            b.minimum=std::min(src,std::max(1.0,0.006*sr));
            b.base=std::clamp(src*naturalScale,b.minimum,src*1.06);
            b.weight=0.0;
        }
        baseTotal+=b.base;
    }

    if(baseTotal>totalTarget) {
        // Short melody: shrink vowels/sustainables first while preserving stop attacks.
        double fixed=0,flexBase=0,flexMin=0;
        for(size_t i=0;i<pieces.size();++i) {
            if(isHardConsonant(pieces[i].kind)) fixed+=budgets[i].base;
            else { flexBase+=budgets[i].base; flexMin+=budgets[i].minimum; }
        }
        const double room=std::max(0.0,totalTarget-fixed);
        const double scale=flexBase>flexMin ? std::clamp((room-flexMin)/(flexBase-flexMin),0.0,1.0) : 0.0;
        for(size_t i=0;i<pieces.size();++i) {
            double value=budgets[i].base;
            if(!isHardConsonant(pieces[i].kind))
                value=budgets[i].minimum+(budgets[i].base-budgets[i].minimum)*scale;
            pieces[i].outputSamples=std::max(0,(int)std::lround(value));
        }
    } else {
        const double extra=totalTarget-baseTotal;
        double weightTotal=0; for(const auto& b:budgets) weightTotal+=b.weight;
        if(weightTotal<=1e-9) {
            for(size_t i=0;i<pieces.size();++i)
                if(pieces[i].kind==SpeechPieceKind::Vowel) budgets[i].weight=1.0;
            weightTotal=0; for(const auto& b:budgets) weightTotal+=b.weight;
        }
        for(size_t i=0;i<pieces.size();++i) {
            const double value=budgets[i].base + (weightTotal>0 ? extra*budgets[i].weight/weightTotal : 0.0);
            pieces[i].outputSamples=std::max(0,(int)std::lround(value));
        }
    }

    // Cap non-vowel musical articulations. This is also what guarantees the
    // one-pass noise stretcher never needs to slow an s/f/h so far that it turns
    // into a repeated or obviously resampled effect.
    int reclaimed=0;
    const bool hasMainVowel=std::any_of(pieces.begin(),pieces.end(),[](const SpeechPiece& p){return p.kind==SpeechPieceKind::Vowel;});
    if(hasMainVowel) {
        for(size_t i=0;i<pieces.size();++i) {
            auto& p=pieces[i];
            const int sourceLen=std::max(1,p.source.end-p.source.start);
            const double factor=budgets[i].maxStretch;
            if(factor<=0) continue;
            const int cap=std::max(sourceLen,(int)std::lround(sourceLen*factor));
            if(p.outputSamples>cap){reclaimed+=p.outputSamples-cap;p.outputSamples=cap;}
        }
        if(reclaimed>0) {
            size_t vowel=pieces.size(); int longest=-1;
            for(size_t i=0;i<pieces.size();++i) if(pieces[i].kind==SpeechPieceKind::Vowel) {
                int n=pieces[i].source.end-pieces[i].source.start;
                if(n>longest){longest=n;vowel=i;}
            }
            if(vowel<pieces.size()) pieces[vowel].outputSamples+=reclaimed;
        }
    }

    int sum=0; for(const auto&p:pieces) sum+=p.outputSamples;
    const int delta=totalTarget-sum;
    // Rounding and shortfall correction belongs in the principal vowel first,
    // then an off-glide/sustainable consonant. Never dump it into a plosive.
    size_t best=pieces.size(); int bestRank=-1,bestLen=-1;
    for(size_t i=0;i<pieces.size();++i) {
        const int rank=pieces[i].kind==SpeechPieceKind::Vowel?3:
            pieces[i].kind==SpeechPieceKind::VowelOffglide?2:
            isSustainableConsonant(pieces[i].kind)?1:0;
        const int len=pieces[i].source.end-pieces[i].source.start;
        if(rank>bestRank || (rank==bestRank && len>bestLen)){best=i;bestRank=rank;bestLen=len;}
    }
    if(best<pieces.size()) pieces[best].outputSamples=std::max(1,pieces[best].outputSamples+delta);
}

static void appendCrossfaded(std::vector<float>& dst,
                              const std::vector<float>& src,
                              int overlap);

static void appendCrossfaded(std::vector<float>& dst,
                              const std::vector<float>& src,
                              int overlap) {
    if(src.empty()) return;
    if(dst.empty() || overlap<=0) {
        dst.reserve(dst.size()+src.size());
        for(float sample:src) dst.push_back(sample);
        return;
    }
    overlap=std::min({overlap,(int)dst.size(),(int)src.size()});
    if(overlap<=0){
        dst.reserve(dst.size()+src.size());
        for(float sample:src) dst.push_back(sample);
        return;
    }
    size_t at=dst.size()-overlap;
    for(int i=0;i<overlap;++i) {
        float t=(i+1)/(float)(overlap+1);
        // Raised-cosine, constant-sum crossfade. Equal-power was appropriate
        // for unrelated audio, but adjacent phoneme pieces often share phase;
        // adding two sqrt gains could create a short +3 dB pulse at the join.
        float u=0.5f-0.5f*std::cos(3.14159265358979323846f*t);
        dst[at+i]=dst[at+i]*(1.0f-u)+src[i]*u;
    }
    for(size_t i=(size_t)overlap;i<src.size();++i) dst.push_back(src[i]);
}

static RenderedWord renderWord(const Audio& in,
                               const std::vector<PitchFrame>& frames,
                               Region wordRegion,
                               const LyricWord& lyric,
                               const WordTarget& target,
                               const ResoneVocalRenderOptions& options,
                               double voiceReferenceHz,
                               double& previousTargetHz,
                               bool phraseStart) {
    if(target.segments.empty()) return {};
    wordRegion.start=std::clamp(wordRegion.start,0,(int)in.samples.size());
    wordRegion.end=std::clamp(wordRegion.end,wordRegion.start,(int)in.samples.size());
    if(wordRegion.end<=wordRegion.start) return {};

    std::vector<int> targetLengths;
    int totalTarget=0;
    for(const auto&s:target.segments) {
        int n=std::max(1,(int)std::lround((s.end-s.start)*in.sampleRate));
        targetLengths.push_back(n); totalTarget+=n;
    }

    // Build the MIDI pitch contour independently from the source phoneme layout.
    // Every voiced region will read this same continuous contour at its location.
    std::vector<PitchSegment> contour;
    contour.reserve(target.segments.size());
    double wordSourceHz=medianPitch(frames,wordRegion.start,wordRegion.end);
    wordSourceHz=normalizePitchOctave(wordSourceHz,voiceReferenceHz);
    if(wordSourceHz<40) wordSourceHz=voiceReferenceHz>=40?voiceReferenceHz:110;
    int pos=0; double prior=phraseStart?0.0:previousTargetHz;
    float vowelHold=0,consonantDrive=0; double guidanceWeight=0;
    for(size_t i=0;i<target.segments.size();++i) {
        const auto&s=target.segments[i]; const auto&g=s.guidance;
        double targetMidi=foldMidiFloatToRange(s.note+g.pitchOffsetSemitones,options.singingMinMidiNote,options.singingMaxMidiNote);
        double targetHz=440.0*std::pow(2.0,(targetMidi-69.0)/12.0);
        double corrected=wordSourceHz*std::pow(targetHz/wordSourceHz,
            options.pitchCorrectionStrength*std::clamp(g.melodyInfluence,0.0f,1.0f));
        float velGain=0.65f+0.35f*(s.velocity/127.0f);
        float accentGain=std::pow(10.0f,(std::clamp(g.accent,-1.0f,1.0f)*4.0f)/20.0f);
        double slide=g.slideSeconds;
        if(slide<=0 && prior>40 && corrected>40) {
            double cents=std::abs(1200.0*std::log2(corrected/prior));
            if(cents>8.0) {
                slide=std::clamp(0.018+cents/12000.0,0.018,0.052);
                slide=std::min(slide,std::max(0.0,(s.end-s.start)*0.30));
            }
        }
        contour.push_back({pos,pos+targetLengths[i],corrected,prior,slide,velGain*accentGain});
        prior=corrected; pos+=targetLengths[i];
        double w=s.end-s.start; guidanceWeight+=w;
        vowelHold+=std::clamp(g.vowelHold,0.5f,2.0f)*(float)w;
        consonantDrive+=std::clamp(g.consonantDrive,0.0f,1.0f)*(float)w;
    }
    previousTargetHz=prior;
    if(guidanceWeight>0){vowelHold/=(float)guidanceWeight;consonantDrive/=(float)guidanceWeight;}
    else {vowelHold=1.25f;consonantDrive=0.7f;}

    auto pieces=splitWordSpeechPieces(in,frames,wordRegion,in.sampleRate,lyric);
    allocateSpeechPieceDurations(pieces,totalTarget,in.sampleRate,consonantDrive,vowelHold);

    RenderedWord rendered;
    rendered.body.reserve(totalTarget+(int)pieces.size()*32);
    int logicalOffset=0;
    const int smoothJoin=std::max(1,(int)std::lround(0.0040*in.sampleRate));
    const int transientJoin=std::max(1,(int)std::lround(0.0015*in.sampleRate));
    for(size_t i=0;i<pieces.size();++i) {
        auto&p=pieces[i];
        if(p.outputSamples<=0) continue;
        int requestedJoin=isHardConsonant(p.kind) ? transientJoin : smoothJoin;
        int overlap=i==0?0:std::min(requestedJoin,std::max(0,p.outputSamples/5));
        int renderSamples=p.outputSamples+overlap;
        int contourOffset=std::max(0,logicalOffset-overlap);
        std::vector<float> piece;
        std::vector<float> source(in.samples.begin()+p.source.start,in.samples.begin()+p.source.end);

        if(p.kind==SpeechPieceKind::Vowel || p.kind==SpeechPieceKind::VowelOffglide) {
            double pieceHz=normalizePitchOctave(medianPitch(frames,p.source.start,p.source.end),wordSourceHz);
            if(pieceHz<40) pieceHz=wordSourceHz;
            const float vibratoScale=p.kind==SpeechPieceKind::VowelOffglide?0.15f:1.0f;
            piece=psolaSustain(source,p.source.start,frames,in.sampleRate,pieceHz,voiceReferenceHz,
                renderSamples,contourOffset,contour,options.vibratoDepthCents*vibratoScale,options.vibratoRateHz,options.formantPreserve);
            applyRegisterTimbreCompensation(piece,pieceHz,contour,options.formantPreserve);
        } else if(p.kind==SpeechPieceKind::ConsonantVoicedSustainable) {
            // m/n/l/v/z/ng/zh may genuinely carry a sung fundamental.
            double consonantHz=normalizePitchOctave(medianPitch(frames,p.source.start,p.source.end),wordSourceHz);
            if(consonantHz>=40) {
                piece=psolaSustain(source,p.source.start,frames,in.sampleRate,consonantHz,voiceReferenceHz,
                    renderSamples,contourOffset,contour,options.vibratoDepthCents*0.42f,options.vibratoRateHz,options.formantPreserve);
            } else {
                piece=resone::vocals::renderConsonantOnce(source,renderSamples);
            }
        } else if(p.kind==SpeechPieceKind::ConsonantGlide) {
            // y/w/r are vocal-tract transitions into the vowel, not mini-notes.
            // Pitch-synchronous resynthesis of /r/ was the source of the growly
            // gargled artifact; preserve the natural formant transition once.
            piece=resone::vocals::renderGlideTransition(source,renderSamples,in.sampleRate);
        } else if(p.kind==SpeechPieceKind::ConsonantAspirate) {
            // /h/ is a breathy lead-in. Give it presence, but never loop it or
            // force a fake pitch onto the noise.
            piece=resone::vocals::renderAspirate(source,renderSamples,in.sampleRate);
        } else if(p.kind==SpeechPieceKind::ConsonantNoiseSustainable) {
            // s/f/sh/th carry musical duration without a fundamental. The
            // one-pass warp consumes the consonant exactly once.
            piece=resone::vocals::stretchNoiseConsonantMonotonic(source,renderSamples,in.sampleRate);
        } else {
            // Stops/affricates and mixed clusters are single-consumption events.
            piece=resone::vocals::renderConsonantOnce(source,renderSamples);
        }
        appendCrossfaded(rendered.body,piece,overlap);
        logicalOffset+=p.outputSamples;
    }
    rendered.body.resize(totalTarget,0.0f);
    return rendered;
}

static void mixRenderedWord(std::vector<float>& dst,
                            const RenderedWord& rendered,
                            const WordTarget& target,
                            int sr,
                            bool phraseStart,
                            bool phraseEnd,
                            double phraseStartSeconds,
                            double phraseEndSeconds) {
    if(target.segments.empty() || rendered.body.empty()) return;
    size_t cursor=0;
    float previousGain=1.0f;
    for(size_t si=0;si<target.segments.size();++si) {
        const auto&s=target.segments[si];
        int len=std::max(1,(int)std::lround((s.end-s.start)*sr));
        int start=(int)std::lround(s.start*sr);
        if(start<0){cursor+=len;continue;}
        if((size_t)(start+len)>dst.size()) dst.resize(start+len,0);
        float velGain=0.65f+0.35f*(s.velocity/127.0f);
        float accentGain=std::pow(10.0f,(std::clamp(s.guidance.accent,-1.0f,1.0f)*4.0f)/20.0f);
        float gain=velGain*accentGain;
        bool gapBefore=si>0 && s.start-target.segments[si-1].end>0.015;
        bool gapAfter=si+1<target.segments.size() && target.segments[si+1].start-s.end>0.015;
        int edgeFade=std::min(len,std::max(1,(int)std::lround(0.003*sr)));
        int gainRamp=std::min(len,std::max(1,(int)std::lround(0.020*sr)));

        // At a touching word boundary, remove only the instantaneous sample
        // offset. Do not extrapolate the previous waveform slope into the new
        // word: that can overwrite the beginning of a real consonant transient.
        int wordJoin=(si==0 && !phraseStart && start>=2)
            ? std::min(len,std::max(1,(int)std::lround(0.0025*sr))) : 0;
        float prev=wordJoin?dst[start-1]:0.0f;
        float joinOffset=0.0f;
        bool joinOffsetReady=false;

        for(int i=0;i<len && cursor+(size_t)i<rendered.body.size();++i) {
            float g=gain;
            if(si>0 && !gapBefore && i<gainRamp) {
                float u=i/(float)gainRamp;
                g=previousGain+(gain-previousGain)*u;
            }
            if((phraseStart && si==0) || gapBefore) {
                if(i<edgeFade) g*=i/(float)edgeFade;
            }
            if((phraseEnd && si+1==target.segments.size()) || gapAfter) {
                int release=phraseEnd?std::min(len,std::max(edgeFade,(int)std::lround(0.016*sr))):edgeFade;
                if(len-1-i<release) g*=std::max(0,len-1-i)/(float)release;
            }

            // One gentle phrase envelope keeps the vocal line breathing as a
            // single performance instead of amplitude-resetting at each word.
            double absoluteSeconds=(start+i)/(double)sr;
            double phraseSpan=std::max(0.05,phraseEndSeconds-phraseStartSeconds);
            double pu=std::clamp((absoluteSeconds-phraseStartSeconds)/phraseSpan,0.0,1.0);
            double phraseGain=pu<0.65
                ? (0.96+(1.055-0.96)*(pu/0.65))
                : (1.055+(0.94-1.055)*((pu-0.65)/0.35));
            float sample=rendered.body[cursor+i]*g*(float)phraseGain;
            if(i<wordJoin) {
                if(!joinOffsetReady){joinOffset=prev-sample;joinOffsetReady=true;}
                float u=(i+1)/(float)(wordJoin+1);
                float decay=(1.0f-u)*(1.0f-u);
                sample+=joinOffset*decay;
            }
            dst[start+i]+=sample;
        }
        previousGain=gain;
        cursor+=len;
    }
}

} // namespace

extern "C" RESONE_VOCALS_API int resone_analyze_voice_profile(
    const char* inputWavPath,
    int minPitchHz,
    int maxPitchHz,
    ResoneVoicePitchProfile* profile,
    char* errorBuffer,
    int errorBufferLength) {
    try {
        if(!inputWavPath || !profile) throw std::runtime_error("input WAV and profile output are required");
        Audio in=readWav(inputWavPath);
        minPitchHz=std::clamp(minPitchHz,40,400);
        maxPitchHz=std::clamp(maxPitchHz,std::max(80,minPitchHz+20),1400);
        auto frames=analyze(in,minPitchHz,maxPitchHz);
        std::vector<double> voiced;
        voiced.reserve(frames.size());
        for(const auto& f:frames) if(f.voiced && f.f0>=minPitchHz && f.f0<=maxPitchHz) voiced.push_back(f.f0);
        if(voiced.size()<8) throw std::runtime_error("Could not detect enough stable voiced audio to determine this voice's natural octave");
        std::sort(voiced.begin(),voiced.end());
        double firstMedian=voiced[voiced.size()/2];
        for(double& hz:voiced) hz=normalizePitchOctave(hz,firstMedian);
        std::sort(voiced.begin(),voiced.end());
        auto percentile=[&](double q){
            if(voiced.empty()) return 0.0;
            double pos=q*(voiced.size()-1);
            size_t lo=(size_t)std::floor(pos), hi=std::min(voiced.size()-1,lo+1);
            double u=pos-lo;
            return voiced[lo]+(voiced[hi]-voiced[lo])*u;
        };
        // Spoken references often sit well above the bottom of the comfortable
        // singing register. Use the lower stable voiced quintile as the observed
        // anchor, then normalize high speaking voices down one octave for the
        // practical singing anchor. This keeps a typical female reference whose
        // speech sits around C4-D4 in a C3-B4 working range rather than C4-B5.
        double observedBase=percentile(0.20), low=percentile(0.10), high=percentile(0.90);
        int observedMidi=std::clamp(hzToMidi(observedBase),0,127);
        int baseMidi=observedMidi>=60 ? std::max(0,observedMidi-12) : observedMidi;
        double base=440.0*std::pow(2.0,(baseMidi-69.0)/12.0);
        int baseOctave=baseMidi/12-1;
        int singingMin=std::clamp(12*(baseOctave+1),0,127); // C of practical base octave
        int singingMax=std::clamp(singingMin+23,0,127);    // through B of next octave
        if(singingMax-singingMin<11) singingMin=std::max(0,singingMax-23);
        profile->basePitchHz=(float)base;
        profile->lowPitchHz=(float)low;
        profile->highPitchHz=(float)high;
        profile->voicedFraction=frames.empty()?0.0f:(float)(voiced.size()/(double)frames.size());
        profile->baseMidiNote=baseMidi;
        profile->baseOctave=baseOctave;
        profile->singingMinMidiNote=singingMin;
        profile->singingMaxMidiNote=singingMax;
        return 0;
    } catch(const std::exception& ex) {
        if(errorBuffer && errorBufferLength>0) {
            std::snprintf(errorBuffer,(size_t)errorBufferLength,"%s",ex.what());
            errorBuffer[errorBufferLength-1]='\0';
        }
        return 1;
    } catch(...) {
        if(errorBuffer && errorBufferLength>0) {
            std::snprintf(errorBuffer,(size_t)errorBufferLength,"Unknown voice range analysis failure");
            errorBuffer[errorBufferLength-1]='\0';
        }
        return 2;
    }
}

extern "C" RESONE_VOCALS_API int resone_render_singing(
    const char* inputWavPath,
    const char* spokenTextUtf8,
    const char* midiPath,
    const ResoneVocalGuidanceEvent* guidance,
    int guidanceCount,
    const ResoneVocalRenderOptions* options,
    const char* outputWavPath,
    char* errorBuffer,
    int errorBufferLength) {
    try {
        if(!inputWavPath||!midiPath||!outputWavPath) throw std::runtime_error("input WAV, MIDI and output WAV are required");
        ResoneVocalRenderOptions o{1.0f,0.8f,18.0f,5.2f,0.95f,50,800,-1,-1,-1};
        if(options) o=*options;
        o.pitchCorrectionStrength=std::clamp(o.pitchCorrectionStrength,0.0f,1.0f);
        o.outputGain=std::clamp(o.outputGain,0.05f,2.0f);
        Audio in=readWav(inputWavPath);
        auto notes=readMidi(midiPath);
        auto words=lyricWords(spokenTextUtf8?spokenTextUtf8:"");
        if(words.empty()) throw std::runtime_error("No lyric words were found in the supplied vocal text");
        auto frames=analyze(in,std::max(40,o.minPitchHz),std::max(o.minPitchHz+20,o.maxPitchHz));
        auto speechWords=fitSpeechToWords(in,words);
        if(speechWords.size()!=words.size()) throw std::runtime_error("Could not align generated speech to lyric words");
        auto targets=buildWordTargets(notes,words,guidance,guidanceCount,o);
        double voiceReferenceHz=medianPitch(frames,0,(int)in.samples.size());
        if(voiceReferenceHz<40) voiceReferenceHz=110;

        double endSec=notes.back().end+0.5;
        Audio out; out.sampleRate=in.sampleRate; out.samples.assign((size_t)(endSec*in.sampleRate),0);
        double previousTargetHz=0;
        std::vector<size_t> activeWords;
        for(size_t i=0;i<words.size();++i) if(!targets[i].segments.empty()) activeWords.push_back(i);
        constexpr double kPhraseGapSeconds=0.14;
        for(size_t ai=0;ai<activeWords.size();++ai) {
            size_t i=activeWords[ai];
            bool phraseStart=ai==0 || targetStart(targets[i])-targetEnd(targets[activeWords[ai-1]])>kPhraseGapSeconds;
            bool phraseEnd=ai+1==activeWords.size() || targetStart(targets[activeWords[ai+1]])-targetEnd(targets[i])>kPhraseGapSeconds;
            if(phraseStart) previousTargetHz=0;
            size_t phraseFirst=ai;
            while(phraseFirst>0 && targetStart(targets[activeWords[phraseFirst]])-targetEnd(targets[activeWords[phraseFirst-1]])<=kPhraseGapSeconds) --phraseFirst;
            size_t phraseLast=ai;
            while(phraseLast+1<activeWords.size() && targetStart(targets[activeWords[phraseLast+1]])-targetEnd(targets[activeWords[phraseLast]])<=kPhraseGapSeconds) ++phraseLast;
            double phraseStartSeconds=targetStart(targets[activeWords[phraseFirst]]);
            double phraseEndSeconds=targetEnd(targets[activeWords[phraseLast]]);
            auto rendered=renderWord(in,frames,speechWords[i],words[i],targets[i],o,voiceReferenceHz,previousTargetHz,phraseStart);
            mixRenderedWord(out.samples,rendered,targets[i],in.sampleRate,phraseStart,phraseEnd,phraseStartSeconds,phraseEndSeconds);
        }

        // Remove only sub-rumble introduced by long overlap-add tails. 35 Hz is
        // deliberately below the supported male vocal range and does not act as
        // a bass-cut EQ on the singer.
        if(!out.samples.empty()) {
            const double rc=1.0/(2.0*kPi*35.0), dt=1.0/in.sampleRate;
            const float alpha=(float)(rc/(rc+dt));
            float prevIn=out.samples[0],prevOut=out.samples[0];
            for(size_t i=1;i<out.samples.size();++i) {
                float inSample=out.samples[i];
                float hp=alpha*(prevOut+inSample-prevIn);
                out.samples[i]=hp; prevIn=inSample; prevOut=hp;
            }
        }

        // Final first-sound de-click.  Phrase envelopes normally start at zero,
        // but source/DC/filter state can still leave a one-sample glip before the
        // first audible articulation.  Fade only the first 2.5 ms of actual sound
        // so later consonants and the initial /h/ keep their full articulation.
        if(!out.samples.empty()) {
            size_t first=0;
            while(first<out.samples.size() && std::abs(out.samples[first])<1.0e-5f) ++first;
            const int n=std::max(1,(int)std::lround(0.0025*in.sampleRate));
            for(int i=0;i<n && first+(size_t)i<out.samples.size();++i) {
                const float u=(i+1)/(float)(n+1);
                const float g=0.5f-0.5f*std::cos((float)kPi*u);
                out.samples[first+i]*=g;
            }
        }

        // Soft limiter / normalize.
        float peak=0; for(float x:out.samples) peak=std::max(peak,std::abs(x));
        float gain=o.outputGain; if(peak*gain>0.98f) gain=0.98f/std::max(peak,1e-6f);
        for(float&x:out.samples) x=std::tanh(x*gain);
        writeWav(outputWavPath,out);
        setError(errorBuffer,errorBufferLength,"");
        return 0;
    } catch(const std::exception& e) {
        setError(errorBuffer,errorBufferLength,e.what()); return 1;
    } catch(...) {
        setError(errorBuffer,errorBufferLength,"Unknown vocal synthesis error"); return 2;
    }
}
