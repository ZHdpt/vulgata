# Story 12.5 Track C-MVP release report — run 03

## Disposition

**running**

Run 03 is the only prospective release lane. Runs 01 and 02 and their retained
databases remain immutable historical blocked evidence. This report cannot say
`passed` until every retained Scan, real streaming Chat, restart, screenshot,
E2E and final-verification threshold accepts the same candidate.

Preflight began at `2026-08-30T14:04:55.3127121Z` on branch
`demo0831/story-12-5-trackC`.  The active integration checkpoint is
`1c1631c4` plus the pending eighth narrowly observed PostgreSQL retry-strategy correction;
the final candidate remains pending until run-03 and its technical gates
finish. The accepted Story 12.4 dependency is
`4798b97d9f23aef587829175df7ab7ebc3daf995`.

## Lightweight preflight

| Check | Sanitized identity/result | Disposition |
| --- | --- | --- |
| Git ownership | expected branch, clean worktree/index before run-03 evidence mutation | ready |
| Repository `mbs_account` | exact clean committed snapshot `19c60cd3f3976b7697c487c7bafd96142e3a7625`; an unrelated dirty duplicate checkout is excluded | ready |
| Repository `mbs_channel` | exact clean committed snapshot `47f6b627a7598f862873610413eec1e1e7bf46bf` | ready |
| Docker/PostgreSQL | healthy PostgreSQL `17.10`; role `vulgata` has database-create authority | ready |
| Owned database | compiled harness generated and accepted final lane `vulgata_story125_b527027ad0dc4e359070f26498817c3c`; absent before T035 and owned by `vulgata` | ready |
| CodeGraph | `@colbymchenry/codegraph@1.5.0`; accepted component hashes qualified | ready |
| Java/JDT LS | Java `21.0.12` is available for the configured run; JDT LS `jdt-language-server-1.61.0-202608141332` archive SHA-256 `bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f` | ready |
| Provider | `https://opencode.ai/zen/go/v1/chat/completions`; model `deepseek-v4-flash`; credential present through `VULGATA_TEST_KEY` | ready |
| Provider route | source-free one-token probe returned HTTP `200`, `application/json`; response content was not retained | ready |
| Authentication/bootstrap | production first-user registration created one Administrator; an independent fresh login returned authenticated HTTP `200` | ready |
| Browser/manual screenshots | local in-app browser and manual PNG capture capability available | ready |

The default host Java command is not used as release authority. The run will
bind the explicitly available Java 21.0.12 runtime required by the accepted
Prepared Scan configuration.

## Command and exit table

