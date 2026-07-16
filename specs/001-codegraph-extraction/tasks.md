---
description: "Corrective one-shot qualification graph for CodeGraphContext 0.5.1 HTTP JSON evidence"
---

# Tasks: Guarded CodeGraphContext attempt-0002

**Input**: Accepted `spec.md`/`spec-review-4.json`; the complete corrective plan set and
`reviews/plan-review-8.json`; the permanent incident record at
`qualification/evidence/accidental-provider-execution-2026-07-16.md`; and the explicit
`attempt-0002` approval recorded in `workflow-state.json`.

## Boundary and non-negotiable rules

This graph is qualification-only. It may write only `docker/Dockerfile`,
`docker/codegraph/`, `tests/Vulgata.Tests/CodeGraphQualification/`, and
`specs/001-codegraph-extraction/qualification/`. It does not authorize a production
adapter/transport, Code Unit mapping or persistence, migration, API/UI, Worker Agent or
agent tool, Scan Coordinator, Document, Uncertainty, or Cross-Repository Resolution.

Corrected **FR-018** remains binding: one atomic whole-Repository `cgc index` and one
projection, one configurable 1,800-second index-through-projection deadline, audit groups
never used as provider input, and atomic provider-derived failure with no per-file retry,
bisection, continuation, or partial structural success. The HTTP JSON grammar and schema
1.2 evidence are test-only qualification contracts. A pass returns to reviewed parser
planning; a fail returns to corrective planning.

`attempt-0001` is permanently `failed / execution-order-violation`. The approved
replacement is exactly `attempt-0002`; its claim is irreversible and it has no retry.
Before **any** test command, T001 must remove the legacy fixture path embedded in the stale
assembly. Every test invocation below compiles (no `--no-build`), uses an exact category
and/or `FullyQualifiedName=` identity, and never uses a namespace/class substring filter.

### Command reachability classes

- **R0 — provider-unreachable**: filesystem/hash/schema inspection, `dotnet build`, and
  tests categorized exactly `CodeGraphQualificationSynthetic`. R0 code uses only fake
  launchers and in-memory streams; it cannot construct the real launcher without a sealed
  `ProviderExecutionLease`. R0 must not execute or probe `docker`, `cgc`, `/health`, or the
  query endpoint.
- **R1 — provider-capable, single use**: only the exact T013 wrapper command and exact test
  identity categorized `CodeGraphQualificationProvider`. It must claim `started.json`
  durably before its first Docker operation.

## Group 0: Revoke the stale execution capability

**Independent validation**: the old absolute/relative fixture location is absent, so the
known stale assembly exits before image inspection, build, run, or provider access.

- [ ] T001 Move
  `tests/Vulgata.Tests/CodeGraphQualification/Fixtures/QualificationRepository/` to
  `tests/Vulgata.Tests/CodeGraphQualification/ProviderFixtures/Attempt0002Repository/`,
  update fixture manifest/path constants without running tests, and keep the legacy path
  absent for the remainder of the graph.
  - **Depends on:** none.
  - **Write boundary:** the two fixture directories,
    `Fixtures/qualification-fixture-manifest.json`, and `QualificationTestPaths.cs` only.
  - **Validation (R0):**
    `if (Test-Path 'tests/Vulgata.Tests/CodeGraphQualification/Fixtures/QualificationRepository') { throw 'legacy provider fixture path still exists' }`

## Group A: Capability split and sealed lease

**Independent validation**: exact synthetic tests prove stale/current ordinary test runs
are inert and real launch construction requires a valid sealed lease; no Docker/provider
operation is possible.

- [ ] T002 Add RED tests in
  `tests/Vulgata.Tests/CodeGraphQualification/ProviderExecutionGuardTests.cs` for exact
  categories, full-suite/no-token inertness, legacy-path absence, rejected broad/static
  authorization, fake-launch-only synthetic execution, and sealed
  `ProviderExecutionLease` construction.
  - **Depends on:** T001.
  - **RED (R0):**
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName=Vulgata.Tests.CodeGraphQualification.ProviderExecutionGuardTests.Issue_WhenNoClaimedAuthorization_RefusesProviderCapability&Category=CodeGraphQualificationSynthetic"`

