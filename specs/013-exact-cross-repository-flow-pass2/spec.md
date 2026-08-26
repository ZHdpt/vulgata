# Feature Specification: Exact Cross-Repository Flow & Pass 2

**Feature Branch**: `demo0831/story-12-4`

**Created**: 2026-08-26

**Status**: Draft

**Input**: Story 12.4 converts exact current communication evidence from the two release Repositories into deterministic Cross-Repository REFERENCES Edges, bounded endpoint-centered Flow Groups, and immutable Business Logic Document Versions with complete GENERATED_FROM provenance. It must preserve honest Uncertainties whenever the exact provider decision is not unique and must not absorb Story 12.5 Chat, UI, or release-evidence work.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Resolve Exact Cross-Repository Communication (Priority: P1)

As a SystemOwner, I can resolve communication evidence across the two release Repositories and trust that every Cross-Repository REFERENCES Edge names the only exact, current, authorized provider rather than a plausible-looking target selected by an LLM or graph proximity.

**Why this priority**: Pass 2 can explain a business flow only after the underlying provider decision is deterministic and honest. A fabricated or ambiguous provider would contaminate every downstream Document.

**Independent Test**: Supply current consumer and provider fixtures covering one exact provider, no provider, multiple providers, conflicting roles, stale evidence, unsupported communication, and unsafe HTTP normalization. Run resolution repeatedly in different Repository-completion orders and verify identical Edges and Uncertainties.

**Acceptance Scenarios**:

1. **Given** current authorized consumer and provider evidence in the two release Repositories has the same protocol and exact normalized endpoint identifier, and exactly one current provider is eligible, **When** Cross-Repository Resolution runs, **Then** it creates one directed REFERENCES Edge from the consumer Document Version to that provider Document Version.
2. **Given** the endpoint identifier is equal but the protocols differ, **When** resolution runs, **Then** no Cross-Repository REFERENCES Edge is created and the unresolved communication remains an honest Uncertainty.
3. **Given** there is no provider, more than one provider, conflicting evidence, stale evidence, an unsupported communication kind, or an HTTP endpoint that cannot be normalized safely, **When** resolution runs, **Then** no provider is chosen and an honest Uncertainty records the unresolved outcome.
4. **Given** identical current evidence is presented in a different Repository-completion or enumeration order, **When** resolution runs, **Then** it produces the same canonically ordered Edge and Uncertainty outcomes.
5. **Given** raw CodeGraph proximity or an LLM response suggests a different provider, **When** resolution runs, **Then** the suggestion has no effect on provider eligibility or the deterministic outcome.

---

### User Story 2 - Plan Bounded Endpoint-Centered Flow Groups (Priority: P1)

As a SystemOwner, I receive a small, exact Flow Group for each complete resolved communication so that Pass 2 explains the relevant business process without wandering into nearby but unsupported code.

**Why this priority**: Exact resolution is valuable only if downstream planning preserves its boundary. Endpoint-centered membership keeps source volume, provenance, and generated claims auditable.

**Independent Test**: Build resolved fixtures with eligible and ineligible Documents, extra same-Repository neighbors, stale revisions, missing Source Anchors, unauthorized members, and raw graph-proximity candidates. Verify each deterministic key, membership set, ordering, cardinality bound, and omission reason.

**Acceptance Scenarios**:

1. **Given** an exact Cross-Repository REFERENCES Edge and all required current Documents exist, **When** Pass 2 planning runs, **Then** it creates a Flow Group identified by System, protocol, normalized endpoint identifier, consumer entry, and provider Repository.
2. **Given** a complete Flow Group, **When** membership is assembled, **Then** it contains only the relevant consumer Code Logic Document Version, exact REFERENCES Edge, provider XML Code Logic Document Version, provider implementation Code Logic Document Version, and at most one hop of validated same-Repository REFERENCES Edges and their target Document Versions.
3. **Given** a candidate member is stale, unauthorized, unanchored, bound to another Profiled Revision, outside the authorized System, or present only through raw CodeGraph proximity or LLM output, **When** membership is evaluated, **Then** the candidate is excluded.
4. **Given** the initial release bounds would be exceeded, **When** planning evaluates the candidate set, **Then** it stops within two Repositories, eight Code Logic Document Versions, sixteen evidence steps, ten Flow Groups, and one relationship hop, and reports the bounded omission instead of silently truncating an explanation into a false complete group.
5. **Given** the same exact evidence is planned repeatedly, **When** Flow Groups are compared, **Then** their identities, members, evidence order, and complete/incomplete decisions are identical.

