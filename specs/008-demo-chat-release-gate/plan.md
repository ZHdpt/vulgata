# Implementation Plan: Demo Chat, Citations, and Release Gate

**Branch**: `codex/run-story-115-sprint-loop` | **Date**: 2026-08-07 | **Spec**: [spec.md](spec.md)

**Input**: Story 11.5 at source boundary
`c07df7d8789e7b975dc7fb6a121dff564e8549ad`, execution level
`expedited-mvp`, plus the user-approved
[runtime Pass 2 binding correction](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-07-story-11-5-runtime-pass2-binding.md)
and [ordinary Pass 1 Job Identity amendment](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-07-story-11-5-pass1-job-identity-amendment.md).

## Summary

Replace the authenticated Chinese chat placeholder with a transient,
prepared-scope Business/IT flow. The user explicitly selects one visible
System and exactly two visible Repositories. A Web coordinator verifies both
Repositories before calling one bounded, database-backed Chat evidence tool,
passes the finite authorized Evidence Packet to a Chat Agent, accepts only a
closed typed result whose every Material Claim maps to server-owned evidence,
validates the selected support against current PostgreSQL state, rechecks
authorization, and only then renders the complete Grounded Answer.
Business Mode keeps code locators out of the narrative and exposes them in
expandable Chinese Document citations; IT Mode may display authorized
Repository-relative locators directly.

After exact Cross-Repository Resolution, one bounded Infrastructure planner
binds the accepted profile and current persisted evidence to product-created
runtime identities before Pass 2. It fails closed unless every required
selector is unique, current, authorized, profile/ref/commit exact, and in the
accepted consumer-to-provider direction. The Worker dispatch remains the
existing exact seven-slot `Pass2BusinessLogicJobPayload`; the deterministic
support fingerprint and all other binding identities remain server-owned and
are validated by the runner and committer, not added as an eighth payload
slot.

New ordinary Pass 1 dispatches use a deterministic actual Worker `JobId`
derived by the server from the Scan execution, ScanRun, Repository, Code Unit,
and exact Profiled Revision boundary. The sequential runner derives it before
dispatch; the runtime planner independently recomputes it from current
persisted product records and proves the persisted Document Version is the
existing one-way projection of that same Job ID. Unsupported legacy/random,
substituted, collided, or drifted identities fail closed. This requires no
migration and does not create a synthetic member identity or retry policy.

The feature adds no durable conversation state, EF model, migration, matching
rule, graph truth, retry/failover policy, general flow discovery/clustering, or
general V1 retrieval suite. Story 11.5 also owns the final uniquely named
PostgreSQL 17 clean-database two-Repository rehearsal and the single
source-bound Docker build/start/health proof inherited from Stories 11.1-11.4.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (`net10.0`), Razor/Blazor interactive
server rendering

**Primary Dependencies**: ASP.NET Core Identity, Microsoft Fluent UI Blazor,
EF Core/Npgsql, Microsoft/OpenAI Chat Completions packages already referenced
by `Vulgata.Agents`, ASP.NET Core Data Protection

**Storage**: Existing PostgreSQL 17 `vulgata` and `identity` schemas; existing
immutable Document Version, Document Section, Source Anchor, Edge, exact-edge
evidence, Scan execution, Worker job, Code Unit, flow/provenance, System,
Repository, and LLM-provider rows only

**Testing**: xUnit, bUnit, `WebApplicationFactory<Program>`, SQLite relational
integration overrides, PostgreSQL 17 release-gate fixture, isolated
OpenAI-compatible provider fixture, Docker CLI, and test-only
`Microsoft.Playwright` 1.61.0 plus matching Chromium for real Identity and the
interactive-server Blazor circuit

**Target Platform**: Linux container and Windows/Linux developer hosts capable
of .NET 10, Git, PostgreSQL 17, and Docker

**Project Type**: Multi-project .NET Blazor web application

**Performance Goals**: Complete a prepared question within the PRD's 30-second
target for at most five Documents; before Pass 2 perform one bounded relational
binding read; for Chat perform one bounded retrieval read, one bounded
serializable currentness-validation read, and one synthesis call; render no
partial result and hold no database transaction across provider latency

**Constraints**: Chinese-only UI; authenticated users; explicit one-System,
two-Repository prepared scope; current authorized database evidence only;
accepted exact-resolution profile is the sole selector authority; exactly one
consumer-owned runtime Pass 2 flow with exactly two supporting Repositories;
deterministic actual ordinary Pass 1 Worker Job Identity with independently
recomputable canonical bytes and UUIDv8-SHA256 projection;
exact seven-slot Pass 2 dispatch payload with no extension; maximum five Chat
Documents; exact current `REFERENCES` Edge direction; closed JSON; all Material
Claims cited; Business Mode narrative contains no inline code locator; no
absolute path, credential, embedded/fabricated database identity, raw provider
diagnostic, or unauthorized identity; cancellation/timeout fail closed

**Scale/Scope**: One prepared System, two accepted Repositories, one known
RPC/HTTP Cross-Repository flow, one transient interaction at a time, at most
five Documents, twenty Document Sections, thirty-two Source Anchors, twelve
Material Claims, and four citations per claim

## Constitution Check

*GATE: initial pre-design PASS; iteration-4 independently accepted the prior
chat/currentness/Docker plan and those corrections remain closed. Iteration-5
returned `PLAN-115-RUNTIME-IDENTITY-004`; iteration-6 accepted its correction.
T051 preflight then proved ordinary successful Pass 1 loses the random actual
Worker Job ID required for independent runtime binding. The approved
deterministic ordinary Pass 1 Job Identity amendment below resolves that
implementation-blocking gap. Post-design remains pending fresh independent
review.*

