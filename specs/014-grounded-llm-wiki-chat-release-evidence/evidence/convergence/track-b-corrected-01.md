# Story 12.5 Track B convergence 01

## Identity and scope

- Candidate: `9db9b344d897368683236c0eaaabaff655ee0bb5`
- Branch: `demo0831/story-12-5`
- Dependency: Story 12.4 accepted commit `4798b97d9f23aef587829175df7ab7ebc3daf995` is ancestral.
- Intent sources: accepted `spec.md`, `plan.md`, `tasks.md`, `.specify/memory/constitution.md`, planned-change authorization 04, implementation approval 03, definition review 08, and the accepted B1-B4/B-INT evidence.
- Scope: B1-B4 and B-INT convergence only. This report does not start quality review, E2E, final Track B verification, or Track C.

## Checked requirements and decisions

The convergence assessment checked all Track B functional requirements FR-001 through FR-032 and FR-053 through FR-060, success criteria SC-001 through SC-006 and SC-015 through SC-017, the B1-B4/B-INT task obligations T008-T037, and the governing AD-1 through AD-7C decisions. The constitution was checked for the applicable bounded scope, authorization/currentness, test-first, no-migration, ownership, and client-boundary MUST constraints.

The implementation and accepted evidence establish the following current boundaries:

- B1 has one constructor-composed lexical -> relationship -> packet path. The lexical projector owns primary selection; the relationship projector only adds optional complete support; `ChatEvidencePacketBuilder` is the sole packet construction path. The retriever has no `ExactCrossRepositoryEdgeEvidence` query or legacy exact-profile authority query in its production Chat path.
- B2 uses current `DocumentEdge`, accepted v2 exact decisions, `GENERATED_FROM`, and Published Flow Groups. Decision-set count/fingerprint and two decision-owned Flow Groups are covered, as are stale, wrong-scope, wrong-direction, missing, unpublished, incomplete, and mutation outcomes. Packet 2.2 binds advisory authority, selection identities, decision-set authority, Flow Group bindings, and conversation state.
- B3 retains bounded transient state, re-fetches and revalidates current evidence and authorization for follow-ups, applies only the eligible prior-Document hint, and does not activate failed state.
- B4 uses the dedicated raw streaming transport for primary and syntax repair under one deadline. Raw/SSE/received/decoded/final domains, UTF-8, terminal, inactivity, cancellation, timeout, and overflow behavior are covered. The Web publication candidate is preallocated; the owner has one synchronous commit operation and a single terminal atomic transition. Page reveal reads only the accepted commit and remains Unicode-safe.
- B-INT resolves the scoped publication owner and accepted B1-B4 ports, verifies Business and IT initial/follow-up traversal counts, verifies exactly-once follow-up re-fetch and publication commit, and verifies no Track C sink or alternate live Conversation State owner.
- No EF migration, `VulgataDbContext`, model snapshot, Story 12.4 contract, Track C sink, release field, vector search, or autonomous retrieval loop is present in the accepted Track B composition.

## Finding F1

- Classification: `partial`
- Severity: `HIGH`
- Sources: plan AD-3 (canonical selected decision is the first independently valid decision in the ordered set); grounded-chat-v2 contract (the relationship binds the first decision while retaining distinct decision-owned Flow Groups); FR-014, FR-019, SC-001, SC-002.
- Evidence: `src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs` currently computes `selectedDecisionId` from `projectedFlowGroups.OrderBy(group => group.FlowGroupId)` before falling back to `exactDecisionSet.SelectedDecision.Id`. The Flow Group ID is not the decision-set ordering key. With two valid decisions sharing one Edge and Flow Group IDs whose order is opposite to the canonical decision order, the relationship witness can be noncanonical. `ChatEvidenceCurrentnessValidator.HasAcceptedExactDecision` requires the relationship witness to equal `decisionSet.SelectedDecision.Id`, so the same valid packet can then be refused at render-time; alternatively, a wrong decision witness could be exposed if another validator path did not reject it.
- Impact: this is a real current-v2 relationship-authority gap despite the green shared-edge test, because the existing fixture's Flow Group IDs happen to agree with the canonical decision order. Green counts do not prove the AD-3 invariant for adversarial canonical ordering.
- Required correction: T054 adds the reversed-order RED vector in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` and the minimal GREEN correction in `src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs`. It must derive `selectedExactDecisionId` solely from `ChatExactDecisionSet.SelectedDecision`, keep all independently valid decision-owned Published Flow Groups, and preserve currentness/fingerprint behavior. No Story 12.4 contract or persistence change is authorized.

## Outcome

**NOT_CONVERGED** — one actionable HIGH partial gap was found and appended as T054 under the append-only `Phase 11: Convergence` section in `tasks.md`. All other checked B1-B4/B-INT obligations have current implementation/evidence coverage or are correctly gated as later review/E2E/Track C work. Run the T054 RED/GREEN task, then rerun convergence before quality review.

## T054 correction execution

- Honest RED: the reversed Flow Group ID fixture expected the canonical selected decision `acb4000f...`, but the projector selected `0a5a2d82...`; the retrieval suite reported **51 passed / 1 expected RED**.
- Minimal GREEN: `ChatRelationshipEvidenceProjector` now derives `selectedDecisionId` only from `ChatExactDecisionSet.SelectedDecision.Id`. It does not reorder, collapse, or discard independently valid decision-owned Published Flow Groups.
- One adjacent currentness characterization was corrected because it treated `packet.FlowGroups[0]` as selected-decision authority. It now binds the relationship to the canonical decision set and separately proves both decision-owned Flow Groups remain Published and edge-bound.
- Final focused evidence: `ChatEvidenceRetrievalTests` **52/52**, `ChatEvidenceCurrentnessTests` **49/49**, and the combined retrieval/currentness/packet suite **132/132**.
- `git diff --check` is clean apart from informational LF-to-CRLF notices. No migration, persistence, Story 12.4 contract, or authority change was required.

T054 execution closes F1 locally but does not rewrite this independent attempt's `NOT_CONVERGED` verdict. A fresh convergence pass against the corrected candidate remains mandatory.

## Verification evidence

- Focused B1-B4/B-INT test matrix executed at this candidate: 354 passed, 2 failed, 0 skipped. Both failures were the known sandbox-only ASP.NET Data Protection default key-path characterizations under `C:\Users\13728\AppData\Local\ASP.NET\DataProtection-Keys`; the controlled-key-ring variants passed. No Story 12.5 assertion failed in this run.
- Web and test projects built successfully before test execution; existing NU1507/NU1900/MSB3277 warnings remain unchanged.
- Existing B-INT evidence records 409/409 after excluding exactly the two host-path failures, 103/103 Track A status/UI/corpus/evidence tests, and 8/8 authenticated composition tests.
- Existing static guards report one packet builder, one publication-owner implementation and scoped registration, no legacy exact Chat authority, no Track C sink/release fields, no migration/model-snapshot/Story 12.4 mutation, no live coordinator Conversation State field, one candidate-only `AppendValidatedTurn`, and one follow-up read from the committed composite.
- `git diff --check` is clean for the current candidate.
