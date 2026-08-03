# Research: Exact Cross-Repository Resolution

**Date**: 2026-08-03 | **Boundary**: source HEAD `67b5eaf45ff3c3348e240e323bed6f80c9e885e0`

All planning unknowns are resolved. Findings come from the accepted specification/story, PRD, scanning plan/data contracts, predecessor plans/contracts, current code/tests, and read-only inspection of both pinned Git objects. No external Repository was checked out or mutated. The reviewed profile now selects the observed `cust/updateSession` occurrence rather than inventing an endpoint.

## R1. Configuration/profile location and schema

**Decision**: Use one fixed-name embedded JSON resource owned by Infrastructure, loaded through a strict `ExactResolutionProfileCatalog`. The profile is schema-versioned and content-fingerprinted. It contains logical aliases, opaque stable Git identity labels, exact tag refs/full commits, occurrence directions, Repository-relative file allowlists/selectors, provider implementation Document selectors, protocol, and a closed normalization/comparison policy.

**Rationale**: Cross-Repository matching is deterministic infrastructure behavior, not an Agent prompt. An embedded resource is version-controlled, deploys with the application, can be addressed by a fixed allowlist, and follows the successful Story 11.3 prepared-manifest pattern without placing deterministic configuration in Agents. Strict schema/fingerprint makes replay provenance stable.

**Alternatives considered**:

- `appsettings`/environment configuration: rejected because deployment configuration could drift without source review and could accidentally carry paths/URLs/IDs.
- hardcoded C# objects or Repository-name branches: rejected by FR-002/FR-003/FR-023 and extensibility requirements.
- database-authored profile: rejected for the deadline slice because it is mutable and would need a new authoring/version workflow.

## R2. Runtime alias binding

**Decision**: Keep database identities out of the profile and every textual evidence/diagnostic/log/exception field. An invocation-scoped runtime request maps every profile alias to one managed RepositoryId, ScanRunId, and ScanExecutionId selected by the authorized orchestration. Web verifies one visible System, both Repository memberships, both ScanRun/ScanExecution ownerships, distinct Repository bindings, and exact profile provenance before any Git/source access. The validated decision then persists the typed internal PK/FK identities needed for ownership, referential integrity, deterministic idempotency, and authorized joins. The profile's opaque stable Git identity label must equal the label in the runtime binding; the actual Git URL remains solely on the managed Repository.

**Rationale**: The current Repository aggregate owns deployment-visible Git URL and database identity but has no stable alias field. Invocation binding and relational persistence use existing managed identities without embedding them in version control or evidence text and without adding Repository-name inference. Keeping trusted typed columns distinct from serialized text reconciles owner-safe FKs with SC-006. It also naturally supports either direction per occurrence and duplicate/missing-binding refusal.

**Alternatives considered**:

- add a permanent provider/consumer or demo alias to Repository: rejected because roles are occurrence-local and the deadline profile must not redefine Repository identity.
- match by Repository name: rejected as a production conditional and unstable identity.
- match profile to Git URL: rejected because clone URLs/mounts are deployment facts and can contain credentials.

## R3. Exact tag/full-commit acquisition

**Decision**: Add a separate `IExactGitRevisionSnapshotService`, leaving `IGitWorkingCopyService.AcquireAsync` unchanged. Each exact request creates a unique private no-checkout Git repository beneath the Vulgata-owned `.exact-revisions` root, fetches only the full configured tag ref, peels it with `^{commit}`, requires equality to the 40-character lowercase commit, and materializes a private bounded snapshot. The lease owns and cleans both private roots; it never changes the ordinary Repository working copy, its branch, `HEAD`, index, remote, or refs. Reject if any durable Profiled Revision or selected Pass 1 artifact differs.

**Rationale**: Current `AcquireAsync` owns one stable path per Repository and later runs `pull --ff-only`; detaching that shared checkout would strand ordinary acquisition. A private exact-object lease preserves the existing bounded archive/identity controls while making success, failure, cancellation, and concurrent cleanup operation-local. Full-commit verification protects annotated/lightweight tag movement and similarly named refs. Required regressions run exact acquisition followed by and concurrently with ordinary `AcquireAsync` and prove the ordinary attached checkout remains usable.

**Alternatives considered**:

