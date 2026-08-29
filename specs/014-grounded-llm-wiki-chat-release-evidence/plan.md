# Implementation Plan: Story 12.5 Track C-MVP

**Branch**: `demo0831/story-12-5-trackC`
**Date**: 2026-08-29
**Specification**: [spec.md](spec.md), SHA-256 `15bc1cc7afe3d7250d8d247546da4ab72086f030bcb81e7b33b875701ffba309`
**Definition source commit**: `1517d8c85eed643a092fbb1c997f232c90e6b52c`
**Accepted baseline**: Track A and Track B checkpoint `f292caec427f9844be3c0f65b4cb00a9292080a5`
**Immutable dependency**: Story 12.4 commit `4798b97d9f23aef587829175df7ab7ebc3daf995`
**Correction authority**: [Sprint Change Proposal: Story 12.5 Track C-MVP](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-29-story-12-5-track-c-mvp.md)

## Summary

Track C-MVP proves the existing August 30 product path; it does not build a second release-proof product. One pinned candidate runs Vulgata on the documented Windows host against one new lowercase `vulgata_story125_*` PostgreSQL database, scans exactly two real Java/XML Repositories at exact Profiled Revisions through the already accepted CodeGraph, JDT LS, safe XML, Pass 1, exact Cross-Repository Resolution and Pass 2 path, exercises the accepted mandatory-streaming Prepared Chat path, restarts the Vulgata application process once against the same database, and records the result in one human-readable Markdown report.

The report contains a lightweight preflight, sanitized commands and exit results, straightforward database query results, a manually completed twelve-question table, five manually captured/redacted real screenshots, restart observations, failures and limitations. Track C-MVP may quote existing Track B content-dark streaming observations, but it neither recomputes transport counters nor adds attestation, signatures, a rigid release envelope, a duplicate orchestration harness, outage injection, screenshot automation or a release-summary product feature.

Accepted Track A/B production and tests remain immutable. The expected implementation delta is therefore test/evidence tooling only. If the real run exposes a defect whose correction requires any production-source change, the current no-production implementation approval is invalid: stop the evidence lane, record the reproducible defect, return to the plan owner, rerun every invalidated definition/approval gate, and only then begin RED/GREEN work under a newly bound approval. Only evidence-file, operator-command and read-only-query corrections remain inside the current approval. Any need to alter Scan behavior, Story 12.4 contracts, persistence shape or accepted Chat security is additionally an authority contradiction and stops the Track C-MVP lane.

## Technical Context

**Language/runtime**: .NET 10, C#, Blazor Interactive Server, PowerShell operator commands, PostgreSQL 17.
**Existing production seams**: `PreparedScanEndpoints`, `Pass1ExecutionEndpoints`, `Pass2ExecutionEndpoints`, `ManualResumeEndpoints`, `PreparedScanCoordinator`, `PreparedPass1ExecutionHostedService`, `PreparedPass2ExecutionHostedService`, `ProductionScanStatusReader`, `ChatEvidenceRetriever`, `PreparedChatCoordinator`, `OpenAiCompatibleChatProviderStreamingTransport`, and `ChatPage`.
**Existing Track C support seams**: `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`, `Story125Tier2PostgreSqlTests.cs`, the fixed `story125-question-corpus.json`, and `Story125EvaluationContractTests.cs`.
**Persistent authority**: existing `vulgata` and `identity` schemas only. No table, column, index, migration, `VulgataDbContext`, or model-snapshot change.
**External prerequisites**: PostgreSQL 17; two accessible real Repository roots at exact Git commits; `@colbymchenry/codegraph@1.5.0` and its accepted hashes; Java 21; `jdt-language-server-1.61.0-202608141332`; configured OpenAI-compatible streaming provider; authenticated Vulgata user authorized for the selected System and both Repositories; a local browser/screenshot capability.
**Performance/quality bounds**: 50-150 automatically selected File Code Units; every high-value seed successful; ordinary failures within the accepted Scan budget; twelve representative Chat questions when the provider is available; useful answer rate >=80%; accepted material technical claim citation coverage 100%; unauthorized disclosure 0; each accepted complete rendered answer <30 seconds.
**Evidence format**: one Markdown report plus relative screenshot files. No release JSON envelope is required for Track C-MVP.