| Order | UTC time | Sanitized action | Exit/result | Disposition |
| ---: | --- | --- | --- | --- |
| 1 | 2026-08-30 13:59 | Re-observe branch, HEAD, Git common directory, worktree, index and porcelain state | exit 0; expected and clean | completed |
| 2 | 2026-08-30 14:00 | Bind user approval `story-12-5-run-03-task-group-approval-01` to T034-T035 only | checkpoint `1844817f...`; workflow valid | completed |
| 3 | 2026-08-30 14:01 | Locate exact committed Repository snapshots without recording host paths | both accepted revisions; clean pair selected | completed |
| 4 | 2026-08-30 14:02 | Inspect local runtime and PostgreSQL identities/capability | CodeGraph 1.5; PostgreSQL 17.10; create authority true | completed |
| 5 | 2026-08-30 14:02 | Verify accepted JDT LS archive and available Java 21 runtime | identities/hashes match | completed |
| 6 | 2026-08-30 14:03 | Send one source-free, one-token readiness request to the configured provider | HTTP 200 JSON; response not retained | completed |
| 7 | 2026-08-30 14:04 | Run CodeGraph/database-guard/first-user bootstrap qualifications | 18 passed, 0 skipped, 0 failed | completed |
| 8 | 2026-08-30 14:04 | Invoke compiled Story 12.5 run-identity generator and ownership guard | one new lowercase owned name accepted | completed |
| 9 | 2026-08-30 14:07 | Create the first guarded attempt and start the production host | migrations and role seeding succeeded; port 5125 was rejected by Windows | environment retry |
| 10 | 2026-08-30 14:10 | Restart the same attempt on free loopback port 5187 and production-bootstrap the allowed rows | bootstrap rows were correct; post-bootstrap operator credential capture failed | attempt preserved, excluded |
| 11 | 2026-08-30 14:13 | Stop the verified owned host, generate a replacement name through the compiled ownership guard, and prove absence | `vulgata_story125_b527027ad0dc4e359070f26498817c3c`; absent before connection | completed |
| 12 | 2026-08-30 14:14 | Create only the replacement database with the approved PostgreSQL administrator mechanism | PostgreSQL 17.10; owner `vulgata`; prior attempt and retained databases preserved | completed |
| 13 | 2026-08-30 14:15 | Start Vulgata against only the replacement database with explicit Java 21 and a workspace-local key ring | migrations, role seed and HTTP startup succeeded | completed |
| 14 | 2026-08-30 14:15 | Register/authenticate the first user and create the System, encrypted Worker provider assignment and exactly two ordinary Repositories through production HTTP surfaces | all calls succeeded; operator credential protected locally with DPAPI and excluded from evidence | completed |
| 15 | 2026-08-30 14:16 | Query only the replacement database and perform an independent fresh login | exact allowed counts; every prohibited lifecycle/content count zero; login HTTP 200 | completed |
| 16 | 2026-08-30 14:17 | Stop the verified owned Vulgata host before T036 | exact process identity checked; host stopped | completed |
| 17 | 2026-08-30 14:25 | Start the guarded host and submit the corrected authenticated exactly-two-Repository Prepared Scan request | HTTP 201; two approved Manifests published | completed |
| 18 | 2026-08-30 14:36 | Repeat the identical Prepared Scan request | HTTP 200 replay; same Runs, executions, coordination and Manifests; zero duplicate rows | completed |
| 19 | 2026-08-30 16:10-18:17 | Exercise real Pass 1 and correct seven observed PostgreSQL retry-strategy transaction seams through public RED/GREEN tests | checkpoints `31327043`, `6a014a06`, `3ea71c30`, `6feb6284`, `3690f894`, `d699a323`, `1c1631c4`; workflow valid | completed |
| 20 | 2026-08-30 17:36 | Invoke authenticated production recovery for the retained pair | pair returned to `Running`; no direct database repair | completed |
| 21 | 2026-08-30 17:49-17:58 | Recover four expired leases, observe the durable navigation-reservation transaction rejection, and checkpoint its public RED/GREEN correction | exact attempts re-leased without duplicates; pair fail-closed to `PausedInspection`; guarded host stopped for rebuild | completed |
| 22 | 2026-08-30 18:10-18:17 | Resume real provider execution through the corrected navigation boundary, observe the Pass 1 document-publication transaction rejection, and checkpoint its public RED/GREEN correction | two tool attempts and one durable navigation receipt preceded the fail-closed pause; exact regression 1/1 and complete PostgreSQL atomicity class 71/71 passed; no partial Document graph | completed |
| 23 | 2026-08-30 18:22-21:01 | Complete the retained Pass 1 jobs, diagnose the terminal phase stall, and correct the sole reconciliation transaction through public RED/GREEN evidence | 148 Documents and two bounded ordinary failures were durable; exact regression 1/1, PostgreSQL atomicity 72/72, and production capacity 19/19 passed; no direct database repair | completed |

No command or evidence artifact records a credential, token, password,
connection string, provider response body, source content, absolute host path,
fake Backup B state or retained-database content.

## Combined host integration and real release run

T035 production bootstrap and the corrected Prepared Scan entrance are complete
against the final run-03 database. The
production host applied only the repository's existing migrations, seeded the
three existing roles, registered one Administrator, created System
`72438237-cb8e-4570-a3ba-fb28694832ab`, configured encrypted Worker provider
`2302894d-66ef-4fb7-9e10-920e615513e2`, assigned it to that System, and created
exactly these ordinary Repositories:

