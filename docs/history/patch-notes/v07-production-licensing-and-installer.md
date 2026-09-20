# Production licensing + installer patch

This patch is based on the v6 piano-roll/live-build source and adds the production distribution path.

## Customer licensing

- Uses the existing Cloudflare Worker + D1 licensing project.
- A purchased `RSN-...` license checks out to one non-exportable Windows CNG device key.
- Signed offline leases remain device-bound.
- License activation state is stored under Windows DPAPI.
- A new `/v1/instruction-key` proof action returns the protected instruction AES key only to the activated device while its lease is live.
- The locally cached instruction key is stored inside the same DPAPI-protected activation cache.

## Protected LLM instructions

- Customer builds no longer embed the AES key in `PackedInstructions.g.cs`.
- `pack-instructions.mjs` requires a private 32-byte base64 instruction key from a file/environment variable.
- Generated code contains only AES-GCM nonce, tag, and ciphertext.
- The API cannot read protected instructions until `LicenseService` installs a server-delivered key.
- Customer builds require `-InstructionKeyFile`; loose `assets/Instructions` remain prohibited from release staging.

## Installer

- Added an Inno Setup installer project under `installer/Resone.iss`.
- First install presents a masked license-key field.
- Existing activated installs skip the key page on upgrade.
- The key is handed to the launcher via a temporary file, not on the process command line, then the temp file is deleted.
- Setup attempts activation after file installation and does not launch Resone if activation fails.
- Installs the Resone application plus its VST3 bundle for the current user.

## Production build scripts

- `scripts/build-customer-release.ps1` builds a clean customer staging tree then invokes Inno Setup.
- `scripts/build-installer.ps1` discovers Inno Setup 7 or 6, or accepts an explicit compiler path.
- `docs/build/production-build-and-release.md` documents Worker secrets, production packaging, and activation flow.

## Cloudflare secret setup

Fresh deployments create `INSTRUCTION_KEY_BASE64` with the other secrets. Existing deployments can run `cloud/licensing/scripts/create-instruction-key.mjs`, upload the result with Wrangler, and use the same private file during customer packaging.

## Validation performed in this environment

- Cloudflare Worker unit tests: 6/6 pass, including activated-device-only instruction key delivery.
- Worker and instruction packer JavaScript syntax checks pass.
- Customer instruction bundle AES-GCM encrypt/decrypt smoke test passes.
- Generated packed instruction C# verified to contain no AES key constant.
- API/launcher project XML parsed successfully.
- Static checks confirm no `.secrets` or instruction-key file is included in the project package.

The Windows .NET/AOT build, CMake build, and Inno compiler cannot be executed in this Linux sandbox and should be run on the normal Windows build machine.