## Constitution Check

| Principle | Pre-design result | Plan consequence |
| --- | --- | --- |
| I. Layered Project Boundaries | PASS | The release lane invokes existing Web/Infrastructure/Agents composition. Any narrowly necessary guard remains in the test/evidence harness and must not move database access into Core or Web UI code. |
| II. Domain Purity & Ubiquitous Language | PASS | Queries observe existing System, Repository, Profiled Revision, File Code Unit, Document, Source Anchor, REFERENCES Edge, GENERATED_FROM Edge, Uncertainty, Cross-Repository Resolution and Flow Group authorities. No new domain synonym or mutable Document path is introduced. |
| III. Validation & API Contract Discipline | PASS | The product path uses existing authenticated endpoints and Chinese UI. Track C-MVP adds no public API or user input contract. |
| IV. Test-First Integration Harness | PASS | Any executable support change begins with a public-seam database/preflight/report-contract RED. The real run is required E2E evidence, not a substitute for Tier 1 regression. |
| V. Security & Data Stewardship | PASS | Database target is rejected before product connection unless owned; commands/configuration are sanitized; screenshots are redacted; provider secrets, connection strings, absolute host paths and unauthorized evidence never enter the report. |

No Constitution exception or Complexity Tracking entry is required.

## Accepted Baseline and Delta

### Preserved unchanged

- Track A production Scan-status UI, corpus and evaluation preparation.
- Track B deterministic lexical selection, current-v2 relationship/Flow Group expansion, Evidence Packet 2.2, bounded conversation re-fetch, claim/citation validation, mandatory raw streaming, strict UTF-8/SSE/JSON/cap/deadline handling, accepted-only publication, cancellation/reset and Chinese Business/IT behavior.
- Story 12.4 exact `CrossRepositoryResolutionDecision`, current exact `DocumentEdge`, complete `GENERATED_FROM` and published Flow Group authority.
- Existing Scan parsing, filtering, selection, scheduling, retry, dispatch and persistence.

### Preserved Track B compatibility vocabulary

The accepted streaming compatibility literals remain: one received logical Provider Chunk is bounded at **16,384** bytes; the assembled received provider response is bounded at **65,536** bytes; the independent raw response-body safety cap is **1,048,576** bytes; and the independent SSE line/event safety cap is **131,072** bytes. Historical/current compatibility artifacts also retain the closed observation field names `transportOutcome`, `rejectionCategory`, and `validationOutcome`.

Track C-MVP only preserves or quotes these accepted Track B values and fields. It does not recompute any raw-read/body, SSE, received Provider Chunk, assembled-response, decoded-delta or final-answer domain and does not turn the quoted observations into a new release authority.

### Removed from the August 30 boundary

- ECDSA/key/attestation sinks and sealed raw-adapter admission.
- Track C transport-counter recomputation and forgery qualification.
- The fixed ten-check release envelope and schema-2.2 semantic graph.
- Supersession/closure graphs and exhaustive hashing/cross-linking.
- A separate Tier 2 orchestration run, live outage injection and automated screenshot binding.
- A dedicated release-summary product feature.

These remain explicit post-competition backlog; the old schema file is retained only as a marked historical compatibility artifact so accepted Track A test history is not silently erased.

### New Track C-MVP work

1. Correct the release contract so the MVP authority is a Markdown report, not the historical JSON envelope; preserve accepted Track A/B tests unchanged and add only new Track C-MVP contract/guard coverage.
2. Reuse or minimally narrow the existing database-name guard and prove it rejects protected/malformed names before a connection factory/product host is invoked.
3. Record a lightweight preflight and run command log without secrets.
4. Execute one real product run and straightforward read-only queries.
5. Before provider execution, pin corpus cases `q01` through `q12`, then execute each as an independent real-run row with fresh `ChatConversationState` through the real configured streaming Chat path when available.
6. Restart only the Vulgata application process against the same database and re-observe persisted wiki/Chat access and duplicate counts.
7. Manually capture/redact five real screenshots and complete the report.

## Architecture Decisions

### AD-CMVP-1 — Evidence execution, not a product subsystem

**Decision**: Track C-MVP has no new production service, endpoint, DI registration, persistence entity or UI feature. The only planned executable delta is a small test/evidence harness seam if the current `Story125ReleaseHarness.IsOwnedDatabaseName` boundary cannot express preconnection ownership and report-shape checks.

