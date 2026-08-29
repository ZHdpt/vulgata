# Story 12.5 Track C-MVP release report — run 01

## Disposition

**blocked**

Observed at `2026-08-29T17:15:07Z` for candidate
`65b4204b94c16310b8ada631865e39543b4b6f6c` on
`demo0831/story-12-5-trackC`. The accepted Story 12.4 dependency is
`4798b97d9f23aef587829175df7ab7ebc3daf995`.

The two exact Repository inputs and the pinned local analysis tools are ready.
Dependent product access stopped before database creation because the required
Story 12.5 PostgreSQL administrator authority, real-pair/source-binding opt-ins,
System/manifest bindings, configured streaming-provider endpoint/model, and
authenticated authorized-user readiness were not present. No provider request,
source disclosure, product database connection, migration, host start, Scan,
query, Chat request, restart, or screenshot capture occurred.

## Lightweight preflight

The candidate worktree, index, and owned branch were clean before this report
was written. The active root is the sole workflow-state, staging, integration,
and commit owner.

| Check | Expected | Observed | Result |
|---|---|---|---|
| Candidate | post-support checkpoint | `65b4204b94c16310b8ada631865e39543b4b6f6c` | ready |
| Story 12.4 dependency | accepted completion commit | `4798b97d9f23aef587829175df7ab7ebc3daf995` | ready |
| Repository `mbs_account` | exact clean Profiled Revision | `19c60cd3f3976b7697c487c7bafd96142e3a7625`, clean | ready |
| Repository `mbs_channel` | exact clean Profiled Revision | `47f6b627a7598f862873610413eec1e1e7bf46bf`, clean | ready |
| Docker/PostgreSQL | Docker engine and PostgreSQL 17 | Docker client/server `29.7.2`; `postgres:17-alpine` healthy | ready |
| CodeGraph | `@colbymchenry/codegraph@1.5.0` and approved Windows bundle | version `1.5.0`; approved component hashes `03e4c791…de03`, `b3094d0b…aa3`, `80419260…877` all matched | ready |
| Java | major 21 | `21.0.12` available at the accepted configured installation | ready |
| JDT LS | `jdt-language-server-1.61.0-202608141332` | distribution archive SHA-256 `bbb2297d…a0f`; launcher SHA-256 `9b3b1da6…34b7`; both present | ready |
| Streaming-provider credential | present without disclosure | present | ready |
| Streaming-provider identity | configured endpoint and model | neither endpoint nor model configured | blocked |
| Provider network route | safe readiness probe after configured identity | not attempted because identity and source-disclosure opt-in were absent | blocked |
| Real-pair/source authorization | explicit pair and source-binding opt-ins | both absent | blocked |
| Product authority bindings | System and two approved Prepared Scan Manifest identities | all absent | blocked |
| PostgreSQL administrator | approved Story 12.5 administrator mechanism | neither accepted administrator variable present | blocked |
| Authenticated authorized user | principal ready for exactly two authorized Repositories | not established before the owned database exists | blocked |
| Browser/manual screenshot capability | local interactive browser and PNG capture | capability available; no real application state eligible for capture | ready |

Reserved database identity:
`vulgata_story125_4042986b0c574829a5c713a00920b5dd`. It is lowercase,
matches the Story 12.5 owned-name guard, and is reserved only for this run.
It was **not created or contacted**. Intended owner/purpose: the Story 12.5
Track C-MVP integration root, for this one isolated combined release run.

The accepted Story 12.4 qualification recorded a prior non-secret provider
identity, but that historical observation is not substituted for current
Track C configuration or authorization.

## Command and exit table

Commands are described without credentials, connection strings, tokens, or
absolute host paths.

