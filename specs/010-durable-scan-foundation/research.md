# Phase 0 Research: Durable Scan Foundation & Analysis Runtime

**Pinned boundary:** `dd3111bebbe295aa43a01e95be7bbdb97d3eecff`; Story 12.1 spec SHA-256 `DA24F90063D0B66D50D642BED8D1E1709257C6BF8B0B42E4AE89929CE5FEEFB2`.

All design unknowns are resolved. The absent JDT-LS distribution archive checksum is a declared qualification prerequisite, not an unresolved design choice.

## R-1: Durable persistence shape

**Decision:** Evolve the existing per-Repository `ScanExecution` compatibly and add `ScanContextSnapshot`, `ScanJobExecution`, and `TwoRepositoryScanCoordination` entities/tables.

**Rationale:** The current source already has a durable `ScanExecution`, composite Repository/ScanRun identities, immutable knowledge tables, and deterministic job/document IDs. Additive evolution preserves historical Story 11.5 rows while replacing `InFlightJobId` as Epic 12's operational authority.

**Alternatives considered:** Replacing `ScanExecutions` would orphan historical rows and split one domain concept; encoding jobs as JSON would weaken concurrency, FKs, and queryability; one System-level Scan would contradict the canonical per-Repository meaning of Scan.

## R-2: Migration packaging

**Decision:** Use one cohesive additive migration, owned with `VulgataDbContext`, EF configurations, and model snapshot by one integration owner.

**Rationale:** The new tables, expanded phase constraint, composite keys, and Document/job atomicity are one relational invariant set. One reversible migration minimizes partially deployed states.

**Alternatives considered:** Multiple migrations permit unsupported intermediate schemas and multiply rollback states; destructive table replacement violates protected historical-data requirements.

## R-3: Concurrency and atomic completion

**Decision:** Use monotonic `StateVersion` concurrency tokens plus short PostgreSQL row locks/conditional updates. Refactor the knowledge write internally so Document materialization and job `Completed` transition share one `VulgataDbContext` transaction.

**Rationale:** The current knowledge store already implements transactional deterministic commits, but the current scan store commits separately. A shared transaction is the only design that prevents completed-without-Document and Document-with-uncompleted-job windows. Version-qualified transitions make stale writers observable.

**Alternatives considered:** Distributed/saga compensation would expose partial state; PostgreSQL advisory locks introduce an extra lock namespace and do not replace row invariants; relying only on EF tracked state is insufficient across processes.

## R-4: Recovery algorithm

**Decision:** Read an immutable recovery snapshot, perform authorization/currentness/provider/runtime checks outside database locks, then conditionally write against captured state versions. Reset only expired Running and due non-exhausted Retry Scheduled jobs; preserve future/terminal jobs and committed Documents.

**Rationale:** This is idempotent, avoids long locks, and exactly separates durable representation from Story 12.3's retry-window, attempt-budget, concurrency, and dispatch decisions.

**Alternatives considered:** Automatic startup recovery is explicitly deferred; resetting every Running job ignores valid leases; reopening Failed/Cancelled jobs violates terminal outcomes; holding locks during Git/process/provider checks is operationally unsafe.

## R-5: Runtime identity normalization

**Decision:** Canonicalize release identity as versioned sorted JSON containing declared package/release identities, approved relative executable/archive components, integrity hashes, Java major/client versions, and Repository/Profiled Revision workspace binding. Persist its SHA-256 and canonical JSON with the Scan Execution; keep absolute installation/workspace roots in host options only.

**Rationale:** The same trusted installation can move without changing domain identity, while any executable/archive content or revision binding change is detected. This satisfies FR-021 without leaking usernames or drive layout.

**Alternatives considered:** Hashing absolute paths makes machine relocation a domain change; version strings alone accept tampering; persisting only a hash prevents useful audit/revalidation; substituting the core JAR hash for the distribution archive checksum is expressly forbidden.

## R-6: CodeGraph adapter command mapping

**Decision:** Pin `@colbymchenry/codegraph@1.5.0` and map the closed port to fixed CLI templates: `init`, `index --quiet`, `status --json`, `query --json`, `callers --json`, `callees --json`, and `impact --depth 1 --json` for bounded related-file evidence.

