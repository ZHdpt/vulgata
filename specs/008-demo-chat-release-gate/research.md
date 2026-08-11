# Phase 0 Research: Demo Chat and Release Gate

All planning unknowns are resolved.

## R1. Prepared retrieval shape

**Decision**: Add one provider-independent
`IChatEvidenceRetriever.RetrieveAsync` operation for the prepared System and
exactly two Repositories. It returns a finite Evidence Packet containing at
most five current Documents, twenty Document Sections, thirty-two Source
Anchors, required flow/provenance, and the one accepted directed exact
cross-Repository `REFERENCES` Edge. It never reads `index.md`.

**Rationale**: PRD 5.11 defines a general nine-tool retrieval family, while PRD
5.16 accepts a minimal prepared-scope grounded Chat for the August deadline.
The existing `IKnowledgeStoreRepository.GetCurrentAsync` is document-keyed and
already proves currentness/redaction, but it does not discover the finite
prepared flow. A separate specific read avoids changing commit/rebuild behavior
or exposing a generic query surface.

**Alternatives considered**:

- Implement all nine V1 tools: rejected as deferred breadth and unnecessary for
  the one prepared flow.
- Let the model choose tools/scope iteratively: rejected because explicit scope
  is required and model-directed expansion increases authorization risk.
- Read generated Markdown or `index.md`: rejected because PostgreSQL is
  canonical and those files are export/readability artifacts.

## R2. Prepared-pair identification

**Decision**: Offer/accept only a same-System Repository pair backed by current
Documents and the accepted embedded exact-resolution profile/evidence. Do not
hardcode database IDs, Git URLs, or host paths. The final gate additionally
requires the profile's pinned aliases, refs, and full commits.

**Rationale**: Story 11.4 already owns the closed prepared profile and exact
truth. Reusing its persisted exact-edge evidence prevents Story 11.5 from
inventing a second configuration or broadening matching.

**Alternatives considered**:

- Select by Repository display names alone: rejected because names are mutable
  and not proof of accepted exact resolution.
- Add a persisted demo-scope table: rejected because durable configuration and
  a migration are unnecessary.

## R3. Authorization and revocation window

**Decision**: Enforce four ordered layers: coordinator precheck of both
Repositories and common System; live `RepositoryAccessScope` verification
during retrieval; a provider-independent Infrastructure currentness validation
after selection-only provider validation; and a final coordinator Repository
check immediately before render. Missing, unauthorized, revoked, restricted,
stale, or incomplete scope has one missing-equivalent outcome.

**Rationale**: Existing `RepositoryAuthorizationVerifier` already enforces one
authenticated user, one common visible System, and at most two Repositories.
The final check closes the authorization-revocation window required by the
story. The separate database validation closes current-version and support
drift that an in-memory packet cannot observe.

**Alternatives considered**:

- Check only before retrieval: rejected because access can be revoked while
  the provider is running.
- Recompute only retained packet hashes after the provider returns: rejected
  because immutable request memory cannot reveal database supersession or
  Source Anchor/relationship support loss.
- Pass restricted relationship markers to the model: rejected because even a
  marker can bias narrative and the prepared answer requires both endpoints.

## R4. Post-provider database currentness

**Decision**: Add provider-independent
`IChatEvidenceCurrentnessValidator.ValidateCurrentAsync` in Core, implemented
only by Infrastructure and ordered by Web after selection-only provider
validation and immediately before final Repository authorization and render.
Its bounded request contains the request ID, Evidence Fingerprint, selected
Logical Document/Document Version pairs, Stable Section and exact span
identities/hashes, fixed Source Anchor identities/support, and required directed
`REFERENCES`/`GENERATED_FROM` relationship/profile identities. One serializable
read returns only `Current` or the existing whole-answer missing/stale outcome.
Retrieval completes before provider dispatch; no transaction spans provider
latency.

