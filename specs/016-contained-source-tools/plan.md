---
governed_delivery_classification: architecture
governed_delivery_reason: Adds Worker Agent list/read/search tools and fixes prepared-root authority, containment, continuation, and failure interfaces across Core, Infrastructure, and Agent Execution.
---

# Implementation Plan: Contained Prepared Source Tools

**Branch**: `016-contained-source-tools` (SpecKit feature label; native host checkout is detached) | **Date**: 2026-09-28 | **Spec**: [spec.md](spec.md)

**Input**: Story 18.1 and `spec.md`, governed by October O-FR01, O-FR04, O-NFR01. This is a plan candidate, not runtime or approval evidence.

## Summary

Expose one application-issued, batch and Repository bound prepared-source read context through small Worker Agent list, read, and literal-search interfaces. An injected current-authority verifier rechecks the approving subject and purpose throughout each operation. Infrastructure alone holds the physical directory and performs handle-relative, no-follow traversal. Each result carries Repository-relative locations and truthful completion or limitation; continuations stay bound to current authority as well as grant and request scope. Prepared labels do not become Profiled Revisions or Source Snapshots. Existing retained Git source capture and reads remain separate. See [research.md](research.md), [data-model.md](data-model.md), and [contracts/prepared-source-tools.md](contracts/prepared-source-tools.md).

## Technical Context

**Language/Version**: C# / .NET 10, per project files and architecture gateway.
**Primary Dependencies**: Existing Core contracts, injected current-authority verifier, Infrastructure filesystem adapter, Agents tool-function conventions and Microsoft.Extensions.AI/MAF packages in `Directory.Packages.props`; no new package assumed.
**Storage**: Operator-maintained live prepared directories; grant-lifetime in-memory cursors only. No database, copy, hash, Git acquisition, or retention.
**Testing**: xUnit in `tests/Vulgata.Tests`; controlled owned-directory tests through production interfaces and separate Git-bound regression.
**Target Platform**: Windows host execution for October. The broader application target includes Linux, but October does not require Docker/Linux qualification. Any enabled platform needs a handle-relative, no-follow adapter and platform-specific containment test; otherwise it fails closed.
**Project Type**: Existing .NET modular monolith; Core interface, Infrastructure adapter, Agents tool facade, trusted host composition.
**Performance Goals**: Finite per-call/grant work, deterministic bounded pages and no whole-tree preload; caps are safety limits, not throughput claims.
**Constraints**: Host root never enters model arguments or public results. Default grant ceilings: 100 operations, 1,000 metadata entries, 8 MiB bytes examined, 1 MiB returned, 30 seconds; per call at most 100 entries, 64 KiB returned text, 64 search matches and 1 MiB examined. Trusted issuer may lower but not raise caps.
**Scale/Scope**: One approved batch/Repository/root grant per context; recursive list and literal search traverse incrementally. No Scan scheduling, admission, UI, management CLI, CodeGraph, LSP, LLM, provider, or database work.

## Constitution Check

*GATE: checked before Phase 0; re-checked after Phase 1.*

| Principle / authority | Pre-research | Post-design |
| --- | --- | --- |
| I. Layered Project Boundaries; AD-2 | Pass: Core defines small read/current-authority interfaces; Infrastructure holds filesystem authority; Agents receive only an injected context. | Pass: trusted root issuance and current-authority adapter stay outside Agents, with no EF/process authority or DB dependency in direct fixtures. |
| II. Domain Purity & Ubiquitous Language; AD-3 and October prepared-origin exception | Pass: prepared label stays an operator label, separate from Profiled Revision and Source Snapshot. | Pass: no stored domain entity, snapshot, hash or Git identity; retained-source interface unchanged. |
| III. Validation & interface discipline | Pass: validate relative paths, ranges, scopes and limits. | Pass: contract fixes shape, status, ordering and no-host-path diagnostics; no HTTP/UI surface. |
| IV. Test-First Integration Harness | Pass: behavior requires honest RED/GREEN and owned-directory evidence. | Pass: quickstart specifies behavioral, adversarial, cancellation and Git regression tests without claiming them run. |
| V. Security & Data Stewardship; AD-6; O-NFR01; PRD NFR-2.3 | Pass: grant binding, live subject/purpose authorization, no-follow traversal, bounded work and sanitized disclosure are mandatory. | Pass at design: issuance and every operation recheck current approval; revocation/unavailability closes the context before disclosure. Windows containment/race and revocation tests remain implementation qualification; unqualified platform adapters fail closed. |

