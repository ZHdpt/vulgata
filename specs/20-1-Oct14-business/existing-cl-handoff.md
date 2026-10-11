# Existing accepted CL → Business Logic: owner handoff

Reconciled: 2026-10-11. Scope: October first deliverable, login/registration presentation.
The user retains management authority. The user explicitly authorized integration
and Business synchronization, followed by a separate prompt to start Business work.
This synchronization grants no provider, database, source, host or retained-data operation.

The subsequent user-authorized readiness check at `f06b85bf` verified the retained
publication and selected stored flow roots. See [retained CL readiness](retained-cl-readiness-2026-10-11.md)
for the measured payload and observation-role blockers, controlled test evidence,
and the execution proposal that remains blocked before paid work.

The approved narrow correction is now recorded in the
[one local-flow BL checkpoint](minimal-bl-publication-checkpoint.md): compact exact
root evidence, complete owner lineage, corrected provider accounting/settings and
373 passing controlled tests. That checkpoint supersedes the earlier multi-root
execution proposal. The user subsequently authorized one invocation: one registration
BL is now canonically published and ordinary-reader verified, with original CL intact.
The command's post-publication catalog check still reports a response-budget failure;
see the checkpoint for exact IDs, usage, settlement and the milestone stop boundary.

## Accepted integration and superseded assessment

Assigned checkout: `C:/Users/13728/.codex/worktrees/f081/vulgata`, `oct14/business`.
Starting Business HEAD: `3820d77a7ec9b76f05a197279a4391411fecbe04`, clean.
Accepted main checkpoint: **`2bb30f4a6c6aa242b832cc9130b0ec07e353c627`**, including
Scan `c473ad15` / implementation `5c220d55` and preserved Chat/Explorer improvements.
Business merge: `643a21574c9a3465011cc07a73d73a1019cb060e`, without conflicts or reset.
The merged tree exactly matched accepted main: `a41a2b9f49472b6d183e4e4d89241583f7c5175f`.
Only this handoff and Business qualification notes are changed after that merge.

The earlier assessment at `d623c509` correctly described the older APIs but is now
superseded **for the explicit existing-CL route**. Publication-to-corpus loading,
fresh exclusive Business admission and validated partial resolution reuse now exist.
The competing Business dispatch-extension proposal remains held/superseded; do not
implement it in parallel. Ordinary fresh BeginGenerate retains its original rules.
The older original-slot XML prose still mentions only BeginGenerate; the implemented
new admission and `OriginalAdmissionMatches` route are the current reference here.
No canonical-contract/source edit is made during this documentation reconciliation.

