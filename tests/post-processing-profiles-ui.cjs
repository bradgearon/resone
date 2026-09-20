const fs=require('fs');
const path=require('path');
const root=path.resolve(__dirname,'..');
const ui=fs.readFileSync(path.join(root,'src/wds.resone.ui/resources/web/app.js'),'utf8');
const html=fs.readFileSync(path.join(root,'src/wds.resone.ui/resources/web/index.html'),'utf8');
const css=fs.readFileSync(path.join(root,'src/wds.resone.ui/resources/web/style.css'),'utf8');
const contracts=fs.readFileSync(path.join(root,'src/wds.resone.api/Contracts.cs'),'utf8');
const processor=fs.readFileSync(path.join(root,'src/wds.resone.api/MusicPostProcessor.cs'),'utf8');
function must(v,m){if(!v)throw new Error(m)}
for (const line of [
  "Melody : 'Stay above E2 and below C6. Exceed octave 4 sparingly.'",
  "Chords : 'Stay above E2 and below C6. Exceed octave 4 sparingly.'",
  "Guitar : 'Stay above B1 and below C5. Exceed octave 3 sparingly.'",
  "Strings : 'Stay above E2 and below C5. Exceed octave 3 sparingly.'",
  "Vocals : 'Stay above E2 and below C5. Exceed octave 3 sparingly.'",
  "DEFAULT_GUITAR_DEEP_POST_PROCESSING = 'Stay above E0 and below C4. Exceed octave 2 sparingly.'"
]) must(ui.includes(line), 'missing post-processing default: '+line);
must(ui.includes('POST_PROCESSING_DEFAULTS_VERSION = 2'), 'missing persisted defaults migration');
must(ui.includes('hydratePostProcessingDefaults(l);'), 'legacy lanes are not hydrated');
must(ui.includes('currentNormal === previousNormal'), 'stock v1 normal profiles are not migrated');
must(ui.includes('currentDeep === previousDeep'), 'stock v1 deep profiles are not migrated');
must(ui.includes('postProcessingEnabled : defaultPostProcessingEnabled(name)'), 'default-enabled lane behavior missing');
must(ui.includes('target.postProcessingDefaultsVersion = POST_PROCESSING_DEFAULTS_VERSION'), 'saved lanes do not mark defaults initialized');
must(contracts.includes('PostProcessingDefaultsVersion'), 'defaults version not persisted through project.json');
must(ui.includes("deep.textContent = l.postProcessingDeepMode ? 'Deep on' : 'Deep'"), 'lane Deep button/state text missing');
must(ui.includes("postProcessCluster.append(postProcess, deep)"), 'Deep button is not stacked with PP');
must(ui.includes('l.postProcessingDeepMode = !l.postProcessingDeepMode'), 'lane Deep button does not switch profiles');
must(ui.includes('postProcessingDraft.deepMode = !postProcessingDraft.deepMode'), 'editor Deep button does not switch profiles');
must(html.includes('id="postProcessingProfileLabel"'), 'editor active profile label missing');
must(html.includes('class="postProcessingDeepButton"'), 'editor Deep button missing');
must(!html.includes('Applied after Composer'), 'removed Composer helper copy returned');
must(!html.includes('These instructions are stored with this lane'), 'removed storage helper copy returned');
must(css.includes('.postProcessCluster{grid-area:pp;display:flex;flex-direction:column'), 'PP/Deep lane controls are not vertically stacked');
must(css.includes('.deepModeButton.enabled') && css.includes('#7b3fc9'), 'Deep lane button lacks a strong purple enabled state');
must(css.includes('.postProcessButton.enabled::before'), 'PP styling lacks enabled indicator');
must(processor.includes('if (lane.PostProcessingDeepMode && !string.IsNullOrWhiteSpace(lane.PostProcessingDeepInstructions))'), 'Deep profile is still Guitar-only in post processor');
console.log('PASS generic persisted Normal/Deep post-processing profiles, defaults migration, default enable state, and compact PP/Deep UI');

must(ui.includes('postProcessingDialogElements()'), 'PP dialog lacks robust element check');
must(ui.includes('if (postProcessingEnabledControl)'), 'PP editor event binding is not null-safe');
must(css.includes('grid-template-areas:"song remove" "pp remove"'), 'lane controls do not place PP cluster below Song control');