| Principle / gate | Pre-design | Post-design evidence |
|---|---|---|
| I. Clean Architecture and explicit dependencies | PASS | Core owns provider-independent chat/runtime Pass 2 binding contracts and the canonical ordinary Pass 1 Job Identity derivation; Infrastructure alone performs the bounded planner and Chat EF reads; Agents keeps the existing exact seven-slot Worker payload and owns Chat dispatch/prompts/selection-only JSON validation; Web owns user authorization, call ordering, and Razor. No EF type, `DbSet`, or `IQueryable` crosses Infrastructure. |
| II. Domain model and persistence discipline | PASS | Canonical terms are preserved. The planner reads existing immutable current Code Logic Document Versions, Source Anchors, Scan/Worker/Code Unit identities, and the accepted exact directed `REFERENCES` Edge. Consumer Repository ownership and exactly-two-Repository support are fixed without adding an entity, table, or migration. |
| III. Validation and API/UI discipline | PASS | Request and model-output contracts enforce closed bounds; the UI and errors are Simplified Chinese. No new HTTP API is required. If input validation is extracted from the component, it uses FluentValidation rather than data annotations. |
| IV. Test-first integration harness | PASS | Ordinary Pass 1 Job Identity RED tests pin independently constructed canonical bytes and UUIDv8, same-boundary repeat identity, each-input substitution, tuple-framing/collision-domain separation, and malformed/mixed-endian rejection. Runtime planner/runner/committer RED tests prove the persisted Document Version projection, reject legacy/random/substituted/drifted Job IDs, and retain the existing independent Manifest/Flow/support vectors and fail-closed cases before dispatch and commit. Exact-span/support Chat RED tests retain their adversarial and barrier-controlled cases. `Story115ReleaseHarness` owns the still-live clean database, product-service provisioning, shared key ring, isolated providers, one final image, curl health, real Identity/Playwright Blazor circuit, negative revocation, and verified cleanup. |
| V. Security and data stewardship | PASS | Runtime binding authorizes both Repositories before reading support and serializes no internal binding identity beyond the accepted seven dispatch slots. Encrypted Chat provider selection is reused; the provider receives only bounded authorized evidence; authorization occurs before retrieval and before rendering; citations are server-derived; source is Repository-relative; failures expose bounded Chinese states. |
| Fixed stack and source isolation | PASS | Existing .NET 10/Blazor/PostgreSQL/OpenAI-compatible stack and self-hosted-provider boundary are retained. |
| ADR, architecture, and ubiquitous-language gate | PASS | No repository ADR or context-specific ADR applicable to this feature was found at the pinned boundary. The bounded planner follows the graph-first rule by consuming the already committed exact `REFERENCES` Edge and never rediscovering graph truth; Epic 5 retains general tracing and clustering. `docs/UBIQUITOUS_LANGUAGE.md` terms are used verbatim. The absent root `CONTEXT-MAP.md` is an existing repository condition, not changed here. |
| Expedited-MVP retained gates | PASS | Independent plan review, planned-change authorization, transient RED/GREEN, combined quality review, security and migration checks, final clean-database two-Repository E2E, and final Docker verification remain required. |

No Constitution violation requires Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/008-demo-chat-release-gate/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── chat-agent-contract.md
│   └── release-verification-contract.md
└── tasks.md                         # created only by speckit-tasks after gates
```

### Planned source ownership

```text
src/dotnet/
├── Vulgata.Core/
│   └── DomainServices/
│       ├── PreparedPass2RuntimeBinding.cs   # server-only identities/support fingerprint
│       ├── IPreparedDemoPass2FlowPlanner.cs # bounded post-resolution planner contract
│       ├── ChatContracts.cs                 # modes, request/evidence/claim/citation/result
│       ├── IChatEvidenceRetriever.cs        # one bounded prepared-scope retrieval tool
│       ├── IChatEvidenceCurrentnessValidator.cs # provider-independent pre-render proof
│       └── IChatAgentDispatcher.cs          # provider-independent synthesis boundary
├── Vulgata.Infrastructure/
│   └── Data/
│       ├── PreparedDemoPass2FlowPlanner.cs  # one authorized relational binding read
│       ├── ChatEvidenceRetriever.cs         # authorized relational current-evidence read
│       └── ChatEvidenceCurrentnessValidator.cs # bounded serializable validation read
├── Vulgata.Agents/
│   ├── ChatExecution/
│   │   ├── ChatAgentProviderResolver.cs     # AgentType.Chat assignment
│   │   ├── ChatAgentDispatcher.cs           # one OpenAI-compatible synthesis call
│   │   ├── ChatAgentResultValidator.cs      # selection-only JSON + retained binding
│   │   └── ChatPromptCatalog.cs             # embedded-resource loader
│   ├── Prompts/chat-business.v1.md
│   ├── Prompts/chat-it.v1.md
│   └── Vulgata.Agents.csproj                # embed both prompts
└── Vulgata.Web/
    ├── Data/
    │   └── PreparedChatCoordinator.cs       # scope listing, double auth, composition
    ├── Components/Pages/
    │   ├── ChatPage.razor                   # Chinese prepared chat UI
    │   └── ChatPage.razor.cs                # transient state, LoadState, cancellation
    ├── Program.cs                           # specific scoped registrations
    └── wwwroot/css/brand.css                # only existing Library Oak tokens/classes

tests/Vulgata.Tests/
├── PreparedDemoPass2FlowPlannerTests.cs
├── PreparedPass2RuntimeDispatchTests.cs
├── ChatContractTests.cs
├── ChatEvidenceRetrievalTests.cs
├── ChatEvidenceCurrentnessTests.cs
├── ChatAgentDispatcherTests.cs
├── ChatAgentResultValidatorTests.cs
├── PreparedChatCoordinatorTests.cs
├── ChatPageTests.cs
├── PreparedChatIntegrationTests.cs
├── DemoChatReleaseGatePostgreSqlTests.cs
└── ReleaseGate/
    ├── Story115ReleaseHarness.cs             # sole clean-DB/Docker/browser owner
    └── loopback-openai-provider.py           # test-only deterministic fixture
