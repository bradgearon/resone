import {Miniflare,convertV4MiniflareOptions} from 'miniflare';
import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
import {b64} from '../src/worker.mjs';
const p=await crypto.subtle.generateKey({name:'ECDSA',namedCurve:'P-256'},true,['sign','verify']);
const mf=new Miniflare(convertV4MiniflareOptions({modules:true,scriptPath:new URL('../src/worker.mjs',import.meta.url).pathname,compatibilityDate:'2026-09-01',d1Databases:['DB'],bindings:{SIGNING_PRIVATE_JWK:JSON.stringify(await crypto.subtle.exportKey('jwk',p.privateKey)),ADMIN_TOKEN:'test',ISSUANCE_SECRET:'test',SIGNING_KEY_ID:'resone-1',LEASE_SECONDS:'604800',INSTRUCTION_KEY_BASE64:Buffer.alloc(32,7).toString('base64')}}));
try{
 const db=await mf.getD1Database('DB');for(const sql of readFileSync(new URL('../migrations/0001.sql',import.meta.url),'utf8').split(';').filter(x=>x.trim()))await db.prepare(sql).run();
 async function call(path,b,admin=false){const r=await mf.dispatchFetch('https://local.test'+path,{method:'POST',headers:admin?{Authorization:'Bearer test'}:{},body:JSON.stringify(b)});return {status:r.status,...await r.json()};}
 const {licenseKey}=await call('/admin/issue',{provider:'test',purchaseId:'concurrent'},true);
 async function proof(){const pair=await crypto.subtle.generateKey({name:'ECDSA',namedCurve:'P-256'},true,['sign','verify']);const devicePublicKey=await crypto.subtle.exportKey('jwk',pair.publicKey);const {challenge}=await call('/v1/challenge',{action:'activate',licenseKey,devicePublicKey});return {licenseKey,devicePublicKey,challenge,signature:b64(await crypto.subtle.sign({name:'ECDSA',hash:'SHA-256'},pair.privateKey,new TextEncoder().encode(challenge)))};}
 const requests=await Promise.all([proof(),proof()]);const results=await Promise.all(requests.map(b=>call('/v1/activate',b)));
 assert.deepEqual(results.map(r=>r.status).sort(),[200,409]);
 assert.equal((await db.prepare('SELECT COUNT(*) AS n FROM licenses WHERE device IS NOT NULL').first()).n,1);
 console.log('PASS actual Workers runtime / D1 simultaneous device activation: exactly one winner');
}finally{await mf.dispose();}
