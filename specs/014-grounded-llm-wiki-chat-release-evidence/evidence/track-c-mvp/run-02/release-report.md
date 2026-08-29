# Story 12.5 Track C-MVP release report — run 02

## Disposition

**blocked**

Run 02 supersedes run 01 for prospective Track C-MVP execution while preserving
run 01 as an immutable truthful blocked observation. This report is not a pass
until every retained Scan, real streaming Chat, restart, screenshot, review,
E2E, and verification gate accepts.

Observed at `2026-08-29T23:16:52Z` for release candidate
`65b4204b94c16310b8ada631865e39543b4b6f6c`; evidence lineage is clean on
`demo0831/story-12-5-trackC` at
`9013faddb52b53c14db6e5bfefc8f79ce0157af7`. The accepted Story 12.4 dependency
is `4798b97d9f23aef587829175df7ab7ebc3daf995`.

## Lightweight preflight

| Check | Observed identity/result | Disposition |
|---|---|---|
| Git ownership | expected branch; clean index/worktree before evidence mutation | ready |
| Repository `mbs_account` | exact Profiled Revision `19c60cd3f3976b7697c487c7bafd96142e3a7625`; clean at T008, with later unrelated `.settings/org.eclipse.jdt.apt.core.prefs` worktree drift excluded by selecting the committed Git snapshot | bounded |
| Repository `mbs_channel` | exact clean Profiled Revision `47f6b627a7598f862873610413eec1e1e7bf46bf` | ready |
| Docker/PostgreSQL | PostgreSQL `17.10`; verified role `vulgata` has database-creation authority | ready |
| Owned database | `vulgata_story125_93fd44c9b3044fee84f2d032ac3af8a2`; owner `vulgata`; created from `template0` with UTF-8 after absence proof | ready |
| CodeGraph | `@colbymchenry/codegraph@1.5.0`; approved hashes `03e4c791…de03`, `b3094d0b…aa3`, `80419260…877` matched | ready |
| Java/JDT LS | Java `21.0.12`; `jdt-language-server-1.61.0-202608141332` archive SHA-256 `bbb2297d…a0f` matched | ready |
| Provider | `https://opencode.ai/zen/go/v1/chat/completions`; `deepseek-v4-flash`; credential present via `VULGATA_TEST_KEY` | ready |
| Provider route | one source-free, one-token request returned HTTP `200`, `application/json`; response body not read or persisted | ready |
| Real-pair/source authorization | standing authorization 02 bound to process-local opt-ins | ready |
| Authentication bootstrap | first-user Administrator, production registration/sign-in, and exactly-two-Repository request tests `3/3` passed | ready |
| Browser/manual screenshots | local interactive browser and PNG capture available | ready |

Owned database identity:
`vulgata_story125_93fd44c9b3044fee84f2d032ac3af8a2`.

The compiled Story 12.5 harness generated the name. Its preconnection factory
guard rejected `postgres` with zero factory calls, then accepted this owned name
with exactly one non-connecting marker call. T009 then proved the name absent,
created exactly this database through the approved PostgreSQL role, and read back
its owner/version from the administrator catalog. The authorized local Vulgata
host then applied only the repository's existing Identity/domain migrations and
seeded the three roles successfully. Intended owner/purpose: the Story 12.5 Track
C-MVP integration root, for this one combined release run.

The production UI/API created System
`43e0ee83-11dd-48d9-8a4d-449d5ee394f8`, Repository `mbs_account`
`acdb4c8f-6d38-43af-a5a6-2586e2661b8e`, and Repository `mbs_channel`
`bbc9a01d-f6be-4910-a49e-32efbf85374d`. No Prepared Scan Manifest GUID exists:
the production Scan start surface failed before creating Scan Runs.

## Command and exit table