```

**Structure Decision**: Extend the existing four-layer vertical slice rather
than introduce a new project. `Vulgata.Web.ViewModels` remains unchanged: the
Razor page's transient state is not a reusable ViewModel, but it still exposes
`LoadState` for every data-loading/sending transition. Existing
`IKnowledgeStoreRepository` behavior remains compatible; the bounded Chat read
is a separate specific repository/tool rather than a generic repository or a
public query surface. The runtime planner is likewise a specific deadline-only
Infrastructure read, not a generic repository, graph query API, scheduler, or
flow-clustering service.

## Runtime Pass 2 Binding and Exact Dispatch Contract

The exact-resolution profile and its persisted evidence are the sole semantic
authority. After the accepted directed `mbs_account` -> `mbs_channel`
`REFERENCES` Edge is committed, the caller supplies the reviewed profile
identity/fingerprint, both explicit runtime bindings, and live
`RepositoryAccessScope` to `IPreparedDemoPass2FlowPlanner`. Infrastructure
performs one bounded relational read in this order:

```text
validate accepted profile identity/version/fingerprint
-> authorize one common System and exactly the two bound Repositories
-> prove both product-created Scan executions and Pass 1 results are current
-> prove aliases, stable Git identities, refs, full commits, and Profiled Revisions
-> map each reviewed consumer/provider selector to exactly one current
   Code Unit + Worker job + Logical Document + Code Logic Document Version
   + required Source Anchors
