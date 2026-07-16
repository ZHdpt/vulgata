---
description: "Qualification-only task graph for CodeGraphContext 0.5.1 HTTP JSON evidence"
---

# Tasks: CodeGraphContext 0.5.1 HTTP JSON Qualification

**Input**: The corrected specification accepted in
`specs/001-codegraph-extraction/reviews/spec-review-4.json`, the complete plan
set accepted in `specs/001-codegraph-extraction/reviews/plan-review-7.json`,
and the approved qualification correction in
`docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-15-http-json-qualification.md`.

## Workflow boundary

This executable graph covers only the qualification gate that must precede
Story 4.1 production work. It preserves the approved whole-Repository
`ScanRun` baseline and qualifies only CodeGraphContext `0.5.1` through its
loopback HTTP JSON candidate. Because no product User Story is implemented
before the qualification hard stop, every task is foundational and therefore
has no `[US#]` label.

This graph authorizes writes only under:

- `docker/Dockerfile` and `docker/codegraph/`;
- `tests/Vulgata.Tests/CodeGraphQualification/`; and
- `specs/001-codegraph-extraction/qualification/`.

It does not authorize a production provider adapter or transport, direct Kuzu
integration, Code Unit mapping or persistence, migrations, Git/API/UI/E2E
work, a Vulgata MCP or Worker Agent tool, Scan Coordinator work, Documents,
Uncertainty, or Cross-Repository Resolution.

Corrected FR-018 is binding throughout this graph: audit/progress groups are
metadata only and never provider input; qualification performs one
whole-Repository `cgc index` plus one projection under one 1,800-second
index-through-projection limit; and any cancellation, timeout, hang, crash,
unsuccessful exit, or unreadable response fails the provider-derived outcome
atomically. Because this graph stops before production Scan implementation, it
does not implement audit-group persistence or claim per-file progress,
deadlines, retry, bisection, failure isolation, or continuation.

## Execution rules

- Run the read-only `speckit-analyze` gate after this file is accepted and
  before T001 begins.
- Within each bounded group, observe the new test failing for the expected
  missing behavior before changing its implementation, then run the targeted
  and surrounding tests green.
- Use the exact 11-entry lifecycle: `cgc-version`, `cgc-doctor`,
  `cgc-context-create`, `cgc-context-mode`, `cgc-context-default`, `cgc-index`,
  `cgc-api-start`, `cgc-api-health`, `cgc-api-query`, `gateway-stop`, and
  `cgc-context-delete`.
- Use one aggregate `cgc-api-health` channel and one `cgc-api-query` channel.
  Apply one inclusive 65,536-byte raw budget to every provider stdout/stderr
  channel and both HTTP channels; byte 65,537 is counted and hashed once as the
  overflow sentinel before observation terminates.
- Parse the query response with strict incremental UTF-8 and streaming JSON.
  The inherited raw-response-file Python bridge, any whole-document bridge,
  and any conversion to synthetic pipe-separated `key=value` rows are
  explicitly rejected and must be removed. Do not use `ReadAsString`, retain
  raw response chunks, or parse CodeGraphContext's terminal table.
- Cleanup failure has precedence over every other result. A forced gateway
  kill makes the gate fail.
- T009 is the only task that may execute the replacement provider gate. It may
  run exactly once, only after Groups A-C and T007-T008 are green. There is no
  retry in this graph.

## Group A: Reproducible image, Context isolation, and exact lifecycle

**Independent validation**: Static image tests and synthetic lifecycle tests
prove the pinned non-root runtime, private named Context, no-network loopback
gateway, exact lifecycle, and cleanup behavior without executing the
replacement provider gate.

