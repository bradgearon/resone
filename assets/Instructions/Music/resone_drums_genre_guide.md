# Resone Drums Genre Guide — Merged Grammar

## Purpose
This is the runtime drum/percussion genre reference used with `resone_song_design_genre_guide.md`. It merges the original Resone genre brief with the expanded Drum & Percussion Grammar.

The user's request, current song, current lane, producer identity, composer overview, and explicit rhythmic material are authoritative.

## Retrieval contract
Every retrieval gets `drum_core`. Then inherit the full family chain from broad to specific (for example `edm -> trance -> uplifting_trance`, or `classical -> romantic_classical -> late_romantic`). Add at most one separate compatible hybrid and one modifier when useful. Specific drum subgenres are matched independently from the song-design genre so details such as deep house, classic trance, jungle, grunge, or nu metal are not lost.

Do not merge unrelated grooves merely because fuzzy matching found them. For hybrids, combine **rhythmic functions**, not two complete incompatible drum kits.


# drum_core

**Canonical ID:** `drum_core`
**Aliases:** drum grammar, drums, percussion, drum basics, percussion basics

Use this compact base for every drum lane. The user's explicit rhythm and existing song always override genre defaults.

**Resone kit:** K=`C2` kick, S=`D2` snare, H=`F#2` closed hat, O=`A#2` open hat, L=`F2` low tom, M=`A2` mid tom, T=`D3` high tom, C=`C#3` crash, R=`D#3` ride. Use letters only while reasoning; emit mapped notes.

Before writing, decide: **pulse** (four-floor/backbeat/halftime/broken/swing/orchestral), **kick policy**, **snare anchor**, **hat/ride subdivision**, **fill language**, **energy move**, and **human feel**. Establish a recognizable groove before varying it.

Core atoms: four-floor = K each quarter; rock backbeat = S on 2/4 with riff-supporting K; halftime = S around beat 3; offbeat H/O creates dance lift; C+K marks a major arrival; R can widen a live climax; S rolls/tom fills belong at meaningful phrase boundaries.

Energy should usually change only a few dimensions at once: subdivision, selective K activity, H→O/R/C spectrum, velocity, rests, or fills. Builds may densify and briefly remove K before impact; drops/choruses should restore the defining anchor clearly.

Default phrase behavior: repeat enough to establish identity; make small variations every 2–4 bars and larger changes at real 8/16-bar or sectional boundaries. Fills must have a job. Kick and bass may hard-lock, partially lock, answer each other, or deliberately leave space.

**Central rule:** genre is a hierarchy of rhythmic expectations, not a fixed beat. Preserve the defining anchors and let the song determine the variations. Classical/orchestral guidance may call for sparse or absent kit percussion rather than a loop.

# rock

**Canonical ID:** `rock`
**Aliases:** rock, guitar rock, classic rock, arena rock, hard rock

**Pocket:** strong backbeat; kick supports the riff without tracing every note; hats/ride carry pulse.
**Verse:** tighter hats, simpler kick, lighter fills.
**Pre-chorus:** open hats/toms/more kick drive.
**Chorus:** crash or ride, stronger kick/snare, wider accents.
**Fills:** usually 1/4 to 1 bar; reuse a fill language so the drummer has identity.
**Humanization:** moderate velocity/timing variation.
**Avoid:** crash every bar, fill every bar, kick on every guitar attack.

- S on 2 and 4.
- H/R usually 8ths.
- K follows the riff and supports 1/3 plus syncopations.
- Every 4–8 bars, replace the last beat or two with S/tom fill.
- C + K marks section entrance.
- R often replaces H in choruses.

---


# alternative_indie_rock

**Canonical ID:** `alternative_indie_rock`
**Aliases:** alternative, alt rock, indie, indie rock, art rock
**Drum parent:** `rock`

**Pocket:** can be straight, loose, dry, awkward, minimalist, or repetitive.
**Color:** unusual snare placement, floor tom, rim-like patterns can replace standard backbeat behavior.
**Sections:** texture changes may matter more than density changes; abrupt dropouts are valid.
**Avoid:** generic arena-rock defaults and over-polished fills.

- Backbeat can be conventional or deliberately displaced.
- H patterns may be sparse.
- Use unusual kick omissions.
- Fills are often understated.

---


# punk_hardcore_punk

**Canonical ID:** `punk_hardcore_punk`
**Aliases:** punk, punk rock, hardcore punk, melodic hardcore, skate punk
**Drum parent:** `rock`

**Pocket:** fast direct backbeat, kick reinforces momentum, ride/crash may replace delicate hat detail.
**Transitions:** short snare/tom bursts, stops, immediate re-entry, half-time breakdowns.
**Avoid:** long ornate fills, delicate swing unless requested, excessive ghost-note detail.

**Tempo:** usually fast.

- S 2/4.
- H/R fast 8ths.
- K drives hard.
- Short fills.
- Double-time feel common.
- Simplicity + speed beats complexity.

---

- Fast, simple K/S.
- Strong 2/4 or double-time backbeat.
- H/R fast.
- Short fills.
- Breakdowns may switch abruptly to halftime.

---


# post_rock

**Canonical ID:** `post_rock`
**Aliases:** post rock, atmospheric rock, crescendo rock, instrumental post-rock

**Entry:** drums may arrive late.
**Build:** no drums -> pulse -> kick/tom frame -> backbeat -> open cymbals -> full crash-driven climax.
**Fills:** tom motion can become part of the crescendo.
**Avoid:** full rock groove from bar one and intensity resets every four bars.


# classic_rock

**Canonical ID:** `classic_rock`
**Aliases:** classic rock
**Drum parent:** `rock`

- Moderate K complexity.
- Strong human backbeat.
- 8th H/R.
- Tom fills are common.
- Let fills breathe; avoid machine-gun density.

---


# hard_rock

**Canonical ID:** `hard_rock`
**Aliases:** hard rock
**Drum parent:** `rock`

- Heavier K.
- Strong S.
- 8th H or R.
- Kick doubles before/after S increase drive.
- Fills often use repeated S followed by descending toms.
- C on choruses/riff arrivals.

---


# arena_rock

**Canonical ID:** `arena_rock`
**Aliases:** arena rock
**Drum parent:** `rock`

- Very clear backbeat.
- Big C.
- Simpler groove, larger accents.
- Tom fills can be broad and dramatic.
- Avoid tiny overcomplicated details.

---


# grunge

**Canonical ID:** `grunge`
**Aliases:** grunge
**Drum parent:** `rock`

- Heavy rock backbeat.
- Loose, human feel.
- Strong C/R in loud sections.
- Big tom fills.
- Avoid overly polished electronic hat rolls.

---


# pop_rock

**Canonical ID:** `pop_rock`
**Aliases:** pop rock
**Drum parent:** `rock`

- Clear K/S anchors.
- Consistent 8th H.
- Small fills before chorus.
- Strong C on chorus.
- Keep patterns readable.

---


# pop_punk

**Canonical ID:** `pop_punk`
**Aliases:** pop punk
**Drum parent:** `punk_hardcore_punk`

- Punk drive with cleaner phrase structure.
- Fast H/R.
- Clear S 2/4.
- Frequent short fills into sections.
- Crash-heavy choruses.

---


# post_punk

**Canonical ID:** `post_punk`
**Aliases:** post punk, post-punk
**Drum parent:** `rock`

- Repetitive groove.
- Tighter K/S.
- H/R can carry angular patterns.
- Fewer classic rock fills.
- Mechanical consistency can be desirable.

---


# progressive_rock

**Canonical ID:** `progressive_rock`
**Aliases:** prog rock, progressive rock, art prog, symphonic prog
**Drum parent:** `rock`

**Pocket:** alternate straight groove, odd meter, syncopation, and tom/orchestral passages.
**Meter:** changes should finish musical sentences naturally.
**Avoid:** treating every odd bar like a drum solo and sacrificing the emotional arc to technique.

- Preserve the meter and the riff's accent pattern.
- Let the groove change inside a phrase instead of repeating one bar forever.
- Toms can act like melodic voices, not just fills.
- Backbeats may move away from obvious 2/4 placement.
- Use fills to connect metric or riff changes.
- Odd meters should still have a simple internal anchor.
- Do not make every bar complicated; complexity works because stable ideas return.

### Progressive-rock core rule

Reduce progressive-rock drumming to:

1. **Find the riff accents.**
2. **Choose one repeating anchor** — K, S, H/R, or a tom pulse.
3. **Let one layer stay stable while another layer moves across it.**
4. **Use toms to answer or extend the riff.**
5. **Displace a backbeat occasionally instead of randomly.**
6. **Use one surprising fill near a phrase boundary, then return to the anchor.**

The groove should sound intentional even when the meter is unusual.

---


# prog_rock_odd_meter_heavy

**Canonical ID:** `prog_rock_odd_meter_heavy`
**Aliases:** odd meter prog, odd-meter prog, heavy odd meter prog, heavy odd-meter prog
**Drum parent:** `progressive_rock`

**Useful for:** dark, heavy, hypnotic progressive rock with odd meters, repeating riffs, and controlled complexity.

**Core idea:** make a strange meter feel inevitable.

**Rules:**
- Keep one pulse constant with **H** or **R**.
- Let **K** follow the main riff accents.
- Use **S** as a structural marker rather than automatically on 2 and 4.
- Group odd meters into small cells:
  - 5/4 = `3+2` or `2+3`
  - 7/8 = `2+2+3`, `3+2+2`, or `2+3+2`
  - 9/8 = `2+2+2+3` or `3+3+3`
- Repeat the grouping long enough for the listener to learn it.
- Then move one accent, omit one K, or answer with toms.
- Use **L/M/T** as part of the groove, not only at the end.
- Heavy sections can use a halftime-feeling S anchor inside the odd meter.
- Big transitions: short tom run -> **C + K** -> return to the core grouping.

**Simple example — 7/8 grouped 2+2+3:**
- H/R keeps all 7 eighth-note pulses.
- K accents pulse 1, 3, and 5.
- S marks pulse 5 or 7 depending on the riff.
- T/M/L can answer the last 3-note group.

**Energy increase:**
- keep the grouping;
- add kick doubles around riff accents;
- move H -> R;
- add tom answers;
- increase fill density only at phrase boundaries.

**Avoid:**
- random odd-meter accents;
- changing the grouping every bar;
- constant fills;
- making every limb equally busy.

---


# prog_rock_tom_architecture

**Canonical ID:** `prog_rock_tom_architecture`
**Aliases:** tribal prog, ritualistic prog, tom architecture, tom driven prog
**Drum parent:** `progressive_rock`

**Useful for:** tribal, ritualistic, cinematic, or heavy progressive passages.

**Core idea:** toms become part of the main sentence.

**Rules:**
- Start with one repeating tom cell.
- Move the cell across **T -> M -> L** or back upward.
- K reinforces only selected tom accents.
- S may disappear entirely for several bars.
- R or H can quietly preserve time underneath.
- Build intensity by expanding the tom cell, not simply playing faster.

**Example development:**
- bar 1: `T M L`
- bar 2: repeat
- bar 3: `T T M L`
- bar 4: `T M M L L`
- transition: `T T M M L L -> C + K`

---


# prog_rock_fusion_flow

**Canonical ID:** `prog_rock_fusion_flow`
**Aliases:** fusion prog, progressive fusion, technical prog fusion
**Drum parent:** `progressive_rock`

**Useful for:** technical progressive rock that still needs groove and musical conversation.

**Core idea:** complexity should sound like phrasing, not math homework.

**Rules:**
- H/R carries a readable pulse.
- K responds to bass/riff movement.
- S may ghost, answer, or move off the backbeat.
- Toms connect phrases.
- Use occasional triplet or 16th bursts against a simpler main groove.
- Return to a recognizable anchor every 1-2 bars.

**Good default:**
- simple groove for 1 bar;
- variation in bar 2;
- more adventurous fill in bar 4;
- return to original groove in bar 5.

---


# psychedelic_latin_progressive_rock

**Canonical ID:** `psychedelic_latin_progressive_rock`
**Aliases:** mars volta, mars-volta-like, chaotic latin prog, psychedelic prog, latin prog, frenzied latin jazz prog
**Drum parent:** `progressive_rock`

**Pocket:** elastic, explosive, syncopated; ghost notes, tom motion, open hats, percussion layers.
**Behavior:** kit may push against the riff instead of simply locking.
**Transitions:** eruption -> near silence -> frenetic pickup -> full return.
**Fills:** can cross barlines if landing remains clear.
**Avoid:** random busyness, stiff identical quantization, copying a specific song.

**Useful for:** explosive, highly mobile progressive rock with punk, Latin, jazz, and fusion energy.

**Core idea:** the kit feels constantly alive, but important accents still line up with the song.

**Rules:**
- K and S should converse rather than form a fixed rock loop.
- H/R can alternate between straight, swung, or broken subdivisions.
- Use rapid tom runs frequently, but attach them to phrase boundaries or riff answers.
- Short S bursts can interrupt the groove.
- Change cymbal surface often:
  - H for tightness
  - R for openness
  - C for explosive arrival
- Let one bar become very dense, then suddenly leave space.
- Use syncopated K around the riff instead of constant four-on-floor.
- Toms may begin before the barline and finish after it.
- Fast fills can use:
  - `S S T M`
  - `T M T L`
  - `T T M M L L`
  - mixed S + tom bursts
- Preserve at least one repeating accent or pulse so the result does not become random.

**Feel:** volatile, acrobatic, urgent, theatrical.

**Simple energy cycle:**
1. tight groove
2. syncopated K/S answer
3. dense tom/S burst
4. sudden space
5. C + K re-entry

---


# metal

**Canonical ID:** `metal`
**Aliases:** metal, heavy metal, extreme metal

**Pocket:** reinforce riff weight; kick may lock selected low attacks; snare keeps larger pulse clear.
**Double kick:** use as a structural texture, not wallpaper.
**Fills:** derive from riff grouping; expose the next riff entrance.
**Avoid:** cymbal on every accent, nonstop double kick, fills obscuring downbeats.

- Strong S backbeat when not in blast/double-time sections.
- K may use doubles.
- R/C open the sound.
- Toms bridge riffs.
- Match K accents to guitar/bass riff when context exists.

---


# traditional_heavy_metal

**Canonical ID:** `traditional_heavy_metal`
**Aliases:** traditional heavy metal, classic heavy metal
**Drum parent:** `metal`

- Rock-derived groove.
- More R.
- Strong tom fills.
- Kick doubles for emphasis, not constant machine-gun patterns.

---


# thrash_metal