-> require exactly one accepted current directed REFERENCES Edge and evidence
-> create one consumer-owned ResolvedPreparedPass2Flow
-> compute one canonical server-only support fingerprint
-> runner validates the runtime flow/fingerprint and dispatches exactly once
-> committer revalidates the same support before one Business Logic commit
```

Every zero/multiple, stale, unauthorized, wrong-System, wrong-profile,
wrong-direction, wrong-ref, wrong-commit, wrong-Profiled-Revision, identity, or
support mismatch fails before Worker dispatch and produces zero generated
Documents. Runtime identities come only from current product-created records;
the fixed order/inventory manifest is not production authority and may remain
only as an explicit non-production test fixture. No fixture SQL, direct
`DbSet`, reflection, fabricated identifier, alternate profile, Repository-name
conditional, or implementation-time selector is permitted.

### Ordinary Pass 1 Worker Job Identity

The actual `WorkerAgentJobPacket.JobId` for every newly dispatched ordinary
Pass 1 Code Unit is `Uuid8Sha256` over one version-1 canonical envelope with
ASCII magic `VULGATA-ORDINARY-PASS1-JOB`. It uses the same closed binary TLV
and RFC-network-order UUID rules as the runtime binding codec
`vulgata.prepared-pass2-binding-c14n/1`, but the distinct magic supplies a
separate collision domain. Its only tags, in mandatory order, are:

| Tag | Canonical value |
|---:|---|
| `0x0001` | actual Scan execution UUID, RFC-network-order bytes |
| `0x0002` | actual ScanRun UUID, RFC-network-order bytes |
| `0x0003` | actual Repository UUID, RFC-network-order bytes |
| `0x0004` | actual Code Unit UUID, RFC-network-order bytes |
| `0x0005` | exact Profiled Revision decoded from one full lowercase Git object ID of exactly 40 or 64 hexadecimal characters to 20 or 32 bytes |

The envelope is exactly `magic || 0x00 || 0x01 || field...`; fields are
`tag:u16be || length:u32be || value`. All UUIDs and the revision must be
non-empty and already canonical. UUID text, trimmed/uppercased/odd-length/
abbreviated revision text, JSON, an unknown tag/version, and .NET mixed-endian
`Guid.ToByteArray()` bytes are rejected, never repaired. The UUID projection
uses the exact version-8/variant/network-byte algorithm below.

`SequentialScanRunner` invokes the Core derivation immediately before each
ordinary Pass 1 packet is created and dispatches that derived value as the
packet's actual Job ID. Re-entering the same logical boundary derives the same
identity, but this does not schedule or authorize a retry, duplicate dispatch,
or duplicate commit. Any changed Scan execution, ScanRun, Repository, Code
Unit, or Profiled Revision derives a different identity.

The runtime planner does not accept a member Job ID. From current authorized
rows it independently reconstructs the five canonical inputs, recomputes the
Job ID, and verifies that the selected current ordinary Code Logic Document
Version ID equals the existing persisted projection
`CreateStableId("pass1-document-version", derivedJobId.ToString("D"))` used by
the ordinary Pass 1 committer. This projection check proves the historical
Document Version came from the same actual deterministic Worker job despite
`ScanExecution.InFlightJobId` having been cleared. A pre-amendment random job,
unrecomputable/unsupported legacy row, substituted Job ID, projection
mismatch, duplicate derived ID, same-ID/different-boundary collision, or
current-record drift refuses binding before Pass 2 dispatch; the committer
repeats the runtime support check before persistence.

The independent golden vector in [data-model.md](data-model.md) pins all 142
canonical bytes, the full SHA-256 digest, and Job UUID
`6b9362f7-193b-88d9-8d9c-b2a21ecfe2fd`. Tests construct those bytes without
calling the production encoder. They also prove repeat equality; change each
of the five fields one at a time; distinguish adversarial tuples that would
collide under delimiter concatenation; reject malformed/noncanonical and
mixed-endian input; and refuse any duplicate/colliding derived member
identity. There is no EF field, migration, backfill, synthetic planner member
ID, retry-policy change, or change to the seven-slot Pass 2 payload.

### Authoritative dispatch identity derivation

The runtime planner creates the first three payload slots; callers, the
embedded test fixture, and the release harness cannot supply or override them.

- `PreparedFlowSchemaIdentity` is exactly the existing
  `PreparedPass2FlowLimits.SupportedSchemaIdentity` literal
  `vulgata.prepared-pass2-flow/1.0`. The binder has one closed mapping from the
  accepted exact-profile schema/version `1.0` to this supported Worker contract
  version; any other profile or contract version is refused. The value is
  compared ordinally and never copied from the rejected manifest or caller
  input.
- `ManifestId` preserves the meaning "identity of the prepared manifest" by
  treating the accepted exact-resolution profile as the runtime prepared
  manifest authority. It is `Uuid8Sha256` over the manifest identity envelope
  containing the schema identity, accepted profile ID, accepted profile
  version, and raw 32-byte accepted profile fingerprint.
- `FlowId` preserves the meaning "one flow within that manifest". It is
  `Uuid8Sha256` over the flow identity envelope containing the derived
  `ManifestId`, accepted occurrence ID and consumer/provider aliases/direction,
  actual common System ID, both ordered alias-bound Repository/ScanRun/Scan
  execution identities and stable Git identities, accepted refs/full commits/
  Profiled Revisions, and the accepted exact relationship identity. It does
  not include mutable display names or any Worker-produced narrative.

`Uuid8Sha256` is fully deterministic: hash the canonical envelope with
SHA-256, take digest bytes 0-15, set byte 6 to
`(byte6 & 0x0f) | 0x80` (UUID version 8), set byte 8 to
`(byte8 & 0x3f) | 0x80` (RFC 4122 variant), and interpret all 16 bytes in RFC
network byte order. A .NET `Guid` conversion must preserve that network order;
the mixed-endian `Guid.ToByteArray()` representation is never canonical input.
Text rendering, when needed for evidence, is lowercase `D` form. Empty,
caller-provided, fixed-manifest, substituted, or recomputation-mismatched
values are rejected.

### Canonical bytes and support fingerprint

All three identity envelopes and the support-fingerprint envelope use codec
`vulgata.prepared-pass2-binding-c14n/1`:

```text
envelope = magic || 0x00 || version(0x01) || field...
field    = tag:u16be || length:u32be || value:length-bytes
uuid     = 16 RFC-network-order bytes
u32      = four unsigned big-endian bytes
text     = strict UTF-8 without BOM; value must already be Unicode NFC
hex      = exact lowercase hexadecimal decoded to raw bytes
list     = count:u32be || length-delimited item...
```

Tags are unique, strictly increasing, and envelope-specific; unknown, missing,
duplicate, out-of-order, or zero-length-required fields are invalid. Text is
validated, not normalized during encoding. UUID text and .NET mixed-endian
bytes are forbidden. The manifest envelope magic is ASCII
`VULGATA-PREPARED-PASS2-MANIFEST`, the flow envelope magic is ASCII
`VULGATA-PREPARED-PASS2-FLOW`, and the support envelope magic is ASCII
`VULGATA-PREPARED-PASS2-SUPPORT`.

The support envelope contains, in the fixed tag order defined in
[data-model.md](data-model.md), the schema/derived Manifest/derived Flow
identities; profile ID/version/raw fingerprint; occurrence and exact direction;
System; ordered consumer then provider binding records (alias, stable Git
identity, Repository, ScanRun, Scan execution, ref, raw full commit, raw
Profiled Revision); exact relationship and canonically sorted relationship-
evidence identities; profile-ordered member records (order, Worker job, Code
Unit, Logical Document, Repository, current Code Logic Document Version, and
RFC-byte-sorted unique Source Anchor identities); and exact ordered trace-
evidence records (order, member job, Code Unit, label, locator). Counts and
item lengths are encoded explicitly. The support fingerprint is
`SHA-256(support-envelope)` rendered as exactly 64 lowercase hexadecimal
characters; uppercase, prefixes, separators, whitespace, or alternative
serialization are invalid.

Infrastructure builds these bytes only after unique current authorization and
profile/ref/commit/direction checks. Before dispatch, the runner independently
recomputes all three slots and the raw 32-byte support digest from the current
server-owned binding and compares the digest in fixed time. Immediately before
commit, the committer revalidates current support and repeats the same
recomputation. Any fixed-manifest identity, slot substitution, binding drift,
noncanonical bytes/text/order, changed trace evidence, digest mismatch, or
mixed-endian UUID encoding produces zero dispatches before dispatch or zero
generated Documents before commit. No value is repaired or normalized.

The Worker-facing `Pass2BusinessLogicJobPayload` remains closed to exactly
these seven top-level slots, in this order and with their existing meanings:

1. `PreparedFlowSchemaIdentity`;
2. `ManifestId`;
3. `FlowId`;
4. `SystemId`;
5. `RepositoryId` — the consumer Repository that owns the Business Logic
   Document;
6. `TraceEvidence`;
7. `CodeLogicDocumentVersions` — the exact ordered versions actually used,
   whose server-validated support spans exactly the consumer and provider
   Repositories.

There is no eighth dispatch slot. Scan execution, Worker job, Code Unit,
Logical Document, Document Version, Source Anchor, relationship, profile,
ref/commit, authorization, and support-fingerprint identities remain in the
server-owned runtime binding and validation context. `TraceEvidence` carries
only the bounded safe prepared evidence already accepted by the Worker
contract. The runner and committer compare the canonical support fingerprint
and exact runtime flow; they do not compare against the embedded fixed
singleton. `GENERATED_FROM` targets equal every Code Logic Document Version
actually used. General entry-point discovery, traversal, flow clustering,
multiple Pass 2 jobs, scheduling, concurrency, retry, refinement, and
cross-verification waves remain deferred to Epic 5.

## Design and Execution Flow

```text
authenticated ChatPage
  -> PreparedChatCoordinator.ListScopesAsync
       -> visible System/Repository reads
       -> only pairs backed by the accepted current exact-edge profile are offered
  -> AskAsync(mode, systemId, exactly two repositoryIds, question)
       -> IRepositoryAuthorizationVerifier (both Repositories, same System)
       -> IChatEvidenceRetriever.RetrieveAsync
            -> current Document Versions only
            -> bounded Sections + Source Anchors + flow/provenance
            -> exact directed Cross-Repository REFERENCES Edge, both endpoints
            -> missing-equivalent on absent, stale, incomplete, or unauthorized data
       -> ChatCandidateExtractor
            -> Business: exact eligible BL Section paragraph/list-item spans
            -> IT: exact eligible CL Section paragraph/list-item spans
            -> immutable offsets/text hash + fixed Document/Anchor/relationship support
       -> IChatAgentDispatcher.GenerateAsync
            -> AgentType.Chat provider resolution + decrypted key
            -> embedded mode prompt + finite candidate handles/text
            -> one closed selection/order result; no provider factual prose
        -> ChatAgentResultValidator
             -> exact schema, bounds, mode, evidence fingerprint
             -> every handle is in this packet's eligible allowlist
             -> retained in-memory span hash and fixed support signature match
             -> no provider-authored locator enters the render model
        -> IChatEvidenceCurrentnessValidator.ValidateCurrentAsync
             -> one bounded serializable Infrastructure read
             -> same current Document Versions and Stable Sections/exact spans
             -> fixed same-version safe positive-range Source Anchors
             -> required current authorized REFERENCES/GENERATED_FROM support
             -> missing/stale whole-answer failure on any mismatch
        -> IRepositoryAuthorizationVerifier (same two Repositories again)
       -> server renders retained span text and fixed citations
       -> atomically publish complete Grounded Answer or bounded Chinese failure
