# Resone v11 — Merged Drum/Percussion Grammar

## What changed

- Merged `resone_drum_percussion_grammar_v3` into the protected `resone_drums_genre_guide.md`.
- Added a compact `drum_core` that is supplied on every non-empty song-generation genre lookup, even when no genre match is confident.
- `drum_core` includes the Resone K/S/H/O/L/M/T/C/R note map, universal rhythmic atoms, energy controls, section dynamics, phrase defaults, fill behavior, and the minimal drum decision procedure.
- Drum genre matching is now independent from song-design genre matching. The Producer can choose a broad song identity while percussion retrieves a more specific grammar such as deep house, classic trance, Goa, jungle, grunge, nu metal, etc.
- Drum retrieval walks the complete family chain broad-to-specific. Examples:
  - `edm -> trance -> uplifting_trance`
  - `edm -> house -> deep_house`
  - `edm -> drum_and_bass -> jungle`
  - `classical -> romantic_classical -> late_romantic`
  - `rock -> grunge`
  - `metal -> nu_metal`
- Song-design retrieval also now walks all available ancestors instead of stopping after only one parent.
- Explicit hybrid roles still remain separate instead of averaging incompatible grooves. `trap metal`, for example, retains its hip-hop/trap function and metal function as separate inherited jobs.
- The merged guide retains the complete uploaded Drum & Percussion Grammar in a source appendix for maintenance/reference while runtime retrieval injects only the relevant compact blocks.
- Increased stored song genre-context allowance from 16k to 26k characters so universal + family + specific genre information is not prematurely clipped.
- Song generation state now records the independently selected drum genre and resolved drum-family chain.
- The merged guide remains part of the encrypted production instruction bundle; the production AES key is still not embedded in generated C#.

## Validation

Passed in this environment:

- genre-guide retrieval regression
- encrypted production instruction-bundle round trip
- song composer design pass
- composer-overview continuity
- song-composer continuity
- generation-state/song-mode regressions
- piano-roll interaction regression
- updater regression
- live Llama bridge build regression

The Windows .NET compiler is not installed in this sandbox, so the local Windows build remains the definitive C# compiler check.
