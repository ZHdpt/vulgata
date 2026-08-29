# Story 12.5 Track C-MVP release report — run 02

## Disposition

**running**

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
| Repository `mbs_account` | exact clean Profiled Revision `19c60cd3f3976b7697c487c7bafd96142e3a7625` | ready |
| Repository `mbs_channel` | exact clean Profiled Revision `47f6b627a7598f862873610413eec1e1e7bf46bf` | ready |
| Docker/PostgreSQL | PostgreSQL `17.10`; verified role `vulgata` has database-creation authority | ready |
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
with exactly one non-connecting marker call. The database has not yet been
created or contacted as a product database. Intended owner/purpose: the Story
12.5 Track C-MVP integration root, for this one combined release run.

System, Repository, and Prepared Scan Manifest GUIDs are deliberately absent at
T008. T010 must capture their actual values from the existing authenticated
UI/API after T009 creates the database; none may be inferred or fabricated.

## Command and exit table

| Order | UTC time | Sanitized action | Exit/result | Disposition |
|---:|---|---|---|---|
| 1 | 2026-08-29 23:12 | Re-observe branch, HEAD, Git common directory, index and porcelain state | exit 0; exact and clean | completed |
| 2 | 2026-08-29 23:13 | Resolve Vulgata API-key variable names without reading values | exit 0; `VULGATA_TEST_KEY` and legacy T062 key present | completed |
| 3 | 2026-08-29 23:14 | Source-free one-token request to supplied endpoint/model | HTTP 200 JSON; no response body retained | completed |
| 4 | 2026-08-29 23:14 | Read-only PostgreSQL administrator/version/capability query | exit 0; PostgreSQL 17.10; create authority true | completed |
| 5 | 2026-08-29 23:15 | Invoke compiled Story 12.5 identity and preconnection factory guard | system target rejected with zero calls; owned target accepted with one marker call | completed |
| 6 | 2026-08-29 23:16 | Revalidate exact Repository revisions, CodeGraph/Java/JDT hashes, provider/auth/browser bindings | exit 0; all ready | completed |

No command emitted a credential, token, password, connection string, response
body, source content, prompt corpus, absolute host path, or unrelated state.

## Combined host integration and real release run

Pending T009 database creation and T010 execution. No Scan or generated-output
claim exists yet.

## Straightforward database query table

Pending. No product query has run.

## Manual twelve-question table

Pending. No corpus question or source-bearing provider request has run.

## Application-process restart

Pending.

## Manual screenshots and redaction

Pending. No historical or fake Backup B screenshot is substituted.

## Failures and limitations

- Run 01 remains blocked historical evidence and is not edited.
- Run 02 preflight is ready, but database creation and every dependent product
  observation remain pending.
- The source-free provider probe establishes route/credential/model readiness;
  it is not a grounded answer and counts toward no release metric.

## Passing checklist

Not yet passed. T009-T022 and all retained technical gates remain incomplete.
