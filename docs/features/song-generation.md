# Resone full-song generation

Full-song generation is opt-in. The normal single-lane `compose` workflow is unchanged.

## UI mode

`Song mode` is a persisted UI checkbox and defaults off the first time. Each lane has a `Song` checkbox (`includeInAi`). Only checked lanes are submitted/generated during Song mode.

## 1. Producer runs once

Worker operation: `songDesign`

The user's song request goes to `SongCompositionDesigner` once. It returns streaming plain text only:

```text
Producer notes:
Overall identity: ...
Motif strategy: ...
Rhythmic strategy: ...
Sections:
SECTION 1 [intro] — Intro
Bars: 8
Purpose: ...
Feeling / energy: ...
Development: ...
Transition: ...
...
Final payoff: ...
```

The producer never writes individual chunks. Its ordered section list initializes `SongGenerationState`.

## 2. Section/lane loop

The UI walks the producer's sections from first to last. Inside each section it walks the checked lanes one at a time.

Example:

```text
Producer
-> Section 1 / Melody: director -> composer
-> Section 1 / Bass: director -> composer
-> Section 1 / Chords: director -> composer
-> Section 1 / Drums: director -> composer
-> Section 2 / Melody: director -> composer
-> ...
-> complete
```

Each request is section-scoped. The UI slices the current section to local beat zero before sending it and shifts returned notes back to the section's full-song beat position afterward. Previously generated checked lanes in that section are therefore available as context to later lanes.

Unchecked lanes are not submitted to Song mode.

Worker operation: `songChunk`

Each chunk receives:

- original song request;
- producer notes;
- complete ordered section list, with the current section marked;
- current section plan;
- recent compact `SONG MEMORY NOTES`;
- exact remembered motif/chord/line fragments worth carrying forward;
- the current section's already-generated checked lanes as ordinary arrangement context.

The chunk then uses the existing `MusicNarrativePlanner` director followed by the existing `ArrangementComposer`.

## 3. Composer memory contract

Only Song mode permits text after the Resonator track. After the notation the composer writes:

```text
SONG MEMORY NOTES
Melody notes: ...
Motifs: ...
Important chords: ...
Next composer notes: ...
```

Reusable exact Resonator fragments can be placed in backticks. The marker and memory text are removed before MIDI parsing.

`SongGenerationProvisioner` stores the compact stable notes and extracts exact reusable musical memories. `Next composer notes` is treated differently: it is the live handoff list. The latest composer replaces that list with every incoming commitment it did not fulfill plus any new setup/payoff obligations it created. Historical section memory does not keep stale handoff lines, so completed obligations do not accidentally reappear. Every later chunk receives the current list as `OPEN COMPOSER COMMITMENTS — MUST BE FULFILLED OR CARRIED FORWARD`.

A handoff should say what is owed and, when relevant, its target lane/section. Examples: finish a two-chord setup, resolve/return to a remembered pitch, answer or counter a phrase later, land a drum roll on the next section downbeat, or complete a transition in another lane. If the current composer cannot fulfill an item because it belongs to a later lane/section, it must carry it forward rather than dropping it.

## 4. Statements, answers, and counters

The composer treats a remembered statement as positional note anchors. To **answer**, each important response note is designed relative to the source note at the corresponding remembered position and uses the narrative director / interval guide's **Continuation** relationship to carry the thought farther. To **counter**, the same positional anchoring and Continuation logic is used, but the result deliberately contrasts, rejects, interrupts, inverts, or reframes the prior statement rather than simply extending it.

Rhythm and delivery are independent expressive dimensions and should vary. For example, if a statement hits A three times, the response can use the chosen Continuation relationship from each remembered A while answering the first two faster/staccato and delaying or holding the third. This keeps the musical conversation recognizable without turning it into literal repetition.

## 5. Simultaneous drum hits

The drum composer is explicitly told that square-bracket explicit-note chords are simultaneous percussion events. It should use forms such as `[C2 F#2]` for kick + closed hat and `[C2 D2 C#3]` for a layered downbeat/crash when those notes exist in the supplied drum map. The bracket consumes one rhythmic position, so layered kit hits do not accidentally become a fast sequence.

## 6. Existing generation remains unchanged

When Song mode is off:

- Submit calls only `compose`.
- The producer is never called.
- `SONG MEMORY NOTES` are not requested or accepted as ordinary output.
- The selected-lane director/composer behavior remains the existing workflow.
