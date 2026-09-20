# Resone Song Design Genre Guide

## Purpose

This guide tells Resone how music in a genre tends to be **designed over time**. It is about form, phrase behavior, motif development, harmony, melody, bass, density, transitions, climax behavior, and repetition/variation.

Use it together with:
- the user's request
- the Interval Emotion Field Guide
- Anchored Harmonic Divergence (AHD) when enabled
- `resone_drums_genre_guide.md`
- the current song state and existing motifs

The user's request is authoritative. Genre guidance should shape realization, not erase requested emotions, motifs, chords, meters, or deliberate AHD behavior.

## Retrieval contract

Prefer:

**specific subgenre > parent genre > optional modifier**

Normally send:
- one primary guide
- one parent guide if useful
- optionally one modifier

Examples:
- `progressive metal` -> `progressive_metal` + `metal`
- `Tool-ish odd-meter heavy` -> `polyrhythmic_progressive_metal` + `progressive_metal`
- `Mars Volta chaotic latin prog` -> `psychedelic_latin_progressive_rock` + `progressive_rock`
- `uplifting trance` -> `uplifting_trance` + `trance`
- `romantic orchestra` -> `romantic_classical` + `classical`
- `neoclassical dark piano` -> `neoclassical_modern_classical` + `classical`
- `trap metal` -> `trap` + `metal`
- `post-rock cinematic build` -> `post_rock` + `cinematic_orchestral`

When combining guides, assign jobs instead of averaging them:
- primary genre controls form and pacing
- parent genre supplies broad grammar
- modifier changes one dimension such as scale, darkness, psychedelia, minimalism, or cinematic weight

## Global design rules

### Preserve identity while developing
Prefer transforming remembered material over constantly replacing it. Useful transformations include octave transfer, register expansion, rhythmic displacement, fragmentation, inversion, reharmonization, pedal reinterpretation, density growth, call/response, orchestral doubling, AHD divergence, and motivic reassertion.

### Make section contrast legible
Different sections should usually differ in at least two dimensions: density, register, harmony, rhythm, phrase length, articulation, bass activity, melodic range, repetition rate, or dynamics.

### Earn the climax
A climax should usually combine previously established identities at greater scale rather than introduce unrelated material.

### AHD integration
Genre conventions decide **how divergence is presented**, not whether foreign notes are allowed. Rock may mutate a riff; metal may turn divergence into weight; trance may normalize it through repetition; hip-hop may loop it; classical may develop it motivically; progressive music may let the divergent state become a temporary secondary home.

---



# rock

**Canonical ID:** `rock`

**Aliases:** rock, classic rock, guitar rock, arena rock, hard rock

**Core:** memorable sections, strong riff/chord identity, physical energy changes.
**Form:** intro -> verse -> chorus -> verse -> chorus -> bridge -> final chorus; or riff-based A/B forms.
**Phrases:** 2/4/8-bar phrasing is common. Repetition matters; second passes should gain something.
**Harmony:** diatonic/modal mixture, power relationships, pedals, borrowed chords, chromatic passing motion. AHD works well when divergence attaches to an established riff or pedal.
**Melody:** singable contour, recognizable peaks; choruses usually simplify or widen.
**Bass:** reinforce roots/riff identity, then connect or counter where space allows.
**Density:** verses leave room; choruses widen register/doubling; bridges subtract or reframe.
**Transitions:** fills, pickups, held chords, stop-time, one-beat vacuums.
**Avoid:** changing the central riff constantly, identical verse/chorus density, harmony so elaborate it destroys the physical hook.


# alternative_indie_rock

**Canonical ID:** `alternative_indie_rock`

**Aliases:** alternative, alt rock, indie, indie rock, art rock

**Core:** character over polish. Hooks may be textural, harmonic, or rhythmically awkward.
**Form:** may be asymmetrical; sections can arrive early or end abruptly.
**Harmony:** borrowed chords, modal color, open intervals, pedals, suspended shapes, unexpected substitutions.
**Melody:** narrow understated verses can bloom suddenly; awkward-but-memorable intervals are valid.
**Arrangement:** subtraction is powerful. One unusual sound or register can define a section.
**Avoid:** generic arena-rock defaults, constant wall-of-sound, forcing every phrase into symmetrical 8-bar blocks.