---

### User Story 3 - Retrieve Closed Supporting Knowledge (Priority: P2)

As a Pass 2 Worker Agent, I can use `retrieve_code_logic` to obtain a bounded set of current Code Logic Document sections and Source Anchor-backed source inside the authorized System, so that I can explain the Flow Group without open-ended retrieval or changing its provider decision.

**Why this priority**: A complete group may require local supporting context, but retrieval must remain subordinate to the deterministic Flow Group and preserve complete provenance.

**Independent Test**: Exercise searches, section reads, Source Anchors, source excerpts, and related Documents at every call/result/Document/section/byte boundary; then attempt stale, unauthorized, cross-System, provider-changing, and over-bound retrievals. Verify closed outcomes and the exact set of Documents recorded as used.

**Acceptance Scenarios**:

1. **Given** a running Pass 2 job for an authorized Flow Group, **When** the Worker Agent calls `retrieve_code_logic`, **Then** it may receive current Code Logic Document sections, Source Anchors, bounded source excerpts, and related Documents only within that authorized System and the relevant Profiled Revisions.
2. **Given** retrieval reaches eight calls, ten results, four additional Documents, four sections per Document, or 64 KiB of cumulative returned evidence, **When** more evidence is requested, **Then** the request returns a bounded refusal and adds no evidence.
3. **Given** retrieval returns an additional Document that the Worker Agent actually uses, **When** the Business Logic Document Version is accepted, **Then** that exact current Code Logic Document Version is included in its GENERATED_FROM provenance.
4. **Given** retrieved material proposes another endpoint, provider, or Flow Group member, **When** the Worker Agent synthesizes its result, **Then** the material cannot alter the deterministic resolution fact or group membership.
5. **Given** direct Pass 2 JDT-LS or CodeGraph verification is absent or unavailable, **When** required Document and Source Anchor-backed retrieval succeeds, **Then** the Pass 2 job remains eligible to complete.

---

### User Story 4 - Generate an Immutable Business Logic Document (Priority: P1)

As a SystemOwner, I receive one trustworthy Business Logic Document Version for every successfully completed Flow Group, with complete provenance to every Code Logic Document Version actually used.

**Why this priority**: The user value of Story 12.4 is an understandable cross-Repository business explanation whose claims can be traced back to current source-grounded Documents.

**Independent Test**: Run one complete group through Pass 2 with varying retrieval choices, replay the same accepted result, mutate group/revision/authorization/provenance inputs, and verify one immutable Document Version, complete GENERATED_FROM Edges, deterministic replay, and zero partial persistence on rejection.

**Acceptance Scenarios**:

1. **Given** a complete Flow Group and sufficient current authorized evidence, **When** its one Pass 2 Worker Agent job succeeds, **Then** one immutable Business Logic Document Version explains the bounded cross-Repository flow and cites evidence belonging to that group or to validated bounded retrieval.
2. **Given** the Worker Agent used the initial group Documents and additional retrieved Documents, **When** persistence succeeds, **Then** the Business Logic Document Version has one GENERATED_FROM Edge to every distinct Code Logic Document Version actually used and none to Documents not used.
3. **Given** any used Document is stale, unauthorized, outside the System, bound to a mismatched Profiled Revision, no longer current, or absent from the validated group/retrieval record, **When** acceptance is attempted, **Then** no Business Logic Document Version or provenance Edge is persisted.
4. **Given** the accepted result is replayed after an acknowledgement loss or process restart, **When** the same Flow Group completion is committed again, **Then** the existing immutable Business Logic Document Version is returned without duplication or mutation; changed replay conflicts without mutation.
5. **Given** the generated explanation asserts a provider or relationship that differs from the deterministic resolution and Flow Group, **When** it is validated, **Then** the result is rejected and creates no Document or Edge.

