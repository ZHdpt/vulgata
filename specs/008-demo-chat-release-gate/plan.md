# Implementation Plan: Demo Chat, Citations, and Release Gate

**Branch**: `codex/run-story-115-sprint-loop` | **Date**: 2026-08-04 | **Spec**: [spec.md](spec.md)

**Input**: Story 11.5 at source boundary
`c07df7d8789e7b975dc7fb6a121dff564e8549ad`, execution level
`expedited-mvp`.

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

The feature adds no durable conversation state, EF model, migration, Scan
behavior, matching rule, graph truth, retry/failover policy, or general V1
retrieval suite. Story 11.5 also owns the final uniquely named PostgreSQL 17
clean-database two-Repository rehearsal and the single source-bound Docker
build/start/health proof inherited from Stories 11.1-11.4.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (`net10.0`), Razor/Blazor interactive
server rendering

**Primary Dependencies**: ASP.NET Core Identity, Microsoft Fluent UI Blazor,
EF Core/Npgsql, Microsoft/OpenAI Chat Completions packages already referenced
by `Vulgata.Agents`, ASP.NET Core Data Protection

**Storage**: Existing PostgreSQL 17 `vulgata` and `identity` schemas; existing
immutable Document Version, Document Section, Source Anchor, Edge, exact-edge
evidence, flow/provenance, System, Repository, and LLM-provider rows only

**Testing**: xUnit, bUnit, `WebApplicationFactory<Program>`, SQLite relational
integration overrides, PostgreSQL 17 release-gate fixture, isolated
OpenAI-compatible provider fixture, Docker CLI, and test-only
`Microsoft.Playwright` 1.61.0 plus matching Chromium for real Identity and the
interactive-server Blazor circuit

**Target Platform**: Linux container and Windows/Linux developer hosts capable
of .NET 10, Git, PostgreSQL 17, and Docker

**Project Type**: Multi-project .NET Blazor web application

**Performance Goals**: Complete a prepared question within the PRD's 30-second
target for at most five Documents; perform one bounded retrieval read, one
bounded serializable currentness-validation read, and one synthesis call;
render no partial result and hold no database transaction across provider
latency

**Constraints**: Chinese-only UI; authenticated users; explicit one-System,
two-Repository prepared scope; current authorized database evidence only;
maximum five Documents; exact current `REFERENCES` Edge direction; closed JSON;
all Material Claims cited; Business Mode narrative contains no inline code
locator; no absolute path, credential, raw provider diagnostic, or
unauthorized identity; cancellation/timeout fail closed

**Scale/Scope**: One prepared System, two accepted Repositories, one known
RPC/HTTP Cross-Repository flow, one transient interaction at a time, at most
five Documents, twenty Document Sections, thirty-two Source Anchors, twelve
Material Claims, and four citations per claim

## Constitution Check

*GATE: initial pre-design PASS; prior semantic and Docker corrections remain
closed; iteration-3 independent review REVISE on post-provider database
currentness; this corrected post-design is PASS pending fresh independent
review.*

| Principle / gate | Pre-design | Post-design evidence |
|---|---|---|
| I. Clean Architecture and explicit dependencies | PASS | Core owns provider-independent chat contracts, including retrieval and currentness-validation interfaces; Infrastructure alone implements both EF reads; Agents owns provider dispatch/prompts/selection-only JSON validation; Web owns user authorization, call ordering, and Razor. No EF type, `DbSet`, or `IQueryable` crosses Infrastructure. |
| II. Domain model and persistence discipline | PASS | Canonical terms are preserved. Existing immutable Document Versions and current-version projections are read only. Transient session state creates no entity, table, or migration. |
| III. Validation and API/UI discipline | PASS | Request and model-output contracts enforce closed bounds; the UI and errors are Simplified Chinese. No new HTTP API is required. If input validation is extracted from the component, it uses FluentValidation rather than data annotations. |
| IV. Test-first integration harness | PASS | Exact-span/support RED tests include unrelated/contradictory decoy handles and whole-result rejection; barrier-controlled integration tests supersede a Document Version and remove Source Anchor/relationship support during provider latency. `Story115ReleaseHarness` owns the still-live clean database, product-service provisioning, shared key ring, isolated providers, one final image, curl health, real Identity/Playwright Blazor circuit, negative revocation, and verified cleanup. |
| V. Security and data stewardship | PASS | Encrypted Chat provider selection is reused; the provider receives only a bounded authorized packet; authorization occurs before retrieval and before rendering; citations are server-derived; source is Repository-relative; failures expose bounded Chinese states. |
| Fixed stack and source isolation | PASS | Existing .NET 10/Blazor/PostgreSQL/OpenAI-compatible stack and self-hosted-provider boundary are retained. |
| ADR and ubiquitous-language gate | PASS | No repository ADR or context-specific ADR applicable to this feature was found at the pinned boundary. `docs/UBIQUITOUS_LANGUAGE.md` terms are used verbatim. The absent root `CONTEXT-MAP.md` is an existing repository condition, not changed here. |
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
│       ├── ChatContracts.cs                 # modes, request/evidence/claim/citation/result
│       ├── IChatEvidenceRetriever.cs        # one bounded prepared-scope retrieval tool
│       ├── IChatEvidenceCurrentnessValidator.cs # provider-independent pre-render proof
│       └── IChatAgentDispatcher.cs          # provider-independent synthesis boundary
├── Vulgata.Infrastructure/
│   └── Data/
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
public query surface.

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

