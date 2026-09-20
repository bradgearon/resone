import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'config/runtime.release.json').read_text())
release = manifest.get('release', {})
assert release.get('licensingEnabled') is False
assert release.get('encryptInstructions') is False

build_release = (root / 'scripts/build-release.ps1').read_text()
assert 'LicensingEnabled' in build_release and 'EncryptInstructions' in build_release
assert '$LicensingEnabled -and !(Test-Path $PublicKey' in build_release
assert '$EncryptInstructions -and !(Test-Path $InstructionKey' in build_release

build_windows = (root / 'scripts/build-windows.ps1').read_text()
assert 'ResoneLicensingEnabled=' in build_windows
assert 'ResoneEncryptInstructions=' in build_windows
assert 'Plain production instructions were not staged' in build_windows
assert 'enabled=$ReleaseLicensingEnabled' in build_windows

api_proj = (root / 'src/wds.resone.api/wds.resone.api.csproj').read_text()
assert "'$(ResoneEncryptInstructions)' == 'true'" in api_proj
assert 'RESONE_ENCRYPTED_INSTRUCTIONS' in api_proj

instruction_content = (root / 'src/wds.resone.api/InstructionContent.cs').read_text()
assert '#if RESONE_ENCRYPTED_INSTRUCTIONS' in instruction_content
assert '#if RESONE_CUSTOMER_RELEASE' not in instruction_content

launcher_proj = (root / 'src/wds.resone.launcher/wds.resone.launcher.csproj').read_text()
assert "'$(ResoneEncryptInstructions)' != 'true'" in launcher_proj
assert 'RESONE_LICENSED_RELEASE' in launcher_proj
assert 'RESONE_ENCRYPTED_INSTRUCTIONS' in launcher_proj
assert "'$(ResoneLicensingEnabled)' == 'true'" in launcher_proj

license_service = (root / 'src/wds.resone.launcher/LicenseService.cs').read_text()
assert '#if RESONE_LICENSED_RELEASE' in license_service
assert '#if RESONE_CUSTOMER_RELEASE' not in license_service

installer = (root / 'installer/Resone.iss').read_text()
assert '#if LicensingEnabled == 1' in installer
assert 'AiRootPage := CreateInputDirPage(wpWelcome' in installer
assert 'Result := True;' in installer

installer_build = (root / 'scripts/build-installer.ps1').read_text()
assert '/DLicensingEnabled=$LicensingDefine' in installer_build

worker = (root / 'src/wds.resone.launcher/WorkerHost.cs').read_text()
assert '["licensingEnabled"] = licensing.Enabled' in worker
ui = (root / 'src/wds.resone.ui/resources/web/app.js').read_text()
html = (root / 'src/wds.resone.ui/resources/web/index.html').read_text()
assert "licenseSettings.hidden = !p.licensingEnabled" in ui
assert 'id="licenseSettings"' in html

print('release protection flags regression: PASS')