- accept the current working copy if its branch appears correct: rejected because it can silently advance.
- verify only the durable ScanRun commit without acquiring source: insufficient because resolver evidence must be read from the same object.
- detach/restore the ordinary managed checkout under a lock: rejected because cancellation/process failure makes restoration a correctness dependency and unnecessarily blocks normal acquisition.
- mutate/check out external production Repositories: rejected; only unique Vulgata-owned private exact roots are created.

## R4. Post-Pass-1 lifecycle placement

**Decision**: Add a separate deterministic coordinator accepting exactly two explicit prepared execution bindings. Require both at `Pass1Completed` (or later completed with the same current Pass 1 artifacts), then resolve. Return `WaitingForPass1` when one is not ready. Do not add a new ScanExecution phase or automatic Repository enumeration.

**Rationale**: Story 11.2 already makes `Pass1Completed` durable and Story 11.3 exposes Pass 2 separately. A separate coordinator uses the intended seam, avoids changing the single-Repository runner into a scheduler, and lets Story 11.5 compose the final sequence.

**Alternatives considered**:

- invoke after Pass 2/Completed only: rejected because FR-001 explicitly binds resolution to durable Pass 1 availability.
- inject a general system-wide scheduler into the runner: rejected by the deadline deferral.
- add new durable execution phases: unnecessary for a deterministic one-shot resolver and would broaden migration/lifecycle scope.

## R5. Provider candidate extraction

**Decision**: Enumerate only exact profile-listed XML files resolving beneath `src/main/resources/disgram/in/`. Use bounded `XmlReader` with `DtdProcessing.Prohibit`, `XmlResolver = null`, comments/whitespace ignored as appropriate, line information enabled, and finite file/aggregate/candidate limits. Emit candidates only from configured `trans` elements with exactly one non-empty `name` attribute.

**Rationale**: This is the accepted organization-specific provider rule and requires no LLM. Streaming XML supplies deterministic decoded attribute values and line/column evidence while preventing entity expansion and external reads.

**Alternatives considered**:

- regex XML: rejected as structurally unsound and unsafe for escaping/namespaces.
- scan all XML/configuration files: rejected because the profile must be a closed allowlist and normal pre-scan excludes configuration data.
- ask a Worker Agent/LLM: rejected because match truth must be deterministic.

## R6. Consumer candidate extraction

**Decision**: Implement a bounded Java-like lexical scanner for exact profile-listed source files and the closed call form `(simple-member-invocation, receiverSymbol, memberName, line, occurrenceOrdinalOnLine, endpointArgumentIndex)`. The reviewed occurrence is `netTools.execute` in `UpdateChannelSessionOp.java` at line 45, ordinal 0, with the endpoint at zero-based argument index 1. The scanner skips comments, decodes ordinary string literals, tracks call parentheses/arguments, and accepts only one direct string-literal token at that position. It rejects variables, symbols, concatenation, templates/interpolation, computed expressions, malformed escapes, and ambiguous forms.

**Rationale**: Read-only `git show` at `mbs_account` commit `19c60cd3f3976b7697c487c7bafd96142e3a7625` proves the line is the supported token sequence `netTools.execute(ctx, "cust/updateSession")`. The pinned project has no general Java AST dependency exposed at this boundary. A closed lexical grammar is small, testable, and more exact than regex.

**Alternatives considered**:

- regex source matching: rejected because comments, escaping, nested calls, and concatenation create false positives.
- add a new Java parser dependency: rejected as unnecessary package/surface risk for one configured form.
- accept Pass 1 narrative or symbol names: rejected by FR-008/FR-018.

## R7. Normalization and exact comparison

**Decision**: `custom-rpc-in-prefix-v1` decodes source-format escapes, normalizes Unicode to Form C, removes only leading/trailing ASCII SP/HT/CR/LF, rejects empty/over-512-byte/control-containing output, requires and removes exactly one provider-side `in/` prefix, leaves the consumer side unchanged, and performs `StringComparison.Ordinal`. It does not case-fold, URL-decode, otherwise rewrite slashes, strip an arbitrary/configurable prefix, run regex/mapping scripts, or infer aliases.

