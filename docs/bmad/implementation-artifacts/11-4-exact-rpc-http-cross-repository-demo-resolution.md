---
story_key: 11-4-exact-rpc-http-cross-repository-demo-resolution
title: "Story 11.4: Exact RPC/HTTP Cross-Repository Demo Resolution"
Status: in-progress
Epic: 11
Story: 4
created: 2026-08-03
execution_level: expedited-mvp
depends_on:
  - 11-1-minimal-knowledge-store-and-retrieval-projection
  - 11-2-sequential-scan-runner-and-typed-agent-dispatch
  - 11-3-pass-1-and-pass-2-demo-document-generation
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md
  - docs/agent-workflow/august-15-expedited-mvp-exception.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - specs/004-minimal-knowledge-store/plan.md
  - specs/005-sequential-scan-runner/plan.md
  - specs/006-demo-document-generation/plan.md
---

# Story 11.4: Exact RPC/HTTP Cross-Repository Demo Resolution

## Story

As a **SystemOwner**,
I want one configured custom-RPC communication path resolved across the two
prepared **Repositories**,
so that the August 15 demonstration traces a real business flow across a
Repository boundary without guessing graph truth.

## Scope and boundary

- Resolve one version-controlled, closed-allowlist custom-RPC configuration
  after Pass 1 results for both prepared Repositories are durably available.
- Bind configuration aliases at runtime to authorized Repository management
  records. Never hardcode database identifiers, working-copy roots, host paths,
  WSL paths, or environment-specific Git URLs in production code.
- Pin each prepared Repository to the exact Git object selected by the
  `vulgata/August15th-sample` tag and fail closed when the acquired **Profiled
  Revision** does not equal the configured full commit.
- Treat provider and consumer as roles observed for one communication
  occurrence. Either prepared Repository may play either role; do not add a
  durable provider-only or consumer-only Repository classification.
- Extract provider candidates only from configured XML files under
  `src/main/resources/disgram/in/**` and the configured `trans@name` field.
- Extract consumer candidates only from configured `netTools*` invocation
  forms with a statically exact literal endpoint. A symbol name, variable such
  as `transCode`, LLM interpretation, or System context alone is not exact
  evidence.
- Apply an explicit deterministic normalizer and ordinal exact comparison.
  Repository direction/target binding is part of the match because endpoint
  names can exist in both Repositories.
- Persist an exact cross-Repository `REFERENCES` Edge only when exactly one
  eligible provider matches. Preserve raw and normalized endpoint identifiers,
  protocol, configuration identity/version, confidence, evidence, and source
  anchors for both sides.
- Preserve unmatched, variable, malformed, revision-mismatched, or ambiguous
  evidence as a bounded warning or **Uncertainty** and create no false Edge.
- Record MQ, file/storage, page navigation, indirect object flow, Service
  Topology MCP, and general HTTP/RPC discovery as unsupported or deferred.
- Use current-boundary integration, migration, authorization, and source-
  isolation coverage. Story 11.5 retains the final clean-database two-
  Repository E2E and Docker-image release gate.

## Acceptance Criteria

### AC-1: Bind the prepared repositories and revisions exactly

**Given** a version-controlled demo configuration selects two Repository
aliases, expected refs, and full commits
**When** the resolver binds them to Repository management records within an
authorized System
**Then** each alias resolves to exactly one permitted Repository and one exact
**Profiled Revision**
**And** a missing, duplicate, inaccessible, or revision-mismatched binding
fails before source evidence is inspected or graph state is written.

### AC-2: Resolve one configured exact custom-RPC path

**Given** an allowlisted `netTools*` call contains a statically exact literal
endpoint and an eligible provider XML file contains a `trans@name`
**When** the configured normalization produces the same endpoint identifier
for the configured Repository direction
**Then** exactly one cross-Repository `REFERENCES` Edge is committed from the
consumer Document Version to the provider Document Version
**And** the commit preserves consumer and provider Source Anchors, raw and
normalized endpoint identifiers, protocol, configuration identity/version,
exact-match confidence, and bounded evidence.

### AC-3: Fail closed for ambiguity or incomplete evidence

