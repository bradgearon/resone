# Resone v10 — Genre Brief Retrieval

## Added
- Added `resone_song_design_genre_guide.md` and `resone_drums_genre_guide.md` to `assets/Instructions/Music`.
- Added deterministic `GenreBriefResolver` with canonical-ID/alias parsing, typo-tolerant fuzzy matching, parent genre support, explicit hybrid handling, optional modifiers, and fail-soft behavior.
- Producer receives a best-effort genre brief from the user's original request before it writes the song identity.
- After producer output, Resone extracts `Overall identity` and re-resolves the final canonical genre from that identity; the producer identity outranks the original request.
- Composer Design receives the final song-design and drum briefs.
- Song generation state persists the selected genre IDs and compact genre context.
- Every later song Director and Composer call receives the same selected genre context through `FULL SONG GENERATION CONTEXT`.
- `DesignSongAsync` returns `genreId` for diagnostics/UI use.

## Matching behavior
- Specific aliases outrank generic parent genres.
- Typo-tolerant token matching supports near matches such as `progressive metel`.
- Explicit `trap metal` is treated as the documented trap + metal hybrid.
- Cinematic/orchestral language can add `cinematic_orchestral` as a secondary role rather than replacing a more specific primary genre.
- Unknown/genre-neutral requests simply receive no genre injection; generation continues normally.

## Production protection
- Both genre guides were added to the encrypted customer instruction bundle.
- The instruction bundle test now validates seven protected files and confirms the AES key is not embedded in generated C#.

## Tests
- Added `tests/genre-guide-retrieval-regression.py`.
- Existing composer continuity, song mode, workspace, piano-roll, updater, live Llama bridge, and encrypted instruction-bundle regressions pass in this environment.
- The .NET SDK is not installed in this sandbox, so the Windows/.NET compiler build still needs to be run locally.

## Future drum-guide expansion
The retrieval code reads canonical IDs and aliases from the Markdown at runtime. You can expand the current drum entries without changing code as long as canonical IDs remain compatible with the song-design guide. New aliases are picked up automatically.
