# October fresh execution admission agreement

Canonical declaration ed0f2600ecc9bd4896a2c91bc14c36eec93fe35f accepted by Scan through Integrator on 2026-10-07 (integration af5520d2868b310df9444b272ed4cb5175f38628). Store implementation and runtime qualification remain separate. This enforces the accepted fresh-batch/no-restart
boundary. It is not a durable job, reservation, lease takeover or recovery framework.
There is no Reserved/Abort lifecycle and no new table or migration.

## Boundary and original live run

Run source-free configuration, authentication and selection checks first. Then call
`StartFreshExecutionAsync` BEFORE native CodeGraph preparation, source inventory/frame
acquisition or Worker/provider activity. Those activities are already execution, even
when no provider cost or Knowledge write has occurred. Revalidate the exact selection
and current operator at the atomic admission boundary; preflight observation is not a grant.
A clean failure before admission writes no claim. Once execution admission commits, the
batch is consumed even if later preparation/dispatch fails. Abrupt failure requires an
explicit fresh batch; no later process may adopt the claim, native state or old run.

The immutable request is complete KnowledgeBinding, trusted SubjectId, app-owned RunId,
stable AdmissionOperationId and CanonicalPayloadDigest. The request must remain identical
across reconciliation. Neither a model nor a browser selects the Run or admission identity.
The dedicated `IOctoberGenerateAdmissionAuthority` checks the actual current held operator,
exact subject and selected binding for both admission and receipt disclosure. Empty Knowledge
contributors do not establish this authority. `IOctoberCommitSubmissionGuard` remains
mandatory: `CommitAttempt.AttemptId = request.RunId`, with precisely Kind5, the owner-derived BatchId key and owner-computed digest registered for execution admission. This never implicitly registers AcceptFile or any later phase.

## One existing operation, no competing store

Append `OperationKind.BeginGenerate = 5`, preserving all prior numeric values. Business
alone derives `OperationKey(BatchId, BeginGenerate, BatchId)`; no API accepts a different
storage key. Existing `KnowledgeOperations` composite-PK uniqueness then enforces one
admission row per batch for all conforming callers. The ordinary immutable
KnowledgeWriteResult/operation JSON and an empty contributor array represent execution
admission only, never accepted file content or a published Knowledge selection.

Use one short transaction taking the batch row FOR UPDATE. Revalidate binding/current
subject, reconcile exact replay first, otherwise reject any existing execution claim,
accepted Knowledge operation or corpus/freeze. The batch lock also serializes setup and
acceptance transitions; repeat F1 entry/submission checks and invoke the actual provider
CommitAsync inside the shared guard. No native/source/provider work runs in this transaction.
Existing setup immutability after any Knowledge operation freezes the selection after
execution admission. No separate index, context or schema is needed.

A committed receipt proves durable admission only. Scan must enforce one dispatch for
that ORIGINAL LIVE Run, including concurrent identical calls and acknowledgement loss.
An AlreadyCommitted result is never a new dispatch grant. A different Run/admission/digest
conflicts or is not fresh. Missing-row lookup stays indeterminate while any earlier request
could still submit; acquiring a lock alone does not prove a pre-lock request is gone.
Lookup never regenerates, starts native work, creates a second claim or grants restart.

Generic producer LookupOperationAsync must reject BeginGenerate; the dedicated lookup
must apply its exact Subject/operator rules before disclosing receipt/conflict information.
The operation table need only retain the canonical digest if lookup always supplies the
original exact request. Do not promise recovery of omitted Run/admission fields from the
ordinary result JSON. Current live authority remains separate from durable storage truth.

## Canonical identity and verification

`OctoberGenerateAdmissionPolicy.OperationFor(binding)` returns the sole key above.
`ComputePayloadDigest(request)` hashes `OctoberKnowledgePolicy.SerializeCanonical` of
`{domain:"october/generate-admission/1",binding,subjectId,runId,admissionOperationId}`.
Use existing strict UTF-8/canonical object ordering, numeric enums and lowercase Guid-D;
exclude CanonicalPayloadDigest itself. No source tree identity, Git ref, credential or
new snapshot/version registry is added.

Required tests: canonical key/digest binding; no-row source-free preflight failure;
concurrent different Runs and identical requests; existing accepted/frozen batch rejection;
late selection/subject changes; entry/submission fencing; real uniqueness/rollback/ack loss;
delayed pre-lock request remains indeterminate; setup mutation denied after admission;
generic lookup rejection; original-live-run one-shot dispatch controlled by Scan.

Business digest helper/store controlled implementation and review are recorded in quickstart.md. Corresponding live host authority, one-shot runner integration and PostgreSQL qualification remain required.
No runtime/paid call or PostgreSQL qualification is authorized or claimed by this document.