- [ ] T003 Implement the capability boundary in
  `ProviderExecutionGuard.cs`, `ProviderExecutionLease.cs`,
  `IQualificationProcessLauncher.cs`, `FakeQualificationProcessLauncher.cs`, and
  `DockerQualificationProcessLauncher.cs`; categorize every synthetic test exactly
  `CodeGraphQualificationSynthetic` and make the real launcher accept only the sealed
  lease.
  - **Depends on:** T002.
  - Production-capable types must be inaccessible to ordinary/full-suite discovery and
    must not accept a boolean, static environment flag, or public constructor substitute.
  - **GREEN (R0):** rerun T002, then
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "Category=CodeGraphQualificationSynthetic"`.

## Group B: Accepted-plan-bound append-only attempt ledger

**Independent validation**: in-memory/temp-directory tests prove token, expiry, exact
command, approval, and accepted-plan identity checks; an atomic create-new start claim
always precedes launcher reachability; attempts cannot be reused or overwritten.

- [ ] T004 Add RED tests in `QualificationAttemptLedgerTests.cs` covering
  `authorized.json`, token SHA-256, expiry, exact command identity, corrective plan commit
  `84b0909b6713b410258d4db7ec37503cfe0c5980`, accepted
  `reviews/plan-review-8.json` identity, explicit workflow approval, atomic flushed
  `started.json`, single redacted `terminal.json`, existing/mismatched/expired state, and
  claim-before-launch ordering.
  - **Depends on:** T003.
  - **RED (R0):**
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName=Vulgata.Tests.CodeGraphQualification.QualificationAttemptLedgerTests.Claim_WhenAuthorizationMatches_CreatesAndFlushesStartBeforeReachabilityAsync&Category=CodeGraphQualificationSynthetic"`

- [ ] T005 Implement `QualificationAttemptLedger.cs`, closed ledger schemas, and the
  append-only `qualification/attempts/attempt-0001/started.json` and `terminal.json`
  incident import. Preserve the incident citation; never fabricate schema-1.2 HTTP
  evidence or rewrite/delete any attempt record.
  - **Depends on:** T004.
  - `attempt-0002/authorized.json` is not created by synthetic work. The one-shot wrapper
    may create it only from the recorded approval, with the random token retained solely
    in its child process.
  - **GREEN (R0):** rerun T004, then the exact synthetic-category command from T003.

## Group C: Streaming HTTP JSON observer

**Independent validation**: in-memory streams prove strict incremental UTF-8/JSON,
bounded/redacted logical-channel evidence, and fail-closed structural predicates without
provider reachability or retained provider values.

- [ ] T006 Replace obsolete tests with RED schema-1.2 HTTP cases in
  `CgcStreamingEvidenceObserverTests.cs`: aggregate health plus one query channel; inclusive
  65,536-byte cap and byte-65,537 sentinel; byte conservation and channel hashes; exact
  envelope/query echo; twelve scalar aliases; complete per-record `CALLS`, `CONTAINS`,
  `IMPORTS`, `INHERITS`, and filtered-target predicates; strict rejection of invalid UTF-8,
  trailing JSON, duplicate/missing/unknown/nested/source-bearing fields, wrong/null types,
  >4,096-byte records, and cross-record joins. Add regressions rejecting raw-response
  files, `ReadAsString`, Python/whole-document bridges, synthetic `key=value` rows, and CLI
  tables.
  - **Depends on:** T005.
  - **RED (R0):**
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName=Vulgata.Tests.CodeGraphQualification.CgcStreamingEvidenceObserverTests.Complete_WithChunkedHttpJson_ProducesOnlySchema12StructuralPredicates&Category=CodeGraphQualificationSynthetic"`

- [ ] T007 Implement bounded streaming in `CgcStreamingEvidenceObserver.cs` and the
  stream-facing seams of `CodeGraphQualificationRunner.cs`; hash/frame/discard transient
  bytes and remove, rather than adapt, every rejected bridge/table path.
  - **Depends on:** T006.
  - **GREEN (R0):** rerun T006, then the exact synthetic-category command from T003.

## Group D: Fake-launched lifecycle, writer, and terminal precedence

**Independent validation**: fake launchers prove the exact 11-entry lifecycle, Context
isolation, one query, cleanup precedence, atomic FR-018 failure, and closed schema-1.2
writing without Docker/provider access.

- [ ] T008 Add RED tests in `CgcCommandQualificationTests.cs` and
  `QualificationGateTests.cs` for the exact lifecycle; one private `HOME`/Kuzu Context;
  `--network none`, loopback/no published port; bounded health polling; exactly one query;
  no bundled MCP/tool route; one 1,800-second clock; process-tree termination; gateway
  SIGTERM/five-second grace/conditional SIGKILL; cleanup precedence; closed schema 1.2;
  canonical summary; and pass→parser-planning/fail→corrective-planning.
  - **Depends on:** T007.
  - **RED (R0):**
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName=Vulgata.Tests.CodeGraphQualification.CgcCommandQualificationTests.Run_WithFakeLauncher_UsesOneAtomicWholeRepositoryLifecycleAsync&Category=CodeGraphQualificationSynthetic"`

- [ ] T009 Implement fake-launchable orchestration in `CodeGraphQualificationRunner.cs`
  and the closed writer/validator in `QualificationResultWriter.cs` and
  `qualification/qualification-result.schema.json`. Preserve historical schema-1.0 failure
  readability, but never write a schema-1.1/current result and never pre-create the
  attempt-0002 qualification result.
  - **Depends on:** T008.
  - **GREEN (R0):** rerun T008, then the exact synthetic-category command from T003.

## Group E: Reachability-free preflight and exact provider entry point

