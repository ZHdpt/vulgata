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

**Decision**: `Story115ReleaseHarness` is the sole owner. It keeps one unique
PostgreSQL 17 database alive from migrations through prepared Scan services and
the final container/browser proof; provisions Identity, scope, providers,
assignments, and knowledge through product services; shares an explicitly
configured gate-owned Data Protection ring with the final container; uses an
isolated deterministic provider fixture that listens on all interfaces in one
nonce-owned namespace; publishes equal host/container Worker and Chat port
numbers only to host `127.0.0.1`; pre-publishes that namespace's app port; and
starts the final app with `--network container:<provider>` so the stored
`127.0.0.1` provider URLs work from both host-side Scan and final-container
Chat. It builds the final image once, supplies a `curl` health command through
`docker run`, and drives real Identity plus the Blazor circuit with test-only
Microsoft.Playwright 1.61.0/Chromium after an explicit Release test-project
restore/build. Cleanup drops and proves absence before removing all nonce-owned
resources.

**Rationale**: Each predecessor explicitly hands these final gates to Story
11.5. Story 11.4 fixtures do not substitute for actual prepared Repository
revisions or final image evidence.

**Alternatives considered**:

- Reuse predecessor integration evidence: rejected by the Story 11.4 handoff.
- Use a shared development database: rejected because isolation and verified
  cleanup are acceptance criteria.
- Use `HttpClient`, SSR markup, or bUnit as final chat evidence: rejected
  because none proves an interactive-server circuit in the accepted image.
- Rely on compose `wget` or an image healthcheck: rejected because `wget` is
  absent and the Dockerfile has no `HEALTHCHECK`; runtime `curl` is present.
- Insert encrypted providers directly: rejected because ciphertext must be
  produced by the same Data Protection ring/application discriminator that the
  runtime uses.

## R10. Constitution and ADR result

**Decision**: Proceed with no exception or Complexity Tracking entry.

**Rationale**: The design respects Clean Architecture, immutable Document
Versions, Chinese UI, integration-first tests, encrypted secrets, source
isolation, and authorization. No applicable ADR was found at the pinned
boundary. Canonical terms come from `docs/UBIQUITOUS_LANGUAGE.md`.

**Alternatives considered**: None; a discovered conflicting ADR or need for a
new persistence model would stop planning and require escalation.
