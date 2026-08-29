# Contract: Story 12.5 Track C-MVP Release Report

## Status and authority

This Markdown contract is the current August 30 Track C-MVP release-evidence authority. It supersedes the Track C acceptance role of story125-release-evidence.schema.json and the former release-envelope 2.2 semantics.

The historical JSON schema remains in this directory only as a marked post-competition compatibility artifact. It is not required, populated, semantically validated or accepted as evidence for Track C-MVP.

For unchanged historical-receipt compatibility tests, the plan/data/report vocabulary continues to quote the accepted limits **16,384**, **65,536**, **1,048,576**, and **131,072**, and the closed fields `transportOutcome`, `rejectionCategory`, and `validationOutcome`. Track C-MVP preserves or quotes those values only; it does not recompute their byte domains or require a historical receipt.

## Corpus

The accepted corpus remains tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json under story125-evaluation-case.schema.json. It contains 12-20 fixed cases and no desired provider prose.

Before the first provider call, the report pins `q01` through `q12`. When the configured provider is available, the combined run executes exactly those IDs as independent rows with fresh `ChatConversationState`; failed cases cannot be replaced after observation. Corpus `followUpTo` is topical lineage only. `q01` (Business), `q02` (IT) and `q03` (Business) do not retain prior question, evidence or citation state across their mode changes.

Accepted Tier 1 tests remain the authority for same-mode multi-turn re-fetch/revalidation and actual authorization/currentness denial behavior. The real run uses one authenticated principal and exactly two authorized Repositories; it does not add a third Repository or denied principal.

## Report disposition

The report begins with exactly one disposition:

- passed — every retained Track C-MVP release value is observed;
- failed — execution completed far enough to disprove a required threshold or expose a product/result failure;
- blocked — a required external prerequisite or unsafe action prevented dependent work;
- not-run — no release execution began.

Completed, failed and blocked observations remain truthful. Missing work never receives invented counts, screenshots, provider results or success wording.

## Lightweight preflight

Before any product database connection or product data access, record:

- candidate commit and accepted Story 12.4 commit;
- branch/ownership/clean-state observation;
- exactly two Repository aliases and exact Profiled Revisions;
- PostgreSQL, CodeGraph, Java 21 and JDT LS versions/hashes;
- provider API/model and non-secret configuration identity;
- generated unique lowercase vulgata_story125_* database name, owner and purpose;
- readiness or sanitized blocked status for every required prerequisite.

The database guard rejects PostgreSQL system databases, vulgata, every vulgata_story124_* name, wrong case, malformed or empty names before a connection factory, Vulgata process, startup migration or query is invoked.

## Command and exit table

Record each material command/action in execution order with purpose, sanitized command/description, start/end time, exit code or closed UI/API result, disposition and bounded notes.

Never include credentials, provider keys, tokens, passwords, complete connection strings, secret-bearing environment output or absolute host paths.

No command hash graph, closure graph, supersession graph or signature is required.

## Combined host integration and real release run

One run uses:

- Vulgata on the documented Windows host;
- PostgreSQL 17 as the only required Docker service;
- one clean owned vulgata_story125_* database;
- exactly two real Java/XML Repositories at exact Profiled Revisions;
- real CodeGraph indexing;
- real JDT LS Java PreScan/navigation;
- safe catalog-driven XML parsing;
- at least 50 automatically selected File Code Units;
- the existing source-anchored Pass 1, exact Cross-Repository Resolution and Pass 2 path;
- the configured real LLM through the accepted mandatory streaming Prepared Chat path.

There is no separate automated Tier 2 orchestration, live outage injection or duplicate release coordinator.

## Straightforward database query table

Record sanitized read-only query descriptions/SQL, timestamps, exit results and bounded result rows proving:

1. two approved Prepared Scan Manifests, exact Profiled Revisions and 50-150 selected File Code Units;
2. real CodeGraph/JDT LS/catalog bindings, terminal Pass 1/Pass 2 state, and explicit high-value-seed selected/succeeded/failed counts;
3. source-anchored current Code Logic and Business Logic Documents;
4. at least one accepted exact Cross-Repository Resolution decision bound to its current cross-Repository REFERENCES Edge;
5. at least one honest Uncertainty;
6. at least three Business Logic Documents, including one published Cross-Repository Flow Group;
7. pre/post-restart Document, Edge and Flow Group identities/counts with zero restart-caused duplicate publication.

