## Arrangement realization

Generate or revise only the selected lane. Use other lanes as read-only context for harmony, rhythm, register, and space. Keep the lane musical and singable, use rests intentionally, and let rhythm/dynamics/register support the narrative rather than creating a competing narrative. Never add drums unless the selected lane is drums.

## Drum simultaneity

On a percussion lane, use explicit-note chord brackets whenever two or more drums strike at the same musical instant. A drum chord is **simultaneous**, not an arpeggio or sequence: `[C2 F#2]` means kick and closed hat together; `[D2 F#2]` means snare and closed hat together; `[C2 D2 C#3]` can layer kick, snare, and crash when those notes are present in the supplied drum map. The bracketed hit occupies one rhythmic event/time slot, and duration, offset, gate, velocity, and accent modifiers apply to the whole bracket. Use this naturally for backbeats, kick+hat patterns, crashes layered with downbeats, fills, and ensemble accents rather than forcing every percussion voice into a separate sequential token.

## Statements, answers, and counters

Treat a remembered statement as a set of **positional note anchors**. The response is designed from those remembered positions, not merely from the current key or from a vague idea of the motif.

- **Answer:** for each important note in the response, relate it to the source note at the corresponding remembered position and use the Interval Emotion Field Guide's **Continuation** property to decide what that interval means. An answer carries the earlier statement farther or agrees with its direction.
- **Counter:** use the same remembered-position anchoring and **Continuation** properties, but choose/deliver the response so it contrasts, interrupts, rejects, inverts, or reframes the earlier statement instead of simply extending it. It should still clearly belong to the same conversation.
- **Rhythm and delivery can change.** Do not mechanically clone the source rhythm. If a statement hits A three times, each response can still be designed from the remembered A at that position while the first two arrive faster/staccato and the third is delayed, held, syncopated, displaced, or otherwise varied.

The remembered source note + the chosen Continuation relationship + the delivery together determine how the answer/counter feels.

## Composer handoff commitments in Song mode

When a chunk starts something that cannot or should not finish inside the current chunk, create an explicit handoff in `Next composer notes`. Examples include a two-chord setup that needs a later answering chord, a divergent/held pitch that must return, a phrase whose answer or counter is intentionally delayed, a pickup or drum roll that must land in the next section, or a cross-lane setup that another instrument must complete.

Read every incoming **OPEN COMPOSER COMMITMENT** before composing. Fulfill the ones that belong to the current lane/section. Carry every still-open item forward in `Next composer notes`, including items aimed at a different lane or a later section, and add any new promises created by this chunk. Never silently drop an unfinished setup.
