---
description: "Qualification-only task list for CodeGraph Code Unit Extraction"
---

# Tasks: CodeGraph Code Unit Extraction — Provider Qualification Gate

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`,
and `contracts/repository-profiling-api.md` in
`specs/001-codegraph-extraction/`.

**Plan authority**: The accepted plan keeps one private local whole-Repository
`cgc` graph per `ScanRun`; at-most-1,000-file groups are Vulgata audit/filter
metadata only. There is no provider batch, per-file timer, continuation, retry,
or dispatch behavior. The only provider deadline is the configured 1,800-second
whole-process limit. This list does not alter that user-approved Scan strategy or
the corresponding PRD correction.

**Provider-pin correction**: The configured package index reports `0.5.1` as
the latest available CodeGraphContext release. The only executable provider
criterion in this task graph is `codegraphcontext==0.5.1`, with `cgc version`
reporting `0.5.1`. The unavailable earlier pin remains solely in historical
qualification evidence; no task may execute, assert, lock, or accept it.

**Authorization**: The user approved the whole-Repository `cgc` correction and
the exact latest-available provider correction for task generation. That
authorization permits only the qualification gate below. It does not authorize
structural mapping, persistence, migrations, Git working-copy implementation,
API, UI, E2E, LLM, MCP, Worker Agent, Scan Coordinator, or Cross-Repository
Resolution work.

**Hard boundary**: Every task stops at a deterministic provider qualification
result. A `pass` requires observed grammar evidence and a fresh `plan` amendment
plus independent plan review before any later implementation tasks may be
generated. A `fail` requires corrective planning. Neither result permits a
later implementer to continue into production profiling from this file.

**Tests**: Required. The Constitution's test-first integration rule and the
accepted plan make qualification tests the first executable increment. Update
the red tests for `0.5.1`, observe their expected failure against the current
shim/no-lock image, and capture only bounded synthetic evidence before Docker or
provider changes.

## Phase 1: Setup — deterministic, non-secret qualification fixture

**Purpose**: Preserve the valid synthetic fixture and correct the version-bound
schema before the red tests are regenerated. The fixture models a whole
Repository but contains no proprietary source, credentials, tokens, absolute
working-copy paths, or remote endpoint.

- [x] T001 Preserve the completed synthetic non-secret whole-Repository fixture and manifest in `tests/Vulgata.Tests/CodeGraphQualification/Fixtures/QualificationRepository/` and `tests/Vulgata.Tests/CodeGraphQualification/Fixtures/qualification-fixture-manifest.json`; it contains symbols and fixture relationships for `CALLS`, `IMPORTS`, `INHERITS`, and `CONTAINS`, plus one relationship to a path marked filtered. Its contents are independent of the provider pin and require no rewrite.
- [ ] T002 Update the qualification result contract in `specs/001-codegraph-extraction/qualification/qualification-result.schema.json` while preserving `specs/001-codegraph-extraction/qualification/evidence/.gitignore`; require image digest, CPython/package-lock identities, hash-verified exact `codegraphcontext==0.5.1`, non-root identity, redacted command/output hashes, elapsed duration, pass/fail category, and `nextAction` restricted to `return-to-plan` or `corrective-planning`.

**Checkpoint**: The fixture remains inspectable without business source, and the
result contract cannot express a production adapter, persistence result, or an
obsolete executable provider pin.

---

## Phase 2: Foundational — red qualification tests (blocking)

**Purpose**: Define the deterministic `0.5.1` pass/fail contract before
installing the provider. These tests must be observed red against the current
`codegraph` shim/no-lock image and recorded only as bounded synthetic evidence.

- [ ] T003 [P] Update and observe the failing container-lock/non-root test in `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphImageQualificationTests.cs`; require a digest-pinned Docker base, committed CPython 3.12.x lock, `--require-hashes`, hash-verified exact `codegraphcontext==0.5.1` and Kuzu distributions, no runtime resolver, final `app` user, writable private Kuzu directory, `cgc version == 0.5.1`, and `cgc doctor` exit code zero.
- [ ] T004 [P] Replace the Docker-daemon sentinel with an observed-red whole-Repository command/cleanup contract in `tests/Vulgata.Tests/CodeGraphQualification/CgcCommandQualificationTests.cs`; require one generated-private context create, one `index <fixture> --force`, static read-only query, and context delete using the candidate command surface under the 1,800-second whole-process limit. Require timeout, cancellation, non-zero exit, and unreadable output to terminate the process tree and remove the private context/database directory; forbid any per-file timeout, audit-group process, retry, remote backend, API, MCP, watcher, bundle, registry, datasource, or Neo4j command.
- [ ] T005 Update and observe the failing redaction/hard-stop test in `tests/Vulgata.Tests/CodeGraphQualification/QualificationGateTests.cs`; require a `0.5.1` schema-valid result to retain only bounded synthetic command/output hashes, reject source text, credentials, tokens, and absolute checkout paths, and assert that both `pass` and `fail` next actions prevent provider mapping, persistence, migration, Git/API/UI, E2E, or agent work until the required plan return.

**Checkpoint**: T003–T005 are red and demonstrate that the current image/shim
cannot be mistaken for the selected `cgc` provider. Do not alter `src/`, EF,
migrations, Git services, contracts, APIs, components, or the feature plan at
this checkpoint.

---

## Phase 3: User Story 1 — qualify the exact local provider (Priority: P1) 🎯 MVP

**Goal**: Prove, in a reproducible non-root container, whether the exact local
provider can index one synthetic whole Repository and expose enough bounded,
safe structural evidence to justify returning to planning.

**Independent Test**: The qualification tests run a Docker-built image against
only the synthetic fixture. A passing provider result emits a schema-valid,
redacted `qualification-result.json` with `nextAction: return-to-plan`; a valid
provider failure emits `nextAction: corrective-planning`. In either case the
gate creates no Vulgata domain record or public route.

### Implementation for User Story 1

- [ ] T006 [US1] Replace only the existing provider shim with a reproducible, digest-pinned non-root qualification image in `docker/Dockerfile`, `docker/codegraph/python-version.txt`, and `docker/codegraph/requirements.lock`; hash-lock exact `codegraphcontext==0.5.1`, Kuzu, and every transitive Python distribution, install with `--require-hashes`, prohibit runtime installation/resolution, and prohibit `cgc api`, `cgc mcp`, watcher, bundle, registry, datasource, Neo4j, or remote-database paths.
- [ ] T007 [US1] Implement the test-only qualification runner in `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphQualificationRunner.cs`; use `ProcessStartInfo.ArgumentList`, generated private paths, bounded stdout/stderr capture and redaction, one whole-process timeout defaulting to 1,800 seconds, process-tree termination, and `finally` cleanup. It may execute only `cgc version`, `cgc doctor`, `cgc context create`, `cgc index`, static read-only `cgc query`, and `cgc context delete`; it must neither partition provider input nor parse an undocumented provider result into a Vulgata contract.
- [ ] T008 [US1] Update deterministic result writing in `tests/Vulgata.Tests/CodeGraphQualification/QualificationResultWriter.cs`; emit only the `0.5.1` schema-valid pass/fail categories, exact lock/image/command/evidence hashes, duration, cleanup status, and restricted next action. On provider error, timeout, crash, non-zero exit, malformed/unbounded evidence, a missing relationship kind, or absent filtered-target endpoint evidence, emit `corrective-planning` and no partial success.
- [ ] T009 [US1] Run the qualification gate through `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphImageQualificationTests.cs`, `tests/Vulgata.Tests/CodeGraphQualification/CgcCommandQualificationTests.cs`, and `tests/Vulgata.Tests/CodeGraphQualification/QualificationGateTests.cs`; store only redacted synthetic evidence in `specs/001-codegraph-extraction/qualification/evidence/` and the schema-valid decision in `specs/001-codegraph-extraction/qualification/qualification-result.json`. A provider `pass` must prove the exact `cgc` version, image and lock hashes, non-root `doctor`, one context/index/query/delete sequence, cleanup, all four relationship kinds, a filtered-target endpoint, and a deterministic bounded output grammar for normalized path, kind, symbol, positive ordered range, relationship kind, and endpoint identity. Capturing that grammar is evidence only, never a production parser or `CodeGraphParseResult` implementation.
- [ ] T010 [US1] Execute the deterministic stop decision from `specs/001-codegraph-extraction/qualification/qualification-result.json`: on `pass`, return control to `specs/001-codegraph-extraction/plan.md` for the observed static queries, normalized safe grammar/snapshot, endpoint proof, image digest, and lock hashes followed by fresh independent plan review; on `fail`, return only the safe result/evidence identities for corrective planning. Do not create any further task or implementation artifact in either branch.

**Checkpoint**: This is the only deliverable authorized by the current task
list. A successful provider result is evidence to re-plan, not permission to
implement `ICodeGraphCliService`, `CodeGraphParseResult`, `ScanRun`, Code Unit,
Code Unit Edge, Unresolved Structural Relationship Candidate, EF
configuration/migration, working-copy service, API, or UI.

---

## Phase 4: Qualification closure — verify and stop

**Purpose**: Verify the gate itself, then stop in the correct workflow stage.

- [ ] T011 Verify the qualification-only boundary with `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~CodeGraphQualification --no-restore`, `docker build --file docker/Dockerfile .`, schema validation of `specs/001-codegraph-extraction/qualification/qualification-result.json` against `specs/001-codegraph-extraction/qualification/qualification-result.schema.json`, and `cgc version`/`cgc doctor` as the final non-root image user; record exact exit codes and redacted synthetic evidence hashes in `specs/001-codegraph-extraction/qualification/evidence/verification.md`.
- [ ] T012 Verify no unapproved production scope was changed by reviewing only `src/dotnet/`, `tests/Vulgata.Tests/CodeGraphQualification/`, `docker/`, and `specs/001-codegraph-extraction/qualification/`; append the explicit `PASS -> re-plan/review` or `FAIL -> corrective planning` stop decision to `specs/001-codegraph-extraction/qualification/evidence/verification.md`, without generating mapping/persistence/API/UI/E2E tasks.

---

## Dependencies & Execution Order

```text
T001 preserved fixture ─┬─> T003 image/non-root red test ─┐
                        └─> T004 command/cleanup red test ├─> T006 image + exact lock ─> T007 runner ─> T008 writer ─> T009 evidence decision ─> T010 hard stop ─> T011 verification ─> T012 return
