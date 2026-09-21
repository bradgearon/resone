# Resone documentation

Resone takes input and does composes or alters midi. You can drag and drop midi in. It uses a local gemma llm and qwen for tts and whisper for asr. The launcher hosts these and auto runs when you launch the app or vst.

More about it on the itch.io page.

https://www.youtube.com/watch?v=QgrTdx8BhL8

![Resone](https://img.itch.zone/aW1hZ2UvNTAyOTM1OC8zMDExMjE3NS5wbmc=/original/K8u%2F%2Bf.png)


Project documentation is intentionally kept in one flat `docs/` folder.

- [Architecture overview](architecture-overview.md) — native/UI/API structure and reuse provenance.
- [Production build and release](production-build-and-release.md) — customer build, optional licensing/instruction protection, and installer packaging.
- [Song generation](song-generation.md) — full-song generation architecture and memory/context flow.
- [Vocals lane](vocals-lane.md) — voice design, saved voices, and singing behavior.
- [AI runtime provisioning](ai-runtime-provisioning.md) — runtime/model locations and provisioning behavior.
- [Application updates](application-updates.md) — launcher/updater flow and release manifests.
- [Validation log](validation-log.md) — current validation and regression notes.
- [Validation record — 2026-09-16](validation-2026-09-16.md) — retained validation snapshot.
- [Third-party dependencies](third-party-dependencies.md) — bundled/runtime dependencies and distribution notes.
- [iPlug2 license](iplug2-license.txt) — third-party license text.
- [Release history](release-history.md) — chronological release changes.
- [Development history](development-history.md) — merged historical patch/handoff notes from earlier snapshots.
