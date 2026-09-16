#pragma once
#include "Model.hpp"
#include <cstdint>
namespace resone {
// Build the current edited lane locally so a drag never waits for an API response.
inline std::vector<uint8_t> laneMidi(const Json& project,const std::string& laneId) {
 auto song=parseSong(project);auto it=std::find_if(song.lanes.begin(),song.lanes.end(),[&](const Lane& l){return l.id==laneId;});
 if(it==song.lanes.end()||it->notes.empty())throw std::runtime_error("This lane has no MIDI notes to drag.");
 std::vector<uint8_t> track;
 auto vlq=[](std::vector<uint8_t>& out,uint32_t v){uint8_t b[4];int n=0;b[n++]=v&127;while((v>>=7)!=0)b[n++]=(v&127)|128;while(n)out.push_back(b[--n]);};
 const int tempo=int(std::llround(60000000/song.tempo));track={0,0xff,0x51,3,uint8_t(tempo>>16),uint8_t(tempo>>8),uint8_t(tempo)};
 auto meter=project.value("meter",std::string("4/4"));auto slash=meter.find('/');if(slash==std::string::npos)throw std::runtime_error("Invalid meter.");
 int numerator=std::stoi(meter.substr(0,slash)),denominator=std::stoi(meter.substr(slash+1)),power=0;
 if(numerator<1||numerator>12||(denominator!=2&&denominator!=4&&denominator!=8&&denominator!=16))throw std::runtime_error("Invalid meter.");
 while((1<<power)<denominator)++power;track.insert(track.end(),{0,0xff,0x58,4,uint8_t(numerator),uint8_t(power),24,8});
 int channel=it->drums?9:0;
 track.insert(track.end(),{0,uint8_t(0xb0|channel),0,uint8_t(it->drums?0:it->bank),0,uint8_t(0xc0|channel),uint8_t(it->program)});
 struct E{uint32_t tick;int pitch,velocity;bool on;};std::vector<E> events;
 for(const auto& n:it->notes){auto start=uint32_t(std::llround(n.start*480));auto end=std::max(start+1,uint32_t(std::llround((n.start+n.duration)*480)));events.push_back({start,n.pitch,n.velocity,true});events.push_back({end,n.pitch,0,false});}
 std::stable_sort(events.begin(),events.end(),[](const E&a,const E&b){return a.tick==b.tick?a.on<b.on:a.tick<b.tick;});uint32_t tick=0;
 for(const auto& e:events){vlq(track,e.tick-tick);track.insert(track.end(),{uint8_t((e.on?0x90:0x80)|channel),uint8_t(e.pitch),uint8_t(e.velocity)});tick=e.tick;}
 track.insert(track.end(),{0,0xff,0x2f,0});std::vector<uint8_t> result={'M','T','h','d',0,0,0,6,0,0,0,1,1,0xe0,'M','T','r','k'};
 uint32_t size=uint32_t(track.size());for(int shift=24;shift>=0;shift-=8)result.push_back(uint8_t(size>>shift));result.insert(result.end(),track.begin(),track.end());return result;
}
}