**Rationale**: Read-only pinned evidence is `cust/updateSession` on the consumer and `in/cust/updateSession` in provider `src/main/resources/disgram/in/cust/updateSession.xml`. The fixed provider-only namespace rule converts both to the observed `cust/updateSession` without guessing. XML processors and Java literals expose decoded values; Form C and ordinal comparison remain stable.

**Alternatives considered**:

- invariant case-insensitive comparison: rejected because endpoint case semantics are unknown.
- arbitrary/configurable prefix removal, transformation scripts, or regex: rejected as too broad and unsafe.
- raw comparison without Unicode normalization: rejected because equivalent decoded values can differ in code-point composition.

## R8. Mapping evidence to Document Versions and Source Anchors

**Decision**: A consumer invocation must fall inside exactly one current Pass 1 Source Anchor for the bound Repository/ScanRun/Profiled Revision. Provider XML evidence uses its own bounded declaration locator, while the reviewed profile selects `src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java` plus exact type symbol `cn.com.yitong.actions.domain.login.UpdateSessionOp`; that selector must map through CodeUnit/SourceAnchor to one current provider Document Version. Persist both selected Source Anchor IDs as typed internal relational columns, never inside evidence text or unauthorized output. The reviewed unsupported observation similarly selects `UploadOcrToOssOp.java` plus its exact type and requires line 66 to fall inside one authorized current owner Source Anchor.

**Rationale**: Current SourceAnchor requires a CodeUnit and normal profiling excludes XML configuration. Creating a fake XML CodeUnit or weakening anchor ownership would violate predecessor invariants. Separate declaration evidence and provider implementation Source Anchor preserve both exact configuration proof and a valid target Document Version.

**Alternatives considered**:

- make SourceAnchor.CodeUnitId nullable for XML: rejected as weakening Story 11.1 ownership and Source Anchoring.
- create an Edge to an XML-only synthetic Document: rejected because no such Pass 1 Document exists and synthetic directory/config Documents are outside scope.
- choose the provider Document by endpoint/name heuristic: rejected as inference.

## R9. Persistence shape

**Decision**: Reuse `DocumentEdge` for graph truth and add required one-to-one `ExactCrossRepositoryEdgeEvidence`. Add a first-class `Uncertainty` table for non-exact eligible outcomes. Their typed internal PK/FK columns retain the owner Repository/ScanRun/Document Version/Source Anchor identities required by the current relational model; their bounded text columns never serialize those IDs. Use a new specific `IExactCrossRepositoryResolutionStore` that validates and atomically applies the bounded profile-ordered decision batch; do not change `IKnowledgeStoreRepository.CommitAsync` semantics.

**Rationale**: The current knowledge commit creates Edges only from a newly committed source Document Version, while Story 11.4 links two existing immutable versions. A specific store supports atomic currentness checks without broadening document-generation commits. A typed evidence row is safer and more queryable than arbitrary JSON and keeps existing Edge uniqueness/type semantics intact.

**Alternatives considered**:

- add many nullable exact-resolution columns to every DocumentEdge: rejected because GENERATED_FROM and ordinary REFERENCES Edges do not carry them.
- store evidence only in generic JSON: rejected because required fields, FKs, bounds, and authorization are harder to enforce.
- overload KnowledgeCommit with existing-version writes: rejected because it would weaken the stable aggregate boundary.

## R10. Canonical endpoint/dispatch reconciliation

**Decision**: `ProviderEndpointCandidate` and `ConsumerDispatchCandidate` are typed transient Core records for the one operation. They correspond respectively to canonical `communication_endpoints` and `dispatch_sites`, but their general persistent tables are not added. Durable output is only exact Edge evidence or Uncertainty.

**Rationale**: Scanning data contracts name the long-term model, while PRD §5.16 and the accepted story deliberately narrow the demo to one configured exact path. Persisting partial general endpoint/dispatch schemas now would imply capabilities not implemented and expand migration/retrieval scope.

**Alternatives considered**:

- implement all endpoint/dispatch/candidate tables: rejected as out of deadline scope.
- ignore canonical terminology/model entirely: rejected because the exact candidates still represent those concepts and future migration must be explicit.

## R11. Idempotency and concurrency

