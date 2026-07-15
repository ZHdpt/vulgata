---
description: "Qualification-only task list for CodeGraph Code Unit Extraction"
---

# Tasks: CodeGraph Code Unit Extraction — Provider Qualification Gate

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`,
and `contracts/repository-profiling-api.md` in
`specs/001-codegraph-extraction/`

**Authorization**: The user approved the whole-Repository `cgc` correction for
task generation on 2026-07-15. That authorization permits only the qualification
gate below. It does not authorize the structural mapping, persistence, migration,
Git, API, UI, E2E, LLM, MCP, Worker Agent, Scan Coordinator, or
Cross-Repository Resolution work described by the feature documents.

**Hard boundary**: Every task stops at a deterministic provider qualification
result. A `pass` result requires an observed grammar and a fresh `plan` amendment
plus independent plan review before any later implementation tasks may be
generated. A `fail` result requires corrective planning. Neither result permits a
later implementer to continue into production profiling from this file.

**Tests**: Required. The Constitution's test-first integration rule and the
accepted plan make qualification tests the first executable increment. Write the
red tests and capture their expected failure before Docker/provider changes.

## Phase 1: Setup — deterministic, non-secret qualification fixture

**Purpose**: Establish the only fixture and evidence locations allowed for this
gate. The fixture models a whole Repository but contains no proprietary source,
credentials, tokens, absolute working-copy paths, or remote endpoint.

- [ ] T001 Create the synthetic non-secret whole-Repository fixture and its manifest in `tests/Vulgata.Tests/CodeGraphQualification/Fixtures/QualificationRepository/` and `tests/Vulgata.Tests/CodeGraphQualification/Fixtures/qualification-fixture-manifest.json`; include symbols and fixture relationships for `CALLS`, `IMPORTS`, `INHERITS`, and `CONTAINS`, plus one relationship to a path marked filtered.
- [ ] T002 Create the qualification result schema and an ignored bounded-evidence directory in `specs/001-codegraph-extraction/qualification/qualification-result.schema.json` and `specs/001-codegraph-extraction/qualification/evidence/.gitignore`; require image digest, Python/package lock identities, non-root identity, redacted command/output hashes, elapsed duration, pass/fail category, and `nextAction` restricted to `return-to-plan` or `corrective-planning`.

**Checkpoint**: The fixture can be inspected without exposing business source,
and the result contract cannot express a production adapter or persistence result.

---

## Phase 2: Foundational — red qualification tests (blocking)

**Purpose**: Define the deterministic pass/fail contract before installing the
provider. All tests in this phase must be observed failing against the current
`codegraph` shim and recorded only as bounded synthetic evidence.

- [ ] T003 [P] Write a failing container-lock and non-root test in `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphImageQualificationTests.cs`; require a digest-pinned Docker base, locked CPython 3.12.x, hash-verified exact `codegraphcontext==0.5.0` and Kuzu distributions, no runtime resolver, final `app` user, writable private Kuzu directory, `cgc version == 0.5.0`, and `cgc doctor` exit code zero.
- [ ] T004 [P] Write a failing whole-Repository command-sequence and timeout/cancellation test in `tests/Vulgata.Tests/CodeGraphQualification/CgcCommandQualificationTests.cs`; require `context create`, one `index <fixture> --force`, a static read-only `query`, and `context delete` under the configured 1,800-second whole-process limit, with timeout/non-zero/cancellation killing the process tree and removing the private context/database directory.
- [ ] T005 Write a failing evidence-redaction and hard-stop test in `tests/Vulgata.Tests/CodeGraphQualification/QualificationGateTests.cs`; accept only bounded synthetic command/output evidence, reject source text, credentials, tokens, and absolute checkout paths, and assert that both pass and fail produce `nextAction` values that prevent mapping, persistence, migration, Git/API/UI, or E2E work.

**Checkpoint**: T003–T005 are red and demonstrate that the current image/shim is
not being mistaken for `cgc`. Do not alter `src/`, EF, migrations, Git services,
contracts, APIs, components, or the feature plan at this checkpoint.

---

## Phase 3: User Story 1 — qualify the pinned local provider (Priority: P1) 🎯 MVP

**Goal**: Prove, in a reproducible non-root container, whether the selected local
provider can index one synthetic whole Repository and expose enough bounded,
safe structural evidence to justify returning to planning.

**Independent Test**: The three qualification test files run a Docker-built
image against only the synthetic fixture. They either emit a valid redacted
`qualification-result.json` with `nextAction: return-to-plan`, or a valid fail
result with `nextAction: corrective-planning`; neither outcome creates Vulgata
domain records or a public route.

### Implementation for User Story 1

- [ ] T006 [US1] Replace only the existing provider shim with a reproducible, digest-pinned non-root qualification image in `docker/Dockerfile`, with locked CPython version in `docker/codegraph/python-version.txt` and hash-verified exact Python distributions in `docker/codegraph/requirements.lock`; prohibit `cgc api`, `cgc mcp`, watcher, bundle, registry, datasource, Neo4j, remote database, and runtime installation/resolution paths.
- [ ] T007 [US1] Implement the test-only qualification runner in `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphQualificationRunner.cs`; use `ProcessStartInfo.ArgumentList`, generated private paths, bounded stdout/stderr capture and redaction, one whole-process timeout defaulting to 1,800 seconds, process-tree termination, and `finally` cleanup. It may execute only `cgc version`, `cgc doctor`, `cgc context create`, `cgc index`, static read-only `cgc query`, and `cgc context delete`; it must not parse an undocumented provider grammar into a Vulgata contract.
- [ ] T008 [US1] Implement deterministic result writing in `tests/Vulgata.Tests/CodeGraphQualification/QualificationResultWriter.cs` and `specs/001-codegraph-extraction/qualification/qualification-result.schema.json`; record only schema-valid pass/fail categories, fixture/image/lock/command/evidence hashes, duration, cleanup status, and the restricted next action. On every provider error, timeout, crash, non-zero exit, malformed/unbounded evidence, missing relationship kind, or absent filtered-target endpoint evidence, write `corrective-planning` and no partial success.
- [ ] T009 [US1] Make T003–T005 green by running the qualification tests against the image and fixture, using `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphImageQualificationTests.cs`, `tests/Vulgata.Tests/CodeGraphQualification/CgcCommandQualificationTests.cs`, and `tests/Vulgata.Tests/CodeGraphQualification/QualificationGateTests.cs`; store only redacted synthetic evidence under `specs/001-codegraph-extraction/qualification/evidence/` and the schema-valid result at `specs/001-codegraph-extraction/qualification/qualification-result.json`.
- [ ] T010 [US1] Execute the deterministic gate decision from `specs/001-codegraph-extraction/qualification/qualification-result.json`: on `pass`, return control to `specs/001-codegraph-extraction/plan.md` for the observed static queries, normalized safe grammar/snapshot, endpoint proof, image digest, and lock hashes followed by a fresh independent plan review; on `fail`, return only the safe result/evidence identities for corrective planning. Do not create any further task or implementation artifact in either branch.

**Checkpoint**: This is the only deliverable authorized by the current task
list. A successful test run is evidence to re-plan, not permission to implement
`ICodeGraphCliService`, `CodeGraphParseResult`, `ScanRun`, Code Unit, Code Unit
Edge, Unresolved Structural Relationship Candidate, EF configuration/migration,
working-copy service, API, or UI.

---

## Phase 4: Qualification closure — verify and stop

**Purpose**: Verify the gate itself, then stop in the correct workflow stage.

- [ ] T011 Verify the qualification-only boundary with `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter CodeGraphQualification`, `docker build --file docker/Dockerfile .`, and schema validation of `specs/001-codegraph-extraction/qualification/qualification-result.json` against `specs/001-codegraph-extraction/qualification/qualification-result.schema.json`; record command exit codes and redacted synthetic evidence hashes in `specs/001-codegraph-extraction/qualification/evidence/verification.md`.
- [ ] T012 Verify no unapproved production scope was changed by reviewing only `src/dotnet/`, `tests/Vulgata.Tests/CodeGraphQualification/`, `docker/`, and `specs/001-codegraph-extraction/qualification/`; append the explicit `PASS -> re-plan/review` or `FAIL -> corrective planning` stop decision to `specs/001-codegraph-extraction/qualification/evidence/verification.md`, without generating mapping/persistence/API/UI/E2E tasks.

---

## Dependencies & Execution Order

```text
T001 fixture ─┬─> T003 image/non-root red test ─┐
              └─> T004 command/cancellation red ├─> T006 image locks ─> T007 runner ─> T008 writer ─> T009 green proof ─> T010 hard stop ─> T011 verification ─> T012 return