**Rationale**: the existing application already exposes the complete Scan and Prepared Chat path. Duplicating it in a release harness would test a second implementation rather than the candidate shown to judges.

**Alternatives rejected**: retaining the attestation/release-envelope subsystem, adding a release-summary page/API, and copying Story 12.4 qualification orchestration. Each is outside the approved MVP boundary.

**Consequence**: real-run failures are classified as prerequisite, operator, evidence, or product failures. Evidence/operator/query defects may be corrected in this lane. A source correction invalidates the current approval and returns to plan/review/rebinding before any TDD; missing external prerequisites produce honest blocked evidence.

### AD-CMVP-2 — Database ownership is a zero-product-access gate

**Decision**: before constructing a product database connection, starting Vulgata, applying startup migrations, or issuing any query, preflight requires a new unique lowercase name matching `^vulgata_story125_[a-z0-9_]{8,48}$`, explicitly rejects `vulgata`, PostgreSQL system databases and every `vulgata_story124_*` name, and records the intended owner/purpose. Creation uses the administrator connection only after this guard passes.

**Reusable seam**: `Story125ReleaseHarness.CreateRunIdentity` and `IsOwnedDatabaseName`, with `Story125Tier2PostgreSqlTests` as the public behavioral boundary. A minimal extension may accept a connection-factory spy to prove zero calls for rejected names; it must not recreate a database provisioner or product host.

**Alternatives rejected**: trusting a manually typed connection string or cloning Story 12.4 qualification data. Both violate the preconnection/clean-database boundary.

**Consequence**: inability to create or own the database is a hard blocked disposition, not permission to reuse another database.

### AD-CMVP-3 — One combined host integration and release run

**Decision**: start the existing Vulgata host with the owned database, invoke the existing authenticated preparation/Pass 1/Pass 2/resume surfaces and UI, and let the existing hosted services and provider adapters perform the work. There is no separately automated Tier 2 run.

**Run order**:

1. Pin candidate and Repository Profiled Revisions; capture tool/provider identities.
2. Prove database ownership and create the clean database.
3. Start Vulgata; authenticate and select the System plus exactly two Repositories.
4. Prepare both Repositories and verify real CodeGraph, JDT LS and safe XML evidence in the persisted Prepared Scan Manifests.
5. Run the accepted Pass 1, exact Cross-Repository Resolution and Pass 2 paths to terminal states.
6. Query minimum output facts.
7. Before the first provider call, record fixed corpus IDs `q01` through `q12`; execute each row with fresh `ChatConversationState` through the existing Business/IT page and manually score outcomes. `followUpTo` is topical lineage only and never permission to carry state across rows.
8. Stop only the Vulgata process, restart it with the same database, re-authenticate, open persisted wiki/Chat, and rerun duplicate-count queries.
9. Capture/redact screenshots and finish the report.

**Consequence**: a prerequisite or product failure stops dependent observations but preserves earlier truthful command/query evidence. The run is not rerun unchanged after an external failure until the prerequisite is diagnosed.

### AD-CMVP-4 — Lightweight, human-auditable report

**Decision**: the release authority is one candidate-bound Markdown document using [contracts/release-evidence-contract.md](contracts/release-evidence-contract.md). It contains bounded tables, not a machine-enforced ten-check envelope.

**Minimum sections**: disposition and candidate; exact Repository Profiled Revisions; tool/provider/non-secret configuration/database identities; sanitized commands/exits; required query results; twelve manual Chat rows; restart observation; five screenshot paths/redaction results; failures and limitations.

**Consequence**: no signature, per-artifact hash graph, supersession graph or automated screenshot validation is required. Candidate/revision identity remains explicit in the report itself.

### AD-CMVP-5 — Straightforward queries observe existing authority

**Decision**: use read-only SQL/EF queries over existing `vulgata` tables. Query output includes exact identifiers/revisions needed to review the claim; it does not reconstruct relationship truth or add a new semantic validator.

**Required observations**:

- two approved `PreparedScanManifests` and 50-150 selected `PreparedScanManifestEntries` total, with exact Profiled Revisions and real tool bindings;
- terminal Pass 1/Pass 2 execution/job counts, explicit high-value-seed selected/succeeded/failed counts, proof that failed is zero and selected equals succeeded, and the ordinary failure budget;
- current source-anchored Code Logic and Business Logic `DocumentVersions` plus `SourceAnchors`;
- at least one accepted current `CrossRepositoryResolutionDecision` bound to one cross-Repository REFERENCES `DocumentEdges` row;
- at least one honest `Uncertainties` row;
- at least three current Business Logic Documents and one published `FlowGroups` row bound to the accepted decision/edge authority;
- pre/post-restart counts proving no restart-caused duplicate publication.

**Consequence**: a stale query may be corrected to the model; a missing required persisted fact is a product/result failure and cannot be manufactured.

### AD-CMVP-6 — Restart proves persistence, not transient conversation survival

**Decision**: stop and restart the Vulgata application process only; keep the same PostgreSQL service/database. After restart, re-authenticate and prove the generated wiki and a new grounded Chat request are available. Transient `ChatConversationState` is intentionally not expected to survive.

**Consequence**: compare pre/post persisted Document, Edge and Flow Group counts/identities. Do not claim saved conversation history.

### AD-CMVP-7 — Streaming is verified by existing behavior plus real observation

**Decision**: Tier 1 continues to verify the accepted strict raw streaming boundary. The real run records that the configured provider path used `OpenAiCompatibleChatProviderStreamingTransport`, the answer completed or failed under the existing closed outcome, and the complete rendered result duration. Track C quotes existing Track B observation fields where available; it does not recompute raw/SSE/provider-chunk/decoded/final counters.

**Consequence**: a non-streaming result, operational deterministic rescue, provider fragment, or unvalidated result cannot count as an accepted release answer. Provider unavailability yields blocked real-LLM evidence rather than fake Backup B evidence.

### AD-CMVP-8 — Manual screenshots are controlled evidence

**Decision**: a human captures one redacted real screenshot for each category: Scan, grounded Business Chat, grounded IT Chat with Source Anchors, exact Cross-Repository result, and persisted wiki/Chat access after restart. A checklist verifies no secret, connection string, absolute host path, fake Backup B state or unrelated candidate output is visible.

**Consequence**: no image parser, candidate-binding automation or product screenshot feature is built. A missing/unreviewed category prevents a passed report.

### AD-CMVP-9 — Corpus lineage is not Conversation State

**Decision**: the real run pins `q01` through `q12` and executes every case as an independent row with fresh `ChatConversationState`. The `followUpTo` field records topical lineage for human evaluation only. In particular, `q01` is Business, `q02` is IT and `q03` is Business; each mode transition invokes the accepted mode/scope reset boundary, so no prior question, evidence or citation context is retained between those rows.

**Authorization oracle**: `q12` runs under the same authenticated principal and the same exactly-two-authorized-Repositories scope as the other rows. It can honestly prove only that a prompt asking about a Repository outside that selected scope does not cause the answer to confirm the outside Repository's existence or disclose its Business Rules, Documents, relationships, citations or Source Anchors. Its oracle is `prompt-boundary-only`, never `denied-access-exercised` or a proof of revocation, a denied principal, a known protected Repository or true missing-equivalent behavior.

**Tier 1 authority**: accepted Track B tests remain the authority for same-mode multi-turn re-fetch/revalidation and for actual authorization/currentness denial behavior. Track C-MVP does not add a third Repository, denied principal or protected-scope fixture because that would broaden the accepted exactly-two-authorized-Repositories public seam.

**Alternatives rejected**: carrying Conversation State from `q01` to `q03` across mode changes contradicts the accepted reset contract; inventing a denied-access setup or treating a prompt assertion as access-denial proof would manufacture an unaccepted security oracle.

**Consequence**: the report records `freshConversationState`, topical lineage and the applicable authorization oracle for every row, and cites the retained Tier 1 evidence for security/currentness claims the real-run corpus does not exercise.

### Alternatives and consequence summary