**Canonical ID:** `thrash_metal`
**Aliases:** thrash, thrash metal, speed metal
**Drum parent:** `metal`

**Pocket:** fast kick drive, backbeat, ride/crash energy; gallop/pedal patterns may be reinforced.
**States:** fast skank-like drive, half-time stomp, double-kick run.
**Avoid:** over-syncopated snare against already-busy riffs and constant tom fills.

- Fast tempo.
- Double-time S feel.
- Rapid H/R.
- More continuous K or double-kick patterns.
- Short aggressive fills.

---


# death_metal

**Canonical ID:** `death_metal`
**Aliases:** death metal, technical death metal, brutal death, melodic death
**Drum parent:** `metal`

**States:** blast, double-kick drive, half-time crush, syncopated riff lock, tom-led transition.
**Technical material:** accents may mirror irregular cells while a larger pulse remains perceptible.
**Avoid:** maximum-density sameness, random kick transcription of every note.

- Dense K, often rapid/double-kick.
- S may use blast-beat logic.
- R/C can sustain aggression.
- Toms used in violent transitions.
- Density should still reflect riff accents.

---


# black_metal

**Canonical ID:** `black_metal`
**Aliases:** black metal, atmospheric black metal, symphonic black metal
**Drum parent:** `metal`

**Pocket:** blast/fast repetitive pulse can create a sustained field; cymbal texture is atmospheric.
**Development:** slowly change cymbal surface, kick density, snare emphasis.
**Avoid:** funk-like ghost complexity and too many abrupt groove changes in hypnotic sections.

- Blast-beat textures common:
  - rapid alternating/overlapping K and S
  - constant H/R/C pulse
- Cymbal wash can be intentional.
- Transitions may abruptly drop to half-time or tom-heavy patterns.

---


# doom_sludge

**Canonical ID:** `doom_sludge`
**Aliases:** doom, doom metal, sludge, sludge metal, stoner doom
**Drum parent:** `metal`

**Pocket:** slow, heavy, spacious; let hits decay; large gaps amplify weight.
**Fills:** slow tom descents, flams, short pickup into giant downbeat.
**Avoid:** busy hats, constant double kick, filling oppressive silence.

- Slow.
- Huge spaces.
- Heavy K and S.
- Sparse H/R.
- Toms and C should feel monumental.
- Do not add fast detail merely because it is metal.

---


# stoner_sludge

**Canonical ID:** `stoner_sludge`
**Aliases:** stoner metal, sludge, sludge metal, stoner doom
**Drum parent:** `doom_sludge`

- Slow-to-mid groove.
- Heavy backbeat.
- Loose K.
- Big C/R.
- Thick tom fills.
- Human drag is more important than precision.

---


# power_symphonic_metal

**Canonical ID:** `power_symphonic_metal`
**Aliases:** power metal, symphonic metal, epic metal, fantasy metal
**Drum parent:** `metal`

**Pocket:** driving eighths/double kick can support heroism; snare keeps anthem readable.
**Coordination:** reserve crashes/toms for orchestral structural hits.
**Avoid:** maximum double kick everywhere and overfilling dense orchestration.

- Fast, driving double-kick feel.
- S clear and heroic.
- R/C during climaxes.
- Tom rolls into major transitions.
- Rhythmic regularity supports melodic grandeur.

---


# metalcore

**Canonical ID:** `metalcore`
**Aliases:** metalcore, modern metalcore, melodic metalcore, post-hardcore metal
**Drum parent:** `metal`

**Verse:** riff-locked syncopation.
**Chorus:** simpler wider backbeat.
**Breakdown:** sparse low kick + snare/crash punctuation; leave air between impacts.
**Avoid:** breakdown groove under every section and hyperactive kick beneath emotional choruses.

- Alternate between:
  - driving metal groove
  - double-time
  - halftime breakdown
- Breakdown: S on beat 3, sparse huge K aligned with riff.
- Builds can use S rolls/tom runs.
- C on breakdown/chorus arrival.

---


# beatdown_hardcore

**Canonical ID:** `beatdown_hardcore`
**Aliases:** beatdown, hardcore breakdown, beatdown hardcore
**Drum parent:** `punk_hardcore_punk`

- Half-time S.
- Sparse K tightly matching riff accents.
- Large rests matter.
- C can punctuate hits.
- Avoid decorative hats that weaken the impact.

---


# post_hardcore

**Canonical ID:** `post_hardcore`
**Aliases:** post hardcore, post-hardcore
**Drum parent:** `rock`

- Rock/hardcore foundation.
- More dynamic transitions.
- Tom-heavy sections are useful.
- Move between sparse verses and crash-heavy peaks.

---


# djent_polyrhythmic_metal

**Canonical ID:** `djent_polyrhythmic_metal`
**Aliases:** djent, polyrhythmic metal, math metal, syncopated modern metal
**Drum parent:** `metal`

**Core:** learn the riff accent cycle.
**Grid:** cymbal/snare may preserve 4/4 while kick/guitar imply odd groupings.
**Polymeter:** keep one stable reference layer; let another follow the cycle.
**Avoid:** every limb/lane sharing every odd accent and random 'complex' kick patterns.

- S often supplies a stable backbeat/halftime anchor.
- K mirrors irregular riff accents.
- H/R can remain steady against polymetric guitar accents.
- Space and precision are critical.
- Do not randomize kick rhythm independently of the riff.

---


# polyrhythmic_progressive_metal

**Canonical ID:** `polyrhythmic_progressive_metal`
**Aliases:** polyrhythmic prog, polyrhythmic progressive metal, tool-ish, tool like, tool-like, hypnotic prog metal, tribal prog metal, Tool
**Drum parent:** `progressive_metal`

**Philosophy:** drums can be a compositional voice.
**Cycles:** establish repeating accent cycles long enough to internalize; hands/feet may imply different cycles.
**Toms:** can carry melodic/rhythmic motifs.
**Development:** add/subtract one beat, shift cycle start, move a pattern between kit voices, let cycles realign.
**Avoid:** random odd-meter fireworks, resetting before the cycle becomes memorable, copying a specific existing part.

**Useful for:** progressive metal / heavy prog where the riff seems to move against a stable pulse.

**Core idea:** one layer says **"the meter is stable"**, another says **"the riff is moving."**

**Rules:**
- Keep H/R on a steady subdivision.
- Let K mirror the guitar/bass riff.
- Let S provide a slower repeating anchor.
- A repeated K pattern can cross the barline while H/R keeps the listener oriented.
- Do not change all layers together.
- When the riff resolves, use C or a short tom fill to reveal the downbeat again.

**Very simple formula:**

`steady H/R + riff-following K + slower S anchor`

That is enough to create a polymetric feeling without complicated notation.

---


# progressive_metal

**Canonical ID:** `progressive_metal`
**Aliases:** prog metal, progressive metal, technical progressive metal, cinematic prog metal

**Development:** transform drum motifs alongside musical motifs.
**Meter:** changes should serve phrase architecture; preserve subdivision through transitions when useful.
**Avoid:** technical display with no recurring drum identity and novelty cymbal switching for its own sake.


# nu_metal

**Canonical ID:** `nu_metal`
**Aliases:** nu metal, nu-metal
**Drum parent:** `metal`

- Heavy halftime or syncopated rock groove.
- K locks to riff.
- S is broad and obvious.
- H relatively simple.
- Hip-hop influence can justify sparse kick patterns.

---


# edm

**Canonical ID:** `edm`
**Aliases:** EDM, electronic dance, dance music, club electronic

**Family fundamentals:** EDM-family drums communicate energy state through a legible pulse, repetition, subdivision, layer entry/removal, and contrast. Four-on-the-floor styles normally keep K dependable; broken-beat styles preserve their defining K/S relationship instead. Builds may increase H/S subdivision and briefly remove K before a payoff; drops should re-establish the defining physical pulse clearly. Avoid changing the fundamental groove every bar.

**Core:** repetition must be extremely legible; drum changes communicate energy state.
**Build:** increase hat/snare subdivision, remove kick near peak, reserve fill for final bar, use pre-drop silence.
**Drop:** immediately re-establish a clear physical pulse.
**Avoid:** acoustic-rock fill habits by default and changing the kick pattern every bar.


# house

**Canonical ID:** `house`
**Aliases:** house, deep house, tech house, progressive house
**Drum parent:** `edm`

**Core:** four-on-the-floor kick; clap/snare commonly 2 and 4; offbeat hats create lift.
**Deep:** softer percussion and subtle syncopation.
**Tech:** percussive hooks and small mutes.
**Progressive:** layer density changes over 8/16/32 bars.
**Avoid:** unnecessary kick omissions and giant fills every four bars.

**Tempo:** commonly ~115–130 BPM.

Science:
- K every quarter.
- S commonly reinforces 2 and 4.
- H/O on offbeats.
- Additional H can fill 8ths or 16ths.
- Small fills every 4–8 bars.
- C on major section changes.

Skeleton:
**K: 1 2 3 4**
**S: 2 4**
**O/H: offbeats**

---


# deep_house

**Canonical ID:** `deep_house`
**Aliases:** deep house
**Drum parent:** `house`

- Four-on-floor K.
- Softer S.
- Offbeat H/O.
- Sparse extra percussion.
- Leave breathing room.
- Avoid constant aggressive rolls.
- Micro-syncopation matters more than density.

**Feel:** relaxed, warm, hypnotic.

---


# tech_house

**Canonical ID:** `tech_house`
**Aliases:** tech house
**Drum parent:** `house`

- Four-on-floor K stays dominant.
- Strong offbeat or 16th hats.
- More syncopated percussive gaps and answers.
- Short S/tom fills.
- Repetition with tiny mutations every 2–4 bars.

**Feel:** dry, physical, cheeky, looping.

---


# progressive_house

**Canonical ID:** `progressive_house`
**Aliases:** progressive house, prog house
**Drum parent:** `house`

- Four-on-floor foundation.
- Gradually add hats/percussion over long phrases.
- Fills are restrained.
- Open hat and crash mark structural growth.
- Use density as arrangement automation.

**Feel:** patient expansion.

---


# big_room_festival_house

**Canonical ID:** `big_room_festival_house`
**Aliases:** big room house, big-room house, festival house, big room
**Drum parent:** `house`

- Huge four-on-floor K.
- S/clap on 2 and 4.
- Builds: increasing S roll density.
- Drop: simplify back to giant K + strong backbeat.
- C on drop entrance.
- Avoid overcomplicated groove at the main drop.

**Feel:** obvious, massive, communal.

---


# electro_house

**Canonical ID:** `electro_house`
**Aliases:** electro house
**Drum parent:** `house`

- Four-on-floor or aggressively syncopated K.
- Strong S 2/4.
- 16th hats or broken hats.
- Abrupt fills and rests can answer synth riffs.

**Feel:** hard-edged, mechanical, punchy.

---


# trance

**Canonical ID:** `trance`
**Aliases:** trance, classic trance, progressive trance
**Drum parent:** `edm`

**Core:** stable dance pulse and long-section consistency.
**Build:** increase upper subdivision, add snare roll gradually, pull kick before peak.
**Breakdown:** drums may nearly disappear.
**Avoid:** busy breakbeat kick during main trance groove and excessive fills.

**Tempo:** commonly ~128–140 BPM.

Reduce trance drums to:
1. **K every quarter.**
2. **H/O between kicks.**
3. **S on 2 and 4** or a clap/snare layer implied by S.
4. **16th closed hats** when energy rises.
5. **Short snare rolls** before 4/8/16-bar boundaries.
6. **C + K** on important arrivals.
7. Groove should be regular enough that harmony/melody can provide emotional complexity.

Basic trance:
- K: 1,2,3,4
- S: 2,4
- O: offbeats
- H: optional 16th stream with accents

---


# classic_trance

**Canonical ID:** `classic_trance`
**Aliases:** classic trance, old school trance, old-school trance, 90s trance, early trance, melodic trance
**Drum parent:** `trance`

**Aliases:** classic trance, old school trance, old-school trance, 90s trance, early trance, melodic trance

**Base:** TRANCE_BASE.

**Overrides:**
- Simpler and more hypnotic than modern festival trance.
- Fewer hyper-detailed hat patterns.
- Longer repeated groove cells.
- Less dependence on giant snare-build clichés.
- H/O evolution should happen gradually.
- Fills are usually small and functional.
- K remains extremely dependable.

**Feel:** hypnotic, driving, melodic, spacious.

**Example concept:**

Bars 1-2:
- K quarters
- O offbeats
- S 2/4

Bars 3-4:
- same groove
- add a few H subdivisions
- tiny S pickup into the next phrase

---

**Purpose:** default classic trance groove.

- K: quarters.
- S: 2 and 4.
- O: every offbeat.
- H: optional sparse 16ths.
- Fill: 1-beat S or tom fill every 4-8 bars.
- Arrival: C + K.

**Use for:** classic trance, melodic trance, general trance.

---


# uplifting_trance

**Canonical ID:** `uplifting_trance`
**Aliases:** uplifting trance, euphoric trance, emotional trance, anthem trance, anthemic trance
**Drum parent:** `trance`

**Core:** clean powerful kick, crisp clap/snare, offbeat hats.
**Build:** percussion density climbs with harmonic/melodic ascent; final vacuum before return.
**Climax:** full groove at maximum clarity, not maximum complexity.
**Avoid:** unnecessary kick complexity under the anthem.

- Firm four-on-floor K.
- Bright O on offbeats.
- 16th H in energetic passages.
- Long builds with S rolls that accelerate or crescendo.
- Big C on release/drop.
- Breakdown may remove K almost entirely.

**Feel:** ascent → suspension → release.

---

**Aliases:** uplifting trance, euphoric trance, emotional trance, anthem trance, anthemic trance

**Base:** TRANCE_BASE.

**Overrides:**
- Bright, obvious O on offbeats.
- More 16th H during high-energy passages.
- Stronger and longer S-roll builds.
- C + K arrivals should feel large.
- Breakdown can become nearly percussionless.
- The return should feel like release after suspension.
- Preserve regularity so melody/harmony can carry emotional complexity.

**Feel:** ascent -> suspension -> release.

**Typical 8-bar energy shape:**
- bars 1-2: K + O
- bars 3-4: add S 2/4 and H
- bars 5-6: denser H
- bar 7: S roll starts
- bar 8: S roll intensifies, brief gap
- next bar: C + K full return

---

**Purpose:** bright high-energy uplift.

- K: quarters.
- S: 2 and 4.
- O: offbeats.
- H: denser 16ths.
- Build: S 8ths -> 16ths.
- Last beat may briefly thin before arrival.
- Arrival: C + K + full H/O.

