import {mkdir,writeFile} from 'node:fs/promises';
import {randomBytes} from 'node:crypto';
await mkdir('.secrets',{recursive:true,mode:0o700});
await writeFile('.secrets/INSTRUCTION_KEY_BASE64',randomBytes(32).toString('base64'),{mode:0o600,flag:'wx'});
console.log('Created .secrets/INSTRUCTION_KEY_BASE64. Upload it with `npx wrangler secret put INSTRUCTION_KEY_BASE64` and use the same file for customer-release packaging.');