# punk_hardcore_punk

**Canonical ID:** `punk_hardcore_punk`

**Aliases:** punk, punk rock, hardcore punk, melodic hardcore, skate punk

**Core:** urgency, directness, short functional sections.
**Form:** rapid verse/chorus/bridge cycles; little setup required.
**Harmony:** strong roots, power relationships, simple modal/minor/major fields.
**Melody:** chants, repeated notes, octaves, short answering phrases.
**Transitions:** abrupt stops and immediate re-entry are idiomatic.
**Avoid:** ornate harmonic detours, long atmospheric intros unless explicitly post-hardcore, excessive countermelody.


# post_rock

**Canonical ID:** `post_rock`

**Aliases:** post rock, atmospheric rock, crescendo rock, instrumental post-rock

**Core:** long arcs built from a small seed.
**Form:** quiet seed -> repetition -> added layer -> wider register -> rhythmic activation -> massive climax -> aftermath.
**Harmony:** pedals, open fifths, added tones, slow bass changes, suspended/modal fields.
**Motifs:** repeat enough for memory, then add one meaningful change at a time.
**AHD:** excellent for introducing one foreign color quietly, then making its later return emotionally huge.
**Avoid:** unrelated motif proliferation, early maximum density, constant busy bass.


# metal

**Canonical ID:** `metal`

**Aliases:** metal, heavy metal, extreme metal

**Core:** riff identity often matters more than conventional chord progression.
**Form:** riff A -> A' -> B -> return A -> breakdown/bridge -> expanded A.
**Harmony:** pedals, chromatic roots, modal minor, tritones, seconds, diminished/augmented color, parallel motion.
**Melody:** leads can contrast low riffs; sustained high notes over low rhythmic mass create scale.
**Bass:** tight riff lock for weight, selective departure for harmonic perspective.
**Density:** coordinated attacks and register placement create weight; gaps before impacts matter.
**Avoid:** chord-pad behavior under every riff, smoothing chromatic friction, permanent maximum density.


# thrash_metal

**Canonical ID:** `thrash_metal`

**Aliases:** thrash, thrash metal, speed metal

**Core:** forward propulsion and riff contrast.
**Form:** short riff cells can cycle rapidly; quick transitions are expected.
**Harmony:** chromatic root motion, pedal riffs, minor centers, semitone/tritone approaches.
**Development:** raise subdivision, shorten cycles, or alter riff endings.
**Avoid:** lush sustained harmony, long ambient transitions, weak downbeat identity.


# death_metal

**Canonical ID:** `death_metal`

**Aliases:** death metal, technical death metal, brutal death, melodic death

**Core:** dense riff succession anchored by recurring identities.
**Contrast:** alternate blast-like intensity, syncopated technical sections, and slow crushing states.
**Harmony:** chromatic, diminished, minor-second, tritone, symmetrical and pedal-based language.
**Melody:** melodic variants can carry strong minor themes; technical variants use angular contour.
**Avoid:** random-note complexity with no remembered cell, constant same-speed density, excessive prettification.


# black_metal

**Canonical ID:** `black_metal`

**Aliases:** black metal, atmospheric black metal, symphonic black metal

**Core:** hypnotic repetition and large emotional fields.
**Harmony:** minor/modal, semitone clashes, open fifths, parallel shapes, pedals.
**Texture:** high-register dissonance can read as icy/transcendent rather than simply tense.
**Form:** section changes may be primarily register/density/harmonic-field changes.
**Avoid:** overly funky syncopation, constant neat call/response, resolving every exposed dissonance.


# doom_sludge

**Canonical ID:** `doom_sludge`

**Aliases:** doom, doom metal, sludge, sludge metal, stoner doom

**Core:** slow harmonic evaluation; fewer changes with heavier consequences.
**Harmony:** minor 2nds, tritones, lowered degrees, open fifths, chromatic descent, pedals.
**AHD:** foreign notes can be held long enough to color an entire bar or section.
**Arrangement:** silence and decay are structural; climax can come from register expansion rather than tempo.
**Avoid:** constant eighth-note harmonic changes, busy upper counterpoint during the heaviest statements.


