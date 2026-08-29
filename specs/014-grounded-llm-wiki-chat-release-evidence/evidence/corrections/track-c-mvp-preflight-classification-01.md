# Story 12.5 Track C-MVP preflight classification correction 01

- Observed at: `2026-08-29T17:25:50Z`
- Branch: `demo0831/story-12-5-trackC`
- Correction base: `c2a50c790c1bb9567611f9e507ed2c039c6022ef`
- Release candidate under test: `65b4204b94c16310b8ada631865e39543b4b6f6c`
- Accepted Story 12.4 dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Scope SHA-256: `6e88b42958a615399a9b4167a1c9fc27f7229ea7cedcc7f4febedafd8c775400`
- Authorization ID: `story-12-5-track-c-mvp-overnight-standing-authorization-02`
- Classification: allowed evidence/operator-preflight correction; no product,
  specification, plan, contract, task intent, or implementation change

## Preserved original observation

The original blocked report and checkpoint remain immutable at:

- `evidence/track-c-mvp/run-01/release-report.md`
- `evidence/checkpoints/track-c-mvp-blocked-01.md`

They truthfully record what the first preflight classified at its observation
time. This correction supersedes only the blocker interpretation and recovery
route; it does not rewrite that historical record or convert it into success.

## Authority correction

The accepted `plan.md` AD-CMVP-3 run order is:

1. pin candidate, Repository Profiled Revisions, and tool/provider identities;
2. create the owned database;
3. start Vulgata, bootstrap/authenticate, and select the System plus exactly two
   Repositories;
4. prepare the Repositories and persist their Prepared Scan Manifests.

Therefore pre-existing System and Prepared Scan Manifest IDs are not T008
prerequisites. They are actual authenticated T010 outputs and must not be
inferred or fabricated before T009 creates the clean database.

The standing authorization explicitly mandates the exact real `mbs_account` /
`mbs_channel` pair, real source-anchored Pass 1/Pass 2, and a configured real
LLM. For this bounded run it is the real-pair/source-disclosure opt-in. The
qualification process set only these non-secret process-local switches:

- `VULGATA_T062_APPROVED_PAIR_REAL=1`
- `VULGATA_T062_SOURCE_BINDING_READY=1`

They were observed true inside the qualification process and were not persisted
to the user or machine environment. Future T009/T010 commands must bind them
process-locally from the same authorization; the user need not repeat approval.

## Authentication and exactly-two scope proof

Focused command:

    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter <first-user-admin-or-register-sign-in-or-exactly-two-request> --logger <minimal>

Result: `3 passed, 0 failed, 0 skipped`.

The verified existing seams prove:

- the first registered user in a clean database becomes Administrator;
- successful registration signs in and redirects through the production
  Identity path without requiring pre-existing user IDs;
- the Prepared Scan request requires exactly two Repository revision bindings.

This is bootstrap readiness only. T010 must still authenticate against the new
owned database and bind the actual created System, Repository, and Prepared Scan
Manifest identities from the existing UI/API.

## Provider identity, opt-in, and route

The accepted Story 12.4 completion evidence supplies the approved non-secret
current dependency identity `opencode.ai` / `deepseek-v4-flash`; the current API
credential is present without disclosure. A single source-free readiness request
sent only `Reply OK.`, model identity, and a one-token bound. It sent no Repository
content, prompt corpus, Document, Source Anchor, relationship, or credential
output.

The restricted-sandbox request was unobserved as an environment failure. The one
approved host retry reached the configured route but timed out after the bounded
20-second deadline (`TaskCanceledException`). No response body was read or
persisted, and the unchanged request was not repeated.

## Corrected blocker set

Only these prerequisites currently block a complete T008 pass and T009:

1. approved PostgreSQL administrator authority for the reserved Story 12.5
   database; neither accepted administrator variable is present;
2. live readiness of the accepted provider route; the source-free host probe
   timed out.

The following are **not** current blockers:

- System or Prepared Scan Manifest IDs before database creation;
- real-pair/source-disclosure approval;
- provider endpoint/model identity;
- credential presence;
- authentication bootstrap mechanism;
- exactly-two-Repository request structure;
- Repository roots/revisions, Docker/PostgreSQL 17, CodeGraph, Java 21, JDT LS,
  or browser/manual screenshot capability.

## Recovery

Supply the approved PostgreSQL administrator authority and restore the accepted
provider route. Then re-observe Git and prerequisites, bind the two opt-in
switches process-locally, and rerun the complete sanitized T008 preflight. Only
after it passes may T009 create the reserved owned database. T010 then performs
the existing first-user Administrator bootstrap, authenticated System/exactly-two
Repository selection, preparation, and actual System/manifest identity capture.