```

The live verifier passed into `RepositoryAccessScope` remains authoritative
inside retrieval and currentness validation. The retrieval transaction ends
before provider dispatch. After selection-only provider validation, the Web
coordinator immediately performs the separate bounded serializable currentness
read, ends that transaction, performs the final Repository authorization check,
and renders. No transaction spans provider latency. An exact relationship is
usable only when both directed endpoints remain current in the selected
authorized pair. Restricted markers are not Chat evidence.

### Deterministic claim-support predicate

`ChatCandidateExtractor` is server-owned and model-independent. It walks
canonically ordered current `DocumentSection` projections and emits only exact,
non-empty contiguous `ContentMarkdown` spans: plain paragraph bodies and
list-item bodies after excluding the Markdown marker. `SpanStart` and
`SpanLength` address the exact persisted string; rendered factual text is never
paraphrased, translated, concatenated, completed, or normalized. Fenced code,
tables, raw HTML, links, restricted references, and over-bound spans are
ineligible.

Business candidates come only from current Business Logic Document Sections
of kind `Overview` or `ProcessStep` and must also pass safe-text and no-code-
locator rules before provider dispatch. IT candidates come only from current Code Logic Document
Sections of kind `Overview`, `ProcessStep`, `TechnicalDetail`, or `Evidence`.
Every candidate has a non-empty canonical support set from its Section's
`SourceAnchorIds`; every anchor must resolve to the same current Document
Version, selected Repository, available positive line range, and safe
Repository-relative path. Referenced Document Versions must be selected and
authorized and have the retained authorized `REFERENCES`/`GENERATED_FROM`
relationship. A failed predicate removes the candidate from the allowlist.

For each candidate the server retains mode, Document Version, stable Section,
span offsets, exact-text SHA-256, canonical Anchor IDs, and canonical
relationship identities, and hashes them into a support signature. The
provider sees the candidate handle and exact retained text but returns only
unique ordered candidate handles. A result is accepted only when request,
mode, packet fingerprint, order, and bounds match; every handle is in that
exact packet allowlist; and the retained in-memory span hash and fixed support
signature match. Business safe-text/no-code-locator eligibility is revalidated
there. The selected server-retained identities then form a bounded
`ChatCurrentnessValidationRequest`. Infrastructure validates them against the
database in one serializable read: every Logical Document must still select the
same Document Version; every Stable Section must still contain the exact span
at the retained offsets with the same SHA-256; every fixed Source Anchor must
still be available, safe, same-version, selected-Repository, and have a
positive inclusive range; and every required directed `REFERENCES` and
`GENERATED_FROM` Edge, exact-evidence profile, and endpoint must still be
current and authorized. `Current` is the only success; any missing, stale,
changed, extra, unauthorized, or inconsistent support returns the existing
whole-answer missing/stale failure with zero claims and citations. Web renders
only after the subsequent final Repository authorization check.

Adversarial tests supply otherwise well-formed handles from unrelated and
textually contradictory decoy candidates, a handle from another fingerprint,
a swapped/tampered support binding, and a retained span changed after
extraction. All must produce the same whole-answer failure with zero rendered
claims/citations. The executable support claim is identity-based: the rendered
Material Claim is the persisted Document Section span cited by its immutable
Source Anchor set. This story does not claim general semantic entailment or
contradiction detection across persisted Documents.

Barrier-controlled relational tests pause the deterministic dispatcher after
retrieval has completed and provider dispatch has begun. While paused, one test
commits a successor and moves a selected Logical Document's current-version
pointer; separate support-loss cases remove a required fixed Source Anchor or
required exact `REFERENCES`/`GENERATED_FROM` support through test-only database
fault injection. After releasing the provider, selection validation succeeds
but `ValidateCurrentAsync` must observe the new database state and reject the
entire answer. The barriers prove mutation happened after retrieval and before
currentness validation; the tests assert zero claims/citations and the same
bounded missing/stale outcome. Test-only fault injection is not product or
release-harness provisioning behavior.

## No-Migration Decision

**Decision: no migration.** Runtime Pass 2 bindings/support fingerprints are
server-owned operation data and are not persisted as a new entity. Conversation
messages, selected mode, selected scope, cancellation state, and answer render
models live only in the scoped Blazor circuit. The feature reads existing
System, Repository, Scan execution, Worker job, Code Unit, Document Version,
Document Section, Source Anchor, Edge, exact evidence, flow, and provenance
rows. No entity configuration, `DbSet`, migration, or model snapshot changes
are planned.

The ordinary Pass 1 correction changes newly dispatched Job IDs, not the
persistence model. `InFlightJobId` keeps its existing transient lifecycle and
is still cleared after a valid result. The planner proves a deterministic
historical job through the existing one-way Document Version ID projection;
it does not add a historical-job column or rewrite/backfill legacy Documents.
Unsupported legacy rows fail closed, while the final accepted path starts from
a clean database and therefore contains only recomputable amended jobs.

Verification still runs the predecessor migration lifecycle and
`dotnet ef migrations has-pending-model-changes`. Any implementation discovery
that requires durable chat state or a new persisted field invalidates this
plan and must return to plan review with an additive migration justification.

## Planned-Change Authorization Gate

**Trigger 1 — Chat Agent tool**: this plan adds a Chat Agent retrieval tool,
`IChatEvidenceRetriever.RetrieveAsync`, and its Infrastructure implementation.
The paired currentness validator is an internal provider-independent safety
contract over the same bounded tool result; it adds no provider-visible field
or autonomous retrieval behavior.

**Trigger 2 — Scan preparation and dispatch**: the runtime correction adds a
bounded post-resolution prepared-flow planner and changes runner/committer
authority from the fixed embedded singleton to the validated runtime
flow/support fingerprint. It does not change the exact seven-slot Worker
payload, but it materially changes how the single Pass 2 dispatch is prepared
and accepted. The approved ordinary Pass 1 amendment also replaces random Job
ID creation for newly dispatched ordinary Pass 1 packets with the canonical
server derivation, so it materially changes Scan dispatch identity while
leaving scheduling and retry behavior unchanged. AGENTS.md therefore requires
explicit user approval after fresh independent plan acceptance and before
corrected task generation or production implementation. Approval of the
Sprint Change Proposal and Incremental Edit 5 authorized these planning
corrections only; neither is the post-review planned-change authorization.

**Proposed approach and effect**: bind the accepted exact profile and current
product-created identities into one consumer-owned runtime flow, fail closed on
all ambiguity or drift, and dispatch only the unchanged seven-slot payload.
Ordinary Pass 1 derives its actual Job ID from the exact execution boundary;
the runtime planner independently recomputes it and verifies the existing
Document Version projection, refusing legacy/random or drifted evidence.
Implement one prepared-scope Chat tool instead of
the full nine-tool V1 family. It returns a single finite Evidence Packet from
current PostgreSQL-backed Documents, Document Sections, Source Anchors,
flow/provenance, and the accepted exact `REFERENCES` Edge. The Web coordinator,
not the model, supplies the explicit scope and invokes the tool once. This
satisfies the deadline slice while deferring autonomous scope selection,
file upload, iterative retrieval, and the general search/expansion suite.

**Authorization boundary**: both selected Repositories must be visible within
the selected System before retrieval; the retrieval transaction reuses live
`RepositoryAccessScope` authorization and requires both Edge endpoints; the
selected support is revalidated by Infrastructure after provider selection
validation, and the same pair is rechecked immediately afterward before any
claim or citation is rendered. Unauthorized, revoked, missing, stale, or
incomplete scope is missing-equivalent. The model never authors paths or line ranges for display;
the server resolves each selected Candidate Claim's fixed support handles from
the authorized packet and creates every citation itself.

**Implementation intent after approval**: generate corrected test-first work
for the canonical ordinary Pass 1 identity and runtime binding before resuming
the existing release harness and Chat composition. Do not add persistence,
backfill, a synthetic member identity, an eighth dispatch field, general Scan
strategy, matching truth, clustering, retry/failover, durable history, or any
deferred retrieval tool.

**Affected authorities**:

- This plan: `plan.md` sections **Design and Execution Flow**, **No-Migration
  Decision**, **Ordinary Pass 1 Worker Job Identity**, **Runtime Pass 2 Binding
  and Exact Dispatch Contract**, and **Planned-Change Authorization Gate**.
- PRD `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md`
  section 5.16 accepts one sequential deterministic dispatch path, exact
  prepared Cross-Repository matching, at least one Business Logic Document,
  and the final clean-database two-Repository proof while deferring
  concurrency, retry, and broader behavior.
- PRD FR-4.5 requires one Worker Agent per prepared flow group and
  `GENERATED_FROM` provenance to every Code Logic Document Version actually
  used; FR-6.2 requires Cross-Repository Resolution after Repository Scan
  completion.
- PRD `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md:238`
  defines Chat as database-backed and source-grounded.
- PRD lines 242-248 define Business/IT presentation, typed authorization-aware
  Chat retrieval, and Document/source citations.
- PRD lines 289-310 accept the minimal prepared System/two-Repository grounded
  Chat and clean-database/Docker vertical slice.
- PRD lines 312-325 explicitly defer file upload, autonomous scope selection,
  concurrency, retry/failover, and broader behavior.
- PRD lines 340-345 retain encrypted secrets, source isolation, and read-only
  database-tool requirements.

Independent plan acceptance does not itself authorize task generation. The
root orchestrator must request and record the user's explicit planned-change
approval after review acceptance.

## Requirement Traceability

| Requirements | Design owner | Test/evidence |
|---|---|---|
| FR-001-003 | Web transient page/coordinator; explicit mode/System/exactly-two selection; no upload/auto scope | `ChatPageTests`, `PreparedChatCoordinatorTests` |
| FR-004, FR-011 | Infrastructure bounded retrieval plus post-provider serializable currentness validation; exact current Edge/profile/direction predicate; no `index.md` input | `ChatEvidenceRetrievalTests`, `ChatEvidenceCurrentnessTests`, `PreparedChatIntegrationTests` |
| FR-005-006 | Coordinator precheck + live retrieval/currentness checks + final Repository recheck; missing-equivalent result | `PreparedChatCoordinatorTests`, authorization-negative PostgreSQL E2E |
| FR-007-010 | Mode-specific exact-span extraction, selection-only provider result, immutable support predicate, database currentness proof, whole-answer rejection, Business detail expansion/IT inline allowance | `ChatCandidateExtractorTests`, adversarial `ChatAgentResultValidatorTests`, barrier-controlled `ChatEvidenceCurrentnessTests`, `ChatPageTests` |
| FR-012-014 | Whole-result selection and database-currentness validation before publication; atomic render; cancellation/timeout/provider/malformed fail closed | `ChatAgentDispatcherTests`, `ChatEvidenceCurrentnessTests`, `PreparedChatCoordinatorTests` |
| FR-015-016, FR-023-024, SC-006, SC-010-011 | Unique clean DB; deterministic actual ordinary Pass 1 Job IDs independently recomputed from product-created execution boundaries and verified through the existing Document Version projection; exact profile/evidence bound into one consumer-owned runtime flow; unchanged exact seven-slot Pass 2 payload; exactly-two-Repository support; one Pass 2 dispatch/commit before Chat; legacy/random/substituted/drifted evidence fails closed | `Pass1JobIdentityTests`, `PreparedDemoPass2FlowPlannerTests`, `PreparedPass2RuntimeDispatchTests`, `DemoChatReleaseGatePostgreSqlTests` |
| FR-017-021 | Authorization-negative proof, same-candidate green checks, cleanup, truthful limitations | `DemoChatReleaseGatePostgreSqlTests`, release evidence bundle, Docker inspection |
| FR-022 | Explicit negative static/behavioral boundary: no scheduler, retry, durable session, graph/dashboard/SignalR | static inspection, diff review, no-pending-model check |

## Test-First Work Groups

1. **Ordinary Pass 1 identity and runtime Pass 2 binding RED -> GREEN**:
   canonical version-1 Pass 1 bytes and deterministic actual UUIDv8 Worker Job
   ID; independent hard-coded golden vector; same-boundary repeat equality;
   each-input mutation, tuple-framing/collision-domain, malformed,
   mixed-endian, legacy/random, substitution, projection, duplicate, and drift
   rejection; runner dispatch plus planner independent recomputation from
   current product records; product-created identities; exact
   profile/direction/ref/commit/currentness/authorization; unique selector
   mapping; consumer ownership; exactly-two-Repository support; canonical
   byte encoding; deterministic schema/Manifest/Flow derivation; independent
   golden canonical-byte, UUIDv8, and SHA-256 vectors; unchanged exact
   seven-slot payload; runner/committer rejection before dispatch/commit for
   fixed-manifest, substituted, drifted, noncanonical, reordered, duplicate,
   mixed-endian, and digest-mismatched identities/support; production
   composition without the fixed manifest authority; no migration.
2. **Core Chat contracts RED -> GREEN**: exact mode/scope/question/evidence/output
   bounds, canonical ordering, evidence fingerprint, and failure codes.
3. **Infrastructure retrieval/currentness RED -> GREEN**: one bounded
   current-version retrieval; a separate bounded serializable post-provider
   validation of exact current Document Version/Stable Section/span/Source
   Anchor/relationship support; both-endpoint authorization; deterministic
   packet identity; and barrier-controlled supersession, Source Anchor loss,
   and `REFERENCES`/`GENERATED_FROM` support-loss rejection. No transaction is
   held across provider latency.
4. **Candidate/Agents RED -> GREEN**: mode-specific exact BL/CL span
   extraction, immutable support signatures, AgentType.Chat resolution, prompt
   embedding, secret decryption, one-call dispatch, selection-only JSON,
   foreign unrelated/contradictory decoy handles, swapped support, changed
   retained-memory spans, provider failure, timeout, and cancellation.
5. **Web coordinator RED -> GREEN**: prepared scope listing; pre-retrieval
   authorization; exact ordering of selection validation -> Infrastructure
   currentness validation -> final Repository authorization -> render;
   missing-equivalent behavior; server-derived citations; no partial
   publication.
6. **Razor UI RED -> GREEN**: Chinese states, mode-locked theme, inline
   selectors, keyboard send/cancel, Business expandable citation detail, IT
   source locator display, no file upload or automatic scope.
7. **Composition RED -> GREEN**: real Web/Core/Infrastructure/Agents
   registration with relational storage and the shared-namespace provider.
8. **Final gate**: sole `Story115ReleaseHarness` ownership of focused checks,
   migration/no-pending checks, Web build, regression, the unique PostgreSQL
   rehearsal, one source-bound Docker build, curl health, real
   Identity/interactive Blazor Playwright proof, and complete cleanup.

Every production behavior first has a transient failing test; no RED test is
committed or accepted. The expedited exception combines acceptance and quality
review but does not remove any test or release gate.

## Release E2E and Docker Boundary

The final PostgreSQL fixture owns the accepted profile facts:

- `mbs_account`, `refs/tags/vulgata/August15th-sample`,
  `19c60cd3f3976b7697c487c7bafd96142e3a7625`;
- `mbs_channel`, `refs/tags/vulgata/August15th-sample`,
  `47f6b627a7598f862873610413eec1e1e7bf46bf`.

It creates a random database name and gate-owned PostgreSQL 17 container,
Data Protection key directory, Docker network, provider fixture, container
names, and host ports. It applies all migrations, provisions actual users,
scope, providers, assignments, and knowledge through product services, runs
profiling -> canonical actual ordinary Pass 1 Job ID derivation and Pass 1 for
each Repository -> exact Cross-Repository Resolution ->
runtime prepared-flow binding -> exact seven-slot Pass 2 dispatch/commit ->
bounded retrieval -> one fully cited Business or IT answer, then runs
the inaccessible-scope check. The database stays alive for Docker proof. In
`finally`, the harness removes app/provider containers, terminates exact-
database connections, drops it, proves catalog absence, then removes its
PostgreSQL container/volume, network, and key directory.

Users are created with `UserManager<ApplicationUser>`, roles with
`RoleSeeder`/`IAdministratorRoleCoordinator`, ownership with
`ISystemOwnershipCoordinator`, management rows with existing repositories and
coordinators, knowledge with the accepted profiling/Sequential Scan Runner/
exact resolver/Pass 2 services, and provider rows/overrides with
`ILlmProviderManagementCoordinator` and
`ISystemLlmProviderOverrideCoordinator`; no fixture SQL or `DbSet` insertion is
allowed. Real `SecretEncryptionService` encrypts Worker and Chat keys against a
gate-owned key ring. A planned optional `DataProtection:KeyRingPath` plus
`DataProtection:ApplicationName` branch in `Program.cs` activates only when
both are present; provisioner and container use the same mounted ring and
discriminator, while default deployments remain unchanged.

The deterministic OpenAI-compatible fixture runs from the Dockerfile-pinned
Python base in a gate-owned container attached to the nonce-owned internal
Docker network, without a second candidate build. The harness selects distinct
nonce-owned `workerPort`, `chatPort`, and `appHostPort` values, with neither
provider port equal to app port `8080`. The fixture is invoked as
`python /fixture/loopback-openai-provider.py --host 0.0.0.0 --worker-port
<workerPort> --chat-port <chatPort>` and therefore listens on all interfaces in
its namespace. Its container is created with exact mappings
`--publish 127.0.0.1:<workerPort>:<workerPort>`,
`--publish 127.0.0.1:<chatPort>:<chatPort>`, and
`--publish 127.0.0.1:<appHostPort>:8080`. Product-service provisioning and the
host-side profiling/Scan path use stored endpoints
`http://127.0.0.1:<workerPort>/v1` and
`http://127.0.0.1:<chatPort>/v1` through the first two mappings.