T002 result schema ──────────────────────────────┤
T005 redaction/hard-stop red test ───────────────┘
```

- **T001 and T002** may proceed in parallel because they own disjoint paths.
- **T003 and T004** may proceed in parallel after T001; **T005** may proceed
  after T002. They must all be red before T006.
- **T006 → T007 → T008 → T009** is sequential. The Docker/input contract must
  exist before the runner; the runner exists before results can be written;
  result writing exists before qualification can turn green.
- **T010–T012** are sequential stop controls. They do not unlock future work.

## Red/Green Evidence Plan

| Stage | Required evidence | Prohibited inference |
| --- | --- | --- |
| Red (T003–T005) | Current shim fails cgc version/doctor and cannot satisfy the required command/cleanup/result contracts. | Treating a fake executable or documentation as provider compatibility. |
| Green (T006–T009) | Pinned non-root image, whole-fixture context/index/query/delete proof, bounded redacted output, cleanup, and schema-valid decision. | Assuming JSON format, graph-store schema, file batching, per-file timeout, or endpoint grammar that was not observed. |
| Gate (T010–T012) | `pass` returns to plan with observed facts; `fail` returns to corrective planning. | Generating/running production mapping, persistence, migration, Git/API/UI, E2E, or agent work. |

## Coverage Map to the Accepted Plan

| Accepted-plan requirement | Tasks that prove it |
| --- | --- |
| Digest-pinned image, CPython/distribution locks, non-root `version`/`doctor` | T003, T006, T009, T011 |
| Synthetic non-secret whole-Repository fixture and all structural kinds | T001, T004, T009 |
| Local Kuzu context/create/index/query/delete only | T004, T007, T009 |
| One whole-process timeout, cancellation/crash cleanup, atomic provider failure | T004, T007, T008, T009 |
| Bounded/redacted evidence and no remote/API/MCP activity | T005, T006, T007, T008, T011 |
| Deterministic pass/fail and a fresh plan/review before mapping | T002, T005, T008, T010, T012 |

## Explicitly Not Generated

The following remain forbidden until a successful qualification has been
recorded, the observed grammar has been added to a new plan, and that plan has
passed fresh independent review: provider-result mapping; structural Core
entities/contracts; EF configuration or migration; Git working-copy service;
Repository details action; API endpoints/DTOs/validators; production tests or
E2E; Scan Coordinator; Agent tools; LLM/MCP/Worker Agent work; Document,
Uncertainty, language/framework detection, or Cross-Repository Resolution.

## Implementation Strategy

1. Complete T001–T005 and preserve the red failure evidence.
2. Complete T006–T009 to determine provider feasibility, using only the
   synthetic fixture and local private context/database.
3. Execute T010. A pass is a return to `plan`; a fail is a return to corrective
   planning. Do not use this task file as a backlog for the future feature.
4. Complete T011–T012, report the stop decision, and end implementation.

**Format validation**: All executable items above use the required checkbox,
sequential task ID, optional parallel marker, user-story label where applicable,
and exact file path format.