---

### User Story 5 - Complete Remaining Groups Honestly (Priority: P1)

As a SystemOwner, I can keep valid Business Logic Documents from complete groups when another Flow Group fails, while seeing exactly which flow was omitted and why.

**Why this priority**: Partial progress is useful only when incomplete evidence never becomes a fabricated business explanation and omissions remain visible.

**Independent Test**: Run a mixed set of complete, missing-document, retrieval-insufficient, provider-failed, stale, and invalid-result groups. Verify independent terminal outcomes, visible warnings, no synthesis for incomplete groups, and unchanged deterministic resolution facts.

**Acceptance Scenarios**:

1. **Given** one Flow Group lacks a required Document or required Source Anchor-backed evidence, **When** Pass 2 reaches that group, **Then** the group is omitted with a visible warning and no Business Logic Document Version is synthesized from the incomplete evidence.
2. **Given** one Flow Group job fails, **When** other complete groups succeed, **Then** their immutable Business Logic Document Versions and GENERATED_FROM Edges remain committed and the failed group cannot alter their outcomes.
3. **Given** a group is omitted or fails, **When** Pass 2 reports completion, **Then** it preserves the deterministic REFERENCES Edge or Uncertainty outcome produced before Pass 2 and identifies the omitted group without inventing replacement membership.
4. **Given** no Flow Group is complete, **When** Pass 2 finishes, **Then** it creates zero Business Logic Document Versions and reports every bounded omission honestly.

### Edge Cases

- Consumer and provider evidence share endpoint text but not protocol; they never match.
- Case, slash, encoding, or URI-component differences are accepted only when the applicable catalog rule defines one safe, deterministic normalization; otherwise the communication remains an Uncertainty.
- Two current provider candidates normalize to the same key; neither is selected, even if one is graph-nearer or preferred by an LLM.
- A previously eligible provider Document is superseded before resolution or Pass 2 commit; currentness revalidation prevents a stale Edge or Business Logic Document Version.
- Consumer and provider are in different Repositories but outside the same authorized System; they cannot form a release Flow Group.
- A same-Repository REFERENCES cycle exists at the one-hop boundary; membership remains finite and duplicate-free.
- The same Code Logic Document Version is present in the initial group and returned by retrieval; GENERATED_FROM contains one Edge for that distinct used version.
- Retrieval returns evidence that is never used in the accepted explanation; it is not added to GENERATED_FROM merely because it was returned.
- The ten-group or eight-Document-Version release bound is reached exactly; the bounded set remains eligible, while any excess candidate is reported rather than hidden.
- Optional direct analysis disagrees with current persisted Documents; it cannot silently change resolution or membership and the group is rejected or warned if mandatory evidence is no longer trustworthy.

## Requirements *(mandatory)*

### Functional Requirements

#### Exact Cross-Repository Resolution

- **FR-001**: Cross-Repository Resolution MUST consume only current authorized communication observations, Code Logic Document Versions, Source Anchors, and exact same-Repository REFERENCES Edges associated with the two release Repositories and their relevant Profiled Revisions.
- **FR-002**: Provider matching MUST use the complete deterministic key `(protocol, normalized endpoint identifier)`; neither component may be omitted, fuzzily compared, or inferred by an LLM.
- **FR-003**: The release resolver MUST support catalog-driven Custom RPC and MAY resolve exact HTTP only when the applicable endpoint can be normalized safely and deterministically.
- **FR-004**: MQ, file/storage, page navigation, fuzzy matching, indirect object flow, and any unsupported communication kind MUST NOT produce a Cross-Repository REFERENCES Edge in this feature.
- **FR-005**: A Cross-Repository REFERENCES Edge MUST be created only when exactly one eligible current authorized provider Document Version matches the deterministic key.
- **FR-006**: The created REFERENCES Edge MUST be directed from the consumer Document Version to the uniquely matched provider Document Version and MUST preserve the exact resolution evidence needed to reproduce the decision.
- **FR-007**: No provider, multiple providers, conflicting evidence, stale evidence, unsafe normalization, or unsupported communication MUST create an Edge; the unresolved communication MUST create or retain an honest Uncertainty with the available identifying information.
- **FR-008**: Resolution MUST be deterministic and idempotent across evidence enumeration order, Repository completion order, restart, and exact replay.
- **FR-009**: Raw CodeGraph proximity, LLM output, and optional direct analysis MUST NOT add an eligible provider or override the deterministic provider decision.
- **FR-010**: Resolution MUST revalidate authorization, currentness, Repository, System, Profiled Revision, endpoint, protocol, and evidence integrity before creating or retaining an outcome.