Queries observe existing authority. They do not recompute relationship truth, rewrite rows or insert missing evidence.

A passed report requires high-value-seed failed count zero and selected count equal to succeeded count. The report records existing Scan results and does not create a new seed validator or change Scan behavior.

## Manual twelve-question table

Each row records case ID, mode, scope, question/topical-lineage identity, `freshConversationState`, observed disposition, usefulness, accepted and cited material technical claim counts, unauthorized disclosure, authorization oracle, streaming observation, complete-render duration, packet fingerprint when exposed, applicable Tier 1 evidence reference, and bounded notes.

`q12` has authorization oracle `prompt-boundary-only`. With the same authorized scope as the other cases, it can prove only that asking about a Repository outside selected scope does not cause the answer to confirm that Repository's existence or disclose its Business Rules, Documents, relationships, citations or Source Anchors. It cannot be reported as `denied-access-exercised` and does not prove a denied principal, known protected Repository, revocation or true missing-equivalent behavior.

Passing metrics:

- useful answers at least 80%;
- accepted material technical claim citation coverage 100%;
- unauthorized evidence disclosure zero;
- accepted non-streaming provider answers zero;
- every accepted complete render below 30,000 ms.

Track C may quote accepted Track B content-dark runtime/completion observations. It must not recompute raw-read/body, SSE, received Provider Chunk, assembled response, decoded delta or final-answer counters. Operational deterministic rescue and fake provider state cannot count as accepted real-LLM streaming evidence.

## Application-process restart

Stop and restart only the Vulgata application process against the same owned PostgreSQL database. Re-authenticate, open persisted wiki content, submit a new grounded Chat request and repeat publication-count queries.

Passing restart evidence proves persisted wiki/Chat access and zero restart-caused duplicate Documents. It does not claim persistence of transient conversation state.

## Manual screenshots and redaction

The report links at least one repository-relative manually captured real screenshot in each category:

1. production Scan;
2. grounded Business Chat;
3. grounded IT Chat with Source Anchors;
4. exact Cross-Repository result;
5. persisted wiki/Chat access after restart.

Each row records category, relative path, capture time, real-production-state confirmation and manual redaction result. No automated image validation, candidate-binding machinery or required screenshot hash graph is used.

Reject or redact screenshots containing fake Backup B state, secrets, credentials, connection strings, absolute host paths or unrelated candidate output.

## Failures and limitations

The report always has a failures/limitations section. Missing PostgreSQL/Docker, Repository revision, tool identity, provider credential/network route, authentication or screenshot capability is recorded as a sanitized blocked condition. Do not rerun an unchanged expensive operation until the prerequisite or product failure is classified.

Only evidence-file, operator-command and read-only-query corrections remain inside the approved Track C-MVP lane. If correcting an observed defect requires any production-source change, stop and record the defect: the current no-production approval is invalid, and work must return to plan, fresh downstream review and scope-hash rebinding before TDD may begin. Scan, Story 12.4, persistence and accepted Chat-security changes additionally remain authority contradictions.

## Passing checklist

A report may say passed only when:

- preflight and database ownership passed before product access;
- the one real two-Repository run met Scan and minimum persisted-output thresholds;
- high-value-seed selected/succeeded/failed counts were recorded, failed was zero, and selected equaled succeeded;
- the provider-available twelve-question table met every metric;
- restart continuity passed;
- all five screenshot categories passed manual redaction;
- Tier 1, build, convergence, independent quality review, required E2E and pinned verification accepted the same candidate;
- no forbidden migration, VulgataDbContext/snapshot, Story 12.4, Scan-strategy or accepted Track A/B behavior change exists.

## Explicit post-competition backlog

ECDSA/key attestation, sealed raw-adapter admission, transport-counter recomputation/forgery tests, a rigid ten-check/schema-2.2 release envelope, supersession/closure graphs, exhaustive command/artifact/screenshot hashing, separate controlled-provider orchestration, live outage injection, automated screenshot binding and a release-summary product feature are deferred.