**Use for:** uplifting, euphoric, anthem trance.

---


# progressive_trance

**Canonical ID:** `progressive_trance`
**Aliases:** progressive trance, prog trance, deep trance, progressive melodic trance
**Drum parent:** `trance`

- Same fundamental pulse as trance.
- Fewer fills.
- More gradual density changes.
- Use H/O pattern changes rather than constant S rolls.
- Delay the full 16th hat texture.

**Feel:** restrained hypnotic growth.

---

**Aliases:** progressive trance, prog trance, deep trance, progressive melodic trance

**Base:** TRANCE_BASE.

**Overrides:**
- Fewer fills.
- Slower density changes.
- Delay full 16th hats.
- Let one small percussion change carry several bars.
- O/H pattern evolution is more important than constant S rolls.
- Maintain continuity over 8-16 bar spans.

**Feel:** restrained hypnotic growth.

**Typical development:**
- 4 bars K only or K + sparse H
- add O
- later add S
- later add fuller H
- save strong fill for actual section boundary

---

**Purpose:** long hypnotic evolution.

- K: quarters.
- O: sparse at first.
- S: delayed or soft.
- H: added gradually.
- Fills: minimal.
- Change only one upper-percussion idea every 2-4 bars.

**Use for:** progressive trance, deep trance.

---


# psytrance

**Canonical ID:** `psytrance`
**Aliases:** psytrance, psy trance, psychedelic trance, goa, goa trance
**Drum parent:** `trance`

**Core:** relentless stable kick/bass engine; upper percussion supplies psychedelic mutation.
**Development:** change layers without breaking the engine; use quick fills selectively.
**Avoid:** rock backbeat as primary identity and fills that interrupt rolling bass.

**Tempo:** often ~138–150+.

- Unbroken four-on-floor K.
- Very consistent pulse.
- H can run 16ths with selective accents.
- S is less dominant than in rock/house; use strategically.
- Tiny fills, glitches, and hat changes can occur at phrase edges.
- Do not disrupt the kick pulse casually.

**Feel:** relentless, precise, psychedelic.

---

**Aliases:** psytrance, psy trance, psychedelic trance, full-on psy, full on psy

**Base:** TRANCE_BASE.

**Overrides:**
- K is relentless and precise.
- H can run tight 16ths.
- S is usually less dominant than in house/trance-pop.
- Use micro-fills rather than giant backbeat fills.
- Do not interrupt K without a strong structural reason.
- Tiny H omissions and accents can create motion.
- Percussion should interlock tightly with bass.

**Feel:** relentless, precise, psychedelic.

**Typical bar:**
- K every quarter
- H 16ths with selected accents
- sparse S/percussion accents
- tiny end-of-phrase fill every 4-8 bars

---

**Purpose:** relentless precise propulsion.

- K: every quarter without interruption.
- H: tight 16ths.
- S: sparse.
- Micro-fill: tiny H/S variation at phrase boundary.
- No oversized rock-like fill.

**Use for:** psytrance, full-on psy.

---


# full_on_psytrance

**Canonical ID:** `full_on_psytrance`
**Aliases:** full on psytrance, full-on psytrance, full on psy, morning psy
**Drum parent:** `psytrance`

**Aliases:** full-on psytrance, full on psytrance, full-on psy, morning psy

**Base:** PSYTRANCE.

**Overrides:**
- Brighter and more energetic upper percussion.
- More frequent 16th H.
- Slightly more obvious phrase punctuation.
- Keep K/bass interaction extremely stable.
- Builds can increase H/S density, but avoid breaking the pulse.

---


# dark_psytrance

**Canonical ID:** `dark_psytrance`
**Aliases:** dark psy, darkpsy, dark psytrance, forest psy, forest trance
**Drum parent:** `psytrance`

**Aliases:** dark psy, darkpsy, dark psytrance, forest psy, forest trance

**Base:** PSYTRANCE.

**Overrides:**
- Darker sparse accents.
- More abrupt H gaps.
- Less bright O.
- Short disorienting fills.
- Keep the underlying K extremely stable so strange upper rhythm remains intelligible.

---


# goa_trance

**Canonical ID:** `goa_trance`
**Aliases:** goa, goa trance, old school goa, psychedelic goa
**Drum parent:** `trance`

- Psytrance foundation.
- Slightly more flowing/cyclic percussion.
- Longer evolutionary phrases.
- Fewer giant modern EDM build clichés.

---

**Aliases:** goa, goa trance, old school goa, psychedelic goa

**Base:** TRANCE_BASE.

**Overrides:**
- More cyclic percussion logic.
- Long evolving repeated cells.
- Less modern build/drop punctuation.
- Prefer gradual mutation to obvious festival transitions.
- Small H/S changes can rotate through the phrase.
- Keep K stable while upper percussion evolves.

**Feel:** continuous, psychedelic, ritualistic, spiraling.

**Example mutation cycle:**
- phrase A: K + O
- phrase B: add H on selected 16ths
- phrase C: alter H accents, not the K
- phrase D: small S/tom transition
- return to A with one new accent

---

**Purpose:** cyclic psychedelic motion.

- K: quarters.
- O/H: repeating upper-percussion cell.
- Every 2-4 bars shift one H accent.
- S/tom transition only near longer boundary.
- Avoid giant modern build.

**Use for:** Goa, old-school psychedelic trance.

---


# tech_trance

**Canonical ID:** `tech_trance`
**Aliases:** tech trance, tech-trance, techno trance
**Drum parent:** `trance`

- Four-on-floor.
- Harder K.
- Stronger S.
- Darker, more mechanical 16th H.
- Short techno-like fills.
- More abrupt drop transitions.

---

**Aliases:** tech trance, techno trance, tech-trance

**Base:** TRANCE_BASE.

**Overrides:**
- Harder K.
- Stronger S.
- Darker H.
- Short mechanical fills.
- More abrupt section transitions.
- Repetition should feel forceful rather than dreamy.
- Can borrow techno-like ride patterns at peaks.

**Feel:** mechanical propulsion, pressure, force.

---

**Purpose:** forceful mechanical trance.

- K: quarters.
- S: hard 2/4.
- H: tight dark 16ths or 8ths.
- O: selective.
- Fill: short mechanical S burst.
- Peak: R may replace H.

**Use for:** tech trance.

---


# hard_trance

**Canonical ID:** `hard_trance`
**Aliases:** hard trance, hardtrance, hard dance trance
**Drum parent:** `trance`

- Strong four-on-floor K.
- Heavy S 2/4.
- Frequent open hats.
- More rolls and cymbal punctuation than progressive trance.
- Rhythms may feel almost hardstyle-adjacent.

---

**Aliases:** hard trance, hardtrance, hard dance trance

**Base:** TRANCE_BASE.

**Overrides:**
- Heavier K and S.
- More aggressive O.
- More frequent rolls.
- More cymbal punctuation.
- Greater rhythmic density than progressive/classic trance.
- May approach hardstyle energy without adopting hardstyle kick behavior.

**Feel:** euphoric aggression.

---

**Purpose:** aggressive rave drive.

- K: quarters.
- S: strong 2/4.
- H/O: dense.
- Rolls: more frequent.
- C: stronger phrase punctuation.
- Preserve trance pulse even at high density.

**Use for:** hard trance.

---


# vocal_trance

**Canonical ID:** `vocal_trance`
**Aliases:** vocal trance, vocal uplifting trance, vocal progressive trance
**Drum parent:** `trance`

**Aliases:** vocal trance, vocal uplifting trance, vocal progressive trance

**Base:** usually UPLIFTING_TRANCE or PROGRESSIVE_TRANCE.

**Overrides:**
- Verse drums should leave more space.
- Reduce H density under vocals.
- Save large S rolls for transitions.
- Choruses/drops can widen with O, H, and C.
- Do not let percussion compete with the vocal phrase.

---

**Purpose:** leave space under vocals.

- K: quarters or slightly reduced.
- S: soft 2/4.
- O: selective.
- H: sparse 8ths.
- no long roll until phrase transition.
- C only at significant section change.

---


# euro_trance

**Canonical ID:** `euro_trance`
**Aliases:** euro trance, commercial trance, radio trance, eurodance trance
**Drum parent:** `trance`

**Aliases:** euro trance, commercial trance, radio trance, eurodance trance

**Base:** TRANCE_BASE.

**Overrides:**
- Extremely clear four-on-floor.
- Obvious S 2/4.
- Bright O.
- Regular 8th/16th H.
- Predictable short fills.
- Strong C at section arrivals.
- Less subtle than progressive trance.

**Feel:** immediate, catchy, dance-forward.

---


# techno

**Canonical ID:** `techno`
**Aliases:** techno, minimal techno, industrial techno, melodic techno
**Drum parent:** `edm`

**Core:** repetitive kick anchor with microscopic percussion evolution.
**Development:** add/remove a hat, shift one accent, introduce ride only at higher energy.
**Industrial:** harder metallic-like punctuation.
**Avoid:** song-section drumming that completely resets every 8 bars and decorative fill excess.

**Tempo:** commonly ~125–150 depending subtype.

Science:
- Repetition is a feature.
- K often anchors every quarter.
- H/O and percussion create evolution.
- S may be sparse, backbeat-like, or absent.
- Change one small rhythmic variable at a time.

---


# minimal_techno

**Canonical ID:** `minimal_techno`
**Aliases:** minimal techno
**Drum parent:** `techno`

- K stable.
- Very few voices at once.
- H patterns create motion.
- Tiny omissions/additions are meaningful.
- Fills should be extremely small.

---


# dub_techno

**Canonical ID:** `dub_techno`
**Aliases:** dub techno
**Drum parent:** `techno`

- Four-on-floor or restrained pulse.
- Sparse H/O.
- Percussion has space.
- Avoid busy fills.
- Let echoes/harmony carry movement.

---


# driving_techno

**Canonical ID:** `driving_techno`
**Aliases:** peak time techno, peak-time techno, driving techno
**Drum parent:** `techno`

- Heavy four-on-floor.
- 16th H or driving 8ths.
- S/clap often emphasizes 2/4 or phrase accents.
- Short rolls into transitions.
- Ride can enter for peak energy.

---


# hard_techno

**Canonical ID:** `hard_techno`
**Aliases:** hard techno
**Drum parent:** `techno`

- Faster/heavier four-on-floor.
- Dense H.
- More S rolls.
- Frequent industrial accents.
- Short gaps before K returns can increase impact.

---


# acid_techno

**Canonical ID:** `acid_techno`
**Aliases:** acid techno
**Drum parent:** `techno`

- Techno pulse.
- Keep drums fairly repetitive so acid-line movement remains readable.
- H and S fills can answer acid phrases.
- Crash/ride only at major changes.

---


# hardcore_gabber

**Canonical ID:** `hardcore_gabber`
**Aliases:** hardcore techno, gabber, frenchcore, uptempo hardcore, hardcore
**Drum parent:** `edm`

**Core:** aggressive kick dominance; percussion supports kick impact.
**Structure:** contrast full-force sections with stripped breaks; short snare builds into re-entry.
**Avoid:** subtle ghost complexity that vanishes under the kick and permanent cymbal wash.

**Tempo:** often ~160–200+.

- K dominates every quarter, usually extremely hard.
- S can reinforce backbeats or fills.
- H at 8ths/16ths.
- Frequent S rolls and rapid fills.
- C on section hits.
- At extreme tempos, fewer simultaneous decorations keep the groove legible.

**Feel:** impact first.

---


# happy_hardcore

**Canonical ID:** `happy_hardcore`
**Aliases:** happy hardcore
**Drum parent:** `hardcore_gabber`

- Fast four-on-floor K.
- Strong 2/4 S.
- Bright offbeat O.
- 16th H.
- Energetic S rolls.
- Frequent C transitions.

**Feel:** euphoric + hyperactive.

---


# hardstyle

**Canonical ID:** `hardstyle`
**Aliases:** hardstyle, euphoric hardstyle, rawstyle, raw hardstyle
**Drum parent:** `edm`

**Core:** kick is tonal/rhythmic centerpiece; main pulse unmistakable.
**Euphoric:** cleaner percussion beneath melody.
**Raw:** sparser upper percussion focused on kick character.
**Avoid:** acoustic-drum fill language dominating and overbusy hats beneath dense kick sequences.

- Four-on-floor K.
- Strong backbeat support.
- Offbeat H/O.
- Build sections can use S rolls.
- Main groove should leave space for the characteristic kick tail.
- Avoid filling every 16th with percussion.

---


# drum_and_bass

**Canonical ID:** `drum_and_bass`
**Aliases:** dnb, drum and bass, drum & bass, jungle, liquid dnb, neuro dnb
**Drum parent:** `edm`

**Core:** fast break-derived syncopation; snare anchors while kick creates propulsion.
**Liquid:** smoother/lighter fills.
**Neuro:** tighter mechanical interaction with bass design.
**Jungle:** chopped break variation and ghost notes.
**Avoid:** four-on-the-floor simplification and identical velocity on all break accents.

**Tempo:** often ~160–180 BPM.

Core principle:
- **Broken kick/snare pattern**, not four-on-floor.
- S often strongly anchors beat 2 and/or 4 in a fast grid.
- H supplies 8th/16th momentum.
- Ghosted K and S create syncopation.
- Think in 2-bar phrases.

A common conceptual skeleton:
- K near beat 1
- S near beat 2
- another K between major anchors
- S near beat 4

Do not reduce DnB to constant kicks.

---


# liquid_dnb

**Canonical ID:** `liquid_dnb`
**Aliases:** liquid dnb, liquid drum and bass
**Drum parent:** `drum_and_bass`

- Broken K/S groove.
- Softer S.
- Smooth 8th/16th H.
- Fewer violent fills.
- Maintain forward motion without clutter.

---


# neurofunk

**Canonical ID:** `neurofunk`
**Aliases:** neurofunk, neuro dnb
**Drum parent:** `drum_and_bass`

- Precise broken K/S.
- More syncopation.
- Short H gaps and accents.
- Aggressive micro-fills.
- Rhythmic interaction with bass is critical.

---


# jump_up_dnb

**Canonical ID:** `jump_up_dnb`
**Aliases:** jump up dnb, jump-up dnb, jump up drum and bass
**Drum parent:** `drum_and_bass`

- Very clear K/S anchors.
- Bouncy syncopation.
- Simpler than neurofunk.
- Short fills and obvious phrase punctuation.

---


# jungle

**Canonical ID:** `jungle`
**Aliases:** jungle
**Drum parent:** `drum_and_bass`