| Order | UTC time | Sanitized action | Exit/result | Disposition |
|---:|---|---|---|---|
| 1 | 2026-08-29 23:12 | Re-observe branch, HEAD, Git common directory, index and porcelain state | exit 0; exact and clean | completed |
| 2 | 2026-08-29 23:13 | Resolve Vulgata API-key variable names without reading values | exit 0; `VULGATA_TEST_KEY` and legacy T062 key present | completed |
| 3 | 2026-08-29 23:14 | Source-free one-token request to supplied endpoint/model | HTTP 200 JSON; no response body retained | completed |
| 4 | 2026-08-29 23:14 | Read-only PostgreSQL administrator/version/capability query | exit 0; PostgreSQL 17.10; create authority true | completed |
| 5 | 2026-08-29 23:15 | Invoke compiled Story 12.5 identity and preconnection factory guard | system target rejected with zero calls; owned target accepted with one marker call | completed |
| 6 | 2026-08-29 23:16 | Revalidate exact Repository revisions, CodeGraph/Java/JDT hashes, provider/auth/browser bindings | exit 0; all ready | completed |
| 7 | 2026-08-29 23:21 | Prove guarded database absent, create only that database, and read back owner/version | exit 0; absent before; created once; owner `vulgata`; PostgreSQL 17.10 | completed |
| 8 | 2026-08-29 23:30 | Verify exact guarded target and start one controllable local Vulgata host | build completed; existing Identity/domain migrations and role seeding succeeded; HTTP host ready | completed |
| 9 | 2026-08-29 23:34 | Register the first user through the production Identity form and create System/provider configuration through authenticated APIs | Administrator authenticated; System and encrypted Worker provider configuration created | completed |
| 10 | 2026-08-29 23:38 | Select exact committed Git snapshots and create exactly two Repository records through the authenticated API | both exact Repository records created; unrelated source-checkout drift was not modified or included | completed |
| 11 | 2026-08-29 23:38 | Start production Repository Scan Runs for both selected Repositories | HTTP 503 for both; zero Scan Runs, coordinations, or manifests created | blocked |
| 12 | 2026-08-29 23:40 | Query bounded product counts and stop only the local Vulgata process | System 1; Repositories 2; Scan Runs 0; coordinations 0; manifests 0; graceful application shutdown | completed |

No evidence artifact retains a credential, token, password, connection string,
provider response body, source content, prompt corpus, absolute host path, or
unrelated state. Normal transient host logging included its local content root
and generated account identifier; neither is reproduced or linked as release
evidence.

## Combined host integration and real release run

T009 passed. T010 reached a real production-composition blocker after successful
host startup, migration, authentication, System/provider configuration, and
exactly-two-Repository creation. `Program.cs` registers
`RuntimeUnavailableRepositoryProfilingCoordinator` as the sole
`IRepositoryProfilingCoordinator`; both production `scan-runs` requests therefore
returned HTTP 503. The clean database has no Scan Runs or two-Repository
coordination, so Prepared Scan, Pass 1, exact Cross-Repository Resolution, and
Pass 2 cannot start honestly. No Scan or generated-output claim exists.

## Straightforward database query table

| Observation | Count |
|---|---:|
| Systems | 1 |
| Repositories | 2 |
| Scan Runs | 0 |
| two-Repository coordinations | 0 |
| Prepared Scan Manifests | 0 |

These are read-only counts from only the guarded Story 12.5 database. No row was
inserted, repaired, deleted, or semantically recomputed by the observation.

## Manual twelve-question table

Pending. No corpus question or source-bearing provider request has run.

## Application-process restart

Pending.

## Manual screenshots and redaction

Pending. No historical or fake Backup B screenshot is substituted.

## Failures and limitations

- Run 01 remains blocked historical evidence and is not edited.
- Run 02 preflight, database creation, host startup, migration, authentication,
  System/provider configuration, and exactly-two-Repository creation succeeded.
- T010 is blocked by current production composition, not by PostgreSQL, provider
  readiness, Git revision identity, CodeGraph/JDT identity, authentication, or
  authorization. Enabling `RepositoryProfilingCoordinator` would materially
  change the Scan composition and is outside the accepted Track C-MVP plan and
  explicit no-new-Scan-behavior boundary.
- The later unrelated `mbs_account` worktree preference-file modification was
  preserved. The API Repository binding targeted committed Git storage, not
  uncommitted bytes; no Scan reached source acquisition.
- The source-free provider probe establishes route/credential/model readiness;
  it is not a grounded answer and counts toward no release metric.

## Passing checklist

Not passed. T010-T022 and all retained technical gates remain incomplete. The
report cannot advance without an accepted upstream definition/change that owns
the missing production Scan composition; Story 12.4 data cannot substitute.