Business receives BL continuation and its transferred existing-CL command seam per
[Scan's handoff](../20-1-Oct14-scan/handoff-business-generation.md). Shared fresh-Scan,
CL execution and Web composition boundaries remain intact. Implementation or runtime
continuation waits for the user's next Business prompt.

## Exact retained input and command selection

The pinned Scan handoff reports 77/77 completed files and 163 CL Documents, with a
confirmed publication. This synchronization did not query or independently qualify
the retained database, source or ordinary reader.

| Binding | Handoff value / requirement |
| --- | --- |
| Target | `phone-15bfe12615eb` |
| Batch | `1a327657-6a55-4e65-ba06-6992f423d40e` |
| Original CL publication | `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9` |
| Selection digest | `52fde3a3354bb00ce686d6eb4f10feb9279d0f2d9daea9a6b8473f51e48b43bd` |
| Database binding | Use the exact existing protected target/database configuration; a database name alone is not CanonicalDatabaseIdentity. Verify live binding only under authorized runtime scope. |
| Policy | `outputStage: cl-and-bl`, `existingClPublicationId` set to that exact publication; retain explicit finite policy limits and repository scope. |
| Entry configuration | `October__Mode=generate`, `October__ScanPolicyFile` pointing to a fresh authorized policy, and `October__BusinessSettingsFile` or the bounded operator root-selection route. This is a configuration contract, not an instruction to launch now. |

Absence of the selector preserves fresh Generate. Empty selector or use with cl-only
is rejected. The selected publication must be a committed CL publication whose exact
membership matches the frozen corpus, with matching retained coverage/provenance.
Do not use Prepare, create a replacement batch, regenerate CL, union different batches
or retarget the original publication. Environment paths and credentials stay in
protected local configuration, not tracked examples or evaluation inputs.

## Implemented owner admission and settlement

Use [OctoberBusinessFromClContracts.cs](../../src/dotnet/Vulgata.Core/DomainServices/OctoberBusinessFromClContracts.cs)
and [OctoberBusinessFromClStore.cs](../../src/dotnet/Vulgata.Infrastructure/Data/October/Knowledge/OctoberBusinessFromClStore.cs),
not a competing DTO/store or a manually reconstructed corpus.

- `BusinessFromClRequest(PublicationBinding, SubjectId, RunId, CanonicalPayloadDigest)`
  binds the exact publication, current subject and fresh run. Use
  `OctoberBusinessFromClPolicy.ComputeBeginDigest`.
- `IOctoberBusinessFromClStore.BeginAsync` validates the stored binding, publication,
  full corpus membership, manifests, coverage, contributors and retained resolutions.
  Its `BusinessFromClSnapshot` supplies `Corpus`, `AcceptedFiles`, `Coverage`,
  `Contributors` and `ExistingResolutions`; the caller need not recover a lost freeze
  receipt or reconstruct contributor authority from citations.
- `BeginBusinessFromCl` (kind6) and `CompleteBusinessFromCl` (kind7) use the existing
  immutable KnowledgeOperations journal. Begin/completion serialize under the batch
  row lock; an unmatched begin blocks another attempt. No migration is introduced.
- The returned `BusinessFromClAdmission` is an issuer-verified, process-local,
  nonserialized capability. A receipt, lookup, elapsed time or new process cannot
  recreate it or claim takeover. Same-request lookup is reconciliation, not dispatch.
- `CompleteAsync` / `LookupCompletionAsync` require that original capability and
  `IOctoberBusinessFromClSettlementAuthority`. Complete is cleanup bookkeeping after
  semantic work is closed and all actual calls/commits/transports are settled; it
  grants no content disclosure or provider authority. Settled cleanup can finish
  after cancellation/expiry/revocation, while new admission requires fresh authority.
- Unknown commit/transport state, unfinished work or a lost owner remains blocked.
  Never clear unmatched receipts to force a retry. A later explicit fresh attempt
  is permitted only after confirmed original settlement/completion.

The command's [ExistingCl branch](../../src/dotnet/Vulgata.OctoberGenerator/OctoberGenerateRunner.ExistingCl.cs)
uses these capabilities and skips BeginGenerate, source inventory/native tools,
Worker CL generation, file acceptance, corpus freezing and replacement CL publication.
The original finalization slot still binds the exact live run/request/corpus/scope;
its issuer now validates either original admission route. No stored result grants it.

## Resolution, roots, context and publication

1. Load the exact accepted corpus through `IOctoberResolutionStore.LoadCorpusAsync`.
   `OctoberResolutionPolicy.Plan` computes finite inquiries and explicit dispositions.
   The new route reuses validated Supported/Unresolved outcomes in the owner snapshot.
   Provider/parse/budget failures with no witness stop this route before persisting
   Failed/NotEvaluated resolution, allowing a later settled fresh attempt to retry
   only unfinished work. Legacy retained Failed/NotEvaluated outcomes fail closed;
   do not overwrite them. Original fresh cl-and-bl behavior is unchanged.
2. Use `IOctoberFlowPlanningInputLoader.LoadAsync` and its exact units to select
   `FlowRootSelection(UnitId, Document, SelectedCodeUnit, Selector)`. Keep login and
   registration distinct even when they share helpers. No inferred first/all roots.
   Default roots are invalid; explicit empty roots yield NoContent, not useful BL.
3. Preserve exact `DocumentVersionReference` and `PreparedOrigin` bindings, coverage,
   manifest-qualified uncertainties, resolution evidence and complete contributor union.
   Supplemental dependencies are not necessarily flow members or graph edges.
4. Current synthesis consumes `{ input, group, gaps }` from accepted evidence. Context
   can guide root recognition, but accepted source must support behavior/relationships.
   Advisory/hypothesis/uncertain claims keep their qualification. Never feed evaluation
   answers into generation or manually seed accepted knowledge to satisfy a checklist.
5. Bind the same authorized scope/client, current authority, hard stop/deadline and
   commit guard. Finalization uses canonical provider/request/outcome digests and
   `IOctoberFlowPublicationStore` revalidation. Success creates a separate BL
   publication; earlier CL versions, content and membership remain immutable.
6. Keep semantic and persistence outcomes distinct: Failed/AlreadyCommitted is durable
   failure, not successful BL. Exact lookup cannot regenerate. Reader selection is
   not automatically retargeted: verify the returned BL publication explicitly.

The resolution/synthesis executors do not automatically acquire Scan's staged Worker
JSON correction, reasoning replay or tool-budget behavior. Their current known-SDK
reasoning metadata handling and strict final-text validation remain in place. Review
actual Business requests/settings before any paid run; do not copy large Worker caps
or add repair/retry behavior merely because it helped CL.

## Remaining inputs before BL execution

- Explicit stored login/registration roots. Scan reports the last inspected settings
  had `roots: []`, limits64 nodes/128 edges/depth8 and maximumAttemptTokens32768;
  these are reported settings, not approved or independently inspected BL choices.
- Confirmed provider/model, finite budgets/deadlines, current operator and every
  contributor's authority, genuine ordinary-reader Identity binding and exact database
  configuration. A fingerprint or management identity is not reader authorization.
- Fresh user-authorized invocation, log/process boundary, spend/resource and retention
  scope. Consumed CL grants and an old wrapper's single-use marker are not BL authority.
- Resolve or explicitly account for the known strict qualification-report parser
  mismatch before relying on that helper. No concrete API compatibility blocker was
  identified in this bounded source review; paid BL and presentation readiness remain
  unqualified. Do not start resources during this sync.

## Real-data qualification checklist (not executed by this sync)

- [ ] Pin exact accepted corpus/publication, source versions, coverage and selected roots;
  confirm old CL remains readable before and after BL, without Prepare or rescanning.
- [ ] Confirm meaningful distinct login/registration flows and bounded cycles/frontiers.
- [ ] Review each cross-repository link's directed exact endpoints and connecting source
  evidence; shared names, advisory context or confidence do not prove deployment.
- [ ] Check useful conditions, branches, validation/error paths and effects against the
  authorized source revision. Record omissions and uncertainty rather than invent facts.
- [ ] Verify coherent BL publication and complete exact dependencies, including uncited
  and supplemental contributors; preserve all source/advisory qualifications.
- [ ] Ordinary authorized User can search/page/open full BL through canonical Business
  reads. CL metadata and unauthorized references stay hidden; revocation fails closed.
- [ ] Record actual provider usage, outcome, settlement/completion, retained resources and
  any unresolved failures. Controlled tests are not paid-generation or UI evidence.

## Verification provenance and limits

[Accepted integration record](../../docs/bmad/planning-artifacts/october-scan-business-integration-2026-10-11.md):
full SelfContained **4915/4917**, zero skips/build errors, run
`7d1d42bbbca34259a31a625b2ff5adca`. The CodeGraph readiness failure was caused by an
omitted installed directory in the launcher's process-local PATH; unchanged focused
replay passed1/1 (`8380bb7b7e7642128368f379145b0c56`). This does not erase the full-run
failure. The strict `OctoberGeneratedClReport` field-set mismatch remains unresolved.
Do not claim full-suite GREEN or qualification-helper compatibility.

Scan records focused existing-CL6/6 and owned PostgreSQL10/10 with cleanup in
[business-from-existing-cl.md](../20-1-Oct14-scan/business-from-existing-cl.md).
Those are imported lane evidence, not new executions here or proof of paid BL.
Business's earlier462/462 at `bed1870f25ba4b5e9632fca369968ead` belongs to its earlier
source boundary; it does not qualify the newly integrated route.

This synchronization verified clean checkout, conflict-free merge and identical tree
hash to accepted main, then source-checked the documentation against compiled contracts
and runner/store behavior. No production edits, new tests, migrations, provider calls,
database access, production-source access or host startup occurred. No other chat was messaged.
Stop here for the user's next Business prompt.
