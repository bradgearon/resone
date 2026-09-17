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
```

Reusable exact Resonator fragments can be placed in backticks. The marker and memory text are removed before MIDI parsing.

`SongGenerationProvisioner` stores the compact notes and extracts exact reusable musical memories. The next section/lane can receive them without receiving the entire prior song notation.

## 4. Answering parts

The normal composer instructions include an answering-part rule. An answer should be designed relative to the remembered/source note at the corresponding musical position and use the narrative director's Continuation interval/perspective. Rhythm and delivery should vary as part of the response—for example, a three-note repeated statement can be answered by the continuation interval twice faster/staccato and a third time delayed.

## 5. Existing generation remains unchanged

When Song mode is off:

- Submit calls only `compose`.
- The producer is never called.
- `SONG MEMORY NOTES` are not requested or accepted as ordinary output.
- The selected-lane director/composer behavior remains the existing workflow.