**Rationale**: The retained Evidence Packet proves what was retrieved, not what
is current after a provider call. The validation read can atomically prove that
each Logical Document still selects the same Document Version; every Stable
Section still contains the exact span; every fixed Source Anchor remains safe,
same-version, selected-Repository, and positive-range; and every required Edge,
exact-evidence profile, endpoint, and direction remains current and authorized.
Infrastructure owns EF; Agents cannot and does not query currentness.

**Alternatives considered**:

- Hold the retrieval transaction open across the provider call: rejected as an
  unbounded lock/snapshot lifetime across external latency.
- Let `ChatAgentResultValidator` query EF: rejected by the Core/Infrastructure/
  Agents layering boundary.
- Validate only Document Version IDs: rejected because Stable Section spans,
  Source Anchors, and relationship support can independently disappear or
  drift.

## R5. Deterministic claim support

**Decision**: The server extracts mode-appropriate Candidate Claims as exact
contiguous spans from current Document Sections: Business uses safe
`Overview`/`ProcessStep` spans from Business Logic Documents; IT uses
`Overview`/`ProcessStep`/`TechnicalDetail`/`Evidence` spans from Code Logic
Documents. Each candidate retains exact offsets/text hash and an immutable
support signature over its current Document Version, Section, Source Anchors,
and required authorized relationships. The provider returns selection/order
handles only. After selection validation, Infrastructure validates the selected
exact identities against current database state; Web then performs final
Repository authorization and renders the retained span and fixed citations.
Provider factual prose is absent from the schema.

**Rationale**: Exact persisted text is supported by identity, not a fallible
runtime entailment judgment. A structurally valid but unrelated citation cannot
be attached to free prose because there is no free prose or independent
citation choice. Foreign unrelated/contradictory decoy handles, changed spans,
and support swaps are deterministic whole-answer failures.

**Alternatives considered**:

- Render free-form Markdown containing model-created links: rejected because
  unknown/out-of-scope links can leak or miscite evidence.
- Accept provider-authored claim text plus known handles: rejected because
  structural validation cannot prove semantic support.
- Render valid claims while dropping invalid ones: rejected because partial
  narrative can mislead and violates fail-closed acceptance.

## R6. Business/IT reconciliation

**Decision**: Server-extracted Business Candidate Claim text rejects inline
path/line/code-reference patterns before provider dispatch and is revalidated
immediately before render. Each claim still has a Chinese Document citation;
its expandable detail displays the authorized persisted Repository-relative
Source Anchor line range. IT Mode may include that locator directly. Switching
mode clears the transient interaction and applies the mode-locked Library Oak
theme; no session is persisted.

**Rationale**: This is the user-approved reconciliation recorded by Story 11.5
between PRD FR-11.1 and mandatory source-line citations. UX requires Business
light, IT dark, inline selectors, and Fluent components.

**Alternatives considered**:

- Remove source detail from Business citations: rejected because every
  Material Claim must remain verifiable.
- Persist sessions to satisfy the full PRD switch behavior: rejected because
  PRD 5.16 and the story explicitly allow a transient deadline interaction.

## R7. Provider execution

**Decision**: Add a Chat-specific provider resolver following the existing
Worker resolver pattern but selecting `AgentType.Chat`. Use existing encrypted
LLM-provider records, Data Protection decryption, Chat Completions support, two
embedded mode prompts, one provider call, a linked 30-second timeout, and no
retry/failover.

**Rationale**: The stack and assignment model already support Chat agents.
Keeping a distinct resolver prevents accidental Worker assignment and preserves
project ownership. One call is sufficient because retrieval is deterministic
and already complete.

**Alternatives considered**:

- Reuse `WorkerAgentProviderResolver`: rejected because it requires
  `AgentType.Worker` and carries Worker-specific semantics.
- Add dynamic failover/retry: rejected by the deadline boundary.

## R8. Persistence and migrations

**Decision**: Keep mode, selection, messages, LoadState, cancellation token,
and Grounded Answer in the scoped Blazor circuit only. Add no entity, `DbSet`,
configuration, migration, or snapshot edit.