- `mbs_account`: `9a63d82c-f973-475f-b060-26f49c104cd3`, pinned revision
  `19c60cd3f3976b7697c487c7bafd96142e3a7625`;
- `mbs_channel`: `0c2f940f-4462-47eb-a016-44b2561dd3d9`, pinned revision
  `47f6b627a7598f862873610413eec1e1e7bf46bf`.

The authenticated Prepared Scan request created one exact initiation tuple and
two approved Manifests through the production CodeGraph 1.5/JDT-LS/safe-XML
path.  The identical replay returned HTTP 200 with the same durable identities
and zero duplicate rows.  The historical `/scan-runs` route and CGC were not
used.  The pair selected 150 File Code Units from `mbs_channel`; the
`mbs_account` Manifest honestly selected zero because that Repository contained
no eligible units under the accepted catalog path.

Real source-bearing Pass 1 has durably finished all 150 jobs: 148 current Code
Logic Documents were published and two jobs ended in the honest bounded
ordinary `JobTransient` category. The two failures remain within the five-job
budget. The pair is still `Running` only because the first terminal
reconciliation attempt reached the newly observed retry-strategy transaction
composition defect; correction 08 is GREEN and awaits restart/replay against
the same guarded database. Pass 2, Chat, restart and screenshots remain pending.

## Straightforward database query table

| Query predicate | Final run-03 value |
| --- | ---: |
| Administrator users / Administrator role bindings | 1 / 1 |
| Systems / encrypted providers / System provider assignments | 1 / 1 / 1 |
| Repositories (`mbs_account`, `mbs_channel`) | 2 |
| Scan Runs / Pre-Scan Results / Scan Executions | 2 / 2 / 2 |
| Two-Repository coordinations / Prepared Scan Manifests | 1 / 2 |
| Prepared Scan selected File Code Units | 150 |
| Pass 1 dispatched / high-value seed count | 150 / 0 |
| Pass 1 completed Documents / ordinary failed jobs | 148 / 2 |

The stored provider base identity is `https://opencode.ai/zen/go/v1`; the
encrypted API-key field is non-empty. No query printed or compared the secret.

## Manual twelve-question table

Pending. No corpus question or source-bearing provider request has run.

## Application-process restart

Pending.

## Manual screenshots and redaction

Pending. No historical, retained-run or fake Backup B screenshot is substituted.

## Failures and limitations

- The host default Java is newer than the accepted runtime; run 03 must
  explicitly bind the available Java 21.0.12 installation.
- A dirty duplicate `mbs_account` checkout exists and is excluded. The run is
  bound only to the clean exact committed snapshot and must not modify either
  checkout.
- Windows rejected the first loopback port after migrations and role seeding;
  the host succeeded unchanged on free loopback port 5187.
- First guarded attempt `vulgata_story125_547500647a4846ab97a9d9bd8b6c1b39`
  reached the correct bootstrap counts, but its operator session could not be
  resumed after a local credential-protection command failed. It is preserved
  unchanged as excluded failed-attempt evidence. No identity row was edited or
  repaired. The compiled guard generated the final replacement lane before any
  connection to it.
- Two real Pass 1 jobs ended in the bounded ordinary `JobTransient` category
  after bounded attempts.  Their provider content was not retained or
  published; run-03 keeps the failures as evidence and must remain within the
  accepted ordinary failure budget.
- Eight explicit PostgreSQL transaction boundaries used by the production run were not
  initially composed through EF's configured retry strategy: pair start,
  claim, failure recording, the eight-call navigation marker, recovery, and
  navigation reservation/reserved-receipt persistence, Document publication,
  and terminal reconciliation.
  Each was corrected only after an observed public-seam RED, with focused and
  surrounding PostgreSQL GREEN evidence and no schema or Scan-strategy change.

## Passing checklist

Not passed. T034-T035 and the real Prepared Scan/replay portion of T036 are
complete. All real Pass 1 jobs are terminal and correction 08 is ready for an
authenticated reconciliation replay; Pass 2, generated Business Logic
Documents, Chat, restart, screenshots, E2E and final verification remain
pending.
