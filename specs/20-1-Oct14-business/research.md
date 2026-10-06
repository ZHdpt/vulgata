# OCT-BUSINESS research

Date: 2026-10-06. Read-only code research plus direct SpecKit planning. No runtime qualification.

## R1 — A new October feature, using the integrated baseline

**Decision:** Plan on actual `oct14/business` at `fad7f11c9d6bc9ed3da1b371b24beb9f5c09db5d`. The planning commit includes the delivery/integration/feature inputs over the integrated starting point cited by those inputs. `git merge-base --is-ancestor fad7f11 HEAD` returned 0; clean status preceded planning edits.

**Rationale:** The user explicitly replaces full story execution with four bounded first-deliverable lanes. Definition starts now; compiled contracts are future implementation checkpoints.

**Alternatives rejected:** Resuming 18.5, importing its durable receipt/visibility machinery and old holds, or treating old story task lists as the current scope.

**Sources:** `docs/bmad/planning-artifacts/october-delivery-plan-2026-09-30.md`, `october-integration-plan-2026-10-06.md`, `october-delivery/business-knowledge-feature.md`. Historical 18.5 plan read using this worktree's `git show ed4f0205:specs/18-5-accept-semantic-file-knowledge-and-read-exact-published-resu/plan.md`; no other worktree accessed.

## R2 — Extend October persistence, isolate legacy Knowledge

**Decision:** Extend `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberDbContext.cs`, schema `october`, existing migration history `__OctoberMigrationsHistory` and snapshot. Add dedicated October Knowledge ports and tables under existing projects. Business is the sole schema writer, including Chat contributions.

**Rationale:** The current context contains setup/advisory/source-approval records only. `KnowledgeStoreRepository` uses current heads, ScanRun and Git-bound origins; `RepositoryAccessScope` has an optional live verifier. These cannot implement fixed October publication safely unchanged.

**Alternatives rejected:** A second context, consumer-specific stores, dummy ScanRun/Git records, mutating legacy reader defaults. Reuse pure canonicalization/rendering checks only after checking their assumptions.

## R3 — Simple trusted completion, durable idempotent Knowledge

**Decision:** Scan's private one-process finalizer creates a non-model-constructible completion capability bound to admitted file and canonical proposal digest, while separate current attempt eligibility remains revocable. Scan and Business share a process-local commit guard; fence/deadline/authority checks serialize against commit submission. A completed seal followed by drain expiry cannot authorize a late transaction; already-submitted commit outcomes require settlement/lookup. Knowledge keeps durable unique operation results, atomic acceptance and exact lookup; no generic durable completion workflow.

**Rationale:** User brief explicitly permits a simple process-local finalizer. Worker JSON, source access and operator setup are each insufficient proof of whole-file completion. A lost process can reconcile a committed operation but cannot manufacture a new completed result from a draft.

**Alternatives rejected:** Trusting `Complete=true`, granting irrevocable write authority at seal issuance, promising cancellation rolls back an already-submitted database commit, persisting model-issued authority, or adopting the old general terminal-receipt workflow.

## R4 — Separate ordinary-reader policy

**Decision:** A new Knowledge-owned `OctoberKnowledgeDisclosurePolicy` with a Web/Data Identity adapter checks live account eligibility, security stamp, current existing role, exact selected publication, requested scope and every contributor. See `contracts/knowledge-contract.md` for the complete coarse policy. No new ACL schema.

**Rationale:** `OctoberBatchSelector.BindAsync` compares stored operator subject with current operator. `RepositoryAuthorizationVerifier` caps at two Repositories in one System; `SourceRepositoryAuthorizationVerifier` is management-role-only. `UserContext` may cache roles for a circuit. None supplies live ordinary-reader checks. Existing System/Repository records contain management ownership, not User ACLs. FR-1.4 excludes a new fine-grained RBAC system.

**Alternatives rejected:** Requiring Users to authenticate as the generator operator; turning SystemOwnerAssignment into reader ACLs; allowing a captured repository set or stale cookie alone to grant disclosure. Per-user repository ACLs require a separate product decision if desired.

**Reuse:** Live lookup/sign-in/lockout/security-stamp pattern from `OctoberLocalAuthority.CheckCurrentSubjectAsync`, not its operator gate. Current System/Repository existence and association come through existing domain repositories.

## R5 — Immutable explicit selection, bounded reads

**Decision:** Explicit database/batch/publication identity for read selection; fixed ordered membership plus per-call live authority. Protected cursors bind selection/query/order/page size; no visibility epoch. No totals of hidden records, and exact hidden/missing handles share unavailable responses.

