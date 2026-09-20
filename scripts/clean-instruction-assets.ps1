param(
  [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)
$dir = Join-Path $Root 'assets/Instructions/Music'
$canonical = @(
  'music-composition.json',
  'composition-tips.md',
  'resonator_api_v0.1.md',
  'interval_emotion_field_guide.md',
  'anchored_harmonic_divergence.md',
  'resone_song_design_genre_guide.md',
  'resone_drums_genre_guide.md'
)
if (!(Test-Path $dir)) { throw "Instruction directory not found: $dir" }
Get-ChildItem $dir -File | Where-Object { $_.Name -notin $canonical } | ForEach-Object {
  Write-Host "Removing unused instruction asset $($_.Name)"
  Remove-Item $_.FullName -Force
}