# power_symphonic_metal

**Canonical ID:** `power_symphonic_metal`

**Aliases:** power metal, symphonic metal, epic metal, fantasy metal

**Core:** heroic themes, large chorus returns, upward motion, long-range payoff.
**Harmony:** minor/major contrast, dramatic mediants, leading-tone drive, cinematic sequence.
**Melody:** 4ths, 5ths, 6ths, octaves, and high arrivals work strongly for heroism/radiance.
**AHD:** supernatural brightness inside minor can be established without full modulation.
**Climax:** stack main theme, bass drive, broad harmony, high counterline, octave reinforcement.
**Avoid:** maximal orchestration from bar one, too many simultaneous hero melodies.


# metalcore

**Canonical ID:** `metalcore`

**Aliases:** metalcore, modern metalcore, melodic metalcore, post-hardcore metal

**Core:** contrast driving riffs, open emotional choruses, and breakdown weight.
**Verse:** riff-locked and rhythmically active.
**Chorus:** simplify rhythm, widen melody/harmony.
**Breakdown:** re-weight existing identity rather than invent random low material.
**Avoid:** breakdown every four bars, chorus with the same rhythmic density as verse.


# djent_polyrhythmic_metal

**Canonical ID:** `djent_polyrhythmic_metal`

**Aliases:** djent, polyrhythmic metal, math metal, syncopated modern metal

**Core:** the accent cycle is a structural identity.
**Rhythm:** repeat groupings such as 3+3+2 or 5+5+6 long enough to become learnable.
**Harmony:** pedals, extended color, clean atmospheric contrast, chromatic upper structures.
**Melody:** spacious sustained lines can float above busy low material.
**Avoid:** random syncopation, every instrument sharing every guitar accent, changing grouping before the listener learns it.


# progressive_metal

**Canonical ID:** `progressive_metal`

**Aliases:** prog metal, progressive metal, technical progressive metal, cinematic prog metal

**Core:** development, not mere complexity.
**Form:** motif seed -> expansion -> metric reinterpretation -> harmonic divergence -> climax -> memory return.
**Phrases:** uneven lengths are valid when they complete a sentence.
**Harmony:** modal mixture, chromatic mediants, pedals, foreign tonal areas, extended harmony, AHD.
**Melody:** answer earlier phrases from changed emotional perspectives; use register to mark structural stages.
**Avoid:** complexity with no memory, every section introducing a new riff, purposeless meter changes.


# polyrhythmic_progressive_metal

**Canonical ID:** `polyrhythmic_progressive_metal`

**Aliases:** Tool, Tool-ish, Tool-like, hypnotic prog metal, tribal prog metal

**Core:** small obsessive cells, spacious repetition, psychological accumulation.
**Rhythm:** additive/irregular grouping can ride over a larger pulse; cycles may drift and realign.
**Harmony:** pedal-centered minor/Phrygian/chromatic-neighbor/tritone/open-fifth fields.
**AHD:** establish home, normalize divergence through repetition, then make the remembered home powerful on return.
**Arrangement:** bass may carry primary identity; high melody stays sparse; climax often comes from cycle alignment or full-weight riff return.
**Avoid:** constant chord changes, hyperactive leads, odd-meter tricks that reset before becoming perceptible, copying a specific existing song.


# progressive_rock

**Canonical ID:** `progressive_rock`

**Aliases:** prog rock, progressive rock, art prog, symphonic prog

**Core:** multi-section form, thematic return, instrumental development, changing meter, contrasting harmonic worlds.
**Connection:** sections should share motif, interval, rhythm, or narrative role.
**Harmony:** modal mixture, secondary homes, chromatic mediants, extended harmony, pedals.
**AHD:** a secondary world can become a temporary home before reassertion.
**Avoid:** unrelated section collage, virtuosity replacing thematic development.


# psychedelic_latin_progressive_rock

**Canonical ID:** `psychedelic_latin_progressive_rock`

**Aliases:** Mars Volta, Mars-Volta-like, chaotic latin prog, psychedelic prog, latin prog