The final app is started with `--network container:<providerContainer>` and no
app-side `--publish`; it shares the provider namespace, so those same stored
`127.0.0.1` endpoints reach the fixture on the identical internal port
numbers. The provider container's already-created
`127.0.0.1:<appHostPort>:8080` mapping exposes the app only to host loopback for
Playwright. The app remains bound to `0.0.0.0:8080`, reaches PostgreSQL through
the provider container's nonce-owned network attachment, and uses an
`--env-file` plus the read-only key-ring mount. A provider that binds only its
container's `127.0.0.1`, a remapped provider port with unequal host/container
numbers, or `--publish` combined with `--network container:` is invalid.

The Docker gate uses the canonical production-source manifest, builds the final
runtime image exactly once, inspects its source labels/image ID, and starts it
against the still-live database with the key ring mounted read-only. `docker
run` supplies the actual health command `curl --fail --silent --show-error
http://127.0.0.1:8080/Account/Login` with bounded interval, timeout, retries,
and start period; `curl` is installed in `docker/Dockerfile`, so no absent
Dockerfile healthcheck or compose `wget` is assumed.

Test-only `Microsoft.Playwright` 1.61.0 and its matching installed Chromium
are prepared only after
`dotnet build tests/Vulgata.Tests/Vulgata.Tests.csproj --configuration Release`
has generated the Release driver; the harness then invokes
`tests/Vulgata.Tests/bin/Release/net10.0/playwright.ps1 install chromium`.
They
authenticate through the real `/Account/Login` form, wait for the interactive-
server Blazor circuit, operate the Fluent selectors/send/citation controls, and
assert exact retained claims and positive line ranges. A second Identity
context proves unauthorized scope reveals no System/Repository/claim/citation;
after the positive selection, the harness also revokes ownership through the
product coordinator and requires submit to show the fixed missing-equivalent
Chinese state with no stale answer. Static HTML, `HttpClient`, bUnit, or direct
coordinator invocation cannot substitute for this final circuit proof.

