# Backup B: CGC-free Prepared-Scope LLM Demonstration

## Status: Implementation checkpoint — real-provider verification pending

## Objective
Produce a persistent screenshot-ready Vulgata dataset by bypassing CodeGraphContext
while running the real existing LLM Pass 1 and Pass 2 pipelines against a small,
reviewed, exact-revision source scope.

## Motivation
Story 11.5 (T072) requires full CodeGraphContext scanning of entire repositories.
Backup B provides a competition-deadline contingency that demonstrates the complete
LLM pipeline without CGC, using a manually reviewed prepared source scope.

This is NOT the official Story 11.5 completion path. It must never claim that
whole-Repository CGC scanning passed.

## Design Principles

### What we REUSE (no modifications):
- `SequentialScanRunner.RunPass1Async` — core Pass 1 orchestration
- `SequentialScanRunner.RunPreparedPass2Async` — core Pass 2 orchestration
- `IWorkerAgentDispatcher` → `WorkerAgentDispatcher` — real LLM dispatch
- `IWorkerAgentResultCommitter` → `WorkerAgentResultCommitter` — knowledge persistence
- `ICodeUnitSource` → `CodeUnitSource` — reads CodeUnits from DB
- `IApprovedPreScanHandoffSource` → `ApprovedPreScanHandoffSource` — reads handoff from DB
- `IScanExecutionStore` → `ScanExecutionRepository` — ScanExecution CRUD
- `IPreparedDemoPass2FlowPlanner` → `PreparedDemoPass2FlowPlanner` — resolves Pass 2 flow
- `IPreparedChatScopeResolver` → `PreparedChatScopeResolver` — chat scope validation
- `PreparedChatCoordinator` — full chat pipeline with citations
- `ExactResolutionProfileCatalog` — reviewed profile JSON
- All domain entities (CodeUnit, ScanRun, ScanExecution, PreScanResult, LogicalDocument,
  DocumentVersion, SourceAnchor, DocumentEdge, etc.)

### What we CREATE (test/demo-only):
1. **`IBackupBDirectProvisioner`** — interface in `Vulgata.Core/DomainServices/`
   Creates full pre-scan state in DB from reviewed manifest, without CGC
2. **`BackupBDirectProvisioner`** — implementation in `Vulgata.Infrastructure/Git/`
   - Clones/validates pinned Git repos
   - Reads exact committed files at exact revisions
   - Computes file identity hashes and source-slice hashes
   - Verifies repository identity, commit, path, line ranges
   - Creates System, Repository, ScanRun, CodeUnits, PreScanResult, ApprovedPreScanHandoff
   - Does NOT call CodeGraphContext or ICodeGraphProvider
3. **`BackupBManifest`** — reviewed manifest record type
4. **`demos/backup-b-direct-llm/BackupBDemo`** — console entry that provisions and
   runs Pass 1, exact Cross-Repository resolution, Pass 2, and persistence checks

### Hard Constraints
- **No CodeGraphContext or ICodeGraphProvider calls anywhere**
- No modifications to existing domain entities or EF configurations
- No new EF migrations (existing schema has all needed tables)
- No weakening of validation
- No seeding of finished documents
- Pass 1 and Pass 2 must produce Documents through real Worker Agent dispatcher
- Source must be read from exact committed Git revisions
- Verify repository identity, full commit, repository-relative path, line range,
  file hash, source-slice hash, and containment before dispatch
- Never send files outside reviewed manifest to LLM
- Never include credentials, absolute host paths, mutable branch names, or unrelated
  source in persisted evidence or provider requests
- Preserve existing authorization and RepositoryAccessScope checks
- Use existing domain entities and repositories (no direct SQL shortcuts)

### Persistence Isolation
- Database: `vulgata_demo_screenshots`
- Current local container: `vulgata-postgres` on port 5432
- Isolation is provided by the dedicated database name; reset commands must never
  target the ordinary `vulgata` database
- Idempotent provisioning (refuse if incompatible dataset exists)
- Prepared local Git repositories live under ignored `demo-seed-repos/`; the
  committed manifest uses Repository-root-relative paths

### First Demonstration Scope
Use the accepted two-Repository example from `demo-resolution-profile.v1.json`:
- mbs_account → mbs_channel
- Consumer: `UpdateChannelSessionOp.java` line 45 (netTools.execute call)
- Provider: `UpdateSessionOp.java` (implementation)

Minimal Code Unit set (~3-4 units):
1. Consumer invocation: `UpdateChannelSessionOp.execute` (mbs_account)
2. Communication identifier: extracted from line 45
3. Provider endpoint declaration: `updateSession.xml` (mbs_channel)
4. Provider implementation: `UpdateSessionOp` (mbs_channel)