**Rationale**: The accepted story states that durable chat history is not
required. All evidence already exists in the knowledge store.

**Alternatives considered**:

- Persist chat sessions/messages: rejected as deferred scope and avoidable
  migration risk.
- Cache evidence durably: rejected because it creates staleness and revocation
  hazards.

## R9. Final E2E and Docker evidence

**Decision**: Adopt the user-approved Option A topology. After candidate,
manifest, Repository-ref, provisioner-inventory, host PostgreSQL, and Docker
Desktop preflight, `Story115ReleaseHarness` builds the source-bound final image
exactly once and records its immutable ID. It starts a temporary provisioner
container from that ID, removes it, and starts the final app container from the
same ID. The driver is a prebuilt test-owned framework-dependent .NET entry
mounted read-only into `/app`; it is not copied into the image and no
provisioner image is built, committed, pushed, or shipped. An inventory check
forbids mounted duplicate/colliding `Vulgata.*` product assemblies, so every
product service resolves from the inspected image publish.

The provisioner joins `--network container:<provider>`, uses the identical
stored `127.0.0.1` Worker/Chat endpoints later used by the app, and reaches the
external PostgreSQL 17 Alpine nonce database through Docker Desktop's built-in
`host.docker.internal`. It runs migrations, Identity/System/Repository/provider
setup, Repository profiling, deterministic ordinary Pass 1, exact
Cross-Repository Resolution, runtime prepared-flow binding, and the one exact
seven-slot Pass 2 dispatch/commit. `CodeGraphContextProvider` therefore launches
the qualified image's hash-locked Linux CodeGraphContext 0.5.1 `cgc`; no host
installation, pip resolution, Python fallback, or alternate runtime exists.

The two accepted ref/full-commit source origins are mounted read-only at stable
Linux paths and production Git services clone them into a nonce-owned writable
work mount. Permission-restricted config and app env files are read-only in
their consumers; output/work/key-ring paths are nonce-owned, with the key ring
writable for provisioning and read-only for the app. The driver writes one
closed sanitized result atomically. Credentials, connection strings, keys,
source text, raw provider diagnostics, and absolute host paths are rejected.
The nonce database stays alive through app/browser proof. Cleanup aggregates
primary and every cleanup failure and removes only harness-owned provisioner/
app/provider/image/network/source/config/output/work/env/key/browser/database
state, then proves exact absence. External server/container/admin DB remain
untouched.

**Rationale**: The historical live T064 failure proved host-side profiling cannot use the
accepted CodeGraph runtime, while `docker/Dockerfile` already contains the exact
Linux runtime plus .NET, git, and curl. Sequential containers can share the
provider namespace, and a read-only driver mounted into `/app` can execute
against the image's product assemblies without changing the production image.
Immutable-ID equality proves provisioner and app are instances of one artifact.
Each predecessor explicitly hands these final gates to Story
11.5. Story 11.4 fixtures do not substitute for actual prepared Repository
revisions or final image evidence. The user approved reuse of the running
instance under the nonce-database lifecycle. Database-level ownership retains
clean-migration and verified-cleanup evidence without taking ownership of
shared server infrastructure. A publish-capable network is required because
Docker Desktop suppresses published-port access from an `--internal` network;
loopback-only publications retain the host exposure boundary. The sole live
T072 run is deliberately limited to a Windows Docker Desktop host; native-Linux-host proof
is not claimed, so no untested fallback is part of the release contract.

**Alternatives considered**:

- Reuse predecessor integration evidence: rejected by the Story 11.4 handoff.
- Select or migrate the supplied administrator/development database directly:
  rejected because isolation, exact cleanup, and server non-ownership are
  mandatory.
- Create a second gate-owned PostgreSQL container/volume: rejected because the
  approved design reuses the existing PostgreSQL 17 endpoint and owns only the
  nonce database.
- Use an `--internal` provider/app network: rejected because Docker Desktop
  suppresses the required loopback-published provider/app ports.
