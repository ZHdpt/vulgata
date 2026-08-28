# Contract: Story 12.5 Evaluation and Release Evidence

## Corpus

The fixed corpus contains 12-20 schema-valid cases under `tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json`. It covers Business/IT, Cross-Repository flow, rules/decisions, technical traces, failure behavior, follow-ups, insufficient evidence, and authorization-negative behavior.

Each case specifies evidence themes and usefulness criteria, never desired provider prose. Restricted evidence/existence disclosure is an automatic failure. A follow-up chain is at most one initial question plus two follow-ups.

## Run envelope and check states

Every evidence attempt creates an immutable run envelope with a new `runId`. A correction never edits or deletes the earlier envelope; it sets `supersedesRunId` to the prior run and preserves that run's commands, exit codes, observations, artifacts, failure/block reason, and terminal status.

`status` and the discriminated `result.kind` must agree. `releaseReceipt` is non-null only when the run is `passed`; every success threshold and release claim lives inside that passed-only receipt. Therefore planned, attempted, failed, blocked, and not-run envelopes never fabricate File Code Unit counts, Chat percentages, successful restart, or screenshots merely to satisfy a schema.

Every manifest check has exactly one state:

- `planned`: defined but entry gate not yet attempted;
- `attempted`: started and not yet terminal;
- `passed`: all required observations attached;
- `failed`: attempted and acceptance failed;
- `blocked`: external prerequisite unavailable with evidence;
- `not-run`: deliberately not executed with reason.

No state may be inferred from missing files. An attempted run requires commands and attempted observations. A failed run additionally requires stage, bounded reason, and failure artifacts. A blocked run requires stage, bounded reason, at least one observation of the blocking condition, and evidence artifacts. A not-run run requires an explicit reason and has neither commands nor attempted observations.

## Candidate, command, and environment binding

Attempted, failed, and passed execution evidence binds one Story 12.5 candidate commit and one integrated accepted Story 12.4 final commit. Dirty product state is invalid. Each Repository is bound to one Profiled Revision. Prompt, packet, provider-stream, document schema, CodeGraph, JDT LS, Java, PostgreSQL, and provider model/config identities are recorded without secrets. The environment also binds the Track C attestation process/public key and exact raw-adapter plus attestation-sink type/version/assembly hashes. A blocked or planned envelope may omit candidate/environment only when the gate was not reached; the blocking observation proves why. Track B runtime telemetry has no candidate/run/case/signature/export fields and is never accepted directly as release evidence.

Every executed command has a stable ID, redacted exact command, SHA-256 of that redacted command, exit code, UTC start/end, duration, and bounded stdout/stderr artifact references. Passed runs require exit code zero for every bound command. Provider evidence includes exact model, API type, provider alias, a bounded non-secret configuration identity, and its fingerprint; credentials, headers, endpoint secrets, and connection strings never enter evidence.

Artifacts from another candidate cannot replace a failed/missing check. A production correction pins a new candidate and reruns invalidated tiers. Semantic validation rejects self-supersession, a missing `supersedesRunId` target, lineage to a later run, and command/observation/artifact references that do not resolve within the same envelope or named prior envelope where lineage alone is allowed.

## Database preflight

Before any product connection:

1. generate a unique lowercase database name beginning `vulgata_story125_`;
2. validate the closed name grammar and length;
3. compare against the configured main development database and known Story 12.4 qualification database identity;
4. fail with zero product read/write on any mismatch/ambiguity;
5. prove ownership before cleanup and remove only the exact owned database.

## Tier 1

No network LLM. Run focused contract, ranking, relationship, packet, authorization/currentness, citation, conversation, strict provider-stream framing/UTF-8/bounds/deadline/cancellation, streamed syntax repair, accepted-only UI reveal, UI mapping, corpus/schema, and database-guard tests. Record commands, exit codes, durations, candidate, and artifact hashes. No failing test is accepted.

## Tier 2

Use one clean disposable PostgreSQL database, neutral Java/XML fixtures, real Git/CodeGraph/JDT LS, and a controlled provider. Prove migrations on the current model, forced provider outage, persisted pause/retry, Vulgata process stop/restart, provider restoration, manual resume, completion, and duplicate prevention. Story 12.4 qualification and development databases are forbidden.

## Passed release receipt and Tier 3

Use host-run Vulgata and analysis tools, PostgreSQL 17 as the only required Docker service, two real sample Repositories, real CodeGraph/JDT LS/configured LLM, and 50-150 automatically selected File Code Units.

Acceptance requires:

- every high-value seed succeeds and ordinary failures stay in the accepted budget;
- at least one exact Cross-Repository REFERENCES Edge;
- at least one honest Uncertainty;
- at least three Business Logic Documents including one Cross-Repository flow;
- the complete fixed corpus, >=80% useful;
- 100% of accepted material technical claims cited;
- zero unauthorized evidence/existence disclosure;
- every accepted complete answer used mandatory provider streaming, no provider fragment reached the client before validation, and complete render finished under 30 seconds; and
- Vulgata process restart against the same database preserves authenticated wiki/Chat access without duplicate publication or data loss.

Every aggregate is backed by a named receipt rather than an unproved number:

- Scan counts receipt for File Code Units, seeds, and ordinary failures;
- exact-Edge query receipt;
- honest-Uncertainty query receipt;
- Business Logic Document query receipt;
- Cross-Repository-flow query receipt;
- Chat corpus result and packet/result artifacts with invoked-case packet fingerprints;
- same-process signed Track C attestation artifacts binding the immutable sealed-admission runtime observation ID/sequence to candidate/run/case/request, adapter/provider/streaming request, response headers, physical read/EOF, all byte-domain counters and terminal/deadline/rejection facts, plus the distinct matching coordinator completion summary ID and that summary's repair/validation/duration facts;
- restart before/after artifacts plus duplicate query receipt; and
- database preflight, ownership, and cleanup receipt.