No Constitution exception is proposed. AD-3's Git snapshot rule remains binding for ordinary Scan source. October O-FR01 explicitly defines a separate prepared origin without Git identity; this plan does not change retained-source behavior. PRD FR-4.9 and NFR-2.3 source/tool safety remain applicable. See [research.md](research.md#authority-and-compatibility).

## Project Structure

### Documentation (this feature)

```text
specs/016-contained-source-tools/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/prepared-source-tools.md
```

### Source Code (planned areas, not current implementation)

```text
src/dotnet/Vulgata.Core/DomainServices/       injected prepared-source read/current-authority interfaces/results
src/dotnet/Vulgata.Infrastructure/Source/      trusted issuer, live-directory/authority adapters, platform traversal
src/dotnet/Vulgata.Agents/WorkerExecution/     model-visible list/read/search facade over injected context
src/dotnet/Vulgata.Web/                        trusted composition if needed for direct registration
tests/Vulgata.Tests/PreparedSource/             owned-directory contract and platform tests
tests/Vulgata.Tests/RetainedSource/             existing Git-bound regression selection
```

**Structure Decision**: Keep the read interface at the Core seam used by callers and tests. Infrastructure owns path resolution, handles and continuation state. Agent facade only translates closed model arguments to injected calls and projects sanitized results. No separate project or generic source abstraction. Existing `IRetainedSourceApplication`/`ISourceReadContext` remain Git-bound (`src/dotnet/Vulgata.Core/DomainServices/RetainedSourceContracts.cs:3-46`).

## Implementation Design

### Responsibilities

- **ID-R01**: Trusted application composition issues a prepared-source context only after approving batch, Repository, subject, Worker-source-read purpose, label and physical root. Its public Infrastructure issuer is `PreparedSourceContextIssuer(IPreparedSourceCurrentAuthorityVerifier)` with `IssueAsync(PreparedSourceGrantRequest, CancellationToken)` returning the sanitized issue result defined in [contracts/prepared-source-tools.md](contracts/prepared-source-tools.md); constructor rejects a missing verifier and issuance fails closed when approval is absent or unavailable. The issued context retains exact approval and opaque root-binding identities, while Agent Execution receives only `IPreparedSourceReadContext`. Story 18.1 supplies this verifier seam and DB-free direct invocation; later batch setup owns the production approval source, not a model selector. An always-allow production adapter is outside this design.
- **ID-R02**: Infrastructure owns one deep prepared-source module: relative-path validation, contained traversal, bounded enumeration/decoding/search, cursor state and sanitized outcomes sit behind a small list/read/search interface. Agent wrappers have no independent filesystem policy.
- **ID-R03**: Listing reports discoverable entries, including unfamiliar extensions and safely identifiable non-traversable links as limited metadata, without admission/support/exclusion decisions; Scan Control retains later work selection.

### Interfaces

- **ID-I01**: The trusted issuer accepts the existing Core `PreparedSourceGrantRequest` and returns an issue status plus either one `IPreparedSourceReadContext` on success or no context on failure; raw path/exception details are absent. The Core read interface then exposes `List`, `ReadTextRange` and `SearchText` on that context. Model arguments contain only canonical Repository-relative path, byte range or literal query, page size and opaque continuation. No batch, Repository, root, physical path, command or write argument can select authority. Public read results use the bound Repository ID and relative locations, never a physical path or Profiled Revision.
- **ID-I02**: Every response has one explicit outcome: complete, complete-empty, limited, incomplete, denied, authority-unavailable, missing, unreadable, unsupported-text, invalid-range, changed-input, cancelled or deadline-exceeded. It carries only currently authorized data and sanitized reason/limit. Revoked approval returns denied; verifier failure/unavailability returns authority-unavailable, with no entries, matches, text or continuation. A limited page has a same-scope continuation or explicit exhausted-grant limit; incomplete cannot masquerade as complete empty.
- **ID-I03**: Paths use normalized `/`-separated relative segments, excluding empty, `.`, `..`, drive/UNC/absolute forms, alternate streams and platform aliases. Read ranges use zero-based UTF-8 byte offset and positive byte count; returned text must be strictly decodable as a whole slice. Search is case-sensitive ordinal literal matching on strict UTF-8, with 1-based line/Unicode-scalar column and exact byte offsets. UTF-8 BOM is allowed at file start; other encodings and binary/invalid text are explicit unsupported-text.
- **ID-I04**: List/search order is ordinal by canonical relative path; matches within a file order by byte offset. Continuations are opaque, single-context, approval-binding/operation/query/path/limit bound, one-use in-memory capabilities, invalidated on revocation, verifier unavailability, disposal, cancellation, expiry or detected input change. Invalid/foreign tokens return denied without revealing their origin; a context latched closed for authority loss cannot resume with an old token.

