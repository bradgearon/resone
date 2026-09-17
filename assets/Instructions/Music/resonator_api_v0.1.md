# Resonator Language — v0.1 Parser Surface

This document describes the syntax implemented by the first MIDI generator.

## Notes

Notes and chords are one quarter-note beat by default. They can be shortened and offset using the duration and onset offset syntax described below.
Duration names (quarter, eighth, sixteenth, half, whole) and the word rest are explanations, never output tokens.

```text
A B C# Eb
A3 C#4 E4
```

Octaves are optional. `defaultOctave` and `midiNoteForC0` come from the profile.

## Durations

A bare note lasts 1 beat. A comma suffix shortens it to 0.5 beats; a period suffix to 0.25 beats. Output only the note and its optional punctuation:

```text
A
A,
A.
```

Four quarter notes, with no duration words:

```text
E4 G4 A4 B4
```

An explicit-note chord such as [E G# B] also lasts 1 beat by default.

## Rests

A bare underscore lasts 1 beat; comma and period suffixes shorten it to 0.5 and 0.25 beats. Do not append the word rest. Whitespace around the rest marker is optional: `C5_` and `C5 _` both mean play C5, then one beat of rest.

```text
_
_,
_.
C5_
C5 _
```

## Holds

A hold extends the previous note/chord without retriggering it. Whitespace before a trailing hold marker is optional when unambiguous: `A-` and `A -` are equivalent. Negative octaves, cents, and onset offsets keep their normal meaning.

```text
A -
A-
A, -,
A. -.
```

## Bars, phrases, sections

These are structural markers and do not themselves consume time.

```text
| A C E D | / C B A _ // C# E F# G# |
```

- `|` bar marker
- `/` phrase marker
- `//` section marker

## Chords

Exact notes:

```text
[A C E]
[C#3 E3 G#3 B3]
```

## Tempo and meter

```text
tempo=120
120
4/4
6/8
```

The first writer emits one initial MIDI tempo and time signature.

## Dynamics

```text
pp p mp mf f ff
```

These set the default velocity for following notes. Emit them as bare tokens. Do not write `p=mf`, `dynamic=mf`, or `velocity=mf`. The parser accepts common key/value aliases defensively, but they are not canonical output syntax.

## Exact velocity

```text
A:v96
[A C E]:v110
```

## Articulation marks

```text
A>    accent
A^    strong accent
A!    staccato
A?    tenuto/emphasis
A~    legato overlap
```

## Gate / sounding length

```text
A:75%
A,:60%
```

The rhythmic grid remains unchanged; only the actual note length changes.

## Onset offsets

```text
A@+1/8q
A@-1/16q
A@+12.5%
A@+60t
```

Offsets move the attack while leaving subsequent grid events in place.

Combined:

```text
A,@+1/8q:85%
```

## Microtonal cents

```text
C+30c
C+50c
C+70c
C#-30c
```

## Continuous pitch bends

```text
C~C#
C#~C
```

The writer emits a stepped MIDI pitch-bend curve over the duration of the note.