**Core:** volatile transitions, elastic rhythmic energy, sudden density changes, recurring hooks.
**Form:** feverish motion can collapse into eerie space, then snap back to a remembered identity.
**Harmony:** chromatic lines, altered dominant color, modal mixture, unexpected pivots, pedals, semitone voice-leading.
**Melody:** wide leaps, urgent repeated notes, high cries, descending collapses, ornamental runs.
**Arrangement:** independent lanes may periodically align for impact; sudden stop -> isolated gesture -> explosive return works well.
**Avoid:** random chaos with no hook, equal density every bar, cloning any specific song.


# edm

**Canonical ID:** `edm`

**Aliases:** EDM, electronic dance, dance music, club electronic

**Core:** design in energy states: intro -> groove -> build -> pre-drop vacuum -> drop -> breakdown -> rebuild -> larger drop.
**Harmony:** loops can stay fixed while register, rhythm, bass, and density create development.
**Melody:** short hooks often outperform long melodies; breakdowns may be more lyrical.
**Bass:** structural; drop identity often lives in bass rhythm/timbre.
**Transitions:** compositional filter-thinning, ascending sequences, accelerating repetition, pre-drop silence.
**Avoid:** permanent full-spectrum density, too many drop chord changes, long static sections with no evolving layer behavior.


# house

**Canonical ID:** `house`

**Aliases:** house, deep house, tech house, progressive house

**Core:** groove continuity and gradual layer change.
**Form:** 8/16/32-bar energy blocks are common.
**Harmony:** repeating loops, 7ths/9ths, suspensions, pedal relations; tech house may use very little harmony.
**Melody:** small hooks and vocal-like fragments.
**Avoid:** huge resets every four bars, overlong lead melodies fighting the groove.


# techno

**Canonical ID:** `techno`

**Aliases:** techno, minimal techno, industrial techno, melodic techno

**Core:** accumulation, subtraction, timbral mutation, pattern evolution.
**Harmony:** can be extremely static; pedals, one/two-note cells, dark minor/chromatic tension.
**Motif:** one small rhythmic/melodic object can carry long development.
**Avoid:** pop-speed chord turnover, constant unrelated hooks.


# electro_breakbeat

**Canonical ID:** `electro_breakbeat`

**Aliases:** electro, electro breaks, breaks, breakbeat, big beat

**Core:** syncopated groove and riff exchange.
**Form:** short call/response motifs; breakdown by removing kick/bass or fragmenting the hook.
**Harmony:** often secondary to rhythm; modal/minor loops and chromatic synth riffs fit.
**Avoid:** flattening into four-on-the-floor behavior, long pads obscuring rhythmic identity.


# dubstep_bass_music

**Canonical ID:** `dubstep_bass_music`

**Aliases:** dubstep, bass music, brostep, melodic dubstep

**Core:** build/drop contrast and a clear rhythmic bass sentence.
**Drop:** rests are part of the riff.
**Harmony:** sparse during heavy drops; melodic variants may use emotional progressions in intros/breakdowns.
**Avoid:** nonstop bass events, no build/drop contrast, too many simultaneous hooks.


# drum_and_bass

**Canonical ID:** `drum_and_bass`

**Aliases:** dnb, drum and bass, drum & bass, jungle, liquid dnb, neuro dnb

**Core:** fast surface rhythm with slower harmonic perception.
**Harmony:** liquid favors emotional chords; neuro favors darker pedals/chromatic bass architecture.
**Arrangement:** pads/chords may move in half-time perception while drums race.
**Avoid:** every lane becoming 16th-note busy, chord changes at drum speed.


# trance

**Canonical ID:** `trance`

**Aliases:** trance, classic trance, progressive trance

**Core:** long-form tension/release through repetition.
**Form:** intro -> groove -> motif emergence -> breakdown -> build -> full theme -> development -> final return.
**Harmony:** repeating emotional progressions, suspensions, pedals, leading motion.
**Melody:** memorable and sequence-friendly; must survive octave doubling.
**AHD:** one foreign pitch can gain meaning over repeated exposures.
**Avoid:** too many unrelated melodies, impatient builds, changing the progression before expectation forms.


# uplifting_trance

**Canonical ID:** `uplifting_trance`

**Aliases:** uplifting trance, euphoric trance, emotional trance, anthem trance