### State and Lifetime

- **ID-S01**: One context lifetime binds batch ID, Repository ID, approved subject, Worker-source-read purpose, exact approval identity/epoch, opaque root-binding identity, label, held root identity, caps, deadline and cancellation. The injected verifier returns current authorization for that exact tuple or revoked/unavailable; a changed approval or root-binding identity is revocation for this context. Infrastructure alone retains root/handles. Revocation or verifier unavailability latches the context closed, invalidates every cursor and settles handles/streams; restored authority requires a new grant. Cumulative counters remain atomic. Label is descriptive, never version proof.
- **ID-S02**: Continuation stores bounded traversal position, request scope and observed file/directory identity/metadata needed to detect relevant changes; it is neither persisted snapshot nor tree hash. Complete means requested live traversal finished without an observed gap under operator's unchanged-input assumption, not that an exact historical tree was captured.

### Async and Cancellation

- **ID-A01**: Every operation links caller cancellation to context deadline/lifetime. It awaits the injected current-authority check at issuance and at operation start, between directory entries or bounded read/search chunks, and immediately before releasing any buffered result, including metadata, match counts, text and continuation. Revoked, changed or unavailable approval stops further reads and discards buffered output; concurrent calls share the closed latch. Cancellation/expiry similarly stops disclosure. Owned iterators, handles and streams settle in `finally`/async disposal.
- **ID-A02**: Reading/search stream bounded chunks rather than loading a full file/tree. Parallel calls reserve grant budgets atomically and cannot exceed caps through races; cancellation after partial work reports cancelled or deadline-exceeded, never complete, and no content is read after the stop point.

### Failure and Retry

- **ID-F01**: Deny malformed/foreign scope and any symlink or Windows reparse-point traversal, including components of the configured root. Establish the held root from a trusted volume/directory handle, then open each child relative to its held parent with no-follow semantics and reject reparse entries. Lexical `GetFullPath` plus check-then-open is not a containment proof. A platform without this capability fails closed.
- **ID-F02**: Revoked/changed approval maps to sanitized denied; verifier error/unavailability maps to sanitized authority-unavailable, never cached allow, complete-empty or partial disclosure. Missing, permission failure, unreadable/unsupported text, changed observed source identity, invalid range, work limit, deadline and cancellation retain their separate outcomes without raw exception/path/content diagnostics. Partial list/search encountering an unreadable source entry is incomplete with a relative limitation; complete-empty is reserved for fully inspected authorized scope.
- **ID-F03**: A denied outcome caused by revoked/changed authority, or authority-unavailable, closes the current grant; even restored approval needs a new approved grant. A malformed path/token denial applies to that request without granting access or necessarily closing an otherwise authorized context. Missing/unreadable/changed source input may be retried explicitly after the operator restores source. No automatic retry/fallback hides failure; continuation is never retargeted after authority or source change.

### Reuse and Locality

- **ID-L01**: Reuse result/cap/budget/cursor patterns from retained-source reads, but do not modify or wrap `ISourceReadContext`: its Profiled Revision, manifest hash and immutable bytes differ from live prepared input, and it lacks search. The new prepared module centralizes containment/truthfulness for all three calls, giving future Worker callers leverage and Infrastructure locality.
- **ID-L02**: Reuse Agent function construction conventions only for thin translation. Worker tool names and closed schema are fixed in [contracts/prepared-source-tools.md](contracts/prepared-source-tools.md); no fixed-pair Worker identity, Git hash, CodeGraph or LSP adapter is imported into this grant.