- Claim native-Linux-host T072 support or invent an alternate host route:
  rejected because the time-bounded proof has one authorized, executable
  Windows Docker Desktop topology and the prior explicit host-mapping command
  is incompatible with container namespace mode.
- Use `HttpClient`, SSR markup, or bUnit as final chat evidence: rejected
  because none proves an interactive-server circuit in the accepted image.
- Rely on compose `wget` or an image healthcheck: rejected because `wget` is
  absent and the Dockerfile has no `HEALTHCHECK`; runtime `curl` is present.
- Insert encrypted providers directly: rejected because ciphertext must be
  produced by the same Data Protection ring/application discriminator that the
  runtime uses.
- Keep host-side profiling or install host `cgc`/Python: rejected because it
  neither matches nor proves the qualified Linux CodeGraphContext 0.5.1 image.
- Build a second provisioner/helper image: rejected because FR-018/SC-007 and
  NFR-4.1 require the one final application image boundary.
- Mount a published driver directory containing duplicate product assemblies:
  rejected because product execution would no longer be proven against the
  inspected image.

## R10. Constitution and ADR result

**Decision**: Proceed with no exception or Complexity Tracking entry.

**Rationale**: The design respects Clean Architecture, immutable Document
Versions, Chinese UI, integration-first tests, encrypted secrets, source
isolation, and authorization. No applicable ADR was found at the pinned
boundary. Canonical terms come from `docs/UBIQUITOUS_LANGUAGE.md`.

**Alternatives considered**: None; a discovered conflicting ADR or need for a
new persistence model would stop planning and require escalation.

## R11. Runtime Pass 2 identity binding and dispatch shape

**Decision**: Add one deadline-only Infrastructure
`PreparedDemoPass2FlowPlanner` after the accepted exact Cross-Repository
Resolution outcome. The exact-resolution profile remains the sole selector
authority. The planner performs one bounded relational read over the two
product-created Scan executions, current Pass 1 Code Logic Document Versions,
Source Anchors, Worker jobs, Code Units, Logical Documents, and the accepted
directed `REFERENCES` Edge/evidence. Every reviewed consumer/provider selector
must map to exactly one current authorized identity with the accepted profile,
direction, ref, full commit, and Profiled Revision. The resulting runtime flow
is owned by the consumer Repository and its support spans exactly the consumer
and provider Repositories.

The server computes a canonical support fingerprint over the accepted profile
fingerprint plus the actual System, two Repository, Scan execution/ScanRun,
Worker job, Code Unit, Logical Document, Document Version, Source Anchor, and
relationship identities. The runner and committer validate that runtime flow
and fingerprint before dispatch and persistence. The fingerprint and binding
details are server-only; the Worker-facing `Pass2BusinessLogicJobPayload`
remains closed to exactly seven top-level slots:

1. `PreparedFlowSchemaIdentity`;
2. `ManifestId`;
3. `FlowId`;
4. `SystemId`;
5. consumer `RepositoryId`;
6. `TraceEvidence`;
7. `CodeLogicDocumentVersions`.

The three identity slots have one authoritative source and no caller override:

- `PreparedFlowSchemaIdentity` is the only closed mapping from accepted exact-
  profile schema/version `1.0` to the exact supported-contract literal
  `vulgata.prepared-pass2-flow/1.0`; other versions are refused;
- `ManifestId` is deterministic UUIDv8-SHA256 over a versioned,
  domain-separated canonical envelope of that schema identity plus the
  accepted profile ID/version and raw accepted profile fingerprint; and
- `FlowId` is deterministic UUIDv8-SHA256 over a separate canonical envelope
  of the derived Manifest ID, accepted occurrence/direction, current common
  System, ordered consumer/provider runtime bindings (including refs, commits,
  Profiled Revisions, Repository/ScanRun/Scan execution identities, aliases,
  and stable Git identities), and accepted exact relationship identity.