**Core:** emotional ascent.
**Breakdown:** strip to harmony/theme.
**Build:** reintroduce pulse and raise register.
**Climax:** theme becomes physically enormous.
**Melody/Harmony:** major/minor contrast, ascending 5ths/6ths/octaves, suspensions, leading tones.
**AHD:** radiant foreign intervals fit naturally when established, withheld, then activated into the main theme.
**Avoid:** short payoff, maximal density before breakdown, over-fussy anthem melody.


# psytrance

**Canonical ID:** `psytrance`

**Aliases:** psytrance, psy trance, psychedelic trance, goa, goa trance

**Core:** hypnotic repeating bass framework with layered motif mutation.
**Harmony:** pedal-centered, modal/chromatic, semitone/exotic/symmetrical sequences.
**Melody:** short strange cells, spiraling sequences, register repetition.
**AHD:** bizarre material can become normal through repetition and later reframe the home.
**Avoid:** lush constant chord pads covering the engine, conventional long pop melodies everywhere.


# hardcore_gabber

**Canonical ID:** `hardcore_gabber`

**Aliases:** hardcore, hardcore techno, gabber, frenchcore, uptempo hardcore

**Core:** physical impact, extreme repetition, brutal contrast.
**Hooks:** simple enough to survive huge density.
**Harmony:** dark minor/modal loops, rave stabs, horror-like chromatic gestures.
**Breakdowns:** relief makes the kick/bass return matter.
**Avoid:** delicate harmonic detail at full density, constant maximum force with no reset.


# hardstyle

**Canonical ID:** `hardstyle`

**Aliases:** hardstyle, euphoric hardstyle, rawstyle, raw hardstyle

**Core:** tease -> build -> kick-driven drop -> melodic break -> euphoric/raw climax.
**Euphoric:** strong lead theme and emotional minor/major movement.
**Raw:** darker simpler field, kick identity dominates.
**Avoid:** weak break/drop contrast, overcomplicated melody above dense kick design.


# hip_hop_rap

**Canonical ID:** `hip_hop_rap`

**Aliases:** hip hop, hip-hop, rap, rap beat, urban beat

**Core:** loop identity and vocal space.
**Development:** subtraction, sample variation, bass change, sparse countermelody, hook-layer expansion.
**Harmony:** short loops, modal/minor, jazz-derived extensions, chromatic sample movement, pedals.
**Melody:** short signatures and answers; avoid continuous lead.
**Bass:** may reinterpret the same upper loop.
**Avoid:** filling every beat, excessive chord changes, countermelody fighting the imagined vocal.


# boom_bap

**Canonical ID:** `boom_bap`

**Aliases:** boom bap, boombap, old school hip hop, east coast hip hop

**Core:** strong sample/loop identity and head-nod repetition.
**Phrases:** 2/4-bar cells can repeat with tiny variation.
**Harmony:** jazz/soul-derived 7ths/maj7s, altered fragments, chromatic sample motion.
**Avoid:** giant EDM build/drop defaults, over-polished constant variation.


# trap

**Canonical ID:** `trap`

**Aliases:** trap, trap beat, melodic trap, dark trap, rage trap

**Core:** sparse loop identity plus strong density changes.
**Harmony:** minor/modal loops, descending patterns, sustained pads; rage variants may be brighter and repetitive.
**Melody:** plaintive 3rds/6ths, bells/plucks, high-register motifs.
**Bass:** 808 motion can become the harmonic narrative.
**Avoid:** too many melodic lanes, bass and melody always sharing the same rhythm.


# drill

**Canonical ID:** `drill`

**Aliases:** drill, UK drill, NY drill, dark drill

**Core:** menacing loop, negative space, bass movement.
**Harmony:** minor, Phrygian-ish, semitone tension, sparse piano/string/bell motifs.
**Development:** rhythm/808 change more than chord field.
**Avoid:** warm dense pads, overly triumphant conventional cadences unless deliberately contrasted.


# lofi_hip_hop

**Canonical ID:** `lofi_hip_hop`

**Aliases:** lofi, lo-fi, lo fi hip hop, chillhop, study beats

**Core:** gentle repetition with slow micro-variation.
**Harmony:** 7ths, 9ths, suspensions, chromatic passing chords, mellow voice leading.
**Melody:** short, human, understated; silence matters.
**Climax:** usually modest unless explicitly requested.
**Avoid:** dramatic EDM arcs, heroic jumps without emotional reason.


