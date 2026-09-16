// Run on your machine or trusted payment backend AFTER verifying the purchase.
// The customer app must never contain RESONE_ADMIN_TOKEN.
const [provider,purchaseId]=process.argv.slice(2);
const {RESONE_LICENSE_URL,RESONE_ADMIN_TOKEN}=process.env;
if(!provider||!purchaseId||!RESONE_LICENSE_URL?.startsWith('https://')||!RESONE_ADMIN_TOKEN)throw Error('Set RESONE_LICENSE_URL and RESONE_ADMIN_TOKEN, then run: node scripts/issue-purchase.mjs provider verified-purchase-id');
const response=await fetch(RESONE_LICENSE_URL.replace(/\/$/,'')+'/admin/issue',{method:'POST',headers:{'Authorization':'Bearer '+RESONE_ADMIN_TOKEN,'Content-Type':'application/json'},body:JSON.stringify({provider,purchaseId}),signal:AbortSignal.timeout(15000)});
if(!response.ok)throw Error('Issuance failed: '+response.status);
console.log(JSON.stringify(await response.json(),null,2));
