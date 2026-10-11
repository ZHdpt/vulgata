# Existing accepted CL → Business Logic: owner handoff

Date: 2026-10-11. Scope: October first deliverable, login/registration presentation.
The user is management/coordination owner. The former integration chat is advice-only.
No other chat was contacted or dispatched for this assessment. This document is a
source-checked handoff, not a new implementation plan or runtime grant.

## Verified boundary

Assigned checkout: `C:/Users/13728/.codex/worktrees/f081/vulgata`, branch
`oct14/business`, clean starting HEAD `19a2e19c8151c08cf2d9ec593fdece45cbdfd95e`.
Latest locally available integration ref inspected:
`oct14/main` = `d623c509de19824a444babe9d55085e2220128c0`.
Business Core contracts/policies, Agents/prompt, October persistence and Knowledge
tests match that integration ref. No reset, merge, remote fetch, other-worktree
inspection or assessment of Scan's unfinished existing-CL command was performed.

The resolution, flow planner, synthesis executor and atomic BL publication store
already exist. They consume immutable accepted CL, not source files. No Business
engine replacement, new schema or migration is indicated by this inspection.
The missing integration capability must not be confused with missing BL algorithms:
**the current finalizer's dispatch contract permits only the original live
BeginGenerate admission, not a new process continuing a completed CL-only run.**

## Exact input contract — reuse these owner types

| Required input | Existing type / origin | Required semantics |
| --- | --- | --- |
| Database/target/batch selection | `KnowledgeBinding(CanonicalDatabaseIdentity, TargetId, BatchId, SelectionDigest)` | Exact retained binding, with currently authorized access to that target. An arbitrary connection string, batch label or directory name is not equivalent. |
| Frozen accepted corpus | `CorpusHandle(Binding, CorpusId, CorpusDigest)` returned by `FreezeCorpusAsync` | Exact owner-issued immutable corpus. Keep the freeze operation key and canonical digest for `IOctoberKnowledgeWriter.LookupOperationAsync`; do not reconstruct a handle from document counts or a 'latest' query. |
| Optional prior CL publication | `PublicationBinding(Knowledge, PublicationId)` | Identifies an ordinary readable CL selection. It is not a `CorpusHandle` and is not required for private producer processing of an already frozen accepted corpus. A prior publication must remain unchanged. There is no current public publication-to-corpus resolver port. |
| Accepted source identity | Stored `DocumentVersionReference`, manifest and `PreparedOrigin` | Same batch, exact logical/version IDs; RepositoryId, root binding, relative path, prepared version, digest and anchors remain owner-loaded. No source reread or rescan is required by B1/B2. Do not remap existing documents into a fresh batch. |
| Coverage and gaps | Stored `FileCoverage`, file uncertainties, resolution outcomes/dispositions and flow frontiers | Preserve failed/cancelled/unprocessed/zero-unit inputs and every manifest-qualified uncertainty. Successful CLI exit or a subset of CL documents cannot replace terminal coverage. |
| Context | Accepted qualified claims and complete stored contributor ledger | Retain source/advisory/hypothesis/uncertain qualification and all contributors, including supplemental/uncited inputs. Current synthesis receives `{ input, group, gaps }`; it has no free-form evaluation-answer input or new-context acceptance port. Presentation goals can help select roots, not manufacture facts. |
| Flow roots | `FlowRootSelection(UnitId, Document, Kind, Selector)` from owner-loaded `FlowPlanningInput.Units` | Choose exact stored unit/version/semantic selector, with supported `SelectedCodeUnit` kind. Login and registration roots remain distinct even when they share helpers. Do not guess GUIDs, use the first unit implicitly, or infer roots from names alone. Explicit empty roots means durable NoContent; default roots are invalid. |
| Fresh dispatch/commit authority | Original slot, `IOctoberExecutionScope` plus `IOctoberBusinessHandoffLifetime`, producer authorization, `IOctoberCommitSubmissionGuard` | These are independently issued host capabilities, never inferred from a stored receipt or successful read. See the dispatch decision below. |
| Provider selection | `BusinessProviderConfigurationProjection`, profile-bound `IOctoberBusinessAttemptClient`, prompt digest, token ceiling, run ID | Same frozen actual client/credential lifetime and host budget/capacity/deadline; fingerprints are not credentials or permission. No provider work is authorized by this handoff. |

A corpus is one exact batch. Two repositories can belong to it; unrelated batches
cannot be silently unioned. If Scan's accepted CL spans separate batches, report
that mismatch instead of copying identities or rescanning unchanged source.
If only accepted-file receipts exist, freezing still requires the existing trusted
freeze authority and complete settled terminal coverage. If only a publication ID
survives, a bounded owner selection/recovery API would need separate contract approval.