T002 schema correction ─┬─> T003 image/non-root red test ─┤
                        └─> T005 redaction/hard-stop red ─┘
```

- T001 is preserved evidence; it is not rerun unless its fixture manifest
  changes.
- T002 must finish before T003 and T005 because their expected result contract
  has the corrected exact pin.
- T003, T004, and T005 may run in parallel after their dependencies and must
  all be red before T006.
- T006 → T007 → T008 → T009 is sequential. The locked image must exist before
  the runner; the runner before result writing; and result writing before a
  durable decision.
- T010–T012 are sequential stop controls. They never unlock structural work.

## Validation Commands and Decision Criteria

| Stage | Command or evidence | Required criterion |
| --- | --- | --- |
| Red baseline | `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~CodeGraphQualification --no-restore` | T003–T005 fail for the missing real locked provider contract, without invoking production work. |
| Locked image | `docker build --file docker/Dockerfile .` | Image builds from digest-pinned input; requirements install uses hash verification and contains exact `codegraphcontext==0.5.1`. |
| Provider gate | qualification test filter against the image and synthetic fixture | The only candidate commands are version, doctor, context create/delete, whole-Repository index, and static read-only query. The result is either fully proved `pass` or safe `fail`; neither is silently treated as a production success. |
| Result contract | `Test-Json -Path specs/001-codegraph-extraction/qualification/qualification-result.json -SchemaFile specs/001-codegraph-extraction/qualification/qualification-result.schema.json` | Schema-valid, redacted, bounded evidence with lock/image identities and the correct restricted next action. |
| Scope/format | `git diff --check -- specs/001-codegraph-extraction/tasks.md` and the T012 path review | No whitespace error and no source integration, persistence, Git/API/UI, E2E, agent, or Cross-Repository Resolution work. |

## Red/Green Evidence Plan

| Stage | Required evidence | Prohibited inference |
| --- | --- | --- |
| Red (T003–T005) | The current shim/no-lock image fails the exact `0.5.1` image, command/cleanup, and redaction/hard-stop contracts. | Treating a fake executable, an old failed pin, or public documentation as provider compatibility. |
| Green safety work (T006–T008) | A pinned non-root image and runner can deterministically write a bounded result for the qualified decision contract. | Assuming JSON format, graph-store schema, file batching, per-file timeout, or endpoint grammar that was not observed. |
| Qualification decision (T009) | Whole-fixture context/index/query/delete evidence, cleanup, output grammar observation, and a schema-valid pass/fail result. | Treating a valid `fail` result as provider qualification or treating `pass` as authorization for mapping/persistence. |
| Gate (T010–T012) | `pass` returns to fresh plan/review; `fail` returns to corrective planning. | Generating/running production mapping, persistence, migration, Git/API/UI, E2E, or agent work. |

## Coverage Map to the Accepted Plan

| Accepted-plan requirement | Tasks that prove it |
| --- | --- |
| Exact lock, digest-pinned image, CPython/distribution hashes, non-root `version`/`doctor` | T002, T003, T006, T009, T011 |
| Synthetic non-secret whole-Repository fixture and all structural kinds | T001, T004, T009 |
| Local Kuzu context/create/index/query/delete only | T004, T007, T009 |
| One whole-process timeout, cancellation/crash cleanup, atomic provider failure | T004, T007, T008, T009 |
| Bounded/redacted evidence and no remote/API/MCP activity | T005, T006, T007, T008, T011 |
| Deterministic output-grammar observation and fresh plan/review before mapping | T004, T008, T009, T010, T012 |

## Explicitly Not Generated

The following remain forbidden until a provider `pass` has been recorded, the
observed grammar has been added to a new plan, and that plan has passed fresh
independent review: provider-result mapping; structural Core entities/contracts;
EF configuration or migration; Git working-copy service; Repository details
action; API endpoints/DTOs/validators; production tests or E2E; Scan
Coordinator; Agent tools; LLM/MCP/Worker Agent work; Document, Uncertainty,
language/framework detection, or Cross-Repository Resolution.

## Implementation Strategy

1. Preserve T001, correct T002, then complete T003–T005 with observed red
   evidence for the exact available provider contract.
2. Complete T006–T009 to determine provider feasibility using only the
   synthetic fixture and local private context/database.
3. Execute T010. A pass returns to `plan`; a fail returns to corrective
   planning. This task file is not a backlog for the future feature.
4. Complete T011–T012, record the stop decision, and end implementation.

**Format validation**: Every executable item uses the required checkbox,
sequential task ID, optional parallel marker, user-story label where applicable,
and exact repository path format.