**Independent validation**: synthetic tests prove the wrapper/entry point stays inert
without all capabilities; the preflight checks only repository inputs and fresh compiled
synthetic tests.

- [ ] T010 Add RED tests in `ProviderExecutionGuardTests.cs` and
  `QualificationPreflightTests.cs` for the exact provider identity
  `Vulgata.Tests.CodeGraphQualification.CgcProviderQualificationTests.Attempt0002_WhenAuthorizedAndUnclaimed_RecordsSchema12EvidenceAsync`, exact
  category `CodeGraphQualificationProvider`, recorded human approval, accepted-plan and
  command identities, uncommitted process token, irreversible claim, and a preflight whose
  executable allow-list excludes `docker`, `cgc`, HTTP, image inspection/build/pull, and
  container start.
  - **Depends on:** T009.
  - **RED (R0):**
    `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName=Vulgata.Tests.CodeGraphQualification.QualificationPreflightTests.Run_WithRepositoryInputs_UsesOnlyReachabilityFreeCommandsAsync&Category=CodeGraphQualificationSynthetic"`

- [ ] T011 Implement `CgcProviderQualificationTests.cs`,
  `Invoke-CodeGraphQualificationPreflight.ps1`, and
  `Invoke-CodeGraphQualificationProvider.ps1`. The provider test must be inert in the full
  suite and callable only by the wrapper. The wrapper verifies the workflow approval,
  accepted plan-review identity, exact command, expiry and token hash; creates
  `authorized.json` with create-new semantics; passes plaintext attempt ID/token only to
  its child; and permits the child to create+flush `started.json` before constructing the
  Docker launcher.
  - **Depends on:** T010.
  - **GREEN (R0):** rerun T010, then the exact synthetic-category command from T003.

- [ ] T012 Run the complete reachability-free preflight. It must prove legacy-path
  absence; source/fixture, Dockerfile, lock, schema, ledger and accepted-plan hashes; fresh
  compilation; the exact synthetic category; no existing `attempt-0002/started.json` or
  terminal; and the recorded user approval. Stop on any failure without creating an
  authorization/start record.
  - **Depends on:** T011.
  - **Command (R0):**
    `pwsh -NoProfile -File tests/Vulgata.Tests/CodeGraphQualification/Invoke-CodeGraphQualificationPreflight.ps1`

## Group F: Exactly one guarded provider observation and hard stop

**Independent validation**: one claimed attempt produces one redacted terminal record;
regardless of pass/fail, no second provider invocation or production work is permitted.

- [ ] T013 Execute exactly once:
  `pwsh -NoProfile -File tests/Vulgata.Tests/CodeGraphQualification/Invoke-CodeGraphQualificationProvider.ps1 -AttemptId attempt-0002`.
  - **Depends on:** T012 green and the explicit approval already recorded in
    `workflow-state.json`.
  - **Classification:** **R1**, the only provider-capable command in this graph. Its child
    command must use exact equality for the provider test identity and exact
    `CodeGraphQualificationProvider` category; no broad filter and no `--no-build`.
  - Before its first Docker operation, require a valid sealed lease and durably create+flush
    `qualification/attempts/attempt-0002/started.json`. Then perform at most one image
    build/run, one whole-Repository index, readiness polling only, and one query. After
    cleanup, write one redacted `terminal.json` and schema-1.2 result. Existing start or
    terminal state rejects execution. **Never retry T013.**

- [ ] T014 Enforce the terminal hard stop and classify the result without executing any
  command: pass → fresh production parser/integration plan and independent review; fail →
  corrective planning. Mark no Story 4.1 production task complete, and do not implement or
  test production Scan behavior in this graph.
  - **Depends on:** T013 terminal record, whether pass or fail.
  - **Provider reachability:** none; this is a review/state handoff only.

## Dependency graph and traceability

`T001 → T002 → T003 → T004 → T005 → T006 → T007 → T008 → T009 → T010 → T011 → T012 → T013 → T014`.

Groups A-E are strict RED→GREEN increments and remain R0. Only T013 is R1. No task is
parallel because the safety capability, ledger, observer, writer, wrapper, preflight, and
irreversible attempt form one ordered chain.

| Authority | Task proof |
| --- | --- |
| FR-003/FR-011 | T006-T009 qualify only bounded HTTP JSON evidence through Vulgata-owned test seams; no production parser claim. |
| FR-017 | All groups prohibit LLM, Vulgata MCP/Worker Agent tool, Coordinator, Document, Uncertainty, and Cross-Repository work. |
| Corrected FR-018 | T008-T013 enforce one whole-Repository operation, one 1,800-second deadline, atomic failure, and no batch/retry/bisection/continuation. |
| Incident and corrective plan | T001 revokes the stale capability; T004-T005 preserve attempt-0001; T010-T013 bind the approved attempt-0002 to a sealed lease and irreversible claim. |
| Production hard stop | T014 routes pass to fresh reviewed planning and fail to correction; neither route permits production implementation here. |