## Supported owner API sequence

1. Establish the exact `CorpusHandle` and current all-contributor producer authority.
   `IOctoberResolutionStore.LoadCorpusAsync` reloads accepted CL with exact provenance.
   It does not read the source repositories or generate missing CL.
2. `OctoberResolutionPolicy.Plan` derives finite inquiries and explicit dispositions.
   For genuinely outstanding inquiries, the host composes `OctoberResolutionExecutor`
   with its authorized scope/client. Accept proposals through `AcceptOutcomeAsync`
   using `ResolutionAcceptanceRequest` and `OctoberResolutionStore.ComputeAcceptanceDigest`.
   The owner recomputes evidence/outcomes; the model never creates an authoritative edge.
   Retain operation/attempt IDs and digests. Reconcile known operations through
   `LookupOutcomeAsync`, without regeneration. Existing accepted resolution must be
   reused; an unavailable read or conflicting write is not permission to overwrite it.
3. `IOctoberFlowPlanningInputLoader.LoadAsync(corpus, [])` supplies exact selectable
   units only when required resolution/provenance checks succeed. Reload with the
   selected roots for planning. Missing required resolution, bad bindings or failed
   authority yield unavailable input, not partial permission. The loader includes
   complete coverage, contributors, file uncertainties and accepted resolution.
   If partially completed B1 work exists but operation receipts were lost, current
   ports do not enumerate per-inquiry recovery receipts; surface that owner-read gap
   before blindly regenerating every inquiry.
4. `OctoberFlowPlanner.Plan` computes bounded directed groups. Current hard ceilings
   are 256 nodes, 512 edges and depth16; finalization accepts at most256 roots.
   Unsupported root kinds produce explicit dispositions, not invented classifications.
5. Construct `BusinessFinalizationSelection` and same-batch `OperationKey` with kind
   `FinalizeCorpus`; use `OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint`
   and `ComputeRequestDigest`. Call `IOctoberBusinessFinalizer.FinalizeAsync` only with
   the valid original slot and same host scope. The current wrapper loads inputs,
   synthesizes from accepted documents and submits original proposals to the owner.
6. `IOctoberFlowPublicationStore` reloads/revalidates and atomically stores BL,
   exact document/relationship/uncertainty dependencies and publication membership.
   `LookupAsync` is exact reconciliation only. Missing/unavailable rows remain
   CommitIndeterminate; they never authorize model dispatch. Failed/partial outcomes
   and LogUnavailable must remain distinct from a fully successful publication.
7. Use the returned publication with the canonical reader's `OpenReadSelectionAsync`,
   bounded search, exact reads and permitted related reads under the ordinary user's
   live subject, purpose, repository scope and **Business** mode. Producer authority
   is not reader authority. Do not substitute another publication if these reads fail.

An existing B0 CL publication is not a prerequisite for B1/B2, and it is not replaced
by them. The BL publication includes exact corpus CL membership internally while
Business readers see only permitted BL. Complete dependencies are owner-maintained;
Chat/Explorer must not reconstruct them from citations or convert dependency rows
into graph relationships.

## Decision required before detached existing-CL dispatch

`IOctoberOriginalBusinessFinalizationSlot` explicitly binds dispatch to Scan's
original live BeginGenerate admission. `OctoberBusinessFinalizer` refuses a missing
slot; successful absent lookup grants none. `OctoberGenerateAdmissionStore` refuses
fresh admission on a batch that already contains operations/accepted files/corpus.
Thus a new CLI process cannot merely reopen a CL batch and pretend to be its old run.
The existing same-live-run CL-and-BL path remains supported.

For a separate existing-CL command, the smallest proposed **shared-contract change**
is authorization of a fresh, explicit Business-only dispatch against one existing
immutable CorpusHandle, with a new run/finalization operation, pinned roots/provider/
prompt, one consumed original callback and the same F1 commit guard/F2 capacity,
stop, deadline and live-authority requirements. Keep BeginGenerate's fresh-batch
semantics and ordinary receipt/lookup behavior unchanged; no takeover, automatic
retry, cross-batch remapping or rescanning. This changes the permitted origin of a
Business dispatch; it must be agreed before Scan issues such a capability.

**Not implemented here.** Approve that bounded semantic extension, or require the
existing original live run instead. Business would own the narrow contract and
conformance changes; Scan would own command admission, selection and dispatch.
The proposed first patch would update the permitted issuer semantics in `OctoberBusinessOriginalDispatchContracts.cs` and the owning contract documentation, plus conformance tests for fresh existing-corpus dispatch, exact replay and denial/fencing. No public method signature change or second Knowledge store is proposed. Scan admission implementation remains Scan-owned.
No migration is presently proposed. If durable admission/exclusion needs an added
record, its exact schema and compatibility impact must be presented separately
before implementation; this document is not permission to migrate a target.