- Breakbeat logic.
- Fast chopped S patterns.
- Ghost S and K.
- Syncopation and irregular accents.
- H/ride can imply sampled break texture.
- Rolls may cross beat boundaries.

**Feel:** restless, human, chopped.

---


# breakbeat

**Canonical ID:** `breakbeat`
**Aliases:** breakbeat, breaks, electro breaks
**Drum parent:** `edm`

- Broken K.
- Backbeat S.
- Syncopated extra K around 16ths/8ths.
- H follows groove rather than machine-straight four-on-floor.
- Fills can be snare or tom based.

---


# big_beat

**Canonical ID:** `big_beat`
**Aliases:** big beat
**Drum parent:** `breakbeat`

- Breakbeat skeleton.
- Heavy K/S.
- Simple, oversized accents.
- Crash and tom fills are welcome.
- Often rock-like in weight.

---


# uk_garage

**Canonical ID:** `uk_garage`
**Aliases:** uk garage, garage
**Drum parent:** `edm`

- Broken K rather than four-on-floor.
- S/clap around 2/4.
- Shuffled H.
- Syncopation is essential.
- Leave holes.

---


# two_step_garage

**Canonical ID:** `two_step_garage`
**Aliases:** 2 step garage, 2-step garage, two step garage, 2 step
**Drum parent:** `uk_garage`

- Intentionally omit some expected kicks.
- S remains a recognizable anchor.
- H swings/shuffles.
- The empty spaces create the groove.

---


# electro_breakbeat

**Canonical ID:** `electro_breakbeat`
**Aliases:** electro, electro breaks, breaks, breakbeat, big beat

**Core:** syncopated kick/snare, ghost notes, displaced kicks.
**Fills:** chopped extensions of the groove; reuse fragments.
**Avoid:** straight house behavior unless blended and random syncopation without pocket.


# dubstep_bass_music

**Canonical ID:** `dubstep_bass_music`
**Aliases:** dubstep, bass music, melodic dubstep, brostep
**Drum parent:** `edm`

**Core:** half-time large pulse; kick converses with bass; space is critical.
**Build:** increase hats/snare repetition then remove drums before drop.
**Drop:** simple kick/snare frame while bass supplies internal rhythm.
**Avoid:** continuous kick activity and cymbal wash over bass articulation.

**Tempo:** commonly around 140 with halftime perception.

- Half-time S centered on beat 3.
- Sparse, powerful K.
- H often 8ths/16ths with syncopated gaps.
- Big empty spaces.
- Short S fills lead into drops.

---


# brostep

**Canonical ID:** `brostep`
**Aliases:** brostep
**Drum parent:** `dubstep_bass_music`

- Dubstep halftime anchor.
- More aggressive K around bass accents.
- More fills, C, and H bursts.
- Drums can answer sound-design gestures.

---


# future_bass

**Canonical ID:** `future_bass`
**Aliases:** future bass
**Drum parent:** `edm`

- Half-time or broken beat.
- S on strong halftime anchor.
- Syncopated K.
- H can use quick bursts/rolls.
- Cymbal accents support chord swells.

---


# edm_trap

**Canonical ID:** `edm_trap`
**Aliases:** edm trap, festival trap
**Drum parent:** `edm`

- Half-time S.
- Deep syncopated K.
- H uses 8ths, 16ths, 32nd-style bursts, and triplet rolls.
- Leave space between kick events.
- Hat density is a major energy control.

---


# jersey_club

**Canonical ID:** `jersey_club`
**Aliases:** jersey club
**Drum parent:** `edm`

- Fast, syncopated K pattern.
- Repeated kick figures are central.
- S/clap anchors dance pulse.
- Abrupt gaps and repetitions are part of the feel.

---


# synthwave_outrun

**Canonical ID:** `synthwave_outrun`
**Aliases:** synthwave, outrun
**Drum parent:** `edm`

- Electronic rock-like backbeat.
- K on 1/3 or more driving variations.
- S on 2/4.
- Steady 8th H.
- Tom fills evoke 1980s production.
- Large C transitions.

---


# hip_hop_rap

**Canonical ID:** `hip_hop_rap`
**Aliases:** hip hop, hip-hop, rap, modern hip hop, rap beat, urban beat

**Pocket:** leave space for phrasing; kick/snare are conversational.
**Sections:** verse sparse; hook may add kick weight, clap layer, open hat, crash, or percussion.
**Humanization:** often important; timing/velocity matter.
**Avoid:** fill every four bars and rock-style accompaniment defaults.

- S/clap remains a clear anchor.
- K can be sparse and syncopated.
- H ranges from simple 8ths to detailed rolls.
- Space is valuable.

---


# boom_bap

**Canonical ID:** `boom_bap`
**Aliases:** boom bap, boombap, old school hip hop, east coast hip hop
**Drum parent:** `hip_hop_rap`

**Pocket:** strong kick/snare conversation; laid-back or swung feel often useful.
**Texture:** ghost snares and uneven hat velocities can create head-nod feel.
**Fills:** tiny pickups or sample-like interruptions.
**Avoid:** hyperactive trap hats and rigid dance quantization.

**Tempo:** commonly ~75–100 BPM.

- K and S should converse.
- S strongly anchors 2 and 4.
- K syncopates before/after those anchors.
- H usually 8ths or lightly swung 16ths.
- Small ghost S can add human feel.
- Do not overfill.

**Feel:** head-nod pocket.

---


# trap

**Canonical ID:** `trap`
**Aliases:** trap, trap beat, melodic trap, dark trap, rage trap
**Drum parent:** `hip_hop_rap`

**Core:** snare/clap anchors the large pulse; hats are a major expressive layer.
**Hats:** establish a base subdivision, then use rolls/triplets/stutters/open hats selectively.
**Kick:** sparse and intentional; coordinate with 808 without duplicating every glide.
**Avoid:** roll spam, rock fills, kick on every bass note.

**Tempo:** often perceived around 60–80 halftime or 120–160 double grid.

Science:
- S on the halftime anchor, commonly beat 3.
- K is sparse, syncopated, bass-aware.
- H provides most fine rhythmic detail.
- Hat vocabulary: 8ths → 16ths → short rapid bursts → triplets.
- Rolls should highlight words/transitions, not run constantly.

---


# drill

**Canonical ID:** `drill`
**Aliases:** drill, uk drill, ny drill, dark drill
**Drum parent:** `hip_hop_rap`

**Core:** sparse asymmetric kick around moving bass; recognizable snare/clap anchor.
**Hats:** triplet/roll detail while retaining dark space.
**Avoid:** over-dense kick, cheerful constant open hats, generic trap symmetry.

- Half-time S anchor.
- K is highly syncopated and often avoids obvious downbeats.
- H can be comparatively restrained.
- Sliding bass rhythm and kick should interlock.
- Use sudden empty spaces.

---


# grime

**Canonical ID:** `grime`
**Aliases:** grime
**Drum parent:** `hip_hop_rap`

- Sparse hard K/S.
- Strong halftime-ish framework.
- Angular syncopation.
- Less decorative hat density than trap in many patterns.

---


# lofi_hip_hop

**Canonical ID:** `lofi_hip_hop`
**Aliases:** lofi, lo-fi, lo fi hip hop, chillhop, study beats
**Drum parent:** `hip_hop_rap`

**Pocket:** relaxed; slightly late snare/loose hats can work; lower velocities.
**Variation:** remove one kick, add one ghost, change hat velocity, tiny boundary fill.
**Avoid:** pristine rigid quantization, EDM fills, dense kick patterns.

- Boom-bap logic.
- Softer K/S.
- Laid-back H.
- Slight swing.
- Fewer fills.
- Repetition is comforting.

---


# phonk

**Canonical ID:** `phonk`
**Aliases:** phonk
**Drum parent:** `hip_hop_rap`

- Hip-hop/trap skeleton.
- Strong K.
- S/clap anchor.
- Fast hats possible.
- Cowbell is stylistic but not in the current core Resone kit; do not fake it with another mapped drum unless requested.

---


# funk

**Canonical ID:** `funk`
**Aliases:** funk

- K highly syncopated.
- S backbeat plus ghost-style answers.
- H often 16ths with accents/openings.
- Groove depends on interplay, not raw density.
- Small omissions create pocket.

---


# soul

**Canonical ID:** `soul`
**Aliases:** soul

- Strong backbeat.
- Moderate K.
- H 8ths.
- Tasteful fills.
- Human feel and restraint.

---


# motown

**Canonical ID:** `motown`
**Aliases:** motown, motown style
**Drum parent:** `soul`

- Clear pulse.
- Strong S.
- Tambourine would often reinforce beats, but current core kit lacks it.
- K is busier than simple pop but still song-serving.

---


# disco

**Canonical ID:** `disco`
**Aliases:** disco
**Drum parent:** `edm`

- Four-on-floor K.
- S 2/4.
- O on offbeats.
- H often 16ths.
- Very regular dance pulse.
- C at structural changes.

---


# contemporary_rnb

**Canonical ID:** `contemporary_rnb`
**Aliases:** r&b, rnb, contemporary r&b, contemporary rnb
**Drum parent:** `soul`

- Sparse K.
- Strong but often soft S/clap anchor.
- H may use subtle 16th details.
- Silence and anticipation matter.
- Avoid dense rock fills.

---


# neo_soul

**Canonical ID:** `neo_soul`
**Aliases:** neo soul, neo-soul
**Drum parent:** `soul`

- Behind-the-beat feel.
- Syncopated K.
- S backbeat.
- H can swing or use broken 16ths.
- Keep it human and slightly asymmetrical.

---


# jazz_swing

**Canonical ID:** `jazz_swing`
**Aliases:** jazz, jazz swing, swing jazz

- R is the primary timekeeper.
- Implied swing/triplet subdivision.
- H would normally close on 2/4, but current kit has one closed hat voice.
- K is light and sparse unless style demands otherwise.
- S provides comping accents rather than a rock backbeat.
- Do not quantize the concept into EDM regularity.

---


# bebop

**Canonical ID:** `bebop`
**Aliases:** bebop
**Drum parent:** `jazz_swing`

- Fast R swing pulse.
- Sparse K.
- Syncopated S comping.
- Short fills.
- Maintain conversational independence.

---


# big_band_swing

**Canonical ID:** `big_band_swing`
**Aliases:** big band, swing
**Drum parent:** `jazz_swing`

- R keeps swing.
- H/S reinforce ensemble hits.
- K supports major accents.
- Toms and C can lead into section hits.
- Percussion follows arrangement punctuation.

---


# jazz_fusion

**Canonical ID:** `jazz_fusion`
**Aliases:** jazz fusion, fusion jazz
**Drum parent:** `jazz_swing`

- Jazz independence plus rock/funk weight.
- H/R can use dense 16ths.
- K syncopates.
- S may keep backbeat while adding ghost-like figures.
- Toms can participate melodically.

---


# blues

**Canonical ID:** `blues`
**Aliases:** blues

- Shuffle or straight feel depending request.
- S on 2/4.
- K simple and supportive.
- H/R carries shuffle.
- Fills answer vocal/instrument phrases.

---


# reggae

**Canonical ID:** `reggae`
**Aliases:** reggae

- Sparse K.
- Strong emphasis around beat 3 is common.
- H/R steady but relaxed.
- Avoid rock-like constant kick drive.
- Space is central.

---


# dub_reggae

**Canonical ID:** `dub_reggae`
**Aliases:** dub reggae, dub
**Drum parent:** `reggae`

- Reggae-derived skeleton.
- Even fewer events.
- Drums should leave room for effects and bass.
- C/R accents can be isolated and dramatic.

---


# ska

**Canonical ID:** `ska`
**Aliases:** ska
**Drum parent:** `reggae`

- Faster than reggae.
- Clear backbeat.
- H/R brisk.
- K supports upbeat guitar pattern.
- More energetic fills.

---


# classical

**Canonical ID:** `classical`
**Aliases:** classical, classical era, orchestral classical, chamber, symphonic, baroque

**Orchestral percussion base:** do not automatically use a drum kit. Treat percussion as structural color: low drum/timpani-like weight for harmonic pillars, snare/side-drum for martial or rhythmic drive, tom/timpani contours for approach, crash cymbal for earned structural peaks, and silence as a normal texture. Percussion follows orchestral phrases rather than looping by default.

**Principle:** do not force a drum-set groove into orchestral music.
**Roles:** punctuation, color, momentum, ceremonial weight, climax support, transition.
**Timpani concept:** reinforce important roots/dominants; rolls create approach; do not hit every chord.
**Cymbals:** structural peaks only.
**Snare:** march/tension only when stylistically appropriate.
**Avoid:** permanent groove and crash on every downbeat.

- Percussion is structural.
- Timpani-like K/L hits reinforce tonic/dominant and cadences.
- S for military/march character.
- C for exceptional climactic punctuation, not constant wash.
- Silence is normal.

---


# baroque

**Canonical ID:** `baroque`
**Aliases:** baroque
**Drum parent:** `classical`

- Percussion usually sparse in concert music.
- Timpani/bass-drum-like impacts mainly support ceremonial or martial material.
- Do not add continuous drum-kit grooves unless the user explicitly asks for fusion.

---


# romantic_classical

**Canonical ID:** `romantic_classical`
**Aliases:** romantic, romantic classical, romantic orchestra, late romantic, lush orchestral
**Drum parent:** `classical`

**Role:** long crescendos, timpani at harmonic turning points, cymbal/crash at earned peaks, rolls under dominant tension.
**Silence:** percussion can disappear for long intimate spans.
**Avoid:** modern trailer hit every few bars unless explicitly hybrid.

- Broader dynamic percussion.
- Timpani-like L/M/K can support harmonic drama.
- C at climaxes.
- S rolls can create military tension or crescendos.
- Percussion follows orchestral phrases rather than looping grooves.

---


# late_romantic

**Canonical ID:** `late_romantic`
**Aliases:** late romantic, late romantic orchestra
**Drum parent:** `romantic_classical`

- Larger percussion palette and stronger climaxes.
- Long crescendos may move:
  sparse low impact → roll → tom/timpani motion → C/K climax.
- Use percussion for destiny, catastrophe, triumph, revelation.

---


# neoclassical_modern_classical

**Canonical ID:** `neoclassical_modern_classical`
**Aliases:** neoclassical, neo classical, modern classical, contemporary classical, minimalist classical, piano cinematic
**Drum parent:** `classical`

**Role:** often minimal or absent; soft pulse may come from low drum, mallet, muted percussion, or repeated attack.
**Development:** introduce percussion late; one repeating pulse may gradually widen.
**Avoid:** unnecessary full kit and giant impacts overpowering intimate motifs.

