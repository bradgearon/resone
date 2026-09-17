#pragma once
#include "WindowsSupport.hpp"
#include <atomic>
#include <ole2.h>
#include <shellapi.h>
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
class MidiFileDataObject final:public IDataObject{
 std::atomic<ULONG> refs_{1};
 std::wstring path_;
 static FORMATETC format(){return FORMATETC{CF_HDROP,nullptr,DVASPECT_CONTENT,-1,TYMED_HGLOBAL};}
 public:
 explicit MidiFileDataObject(std::wstring path):path_(std::move(path)){}
 HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id,void** out)override{
  if(!out)return E_POINTER;*out=nullptr;
  if(id==IID_IUnknown||id==IID_IDataObject){*out=static_cast<IDataObject*>(this);AddRef();return S_OK;}
  return E_NOINTERFACE;
 }
 ULONG STDMETHODCALLTYPE AddRef()override{return ++refs_;}
 ULONG STDMETHODCALLTYPE Release()override{auto n=--refs_;if(!n)delete this;return n;}
 HRESULT STDMETHODCALLTYPE GetData(FORMATETC* requested,STGMEDIUM* medium)override{
  if(!requested||!medium)return E_POINTER;
  if(FAILED(QueryGetData(requested)))return DV_E_FORMATETC;
  const SIZE_T chars=path_.size()+2;
  const SIZE_T bytes=sizeof(DROPFILES)+chars*sizeof(wchar_t);
  HGLOBAL memory=GlobalAlloc(GMEM_MOVEABLE|GMEM_ZEROINIT,bytes);if(!memory)return STG_E_MEDIUMFULL;
  auto* drop=static_cast<DROPFILES*>(GlobalLock(memory));if(!drop){GlobalFree(memory);return STG_E_MEDIUMFULL;}
  drop->pFiles=sizeof(DROPFILES);drop->fWide=TRUE;
  auto* names=reinterpret_cast<wchar_t*>(reinterpret_cast<BYTE*>(drop)+sizeof(DROPFILES));
  memcpy(names,path_.c_str(),path_.size()*sizeof(wchar_t));
  names[path_.size()]=L'\0';names[path_.size()+1]=L'\0';
  GlobalUnlock(memory);
  medium->tymed=TYMED_HGLOBAL;medium->hGlobal=memory;medium->pUnkForRelease=nullptr;return S_OK;
 }
 HRESULT STDMETHODCALLTYPE GetDataHere(FORMATETC*,STGMEDIUM*)override{return DATA_E_FORMATETC;}
 HRESULT STDMETHODCALLTYPE QueryGetData(FORMATETC* requested)override{
  if(!requested)return E_POINTER;
  if(requested->cfFormat!=CF_HDROP)return DV_E_FORMATETC;
  if(!(requested->tymed&TYMED_HGLOBAL))return DV_E_TYMED;
  if(requested->dwAspect!=DVASPECT_CONTENT)return DV_E_DVASPECT;
  return S_OK;
 }
 HRESULT STDMETHODCALLTYPE GetCanonicalFormatEtc(FORMATETC*,FORMATETC* out)override{if(out)out->ptd=nullptr;return E_NOTIMPL;}
 HRESULT STDMETHODCALLTYPE SetData(FORMATETC*,STGMEDIUM*,BOOL)override{return E_NOTIMPL;}
 HRESULT STDMETHODCALLTYPE EnumFormatEtc(DWORD direction,IEnumFORMATETC** out)override{
  if(!out)return E_POINTER;*out=nullptr;if(direction!=DATADIR_GET)return E_NOTIMPL;
  auto f=format();return SHCreateStdEnumFmtEtc(1,&f,out);
 }
 HRESULT STDMETHODCALLTYPE DAdvise(FORMATETC*,DWORD,IAdviseSink*,DWORD*)override{return OLE_E_ADVISENOTSUPPORTED;}
 HRESULT STDMETHODCALLTYPE DUnadvise(DWORD)override{return OLE_E_ADVISENOTSUPPORTED;}
 HRESULT STDMETHODCALLTYPE EnumDAdvise(IEnumSTATDATA**)override{return OLE_E_ADVISENOTSUPPORTED;}
};
inline void dragMidiBytes(const std::vector<uint8_t>& data,bool fullProject){
 if(!(GetAsyncKeyState(VK_LBUTTON)&0x8000))throw std::runtime_error("Press and hold the left mouse button to drag MIDI.");
 if(data.empty())throw std::runtime_error("Local Resonator returned no MIDI data.");
 auto directory=preferences()/"midi-drags";std::filesystem::create_directories(directory);
 GUID guid;if(FAILED(CoCreateGuid(&guid)))throw std::runtime_error("Could not create a MIDI drag file name.");
 wchar_t name[40];StringFromGUID2(guid,name,40);
 auto prefix=fullProject?std::wstring(L"Resone-Full-"):std::wstring(L"Resone-");
 auto path=std::filesystem::absolute(directory/(prefix+name+L".mid"));
 {std::ofstream out(path,std::ios::binary);out.write(reinterpret_cast<const char*>(data.data()),data.size());if(!out)throw std::runtime_error("Could not stage MIDI drag file.");}
 HRESULT init=OleInitialize(nullptr);if(FAILED(init))throw std::runtime_error("MIDI drag requires an STA editor thread.");
 auto object=new MidiFileDataObject(path.wstring());auto source=new MidiDropSource();DWORD effect=0;
 ReleaseCapture();
 auto hr=DoDragDrop(object,source,DROPEFFECT_COPY,&effect);
 source->Release();object->Release();OleUninitialize();
 if(FAILED(hr))throw std::runtime_error(fullProject?"Windows could not start multitrack MIDI file drag.":"Windows could not start MIDI file drag.");
}


}
