# Story 12.5 Track C-MVP production-composition blocker checkpoint 02

- Observed at: `2026-08-29T23:41:16Z`
- Branch: `demo0831/story-12-5-trackC`
- Evidence HEAD: `d8f60354004cff7233f30570feb7d3cc4594441f`
- Product candidate: `65b4204b94c16310b8ada631865e39543b4b6f6c`
- Scope SHA-256: `6e88b42958a615399a9b4167a1c9fc27f7229ea7cedcc7f4febedafd8c775400`
- Host-start decision: `story-12-5-track-c-mvp-host-startup-authorization-01`
- Gate: T010 combined host run
- Result: **blocked authority/product composition**

## Completed truthful boundary

- The guarded `vulgata_story125_*` database alone received the repository's
  existing Identity/domain migrations and role seeds.
- Production registration created and authenticated the first Administrator.
- Authenticated production APIs created one System, one encrypted Worker provider
  assignment, and exactly the two required Repository records at their pinned
  Profiled Revisions.
- The unrelated dirty source checkout was preserved; committed Git storage was
  selected, and no uncommitted source was read or disclosed.
- The local Vulgata process was stopped gracefully after the bounded observation.

## Proven blocker

Both production Repository Scan start requests returned HTTP 503. The guarded
database contains one System and two Repositories but zero Scan Runs, zero
two-Repository coordinations, and zero Prepared Scan Manifests. Static composition
confirms `Program.cs` registers `RuntimeUnavailableRepositoryProfilingCoordinator`
as `IRepositoryProfilingCoordinator`, even though the real coordinator type exists.
Prepared Scan requires current Scan Runs and a coordination in `Preparing`, so the
retained real Scan cannot proceed from a clean database through the current host.

This is not permission to clone or query Story 12.4 release data. It is not an
environmental PostgreSQL/provider/tool failure. Replacing the unavailable
registration with real profiling materially enables Scan behavior, which the
accepted Track C-MVP definition explicitly forbids changing during the run.

## Recovery

Return to the earliest plan owner and decide whether a separately reviewed story
or planned change should enable the existing production profiling composition.
That decision must receive the repository's Scan-strategy planned-change review
and authorization before production modification. After an accepted replacement
is integrated, start a fresh guarded Track C run; do not reuse these partial rows
as passing Scan evidence.