- Clear form and rhythmic precision.
- Percussion usually selective and architectural.
- S may articulate ostinati/march material.
- Timpani-like low notes support harmonic pillars.
- Avoid Hollywood-style constant impacts unless the user asks for cinematic neoclassical.

---


# neo_romantic

**Canonical ID:** `neo_romantic`
**Aliases:** neo romantic, neo-romantic
**Drum parent:** `romantic_classical`

- Emotional, tonal, expansive.
- Percussion supports long emotional arcs.
- Deep K/L/M for arrival and gravity.
- S rolls only for genuine build/tension.
- C marks major emotional release.
- Timpani-like movement can connect harmonic changes.
- Leave long passages without percussion when melody/harmony carry the emotion.

---


# minimalist_classical

**Canonical ID:** `minimalist_classical`
**Aliases:** minimalist classical, minimal classical, minimalist orchestra
**Drum parent:** `neoclassical_modern_classical`

- Repetition is central.
- Percussion may repeat one quiet cell for many bars.
- Add one voice at a time.
- Tiny phase/accent changes matter more than fills.

---


# contemporary_chamber

**Canonical ID:** `contemporary_chamber`
**Aliases:** contemporary chamber, chamber music
**Drum parent:** `classical`

- Percussion can be textural rather than groove-based.
- Use isolated hits, silence, unusual spacing, and register contrast.
- Do not force pop backbeats.

---


# military_march

**Canonical ID:** `military_march`
**Aliases:** march, military, military march
**Drum parent:** `classical`

- S carries repeated rudimental subdivision.
- K marks major beats.
- C punctuates cadences.
- Rolls lead into structural hits.
- Keep pulse extremely clear.

---


# cinematic_orchestral

**Canonical ID:** `cinematic_orchestral`
**Aliases:** cinematic, orchestral, soundtrack, film score, game score, epic orchestral
**Drum parent:** `classical`

**Role:** structural punctuation modifier.
**Layers:** separate pulse, transition, impact, and detail roles.
**Tools:** low drums for large events, tom builds, cymbal swells, silence before payoff.
**Avoid:** every layer hitting every accent and 'epic' percussion at all times.

- K for deep impacts.
- S for rhythmic ostinato or military drive.
- T/M/L for escalating fills.
- C for major arrivals.
- Rolls can crescendo into trailer-style impacts.
- Avoid an impact on every phrase; contrast creates scale.

---


# epic_trailer

**Canonical ID:** `epic_trailer`
**Aliases:** epic, trailer, trailer music, epic trailer
**Drum parent:** `cinematic_orchestral`

- Deep K on structural beats.
- T/M/L rolls build momentum.
- S can create marching 8ths/16ths.
- C on climaxes.
- Increase density toward the payoff, then simplify on impact.

---


# cinematic_action

**Canonical ID:** `cinematic_action`
**Aliases:** action score, action cinematic, action soundtrack
**Drum parent:** `cinematic_orchestral`

- Faster K/S interplay.
- Toms can form ostinatos.
- S rolls and C punctuate cuts.
- Use changing density every 2–4 bars.

---


# cinematic_suspense

**Canonical ID:** `cinematic_suspense`
**Aliases:** suspense, suspense score, thriller score
**Drum parent:** `cinematic_orchestral`

- Sparse low K/L.
- Isolated H/R metallic ticks.
- S rolls only when tension rises.
- Long gaps matter.

---


# heroic_cinematic

**Canonical ID:** `heroic_cinematic`
**Aliases:** heroic score, heroic cinematic
**Drum parent:** `cinematic_orchestral`

- March-like S or broad rock/orchestral pulse.
- K on major structural beats.
- T/M/L fills into statements.
- C on triumphant arrival.
- Avoid frantic micro-detail unless the scene demands urgency.

---


# tender_cinematic

**Canonical ID:** `tender_cinematic`
**Aliases:** tender cinematic, emotional cinematic, tender score
**Drum parent:** `cinematic_orchestral`

- Often no drums initially.
- Sparse low impact at phrase boundaries.
- Soft cymbal-like arrival.
- Percussion should not steal emotional focus.

---


# latin_fusion

**Canonical ID:** `latin_fusion`
**Aliases:** salsa, afro cuban, afro-cuban, latin fusion

- Do not use four-on-floor by default.
- Use syncopated K/S/T patterns.
- Layer repeating cross-rhythmic cells.
- Avoid pretending a drum kit exactly reproduces clave/conga/timbale language.

---


# reggaeton

**Canonical ID:** `reggaeton`
**Aliases:** reggaeton, dembow
**Drum parent:** `latin_fusion`

- Repeating syncopated K/S framework.
- Backbeat placement differs from straight rock.
- Keep the core pattern highly repetitive.
- H adds subdivision without obscuring the dembow pulse.

---


# latin_house

**Canonical ID:** `latin_house`
**Aliases:** latin house
**Drum parent:** `house`

- House K four-on-floor.
- Add syncopated tom/percussion-like hits between beats.
- O/H keeps dance lift.

---


# Cross-genre drum modifiers

## more_energy
Choose 1-3: more kick activity, faster hats, ride/crash entry, stronger velocity, shorter gaps, more fills, double-time perception. Do not automatically increase every dimension.

## less_energy
Choose 1-3: remove kicks, close hats, reduce cymbal spectrum, half-time perception, lower velocity, longer rests, remove fills.

## more_human
Velocity variation, small timing variation, occasional ghosts, varied fill velocities; keep structural hits tight.

## more_mechanical
Tighter quantization, exact repeated cells, consistent velocity where appropriate, systematic rather than loose mutations.

## more_polyrhythmic
Preserve a stable reference pulse, create one repeated alternate grouping, repeat it enough to learn, then allow cycle realignment.

## more_cinematic
Fewer but larger events, tom builds, swells, low impacts, silence before payoff.

# Smart retrieval notes

1. Use the most specific matching guide.
2. Add a parent only when useful.
3. Read current song lanes before inventing the groove.
4. Identify the section role: intro, verse, build, chorus/drop, breakdown, bridge, climax, outro.
5. Identify the anchor: riff, bass, chord loop, melody, vocal space, or orchestral pulse.
6. Decide whether drums should lock, counter, simplify, or stay absent.
7. Establish one recognizable groove before varying it.
8. Make fills serve phrase boundaries.
9. Use drum density to reveal the song's energy arc.
10. Never treat genre as one fixed bar copied across the entire song.

The desired result is:

> **The drummer understands what this section is doing, and the groove develops the way this genre expects.**


# Source appendix — merged Drum & Percussion Grammar

The following is the complete source grammar merged into this guide for maintenance/reference. Runtime retrieval uses the canonical blocks above so prompts remain focused.

# Resone Drum & Percussion Grammar

A compact style guide for generating drums and percussion from genre language.

The goal is not to memorize famous patterns. Reduce a style to a few rhythmic decisions:

1. **Pulse** — straight, swung, triplet, halftime, double-time, broken.
2. **Kick policy** — where the weight lands.
3. **Snare policy** — backbeat, halftime anchor, syncopated answer, roll, or absent.
4. **Hat / ride subdivision** — quarters, 8ths, 16ths, offbeats, triplets, broken accents.
5. **Fill policy** — how often fills occur and whether they use snare, toms, or cymbals.
6. **Energy policy** — increase density, subdivisions, accents, cymbals, doubles, or rolls without destroying the groove.
7. **Human feel** — rigid/mechanical, lightly varied, swung, loose, behind/ahead of beat.

These are tendencies, not laws. Preserve a user's explicit rhythm over any genre default.

---

## 1. Resone kit shorthand

Use letters when reasoning. Emit the actual mapped drum notes when writing Resonator notation.

| Letter | Instrument | Resone note |
|---|---|---:|
| **K** | Kick | `C2` |
| **S** | Snare | `D2` |
| **H** | Closed hi-hat | `F#2` |
| **O** | Open hi-hat | `A#2` |
| **L** | Low tom | `F2` |
| **M** | Mid tom | `A2` |
| **T** | High tom | `D3` |
| **C** | Crash | `C#3` |
| **R** | Ride | `D#3` |

Examples:

- `K K K K` = four-on-the-floor kick.
- `S... S... S... S...` = 16th-note snare roll when the notation timing makes each hit a sixteenth.
- `T T M M L L` = descending tom fill.
- `H H H H H H H H` = steady eighth-note hat stream.
- `O` usually marks a phrase lift, offbeat accent, or transition.
- `C` normally marks a section arrival rather than repeating constantly.

---

# 2. Universal rhythmic atoms

## Four on the floor
Kick on every quarter-note beat.

**Feel:** propulsion, certainty, danceability, physical forward motion.

Typical:
- House
- trance
- techno
- disco
- hardstyle
- gabber
- much of EDM

Core:
`K--- K--- K--- K---`

Add offbeat hats:
`K + H(&)`

---

## Rock backbeat
Kick supports the riff; snare strongly marks beats **2 and 4**.

**Feel:** grounded, human, driving.

Core concept:
- Beat 1: K
- Beat 2: S
- Beat 3: K or kick variation
- Beat 4: S

Hats/ride usually carry 8ths.

---

## Half-time
Snare emphasizes **beat 3** instead of 2 and 4.

**Feel:** heavier, wider, slower, brutal, spacious.

Common in:
- trap
- dubstep
- metalcore
- hardcore breakdowns
- djent
- heavy rock

---

## Double-time
Keep the harmonic tempo but make the drums feel twice as active.

Methods:
- faster hats
- more frequent kick/snare answers
- snare backbeat perceived at twice the rate
- 16th subdivisions

Common in:
- punk
- thrash
- drum & bass
- metal
- energetic choruses

---

## Offbeat hat
Closed or open hat between quarter-note kicks.

For four-on-the-floor:
- K on beats
- H/O on the `&`

**Feel:** dance lift, breathing between kicks.

Essential to:
- house
- trance
- disco
- many techno styles

---

## Sixteenth-note snare roll
Repeat S at 16th-note spacing.

Use:
- last half-beat
- last beat
- last 2 beats
- occasionally a whole bar for a build

Intensity can rise by:
1. 8ths → 16ths
2. soft → loud
3. sparse → continuous
4. snare → snare + toms
5. finish with C + K

Do not make every fill a roll.

---

## Tom fill
Use several toms in a rhythmic contour.

Common shapes:
- `T M L`
- `T T M M L L`
- `M T M L`
- `L M T` for upward anticipation

Rock/metal fills often use toms to replace part of the normal beat for the last 1–2 beats of a phrase.

---

## Crash arrival
Use C on:
- first beat of a new section
- chorus/drop arrival
- after a fill
- major accent

Usually pair with K.

Do not crash every beat unless the style explicitly wants wash/noise.

---

## Ride lift
Replace hats with R when the section should feel:
- wider
- louder
- more open
- more forward
- more live

Typical in rock, metal, jazz-derived playing, and climactic sections.

---

# 3. Energy controls

When asked for **more energy**, prefer transforming the existing groove instead of replacing it.

Possible energy moves:

1. 8th hats → 16th hats.
2. Closed H → occasional O.
3. Add ghost/syncopated K around the existing anchors.
4. Add extra kick before a snare.
5. Add a short S roll before a section boundary.
6. Add tom fill in the final beat or two.
7. Hat → ride in the bigger section.
8. Add C on section arrival.
9. Move from halftime → normal time or normal → double-time.
10. Add kick doubles in rock/metal.
11. Shorten spaces while preserving the original accents.
12. Increase velocity/accent before increasing raw note count.

When asked for **less energy**, reverse those operations.

---

# 4. EDM family

## House — general
**Tempo:** commonly ~115–130 BPM.

Science:
- K every quarter.
- S commonly reinforces 2 and 4.
- H/O on offbeats.
- Additional H can fill 8ths or 16ths.
- Small fills every 4–8 bars.
- C on major section changes.

Skeleton:
**K: 1 2 3 4**
**S: 2 4**
**O/H: offbeats**

---

## Deep house
- Four-on-floor K.
- Softer S.
- Offbeat H/O.
- Sparse extra percussion.
- Leave breathing room.
- Avoid constant aggressive rolls.
- Micro-syncopation matters more than density.

**Feel:** relaxed, warm, hypnotic.

---

## Tech house
- Four-on-floor K stays dominant.
- Strong offbeat or 16th hats.
- More syncopated percussive gaps and answers.
- Short S/tom fills.
- Repetition with tiny mutations every 2–4 bars.

**Feel:** dry, physical, cheeky, looping.

---

## Progressive house
- Four-on-floor foundation.
- Gradually add hats/percussion over long phrases.
- Fills are restrained.
- Open hat and crash mark structural growth.
- Use density as arrangement automation.

**Feel:** patient expansion.

---

## Big-room / festival house
- Huge four-on-floor K.
- S/clap on 2 and 4.
- Builds: increasing S roll density.
- Drop: simplify back to giant K + strong backbeat.
- C on drop entrance.
- Avoid overcomplicated groove at the main drop.

**Feel:** obvious, massive, communal.

---

## Electro house
- Four-on-floor or aggressively syncopated K.
- Strong S 2/4.
- 16th hats or broken hats.
- Abrupt fills and rests can answer synth riffs.

**Feel:** hard-edged, mechanical, punchy.

---

## Trance — general
**Tempo:** commonly ~128–140 BPM.

Reduce trance drums to:
1. **K every quarter.**
2. **H/O between kicks.**
3. **S on 2 and 4** or a clap/snare layer implied by S.
4. **16th closed hats** when energy rises.
5. **Short snare rolls** before 4/8/16-bar boundaries.
6. **C + K** on important arrivals.
7. Groove should be regular enough that harmony/melody can provide emotional complexity.

Basic trance:
- K: 1,2,3,4
- S: 2,4
- O: offbeats
- H: optional 16th stream with accents

---

## Uplifting trance
- Firm four-on-floor K.
- Bright O on offbeats.
- 16th H in energetic passages.
- Long builds with S rolls that accelerate or crescendo.
- Big C on release/drop.
- Breakdown may remove K almost entirely.

**Feel:** ascent → suspension → release.

---

## Progressive trance
- Same fundamental pulse as trance.
- Fewer fills.
- More gradual density changes.
- Use H/O pattern changes rather than constant S rolls.
- Delay the full 16th hat texture.

**Feel:** restrained hypnotic growth.

---

## Psytrance
**Tempo:** often ~138–150+.

- Unbroken four-on-floor K.
- Very consistent pulse.
- H can run 16ths with selective accents.
- S is less dominant than in rock/house; use strategically.
- Tiny fills, glitches, and hat changes can occur at phrase edges.
- Do not disrupt the kick pulse casually.

