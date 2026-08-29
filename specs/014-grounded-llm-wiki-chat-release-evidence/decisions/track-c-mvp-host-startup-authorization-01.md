# Story 12.5 Track C-MVP host-startup authorization 01

- Decision ID: `story-12-5-track-c-mvp-host-startup-authorization-01`
- Decided at: `2026-08-29T23:28:00Z`
- Recorded at: `2026-08-29T23:29:44Z`
- Scope SHA-256: `6e88b42958a615399a9b4167a1c9fc27f7229ea7cedcc7f4febedafd8c775400`
- Standing authorizations: `story-12-5-track-c-mvp-overnight-standing-authorization-01/02`
- Status: **accepted**

## Authorized action

Start one controllable local Vulgata host for the bounded Track C-MVP run. Bind
both application contexts to only the guarded database
`vulgata_story125_93fd44c9b3044fee84f2d032ac3af8a2`, allow `Program.cs` to apply only the
repository's already-existing migrations at startup, and keep the service alive
only for the planned authenticated UI/API, restart, and screenshot evidence.

Before startup, the connection target must resolve to that exact database name.
No connection string, password, API key, token, or source content may be written
to evidence or command output.

## Exclusions

This decision does not authorize creating or editing an EF migration,
`VulgataDbContext`, a model snapshot, schema authority, another database, source
configuration, Story 12.4 state, Scan behavior, or fake evidence. Startup or
migration failure must remain an honest Track C result.
