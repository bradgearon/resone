from pathlib import Path
root=Path(__file__).resolve().parents[1]
s=(root/'scripts/build-windows.ps1').read_text()
assert "Get-Process -Name 'wds.resone.launcher'" in s
assert '$DeployInstall' in s
assert 'leaving the live installation untouched' in s
assert 'if (!$DeployInstall)' in s and 'exit 0' in s
assert '[switch]$ForceDeploy' in s
print('launcher live-build regression: PASS')
