# Resone v12 — Compact Genre Context + Melody-Mode Drum Genre Identification

## Drum genre context is compact and lane-scoped

- The always-on `drum_core` runtime brief is now a compact fundamentals block (kit map, pulse, kick/snare/hat policy, fills, energy, phrase behavior, central genre rule).
- The full merged Drum & Percussion Grammar remains in the protected guide as a source appendix, but is not injected wholesale into LLM requests.
- Drum retrieval still inherits broad-to-specific fundamentals, e.g. `drum_core -> edm -> trance -> uplifting_trance`.
- Song-mode pitched/vocal lanes receive song-design genre guidance only; drum grammar is added only to drum-lane packets.
- Song mode still gives the Producer and global Composer Design pass the combined selected genre context so they can plan the whole arrangement.

## Melody-mode drum genre identification

For ordinary non-Song-mode lane generation only:

1. If the selected lane is a drum lane, Resone makes one tiny `DrumGenreIdentification` LLM request.
2. The model returns one short genre/subgenre string (or `NONE`).
3. Resone fuzzy-matches that label against the local deterministic genre guide.
4. The resulting compact drum brief is supplied to both the Director and Composer.
5. If the classifier returns `NONE`, an unfamiliar label, or errors, Resone falls back to deterministic matching against the user request and ultimately the compact drum core. Generation never fails because genre identification failed.
6. Revision requests include the lane's original brief and recent update prompts in the tiny classifier request, so `more energy` can retain a prior `trance drums` identity.

No extra genre-identification LLM call runs for:

- Song mode (the Producer's `Overall identity` already owns genre selection),
- pitched melody/bass/chord/accent lanes,
- vocal lanes.

## No artificial LLM context/output clipping

- Removed the v11 `MaxGenreContextChars` limit rather than increasing it.
- Removed genre-context slicing from Producer and Composer Design prompts.
- Removed the 65,536-character streaming output guard from both HTTP and in-process llama.cpp clients; EOS/model context is the generation limit.
- Removed arbitrary character clipping from Song-mode producer design, composer design, section memory, exact musical memories, and open composer commitments.
- Removed producer/composer design persistence truncation in workspaces.
- Existing semantic validation such as valid notation, meter, lane count, etc. remains intact.

## Compatibility

- `GenreContext` remains on `SongGenerationState` only for loading older saved state.
- New states store `GenreSongContext` and `GenreDrumContext` independently.

## Validation

Passed in this environment:

- genre guide retrieval regression
- song section parser regression
- piano-roll interaction regression
- updater regression
- launcher live-build regression
- llama bridge live-build regression
- UI DOM contract regression
- unlimited LLM generation regression
- Song mode regression
- Composer Design pass regression
- composer continuity / handoff commitments
- composer overview workspace continuity
- song workspace regression
- generation-state regression
- encrypted seven-file instruction bundle round-trip

The Windows .NET SDK is not installed in this sandbox, so the actual Windows compiler/build still needs to be run locally.