UUIDv8-SHA256 uses the first 16 SHA-256 digest bytes, sets the version-8 and
RFC-variant bits, and interprets the result in RFC network byte order. This
preserves the slots' schema/manifest/flow meanings without retaining or
fabricating the rejected fixed-manifest GUIDs.

Canonical encoding is the binary TLV codec
`vulgata.prepared-pass2-binding-c14n/1`: ASCII domain magic, NUL, version byte
1, then unique strictly increasing `u16be` tags with `u32be` byte lengths.
UUIDs are RFC-network-order bytes; integers/counts are big-endian; text is
strict UTF-8 without BOM and must already be NFC; exact lowercase hex is
decoded to raw bytes; lists have an explicit big-endian count and
length-delimited items. The support envelope's exact tags and nested record
orders are normative in [data-model.md](data-model.md). Its SHA-256 is rendered
as exactly 64 lowercase hex characters. No JSON serialization, UUID text,
.NET mixed-endian Guid bytes, normalization-on-write, alternate order, or
unknown field/version is accepted.

Planner tests use an independently constructed, hard-coded golden canonical
byte sequence with pinned expected Manifest UUID, Flow UUID, and lowercase
support fingerprint. Runner and committer tests separately recompute from
current authoritative state and reject the old fixed-manifest IDs, random or
substituted IDs, changed binding/support/trace fields, uppercase or malformed
fingerprints, reordered/duplicate list items, non-NFC text, wrong envelope
version/tag order, and mixed-endian UUID bytes before dispatch and before
commit.

There is no eighth dispatch slot. `CodeLogicDocumentVersions` contains the
exact ordered versions actually used across the two authorized supporting
Repositories; `GENERATED_FROM` targets that same used set. The fixed
order/inventory manifest is removed as production authority and may remain
only as an explicit non-production fixture. Runtime database identities are
never copied into the reviewed profile or fabricated by the harness.

**Rationale**: The final product-service-only path creates new database
identities and therefore cannot satisfy the existing fixed manifest singleton.
The accepted exact profile already contains the stable semantic selectors,
pinned refs/commits, direction, invocation, provider declaration, and
implementation evidence needed to bind the real current rows without a second
configuration. Keeping the seven-slot payload unchanged preserves the accepted
Worker contract, while a server-only fingerprint lets the runner/committer
detect drift or substitution before one Pass 2 dispatch/commit. The design is
graph-first because it consumes the already committed exact Edge; it does not
rediscover, infer, traverse, or cluster graph truth.

**Alternatives considered**:

- Keep the fixed production singleton and force its GUIDs into the clean
  database: rejected because product constructors create identities and
  fixture SQL, direct `DbSet`, reflection, or fabricated identifiers violate
  the release contract.
- Add an eighth Worker payload field for the support fingerprint or runtime
  bindings: rejected because the accepted dispatch contract is exactly seven
  slots and the Worker does not need persistence identities to explain the
  prepared evidence.
- Generate new random Manifest/Flow GUIDs per run: rejected because retries,
  runner validation, and committer validation require independently
  reproducible identity.
- Hash JSON, Guid text, or platform-native Guid bytes: rejected because
  property order, escaping, casing, and .NET mixed-endian layout are not a
  byte-stable cross-process contract.
- Add a second `mbs_account`/`mbs_channel` manifest or profile: rejected because
  the exact-resolution profile is already the reviewed selector authority and
  duplicate configuration can drift.
- Implement general entry-point tracing, flow discovery, or clustering:
  rejected as Epic 5 ownership and unnecessary for the single accepted
  deadline flow.

## R12. Deterministic actual ordinary Pass 1 Worker Job Identity

