const encoder = new TextEncoder();
const alg = { name: 'ECDSA', namedCurve: 'P-256' };
const sigAlg = { name: 'ECDSA', hash: 'SHA-256' };
export const b64 = bytes => btoa(String.fromCharCode(...new Uint8Array(bytes))).replaceAll('+','-').replaceAll('/','_').replace(/=+$/,'');
export const unb64 = s => Uint8Array.from(atob(s.replaceAll('-','+').replaceAll('_','/')), c=>c.charCodeAt(0));
const now = () => Math.floor(Date.now()/1000);
class Failure extends Error { constructor(status,code) { super(code); this.status=status; } }
const fail = (status,code) => { throw new Failure(status,code); };
function field(o,k,max=256) { const v=o[k]; if(typeof v!=='string'||!v.length||v.length>max) fail(400,'invalid_'+k); return v; }
async function hash(s) { return b64(await crypto.subtle.digest('SHA-256',encoder.encode(s))); }
async function mac(secret,text) { const key=await crypto.subtle.importKey('raw',encoder.encode(secret),{name:'HMAC',hash:'SHA-256'},false,['sign']); return b64(await crypto.subtle.sign('HMAC',key,encoder.encode(text))); }
async function same(a,b) { return await hash(a) === await hash(b); }
function publicJwk(j) {
 if(!j||j.kty!=='EC'||j.crv!=='P-256'||j.d||typeof j.x!=='string'||typeof j.y!=='string'||j.x.length!==43||j.y.length!==43) fail(400,'invalid_device_key');
 return {crv:'P-256',kty:'EC',x:j.x,y:j.y};
}
async function signer(env,usage) {
 const j=JSON.parse(env.SIGNING_PRIVATE_JWK);
 if(usage==='verify') { delete j.d; delete j.key_ops; }
 return crypto.subtle.importKey('jwk',j,alg,false,[usage]);
}
export async function sign(env,claims) {
 const head=b64(encoder.encode(JSON.stringify({alg:'ES256',typ:'JWT',kid:env.SIGNING_KEY_ID})));
 const payload=b64(encoder.encode(JSON.stringify(claims))); const message=head+'.'+payload;
 return message+'.'+b64(await crypto.subtle.sign(sigAlg,await signer(env,'sign'),encoder.encode(message)));
}
async function verify(env,token) {
 try {
 const parts=token.split('.'); if(parts.length!==3) throw Error();
 const h=JSON.parse(new TextDecoder().decode(unb64(parts[0])));
 if(h.alg!=='ES256'||h.kid!==env.SIGNING_KEY_ID) throw Error();
 if(!await crypto.subtle.verify(sigAlg,await signer(env,'verify'),unb64(parts[2]),encoder.encode(parts[0]+'.'+parts[1]))) throw Error();
 const c=JSON.parse(new TextDecoder().decode(unb64(parts[1])));
 if(c.iss!=='resone-licensing'||c.exp<=now()||c.iat>now()+30) throw Error();
 return c;
 } catch { fail(401,'invalid_or_expired_challenge'); }
}
async function read(request) {
 // Bound actual bytes, not only the untrusted Content-Length header.
 const reader=request.body?.getReader(); if(!reader) fail(400,'missing_body');
 const pieces=[];let size=0;
 while(true) {const {done,value}=await reader.read();if(done)break; size+=value.length;if(size>16384){await reader.cancel();fail(413,'body_too_large');}pieces.push(value);}
 const all=new Uint8Array(size);let offset=0;for(const p of pieces){all.set(p,offset);offset+=p.length;}
 try{return JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(all));}catch{fail(400,'invalid_json');}
}
const response = (body,status=200)=>Response.json(body,{status,headers:{'Cache-Control':'no-store','X-Content-Type-Options':'nosniff'}});
export default {
 async fetch(request,env) {
  try {
   const path=new URL(request.url).pathname;
   if(request.method==='GET'&&path==='/health') return response({service:'resone-licensing',version:1});
   if(request.method!=='POST') fail(405,'method_not_allowed');
   if(!env.SIGNING_PRIVATE_JWK||!env.ADMIN_TOKEN||!env.ISSUANCE_SECRET) fail(503,'service_not_configured');
   if(env.LIMITER && !(await env.LIMITER.limit({key:request.headers.get('CF-Connecting-IP')||'local'})).success) fail(429,'rate_limited');
   const b=await read(request);
   if(!b||Array.isArray(b)||typeof b!=='object') fail(400,'invalid_body');
   if(path.startsWith('/admin/')) {
    if(!await same(request.headers.get('Authorization')||'', 'Bearer '+env.ADMIN_TOKEN)) fail(401,'unauthorized');
    const provider=field(b,'provider',64),purchase=field(b,'purchaseId');
    if(path==='/admin/issue') {
     // Only a trusted operator/verified payment backend may call this endpoint.
     // Purchase IDs are metadata; knowing one never authorizes activation.
     const key='RSN-'+await mac(env.ISSUANCE_SECRET,JSON.stringify([provider,purchase]));
     await env.DB.prepare('INSERT INTO licenses(id,provider,purchase_id,key_hash,created_at) VALUES(?,?,?,?,?) ON CONFLICT(provider,purchase_id) DO NOTHING').bind(crypto.randomUUID(),provider,purchase,await hash(key),now()).run();
     const row=await env.DB.prepare('SELECT id,key_hash,status FROM licenses WHERE provider=? AND purchase_id=?').bind(provider,purchase).first();
     if(row.status!=='active') fail(409,'purchase_revoked');
     if(row.key_hash!==await hash(key)) fail(503,'issuance_secret_changed');
     return response({licenseId:row.id,licenseKey:key});
    }
    if(path==='/admin/revoke') {
     const result=await env.DB.prepare("UPDATE licenses SET status='revoked' WHERE provider=? AND purchase_id=?").bind(provider,purchase).run();
     if(!result.meta.changes) fail(404,'purchase_not_found');
     return response({revoked:true,offlineLeasesExpireNaturally:true});
    }
    if(path==='/admin/reset-device') {
     // Preserve existing offline lease exclusivity. No immediate transfer overlap.
     const r=await env.DB.prepare("UPDATE licenses SET released=1 WHERE provider=? AND purchase_id=? AND status='active' RETURNING lease_until").bind(provider,purchase).first();
     if(!r) fail(404,'purchase_not_found');
     return response({transferAvailableAt:r.lease_until});
    }
    fail(404,'not_found');
   }
   if(path==='/v1/challenge') {
    const action=field(b,'action');if(!['activate','renew','release'].includes(action)) fail(400,'invalid_action');
    const device=publicJwk(b.devicePublicKey);
    // The challenge is signed, short-lived, action- and device-bound, single-use.
    const challenge=await sign(env,{iss:'resone-licensing',aud:'resone-challenge',iat:now(),exp:now()+120,
     jti:crypto.randomUUID(),action,keyHash:await hash(field(b,'licenseKey')),device:await hash(JSON.stringify(device))});
    return response({challenge});
   }
   if(['/v1/activate','/v1/renew','/v1/release'].includes(path)) {
    const challenge=field(b,'challenge',4096),c=await verify(env,challenge);
    const device=publicJwk(b.devicePublicKey),fingerprint=await hash(JSON.stringify(device));
    if(c.aud!=='resone-challenge'||c.action!==path.slice(4)||c.device!==fingerprint||c.keyHash!==await hash(field(b,'licenseKey'))) fail(401,'challenge_mismatch');
    let proven=false;try{proven=await crypto.subtle.verify(sigAlg,await crypto.subtle.importKey('jwk',device,alg,false,['verify']),unb64(field(b,'signature',256)),encoder.encode(challenge));}catch{}
    if(!proven) fail(401,'invalid_device_signature');
    const seconds=Number(env.LEASE_SECONDS||604800);if(!Number.isInteger(seconds)||seconds<300||seconds>604800) fail(503,'invalid_lease_configuration');
    const expiry=now()+seconds;
    let sql,params;
    if(c.action==='activate') {
     sql="UPDATE licenses SET device=?, released=0, lease_until=MAX(lease_until,?) WHERE key_hash=? AND status='active' AND (device IS NULL OR device=? OR (released=1 AND lease_until<=?)) RETURNING id,device,lease_until";
     params=[fingerprint,expiry,c.keyHash,fingerprint,now()];
    } else if(c.action==='renew') {
     sql="UPDATE licenses SET lease_until=MAX(lease_until,?) WHERE key_hash=? AND device=? AND status='active' AND released=0 RETURNING id,device,lease_until";
     params=[expiry,c.keyHash,fingerprint];
    } else {
     sql="UPDATE licenses SET released=1 WHERE key_hash=? AND device=? AND status='active' RETURNING id,device,lease_until";
     params=[c.keyHash,fingerprint];
    }
    let results;
    try {
     results=await env.DB.batch([
      env.DB.prepare('INSERT INTO used_nonces(nonce,expires_at) VALUES(?,?)').bind(c.jti,c.exp),
      env.DB.prepare(sql).bind(...params)
     ]);
    } catch(e) { if(String(e).includes('UNIQUE')) fail(409,'challenge_already_used');throw e; }
    const row=results[1].results[0];
    if(!row) fail(409,'license_unavailable_for_device');
    if(c.action==='release') return response({released:true,transferAvailableAt:row.lease_until});
    return response({lease:await sign(env,{iss:'resone-licensing',aud:'wds.resone',sub:row.id,device:row.device,iat:now(),exp:row.lease_until,jti:crypto.randomUUID()}),expiresAt:row.lease_until});
   }
   fail(404,'not_found');
  } catch(e) {
   // Never return database errors, credentials, prompts or request bodies.
   return response({error:e instanceof Failure?e.message:'internal_error'},e instanceof Failure?e.status:500);
  }
 },
 async scheduled(_event,env) {await env.DB.prepare('DELETE FROM used_nonces WHERE expires_at<?').bind(now()-300).run();}
};