**Given** an endpoint is variable, non-literal, outside an allowed invocation,
outside the configured XML root, malformed, unmatched, normalized differently,
or has zero or multiple eligible providers
**When** resolution runs
**Then** no `REFERENCES` Edge is created
**And** a bounded warning or **Uncertainty** preserves enough Repository-
relative evidence for diagnosis without source content or host paths.

### AC-4: Keep roles observational and bidirectional

**Given** an eligible configuration entry selects either prepared Repository
as consumer and the other as provider
**When** exact evidence matches
**Then** resolution may succeed in that configured direction
**And** neither Repository is globally classified as only a provider or only a
consumer.

### AC-5: Keep LLM and System context advisory

**Given** Pass 1 communication evidence or mutable System context mentions the
custom framework, likely target, or business meaning
**When** deterministic resolution evaluates it
**Then** that information may select or explain a configured candidate
**But** it cannot create an endpoint, role, target, confidence, or Edge without
the configured exact code/XML match
**And** System context changes during an active Scan retain the existing queued
application behavior.

### AC-6: Preserve authorization and source isolation

**Given** a caller lacks visibility to either participating System or
Repository
**When** resolution is invoked or its graph result is queried
**Then** no source, endpoint metadata, evidence, or cross-Repository graph data
is disclosed or persisted for that caller
**And** durable evidence uses Repository-relative locators and never plaintext
source, credentials, absolute host paths, or external-repository locations.

### AC-7: Defer unsupported communication kinds safely

**Given** observed communication is MQ, file/storage, page navigation,
indirect object flow, general HTTP/RPC discovery, or requires Service Topology
MCP
**When** the deadline resolver encounters it
**Then** it records an unsupported/deferred condition
**And** it does not attempt general inference or create a relationship.

## Prepared Repository facts

- `mbs_account` tag `vulgata/August15th-sample` resolves to
  `19c60cd3f3976b7697c487c7bafd96142e3a7625`.
- `mbs_channel` tag `vulgata/August15th-sample` resolves to
  `47f6b627a7598f862873610413eec1e1e7bf46bf`.
- Both tags are immutable planning/test inputs. Planning and implementation
  must read Git objects without checking out, modifying, staging, or committing
  either production Repository.
- Development host and Docker/WSL paths are deployment facts only. Runtime
  source acquisition uses Repository management plus deployment mounts or
  container-visible Git URLs; it does not compile these paths into Vulgata.
- The custom framework declares providers in XML beneath
  `src/main/resources/disgram/in/**`. The `trans@name` field is provider
  evidence only after configured normalization.
- `netTools*` symbols are candidate consumer evidence. Calls using variables
  are non-exact unless a separately configured deterministic source fact
  supplies a unique literal; LLM recognition does not satisfy this story.
- Field testing may use System context to explain the framework, but the
  reviewed version-controlled demo profile is the sole selector authority for
  the pinned literal/XML occurrence. System context cannot replace or change
  those exact source selectors.

## Architecture and security guardrails

- Reuse existing System/Repository management and Repository-scoped
  authorization as the runtime source of identity and access scope.
- Keep configuration version-controlled and schema-versioned. Store aliases,
  stable Git identity, expected ref/commit, evidence selectors, normalizer, and
  comparison policy; exclude environment paths and persistence GUIDs.
- Prefer a generic loader, validator, binder, candidate extractor, and resolver
  over Repository-name conditionals. The custom framework is data in the demo
  profile, not a hardcoded code branch.
- Preserve the exact **Profiled Revision** end to end. The current clone path
  profiles default `HEAD`; planning must define a narrow ref/object acquisition
  or equivalent verification change that cannot silently scan another commit.
- Do not use Pass 1 narrative or LLM output as match truth. Pass 1 may identify
  bounded candidate evidence; deterministic source/config validation owns the
  final relationship.
- Extend the persistence model narrowly enough to preserve endpoint identity,
  match evidence, confidence, configuration provenance, and both Source
  Anchors. Coordinate all EF migration and model-snapshot changes with the
  integration owner.
- Keep Core free of EF/provider dependencies, persistence in Infrastructure,
  and composition/authorization in Web. Do not modify predecessor workflow
  evidence or the external production Repositories.
