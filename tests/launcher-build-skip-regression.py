from pathlib import Path
root = Path(__file__).resolve().parents[1]
tray = (root / 'src/wds.resone.launcher/TrayIcon.cs').read_text()
build = (root / 'scripts/build-windows.ps1').read_text()

assert 'public void Notify(' in tray
assert 'struct NotifyIconData' in tray
assert 'struct Notify{' not in tray and 'struct Notify {' not in tray
assert 'Marshal.SizeOf<NotifyIconData>()' in tray
assert 'ref NotifyIconData data' in tray

assert 'function Get-LauncherSourceFingerprint' in build
assert "Get-Process -Name 'wds.resone.launcher'" in build
assert '$SkipLauncherPublish' in build
assert 'skipping launcher publish' in build
assert "src/wds.resone.api" in build, 'API source must invalidate AOT launcher publish because it is a ProjectReference'
assert "launcher-next" in build, 'changed launcher should still build safely when build/launcher itself is running'
assert '$LauncherOutputDir/wds.resone.launcher.exe' in build
print('launcher compile + live skip regression: PASS')
