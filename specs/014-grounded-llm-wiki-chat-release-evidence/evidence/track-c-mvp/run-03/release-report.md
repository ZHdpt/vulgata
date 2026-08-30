# Story 12.5 Track C-MVP release report — run 03

## Disposition

**running**

Run 03 is the only prospective release lane. Runs 01 and 02 and their retained
databases remain immutable historical blocked evidence. This report cannot say
`passed` until every retained Scan, real streaming Chat, restart, screenshot,
E2E and final-verification threshold accepts the same candidate.

Preflight observed at `2026-08-30T14:04:55.3127121Z` on branch
`demo0831/story-12-5-trackC`, clean integration HEAD
`1844817f56da1d2101c1bd045ea004caee15d0dd`, production candidate
`9413558a42b39f8f2a9ce41a717a0b37c9f72a98`, and accepted T033 checkpoint
`be8f51907b98335ec8ffc73a823aff27651cbe32`. The accepted Story 12.4
dependency is `4798b97d9f23aef587829175df7ab7ebc3daf995`.

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

No command or evidence artifact records a credential, token, password,
connection string, provider response body, source content, absolute host path,
fake Backup B state or retained-database content.

## Combined host integration and real release run

T035 production bootstrap is complete against the final run-03 database. The
production host applied only the repository's existing migrations, seeded the
three existing roles, registered one Administrator, created System
`72438237-cb8e-4570-a3ba-fb28694832ab`, configured encrypted Worker provider
`2302894d-66ef-4fb7-9e10-920e615513e2`, assigned it to that System, and created
exactly these ordinary Repositories:

- `mbs_account`: `9a63d82c-f973-475f-b060-26f49c104cd3`, pinned revision
  `19c60cd3f3976b7697c487c7bafd96142e3a7625`;
- `mbs_channel`: `0c2f940f-4462-47eb-a016-44b2561dd3d9`, pinned revision
  `47f6b627a7598f862873610413eec1e1e7bf46bf`.

No Prepared Scan request, historical `/scan-runs` request, Pass 1, Pass 2,
Cross-Repository Resolution, Chat or source-bearing provider request has run.
The verified owned host was stopped before T036.

## Straightforward database query table

| Query predicate | Final run-03 value |
| --- | ---: |
| Administrator users / Administrator role bindings | 1 / 1 |
| Systems / encrypted providers / System provider assignments | 1 / 1 / 1 |
| Repositories (`mbs_account`, `mbs_channel`) | 2 |
| Scan Runs / Pre-Scan Results / Scan Executions | 0 / 0 / 0 |
| Two-Repository coordinations / Prepared Scan Manifests | 0 / 0 |
| Code Units / Logical Documents / Document Versions | 0 / 0 / 0 |

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
- T036-T045 remain unauthorized. The final database is preserved and the host
  is stopped before the Prepared Scan request.

## Passing checklist

Not passed. T034 and T035 are complete. The final database and production
bootstrap are ready, while every Prepared Scan, generated-output, Chat,
restart, screenshot, E2E and final-verification requirement remains pending.