**Feel:** relentless, precise, psychedelic.

---

## Goa trance
- Psytrance foundation.
- Slightly more flowing/cyclic percussion.
- Longer evolutionary phrases.
- Fewer giant modern EDM build clichés.

---

## Tech trance
- Four-on-floor.
- Harder K.
- Stronger S.
- Darker, more mechanical 16th H.
- Short techno-like fills.
- More abrupt drop transitions.

---

## Hard trance
- Strong four-on-floor K.
- Heavy S 2/4.
- Frequent open hats.
- More rolls and cymbal punctuation than progressive trance.
- Rhythms may feel almost hardstyle-adjacent.

---

## Techno — general
**Tempo:** commonly ~125–150 depending subtype.

Science:
- Repetition is a feature.
- K often anchors every quarter.
- H/O and percussion create evolution.
- S may be sparse, backbeat-like, or absent.
- Change one small rhythmic variable at a time.

---

## Minimal techno
- K stable.
- Very few voices at once.
- H patterns create motion.
- Tiny omissions/additions are meaningful.
- Fills should be extremely small.

---

## Dub techno
- Four-on-floor or restrained pulse.
- Sparse H/O.
- Percussion has space.
- Avoid busy fills.
- Let echoes/harmony carry movement.

---

## Peak-time / driving techno
- Heavy four-on-floor.
- 16th H or driving 8ths.
- S/clap often emphasizes 2/4 or phrase accents.
- Short rolls into transitions.
- Ride can enter for peak energy.

---

## Hard techno
- Faster/heavier four-on-floor.
- Dense H.
- More S rolls.
- Frequent industrial accents.
- Short gaps before K returns can increase impact.

---

## Acid techno
- Techno pulse.
- Keep drums fairly repetitive so acid-line movement remains readable.
- H and S fills can answer acid phrases.
- Crash/ride only at major changes.

---

## Hardcore / gabber
**Tempo:** often ~160–200+.

- K dominates every quarter, usually extremely hard.
- S can reinforce backbeats or fills.
- H at 8ths/16ths.
- Frequent S rolls and rapid fills.
- C on section hits.
- At extreme tempos, fewer simultaneous decorations keep the groove legible.

**Feel:** impact first.

---

## Happy hardcore
- Fast four-on-floor K.
- Strong 2/4 S.
- Bright offbeat O.
- 16th H.
- Energetic S rolls.
- Frequent C transitions.

**Feel:** euphoric + hyperactive.

---

## Hardstyle
- Four-on-floor K.
- Strong backbeat support.
- Offbeat H/O.
- Build sections can use S rolls.
- Main groove should leave space for the characteristic kick tail.
- Avoid filling every 16th with percussion.

---

## Drum & bass — general
**Tempo:** often ~160–180 BPM.

Core principle:
- **Broken kick/snare pattern**, not four-on-floor.
- S often strongly anchors beat 2 and/or 4 in a fast grid.
- H supplies 8th/16th momentum.
- Ghosted K and S create syncopation.
- Think in 2-bar phrases.

A common conceptual skeleton:
- K near beat 1
- S near beat 2
- another K between major anchors
- S near beat 4

Do not reduce DnB to constant kicks.

---

## Liquid DnB
- Broken K/S groove.
- Softer S.
- Smooth 8th/16th H.
- Fewer violent fills.
- Maintain forward motion without clutter.

---

## Neurofunk
- Precise broken K/S.
- More syncopation.
- Short H gaps and accents.
- Aggressive micro-fills.
- Rhythmic interaction with bass is critical.

---

## Jump-up DnB
- Very clear K/S anchors.
- Bouncy syncopation.
- Simpler than neurofunk.
- Short fills and obvious phrase punctuation.

---

## Jungle
- Breakbeat logic.
- Fast chopped S patterns.
- Ghost S and K.
- Syncopation and irregular accents.
- H/ride can imply sampled break texture.
- Rolls may cross beat boundaries.

**Feel:** restless, human, chopped.

---

## Breakbeat / breaks
- Broken K.
- Backbeat S.
- Syncopated extra K around 16ths/8ths.
- H follows groove rather than machine-straight four-on-floor.
- Fills can be snare or tom based.

---

## Big beat
- Breakbeat skeleton.
- Heavy K/S.
- Simple, oversized accents.
- Crash and tom fills are welcome.
- Often rock-like in weight.

---

## UK garage
- Broken K rather than four-on-floor.
- S/clap around 2/4.
- Shuffled H.
- Syncopation is essential.
- Leave holes.

---

## 2-step garage
- Intentionally omit some expected kicks.
- S remains a recognizable anchor.
- H swings/shuffles.
- The empty spaces create the groove.

---

## Dubstep
**Tempo:** commonly around 140 with halftime perception.

- Half-time S centered on beat 3.
- Sparse, powerful K.
- H often 8ths/16ths with syncopated gaps.
- Big empty spaces.
- Short S fills lead into drops.

---

## Brostep
- Dubstep halftime anchor.
- More aggressive K around bass accents.
- More fills, C, and H bursts.
- Drums can answer sound-design gestures.

---

## Future bass
- Half-time or broken beat.
- S on strong halftime anchor.
- Syncopated K.
- H can use quick bursts/rolls.
- Cymbal accents support chord swells.

---

## EDM trap
- Half-time S.
- Deep syncopated K.
- H uses 8ths, 16ths, 32nd-style bursts, and triplet rolls.
- Leave space between kick events.
- Hat density is a major energy control.

---

## Jersey club
- Fast, syncopated K pattern.
- Repeated kick figures are central.
- S/clap anchors dance pulse.
- Abrupt gaps and repetitions are part of the feel.

---

## Synthwave / outrun
- Electronic rock-like backbeat.
- K on 1/3 or more driving variations.
- S on 2/4.
- Steady 8th H.
- Tom fills evoke 1980s production.
- Large C transitions.

---

# 4A. Trance subgenre quick-reference hierarchy

This section is intentionally structured for later deterministic extraction.

Runtime idea:

`EDM -> Trance -> specific trance subgenre`

If the prompt only says **trance**, use the Trance Base block.
If it names a specific subtype, use **Trance Base + that subtype's overrides**.

Possible aliases are included so a deterministic matcher can map natural-language requests onto the correct block.

---

## [TRANCE_BASE]

**Aliases:** trance, trance drums, trance beat, trance percussion, classic trance

**Pulse:** four-on-the-floor.

**Core rules:**
- **K** every quarter note.
- **S** commonly on beats 2 and 4.
- **O** commonly on the offbeats.
- **H** adds 8th/16th subdivision as energy rises.
- Keep the K pulse stable through normal driving sections.
- Use short **S** rolls near phrase boundaries.
- Use **C + K** on important arrivals.
- Breakdowns can remove K almost entirely.
- Builds add subdivision and density.
- Drops simplify back toward the strongest defining pulse.

**Basic skeleton:**

`K--- K--- K--- K---`

with:

`S` on 2 and 4

and:

`O` on each offbeat.

**Energy ladder:**
1. K only.
2. K + offbeat O.
3. Add S on 2/4.
4. Add sparse H.
5. Add 16th H.
6. Add short S roll.
7. Brief gap or breakdown.
8. Return with C + K and full groove.

**Avoid:**
- breaking the quarter-note K casually;
- turning every bar into a fill;
- making the normal groove denser than the build;
- using constant crash cymbals.

---

## [CLASSIC_TRANCE]

**Aliases:** classic trance, old school trance, old-school trance, 90s trance, early trance, melodic trance

**Base:** TRANCE_BASE.

**Overrides:**
- Simpler and more hypnotic than modern festival trance.
- Fewer hyper-detailed hat patterns.
- Longer repeated groove cells.
- Less dependence on giant snare-build clichés.
- H/O evolution should happen gradually.
- Fills are usually small and functional.
- K remains extremely dependable.

**Feel:** hypnotic, driving, melodic, spacious.

**Example concept:**

Bars 1-2:
- K quarters
- O offbeats
- S 2/4

Bars 3-4:
- same groove
- add a few H subdivisions
- tiny S pickup into the next phrase

---

## [UPLIFTING_TRANCE]

**Aliases:** uplifting trance, euphoric trance, emotional trance, anthem trance, anthemic trance

**Base:** TRANCE_BASE.

**Overrides:**
- Bright, obvious O on offbeats.
- More 16th H during high-energy passages.
- Stronger and longer S-roll builds.
- C + K arrivals should feel large.
- Breakdown can become nearly percussionless.
- The return should feel like release after suspension.
- Preserve regularity so melody/harmony can carry emotional complexity.

**Feel:** ascent -> suspension -> release.

**Typical 8-bar energy shape:**
- bars 1-2: K + O
- bars 3-4: add S 2/4 and H
- bars 5-6: denser H
- bar 7: S roll starts
- bar 8: S roll intensifies, brief gap
- next bar: C + K full return

---

## [PROGRESSIVE_TRANCE]

**Aliases:** progressive trance, prog trance, deep trance, progressive melodic trance

**Base:** TRANCE_BASE.

**Overrides:**
- Fewer fills.
- Slower density changes.
- Delay full 16th hats.
- Let one small percussion change carry several bars.
- O/H pattern evolution is more important than constant S rolls.
- Maintain continuity over 8-16 bar spans.

**Feel:** restrained hypnotic growth.

**Typical development:**
- 4 bars K only or K + sparse H
- add O
- later add S
- later add fuller H
- save strong fill for actual section boundary

---

## [GOA_TRANCE]

**Aliases:** goa, goa trance, old school goa, psychedelic goa

**Base:** TRANCE_BASE.

**Overrides:**
- More cyclic percussion logic.
- Long evolving repeated cells.
- Less modern build/drop punctuation.
- Prefer gradual mutation to obvious festival transitions.
- Small H/S changes can rotate through the phrase.
- Keep K stable while upper percussion evolves.

**Feel:** continuous, psychedelic, ritualistic, spiraling.

**Example mutation cycle:**
- phrase A: K + O
- phrase B: add H on selected 16ths
- phrase C: alter H accents, not the K
- phrase D: small S/tom transition
- return to A with one new accent

---

## [PSYTRANCE]

**Aliases:** psytrance, psy trance, psychedelic trance, full-on psy, full on psy

**Base:** TRANCE_BASE.

**Overrides:**
- K is relentless and precise.
- H can run tight 16ths.
- S is usually less dominant than in house/trance-pop.
- Use micro-fills rather than giant backbeat fills.
- Do not interrupt K without a strong structural reason.
- Tiny H omissions and accents can create motion.
- Percussion should interlock tightly with bass.

**Feel:** relentless, precise, psychedelic.

**Typical bar:**
- K every quarter
- H 16ths with selected accents
- sparse S/percussion accents
- tiny end-of-phrase fill every 4-8 bars

---

## [FULL_ON_PSYTRANCE]

**Aliases:** full-on psytrance, full on psytrance, full-on psy, morning psy

**Base:** PSYTRANCE.

**Overrides:**
- Brighter and more energetic upper percussion.
- More frequent 16th H.
- Slightly more obvious phrase punctuation.
- Keep K/bass interaction extremely stable.
- Builds can increase H/S density, but avoid breaking the pulse.

---

## [DARK_PSYTRANCE]

**Aliases:** dark psy, darkpsy, dark psytrance, forest psy, forest trance

**Base:** PSYTRANCE.

**Overrides:**
- Darker sparse accents.
- More abrupt H gaps.
- Less bright O.
- Short disorienting fills.
- Keep the underlying K extremely stable so strange upper rhythm remains intelligible.

---

## [TECH_TRANCE]

**Aliases:** tech trance, techno trance, tech-trance

**Base:** TRANCE_BASE.

**Overrides:**
- Harder K.
- Stronger S.
- Darker H.
- Short mechanical fills.
- More abrupt section transitions.
- Repetition should feel forceful rather than dreamy.
- Can borrow techno-like ride patterns at peaks.

**Feel:** mechanical propulsion, pressure, force.

---

## [HARD_TRANCE]

**Aliases:** hard trance, hardtrance, hard dance trance

**Base:** TRANCE_BASE.

**Overrides:**
- Heavier K and S.
- More aggressive O.
- More frequent rolls.
- More cymbal punctuation.
- Greater rhythmic density than progressive/classic trance.
- May approach hardstyle energy without adopting hardstyle kick behavior.

**Feel:** euphoric aggression.

---

## [VOCAL_TRANCE]

**Aliases:** vocal trance, vocal uplifting trance, vocal progressive trance

**Base:** usually UPLIFTING_TRANCE or PROGRESSIVE_TRANCE.

**Overrides:**
- Verse drums should leave more space.
- Reduce H density under vocals.
- Save large S rolls for transitions.
- Choruses/drops can widen with O, H, and C.
- Do not let percussion compete with the vocal phrase.

---

## [EURO_TRANCE]

**Aliases:** euro trance, commercial trance, radio trance, eurodance trance

**Base:** TRANCE_BASE.

**Overrides:**
- Extremely clear four-on-floor.
- Obvious S 2/4.
- Bright O.
- Regular 8th/16th H.
- Predictable short fills.
- Strong C at section arrivals.
- Less subtle than progressive trance.

**Feel:** immediate, catchy, dance-forward.

---

## [HARDCORE_TRANCE_ADJACENT]

This is not a single formal genre block. Use when the user combines trance with:
- hardcore
- gabber
- hard dance
- rave

Start from HARD_TRANCE and move toward the appropriate harder family.

Do not automatically assume "hardcore" means gabber if the surrounding words indicate punk/hardcore rock.

---

# 4B. Trance groove archetypes

These are not fixed loops. They are reusable rhythmic roles that a generator can choose and mutate.

---

## [TRANCE_ARCHETYPE_CLASSIC_DRIVE]

**Purpose:** default classic trance groove.

- K: quarters.
- S: 2 and 4.
- O: every offbeat.
- H: optional sparse 16ths.
- Fill: 1-beat S or tom fill every 4-8 bars.
- Arrival: C + K.

**Use for:** classic trance, melodic trance, general trance.

---

## [TRANCE_ARCHETYPE_UPLIFTING_LIFT]

**Purpose:** bright high-energy uplift.

- K: quarters.
- S: 2 and 4.
- O: offbeats.
- H: denser 16ths.
- Build: S 8ths -> 16ths.
- Last beat may briefly thin before arrival.
- Arrival: C + K + full H/O.

**Use for:** uplifting, euphoric, anthem trance.

---

## [TRANCE_ARCHETYPE_PROGRESSIVE_PULSE]

**Purpose:** long hypnotic evolution.