## Implementation Order

### Phase 1: Specification & RED Tests
1. ✅ Inspect existing infrastructure
2. Write Backup B spec (this document)
3. Write RED tests for CGC non-invocation and direct provisioning

### Phase 2: Implement Provisioner
4. Create `BackupBManifest` record types
5. Implement Git source reader with hash verification
6. Implement DB provisioner
7. Run RED→GREEN tests

### Phase 3: End-to-End Pipeline
8. Build demo runner
9. Provision `vulgata_demo_screenshots` database
10. Execute Pass 1 with real LLM (deterministic fixture first, then real provider)
11. Verify persisted Code Logic Documents and Source Anchors
12. Apply prepared exact Cross-Repository relationship
13. Execute Pass 2 with real LLM
14. Verify Business Logic Document and GENERATED_FROM relationships

### Phase 4: Chat Verification & Screenshot Database
15. Start app against `vulgata_demo_screenshots`
16. Verify Chat retrieval, Business/IT modes, Document citations, source line citations
17. Create sanitized evidence report
18. Create pg_dump backup
19. Run regression tests

## Evidence Labels
The evidence must distinguish:
1. prepared-scope source selection (Backup B)
2. real Pass 1/Pass 2 LLM generation
3. deterministic-provider fallback (if used)
4. official whole-Repository CGC/T072 status (NOT claimed by Backup B)

Evidence labels the run "prepared-scope LLM demonstration" and does NOT claim
T072 or whole-Repository CGC success.

## Acceptance Criteria
- [x] No CGC process or CodeGraph provider call occurs
- [x] At least one real LLM Pass 1 dispatch succeeds for each Repository
- [x] At least one real LLM Pass 2 dispatch succeeds
- [x] Code Logic Documents are persisted through normal result committer in the
  deterministic E2E
- [x] Business Logic Document is persisted through normal result committer in the
  deterministic E2E
- [x] Source Anchors point to real exact-revision files and line ranges
- [x] Prepared exact Cross-Repository relationship is persisted
- [ ] Chat retrieves persisted Documents
- [ ] Chat renders Document citations and source-code citations
- [ ] Screenshot database survives process/container cleanup
- [x] Evidence labels run as "prepared-scope LLM demonstration"
- [x] Evidence does not claim T072 or whole-Repository CGC success
- [x] All committed tests pass

## Verification Checkpoint — 2026-08-13

- Demo build: passed with zero errors.
- Focused regression filter
  `FullyQualifiedName~SequentialScanRunner|WorkerAgent|BackupB|ExactResolution`:
  **243 passed, 0 failed**.
- Fresh deterministic dedicated-database E2E: **6 Code Logic Documents, 1 Business
  Logic Document, 1 REFERENCES Edge, and 2 GENERATED_FROM Edges**, exit code 0.
- Cross-Repository Pass 2 regression verifies that GENERATED_FROM ownership and
  relationship trace steps retain each Repository and Scan Run while the Business
  Logic Document persists only consumer-owned Source Anchors.
- Fresh real DeepSeek dedicated-database E2E: both Repository Pass 1 executions
  reached `Pass1Completed`, Pass 2 reached `Completed`, and the run persisted
  **6 Code Logic Documents, 1 Business Logic Document, 1 REFERENCES Edge, and
  2 GENERATED_FROM Edges**, exit code 0 in **250.7 seconds**.
- The first real Pass 2 response passed schema validation but persistence rejected
  provider-owned Source Anchors attached directly to consumer-owned Business Logic
  sections. Pass 2 projections now carry Repository ownership, the prompt requires
  consumer-owned section evidence, and the domain envelope enforces the same rule.
- Backup B real-provider composition retries only `SchemaInvalid` and `Error`, with
  a closed ceiling of three total attempts. `Valid`, `Unbounded`, and cancellation
  are never retried; production composition remains unchanged.
- Temporary diagnostic logging has been removed.
- Real-provider execution remains intentionally blocked when
  `VULGATA_BACKUP_B_WORKER_MODEL`, `VULGATA_BACKUP_B_WORKER_ENDPOINT`, and
  `VULGATA_BACKUP_B_WORKER_API_KEY` are absent; deterministic mode requires the
  explicit `VULGATA_BACKUP_B_FORCE_DETERMINISTIC=1` flag.
- This checkpoint does not claim T072 or whole-Repository CGC success.

## Time Discipline
- First deterministic end-to-end result: 4–6 hours
- Real-LLM screenshot-ready result: 8–12 hours
- Avoid broad refactors, new migrations, full UI redesign, unrelated cleanup