#### Endpoint-Centered Flow Groups

- **FR-011**: Pass 2 planning MUST begin only from deterministic current resolution outcomes and MUST NOT reinterpret an Uncertainty as a resolved relationship.
- **FR-012**: Each Flow Group MUST have a deterministic identity derived from its System, protocol, normalized endpoint identifier, consumer entry, and provider Repository.
- **FR-013**: A Flow Group MAY contain only the relevant consumer Code Logic Document Version, the exact Cross-Repository REFERENCES Edge, the provider XML Code Logic Document Version, the provider implementation Code Logic Document Version, and at most one hop of validated same-Repository REFERENCES relationships and target Code Logic Document Versions.
- **FR-014**: Every Flow Group member MUST be current, authorized, Source Anchor-backed where source evidence is required, and bound to the applicable Profiled Revision.
- **FR-015**: Raw CodeGraph proximity, LLM selection, fuzzy relationship evidence, and unrelated Documents MUST NOT add Flow Group members.
- **FR-016**: Initial Pass 2 planning MUST remain within two Repositories, eight distinct Code Logic Document Versions, sixteen evidence steps, ten Flow Groups, and one relationship hop.
- **FR-017**: When a required Flow Group member is missing or invalid, the planner MUST mark the group incomplete and state the bounded reason rather than synthesizing a smaller group as if it were complete.
- **FR-018**: Repeated planning over identical validated inputs MUST yield byte-for-byte equivalent ordered group identities, membership, evidence, and completeness outcomes.
- **FR-019**: Exactly one durable Pass 2 Worker Agent job MUST be associated with each complete Flow Group, and no job may be dispatched for an incomplete group.

#### Closed Supporting-Knowledge Retrieval

- **FR-020**: A Pass 2 Worker Agent MUST receive typed access to `retrieve_code_logic` for bounded search, current Code Logic Document sections, Source Anchors, Source Anchor-backed source excerpts, and related Documents within the authorized System.
- **FR-021**: `retrieve_code_logic` MUST reject stale, superseded, unauthorized, cross-System, mismatched-Profiled-Revision, or otherwise unvalidated evidence without disclosing excluded content.
- **FR-022**: One Pass 2 job MUST accept at most eight retrieval calls, ten returned results, four additional Documents, four sections per Document, and 65,536 cumulative UTF-8 bytes.
- **FR-023**: Duplicate results and repeated requests MUST count consistently toward bounds and MUST NOT bypass the distinct-Document or byte limits.
- **FR-024**: Reaching any retrieval bound MUST produce a closed bounded outcome and MUST NOT add evidence beyond that bound.
- **FR-025**: Retrieval MUST NOT change the deterministic endpoint/provider decision, Flow Group identity, or Flow Group membership.
- **FR-026**: The accepted Pass 2 result MUST distinguish evidence returned from evidence actually used so that provenance includes all and only used Code Logic Document Versions.
- **FR-027**: Every additional Document actually used MUST be revalidated for currentness, authorization, System, Repository, Profiled Revision, and evidence integrity immediately before Business Logic Document persistence.
- **FR-028**: Direct Pass 2 JDT-LS and CodeGraph verification MUST remain optional; removing or disabling it MUST NOT weaken mandatory current Document, Source Anchor, and source-excerpt retrieval.

#### Business Logic Documents and Provenance

