from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
runtime = json.loads((root/'config/runtime.json').read_text())
updates = runtime['updates']
assert updates['manifestUrl'].startswith('https://resone.io/')
assert updates['downloadAttempts'] >= 2
assert updates['requireHashes'] is True
assert updates['verifyExtractedFiles'] is True
assert updates['updaterExecutable'] == 'wds.resone.updater.exe'
assert 'user' in updates['preservePaths']
assert runtime['logging']['retentionDays'] > 0

manifest = json.loads((root/'config/update-manifest.example.json').read_text())
app = next(a for a in manifest['artifacts'] if a['kind'] == 'app')
assert app['installMode'] == 'archive'
assert app['sha256']
assert app['files']
assert any(a['kind'] == 'vst3' and a['installMode'] == 'installer' for a in manifest['artifacts'])

launcher_program = (root/'src/wds.resone.launcher/Program.cs').read_text()
assert launcher_program.index('TryStartUpdateAsync') < launcher_program.index('if(!args.Contains("--no-ui"))controller.OpenUi();')
service = (root/'src/wds.resone.launcher/AppUpdateService.cs').read_text()
assert 'runner-' in service and 'UpdaterExecutable' in service
assert '--skip-update-once' in service
assert 'NotifyAfterConsecutiveFailures' in service

updater = (root/'src/wds.resone.updater/Program.cs').read_text()
for token in ['VerifyShaAsync', 'ExtractVerifiedAsync', 'Rolled back', 'DownloadAttempts', 'TryLaunchLauncher', 'WindowsTrayNotice.Show']:
    assert token in updater, token
assert 'Directory.Move(installRoot, backupRoot)' in updater
assert 'Directory.Move(stage, installRoot)' in updater
assert updater.count('static bool TryLaunchLauncher(') == 1, 'top-level local functions cannot overload TryLaunchLauncher'
assert 'TryLaunchLauncher(installRoot, policy, aiRoot, "--skip-update-once", "--update-failed", null, out Process? fallback, out string relaunchError)' in updater

build = (root/'scripts/build-windows.ps1').read_text()
assert 'wds.resone.updater.csproj' in build
assert 'build/updater/wds.resone.updater.exe' in build
assert "'logging','updates'" in build
assert (root/'scripts/package-resone-update.ps1').exists()

# Logging regression: no JSONL or dedicated whisper/LLM-per-call files in source.
source_text = '\n'.join(p.read_text(errors='ignore') for p in (root/'src').rglob('*.cs'))
assert '.jsonl' not in source_text
assert 'whisper-cpp.log' not in source_text
assert 'llm-' not in (root/'src/wds.resone.api/LlmRequestLog.cs').read_text()
assert 'ResoneDailyLog' in (root/'src/wds.resone.api/LlmRequestLog.cs').read_text()
print('updater regression: ok')