Each receipt records observation time, query fingerprint, one or more command IDs, and one or more hashed artifacts. Passed-check records also require command IDs, attempted-observation IDs, and result artifacts. The schema permits no passed release receipt without every receipt above.

## Mandatory passed-check inventory

A passed run has exactly ten checks. Each ID occurs once, has the fixed tier below, has status `passed`, references only zero-exit commands in the same envelope, and carries artifacts reproduced byte-for-byte by a same-envelope command output or attempted observation.

| Tier | Mandatory check ID | Closed evidence |
| --- | --- | --- |
| 1 | `tier1-contract-ranking-relationship` | packet, ranking, v2 relationship, bounds and deterministic-order contract tests |
| 1 | `tier1-conversation-synthesis-security` | follow-up, primary/repair streaming, strict framing/UTF-8/bounds/liveness/cancellation, deadline, authorization, currentness, citation, no-fragment, and accepted-reveal tests |
| 1 | `tier1-production-ui-corpus-evidence` | production Scan UI, corpus, run-envelope/schema, semantic-validator and database-guard tests |
| 1 | `tier1-build-full-tests` | clean build plus complete automated test suite |
| 2 | `tier2-postgresql-restart-resume` | disposable PostgreSQL, real analysis tools, controlled outage, process restart, manual resume and duplicate proof |
| 3 | `tier3-candidate-tool-database-preflight` | clean candidate, accepted Story 12.4 commit, exact Repository/tool/provider identities, database ownership and topology |
| 3 | `tier3-real-scan` | two Repository Scan, 50-150 File Code Units, seed/failure budget, Edge, Uncertainty, Business Logic Documents and flow |
| 3 | `tier3-grounded-chat-corpus` | all fixed cases, per-case packet/result/stream artifacts, zero non-streaming accepted answers, usefulness, citation, leakage and timing |
| 3 | `tier3-restart-continuity` | same-database restart, authenticated wiki/Chat, duplicate/data-loss queries |
| 3 | `tier3-screenshot-release-integrity` | five screenshot categories, redaction, hashes, candidate binding and final manifest integrity |

Schema `contains`/`minContains`/`maxContains`, ten-item cardinality, and fixed ID/tier/status alternatives reject omitted, duplicated, unknown, mis-tiered, or non-passed mandatory checks. Semantic validation rejects dangling command IDs, nonzero command exits, artifacts absent from same-envelope command/observation evidence, or any cross-candidate/cross-run substitution.

## Aggregate recomputation

The semantic validator verifies every referenced artifact hash before parsing it, then derives every recorded aggregate from the named same-candidate receipt artifacts. It requires:

- `ordinaryFailureCount <= ordinaryFailureBudget`, with all Scan counts equal to the counts/query artifacts;
- `usefulCount <= caseCount` and `corpusResultCount = caseCount`;
- fixed-corpus IDs, corpus result-row IDs, and per-case receipt IDs to be identical sets with exactly one row/receipt per case;
- invoked cases to have one primary stream and at most one ordered syntax-repair stream; insufficient-evidence/authorization-boundary cases to have no packet or stream;
- ordered `packetFingerprints` to equal invoked-case fingerprints in fixed corpus order;
- raw/SSE/decoded-delta/final counts and bounds plus terminal/EOF/outcome to equal the immutable runtime observation and signed Track C attestation; the receipt's distinct `completionSummaryId` and repair/validation/accepted duration to equal the coordinator completion summary and attestation, with `nonStreamingAcceptedAnswerCount == 0`;
- `usefulPercent = floor((10000 * usefulCount / caseCount) + 0.5) / 100`, using exact nonnegative arithmetic, two-decimal round half up, and `usefulPercent >= 80.00`; and
- citation, disclosure, over-30-second, restart, duplicate/data-loss, and database ownership/access values to equal their named artifacts.

Positive vectors include exact 10/12 = 83.33 scoring, exact-limit streaming in every domain, one valid repair, one genuine sealed runtime observation plus signed Track C attestation, a non-invoked case, and all ten checks. Adversarial vectors split wire/SSE/JSON/delta/terminal boundaries; directly submit an unattested observation; alter runtime equality or candidate/run/case/request/process/adapter/provider/request/header/read/EOF/signature binding; provide a perfectly consistent SDK fake-chunk receipt lacking authorized adapter-to-sink provenance; and retain all earlier aggregate, coverage, content, and mandatory-check negatives.

## Screenshots and redaction

Required views include at least one screenshot in each of five mandatory categories: real Scan state, grounded Business answer with citations, grounded IT answer with Source Anchors, restart result, and release summary. Draft-2020-12 `contains`/`minContains` rules enforce each category independently, so duplicate views cannot substitute for an omitted category. Every screenshot records a relative evidence path, SHA-256, candidate binding, view type, production-state attestation, and redaction result.

Reject screenshots containing fake Backup B state, secrets, connection strings, credentials, absolute host paths, unrelated candidate output, or unbound/unreadable state.

## Evidence output

The closed run envelope and passed-only receipt follow schema `story125-release-evidence/2.2` in [story125-release-evidence.schema.json](story125-release-evidence.schema.json). Corpus cases follow [story125-evaluation-case.schema.json](story125-evaluation-case.schema.json). Observed results are immutable within one run directory; correction creates a new run identity, links `supersedesRunId`, and never overwrites failed or blocked evidence.