| Order | UTC time | Sanitized action | Exit/result | Disposition |
|---:|---|---|---|---|
| 1 | 2026-08-29 17:09 | Observe branch, candidate, common Git directory, index and porcelain state | exit 0; expected branch/candidate; clean | completed |
| 2 | 2026-08-29 17:10 | Verify both Repository `HEAD` values and porcelain state | exit 0; both exact and clean | completed |
| 3 | 2026-08-29 17:10 | Presence-only check for required database/tool/provider/authorization variables | exit 0; secret values never read or emitted | completed |
| 4 | 2026-08-29 17:11 | Read tool versions and verify approved CodeGraph/JDT component hashes | exit 0; identities matched | completed |
| 5 | 2026-08-29 17:12 | Docker client/server read-only version probe | exit 0 outside the restricted sandbox; `29.7.2`/`29.7.2` | completed |
| 6 | 2026-08-29 17:13 | Read-only running-container probe | exit 0; PostgreSQL 17 healthy | completed |
| 7 | 2026-08-29 17:15 | Apply external blocker protocol before any product connection | closed gate result `blocked` | completed |
| 8 | 2026-08-29 17:17 | Focused no-build support-test probe | exit 0; shared normal-output assembly was stale and contained no matching Track C tests | environment-only, unobserved |
| 9 | 2026-08-29 17:19 | Repository workflow-state validator | exit 0 after one bounded state-length correction; configuration and contracts valid | completed |

The initial sandboxed Docker probe could not access the local engine. A single
read-only approved host probe separated that environmental restriction from
actual service readiness; the unchanged product run was not retried.

The focused Track C support tests were already accepted at this exact candidate
from isolated-output runs (`18/18`, with a `93/93` surrounding slice) in
`evidence/verification/track-c-mvp-support-01.md`. The later no-build probe used
the stale shared normal-output assembly and therefore produced no executable
test observation; it does not replace or contradict the pinned support evidence.

## Combined host integration and real release run

Not run. The preflight blocker occurred before database creation and product
access. No real CodeGraph index, JDT LS Repository session, XML catalog parse,
File Code Unit selection, Pass 1, Cross-Repository Resolution, or Pass 2 result
is claimed by this report.

## Straightforward database query table

Not run. The reserved database was not created or contacted, so no row count,
Document, Source Anchor, REFERENCES Edge, GENERATED_FROM Edge, Uncertainty,
Cross-Repository Resolution, or Flow Group result is claimed.

## Manual twelve-question table

Not run. No provider call occurred. The accepted `q01`-`q12` corpus remains the
planned immutable evaluation input, but it has no Track C observation rows in
this blocked run. No answer, usefulness score, citation count, duration,
streaming observation, authorization oracle, or packet fingerprint is claimed.

Accepted Tier 1 evidence remains in
`evidence/verification/track-b-final-01.md` and
`evidence/e2e/track-b-run-01.md`; it is not represented as real-provider Track C
evidence.

## Application-process restart

Not run. Vulgata was never started against the reserved database, so no
persisted wiki/Chat access or duplicate-publication result is claimed.

## Manual screenshots and redaction

Not run. No eligible production state existed for the five required captures.
No fake Backup B or historical screenshot is substituted.

## Failures and limitations

- Required external configuration is missing: PostgreSQL administrator
  authority; configured streaming-provider endpoint/model; explicit real-pair
  and source-binding opt-ins; System and Prepared Scan Manifest bindings; and
  authenticated authorized-user readiness.
- Credential presence alone is insufficient authorization to disclose source or
  contact an unconfigured provider.
- Docker/PostgreSQL, both exact Repository revisions, CodeGraph, Java 21, JDT LS,
  local credential presence, and browser capability were independently ready.
- The correct recovery is to provide the missing approved, non-secret bindings
  and administrator authority in the local execution environment, then resume
  at T008 and re-run the complete preflight before T009. Do not reuse or infer
  Story 12.4 database credentials, create the reserved database through another
  mechanism, or replay an unchanged expensive Scan/provider run.

## Passing checklist

This report is not passed. Preflight/database ownership, real Scan/output
thresholds, twelve real streaming Chat cases, restart continuity, five real
screenshots, convergence, independent quality review, required E2E, pinned
verification, and completion remain unclaimed and incomplete.
