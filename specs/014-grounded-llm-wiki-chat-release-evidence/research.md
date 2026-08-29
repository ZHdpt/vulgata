# Research: Story 12.5 Track C-MVP

## Sources inspected

- corrected Story 12.5 specification and approved 2026-08-29 Sprint Change Proposal;
- PRD Section 0.1, Story 12.5, August 30 architecture and docs/judge-rubric.txt;
- Constitution, AGENTS.md, Ubiquitous Language and accepted Track B final verification;
- existing Track A/B release-gate tests, fixed question corpus and database-name guard;
- production Prepared Scan, Pass 1, Pass 2, production Scan-status and Prepared Chat composition;
- current contracts under contracts/, treated as downstream artifacts rather than authority.

No unresolved design unknown remains. Machine- and credential-specific values are runtime prerequisites with fail-closed outcomes.

## R-1: Track C-MVP should operate the product, not duplicate it

**Decision**: execute the existing authenticated Vulgata endpoints, hosted services and Blazor pages in one host run.

**Rationale**: PreparedScanEndpoints, Pass1ExecutionEndpoints, Pass2ExecutionEndpoints, ManualResumeEndpoints, the Pass 1/Pass 2 hosted services, ProductionScanStatusReader, PreparedChatCoordinator and ChatPage already compose the accepted path. A second orchestration harness would not prove the judge-visible product.

**Alternatives considered**:

- copy the Story 12.4 production qualification into Story 12.5 — rejected because it clones another story's orchestration and database assumptions;
- add a Track C product coordinator — rejected as a new product feature and deadline risk;
- keep a separate Tier 2 controlled-provider run — explicitly deferred.

## R-2: The expected production delta is none

**Decision**: plan no production write. Limit executable Track C support to tests/evidence helpers for database preflight and Markdown report completeness.

**Rationale**: accepted Track A/B plus Story 12.4 already implement the required Scan/Chat behavior. The correction changes proof ceremony, not product behavior.

**Alternatives considered**:

- add DI registrations or a release service — rejected; there is no current product need;
- add persistence for run evidence — rejected because the report is a repository artifact and no EF migration is permitted.

**Failure implication**: a later need for any production-source correction invalidates the current no-production approval. Stop, preserve the defect evidence, return to the plan owner, rerun invalidated definition/approval gates and bind a new scope before any RED/GREEN work. Only evidence-file, operator-command and read-only-query corrections remain inside the current Track C-MVP lane; a requested feature, Scan/Story 12.4/persistence change or accepted Chat-security change is additionally an authority contradiction.

## R-3: Database safety must precede every product connection

**Decision**: reuse Story125ReleaseHarness.CreateRunIdentity and IsOwnedDatabaseName; add only enough test seam to prove rejected names cause zero connection/product-host calls.

**Rationale**: the current regex already creates unique lowercase vulgata_story125_* identities and rejects common protected/malformed targets. A spy-able preconnection boundary closes the remaining ordering proof without becoming a provisioner.

**Alternatives considered**:

- inspect the database after connection — too late;
- trust operator naming — unsafe;
- reuse the main or Story 12.4 database — prohibited.

## R-4: One Markdown report is the current evidence contract

**Decision**: replace the Track C acceptance role of story125-release-evidence/2.2 with the human-readable Markdown contract.

**Rationale**: the approved correction explicitly removes rigid ten-check semantics, attestation, closure/supersession graphs and exhaustive artifact hashing. Judges need inspectable revisions, commands, query results, Chat outcomes, restart and screenshots, not a second semantic proof engine.

**Alternatives considered**:

- reduce the JSON schema but keep it authoritative — still creates a rigid release envelope and duplicate maintenance;
- persist a release-summary entity or page — explicitly deferred;
- delete historical schema/tests immediately — rejected because accepted Track A history should remain visible. The schema is marked deferred and no longer used as Track C-MVP acceptance authority.

## R-5: Straightforward queries are enough

**Decision**: run read-only observations against existing vulgata tables for Prepared Scan Manifests/entries, Scan/job terminal state including explicit high-value-seed selected/succeeded/failed counts, Documents/Source Anchors, exact Cross-Repository Resolution decisions/REFERENCES Edges, Uncertainties, Business Logic Documents and Flow Groups.

**Rationale**: these rows are already authoritative. Track C should report them, not reconstruct a new relationship or release authority.

**Alternatives considered**:

- add a release query API — unnecessary product surface;
- recompute decision and Flow Group semantics in a harness — duplicates Story 12.4 and Track B validators;
- edit rows to satisfy minimums — forbidden fabrication.

## R-6: Application restart is the correct persistence proof