## Real-data qualification checklist (not executed)

- [ ] Pin code revision, exact corpus/batch/digest, repository/source bindings,
  accepted document versions, terminal coverage, selected roots and runtime owner.
  Verify Scan's actual acceptance/freeze/publication receipts; successful CL reporting
  alone is insufficient. Keep evaluator answers out of generation input.
- [ ] Login and registration have meaningful distinct source-backed roots. Shared
  helpers do not merge purposes; local-only flows are allowed when no link is proved.
- [ ] Each claimed cross-repository edge has exact directed endpoints and accepted
  caller action, provider operation and connecting evidence. Names, confidence,
  advisory descriptions and assumed deployment are insufficient; retain opposition.
- [ ] Review generated conditions, alternatives, validation/error paths and effects
  against the exact authorized source revision. Mark missing evidence and omissions;
  do not rewrite accepted knowledge manually to make the presentation pass.
- [ ] Uncertain routing, unprocessed inputs and bounded frontiers remain explicit.
  Advisory context stays advisory at statement level, never source-proven behavior.
- [ ] BL publication and full exact dependencies are coherent, including supplemental
  and uncited contributors; prior CL versions/publications remain unchanged. Replay
  resolves the same operation without duplicate versions or another provider call.
- [ ] An ordinary authorized User can search/open complete BL through the exact
  canonical Business selection, including paging. CL titles/IDs/paths/counts and
  unauthorized references are absent. Revocation of any contributor fails closed.
- [ ] Separately record semantic review, controlled tests, PostgreSQL qualification,
  real provider execution and actual presentation UI evidence. Declare partial/failed
  outcomes and resource cleanup/retention honestly; no fixture-as-real claim.

No external/runtime database, provider, host, production-source or retained-dataset operation is
covered by the current instruction. Request exact runtime/source/provider/spend and
retention authority only once the command and its input/admission contract are ready.


## Verification and review for this handoff

Current-source `eng/test.ps1` SelfContained run
`bed1870f25ba4b5e9632fca369968ead`: **462 selected / 462 passed / 0 failed / 0 skipped**;
build zero errors (14,942 existing analyzer warnings). Filter:

```text
FullyQualifiedName~OctoberBusiness|FullyQualifiedName~OctoberFlow|FullyQualifiedName~OctoberResolution|FullyQualifiedName~OctoberPublication|FullyQualifiedName~OctoberDisclosure|FullyQualifiedName~OctoberKnowledgeContract|FullyQualifiedName~OctoberGenerateAdmissionTests
```

These existing fixtures use controlled transports and ephemeral test SQLite/in-memory
state only. No previous disposable-runtime grant was reused. PostgreSQL, native,
External, E2E and HistoricalBoundary suites were excluded and are not qualified by
this run. No new production defect was demonstrated, so no speculative production
patch or manufactured RED test was added.

Source review checked this handoff against the exact owner interfaces, original
slot contract, fresh-admission guard, loader/provenance checks, planner limits and
publication/read tests. In particular, existing tests verify accepted/frozen-batch
admission refusal, missing-slot refusal, original-only generation, exact replay,
publication without changing earlier CL/B0, full dependency authorization and
Business reader exclusion. This was a local source/document review; no independent
agent or other chat was dispatched under the user's coordination restriction.

Primary implementation pointers:

- [Owner identities and writer/reader ports](../../src/dotnet/Vulgata.Core/DomainServices/OctoberKnowledgeContracts.cs)
- [Resolution store](../../src/dotnet/Vulgata.Infrastructure/Data/October/Knowledge/OctoberResolutionStore.cs)
- [Trusted flow input loader](../../src/dotnet/Vulgata.Infrastructure/Data/October/Knowledge/OctoberFlowPlanningInputLoader.cs)
- [Flow planner](../../src/dotnet/Vulgata.Core/DomainServices/OctoberFlowPlanner.cs)
- [Finalizer](../../src/dotnet/Vulgata.Agents/OctoberBusiness/OctoberBusinessFinalizer.cs)
- [Original dispatch boundary](../../src/dotnet/Vulgata.Core/DomainServices/OctoberBusinessOriginalDispatchContracts.cs)
- [Atomic BL publication](../../src/dotnet/Vulgata.Infrastructure/Data/October/Knowledge/OctoberFlowPublicationStore.cs)
The full SelfContained suite was not rerun. The integration project-state record's
unresolved full-suite result is not superseded by this focused passing selection.