**Decision**: Replace random identity creation only for newly dispatched
ordinary Pass 1 jobs with a Core-owned canonical derivation. The actual
`WorkerAgentJobPacket.JobId` is UUIDv8-SHA256 over a version-1 envelope with
ASCII magic `VULGATA-ORDINARY-PASS1-JOB` and exactly five increasing TLV
fields: Scan execution UUID, ScanRun UUID, Repository UUID, Code Unit UUID,
and raw decoded full Profiled Revision bytes. The envelope reuses the exact
binary rules of `vulgata.prepared-pass2-binding-c14n/1`: magic + NUL + version
byte, `u16be` tag, `u32be` length, RFC-network-order UUIDs, and exact lowercase
hex decoded to raw bytes. The separate magic is the Pass 1 collision domain;
the envelope version is the derivation contract version.

Profiled Revision input must already be one full lowercase Git object ID of
exactly 40 or 64 hexadecimal characters. It is encoded as 20 or 32 raw bytes,
not text. Empty/abbreviated/odd-length/uppercase/trimmed input, JSON, unknown
tags or versions, alternative field order, UUID text, and .NET mixed-endian
UUID bytes are rejected without repair. UUIDv8-SHA256 uses the same first-16-
digest-byte, version-bit, RFC-variant-bit, and network-order algorithm as the
prepared runtime Manifest/Flow derivation.

The sequential runner derives the Job ID immediately before building the
ordinary Pass 1 packet. The runtime prepared-flow planner independently
reconstructs the five inputs from current authorized product rows and derives
the same historical ID; it accepts no member Job ID from a caller or fixture.
Because successful Pass 1 clears `ScanExecution.InFlightJobId`, the planner
also requires the selected current Code Logic Document Version ID to equal the
existing ordinary-committer projection
`CreateStableId("pass1-document-version", derivedJobId.ToString("D"))`. This
one-way forward check proves the Document Version was committed for that
actual deterministic job. It neither reverses a hash nor adds persistence.

The independent vector in [data-model.md](data-model.md) fixes 142 canonical
bytes, SHA-256
`6b9362f7193b38d94d9cb2a21ecfe2fd5ffe6a3aaaa00bd8c787e8efccdc71da`,
and UUID `6b9362f7-193b-88d9-8d9c-b2a21ecfe2fd`. Tests construct the
expected bytes independently of the production codec and prove repeat
identity, every bound-input mutation, delimiter-ambiguous tuple separation,
Pass 1-versus-Manifest/Flow domain separation, malformed/mixed-endian
rejection, and duplicate/same-ID-different-boundary collision refusal.

**Rationale**: The accepted runtime member contract means the actual Pass 1
Worker job, not a planner-created correlation identity. Ordinary successful
Pass 1 currently creates a random ID, temporarily records it in
`InFlightJobId`, clears it after validation, and persists only a one-way
Document Version projection. A canonical actual dispatch identity plus the
existing forward projection makes the historical identity independently
verifiable on a clean run without an EF change. Stable re-derivation also
makes a restarted attempt at the same logical boundary identifiable, but does
not add, authorize, or modify retry scheduling or duplicate-commit behavior.

Compatibility is fail-closed: existing random-ID Documents are not rewritten
or backfilled. An unsupported legacy row whose Document Version projection
does not match the canonical derived ID cannot enter the prepared Pass 2 flow
and produces zero Pass 2 dispatches and zero generated Documents. The final
release rehearsal begins from a clean database, so its ordinary Pass 1 jobs
all use the amended contract.

**Alternatives considered**:

- Invent a synthetic planner member ID: rejected because `JobId` and
  `MemberJobId` mean the actual Worker job throughout the runner, resolver,
  trace evidence, and result contract.
- Reverse the current Document Version ID: rejected because it is a one-way
  SHA-256-derived projection; the safe check derives forward from current
  authoritative inputs.
- Persist or backfill the old random Job ID: rejected because it requires an
  EF entity/configuration/migration/snapshot change outside the accepted
  no-migration boundary.
- Concatenate strings or hash JSON/platform-native `Guid` bytes: rejected
  because delimiter ambiguity, text normalization/property order, and .NET
  mixed-endian layout do not define a cross-process canonical contract.
- Add the derived identity or fingerprint as an eighth Pass 2 field: rejected
  because member Job IDs already remain in server-owned runtime support and
  the Worker-facing Pass 2 payload is exactly seven top-level slots.