- K: quarters.
- O: sparse at first.
- S: delayed or soft.
- H: added gradually.
- Fills: minimal.
- Change only one upper-percussion idea every 2-4 bars.

**Use for:** progressive trance, deep trance.

---

## [TRANCE_ARCHETYPE_GOA_CYCLE]

**Purpose:** cyclic psychedelic motion.

- K: quarters.
- O/H: repeating upper-percussion cell.
- Every 2-4 bars shift one H accent.
- S/tom transition only near longer boundary.
- Avoid giant modern build.

**Use for:** Goa, old-school psychedelic trance.

---

## [TRANCE_ARCHETYPE_PSY_DRIVE]

**Purpose:** relentless precise propulsion.

- K: every quarter without interruption.
- H: tight 16ths.
- S: sparse.
- Micro-fill: tiny H/S variation at phrase boundary.
- No oversized rock-like fill.

**Use for:** psytrance, full-on psy.

---

## [TRANCE_ARCHETYPE_TECH_PUSH]

**Purpose:** forceful mechanical trance.

- K: quarters.
- S: hard 2/4.
- H: tight dark 16ths or 8ths.
- O: selective.
- Fill: short mechanical S burst.
- Peak: R may replace H.

**Use for:** tech trance.

---

## [TRANCE_ARCHETYPE_HARD_ASSAULT]

**Purpose:** aggressive rave drive.

- K: quarters.
- S: strong 2/4.
- H/O: dense.
- Rolls: more frequent.
- C: stronger phrase punctuation.
- Preserve trance pulse even at high density.

**Use for:** hard trance.

---

## [TRANCE_ARCHETYPE_BREAKDOWN_BUILD]

**Purpose:** move from breakdown into full groove.

Stage 1:
- no K or sparse K
- sparse H/O

Stage 2:
- S enters in quarters or 8ths

Stage 3:
- S becomes 16ths
- H density increases

Stage 4:
- very short gap if desired

Arrival:
- C + K
- full trance groove

---

## [TRANCE_ARCHETYPE_DROP_RETURN]

**Purpose:** make the main pulse feel huge after a build.

First bar after build:
- C + K on beat 1
- K continues every quarter
- O returns on offbeats
- S returns on 2/4
- H resumes at the intended peak density

Do not continue the build roll through the drop unless specifically requested.

---

## [TRANCE_ARCHETYPE_VOCAL_VERSE]

**Purpose:** leave space under vocals.

- K: quarters or slightly reduced.
- S: soft 2/4.
- O: selective.
- H: sparse 8ths.
- no long roll until phrase transition.
- C only at significant section change.

---

# 4C. Deterministic genre matching notes

Later, this document can be split by bracketed section IDs.

Suggested runtime resolution:

1. Normalize user text.
2. Find the longest exact alias first.
3. Prefer specific subgenre over parent genre.
4. Allow multiple compatible matches for hybrids.
5. Fall back to the parent family when confidence is low.
6. Inject only:
   - universal drum rules,
   - parent genre block,
   - matched subgenre override,
   - optionally one matching archetype.

Examples:

`"make uplifting trance drums"`
-> TRANCE_BASE + UPLIFTING_TRANCE + TRANCE_ARCHETYPE_UPLIFTING_LIFT

`"psy trance drums with more energy"`
-> TRANCE_BASE + PSYTRANCE + TRANCE_ARCHETYPE_PSY_DRIVE + Energy Controls

`"classic 90s trance"`
-> TRANCE_BASE + CLASSIC_TRANCE + TRANCE_ARCHETYPE_CLASSIC_DRIVE

`"progressive trance build"`
-> TRANCE_BASE + PROGRESSIVE_TRANCE + TRANCE_ARCHETYPE_PROGRESSIVE_PULSE + TRANCE_ARCHETYPE_BREAKDOWN_BUILD

`"hard trance drop"`
-> TRANCE_BASE + HARD_TRANCE + TRANCE_ARCHETYPE_HARD_ASSAULT + TRANCE_ARCHETYPE_DROP_RETURN

For ambiguous language:
- `"hardcore trance"` -> prefer hard-trance / rave interpretation.
- `"hardcore drums"` alone -> ambiguous; surrounding terms decide punk-hardcore vs gabber-hardcore.

---

# 5. Rap / hip-hop family

## Boom bap
**Tempo:** commonly ~75–100 BPM.

- K and S should converse.
- S strongly anchors 2 and 4.
- K syncopates before/after those anchors.
- H usually 8ths or lightly swung 16ths.
- Small ghost S can add human feel.
- Do not overfill.

**Feel:** head-nod pocket.

---

## Modern hip-hop
- S/clap remains a clear anchor.
- K can be sparse and syncopated.
- H ranges from simple 8ths to detailed rolls.
- Space is valuable.

---

## Trap
**Tempo:** often perceived around 60–80 halftime or 120–160 double grid.

Science:
- S on the halftime anchor, commonly beat 3.
- K is sparse, syncopated, bass-aware.
- H provides most fine rhythmic detail.
- Hat vocabulary: 8ths → 16ths → short rapid bursts → triplets.
- Rolls should highlight words/transitions, not run constantly.

---

## Drill
- Half-time S anchor.
- K is highly syncopated and often avoids obvious downbeats.
- H can be comparatively restrained.
- Sliding bass rhythm and kick should interlock.
- Use sudden empty spaces.

---

## Grime
- Sparse hard K/S.
- Strong halftime-ish framework.
- Angular syncopation.
- Less decorative hat density than trap in many patterns.

---

## Lo-fi hip-hop
- Boom-bap logic.
- Softer K/S.
- Laid-back H.
- Slight swing.
- Fewer fills.
- Repetition is comforting.

---

## Phonk
- Hip-hop/trap skeleton.
- Strong K.
- S/clap anchor.
- Fast hats possible.
- Cowbell is stylistic but not in the current core Resone kit; do not fake it with another mapped drum unless requested.

---

# 6. Rock family

## Rock — general
- S on 2 and 4.
- H/R usually 8ths.
- K follows the riff and supports 1/3 plus syncopations.
- Every 4–8 bars, replace the last beat or two with S/tom fill.
- C + K marks section entrance.
- R often replaces H in choruses.

---

## Classic rock
- Moderate K complexity.
- Strong human backbeat.
- 8th H/R.
- Tom fills are common.
- Let fills breathe; avoid machine-gun density.

---

## Hard rock
- Heavier K.
- Strong S.
- 8th H or R.
- Kick doubles before/after S increase drive.
- Fills often use repeated S followed by descending toms.
- C on choruses/riff arrivals.

---

## Arena rock
- Very clear backbeat.
- Big C.
- Simpler groove, larger accents.
- Tom fills can be broad and dramatic.
- Avoid tiny overcomplicated details.

---

## Alternative / indie rock
- Backbeat can be conventional or deliberately displaced.
- H patterns may be sparse.
- Use unusual kick omissions.
- Fills are often understated.

---

## Grunge
- Heavy rock backbeat.
- Loose, human feel.
- Strong C/R in loud sections.
- Big tom fills.
- Avoid overly polished electronic hat rolls.

---

## Pop rock
- Clear K/S anchors.
- Consistent 8th H.
- Small fills before chorus.
- Strong C on chorus.
- Keep patterns readable.

---

## Punk
**Tempo:** usually fast.

- S 2/4.
- H/R fast 8ths.
- K drives hard.
- Short fills.
- Double-time feel common.
- Simplicity + speed beats complexity.

---

## Pop punk
- Punk drive with cleaner phrase structure.
- Fast H/R.
- Clear S 2/4.
- Frequent short fills into sections.
- Crash-heavy choruses.

---

## Post-punk
- Repetitive groove.
- Tighter K/S.
- H/R can carry angular patterns.
- Fewer classic rock fills.
- Mechanical consistency can be desirable.

---

## Progressive rock
- Preserve the meter and the riff's accent pattern.
- Let the groove change inside a phrase instead of repeating one bar forever.
- Toms can act like melodic voices, not just fills.
- Backbeats may move away from obvious 2/4 placement.
- Use fills to connect metric or riff changes.
- Odd meters should still have a simple internal anchor.
- Do not make every bar complicated; complexity works because stable ideas return.

### Progressive-rock core rule

Reduce progressive-rock drumming to:

1. **Find the riff accents.**
2. **Choose one repeating anchor** — K, S, H/R, or a tom pulse.
3. **Let one layer stay stable while another layer moves across it.**
4. **Use toms to answer or extend the riff.**
5. **Displace a backbeat occasionally instead of randomly.**
6. **Use one surprising fill near a phrase boundary, then return to the anchor.**

The groove should sound intentional even when the meter is unusual.

---

## [PROG_ROCK_ODD_METER_HEAVY]

**Useful for:** dark, heavy, hypnotic progressive rock with odd meters, repeating riffs, and controlled complexity.

**Core idea:** make a strange meter feel inevitable.

**Rules:**
- Keep one pulse constant with **H** or **R**.
- Let **K** follow the main riff accents.
- Use **S** as a structural marker rather than automatically on 2 and 4.
- Group odd meters into small cells:
  - 5/4 = `3+2` or `2+3`
  - 7/8 = `2+2+3`, `3+2+2`, or `2+3+2`
  - 9/8 = `2+2+2+3` or `3+3+3`
- Repeat the grouping long enough for the listener to learn it.
- Then move one accent, omit one K, or answer with toms.
- Use **L/M/T** as part of the groove, not only at the end.
- Heavy sections can use a halftime-feeling S anchor inside the odd meter.
- Big transitions: short tom run -> **C + K** -> return to the core grouping.

**Simple example — 7/8 grouped 2+2+3:**
- H/R keeps all 7 eighth-note pulses.
- K accents pulse 1, 3, and 5.
- S marks pulse 5 or 7 depending on the riff.
- T/M/L can answer the last 3-note group.

**Energy increase:**
- keep the grouping;
- add kick doubles around riff accents;
- move H -> R;
- add tom answers;
- increase fill density only at phrase boundaries.

**Avoid:**
- random odd-meter accents;
- changing the grouping every bar;
- constant fills;
- making every limb equally busy.

---

## [PROG_ROCK_POLYRHYTHMIC_HEAVY]

**Useful for:** progressive metal / heavy prog where the riff seems to move against a stable pulse.

**Core idea:** one layer says **"the meter is stable"**, another says **"the riff is moving."**

**Rules:**
- Keep H/R on a steady subdivision.
- Let K mirror the guitar/bass riff.
- Let S provide a slower repeating anchor.
- A repeated K pattern can cross the barline while H/R keeps the listener oriented.
- Do not change all layers together.
- When the riff resolves, use C or a short tom fill to reveal the downbeat again.

**Very simple formula:**

`steady H/R + riff-following K + slower S anchor`

That is enough to create a polymetric feeling without complicated notation.

---

## [PROG_ROCK_TOM_ARCHITECTURE]

**Useful for:** tribal, ritualistic, cinematic, or heavy progressive passages.

**Core idea:** toms become part of the main sentence.

**Rules:**
- Start with one repeating tom cell.
- Move the cell across **T -> M -> L** or back upward.
- K reinforces only selected tom accents.
- S may disappear entirely for several bars.
- R or H can quietly preserve time underneath.
- Build intensity by expanding the tom cell, not simply playing faster.

**Example development:**
- bar 1: `T M L`
- bar 2: repeat
- bar 3: `T T M L`
- bar 4: `T M M L L`
- transition: `T T M M L L -> C + K`

---

## [PROG_ROCK_FRENZIED_LATIN_JAZZ]

**Useful for:** explosive, highly mobile progressive rock with punk, Latin, jazz, and fusion energy.

**Core idea:** the kit feels constantly alive, but important accents still line up with the song.

**Rules:**
- K and S should converse rather than form a fixed rock loop.
- H/R can alternate between straight, swung, or broken subdivisions.
- Use rapid tom runs frequently, but attach them to phrase boundaries or riff answers.
- Short S bursts can interrupt the groove.
- Change cymbal surface often:
  - H for tightness
  - R for openness
  - C for explosive arrival
- Let one bar become very dense, then suddenly leave space.
- Use syncopated K around the riff instead of constant four-on-floor.
- Toms may begin before the barline and finish after it.
- Fast fills can use:
  - `S S T M`
  - `T M T L`
  - `T T M M L L`
  - mixed S + tom bursts
- Preserve at least one repeating accent or pulse so the result does not become random.

**Feel:** volatile, acrobatic, urgent, theatrical.

**Simple energy cycle:**
1. tight groove
2. syncopated K/S answer
3. dense tom/S burst
4. sudden space
5. C + K re-entry

---

## [PROG_ROCK_FUSION_FLOW]

**Useful for:** technical progressive rock that still needs groove and musical conversation.

**Core idea:** complexity should sound like phrasing, not math homework.

**Rules:**
- H/R carries a readable pulse.
- K responds to bass/riff movement.
- S may ghost, answer, or move off the backbeat.
- Toms connect phrases.
- Use occasional triplet or 16th bursts against a simpler main groove.
- Return to a recognizable anchor every 1-2 bars.

**Good default:**
- simple groove for 1 bar;
- variation in bar 2;
- more adventurous fill in bar 4;
- return to original groove in bar 5.

---

## Progressive-rock fill grammar

### Controlled heavy fill
`S S T M L`

Use before a major riff return.

### Long tom descent
`T T M M L L`

Use over the final 1-2 beats of a phrase.

### Frenzied fusion fill
`S T S M T L`

Use briefly; do not repeat every bar.

### Odd-meter fill rule
Keep the meter grouping audible during the fill.

Example for 7/8 grouped `2+2+3`:
- first 2 = S/S
- next 2 = T/M
- final 3 = T/M/L

The fill can be wild while the grouping stays understandable.

---

## Progressive-rock update rules

When asked for **more complex**:
- keep the main anchor;
- add one displaced accent;
- add tom interaction;
- add one cross-barline K figure;
- increase fill complexity only at phrase endings.

When asked for **heavier**:
- simplify upper percussion;
- strengthen K around riff accents;
- use a wider S anchor;
- add L/M tom weight;
- use C only on major hits.

When asked for **more chaotic**:
- increase K/S conversation;
- add rapid tom/S bursts;
- alternate H/R surfaces;
- create sudden gaps;
- preserve one stable pulse underneath.

When asked for **more hypnotic**:
- reduce fills;
- repeat the odd-meter grouping longer;
- hold H/R steady;
- let K pattern cycle against the barline;
- make changes slowly.

---

## Minimal progressive-rock decision procedure

Before generating:

