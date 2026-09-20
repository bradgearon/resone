# Resone licensing worker

This is a Cloudflare Worker plus private D1 database. It issues activation keys from verified purchases, binds one device public key, and returns signed ES256 leases. No hosted file per buyer is needed. The launcher stores the lease locally, encrypted with Windows DPAPI; the device signing key is a persisted non-exportable CNG key.

## Deploy

Requires Node 22+ for Wrangler; tests use Node 24's SQLite module.

1. Run `npm ci` and `npm test` here.
2. Run `npx wrangler login` under your Cloudflare account.
3. Run `npx wrangler d1 create resone-licenses`. Copy the returned database ID into `wrangler.jsonc`.
4. Run `npx wrangler d1 migrations apply resone-licenses --remote`.
5. Run `node scripts/create-secrets.mjs` once. This creates `.secrets/SIGNING_PRIVATE_JWK`, `.secrets/ADMIN_TOKEN`, `.secrets/ISSUANCE_SECRET`, and `signing-public.jwk`. Back up the private files securely. The script refuses to overwrite existing keys.
6. Upload each secret with `npx wrangler secret put NAME`, pasting that file's contents at its prompt. `INSTRUCTION_KEY_BASE64` is the AES-256 key used only to package and unlock the protected LLM instruction bundle. Alternatively, pipe the file to the command. Never paste secrets into the app config, source control, or a public website.
7. Run `npx wrangler deploy`. The `workers.dev` URL works immediately; optionally bind `licenses.resone.io` as a Worker custom domain. Put that HTTPS URL in the customer build's licensing configuration.
8. Supply `signing-public.jwk` plus the local `.secrets/INSTRUCTION_KEY_BASE64` file to the customer release build. The public signing key ships; the instruction key does not. The same instruction key file must also be installed as the Worker secret `INSTRUCTION_KEY_BASE64`. The worker private signing key never ships.
9. Issue a test purchase, activate on machine A, reject machine B, renew on A, and test an expired lease. Revoke the test purchase afterward. Test this against the deployed Worker before selling.

`npm run deploy -- --dry-run` validates configuration without publishing. No Cloudflare resources have been created by this package.

## Purchase issuance

After you or your payment backend verify that the purchase was paid, call:

```text
POST /admin/issue
Authorization: Bearer <ADMIN_TOKEN>
Content-Type: application/json

{"provider":"itch","purchaseId":"your-verified-purchase-id"}
```

The response contains `licenseId` and `licenseKey`. Deliver the key to the customer through your store/account/email delivery system. The included `scripts/issue-purchase.mjs` calls this endpoint using environment variables. Repeating the same provider and purchase ID returns the same key without minting extra licenses. Keep ISSUANCE_SECRET stable; changing it requires an explicit key migration.

A purchase ID alone is not proof of purchase. This package has no unauthenticated purchase-claim endpoint and does not assume an itch.io webhook exists. Connecting a storefront's verified checkout notification to `/admin/issue` is the remaining storefront-specific task. Do not put ADMIN_TOKEN in the downloadable app.

Refunds: POST `/admin/revoke` with the same provider/purchaseId and admin token. Device recovery: POST `/admin/reset-device`; it reports when the outstanding offline lease expires and transfer becomes available.

## Client protocol

- POST `/v1/challenge`: `action` (`activate`, `renew`, `release`), `licenseKey`, `devicePublicKey` (public P-256 JWK).
- Sign the returned `challenge` string as UTF-8 with the device private key using ECDSA SHA-256, 64-byte P1363 signature; base64url encode it.
- POST `/v1/<action>`: same key and public key, `challenge`, `signature`.
- Activate/renew returns `lease` (compact ES256 JWS), `expiresAt`.
- Release returns `transferAvailableAt`. The old local cache is removed. Server renewals stop; another device may activate after the old lease expires.

The verifier pins the public key inside customer binaries and checks signature, issuer, audience, device fingerprint, issue time and expiry. The worker uses one atomic SQL update to claim a device, inside a batch that also consumes the challenge nonce. Replaying a challenge cannot extend a lease. Public endpoints have a per-location request rate limit; device exclusivity is enforced by D1, not that approximate rate limiter.

## Offline policy

LEASE_SECONDS defaults to 604800 (7 days), configurable from 300 to 604800. Renewal occurs when generation is requested after expiry. Playback/editing/export of existing music remain available offline. A refund or release cannot invalidate an already issued offline token immediately. Transfers therefore wait until the old lease expires; shorten the lease if you want faster transfers. Single-device licensing means one enrolled installation key; administrators/reverse engineers controlling a machine can still bypass client checks. This is deterrence, not an unbreakable DRM guarantee.

## Storage and operations

D1 stores purchase identifiers, hashed activation keys, device public-key fingerprints, lease expiry, and revocation state. It does not store card data or prompts. Signing/issuance/admin secrets use Worker Secrets. Nightly cleanup removes expired replay nonces. No R2 bucket is required for licensing. Engine packs can be hosted separately over HTTPS with configured hashes.

Tests run the real Worker handler and SQL against SQLite through a D1-shaped adapter. They cover unauthorized issuance, idempotent purchases, forged proof, replay, device exclusivity, release/transfer timing, expiry, revocation, and body limits. A Cloudflare deployment smoke test is still required.

References: [D1 prepared statements](https://developers.cloudflare.com/d1/worker-api/prepared-statements/), [Worker Secrets](https://developers.cloudflare.com/workers/configuration/secrets/), [Web Crypto](https://developers.cloudflare.com/workers/runtime-apis/web-crypto/), [rate limiting bindings](https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/).


## Protected instruction key

Customer releases contain only AES-GCM ciphertext for the LLM instruction bundle. The AES key is never generated into `PackedInstructions.g.cs` or embedded in the launcher/API. After activation or lease renewal, the client proves possession of the non-exportable device signing key and requests `/v1/instruction-key`. The Worker returns the current instruction key only for the active device-bound license. The client stores it inside the existing Windows-DPAPI-protected license cache. Copying that cache to another Windows machine does not make it usable there.

For an existing Worker deployment created before this feature, run `node scripts/create-instruction-key.mjs`, upload `.secrets/INSTRUCTION_KEY_BASE64` with Wrangler, and use that same file when creating the customer build. If you rotate this AES key, rebuild/repackage the protected instruction bundle with the new key before distributing that build.

## Domain verification file

The Worker also serves the certificate/domain-verification token at:

`GET /01a0bc28-2ec7-7db3-abe1-a20b8f279de8.txt`

with the exact plain-text body `B8FxYSQms8N98euAXGKJ8YUuY8Y`. This route is handled before licensing configuration checks so certificate validation does not depend on D1 or Worker secrets.