**Decision**: Derive stable Edge/Uncertainty IDs and evidence fingerprints inside the trusted boundary from profile fingerprint, occurrence/observation identity, direction, revisions, internal Document Version/Source Anchor identities, safe endpoint/locator, and outcome. Lock all distinct Logical Document owner rows in UUID order, revalidate current pointers/authorization/revisions, validate the full profile-ordered batch, and insert Edge+evidence plus Uncertainty in one execution-strategy transaction. Identical replay verifies the full durable row set; drift conflicts and rolls back the batch. Hash inputs and derived IDs remain relational/idempotency material and are never rendered into evidence text, diagnostics, logs, exceptions, or unauthorized responses.

**Rationale**: This follows Story 11.1's owner-lock, exact replay, and fail-closed transaction pattern. Stable keys prevent retries and simultaneous second-repository triggers from duplicating truth.

**Alternatives considered**:

- query-then-insert without locks: rejected due current-version and duplicate races.
- last-write-wins/upsert drift: rejected because immutable graph truth cannot be silently rewritten.

## R12. Authorization and retrieval

**Decision**: Web proves `ManagementAccess` plus visibility of the common System and both Repositories before constructing the request. Infrastructure rechecks `RepositoryAccessScope` before Git/query/write and again at retrieval. When both Edge endpoints are authorized, the exact DTO exposes the stable Edge identity only as opaque `relationshipIdentity` plus safe endpoint/profile/Evidence Locator metadata; it does not serialize Repository, ScanRun, Document Version, or Source Anchor PK/FKs. Otherwise the existing identity-free, metadata-free `RestrictedRelationship` marker is the only result.

**Rationale**: Scanning plan §14 requires both Edge endpoints authorized, and endpoint names/behavior can themselves disclose restricted business information. Rechecking at the data boundary prevents stale/cached Web authorization from becoming the only defense.

**Alternatives considered**:

- authorize only the consumer/source Repository: rejected because it leaks target identity/evidence.
- persist unauthorized attempted resolution and redact later: rejected by FR-020, which requires no persistence for unauthorized callers.

## R13. Warning/Uncertainty policy

**Decision**: Binding, authorization, and revision failures return bounded refusal codes and write nothing because they precede source inspection. Eligible variable/malformed/wrong-root/unmatched/normalization/multiple-provider cases persist one idempotent Uncertainty owned by the consumer Document Version when a safe consumer anchor/locator exists. Durable `sub_status` is restricted to canonical lowercase `cross_repo`, `unknown_target`, or `ambiguous`; variable/malformed/unmatched/normalization details use closed `reason_code` values. The reviewed `account-file-storage-oss-upload-v1` input maps to an authorized current owner Document Version/Source Anchor and persists nonterminal `status=unresolved`, `sub_status=unknown_target`, `reason_code=unsupported_kind`, `observation_kind=file_storage`; replay is exact and creates zero Edges. The record remains eligible for later full-V1 file/storage resolution or reclassification.

**Rationale**: This reconciles fail-before-write AC-1 with AC-3's diagnostic preservation, satisfies FR-022/SC-007 with a realizable record-producing input, and conforms to the Ubiquitous Language/scanning data-contract vocabulary. Canonical `wontfix` is terminal and requires a human-accepted Dangling Link; an automatic deadline deferral cannot make that decision or block future full-V1 processing. The design avoids graph records when no authorized/source identity exists while retaining source-relative diagnosis for inspected eligible evidence.

**Alternatives considered**:

- persist every binding error as Uncertainty: rejected because it would write graph state before valid binding and could leak existence.
- log only all nonmatches: rejected because retry-safe, queryable Uncertainty is an accepted domain concept.
- automatically mark the deferred file/storage observation `wontfix`: rejected because that state is reserved for an explicit human terminal decision and would misclassify future resolvable work.

## R14. Migration and verification boundary

**Decision**: Add an additive migration after `20260801033655_AddSequentialScanRunner`, coordinated by the integration owner. Verify clean apply, rollback to that baseline, reapply, constraints/indexes/FKs, no pending model changes, focused tests, Web build, and full regression. Run current-boundary two-Repository integration now; leave actual prepared clean-database E2E and final Docker build to Story 11.5.

**Rationale**: The expedited exception explicitly preserves migration/security checks and assigns final slice verification to Story 11.5.

**Alternatives considered**:

- defer all migration testing to Story 11.5: rejected by the story and exception.
- run/mutate the production prepared Repositories during planning: rejected by ownership and read-only boundaries.