- [ ] T001 Add and run failing runtime/isolation/lifecycle contract tests in `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphImageQualificationTests.cs` and `tests/Vulgata.Tests/CodeGraphQualification/CgcCommandQualificationTests.cs`
  - **Depends on:** none
  - Require a digest-pinned CPython 3.12.x image, repository-owned Python lock,
    hash-verified dependency lock, exact `codegraphcontext==0.5.1` and Kuzu
    installation, and execution as non-root `app`.
  - Require one generated mode-0700 provider home and Kuzu directory, named
    mode/default Context selection, local config-to-Context isolation proof,
    `--network none`, `127.0.0.1` binding, no published port, and no call to
    bundled MCP/tool routes.
  - Require one whole-Repository provider operation, one 1,800-second
    index-through-projection limit, and fail-closed termination; reject any
    provider-input batching, per-file timer, retry, bisection, partial result,
    or sibling-continuation behavior.
  - Require exactly one occurrence of each approved lifecycle entry and reject
    duplicates, unapproved entries, the old CLI-table assumption, and the old
    prohibition against the qualification-only `cgc api start` surface.
  - Observe RED with:
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~Vulgata.Tests.CodeGraphQualification.CodeGraphImageQualificationTests`
    and
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~Vulgata.Tests.CodeGraphQualification.CgcCommandQualificationTests`.

- [ ] T002 Implement the pinned runtime, structural fixture, private Context lifecycle, and cleanup in `docker/Dockerfile`, `docker/codegraph/python-version.txt`, `docker/codegraph/requirements.lock`, `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphQualificationRunner.cs`, `tests/Vulgata.Tests/CodeGraphQualification/QualificationTestPaths.cs`, and `tests/Vulgata.Tests/CodeGraphQualification/Fixtures/`
  - **Depends on:** T001
  - Keep one whole-Repository index and provide fixture facts for `CALLS`,
    `CONTAINS`, `IMPORTS`, `INHERITS`, and one filtered-target candidate.
  - Apply one configurable qualification clock defaulting to 1,800 seconds
    from the start of `cgc-index` through completion of `cgc-api-query`; any
    cancellation, timeout, hang, crash, unsuccessful exit, or unreadable
    response must terminate the provider tree and yield one atomic failed gate.
  - Implement the exact 11-entry lifecycle, private Context proof, bounded
    gateway stop (`SIGTERM`, five-second grace, conditional `SIGKILL`), and
    cleanup of the Context, Kuzu directory, provider home, and named container.
  - Do not add a production adapter or Vulgata endpoint.
  - Reach GREEN by rerunning both T001 commands and the surrounding
    `CodeGraphQualification` tests that do not execute the replacement gate.

## Group B: Streaming HTTP observer and strict JSON candidate grammar

**Independent validation**: Synthetic byte streams prove deterministic
logical-channel accounting and fail-closed parsing without a provider run or
retaining provider values.