### Test Seams

- **ID-T01**: Public-seam tests construct `PreparedSourceContextIssuer` with a controllable in-memory `IPreparedSourceCurrentAuthorityVerifier`, call `IssueAsync` with the existing Core grant request over small owned directories, assert issue status/context invariants and then call the production Core read interface. Agent wrapper schema/invocation tests exercise the same per-disclosure verifier seam as a Worker without DB, model, CodeGraph or LSP.
- **ID-T02**: Platform owned-fixture tests exercise traversal, sibling/absolute/UNC/ADS aliases, internal and escaping symlink/reparse entries, replacement/retarget races and unsupported-containment failure. Assert no outside bytes/physical paths disclose and no outside content changes.
- **ID-T03**: Contract tests follow list/search continuations and read ranges through completion, including unfamiliar extensions, UTF-8 multibyte boundaries, CRLF, empty/missing/unreadable/binary/changed files, cancellation, deadlines, concurrent budgets and disposal. They revoke or make authority unavailable before a call, between chunks/pages and immediately before result release; assert no buffered metadata/content/continuation escapes, all concurrent calls settle and restoration needs a new grant. Selected existing retained-source read/capture tests form Git-bound regression boundary.

### Alternatives

- **ID-X01**: Extending `ISourceReadContext` was rejected: its Profiled Revision, manifest hash and immutable bytes would falsely assign Git/snapshot semantics to operator-maintained live input and move live traversal policy into an incompatible module.
- **ID-X02**: Lexical path normalization plus `File.GetAttributes` and `File.Open` was rejected as sole containment: link retargeting/parent replacement between check and open can expose outside content. Handle-relative no-follow traversal is required; non-traversable links sacrifice in-root convenience for a single safe policy.
- **ID-X03**: Eager whole-tree index/hash/copy was rejected because October O-FR01 excludes managed snapshots and repository-wide hash prerequisites and it would make bounded access depend on retention work.

### Consequences

- **ID-C01**: This adds a Worker Agent tool surface and shared prepared-root authority interface. Under `AGENTS.md` Planned-change authorization, independent plan-only review must accept this plan before root asks user to authorize tasks. Intent: contained read-only bounded tool with operator-approved root, no new Scan strategy or PRD change. Approval permits tasks only, not implementation.
- **ID-C02**: Prepared reads are truthful live observations, not reproducible Profiled Revision evidence. Operators keep directory unchanged/available; detected drift invalidates affected continuation and can require fresh operation. No Source Snapshot retention or Knowledge acceptance guarantee is created.
- **ID-C03**: Windows host containment needs owned-directory race qualification for this October story. Linux may be enabled only after equivalent qualification; otherwise it fails closed. A path-prefix fallback is outside accepted design.

## Planned Change Authorization Brief for Root

**Trigger**: `AGENTS.md` Planned-change authorization, first bullet: adds/materially changes a Worker Agent tool. This contract introduces list/read/search ([ID-I01](#interfaces), [contracts/prepared-source-tools.md](contracts/prepared-source-tools.md)); it adds no Scan strategy or PRD change. **Effects**: analysis can inspect an approved prepared tree with finite, location-rich responses while current batch/Repository/subject/purpose approval remains valid; application/Infrastructure own grant issuance, live revocation and platform containment; live source carries no revision proof. **Implementation intent**: after later gates, implement this plan while retaining normal Git-bound behavior and excluding acquisition, copy/hash/retention, DB, LLM, CodeGraph, LSP, UI and management CLI. The current-authority interface is DB-free-testable; production approval sourcing belongs to later integration. Affected requirements: `spec.md` FR-001–FR-011; October O-FR01/O-FR04/O-NFR01 (`docs/bmad/planning-artifacts/october-release-epics.md:157-160,204`); PRD FR-4.9/NFR-2.3 (`docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md:84,198`). Root obtains independent plan acceptance before explicit user authorization for tasks.

## Complexity Tracking

No Constitution violation or exception is proposed.