# classical

**Canonical ID:** `classical`

**Aliases:** classical, orchestral classical, chamber, symphonic, baroque

**Core:** develop motives rather than merely loop them.
**Hierarchy:** gesture -> phrase -> period -> section -> movement.
**Harmony:** functional harmony, sequence, tonicization, pedal, counterpoint, thematic modulation.
**Motif tools:** sequence, inversion, fragmentation, augmentation, diminution, register transfer, counterpoint.
**Bass:** strongly defines perspective; contrary/stepwise motion can cohere complex upper harmony.
**Avoid:** repeating a four-bar loop unchanged through an entire piece, constant homorhythm unless ceremonial.


# romantic_classical

**Canonical ID:** `romantic_classical`

**Aliases:** romantic, romantic classical, romantic orchestra, late romantic, lush orchestral

**Core:** long emotional arcs, delayed cadences, thematic transformation, surges and retreats.
**Harmony:** chromatic mediants, altered dominants, common-tone shifts, rich suspensions, appoggiaturas.
**Melody:** wide lyrical arcs, yearning 6ths, exposed 7ths, delayed neighbor resolution, climactic high notes.
**AHD:** remembered home can become intensely desirable through extended divergence.
**Arrangement:** intimate textures grow to broad doubling at peaks.
**Avoid:** quick resolution of every tension, mechanical equal-length phrases, constant tutti.


# neoclassical_modern_classical

**Canonical ID:** `neoclassical_modern_classical`

**Aliases:** neoclassical, neo classical, modern classical, contemporary classical, minimalist classical, piano cinematic

**Core:** small cells, ostinati, repeating harmonic fields, gradual transformation, timbral space.
**Harmony:** modal pedals, added tones, open intervals, restrained chromatic shifts.
**Melody:** sparse and emotionally exact; repetition with changed register/harmony can be enough.
**AHD:** subtle foreign-note meaning can accumulate over repetitions.
**Avoid:** over-orchestrating every phrase, defaulting to constant late-Romantic density.


# cinematic_orchestral

**Canonical ID:** `cinematic_orchestral`

**Aliases:** cinematic, orchestral, soundtrack, film score, game score, epic orchestral

**Role:** usually a modifier.
**Design effect:** stronger long-range dynamics, early motif establishment, later large-scale return, foreground/background hierarchy.
**Harmony:** pedals, broad spacing, modal mixture, chromatic mediants, common-tone shifts, AHD, delayed resolution.
**Climax:** combine motif octaves, bass reinforcement, broad harmony, countermelody, high sustained color, rhythmic unification.
**Avoid:** every bar being epic, same register/density in every section.


# Cross-genre modifiers

## radiant_transcendent
High-register payoff; major 3rd/6th, perfect 5th, octave, or contextually bright foreign intervals when supported by the emotional guide. In minor, AHD may create radiance without changing global home.

## dark_ominous
Lower register, pedals, minor 2nds/tritones/lowered scale degrees, withheld clarification.

## heroic
Strong structural arrivals, 4ths/5ths/6ths/octaves, rising register, rhythmic agreement at payoff. Do not make every bar heroic.

## psychedelic
Recontextualize repeated material through metric displacement, register swaps, harmonic pivots, and role changes while preserving one anchor.

## minimal
Fewer motifs, longer repetition, smaller mutations, meaningful silence.

## funky_syncopated
Interlocking parts, offbeat accents, bass/melody conversation, often static harmonic rhythm with changing rhythmic emphasis.

# Smart retrieval notes

1. Detect explicit genre/subgenre words and descriptive proxies.
2. Choose the most specific guide.
3. Add a parent only when it contributes useful grammar.
4. Add one modifier only when it materially changes the request.
5. Existing strong song identity should be developed, not replaced.
6. Emotional intent overrides genre defaults.
7. Artist references should map to broad traits, not a copied song.
8. When revising one lane, send only genre material relevant to that lane.

The desired result is not merely genre-correct ingredients. The desired result is:

> **This song develops the way music in this genre tends to develop, while still being this particular song.**
