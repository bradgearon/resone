#pragma once
#include "WindowsSupport.hpp"
#include "MidiExport.hpp"
#include <atomic>
#include <shlobj.h>
#include <shlwapi.h>
namespace resone {
class MidiDropSource final:public IDropSource{
 std::atomic<ULONG> refs_{1};
 public:
 HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id,void** out)override{if(!out)return E_POINTER;*out=nullptr;if(id==IID_IUnknown||id==IID_IDropSource){*out=static_cast<IDropSource*>(this);AddRef();return S_OK;}return E_NOINTERFACE;}
 ULONG STDMETHODCALLTYPE AddRef()override{return ++refs_;}
 ULONG STDMETHODCALLTYPE Release()override{auto n=--refs_;if(!n)delete this;return n;}
 HRESULT STDMETHODCALLTYPE QueryContinueDrag(BOOL escape,DWORD keys)override{return escape?DRAGDROP_S_CANCEL:!(keys&MK_LBUTTON)?DRAGDROP_S_DROP:S_OK;}
 HRESULT STDMETHODCALLTYPE GiveFeedback(DWORD)override{return DRAGDROP_S_USEDEFAULTCURSORS;}
};
inline void dragLane(const Json& project,const std::string& id){
 if(!(GetAsyncKeyState(VK_LBUTTON)&0x8000))return;
 auto data=laneMidi(project,id);auto directory=preferences()/"midi-drags";std::filesystem::create_directories(directory);
 // Keep files for deferred DAW import. They can be removed explicitly when Resone is closed.
 GUID guid;CoCreateGuid(&guid);wchar_t name[40];StringFromGUID2(guid,name,40);auto path=directory/(std::wstring(L"Resone-")+name+L".mid");
 {std::ofstream out(path,std::ios::binary);out.write(reinterpret_cast<const char*>(data.data()),data.size());if(!out)throw std::runtime_error("Could not stage MIDI drag file.");}
 HRESULT init=OleInitialize(nullptr);if(FAILED(init))throw std::runtime_error("MIDI drag requires an STA editor thread.");
 PIDLIST_ABSOLUTE full=nullptr;IDataObject* object=nullptr;auto hr=SHParseDisplayName(path.c_str(),nullptr,&full,0,nullptr);
 if(SUCCEEDED(hr)){
  auto parent=ILCloneFull(full);if(!parent){CoTaskMemFree(full);OleUninitialize();throw std::bad_alloc();}
  // SHCreateDataObject takes an array of pointers to const child item IDs.
  PCUITEMID_CHILD child=ILFindLastID(full);
  ILRemoveLastID(parent);hr=SHCreateDataObject(parent,1,&child,nullptr,IID_IDataObject,reinterpret_cast<void**>(&object));CoTaskMemFree(parent);
 }
 if(SUCCEEDED(hr)){auto source=new MidiDropSource();DWORD effect=0;hr=DoDragDrop(object,source,DROPEFFECT_COPY,&effect);source->Release();object->Release();}
 CoTaskMemFree(full);OleUninitialize();if(FAILED(hr))throw std::runtime_error("Windows could not start MIDI file drag.");
}
}