## Risks and Mitigations

| Risk | Mitigation / stop condition |
|---|---|
| Provider-authored prose can carry valid but unrelated citations | Provider returns selection/order handles only. Web renders exact retained Section spans whose Anchor/relationship support signature is fixed and revalidated; unrelated/contradictory decoy handles or support swaps reject the whole result. |
| Document/support changes during provider latency | End retrieval before provider dispatch; after selection validation run one bounded serializable Infrastructure currentness read over the selected exact identities; fail the whole answer on supersession, span/anchor loss, or relationship/profile/direction drift. |
| Authorization revoked after retrieval | Run live authorization inside currentness validation, then recheck both Repositories immediately after it and before render; discard the entire answer on failure. |
| Existing `CurrentKnowledge` projection contains restricted markers or text derived from an unauthorized target | The Chat retriever constructs a narrower packet and rejects restricted relationship/section/flow markers; both exact Edge endpoints are mandatory. |
| Provider output leaks locators or diagnostics | Closed JSON permits only schema/request/mode/fingerprint echoes and unique ordered Candidate Claim handles; it permits no provider-authored claim text, citation handle, locator, narrative, or diagnostic. Business locator safety is applied to server-extracted Candidate Claim text before dispatch and revalidated before render; display locators come only from fixed server evidence; failures use fixed Chinese messages. |
| Provider exceeds 30 seconds | Linked timeout/cancellation; one call, no retry/failover, no partial result. |
| Prepared Repository/profile drift | Require full accepted refs/commits and exact profile fingerprint before runtime binding and the final run; mismatch produces zero Pass 2 dispatches and fails the release gate. |
| Successful ordinary Pass 1 no longer retains `InFlightJobId` | Derive the actual Job ID canonically before dispatch and have the planner recompute it from the persisted execution boundary, then require the current Document Version ID to equal the existing one-way projection of that derived job. No new persisted field or reverse-hash assumption is used. |
| Legacy/random or colliding member identity is mistaken for current evidence | Fail closed unless every current member has canonical inputs, a matching deterministic Document Version projection, and a unique derived Job ID. Pin an independent golden vector plus repeat, each-input-change, tuple-framing, domain-separation, duplicate/collision, substitution, and drift tests. The clean final database has no legacy rows. |
| Runtime identities cannot satisfy the accepted prepared flow | Infrastructure requires unique current authorized mappings for every selector and the directed relationship, computes a canonical server-only support fingerprint, and fails before dispatch. The fixed manifest, fabricated IDs, reflection, fixture SQL, and direct `DbSet` insertion are forbidden. |
| Derived runtime identities drift across processes or platforms | Use the versioned binary TLV codec, strict UTF-8/NFC validation, raw lowercase-hex decoding, RFC-network-order UUID bytes, UUIDv8-SHA256 derivation, and independent hard-coded golden byte/UUID/fingerprint vectors. Runner and committer recompute rather than trust supplied slots. |
| Runtime correction silently expands the Worker contract | Keep `Pass2BusinessLogicJobPayload` at exactly seven top-level slots; assert the closed slot set and production composition statically and behaviorally. Support fingerprint and validation identities never become an eighth slot. |
| Docker build is not the tested source | Require a clean immutable candidate and existing source commit/tree/production-manifest labels; inspect labels before startup. |
| Browser/container proof degrades to static HTTP | Pin Playwright/Chromium, wait for the Blazor circuit, require actual control interaction plus claim/citation assertions, and fail rather than fall back. |
| Provisioner/container Data Protection differs | Supply the exact optional key path and application discriminator to both, mount the same ring, and prove real Worker/Chat decryption. |
| Cleanup failure masks the test result | Preserve primary and cleanup failures, remove only nonce-prefixed owned resources in dependency order, and fail until database and all gate-owned resources are absent. |

## Complexity Tracking

No Constitution violations.