- **FR-029**: A complete Flow Group MUST dispatch at most one active Worker Agent job at a time and MUST produce at most one current immutable Business Logic Document Version for that group completion.
- **FR-030**: A Business Logic Document Version MUST explain only the bounded cross-Repository business flow supported by its validated Flow Group and actually used retrieved evidence.
- **FR-031**: Every material technical or relationship claim in a Business Logic Document Version MUST be supported by a used current Code Logic Document Version and its Source Anchor-backed evidence.
- **FR-032**: Each accepted Business Logic Document Version MUST have one GENERATED_FROM Edge to every distinct Code Logic Document Version actually used, including initial Flow Group members and validated additional retrieved Documents.
- **FR-033**: A GENERATED_FROM Edge MUST NOT be created for a Document merely because it was eligible, grouped, or returned; the Document must have been actually used by the accepted result.
- **FR-034**: Business Logic Document Version persistence, GENERATED_FROM Edge persistence, job completion, and current-version publication MUST be additive and transactional so that either the complete accepted result becomes visible or none of it does.
- **FR-035**: Exact replay MUST return the existing immutable Business Logic Document Version without mutation or duplication, while a changed replay for the same completion identity MUST conflict without mutation.
- **FR-036**: Validation MUST reject any result that changes the deterministic provider, introduces an unsupported relationship, cites evidence outside the validated group/retrieval record, or omits required GENERATED_FROM provenance.
- **FR-037**: Previous Document Versions MUST remain preserved through the Version Chain and MUST never be edited or deleted by Pass 2.

#### Honest Partial Completion and Scope

- **FR-038**: A failed or incomplete Flow Group MUST create no Business Logic Document Version and MUST produce a visible warning that identifies the group and a bounded failure or omission reason without exposing secrets or unauthorized source.
- **FR-039**: One failed or incomplete Flow Group MUST NOT roll back already accepted Business Logic Document Versions from other complete groups.
- **FR-040**: Pass 2 failure or omission MUST NOT rewrite the deterministic REFERENCES Edge or Uncertainty outcome that preceded group execution.
- **FR-041**: Terminal Pass 2 reporting MUST distinguish successful groups, failed groups, incomplete groups, and groups excluded by release bounds.
- **FR-042**: Prompts used by Pass 2 Worker Agents MUST remain externalized configurable text resources, and provider use MUST honor the configured source-data boundary.
- **FR-043**: This feature MUST NOT add Chat retrieval or synthesis, production progress UI, final August release evidence, vector/embedding retrieval, unrestricted Agent tool loops, autonomous multi-hop retrieval, or Story 12.5 behavior.

### Key Entities

