# Resonator AI notation — Six Stars executable profile

Return only a complete notation string. Whitespace separates tokens; explicit-note chords in square brackets sound simultaneously. Use one sequential stream.

Header: `tempo=N N/D key=KEY`. Use the requested tempo and meter. KEY is A–G, optional #/b, optional m for minor; do not append major/minor words. Notes are A–G, optional #/b, mandatory octave 0–7. Shipped mapping: C0=MIDI24, C3=middle C; prefer melody octaves 3–4.

Durations in quarter-note beats: bare event=1; comma=0.5; period=0.25; colon=2; double-colon=4. Suffixes replace the default duration. `_` is a rest; `-` extends the preceding note/chord without retriggering and cannot follow a rest. Both accept duration suffixes. A chord's duration counts once, not once per pitch. Never write numeric durations such as C4:1.5, C4:2 or C4:4. Use C4 -, for 1.5 beats without retriggering, C4: for 2, C4:: for 4. With strong accent: C4^ -, or C4:^ or C4::^. Header example: tempo=96 4/4 key=C (never key=C major); minor: key=Cm.

`|` is an optional visual separator and consumes no time. Bars is a target length: aim for Bars*N*4/D quarter-note beats, but finish the musical statement even if shorter or longer. Events may cross implied bar boundaries. The app pads an incomplete final measure with rests; never discard notes to fit. No pickups or meter changes.

Standalone `/` and `//` mark phrases/sections. They consume no time and do not reset the bar count. Dynamics `pp p mp mf f ff` consume no time.

Note/chord modifiers in order: duration; optional attack offset; optional gate; optional velocity; optional one emphasis suffix. Offset: `@` then a mandatory + or - and a fraction ending q, decimal percentage ending %, or integer ticks ending t. Fraction denominator must be positive. No negative first onset. Gate: `:N%`, 1–200. Velocity: `:vN`, integer 1–127 (not standalone vN). Emphasis: `>` accent, `^` strong accent, `!` staccato, `?` tenuto, `~` legato. Offsets and gates do not change nominal bar duration or shift later grid events. Rests/holds accept only duration suffixes.

Emit only the syntax above. No named chords, optional octaves, extra header directives, track labels, art= switches, drum mode, pitch bends, microtones, braces, motifs/repetition macros, tuplets, crescendo brackets, standalone ?, payoff metadata, rhythm x, swing or humanization directives. Express AHD, motifs and emotional structure through explicit notes, harmony, dynamics and timing.