- Treat deterministic identity as a retry policy: rejected; dispatch timing,
  retry, scheduling, concurrency, and duplicate-commit policy are unchanged.

## R13. Planned-change authority and expedited workflow advancement

**Decision**: Preserve the prior Chat, runtime Pass 2, and deterministic
ordinary Pass 1 authorizations and the already granted Option A plan-to-tasks
authorization recorded at `2026-08-10T01:12:29Z` in `workflow-state.json`.
Because Option A materially relocates accepted Scan execution, a fresh
conditional plan review remains mandatory for this corrected
`expedited-mvp` plan. The execution-level change does not alter the authorized
topology and therefore does not duplicate the Option A authorization.

After a fresh plan `ACCEPT`, the root-owned lifecycle regenerates expedited
test-first tasks without scope expansion and mechanically validates complete
requirement/task/test/security/migration coverage. T064-T071 remain non-live
RED/GREEN and verification. One fresh combined acceptance-and-quality review
then accepts the pinned candidate before T072 performs the sole live same-image
E2E. T073 independently verifies cleanup/absence and final verification.
`spec-review`, active `speckit-analyze`, and the separate `acceptance-review`
remain omitted at iteration zero. TDD, the combined review, security,
migration/no-model-delta, E2E, Docker, cleanup, and verification are not waived.

**Rationale**: The request is not a duplicate of the durable product approvals:
it is the repository-mandated gate for a newly accepted release Scan topology.

**Alternatives considered**:

- Re-request prior Chat/Pass 1/Pass 2 approvals: rejected as duplicate; the new
  request is limited to Option A relocation.
- Re-request the unchanged Option A plan-to-tasks authorization: rejected as a
  duplicate of the durable 2026-08-10 approval; fresh plan judgment remains
  mandatory.
- Use the Full-only analysis and separate review axes: rejected because the
  user explicitly switched Story 11.5 to the allowlisted `expedited-mvp` flow;
  the retained quality floor remains unchanged.
- Generate tasks from the iteration-8 `REVISE`: rejected; fresh independent
  conditional plan acceptance is required first.

## R14. Absolute provider paths and fail-fast Repository order

**Decision**: In the CodeGraphContext adapter only, use the exact committed
`workingCopyPath` as root; apply `Path.GetFullPath` containment, strict
root-plus-separator and same-root checks, reject case-fold-only ambiguity,
derive `Path.GetRelativePath`, normalize to `/`, revalidate with
`SafeProviderPath`, and require an exact full-path round trip. Perform this
before Code Unit and relationship endpoint identity/reconciliation. Safe
relative input remains accepted. Root-only, outside/sibling/alternate-root,
traversal, malformed, platform/case-ambiguous, conflicting, or non-round-trip
input fails closed, and raw absolute input is never retained.

T072 runs `mbs_channel` profile/Pass 1 before `mbs_account` for fail-fast
economics. Both remain mandatory in the same fresh nonce database before exact
Resolution, Pass 2, or Chat proof.

**Rationale**: Live 0.5.1 indexing succeeded for `mbs_channel` but emitted
exact snapshot-rooted absolute paths; the existing Repository-relative guard
then yielded zero Code Units/relationships and failed provisioner stage 26.
Adapter-local normalization restores compatibility without weakening the
domain path contract or changing Scan behavior. Channel-first ordering avoids
the approximately two-hour account profile when that seam still fails.

**Alternatives considered**:

- Relax `SafeProviderPath` globally or strip a textual prefix: rejected as
  unsafe and platform-dependent.
- Normalize only Code Units or after identity: rejected because relationship
  and language reconciliation would split one endpoint.
- Add `.cgcignore`, filtering, retry, scheduling, dispatch, or another root:
  rejected as scope expansion.
- Omit `mbs_account`: rejected because the final gate requires both
  Repositories, both Pass 1 results, exact Resolution, Pass 2, and Chat proof.