**Rationale:** Immutable first publications do not require competing heads or historical latest queries. Final authority checks preserve revocation without revoking the underlying historical content.

**Alternatives rejected:** Copying wiki data per consumer, implicitly selecting a latest publication, raw EF access from Chat/Explore, or long-lived permission grants in turn tokens.

## R6 — Bounded document-first resolution and rooted BL

**Decision:** Freeze accepted corpus after natural finish or a graceful operator stop closes dispatch, settles writes and supplies all selected-file outcomes, including unprocessed/cancelled gaps. Paused/unsettled/indeterminate work remains blocked. For each observation, build a finite deterministic candidate set, make bounded model-assisted inquiry over accepted evidence only, retain opposing evidence, and domain-validate relationship proposals. Plan distinct source-backed roots and finite directed slices. BL generation uses pinned accepted Documents/context; no source tools in this slice.

**Rationale:** This yields a useful real cross-Repository case without universal matching, hardcoded pairs, external registry lookups or an uncontrolled second scan. A missing decisive binding remains an Uncertainty. Preserve material source/advisory/hypothesis qualifications at statement-group level through CL, BL and consumer projections. Structural validity does not prove usefulness.

**Alternatives rejected:** Confidence-only linking, undirected connected components as flows, source-tool Pass 2, and recursively invoking Scan on publication. Generic later-evidence refresh remains outside this fixed-corpus delivery.

## R7 — Shared composition and Chat storage

**Decision:** Business delivers feature registration methods and Identity disclosure adapter; integration owns calling them from Web/host composition. Web uses frozen DefaultConnection plus explicit persisted batch/publication and never migrates October. Chat supplies conversation semantics to Business for serialized tables/migration.

**Rationale:** Generator setup already exists; it does not make Web a selected operator host. No provider secrets or generator credentials are persisted into publication or required for wiki reads. Chat runtime provider composition is a separate owner input.

**Alternatives rejected:** Web startup migration of October, startup generator authentication, new management-provider work and simultaneous migration authors.

## R8 — Verification and approvals

**Decision:** Honest test-first behavioral work after authorization, existing `eng/test.ps1` suite separation, owned PostgreSQL concurrency/atomicity evidence, real provider/source usefulness and separately owned integrated UI evidence. No tests/builds/providers/databases launched for planning.

**Rationale:** Fixture/SQLite success cannot qualify the real corpus or PostgreSQL behavior. B1/B2 finalization adds bounded resolution/grouping strategy, so the plan conservatively reports a Scan-strategy approval trigger before tasks. Chat's future tool wrappers require their own tool assessment; these internal read ports are not exposed as agent tools here.

**Alternatives rejected:** Deadline waivers, unlabelled fixtures, resurrecting historical governance holds, or silently claiming broader PRD completion.


## Cross-lane correction rationale — F1/F2/F5 (2026-10-06)

Read the explicitly named Scan counterpart at `C:/Users/13728/.codex/worktrees/f8dc/vulgata/specs/20-1-Oct14-scan` only; no other checkout was edited. Its scan-boundaries contract requires a shared fence/transaction guard and one host execution/admission/accounting scope. Earlier Business seal-first semantics and numeric-only budgets did not meet those boundaries.

**F1 decision:** analysis completion remains immutable evidence; current write eligibility does not. Choose one guarded commit-submission boundary with authoritative settlement of already-submitted commits. This rejects seal -> drain expiry -> transaction without a general receipt/recovery system. Scan must adopt the exact submission-versus-settlement distinction and acknowledge it; local review is not concurrence.

**F2 decision:** every Business matching/synthesis provider attempt acquires and settles Scan's host-supplied process-local scope. It owns one configured cap, provider request/rate admission, remaining budgets, deadline and hard stop; no multiplied Business pool. This replaces numeric-only coordination, including at cap=1 and during queued/active cancellation. Unknown usage is explicit and conservatively charged, never zero. Web policy reuse does not imply shared cross-process permits.

**F5 decision:** existing explicit PublishSelection supports a CL-only fixed selection with empty relationship/flow/BL sets and honest coverage, without Business provider calls or B1/B2. Scan owns exposing its early Generate path. CP-K1/C0 validation uses minimal probes against an initial owner-published compiling contract/fixture commit; completed consumer production cannot be a circular prerequisite.

These are planning corrections only. No tasks, code, migration, runtime resources, C0 implementation, commits or outbound coordination were performed. Dialogue migration agreement remains outside this correction scope. Independent focused review is recorded in plan-review.md; Scan acknowledgement and coordinator cross-lane verification remain pending.

