# Story 12.5 run-03 all-remaining-tasks approval 02

- Decision ID: `story-12-5-run-03-all-remaining-tasks-approval-02`
- Decision: **accepted**
- Decided at: `2026-08-30T16:09:11.0305547Z`
- Scope SHA-256: `24e509779b785e8aac8dd262e4b9a913a3bf92d9cbb5df1a2cde97fc3de69b1b`
- Starting checkpoint: `7a4163b5ac223004ecfecf7e7a89d3889b6673b1`
- Specification SHA-256: `469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0`
- Plan SHA-256: `6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8`
- Tasks SHA-256: `2e5e6f100f37a2688129304f8cd82478846fc4df98d5ff1a51363d80a6d4d4ab`

## Authorized boundary

The user explicitly authorized execution of every remaining accepted Story
12.5 task, T036-T045, without another routine user-approval pause. This includes:

- the real two-Repository Prepared Scan, Pass 1, exact Cross-Repository
  Resolution and Pass 2 run against the guarded run-03 database;
- sending the accepted bounded source/evidence payloads to the configured remote
  LLM endpoint for the real Worker and streaming Chat paths;
- using the configured `deepseek-v4-flash` model and the credential supplied in
  the user-owned `.env.example` file without copying the credential into Git,
  evidence, logs or assistant output;
- creating additional harness-owned lowercase `vulgata_story125_*` temporary
  databases when a safe bounded retry or independent technical gate requires
  one, while preserving existing databases unless their deletion is separately
  and safely proven in scope;
- ordinary in-scope continuation decisions through convergence, quality review,
  conditional E2E, final verification and completion.

The authorization is bound to final run-03 database
`vulgata_story125_b527027ad0dc4e359070f26498817c3c`, production candidate
`9413558a42b39f8f2a9ce41a717a0b37c9f72a98`, and the accepted definition hashes
above. A new definition, architecture decision, migration, Story 12.4 contract
change or scope expansion is not authorized.

## Preserved controls

This approval does not waive honest product evidence, authorization,
currentness, Source Anchors, citation validation, evidence-packet validation,
mandatory streaming, database isolation, secret protection, independent review,
E2E or final verification. It does not permit destructive Git recovery,
credential disclosure, fake Backup B evidence, CGC or the historical
`/scan-runs` path, an EF migration, `VulgataDbContext`/snapshot changes, or
mutation of retained databases and unrelated Repository checkouts.

The tracked `.env.example` modification observed at approval time is explicitly
user-owned, read-only runtime input. It is outside the workflow write/staging
manifest and must remain unstaged and uncommitted.

Hard blockers still fail closed: divergent history, inseparable ownership,
unavailable required infrastructure, provider refusal, unsafe external action,
security/Constitution contradiction or evidence that would require fabrication.
Technical gates, not the absence of another approval pause, determine whether
Story 12.5 may be marked complete.
