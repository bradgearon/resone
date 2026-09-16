import {mkdtempSync,readFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {execFileSync} from 'node:child_process';
import {createDecipheriv} from 'node:crypto';
import assert from 'node:assert/strict';
const root=resolve(import.meta.dirname,'..'),tmp=mkdtempSync(join(tmpdir(),'resone-bundle-'));
try{
 const out=join(tmp,'bundle.cs');execFileSync(process.execPath,[join(root,'scripts/pack-instructions.mjs'),join(root,'assets/Instructions/Music'),out]);
 const cs=readFileSync(out,'utf8');const b=n=>Buffer.from(cs.match(new RegExp(n+'="([^"]+)"'))[1],'base64');
 const d=createDecipheriv('aes-256-gcm',b('Key'),b('Nonce'));d.setAuthTag(b('Tag'));const text=Buffer.concat([d.update(b('Data')),d.final()]).toString('utf8');
 const files=JSON.parse(text);assert.equal(Object.keys(files).length,5);
 for(const [name,content] of Object.entries(files))assert.equal(content,readFileSync(join(root,'assets/Instructions/Music',name),'utf8'));
 assert(!cs.includes('You are the Resone music composer'));
 console.log('PASS encrypted bundle round-trips all five exact instruction files');
}finally{rmSync(tmp,{recursive:true,force:true});}
