import {mkdir,writeFile} from 'node:fs/promises';
import {randomBytes} from 'node:crypto';
await mkdir('.secrets',{recursive:true,mode:0o700});
const pair=await crypto.subtle.generateKey({name:'ECDSA',namedCurve:'P-256'},true,['sign','verify']);
const values={SIGNING_PRIVATE_JWK:JSON.stringify(await crypto.subtle.exportKey('jwk',pair.privateKey)),ADMIN_TOKEN:randomBytes(32).toString('hex'),ISSUANCE_SECRET:randomBytes(32).toString('hex'),INSTRUCTION_KEY_BASE64:randomBytes(32).toString('base64')};
for(const [name,value] of Object.entries(values)) await writeFile('.secrets/'+name,value,{mode:0o600,flag:'wx'});
await writeFile('.secrets/signing-public.jwk',JSON.stringify(await crypto.subtle.exportKey('jwk',pair.publicKey)),{flag:'wx'});
console.log('Secrets created in .secrets. Upload them with wrangler secret put; never ship private files.');
