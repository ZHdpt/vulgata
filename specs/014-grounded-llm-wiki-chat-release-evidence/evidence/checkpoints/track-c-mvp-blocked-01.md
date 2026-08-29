# Story 12.5 Track C-MVP external-blocker checkpoint 01

- Observed at: `2026-08-29T17:15:07Z`
- Branch: `demo0831/story-12-5-trackC`
- Candidate: `65b4204b94c16310b8ada631865e39543b4b6f6c`
- Accepted Story 12.4 dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Scope SHA-256: `6e88b42958a615399a9b4167a1c9fc27f7229ea7cedcc7f4febedafd8c775400`
- Standing authorization: `story-12-5-track-c-mvp-overnight-standing-authorization-02`
- Gate: T008 external preflight
- Result: **blocked**

## Completed checks

- Candidate branch/index/worktree ownership was exact and clean before evidence
  mutation.
- `mbs_account` and `mbs_channel` exist, are clean, and match their exact accepted
  Profiled Revisions.
- Docker client/server `29.7.2` and a healthy PostgreSQL 17 container are ready.
- CodeGraph `1.5.0`, its three approved component hashes, Java `21.0.12`, and the
  accepted JDT LS archive/checksum and installation are ready.
- A streaming-provider credential is present without disclosure.
- Local browser/manual screenshot capability is available.
- Reserved database identity
  `vulgata_story125_4042986b0c574829a5c713a00920b5dd` passed the lexical owned-name
  boundary by construction but was not created or contacted.

## Proven blockers

The approved PostgreSQL administrator authority, configured streaming-provider
endpoint/model, explicit real-pair/source-binding opt-ins, System/two-manifest
bindings, and authenticated authorized-user readiness are absent. These are
external prerequisite failures, not product/review/convergence failures.

No source was disclosed and no database connection, migration, application
host, Scan, provider request, query, restart, or screenshot operation occurred.
T009-T022 remain gated and unchecked.

## Recovery

Supply the missing approved local bindings without exposing their secret values,
then resume at T008. Re-observe the candidate and complete the whole sanitized
preflight before using the Story 12.5 owned-database guard or creating a database.