- **Communication Resolution Candidate**: A current authorized consumer or provider observation identified by protocol, normalized endpoint identifier, Repository, Document Version, Source Anchors, Profiled Revision, and role evidence. It is evidence for deterministic resolution, not itself an Edge.
- **REFERENCES Edge**: The typed directed relationship between the exact consumer and unique current provider Document Versions. It is cross-Repository because its source and target belong to different Repositories, not because it uses a separate edge type.
- **Uncertainty**: The honest unresolved outcome retained when deterministic evidence cannot identify exactly one current provider. It carries available protocol, endpoint, target, and reason information without pretending a relationship exists.
- **Flow Group**: The deterministic endpoint-centered Pass 2 work boundary identified by System, protocol, normalized endpoint identifier, consumer entry, and provider Repository, with a closed current membership set.
- **Business Logic Document Version**: One immutable generated explanation of a successfully completed Flow Group, published through a stable Logical Document and preserved through its Version Chain.
- **GENERATED_FROM Edge**: A provenance Edge from one Business Logic Document Version to each distinct Code Logic Document Version actually used to synthesize it.
- **Pass 2 Job**: The durable execution record for one complete Flow Group, including its bounded retrieval usage, terminal outcome, and immutable completion identity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across exact-match, protocol-mismatch, zero-provider, two-provider, conflicting, stale, unsupported-kind, and unsafe-HTTP fixtures, 100% of Cross-Repository REFERENCES Edges target the sole exact current provider and 100% of non-unique cases retain an Uncertainty with zero guessed targets.
- **SC-002**: Across at least ten randomized Repository-completion and evidence-enumeration orders, resolution produces the same canonically ordered Edge and Uncertainty set with zero duplicate Edges.
- **SC-003**: Every planned Flow Group is reproducibly keyed by exactly one System, protocol, normalized endpoint identifier, consumer entry, and provider Repository; 100% of members are current, authorized, anchored as required, and bound to the relevant Profiled Revision.
- **SC-004**: Boundary trials never exceed two Repositories, eight distinct Code Logic Document Versions, sixteen evidence steps, ten Flow Groups, or one same-Repository relationship hop, and every excluded or incomplete group has a visible bounded reason.
- **SC-005**: In over-bound and adversarial retrieval trials, no Pass 2 job exceeds eight calls, ten results, four additional Documents, four sections per Document, or 65,536 returned UTF-8 bytes, and zero rejected evidence becomes available to the Worker Agent.
- **SC-006**: In 100% of retrieval trials, provider decisions and Flow Group memberships are identical before and after retrieval, including when returned content suggests another provider.
- **SC-007**: Every accepted Business Logic Document Version has exactly one GENERATED_FROM Edge for each distinct Code Logic Document Version actually used and zero GENERATED_FROM Edges for Documents only eligible, grouped, or returned but unused.
- **SC-008**: In stale, unauthorized, mismatched-revision, changed-replay, unsupported-relationship, and incomplete-provenance trials, persistence creates zero partial Business Logic Document Versions, provenance Edges, or completed-job state.
- **SC-009**: In exact replay, concurrent completion, acknowledgement-loss, and restart trials, each successful Flow Group has exactly one immutable Business Logic Document Version and all previous versions remain preserved.
- **SC-010**: When optional direct JDT-LS and CodeGraph verification is disabled, 100% of groups with sufficient mandatory Document and Source Anchor-backed evidence remain eligible and produce the same deterministic provider and membership outcomes.
- **SC-011**: In mixed runs containing successful, failed, incomplete, and bound-excluded groups, 100% of successful groups remain committed, 100% of unsuccessful groups produce zero Business Logic Document Versions, and terminal reporting accounts for every planned candidate group.
- **SC-012**: Release qualification over the approved two Repositories produces at least one source-grounded Business Logic Document Version spanning an exact Cross-Repository REFERENCES Edge, with every material relationship claim traceable through GENERATED_FROM to current Code Logic Document Versions and Source Anchors.

## Assumptions

- Story 12.1 remains authoritative for durable Scan/Job identity, leases, transactional additive persistence, replay, restart recovery, authorization revalidation, and immutable Document Version publication.
- Story 12.2 remains authoritative for the exact two-Repository Prepared Scan Manifest pair, Profiled Revision identity, File Code Unit/source hashes, Communication Kind Classifications, safe catalog evidence, and two-Repository release boundary.
- Story 12.3 remains authoritative for current immutable Code Logic Document Versions, Source Anchors, deferred communication observations, exact same-Repository REFERENCES Edges, and terminal Pass 1 eligibility. Story 12.4 consumes these facts and does not rewrite their Document bodies.
- The Communication Pattern Catalog supplies the deterministic protocol-specific endpoint normalization rules. If no rule can normalize an observed HTTP endpoint without losing meaning, the safe default is an Uncertainty.
- A consumer entry is the current source-anchored consumer Code Logic Document context from which the exact communication observation originates; planning will define its canonical identity without changing this meaning.
- A Flow Group is complete only when the consumer Document, exact Cross-Repository REFERENCES Edge, provider XML Document, and provider implementation Document required by the prepared release flow are all current and validated. Optional one-hop members and optional direct analysis are not completeness prerequisites.
- Visible warnings reuse the durable bounded scan outcome channel established by earlier stories; Story 12.4 requires truthful content and accounting, not a new production UI.
- The configured Worker Agent provider is authorized to receive the bounded source/context for this System. Provider selection, credentials, balance handling, and general failover policy are not changed by this specification.

## Explicit Exclusions