- [ ] T003 Add and run failing streaming-observer tests in `tests/Vulgata.Tests/CodeGraphQualification/CgcStreamingEvidenceObserverTests.cs`
  - **Depends on:** T002
  - Require the aggregate health and query logical channels, the shared
    inclusive byte budget, per-channel byte counts/hashes, byte conservation,
    the overflow sentinel, and the exact schema-1.2 canonical-summary field
    order.
  - Require strict incremental UTF-8 and one streaming JSON object with no
    trailing token; fail on replacement decoding, invalid JSON, unexpected
    envelope shape, duplicate/missing/unknown/nested/source-bearing aliases,
    wrong types, records over 4,096 raw bytes, or cross-record correlation.
  - Add regression assertions that fail while
    `CodeGraphQualificationRunner.cs` still writes an HTTP response to a raw
    file, invokes a Python/whole-document bridge, emits synthetic `key=value`
    rows, or feeds the observer a rendered CLI table.
  - Observe RED with
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~Vulgata.Tests.CodeGraphQualification.CgcStreamingEvidenceObserverTests`.

- [ ] T004 Replace the inherited bridge with bounded streaming HTTP observation in `tests/Vulgata.Tests/CodeGraphQualification/CgcStreamingEvidenceObserver.cs` and `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphQualificationRunner.cs`
  - **Depends on:** T003
  - Stream, frame, hash, and discard every provider stdout/stderr stream plus
    all readiness bodies in attempt order and the single query-response body.
  - Validate the exact HTTP envelope from `plan.md`: HTTP success,
    `status: "ok"`, null/absent `error`, `data.success: true`, exact query echo,
    non-negative `record_count`, and an equally sized `results` array.
  - Validate only the twelve approved scalar aliases and the
    relationship-specific rules for `CALLS`, `CONTAINS`, `IMPORTS`,
    `INHERITS`, positive ordered ranges, non-empty endpoint identities, and the
    one allowed filtered-target candidate. Discard all transient values as soon
    as their predicates are updated.
  - Remove the raw-response-file Python bridge, whole-document parsing, and
    synthetic `key=value` conversion rather than adapting them.
  - Reach GREEN with the T003 command and the non-provider-executing
    `CgcCommandQualificationTests` suite.

## Group C: Closed schema-1.2 evidence and terminal precedence

**Independent validation**: Writer/schema tests prove that only approved
redacted evidence can be persisted and that cleanup failure always wins,
without changing the historical qualification record or running the provider.

- [ ] T005 Add and run failing schema-1.2 and classification tests in `tests/Vulgata.Tests/CodeGraphQualification/QualificationGateTests.cs`
  - **Depends on:** T004
  - Require version `1.2`, grammar ID
    `vulgata-cgc-http-json-v1,vulgata-cgc-streaming-evidence-v2`, a closed
    `structuralEvidence` object, the exact canonical summary, and only approved
    counts, hashes, lifecycle/relationship/record facts, predicates, and
    cleanup facts.
  - Reject paths, symbols, source, endpoints, ports, raw JSON, raw query,
    arbitrary provider properties, raw-output retention, and the rejected
    schema-1.1 current-write shape. Preserve schema-1.0 historical-failure
    readability only.
  - Require `cleanup-failure / corrective-planning` to override every other
    classification; otherwise route pass to later parser/integration planning
    and fail to corrective planning.
  - Observe RED with
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~Vulgata.Tests.CodeGraphQualification.QualificationGateTests`.

- [ ] T006 Implement the closed schema-1.2 writer and validator in `tests/Vulgata.Tests/CodeGraphQualification/QualificationResultWriter.cs` and `specs/001-codegraph-extraction/qualification/qualification-result.schema.json`
  - **Depends on:** T005
  - Derive `canonicalSummarySha256` from the exact serialization in `plan.md`,
    enforce the redacted allow-list, verify total-byte conservation, and encode
    cleanup precedence plus the two terminal next-action values.
  - Retain schema-1.0 input compatibility only for historical failure
    evidence; do not fabricate or overwrite the durable provider result before
    T009.
  - Reach GREEN with the T005 command and the T003 command.

## Group D: One replacement provider gate and qualification hard stop

**Independent validation**: Synthetic gate tests first prove one-shot and
fail-closed orchestration. Only then one real replacement execution records a
schema-1.2 redacted result and terminates at the qualification boundary.

- [ ] T007 Add and run failing one-shot replacement-gate tests in `tests/Vulgata.Tests/CodeGraphQualification/CgcCommandQualificationTests.cs` and `tests/Vulgata.Tests/CodeGraphQualification/QualificationGateTests.cs`
  - **Depends on:** T006
  - Require the exact static request body from `plan.md`, readiness calls only
    to `GET /health`, one aggregate health channel, exactly one subsequent
    `POST /api/v1/query`, and no query retry or bundled MCP/tool-route call.
  - Require exactly one of every approved lifecycle entry and fail on cleanup,
    forced kill, overflow, byte mismatch, Context-isolation failure, missing
    structural predicate, duplicate/unapproved lifecycle activity, or attempt
    to execute before all prerequisite harness groups are green.
  - Require provider failure to produce no partial structural success and no
    retry, bisection, per-file continuation, or second index/query operation.
  - Observe RED by rerunning the T001, T003, and T005 targeted commands; the
    tests themselves must use controlled synthetic processes/streams and must
    not execute CodeGraphContext.

