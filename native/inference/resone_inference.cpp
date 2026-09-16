#include "llama.h"
#include "chat.h"
#include "json.h"
#include <algorithm>
#include <cstring>
#include <memory>
#include <mutex>
#include <stdexcept>
#include <string>
#include <vector>
#ifdef _WIN32
#define API extern "C" __declspec(dllexport)
#else
#define API extern "C" __attribute__((visibility("default")))
#endif
using callback = int(*)(void*,const char*,int);
using cancelled = int(*)(void*);
struct session {
 llama_model* model{}; llama_context* ctx{}; std::mutex lock;
 ~session(){if(ctx)llama_free(ctx);if(model)llama_model_free(model);}
};
static void error(char* out,int size,const char* text){if(out&&size>0){std::strncpy(out,text,size-1);out[size-1]=0;}}
API int resone_inference_abi(){return 1;}
API void* resone_model_open(const char* path,int context,int gpuLayers,int threads,char* err,int size){
 try{
 static std::once_flag init;std::call_once(init,[]{llama_backend_init();});
 auto s=std::make_unique<session>();auto mp=llama_model_default_params();mp.n_gpu_layers=gpuLayers;
 s->model=llama_model_load_from_file(path,mp);if(!s->model)throw std::runtime_error("Model load failed; check model path, runtime backend and available memory.");
 auto cp=llama_context_default_params();cp.n_ctx=context;cp.n_batch=512;cp.n_threads=threads;cp.n_threads_batch=threads;
 s->ctx=llama_init_from_model(s->model,cp);if(!s->ctx)throw std::runtime_error("Model context allocation failed.");return s.release();
 }catch(const std::exception& e){error(err,size,e.what());return nullptr;}catch(...){error(err,size,"Native model load failed.");return nullptr;}
}
API void resone_model_close(void* ptr){delete static_cast<session*>(ptr);}
API int resone_model_generate(void* ptr,const char* messages,int maxTokens,callback emit,cancelled cancel,void* user,char* err,int size){
 try{
 auto& s=*static_cast<session*>(ptr);std::lock_guard guard(s.lock);
 auto tmpl=common_chat_templates_init(s.model,"");common_chat_templates_inputs in;
 in.messages=common_chat_msgs_parse_oaicompat(common_json::parse(messages));
 in.use_jinja=true;in.enable_thinking=false;in.force_pure_content=true;in.chat_template_kwargs["enable_thinking"]="false";
 auto chat=common_chat_templates_apply(tmpl.get(),in);
 const auto* vocab=llama_model_get_vocab(s.model);
 int n=-llama_tokenize(vocab,chat.prompt.c_str(),int(chat.prompt.size()),nullptr,0,true,true);
 if(n<=0||n+maxTokens>int(llama_n_ctx(s.ctx)))throw std::runtime_error("Request plus output budget exceeds configured model context. Increase contextTokens or reduce lane context.");
 std::vector<llama_token> tokens(n);if(llama_tokenize(vocab,chat.prompt.c_str(),int(chat.prompt.size()),tokens.data(),n,true,true)<0)throw std::runtime_error("Tokenization failed.");
 llama_memory_clear(llama_get_memory(s.ctx),true);
 struct abort_state{cancelled fn;void* user;} state{cancel,user};
 llama_set_abort_callback(s.ctx,[](void* p){auto* a=static_cast<abort_state*>(p);return a->fn&&a->fn(a->user)!=0;},&state);
 struct reset_abort{llama_context* ctx;~reset_abort(){llama_set_abort_callback(ctx,nullptr,nullptr);}} reset{s.ctx};
 for(int pos=0;pos<n;pos+=512){if(cancel&&cancel(user))return 1;auto b=llama_batch_get_one(tokens.data()+pos,std::min(512,n-pos));if(llama_decode(s.ctx,b)!=0){if(cancel&&cancel(user))return 1;throw std::runtime_error("Prompt evaluation failed.");}}
 auto sampler=std::unique_ptr<llama_sampler,decltype(&llama_sampler_free)>(llama_sampler_chain_init(llama_sampler_chain_default_params()),llama_sampler_free);
 llama_sampler_chain_add(sampler.get(),llama_sampler_init_top_k(40));
 llama_sampler_chain_add(sampler.get(),llama_sampler_init_top_p(.95f,1));
 llama_sampler_chain_add(sampler.get(),llama_sampler_init_temp(.7f));
 llama_sampler_chain_add(sampler.get(),llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
 std::string pending;
 size_t reserve=0;for(const auto& stop:chat.additional_stops)reserve=std::max(reserve,stop.size());
 for(int i=0;i<maxTokens;i++){
  if(cancel&&cancel(user))return 1;
  auto t=llama_sampler_sample(sampler.get(),s.ctx,-1);
  if(llama_vocab_is_eog(vocab,t)){if(!pending.empty()&&emit(user,pending.data(),int(pending.size())))return 1;return 0;}
  char small[256];int count=llama_token_to_piece(vocab,t,small,sizeof(small),0,false);
  if(count<0){std::vector<char> large(-count);count=llama_token_to_piece(vocab,t,large.data(),int(large.size()),0,false);pending.append(large.data(),count);}else pending.append(small,count);
  for(const auto& stop:chat.additional_stops){auto at=pending.find(stop);if(at!=std::string::npos){if(at&&emit(user,pending.data(),int(at)))return 1;return 0;}}
  if(pending.size()>reserve){auto count=pending.size()-reserve;if(emit(user,pending.data(),int(count)))return 1;pending.erase(0,count);}
  auto b=llama_batch_get_one(&t,1);if(llama_decode(s.ctx,b)!=0){if(cancel&&cancel(user))return 1;throw std::runtime_error("Token evaluation failed.");}
 }
 throw std::runtime_error("Output token limit reached before completion; increase context/output budget.");
 }catch(const std::exception& e){error(err,size,e.what());return -1;}catch(...){error(err,size,"Native generation failed.");return -1;}
}
