Apply over the current patched project. Back up custom music-composition.json.
Extract into U:\Resone, rebuild and restart launcher and editor.

LLM output is now plain text received over the existing SSE stream:
track=<exact-lane-id> tempo=120 4/4
C4 E4 G4 C5
track=<another-lane-id> tempo=120 4/4
C3 _ G3 _

Each track header starts on its own line; notes may follow on that same line.
A subsequent header or end of response terminates the track. Body syntax still
uses resonator_api_v0.1.md. Both generation and repair prompts live in JSON.

Missing lanes are accepted. Valid tracks survive malformed tracks, unknown IDs,
and duplicates. First valid occurrence of each requested ID wins. If at least
one track is playable, no repair is requested. Omitted lanes remain empty rather
than replaying old music. All-invalid responses get the existing one repair.
Internal application messages remain JSON; LLM-generated notation does not.

This changes output framing and partial-result acceptance, not playback timing:
LLM text streams, but playback still waits for end-of-response parsing.
Verified API build zero warnings/errors, text-header decoding, missing-track
acceptance, drum mapping, invalid-track isolation and UI partial autoplay.
Live LLM and Windows playback not verified.