| Decision | Rejected alternative | Consequence of the selected option |
| --- | --- | --- |
| AD-CMVP-1 | Track C product service or copied qualification orchestration | Real product path is authoritative; a required source correction invalidates approval and returns to plan/rebinding before TDD. |
| AD-CMVP-2 | operator-trusted or post-connection database validation | Unsafe targets fail before product access; blocked ownership cannot be bypassed. |
| AD-CMVP-3 | separate Tier 2 and Tier 3 orchestration | One candidate/database/run reduces ceremony; every dependent observation shares the same real state. |
| AD-CMVP-4 | reduced but still rigid JSON release envelope or release-summary page | Markdown remains human-auditable; signatures/closure/hash graphs are not acceptance work. |
| AD-CMVP-5 | release query API or semantic recomputation harness | Existing persisted rows remain authority; missing facts fail honestly. |
| AD-CMVP-6 | database/container recreation or conversation-history persistence | Same-database process restart proves persisted wiki state only. |
| AD-CMVP-7 | Track C byte-counter recomputation or SDK aggregate acceptance | Track B tests retain transport authority; real run records only real composition/outcome/render time. |
| AD-CMVP-8 | automated image validation/candidate binding | Manual capture/redaction remains required; missing categories block a passed report. |
| AD-CMVP-9 | cross-mode retained Conversation State or invented denied-access fixture | Every corpus row is independent; topical lineage and the limited `q12` oracle are reported honestly, while Tier 1 owns actual reset/authorization/currentness proof. |

## External-Prerequisite Failure Model

| Failure | Classification | Required behavior |
| --- | --- | --- |
| Missing PostgreSQL administrator access or owned database creation failure | blocked | Record sanitized command/endpoint and result; do not select another database. |
| Docker engine unavailable for PostgreSQL | blocked-environment | Record engine/pipe failure once; do not repeatedly rerun unchanged commands. |
| Wrong Java major, CodeGraph/JDT LS version/hash, missing executable or Repository revision | blocked | Stop before the Scan path; record expected and observed non-secret identity. |
| Provider endpoint/model absent, credential unavailable or network route blocked | blocked | Do not expose the credential; do not substitute fake provider output; retain completed Scan evidence. |
| Provider returns a closed product rejection | product/provider result | Record the category. If correction requires production source, invalidate the no-production approval and return to plan/review/rebinding before TDD. |
| Required database output absent after terminal Scan | product/result failure | Preserve query result; do not edit database or manufacture rows. |
| Evidence template, operator command or read-only query is wrong | evidence/operator/query defect | Correct only the owned evidence/operator/query artifact, rerun the affected observation, and preserve the original failure record. |
| Browser/screenshot capability unavailable | blocked-evidence | Preserve technical run results but do not claim complete release proof. |

## Project Structure and Ownership

```text
specs/014-grounded-llm-wiki-chat-release-evidence/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── release-evidence-contract.md
    ├── story125-release-evidence.schema.json # deferred historical contract
    ├── story125-evaluation-case.schema.json  # retained corpus contract
    ├── grounded-chat-v2.md                   # retained Track B contract
    ├── chat-provider-streaming-v1.md         # retained Track B transport
    ├── chat-evidence-packet-v2.schema.json   # retained Track B packet
    └── production-scan-ui.md                 # retained Track A UI
```

Expected later owners: the test/evidence harness owner may add only narrowly scoped Track C-MVP coverage under `tests/Vulgata.Tests/ReleaseGate/Story125/` while preserving accepted Track A/B tests unchanged; the evidence owner writes the Track C run directory/report/screenshots; the integration owner verifies composition/build. `Program.cs`, projects, endpoints and all production source remain unchanged. Any needed production-source edit invalidates the no-production approval and requires return to plan, fresh downstream review and scope-hash rebinding before tasks may authorize RED/GREEN work; Scan, Story 12.4, persistence and accepted Chat-security changes remain authority contradictions.

## Test and Verification Strategy

### Definition/Tier 1

1. Contract tests prove the fixed 12-20 corpus remains valid and contains no desired provider prose.
2. Database guard tests prove unique lowercase names and zero connection/product-host invocation for rejected targets.
3. Markdown report contract checks prove every required section and twelve evaluation rows can be represented without a JSON envelope.
4. Existing focused Track A/B regression, mandatory streaming, same-mode multi-turn re-fetch/revalidation, authorization/currentness, citation, accepted-only reveal, cancellation/mode-scope reset and UI suites remain green; these Tier 1 tests, not `q12`, own actual denial/currentness proof.
5. Web build and migration diff guard prove no production/persistence boundary changed.

