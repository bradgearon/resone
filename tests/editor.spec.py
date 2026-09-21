"""Playwright editor checks against a mock native bridge; no inference/downloads.
Run on a machine with Chromium: python tests/editor.spec.py [chromium executable]
"""
from pathlib import Path
import json,sys
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[1]
with sync_playwright() as p:
    options={'headless':True}
    if len(sys.argv)>1:options['executable_path']=sys.argv[1]
    browser=p.chromium.launch(**options)
    page=browser.new_page(viewport={'width':1440,'height':850})
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.add_init_script('window.sent=[];window.IPlugSendMsg=m=>window.sent.push(JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(m.data),c=>c.charCodeAt(0)))));')
    page.goto((root/'src/wds.resone.ui/resources/web/index.html').as_uri())
    def event(op,payload,id=''):
        page.evaluate('(e)=>receive(e)',{'op':op,'payload':payload,'requestId':id})
    event('instruments',[{'name':'Grand Piano','bank':0,'program':0},{'name':'Standard','bank':128,'program':0}])
    event('connected',{})
    assert page.locator('.lane').count()==6
    page.fill('#brief','Tender, hopeful, then epic')
    page.click('#send');request=page.evaluate('sent.at(-1)');assert request['op']=='compose'
    event('composition',{'tracks':[{'notation':'tempo=120 4/4 C4 E4 G4 C5','originalBrief':'Tender','notes':[{'start':i,'duration':1,'pitch':n,'velocity':96} for i,n in enumerate([60,64,67,72])]}], 'bars':1},request['requestId'])
    assert page.locator('.note').count()==24
    assert page.evaluate('sent.at(-1).op')=='play'
    page.click('#voice');voice=page.evaluate('sent.at(-1)');assert voice['op']=='voiceStart'
    event('recording',True);assert page.locator('#cancel').is_visible()
    page.click('#voice');assert page.evaluate('sent.at(-1).op')=='voiceSend'
    event('recording',False);event('transcript',{'text':'Make the ending brighter'},voice['requestId'])
    assert page.evaluate('sent.at(-1).op')=='compose'
    assert page.evaluate('sent.at(-1).payload.description')=='Make the ending brighter'
    page.click('#cancel');assert page.evaluate('sent.at(-1).op')=='cancel'
    page.click('#settings');page.fill('#apiUrl','wss://resone.example/ws');page.click('#saveSettings')
    assert page.evaluate('sent.at(-1).payload.apiUrl')=='wss://resone.example/ws'
    page.click('#undo');assert page.locator('.note').count()==0
    page.click('#redo');assert page.locator('.note').count()==24
    page.click('#addLane');assert page.locator('.lane').count()==7
    assert not errors,errors
    page.screenshot(path=str(root/'docs/editor-preview.png'))
    browser.close();print('PASS: composition, autoplay, voice/send/cancel, settings, undo/redo, lanes')