1. **Meter/grouping:** normal or odd? If odd, choose one grouping.
2. **Anchor:** which voice keeps time — H, R, S, or toms?
3. **Riff layer:** which accents should K follow?
4. **Backbeat:** normal, displaced, halftime, or absent?
5. **Tom role:** fill only, phrase answer, or main groove?
6. **Complexity:** stable, polymetric, or frenetic?
7. **Return point:** where does the groove clearly come home?

If those seven decisions are clear, the drummer can sound progressive without needing hundreds of rules.

---

# 7. Metal & hardcore family

## Heavy metal — general
- Strong S backbeat when not in blast/double-time sections.
- K may use doubles.
- R/C open the sound.
- Toms bridge riffs.
- Match K accents to guitar/bass riff when context exists.

---

## Traditional heavy metal
- Rock-derived groove.
- More R.
- Strong tom fills.
- Kick doubles for emphasis, not constant machine-gun patterns.

---

## Thrash metal
- Fast tempo.
- Double-time S feel.
- Rapid H/R.
- More continuous K or double-kick patterns.
- Short aggressive fills.

---

## Death metal
- Dense K, often rapid/double-kick.
- S may use blast-beat logic.
- R/C can sustain aggression.
- Toms used in violent transitions.
- Density should still reflect riff accents.

---

## Black metal
- Blast-beat textures common:
  - rapid alternating/overlapping K and S
  - constant H/R/C pulse
- Cymbal wash can be intentional.
- Transitions may abruptly drop to half-time or tom-heavy patterns.

---

## Doom metal
- Slow.
- Huge spaces.
- Heavy K and S.
- Sparse H/R.
- Toms and C should feel monumental.
- Do not add fast detail merely because it is metal.

---

## Stoner metal / sludge
- Slow-to-mid groove.
- Heavy backbeat.
- Loose K.
- Big C/R.
- Thick tom fills.
- Human drag is more important than precision.

---

## Power metal
- Fast, driving double-kick feel.
- S clear and heroic.
- R/C during climaxes.
- Tom rolls into major transitions.
- Rhythmic regularity supports melodic grandeur.

---

## Metalcore
- Alternate between:
  - driving metal groove
  - double-time
  - halftime breakdown
- Breakdown: S on beat 3, sparse huge K aligned with riff.
- Builds can use S rolls/tom runs.
- C on breakdown/chorus arrival.

---

## Hardcore punk
- Fast, simple K/S.
- Strong 2/4 or double-time backbeat.
- H/R fast.
- Short fills.
- Breakdowns may switch abruptly to halftime.

---

## Beatdown / hardcore breakdown
- Half-time S.
- Sparse K tightly matching riff accents.
- Large rests matter.
- C can punctuate hits.
- Avoid decorative hats that weaken the impact.

---

## Post-hardcore
- Rock/hardcore foundation.
- More dynamic transitions.
- Tom-heavy sections are useful.
- Move between sparse verses and crash-heavy peaks.

---

## Djent
- S often supplies a stable backbeat/halftime anchor.
- K mirrors irregular riff accents.
- H/R can remain steady against polymetric guitar accents.
- Space and precision are critical.
- Do not randomize kick rhythm independently of the riff.

---

## Nu metal
- Heavy halftime or syncopated rock groove.
- K locks to riff.
- S is broad and obvious.
- H relatively simple.
- Hip-hop influence can justify sparse kick patterns.

---

# 8. Funk, soul, disco, R&B

## Funk
- K highly syncopated.
- S backbeat plus ghost-style answers.
- H often 16ths with accents/openings.
- Groove depends on interplay, not raw density.
- Small omissions create pocket.

---

## Soul
- Strong backbeat.
- Moderate K.
- H 8ths.
- Tasteful fills.
- Human feel and restraint.

---

## Motown-style
- Clear pulse.
- Strong S.
- Tambourine would often reinforce beats, but current core kit lacks it.
- K is busier than simple pop but still song-serving.

---

## Disco
- Four-on-floor K.
- S 2/4.
- O on offbeats.
- H often 16ths.
- Very regular dance pulse.
- C at structural changes.

---

## Contemporary R&B
- Sparse K.
- Strong but often soft S/clap anchor.
- H may use subtle 16th details.
- Silence and anticipation matter.
- Avoid dense rock fills.

---

## Neo-soul
- Behind-the-beat feel.
- Syncopated K.
- S backbeat.
- H can swing or use broken 16ths.
- Keep it human and slightly asymmetrical.

---

# 9. Jazz and blues

## Jazz swing
- R is the primary timekeeper.
- Implied swing/triplet subdivision.
- H would normally close on 2/4, but current kit has one closed hat voice.
- K is light and sparse unless style demands otherwise.
- S provides comping accents rather than a rock backbeat.
- Do not quantize the concept into EDM regularity.

---

## Bebop
- Fast R swing pulse.
- Sparse K.
- Syncopated S comping.
- Short fills.
- Maintain conversational independence.

---

## Big band / swing
- R keeps swing.
- H/S reinforce ensemble hits.
- K supports major accents.
- Toms and C can lead into section hits.
- Percussion follows arrangement punctuation.

---

## Jazz fusion
- Jazz independence plus rock/funk weight.
- H/R can use dense 16ths.
- K syncopates.
- S may keep backbeat while adding ghost-like figures.
- Toms can participate melodically.

---

## Blues
- Shuffle or straight feel depending request.
- S on 2/4.
- K simple and supportive.
- H/R carries shuffle.
- Fills answer vocal/instrument phrases.

---

# 10. Reggae / ska

## Reggae
- Sparse K.
- Strong emphasis around beat 3 is common.
- H/R steady but relaxed.
- Avoid rock-like constant kick drive.
- Space is central.

---

## Dub
- Reggae-derived skeleton.
- Even fewer events.
- Drums should leave room for effects and bass.
- C/R accents can be isolated and dramatic.

---

## Ska
- Faster than reggae.
- Clear backbeat.
- H/R brisk.
- K supports upbeat guitar pattern.
- More energetic fills.

---

# 11. Orchestral / classical percussion

For orchestral styles, **do not automatically use a drum kit**. Think of percussion as structural color.

The current Resone kit can approximate rhythmic roles, but if orchestral percussion instruments are available separately, prefer the correct orchestral voice.

Kit-role approximations:
- **K** ≈ bass drum / deep orchestral impact
- **S** ≈ orchestral snare / side drum
- **L/M/T** ≈ timpani/tom-like pitch contour only as a rough substitute
- **C** ≈ crash cymbal
- **R/H/O** ≈ metallic/continuous time color only when stylistically appropriate

---

## Baroque
- Percussion usually sparse in concert music.
- Timpani/bass-drum-like impacts mainly support ceremonial or martial material.
- Do not add continuous drum-kit grooves unless the user explicitly asks for fusion.

---

## Classical era
- Percussion is structural.
- Timpani-like K/L hits reinforce tonic/dominant and cadences.
- S for military/march character.
- C for exceptional climactic punctuation, not constant wash.
- Silence is normal.

---

## Romantic
- Broader dynamic percussion.
- Timpani-like L/M/K can support harmonic drama.
- C at climaxes.
- S rolls can create military tension or crescendos.
- Percussion follows orchestral phrases rather than looping grooves.

---

## Late Romantic
- Larger percussion palette and stronger climaxes.
- Long crescendos may move:
  sparse low impact → roll → tom/timpani motion → C/K climax.
- Use percussion for destiny, catastrophe, triumph, revelation.

---

## Neoclassical
- Clear form and rhythmic precision.
- Percussion usually selective and architectural.
- S may articulate ostinati/march material.
- Timpani-like low notes support harmonic pillars.
- Avoid Hollywood-style constant impacts unless the user asks for cinematic neoclassical.

---

## Neo-Romantic
- Emotional, tonal, expansive.
- Percussion supports long emotional arcs.
- Deep K/L/M for arrival and gravity.
- S rolls only for genuine build/tension.
- C marks major emotional release.
- Timpani-like movement can connect harmonic changes.
- Leave long passages without percussion when melody/harmony carry the emotion.

---

## Modern orchestral / cinematic
- K for deep impacts.
- S for rhythmic ostinato or military drive.
- T/M/L for escalating fills.
- C for major arrivals.
- Rolls can crescendo into trailer-style impacts.
- Avoid an impact on every phrase; contrast creates scale.

---

## Minimalist classical
- Repetition is central.
- Percussion may repeat one quiet cell for many bars.
- Add one voice at a time.
- Tiny phase/accent changes matter more than fills.

---

## Contemporary chamber
- Percussion can be textural rather than groove-based.
- Use isolated hits, silence, unusual spacing, and register contrast.
- Do not force pop backbeats.

---

## March / military
- S carries repeated rudimental subdivision.
- K marks major beats.
- C punctuates cadences.
- Rolls lead into structural hits.
- Keep pulse extremely clear.

---

# 12. Cinematic hybrid styles

## Epic / trailer
- Deep K on structural beats.
- T/M/L rolls build momentum.
- S can create marching 8ths/16ths.
- C on climaxes.
- Increase density toward the payoff, then simplify on impact.

---

## Action
- Faster K/S interplay.
- Toms can form ostinatos.
- S rolls and C punctuate cuts.
- Use changing density every 2–4 bars.

---

## Suspense
- Sparse low K/L.
- Isolated H/R metallic ticks.
- S rolls only when tension rises.
- Long gaps matter.

---

## Heroic
- March-like S or broad rock/orchestral pulse.
- K on major structural beats.
- T/M/L fills into statements.
- C on triumphant arrival.
- Avoid frantic micro-detail unless the scene demands urgency.

---

## Tender / emotional cinematic
- Often no drums initially.
- Sparse low impact at phrase boundaries.
- Soft cymbal-like arrival.
- Percussion should not steal emotional focus.

---

# 13. Latin / dance-derived shorthand

This section is intentionally generic because authentic Latin traditions use percussion voices beyond the current core kit.

## Salsa / Afro-Cuban-inspired fusion
- Do not use four-on-floor by default.
- Use syncopated K/S/T patterns.
- Layer repeating cross-rhythmic cells.
- Avoid pretending a drum kit exactly reproduces clave/conga/timbale language.

---

## Reggaeton / dembow
- Repeating syncopated K/S framework.
- Backbeat placement differs from straight rock.
- Keep the core pattern highly repetitive.
- H adds subdivision without obscuring the dembow pulse.

---

## Latin house
- House K four-on-floor.
- Add syncopated tom/percussion-like hits between beats.
- O/H keeps dance lift.

---

# 14. Fill grammar

Fills should normally happen near **phrase boundaries**, not randomly.

## One-beat rock fill
Replace beat 4:
`S S T M` or `T M L L`

## Two-beat rock/metal fill
Replace beats 3–4:
`S S T T M M L L`

## Snare build
Start sparse and tighten:
- quarters
- 8ths
- 16ths

Then:
`C + K` on the next section.

## Trance build
Keep K initially, then optionally remove it near the end of the build.
Increase S subdivision and H density.
Final short silence can make the returning K feel larger.

## Trap hat flourish
Keep main groove unchanged.
Insert a brief H burst at the end of a beat or phrase.
Do not turn every bar into a continuous roll.

## DnB fill
Use quick S/K rearrangement or a short snare run.
Preserve the perceived breakbeat anchor after the fill.

---

# 15. Phrase-length defaults

When the user gives no structural instruction:

- **1 bar:** establish the groove.
- **2 bars:** allow one small variation.
- **4 bars:** phrase; minor fill at the end.
- **8 bars:** larger phrase; clearer fill/transition at the end.
- **16 bars:** introduce at least 2–4 controlled variations; one meaningful larger transition.
- Do not change the fundamental genre groove every bar.

---

# 16. Dynamics and arrangement rules

## Verse / low energy
- Fewer K.
- Closed H.
- Fewer C.
- Short fills.
- Lower density.

## Pre-chorus / build
- Increase H subdivision.
- Add S repetitions/roll.
- Add tom movement.
- Increase velocity.
- Consider removing K immediately before arrival.

## Chorus / drop
- Restore strongest K anchors.
- Add C at entrance.
- Open H or R.
- Keep the main groove simpler than the build if impact depends on clarity.

## Breakdown
- Half-time or sparse K.
- Remove H layers.
- Large rests.
- Toms or isolated S can become focal.

---

# 17. Genre-selection rule

When a request contains several style words, combine **rhythmic functions**, not whole incompatible grooves.

Examples:

### "Hard rock + trance"
- Trance four-on-floor K.
- Rock S 2/4.
- Trance offbeat O.
- Rock tom fills.
- C on large section arrivals.

### "Trap + metal"
- Trap halftime S.
- Metal/riff-aligned K doubles.
- Trap H bursts.
- Metal C/tom fills at phrase boundaries.

### "Orchestral + techno"
- Techno four-on-floor foundation.
- Orchestral-style K/T/C impacts only at structural moments.
- Avoid turning orchestral percussion into a constant second drum kit.

### "Rap + hardcore"
Decide which meaning of hardcore is intended:
- hardcore punk → fast live backbeat
- hardcore/gabber → fast four-on-floor
If unclear, use surrounding genre words to infer the family.

---

# 18. Minimal decision procedure for the AI

Before generating a drum lane, decide:

**A. What is the pulse?**
- four-on-floor
- backbeat
- halftime
- broken
- swing/shuffle
- orchestral/no-loop

**B. What anchors the groove?**
- K
- S
- R/H
- riff/context
- phrase impacts

**C. What subdivision carries motion?**
- quarters
- 8ths
- 16ths
- triplets
- broken/syncopated

**D. How does it fill?**
- S roll
- tom run
- K/S break
- C arrival
- no fill

**E. How often does it vary?**
- every bar: only styles built on micro-variation
- every 2 bars: active dance/break styles
- every 4 bars: common
- every 8 bars: restrained/progressive
- only at structural events: orchestral/minimal

Then generate the simplest groove that satisfies those five decisions.

---

# 19. The central rule

**Genre is not a list of exact beats. It is a hierarchy of expectations.**

Preserve the few events that define the style, then vary the events around them.

Examples:

- Trance survives if the quarter-note K pulse and dance subdivision remain clear.
- Rock survives if the backbeat and riff-supporting kick remain clear.
- Trap survives if the halftime snare anchor, sparse syncopated kick, and detailed hats remain clear.
- DnB survives if the fast broken K/S relationship remains clear.
- Hardcore/gabber survives if the relentless fast K pulse remains clear.
- Metal survives if the drum accents reinforce the riff and sectional intensity.
- Classical/orchestral survives when percussion follows form and dramatic punctuation rather than behaving like a looping kit.

When in doubt: **use fewer rules, preserve the defining anchors, and let the musical context determine the variations.**