Existing tests for the historical 2.2 envelope remain unchanged and green as accepted Track A compatibility evidence, but regenerated Track C-MVP tasks do not implement their deferred attestation, ten-check, supersession/closure, counter-recomputation, outage or screenshot-automation concepts. New current-boundary tests target only the Markdown report shape and preconnection guard. The historical schema is clearly marked deferred; those ideas remain backlog.

### Required E2E

The combined host run is required because it triggers real PostgreSQL, Git, CodeGraph, JDT LS, safe XML, Worker provider, Pass 1/Pass 2, Cross-Repository Resolution, browser/render, restart and screenshot predicates. It may not be coverage-skipped by mocked tests.

### Pinned verification

- candidate HEAD and accepted Story 12.4 ancestry;
- clean owned diff and no migration/DbContext/snapshot/Story 12.4 changes;
- focused Track A/B/Track C guard tests;
- Web build and applicable full/bounded test baseline;
- real-run report disposition and required evidence checklist;
- same database/revision bindings before and after restart;
- five redacted screenshot categories;
- zero secret/fake/unauthorized evidence.

## Requirement Coverage

| Scope | Plan coverage |
| --- | --- |
| FR-001-FR-036, FR-048-FR-060 | Preserved accepted Track A/B and Story 12.4 implementation; regression and migration/source guards only. |
| FR-037-FR-038 | Existing fixed corpus/schema retained; `q01`-`q12` execute independently with fresh Conversation State, and `followUpTo` is topical lineage only. |
| FR-039 | AD-CMVP-4 Markdown report. |
| FR-040 | Tier 1 strategy. |
| FR-041, FR-047 | AD-CMVP-2 preconnection database guard. |
| FR-042 | AD-CMVP-3 combined real product run and prerequisite gate. |
| FR-043 | AD-CMVP-5 queries. |
| FR-044 | AD-CMVP-7 and AD-CMVP-9 plus the manual twelve-row table and honest `q12` prompt-boundary oracle. |
| FR-045 | AD-CMVP-6 same-database application restart. |
| FR-046 | AD-CMVP-8 manual screenshots/redaction. |
| FR-049-FR-052 | Ownership/source/migration guards. |
| FR-061 | Explicit deferred boundary and legacy-schema marker. |
| FR-062 | External-prerequisite failure model. |
| SC-009-SC-014, SC-018 | Required E2E report/query/restart/screenshot/blocked evidence. |

All corrected FRs/SCs have an observable seam. No unresolved design placeholder remains.

## Post-Design Constitution Recheck

PASS. The plan adds no persistence model, API, Agent tool, Scan strategy or product authorization seam. It observes existing authorities through existing application and read-only database surfaces, keeps evidence server-side and redacted, uses integration-first tests for the only possible harness delta, and preserves immutable Documents and canonical terminology.

## Planned-Change Trigger Disposition

**Architecture-decision trigger: YES, historically satisfied.** The Track C-MVP plan materially replaced the prior Track C proof architecture and received independent plan-only review. AD-CMVP-9 applies the unchanged definition-review findings without broadening that accepted intent, so the plan-only acceptance remains historical evidence; this correction requires downstream task regeneration and fresh definition review.

**Repository planned-change trigger: YES, historically satisfied for the PRD correction.** The plan implements the approved August 30 PRD overlay that removes the former proof-automation obligations while preserving product/security requirements. It adds no Worker Agent tool and changes no Scan strategy. This narrow definition correction changes neither the authorized intent nor implementation boundary; downstream artifacts must bind the corrected plan hash before implementation resumes.

## Judge-rubric value trace

| Rubric dimension | Candidate evidence |
| --- | --- |
| Innovation and creativity | real Cross-Repository business-flow extraction and grounded Business/IT explanation across two Java/XML Repositories |
| Technical implementation and complexity | exact Profiled Revisions, CodeGraph, JDT LS, safe XML, source-anchored Pass 1/Pass 2, current exact Edge/Flow Group authority, mandatory streaming and security regression |
| Practical value and deployability | one clean PostgreSQL database, host-run product, straightforward queries, application-process restart and explicit limitations |
| User experience and interaction design | Chinese production Scan feedback, Business/IT Chat, citations/Source Anchors and five real redacted screenshots |