- Preserve source isolation: provider execution and durable evidence must not
  expose source beyond the authorized organization boundary.

## Likely implementation seams for planning

- a schema-versioned prepared communication profile under a version-controlled
  Vulgata resource/configuration directory;
- Core contracts for exact candidate evidence, runtime Repository binding,
  match result, warning/**Uncertainty**, and persisted relationship metadata;
- Git acquisition/revision verification around
  `GitWorkingCopyService` and existing **Profiled Revision** handoffs;
- a deterministic resolver and persistence boundary in Infrastructure;
- `DocumentEdge`/related graph persistence plus EF configuration and migration;
- narrow Sequential Scan Runner integration after both prepared Pass 1 result
  sets are durable, without adding a general multi-Repository scheduler;
- Web composition and existing SystemOwner authorization scope;
- focused tests under `tests/Vulgata.Tests/`.

The accepted plan must refine these seams and explicitly reconcile the
canonical `communication_endpoints`/dispatch-site model with the existing
minimal knowledge store. It must not produce implementation tasks until the
user authorizes the triggered Scan-strategy change.

## Testing requirements

- Observe transient RED then GREEN for every included production behavior.
- Validate profile schema, duplicate aliases/rules, stable binding, exact refs,
  full commits, deterministic ordering, normalization, and ordinal comparison.
- Prove authorized exact matching in both directions and prove that Repository
  direction disambiguates endpoint names present in both Repositories.
- Prove revision mismatch, variable target, malformed XML, wrong root,
  unmatched endpoint, and multiple-provider ambiguity create no Edge.
- Prove idempotent retry cannot duplicate exact relationship truth or warnings.
- Prove persisted metadata retains endpoint/config/evidence/confidence/anchors
  while excluding source content, credentials, and absolute paths.
- Cover System/Repository authorization before source access and persistence.
- Run migration apply/rollback/reapply and no-pending-model-change checks for
  the new persistence model.
- Run focused Story 11.4 tests, Web build, and the full non-final-image
  regression lane. Story 11.5 owns final clean-database two-Repository E2E and
  Docker verification.

## Tasks / Subtasks

- [x] Generate tasks only after independent plan-review acceptance and explicit
      planned-change authorization.

## References

- Story authority: [Source: `docs/bmad/epics.md#Story-114-Exact-RPCHTTP-Cross-Repository-Demo-Resolution`]
- Demo PRD slice: [Source: `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md#510-August-15-Demonstration-Slice`]
- System context requirements: [Source: `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md#514-User-Supplied-Context`]
- Exact deadline boundary: [Source: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md`]
- Canonical Cross-Repository design: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md#11-Cross-Repository-Communication`]
- Persistence contracts: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md`]
- Expedited workflow: [Source: `docs/agent-workflow/august-15-expedited-mvp-exception.md`]
- Canonical terminology: [Source: `docs/UBIQUITOUS_LANGUAGE.md`]

## Dev Agent Record

### Context reference

- Preparation source commit:
  `67b5eaf45ff3c3348e240e323bed6f80c9e885e0`.
- Execution level: `expedited-mvp`.
- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/11-4-exact-rpc-http-cross-repository-demo-resolution.json`.
- Proposed SpecKit short name: `exact-cross-repo-resolution`.
- SpecKit feature directory: `specs/007-exact-cross-repo-resolution`.
- Current workflow stage: `plan`.

### Agent Model Used

- Root sprint-loop orchestrator with direct-child context synthesis,
  specification, planning, and independent plan review before authorization.

### Completion Notes List

- Context synthesis accepted the pinned two-Repository boundary.
- No business endpoint was invented. Read-only planning inspection pinned the
  exact literal/XML occurrence and selectors in the reviewed version-controlled
  demo profile from the two tagged Git objects.
- Plan review and planned-change authorization remain mandatory before tasks.

### File List

- `docs/bmad/implementation-artifacts/11-4-exact-rpc-http-cross-repository-demo-resolution.md`
- `docs/bmad/implementation-artifacts/sprint-status.yaml`
- `docs/bmad/implementation-artifacts/workflow-activations/11-4-exact-rpc-http-cross-repository-demo-resolution.json`