- Story 12.5 Chat, Business/IT retrieval, answer synthesis, citations UI, production progress UI, and final August release evidence.
- MQ, file/storage, page navigation, fuzzy endpoint matching, indirect object-flow inference, and arbitrary protocol resolution.
- LLM-selected provider decisions, LLM-selected Flow Group membership, raw CodeGraph proximity membership, general graph clustering, and unrestricted/multi-hop Agent exploration.
- Vector databases, embeddings, autonomous RAG loops, and general LLM-Wiki retrieval.
- Direct Pass 2 JDT-LS or CodeGraph verification as a mandatory completion dependency.
- Rewriting or deleting Code Logic Documents, Business Logic Documents, REFERENCES Edges from accepted deterministic resolution, or historical Document Versions.
- General V1 bidirectional resolution beyond the approved exact Custom RPC and safely normalizable exact HTTP boundary.

## Authority Alignment

- **BMad Story 12.4** supplies the exact resolution, endpoint-centered grouping, bounded retrieval, provenance, and partial-completion acceptance boundary represented by FR-001 through FR-043.
- **August 30 bounded-scanning design and approved recovery proposal** replace broad PRD FR-4.5 and general FR-6.1/FR-6.2 for this dated release with endpoint-centered grouping and exact Custom RPC/safe HTTP resolution. The full long-term V1 requirements remain backlog rather than being deleted.
- **PRD FR-4.5** remains authoritative for one Worker Agent per Flow Group, Business Logic Document synthesis, and GENERATED_FROM Edges to every Code Logic Document Version actually used, as narrowed by the dated release overlay.
- **PRD FR-6.1 and FR-6.2** remain authoritative for honest Uncertainties and post-scan deterministic Cross-Repository Resolution, as narrowed to the two release Repositories and the exact deterministic key.
- **Ubiquitous Language and Constitution** remain peer authorities: a cross-Repository relationship is a REFERENCES Edge between Document Versions in different Repositories, not a separate Edge type; Documents are immutable; old versions remain in Version Chains; and canonical Cross-Repository terminology is mandatory.
- **Story 12.5 is explicitly excluded**. No requirement in this specification authorizes Chat, final progress UI, or release-evidence implementation.
- **No unresolved authority conflict or NEEDS CLARIFICATION remains**. Planning must expose the repository planned-change triggers for the Cross-Repository Resolution strategy, Flow Group planning/dispatch, and `retrieve_code_logic`, but those are later workflow gates rather than specification ambiguity.

### Authority Source Traceability

| Specification coverage | Authoritative sources |
| --- | --- |
| Story intent, four acceptance groups, bounds, dependencies, and Story 12.5 exclusion | `docs/bmad/epics.md` Story 12.4; `docs/bmad/implementation-artifacts/12-4-exact-cross-repository-flow-and-pass-2.md` |
| Deterministic key, unique-provider rule, exact protocol boundary, grouping, retrieval bounds, optional analysis | `docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md` sections 11-12 |
| Dated replacements for broad FR-4.5 and FR-6.1/FR-6.2 | `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md` |
| Long-term Pass 2, GENERATED_FROM, Uncertainty, and resolution intent | `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md` FR-4.5, FR-6.1, and FR-6.2 |
| Canonical terms, immutable Documents, authorization, source stewardship, and planned-change governance | `docs/UBIQUITOUS_LANGUAGE.md`; `.specify/memory/constitution.md`; `AGENTS.md` |
| Durable execution, Prepared Scan Manifest, exact current Code Logic Documents, Source Anchors, and same-Repository reconciliation inputs | `specs/010-durable-scan-foundation/spec.md`; `specs/011-java-xml-prescan-manifest/spec.md`; `specs/012-resumable-file-pass1-code-logic/spec.md` |

## Scope Boundaries and Planning Triggers

- Planning must identify the exact additive seams for resolution outcomes, Flow Group identity/membership, bounded retrieval accounting, Pass 2 job replay, transactional Business Logic Document persistence, and complete GENERATED_FROM provenance without changing the WHAT and WHY in this specification.
- The plan requires independent plan review because it materially adds a Cross-Repository Resolution strategy, a Pass 2 grouping/dispatch strategy, and the Worker Agent tool `retrieve_code_logic`. The repository's planned-change authorization gate occurs after accepted plan review and before task generation, not during specification.
- If planning discovers that an accepted Story 12.1, 12.2, or 12.3 contract cannot support a requirement, it must surface the compatibility conflict rather than silently redefining the earlier authority.
