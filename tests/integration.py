"""Actual NativeAOT C ABI -> WebSocket host -> deterministic fake inference.
Usage: python tests/integration.py <dotnet> <launcher.dll> <api.so> <Resone root>
Fake inference tests transport/repair; it is not a model quality benchmark.
"""
import asyncio, ctypes, json, os, pathlib, sys, tempfile, shutil, base64
from aiohttp import web

async def main():
    dotnet, launcher, native, source = map(str,sys.argv[1:])
    source=pathlib.Path(source).resolve(); calls=[]
    async def llm(req):
        body=await req.json();calls.append(body)
        if 'cancel-test' in json.dumps(body):
            await asyncio.sleep(0.5)
            if req.transport is None or req.transport.is_closing(): return web.Response(status=499)
        repair=any('Fix ALL errors' in str(m.get('content')) for m in body['messages'])
        text='tempo=120 4/4 key=C | C4 E4 G4 C5 |' if repair else 'tempo=120 4/4 key=C major | C4 E4 G4 C5 |'
        out=web.StreamResponse(headers={'Content-Type':'text/event-stream'});await out.prepare(req)
        for part in [text[:15],text[15:]]:await out.write(('data: '+json.dumps({'choices':[{'delta':{'content':part}}]})+'\n\n').encode())
        await out.write(b'data: [DONE]\n\n');return out
    async def stt(req):
        form=await req.post();assert form['file'].file.read(4)==b'RIFF';return web.json_response({'text':'Tender, then hopeful.'})
    async def register(req):
        data=await req.json();assert data['name']=='default' and base64.b64decode(data['wav_b64']).startswith(b'RIFF');return web.json_response({'name':'default'})
    async def tts(req):
        assert (await req.json())['response_format']=='pcm';r=web.StreamResponse(headers={'Content-Type':'application/octet-stream'});await r.prepare(req)
        for data in [b'\0'*13,b'\0'*23001,b'\0'*1000]:await r.write(data)
        return r
    app=web.Application();app.router.add_post('/llm',llm);app.router.add_post('/stt',stt);app.router.add_post('/tts',tts);app.router.add_post('/v1/audio/voices',register)
    runner=web.AppRunner(app);await runner.setup();await web.TCPSite(runner,'127.0.0.1',18080).start()
    temp=pathlib.Path(tempfile.mkdtemp(prefix='resone-test-'));(temp/'config').mkdir();shutil.copytree(source/'assets/Instructions',temp/'assets/Instructions')
    (temp/'reference.wav').write_bytes(b'RIFF'+b'\0'*60)
    (temp/'config/appsettings.json').write_text(json.dumps({'llmUrl':'http://127.0.0.1:18080/llm','sttUrl':'http://127.0.0.1:18080/stt','ttsUrl':'http://127.0.0.1:18080/tts','timeoutSeconds':30,'ttsReferenceAudioPath':str(temp/'reference.wav')}))
    command=([dotnet] if dotnet!='-' else [])+[str(pathlib.Path(launcher).resolve())]
    proc=await asyncio.create_subprocess_exec(*command,'--no-ui','--no-services',env={**os.environ,'RESONE_HOME':str(temp)},stdout=asyncio.subprocess.PIPE,stderr=asyncio.subprocess.STDOUT)
    lib=ctypes.CDLL(str(pathlib.Path(native).resolve()));cbtype=ctypes.CFUNCTYPE(None,ctypes.c_void_p,ctypes.c_void_p,ctypes.c_int32);loop=asyncio.get_running_loop();queue=asyncio.Queue();handle=0
    @cbtype
    def callback(user,ptr,size):loop.call_soon_threadsafe(queue.put_nowait,json.loads(ctypes.string_at(ptr,size)))
    lib.resone_open.argtypes=[ctypes.c_char_p,cbtype,ctypes.c_void_p];lib.resone_open.restype=ctypes.c_int64
    lib.resone_send.argtypes=[ctypes.c_int64,ctypes.c_char_p,ctypes.c_int32];lib.resone_close.argtypes=[ctypes.c_int64]
    def send(op,payload,id):
        data=json.dumps({'op':op,'requestId':id,'payload':payload}).encode();assert lib.resone_send(handle,data,len(data))==1
    async def until(op,id=None):
        while True:
            e=await asyncio.wait_for(queue.get(),20)
            if e['op']=='error':raise AssertionError(e)
            if e['op']==op and (id is None or e.get('requestId')==id):return e.get('payload')
    try:
        while True:
            line=await asyncio.wait_for(proc.stdout.readline(),20)
            if b'Now listening' in line:break
            if not line:raise RuntimeError('Host failed to start')
        assert lib.resone_abi_version()==1;assert lib.resone_open(b'http://invalid',callback,None)==0
        handle=lib.resone_open(b'ws://127.0.0.1:8078/ws',callback,None);assert handle;await until('ready')
        project={'tempo':120,'meter':'4/4','bars':8,'lanes':[{'id':'melody','name':'Melody','bank':0,'program':0,'volume':.8,'muted':False,'solo':False,'drums':False,'notation':'','originalBrief':'','notes':[]}]}
        send('compose',{'project':project,'laneId':'melody','description':'Hello, then happy','useAhd':True},'c');music=await until('composition','c');assert len(music['notes'])==4 and music['bars']==1 and len(calls)==2
        project['lanes'][0]['notes']=music['notes'];send('export',project,'e');midi=base64.b64decode((await until('midi','e'))['data']);assert midi.startswith(b'MThd') and b'MTrk' in midi
        send('render',{'notation':'tempo=120 4/4 key=C C4 E4 G4 C5'},'r');assert base64.b64decode((await until('midi','r'))['data']).startswith(b'MThd')
        send('transcribe',{'wav':base64.b64encode(b'RIFF'+b'\0'*60).decode()},'v');assert 'hopeful' in (await until('transcript','v'))['text']
        send('speak',{'text':'Hello'},'s');chunks=[]
        while True:
            e=await asyncio.wait_for(queue.get(),10)
            if e['op']=='speechChunk':chunks.append(e['payload'])
            elif e['op']=='speechEnd':break
            elif e['op']=='error':raise AssertionError(e)
        assert [c['sequence'] for c in chunks]==[0,1,2];assert sum(len(base64.b64decode(c['pcm'])) for c in chunks)==24014
        send('compose',{'project':project,'laneId':'melody','description':'cancel-test'},'x');await until('status','x');send('cancel',{},'x');await until('cancelled','x')
        send('render',{'notation':'tempo=96 4/4 key=Cm C3, Eb3, G3, C4::'},'after');await until('midi','after')
        print('PASS: NativeAOT ABI, WebSocket compose/repair, flexible duration, MIDI export/render, voice transcription, ordered PCM streaming, cancellation and recovery')
    finally:
        if handle:await asyncio.to_thread(lib.resone_close,handle)
        proc.terminate();await proc.wait();await runner.cleanup();shutil.rmtree(temp)
asyncio.run(main())
