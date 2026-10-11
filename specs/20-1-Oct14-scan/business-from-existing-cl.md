# Business generation from an existing CL publication

## Authorized and independently reviewed scope

The user authorized implementation, review and tests, followed by a report of
runtime prerequisites, not a paid BL run. The original 77-file CL corpus and 163
CL Documents/publication must remain immutable on success and failure. A new
explicit BL attempt may use the same corpus after a previous attempt is settled.
Generic Scan resume, automatic retries, source rescanning and crash takeover remain
out of scope.

The explicit selector is `existingClPublicationId` in a `cl-and-bl` Generate
policy, combined with the existing exact target/database/batch/selection binding.
Absent selector preserves ordinary fresh Generate behavior. Empty selector or
selector with cl-only is rejected. The selected publication must be a committed,
complete CL-only publication with membership exactly matching the frozen corpus.
The new branch does not invoke BeginGenerate, source tools, Worker CL generation,
file acceptance, corpus freezing or CL publication.

Business owns the bounded authorized snapshot loader and exclusive admission.
It adds immutable BeginBusinessFromCl and CompleteBusinessFromCl receipts in the
existing KnowledgeOperations table, keyed by fresh host RunId. Under the existing
batch-row transaction lock, an unmatched begin blocks any subsequent admission.
Stored receipts alone never authorize execution. No schema/migration is required
at this checkout. Scan owns command selection, live execution scope, orchestration
and original-run settlement; no competing Knowledge store or semantics is added.

The owner snapshot pins all accepted manifests, exact CL versions, coverage,
contributors and validated partial resolution progress. Recheck subject/purpose/
contributor authorization before disclosure and semantic writes. Reuse exact
committed resolution outcomes. For this new route, provider/parse/budget failure
stops before persisting Failed/NotEvaluated resolution outcomes, so a later attempt
can retry the unresolved inquiry. Existing fresh cl-and-bl behavior is unchanged.
Each BL attempt has a new guarded finalization operation and may append a separate
BL publication; the original CL publication/versions/membership never change.

Completion is cleanup bookkeeping only. It requires the original process-local
owner-issued admission capability and trusted proof that semantic work is closed,
all actual calls/commits/providers are settled and no uncertain outcome remains.
It neither reads/discloses corpus content nor grants semantic write or provider
authority, so a settled original attempt can close after cancellation/expiry or
revocation. New attempts always require fresh authorization. Unconfirmed commits,
lost processes or unconfirmed transports leave an unmatched begin and block;
there is no elapsed-time takeover, receipt reconstruction or hidden resume.

Independent plan review accepted this boundary, including the separate cleanup
capability and owner-loaded partial resolution progress. User implementation
approval already covers this bounded activation; no runtime grant is inferred.

## Verification obligations

- Controlled RED/GREEN: exact selector and full corpus checks, no CL/native calls,
  original CL rows/handles/publication unchanged after failure and success,
  failure then fresh successful attempt, committed resolution reuse, no poisoned
  failure rows, authorization loss and time limits.
- Owner storage: concurrent begin exclusion, unmatched/unknown admission blocks,
  original-only completion, exact settlement and malformed/foreign selectors.
- Owned PostgreSQL qualification: actual transaction serialization, immutable
  receipt behavior and preservation of CL/old publication across retries.
- Existing tests and official suite inventory/launcher remain required. Controlled
  or PostgreSQL evidence is not paid provider/ordinary-reader qualification.

## Review and controlled evidence

Independent plan and final static review accepted the implemented boundary. The
review corrections included the existing-BL run identity, exclusive batch-row
locking for both admission and completion, and retaining the original verified
cleanup capability if post-commit disclosure fails. No remaining concrete static
findings were reported.

- RED `isolation-80b93ee4a53a42ac86470c349ddcdbe7`: 2/4 passed; the new
  selector and canonical admission behavior were absent before implementation.
- Focused GREEN `isolation-7c7549d43bb147c99acf8648b75f7515`: 5/5 passed,
  including failed BL followed by fresh successful attempts, exact original CL
  content/membership and original-reader preservation.
- Full SelfContained `isolation-e0ff542518494bddb4df12f9fae4fc8c`: 4908/4909
  passed. The existing unrelated
  `OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`
  fails with `output is not an exact successful one-file CL qualification report`.
  This is not a full-suite GREEN claim.
- Final focused SelfContained `isolation-42b1f54871db4e0da4b993de7b897fba`:
  6/6 passed on the final production sources. The three-attempt test verifies
  invalid resolution leaves no persisted failure, a subsequent valid Unresolved
  outcome survives synthesis failure, and a fresh successful attempt reuses it
  without another resolution request. Exact persisted JSON is unchanged.
- Owned PostgreSQL `isolation-268ce4ffac57480ebdca058728d2e58b`: 10/10 passed,
  including simultaneous begins with one winner, exact original completion and
  subsequent fresh admission, plus existing flow publication/preservation tests.
  The launcher removed its owned container and volume and verified volume absence.
  The original real CL database was never used by these tests.

Independent final review accepted after these results. No live-provider or paid
BL qualification is claimed. All tests stay in their existing suite-classified
test files; no fixture, dataset or suite inventory extension was introduced.

Intermediate targeted checks also caught a test compile/factory mismatch,
duplicate JSON properties in the test helper and record equality over nested
ImmutableArrays. Those assertions were corrected to compare exact stored content
or sequence members; production guards and assertions were not weakened.

## Runtime boundary

No paid BL run is authorized in this step. The preserved target is
`phone-15bfe12615eb`, batch `1a327657-6a55-4e65-ba06-6992f423d40e`, publication
`fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`. Runtime requires verified reader Identity,
current operator/contributor permissions, explicit business roots and bounded
provider/settings/spend/resource scope. No automatic reader retargeting.

The current protected business configuration has an empty `roots` array, with
limits of 64 nodes, 128 edges and depth 8, and `maximumAttemptTokens` 32768.
Select explicit roots (repository/path/semantic selector, or the bounded operator
selection route) before a useful run. Confirm the BL provider/model and finite
run/token/spend limits; the consumed CL run grant is not a new BL grant. Use a
fresh authorized invocation/log boundary with the existing protected credentials
and original database/batch/publication. Do not bypass the previous launcher's
single-use marker, prepare a replacement batch or regenerate CL.