**Rationale:** Local `codegraph --help` confirms these operations in 1.5.0. JSON-capable commands allow strict parsers; `impact` at depth one produces a bounded related set without exposing broader exploratory source output.

**Alternatives considered:** `explore` and `node` return broader source/trail output and are not closed JSON contracts; direct access to `.codegraph` couples domain logic to private storage; CGC's arbitrary query protocol is superseded.

## R-7: JDT-LS client and lifecycle

**Decision:** Implement a JDT-LS-specific Infrastructure adapter using centrally pinned StreamJsonRpc 2.25.29 over stdio. Launch the one exact Equinox launcher with Java 21, `config_win`, and an exclusive Repository/Profiled Revision workspace. Bound initialize/initialized, requests, cancellation, shutdown/exit, stderr, and forced process-tree cleanup.

**Rationale:** This is the exact approved client/runtime decision. Keeping JSON-RPC types behind Infrastructure prevents Worker/Core access to raw messages and permits deterministic lifecycle testing.

**Alternatives considered:** A general LSP client exposes excessive protocol surface; TCP adds a network service forbidden by the correction; one shared workspace permits cross-revision contamination; hand-rolled JSON-RPC framing duplicates the selected StreamJsonRpc responsibility.

## R-8: Ordinary startup versus real qualification

**Decision:** Ordinary startup verifies CodeGraph/JDT files and hashes, Java major, client version, paths, and configuration without indexing or importing a Repository. Explicit qualification can launch tools on the neutral fixture. Story 12.2 later makes successful JDT initialized/import/document-symbol readiness a PreScan gate.

**Rationale:** This satisfies no-index startup while preserving real-tool proof. A NotReady health result blocks Worker dispatch but need not prevent the management application from starting and reporting the bounded reason.

**Alternatives considered:** Indexing at startup violates FR-019; crashing the entire Web host hides correctable health; deferring every real process check until release evidence leaves the adapter unqualified.

## R-9: Source-reading boundary

**Decision:** Expose only exact contained Repository-relative ranges bound to Repository, Profiled Revision, expected source hash, and a byte cap. Resolve under the verified working-copy root and reject traversal, links/escapes, stale bindings, invalid UTF-8/ranges, and unbounded output.

**Rationale:** This reuses the repository's containment discipline while preventing arbitrary filesystem access.

**Alternatives considered:** Passing absolute paths or file handles across the port leaks host structure; whole-file reads violate bounded evidence; allowing the Agent to enumerate paths expands authority.

## R-10: Host composition and CGC compatibility

**Decision:** Register only the new CodeGraph/JDT/source production path by default. Historical CGC implementation/tests may remain unmodified, but no production startup or release command resolves them. Run Vulgata.Web on Windows and only PostgreSQL 17 in Docker; retain the compose application service only behind the explicit `post-deadline-app` profile.

**Rationale:** This implements the approved dated correction without destructive deadline cleanup and preserves NFR-4.1 as later work.

**Alternatives considered:** Deleting all CGC code increases unrelated regression risk; retaining default CGC DI keeps Python/HTTP qualification release-critical; rebuilding the application Docker image is explicitly non-blocking.

## R-11: Manual resume surface

**Decision:** Provide a ManagementAccess-authorized `POST /api/scan-executions/{scanExecutionId}/resume` endpoint that invokes the recovery coordinator and returns a bounded status/count view or ProblemDetails. Story 12.1 has no automatic scheduler and no production progress UI.

**Rationale:** It is one bounded SystemOwner action and gives Story 12.5 a stable UI seam without moving UI scope forward.

**Alternatives considered:** A CLI-only action does not satisfy the product authorization surface; implementing the full Chinese UI belongs to Story 12.5; starting workers directly would absorb Story 12.3 policy.

## R-12: Verification boundary

**Decision:** Require TDD at public seams, real PostgreSQL migration/concurrency/restart E2E, real CodeGraph qualification, real JDT-LS neutral-fixture qualification once its archive checksum is available, and explicit security-negative tests.

**Rationale:** SQLite/fakes establish fast behavior but cannot prove PostgreSQL row locking, process boundaries, or installed runtime identity.

**Alternatives considered:** Controlled doubles alone are insufficient for the accepted release claim; using sample repositories as the only tool fixture risks hardcoded behavior.