- [ ] T008 Implement the one-shot runner safeguards and final gate classification in `tests/Vulgata.Tests/CodeGraphQualification/CodeGraphQualificationRunner.cs`, `tests/Vulgata.Tests/CodeGraphQualification/CgcCommandQualificationTests.cs`, and `tests/Vulgata.Tests/CodeGraphQualification/QualificationResultWriter.cs`
  - **Depends on:** T007
  - Implement exact request/lifecycle accounting, aggregate readiness,
    one-query enforcement, cleanup precedence, byte conservation, and a guard
    that leaves provider execution disabled outside T009.
  - Enforce the single 1,800-second index-through-projection deadline and one
    terminal qualification classification without partial structural output.
  - Reach GREEN with the T001, T003, and T005 targeted commands plus
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~Vulgata.Tests.CodeGraphQualification.CgcCommandQualificationTests`.

- [ ] T009 Execute exactly one replacement provider qualification and write only redacted evidence to `specs/001-codegraph-extraction/qualification/qualification-result.json`, `specs/001-codegraph-extraction/qualification/evidence/red.md`, and `specs/001-codegraph-extraction/qualification/evidence/verification.md`
  - **Depends on:** T002, T004, T006, T008
  - Before execution, confirm all targeted prerequisite commands from T002,
    T004, T006, and T008 are green without a provider run and confirm the
    durable evidence has no previously recorded replacement attempt.
  - Execute the approved full qualification command exactly once:
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~Vulgata.Tests.CodeGraphQualification`.
  - Persist only schema-1.2 redacted evidence. Do not retain stdout/stderr,
    response bodies/chunks, JSON, query text, paths, symbols, endpoint values,
    source, credentials, tokens, or private Context/container values.
  - Whether the result passes or fails, do not rerun the command in this graph.

- [ ] T010 Verify the durable classification and stop at `specs/001-codegraph-extraction/qualification/qualification-result.json`, `specs/001-codegraph-extraction/qualification/evidence/red.md`, and `specs/001-codegraph-extraction/qualification/evidence/verification.md`
  - **Depends on:** T009
  - If the result passes, verify its next action is later reviewed
    parser/integration planning; do not enter production implementation.
  - If the result fails, verify its next action is corrective planning and that
    only bounded redacted evidence remains; do not retry.
  - Verify cleanup was attempted and succeeded, the terminal classification
    follows cleanup precedence, and exactly one replacement execution is
    recorded.

## Dependency graph

```text
T001 -> T002 -> T003 -> T004 -> T005 -> T006 -> T007 -> T008
          \--------------------+--------------------+-------> T009 -> T010
```

Exact dependencies:

- T001: none
- T002: T001
- T003: T002
- T004: T003
- T005: T004
- T006: T005
- T007: T006
- T008: T007
- T009: T002, T004, T006, T008
- T010: T009

No task is marked `[P]`: the bounded groups intentionally serialize writes to
the shared runner and evidence contract, and the provider gate is one-shot.

## Corrected FR-018 traceability

| Corrected behavior | Executable task evidence |
| --- | --- |
| Audit/progress groups never partition provider input or create provider operations. | T001 rejects batching/per-file behavior; T002 performs exactly one whole-Repository index; this qualification-only graph creates no production audit-group implementation. |
| One whole-Repository provider graph operation per Scan Run boundary. | T001-T002 prove one `cgc-index`; T007-T009 enforce one projection and exactly one real replacement execution. |
| One configurable whole-process limit defaults to 1,800 seconds from indexing through projection. | T001 observes RED for the missing limit; T002 implements it in the qualification runner; T007-T008 prove fail-closed enforcement before T009. |
| Cancellation, timeout, hang, crash, unsuccessful exit, or unreadable response fails provider-derived results atomically. | T001-T002 cover process termination/cleanup; T003-T008 cover unreadable/unsafe evidence and terminal precedence; T009-T010 persist one redacted terminal classification without retry. |
| Previously determined local filtering outcomes remain auditable. | Preserved as an accepted production requirement but deliberately not implemented here; the synthetic filtered-target fact only qualifies later mapping input, and the hard stop prevents an unreviewed persistence implementation. |

## Hard stop

This graph completes only at T010. A qualification pass is evidence for a new
reviewed production parser/integration plan; a qualification fail returns to
corrective planning. Neither outcome authorizes a second provider execution or
any Story 4.1 production implementation.