**Decision: no migration.** Conversation messages, selected mode, selected
scope, cancellation state, and answer render models live only in the scoped
Blazor circuit. The feature reads existing System, Repository, provider,
Document Version, Document Section, Source Anchor, Edge, exact evidence, flow,
and provenance rows. No entity configuration, `DbSet`, migration, or model
snapshot changes are planned.

Verification still runs the predecessor migration lifecycle and
`dotnet ef migrations has-pending-model-changes`. Any implementation discovery
that requires durable chat state or a new persisted field invalidates this
plan and must return to plan review with an additive migration justification.

## Planned-Change Authorization Gate

**Trigger**: this plan adds a Chat Agent retrieval tool,
`IChatEvidenceRetriever.RetrieveAsync`, and its Infrastructure implementation.
The paired currentness validator is an internal provider-independent safety
contract over the same bounded tool result; it adds no provider-visible field
or autonomous retrieval behavior.
AGENTS.md requires explicit user approval after independent plan acceptance and
before task generation for any new or materially changed agent tool.

**Proposed approach and effect**: implement one prepared-scope tool instead of
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

**Implementation intent after approval**: generate test-first tasks for this
single tool and the composition above. Do not add Scan strategy, matching
truth, retry/failover, durable history, or any deferred retrieval tool.

**Affected authorities**:

- This plan: `plan.md` sections **Design and Execution Flow**, **No-Migration
  Decision**, and **Planned-Change Authorization Gate**.
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
| FR-015-021 | Unique clean DB, prepared revisions, full predecessor path, green checks, cleanup, truthful limitations | `DemoChatReleaseGatePostgreSqlTests`, release evidence bundle, Docker inspection |
| FR-022 | Explicit negative static/behavioral boundary: no scheduler, retry, durable session, graph/dashboard/SignalR | static inspection, diff review, no-pending-model check |

## Test-First Work Groups

1. **Core contracts RED -> GREEN**: exact mode/scope/question/evidence/output
   bounds, canonical ordering, evidence fingerprint, and failure codes.
2. **Infrastructure retrieval/currentness RED -> GREEN**: one bounded
   current-version retrieval; a separate bounded serializable post-provider
   validation of exact current Document Version/Stable Section/span/Source
   Anchor/relationship support; both-endpoint authorization; deterministic
   packet identity; and barrier-controlled supersession, Source Anchor loss,
   and `REFERENCES`/`GENERATED_FROM` support-loss rejection. No transaction is
   held across provider latency.
3. **Candidate/Agents RED -> GREEN**: mode-specific exact BL/CL span
   extraction, immutable support signatures, AgentType.Chat resolution, prompt
   embedding, secret decryption, one-call dispatch, selection-only JSON,
   foreign unrelated/contradictory decoy handles, swapped support, changed
   retained-memory spans, provider failure, timeout, and cancellation.
4. **Web coordinator RED -> GREEN**: prepared scope listing; pre-retrieval
   authorization; exact ordering of selection validation -> Infrastructure
   currentness validation -> final Repository authorization -> render;
   missing-equivalent behavior; server-derived citations; no partial
   publication.
5. **Razor UI RED -> GREEN**: Chinese states, mode-locked theme, inline
   selectors, keyboard send/cancel, Business expandable citation detail, IT
   source locator display, no file upload or automatic scope.
6. **Composition RED -> GREEN**: real Web/Core/Infrastructure/Agents
   registration with relational storage and the shared-namespace provider.
7. **Final gate**: sole `Story115ReleaseHarness` ownership of focused checks,
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
profiling -> Pass 1 for each Repository -> exact Cross-Repository Resolution ->
Pass 2 -> bounded retrieval -> one fully cited Business or IT answer, then runs
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
| Prepared Repository/profile drift | Require full accepted refs/commits and exact profile fingerprint before the final run; mismatch is a failed release gate. |
| Docker build is not the tested source | Require a clean immutable candidate and existing source commit/tree/production-manifest labels; inspect labels before startup. |
| Browser/container proof degrades to static HTTP | Pin Playwright/Chromium, wait for the Blazor circuit, require actual control interaction plus claim/citation assertions, and fail rather than fall back. |
| Provisioner/container Data Protection differs | Supply the exact optional key path and application discriminator to both, mount the same ring, and prove real Worker/Chat decryption. |
| Cleanup failure masks the test result | Preserve primary and cleanup failures, remove only nonce-prefixed owned resources in dependency order, and fail until database and all gate-owned resources are absent. |

## Complexity Tracking

No Constitution violations.
