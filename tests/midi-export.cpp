#include "MidiExport.hpp"
#include <cassert>
#include <fstream>
int main(int argc,char** argv){
 auto j=resone::Json::parse(R"({"tempo":120,"meter":"4/4","lanes":[{"id":"melody","name":"Melody","program":24,"notes":[{"pitch":61,"start":0.5,"duration":1.5,"velocity":73},{"pitch":65,"start":0.5,"duration":1.5,"velocity":95},{"pitch":67,"start":2,"duration":0.25,"velocity":90}]},{"id":"drums","name":"Drums","drums":true,"bank":128,"notes":[{"pitch":36,"start":0,"duration":0.25},{"pitch":42,"start":0.5,"duration":0.25}]}]})");
 for(auto id:{"melody","drums"}){auto bytes=resone::laneMidi(j,id);assert(bytes.size()>22);std::ofstream f(std::string(argv[1])+"/"+id+".mid",std::ios::binary);f.write((const char*)bytes.data(),bytes.size());}
 bool rejected=false;try{resone::laneMidi(j,"missing");}catch(...){rejected=true;}assert(rejected);
}
