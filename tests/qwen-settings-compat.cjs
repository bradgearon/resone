const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const contracts = fs.readFileSync(path.join(root, 'src/wds.resone.api/Contracts.cs'), 'utf8');
const build = fs.readFileSync(path.join(root, 'scripts/build-windows.ps1'), 'utf8');
for (const token of ['QwenTtsExpectedVersionPrefix', 'QwenTtsLibraryName', 'QwenTtsServerName']) {
  if (!contracts.includes(token)) throw new Error(`Missing ResoneSettings compatibility/server field: ${token}`);
}
for (const token of ['qwenTtsServerName', 'qwenTtsStartupTimeoutSeconds', 'serviceDelays']) {
  if (!build.includes(`'${token}'`)) throw new Error(`Windows build does not propagate ${token}`);
}
for (const stale of ["'qwenTtsMode'", "'qwenTtsLibraryName'", "'qwenTtsExpectedVersionPrefix'"]) {
  const fieldList = build.match(/foreach \(\$NativeField in @\(([^)]*)\)\)/s)?.[1] || '';
  if (fieldList.includes(stale)) throw new Error(`Windows build still propagates stale field ${stale}`);
}
console.log('qwen settings compatibility regression passed');