## Inline Architecture Self-Review

| Lens | Result | Evidence |
| --- | --- | --- |
| Feasibility | PASS | Every product operation already exists in production composition; the only possible code delta is a small test/evidence guard. The combined run uses current endpoints, hosted services, database and Chat page. |
| Repository fit | PASS | No second Chat/Scan stack, generic repository, endpoint, DI registration, product release feature or cross-layer dependency is introduced. |
| Invariants | PASS | Exact Profiled Revisions, current authorized Documents/Source Anchors, Story 12.4 v2 relationship truth, packet fingerprints, streaming caps, citation validation, publication ownership and reset behavior remain unchanged. |
| Failure behavior | PASS | Preflight fails before product access; external prerequisites are sanitized blocked states; product/result failures preserve truthful evidence; no fake or repeated unchanged expensive run is accepted. |
| Test seams | PASS | Existing Track A/B suites guard product behavior; database guard/public harness tests own executable Track C support; the real host run is required E2E. |
| Migration/compatibility | PASS | No EF migration, `VulgataDbContext`, snapshot, Story 12.4 contract, Scan behavior or accepted product signature changes. Historical envelope schema is marked deferred rather than current authority. |
| Historical streaming compatibility | PASS | Plan/data/report contracts retain 16,384, 65,536, 1,048,576 and 131,072 plus `transportOutcome`, `rejectionCategory` and `validationOutcome`; Track C-MVP quotes rather than recomputes them. |
| Security | PASS | Secrets/connection strings/absolute paths are excluded; accepted Tier 1 tests retain actual authorization/currentness authority; the real run observes only the explicitly bounded `q12` prompt oracle; database ownership is preconnection; screenshots are manually redacted; no provider fragments cross the client boundary. |
| Manual evaluation protocol | PASS | `q01`-`q12` are pinned and independently executed with fresh Conversation State; `q01`-`q03` retain topical lineage only across mode resets; `q12` is explicitly limited to `prompt-boundary-only`, while Tier 1 remains authority for same-mode multi-turn and actual authorization/currentness denial. |
| Constitution/ADR | PASS | Constitution I-V and AGENTS.md are satisfied. No located ADR requires the deferred proof subsystem. |
| Judge value | PASS | Work is concentrated on the rubric-visible real two-Repository Scan, Cross-Repository flow, grounded Business/IT Chat, restart and screenshots. |
| Retained/deferred boundary | PASS | Every mandatory retained value is mapped to the real run; every removed item is named as post-competition backlog. |
| Alternatives/consequences | PASS | Each architecture decision records rejected alternatives and operational consequences. |
| Placeholders | PASS | None. External values are runtime prerequisites with fail-closed handling, not design unknowns. |

**Self-review conclusion**: `PLAN_READY_FOR_DOWNSTREAM_REGENERATION`. The smallest feasible Track C-MVP is evidence execution over the existing product, with no planned production write. Corpus execution and its security oracle are now unambiguous. Any production-source correction invalidates the no-production approval and requires return to plan plus fresh review/approval binding before TDD; any later claim that Track C requires a migration, Story 12.4 mutation, Scan change, new product feature or attestation boundary is also an authority contradiction.

## Downstream Task-Author Instructions

1. Regenerate the manual-evaluation task so it pins exactly `q01` through `q12` and executes each row with fresh `ChatConversationState`.
2. Describe `q01` -> `q02` -> `q03` as topical lineage only. Do not call it a retained follow-up conversation; its Business -> IT -> Business mode changes must exercise the accepted reset behavior.
3. Define `q12` as `prompt-boundary-only`: it proves no confirmation or disclosure about a Repository outside the selected scope, but it does not claim a denied principal, known protected Repository, revocation or true missing-equivalent execution.
4. Cite accepted Tier 1 tests for same-mode multi-turn re-fetch/revalidation and actual authorization/currentness behavior; do not add a third Repository or denied-principal setup.
5. Remove every production-TDD escape hatch from Track C-MVP tasks. Evidence/operator/read-only-query corrections remain in scope; any required production-source change stops work, invalidates the no-production approval, and returns to plan/review/scope-hash rebinding before regenerated tasks may authorize RED/GREEN work.