**Decision**: keep PostgreSQL running, stop/restart only Vulgata, then re-authenticate and inspect the same wiki/Chat scope and pre/post publication counts.

**Rationale**: the August 30 architecture explicitly replaces container restart with process restart. Persistent Documents, Edges and Flow Groups must survive; transient ChatConversationState is intentionally scoped and need not.

**Alternatives considered**:

- restart/recreate the database — destroys the continuity being proved;
- require conversation restoration — contradicts accepted transient conversation design.

## R-7: Streaming proof remains owned by Track B

**Decision**: retain all Track B raw streaming, UTF-8/SSE/JSON/cap/deadline/terminal/cancellation tests. Compatibility artifacts continue to quote 16,384, 65,536, 1,048,576 and 131,072 plus `transportOutcome`, `rejectionCategory` and `validationOutcome`. In the real run, record provider identity, transport composition, final closed outcome and complete-render duration; quote existing neutral observations when available.

**Rationale**: recomputing byte domains or signing observations in Track C was the removed proof subsystem. Mandatory streaming itself is retained and cannot be replaced by aggregate/non-streaming or deterministic rescue output.

**Alternatives considered**:

- recompute every raw/SSE/received/decoded counter — explicitly deferred;
- accept provider SDK aggregate streaming — violates accepted transport authority;
- accept fake provider evidence — prohibited.

## R-8: Manual evaluation preserves the fixed corpus without prescribing prose

**Decision**: pin `q01` through `q12` from the existing corpus and execute every case as an independent real-run row with fresh `ChatConversationState`. Preserve `followUpTo` as topical lineage for human scoring only. Thus `q01` (Business), `q02` (IT) and `q03` (Business) do not share server conversation/evidence state across their mode changes. Record observation, usefulness, citations, disclosure and duration manually.

**Rationale**: the accepted mode/scope contract clears Conversation State on a mode change. Tier 1 already proves same-mode multi-turn re-fetch/revalidation and actual authorization/currentness denial behavior. Under the real run's one authenticated principal and exactly-two-authorized-Repositories scope, `q12` can prove only the `prompt-boundary-only` outcome: asking about a Repository outside selected scope does not make the answer confirm its existence or disclose its Business Rules, Documents, relationships, citations or Source Anchors. It cannot honestly prove a denied principal, known protected Repository, revocation or true missing-equivalent behavior.

**Alternatives considered**:

- automate a second LLM judge — adds cost and non-deterministic authority;
- cherry-pick only successful questions — violates repeatability;
- carry `q01` state into `q02`/`q03` — contradicts accepted mode reset;
- add a third Repository or denied-principal fixture — broadens the accepted public seam and is unnecessary because Tier 1 owns that security proof;
- copy planned fields into results or label `q12` `denied-access-exercised` — fabricates execution.

## R-9: Screenshots are manual evidence with a redaction checklist

**Decision**: capture five real views manually and record relative paths plus redaction disposition in the Markdown report.

**Rationale**: visual proof is judge-facing; automated candidate binding and image validation are explicitly deferred.

**Alternatives considered**:

- add a screenshot product feature — deferred;
- hash/cross-link every image — deferred;
- accept fake Backup B images — prohibited.

## R-10: External prerequisites fail closed

**Decision**: missing PostgreSQL/Docker, Repository revision, CodeGraph/JDT LS/Java, provider credential/network, authentication or browser capability yields a sanitized blocked report section and no success claim for dependent work.

**Rationale**: a release run cannot manufacture unavailable external observations. Diagnosing prerequisites before rerunning an unchanged expensive run preserves time and evidence integrity.

**Alternatives considered**:

- silently skip the check — dishonest;
- substitute fixtures/fake provider — cannot support release evidence;
- print full environment/connection details — secret leakage risk.

## R-11: No migration, Story 12.4 or Scan change

**Decision**: freeze VulgataDbContext, migrations, model snapshot, Story 12.4 contracts and every Scan strategy seam.

**Rationale**: existing storage expresses all required evidence and the approved correction permits only proof simplification.

**Alternatives considered**: any new table, decision field, Scan selector or dispatch behavior is outside authorization and must return to plan rather than be hidden as implementation support.

## R-12: Planned-change trigger

**Decision**: require independent plan-only review and a planned-change decision because the corrected plan materially replaces the former Track C proof architecture and implements a PRD-scope correction.

**Rationale**: no Agent tool or Scan strategy changes, but repository policy applies when a plan changes or weakens a PRD requirement. The dated PRD overlay already authorizes this exact reduction; the standing user authorization may accept the post-review decision while preserving all downstream gates.
