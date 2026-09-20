from pathlib import Path

root = Path(__file__).resolve().parents[1]
src = (root / "src/wds.resone.launcher/EnginePackInstaller.cs").read_text()

assert "CleanupDuplicateManagedInstalls" in src
assert "receipt.Url.Equals(pack.Url" in src
assert "receipt.Sha256.Equals(pack.Sha256" in src
assert "File.Exists(candidateReceiptPath)" in src
assert "CleanupDuplicateManagedInstalls(aiRoot, pack, target, progress);" in src
assert "Replace('\\\\', '/')" in src
print("duplicate engine folder regression: PASS")
