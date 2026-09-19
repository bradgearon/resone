# Resone application updater

Resone uses two small native-AOT processes for application lifecycle:

- `wds.resone.launcher.exe` checks the release manifest before the UI or AI worker starts.
- `wds.resone.updater.exe` is copied to a temporary runner directory outside the install tree, waits for the launcher to exit, verifies and installs the selected release, then relaunches the launcher.

The updater is intentionally separate so it can replace both the installed launcher and updater without Windows executable-file locks.

## Local policy

Update behavior lives in `config/runtime.json` under `updates`. The default source manifest keeps updates disabled until a real release endpoint and hashes are published.

Important fields:

- `enabled`: enables application update checks.
- `currentVersion`: version represented by the installed payload.
- `channel`: release channel matched against the remote manifest.
- `manifestUrl`: HTTPS endpoint on `resone.io` (or another configured host).
- `downloadAttempts`, `retryDelaySeconds`: retry behavior for bad downloads/hash failures.
- `failedUpdateRetryMinutes`: prevents an immediate failure/relaunch/update loop.
- `notifyAfterConsecutiveFailures`: tray warning threshold.
- `requireHashes`: requires SHA-256 for downloaded artifacts.
- `verifyExtractedFiles`: requires hashes for critical extracted files.
- `preservePaths`: install-root paths copied from the previous install into the staged replacement (currently `user`).
- `autoInstallInstallerArtifacts`: enables verified installer artifacts such as a future VST3 installer.

`AI_ROOT` is not part of the application payload and is never replaced by an app update.

## Remote release manifest

See `config/update-manifest.example.json`.

A release contains an `app` artifact and can later contain additional installer artifacts. `installMode` is either:

- `archive`: verified ZIP, safe extraction, critical-file verification, staged replacement and rollback.
- `installer`: verified executable/MSI-style payload executed with manifest-provided arguments.

The app archive must contain `config/runtime.json` with `updates.currentVersion` equal to the remote manifest version. This prevents a mismatched archive from being installed under the wrong release number.

## Failure behavior

1. Launcher manifest-check failures are recorded outside the install tree.
2. The updater retries download/hash failures according to policy.
3. If installation fails, it records an install failure and restores the previous app when a staged swap had occurred.
4. The old/current launcher is started with `--skip-update-once` so a failed updater does not create an immediate infinite loop.
5. Later normal launches retry after `failedUpdateRetryMinutes`.
6. Repeated failures trigger a tray warning.
7. If the updater cannot relaunch any launcher at all, the updater itself displays a Windows tray notification (with a message-box fallback).

## Logging

All Resone processes append to the same plain-text daily file:

`%LOCALAPPDATA%\Wds\Logs\Resone\resone-YYYY-MM-DD.log`

`runtime.json -> logging` can override the directory and retention period. There are no JSONL host logs or one-JSON-file-per-LLM-call logs anymore.

## Publishing

After building/deploying a staging install, run:

```powershell
.\scripts\package-resone-update.ps1 `
  -Version 0.2.0 `
  -PackageUrl https://resone.io/downloads/resone-0.2.0-win-x64.zip `
  -InstallDirectory "$env:LOCALAPPDATA\Wds\Resone"
```

The script:

- copies the application payload without `user`, logs, or update work state;
- stamps `updates.currentVersion` and enables update checks in the staged runtime config;
- creates the ZIP;
- computes its SHA-256;
- computes SHA-256 values for the critical extracted binaries/config;
- emits a ready-to-publish release manifest.

Upload the ZIP to `PackageUrl`, then publish the generated manifest at the URL configured by `updates.manifestUrl`.
