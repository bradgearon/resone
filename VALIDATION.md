# Validation — 2026-09-16

## Passed in this workspace

- Cloudflare Worker handler and SQLite tests: five tests covering purchase issuance, authorization, signed proof, replay protection, single-device activation, release/transfer, expiry, revocation and malformed requests.
- Actual local Workers runtime / D1 (Miniflare): two simultaneous device activations yielded exactly one successful claim and one conflict.
- Wrangler deployment dry run completed. No remote Worker or D1 database was created.
- Editor generation-state regression: selected-lane replacement, context with manually edited notes and prompts, other lanes preserved, autoplay, undo, stale responses, errors and suggestion-button submission.
- Saved notation regression: received-string retention, manual edit refresh, deletion, chord/rest/overlap/velocity/tick round trips.
- All five newest instruction files and the logo PNG match the supplied files byte-for-byte. AES-GCM packed instructions round-trip exactly.
- C# developer and CustomerRelease builds; .NET SDK 9.0.318. Use single-process MSBuild (`-m:1 -nr:false`) in this environment.
- Linux x64 CustomerRelease NativeAOT publish completed with no warnings/errors on the final publish. Published assets contain no loose instruction files.
- Executed the published native worker: rejected unauthenticated and browser-Origin WebSocket connections; accepted the bootstrap bearer token; returned ready and valid MIDI for a notation-render request. This used no loose instructions. This was not a real license activation or model inference test.
- Native inference adapter compiled against the exact llama.cpp commit in `native/inference/llama-commit.txt`. Shared library loaded, ABI version was 1, and a missing-model request returned an error without crashing.
- Portable C++ MIDI exporter built and output was parsed: selected lane only, chord simultaneity, manually edited pitches/velocities, fractional beat timing, tempo/meter, and percussion channel 10. Missing lanes rejected.

## Not validated here

Windows iPlug2 compilation, application/tray appearance, CNG/DPAPI device activation, multiple live VST instances, Windows OLE MIDI drag into DAWs, microphone/TTS engines, download packs on clean customer systems, GPU runtime compatibility, and actual generation with the user's GGUF model. No Windows DAW or model was available. Platform-specific macOS/Linux UI, tray, secure key storage and drag/drop still require implementation before those desktop releases.

The source is prepared for integration and target-machine validation; this archive is not a tested customer installer. The focused changes are relative to the recovered saved baseline, not the live Windows checkout.

## Reproduce key checks

```sh
cd cloud/licensing
npm ci
npm test
npm run test:integration
npx wrangler deploy --dry-run
```

From the solution root:

```sh
node tests/generation-state.cjs
node tests/lane-notation.cjs
node tests/instruction-bundle.mjs
```

Follow `RELEASE-UPDATE.md` for Windows/customer build prerequisites. Test credentials used during validation were ephemeral and are not included.
