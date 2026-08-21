# Feature Specification: Resumable File Pass 1 & Code Logic Documents

**Feature Branch**: `demo0831/story-12-3`

**Created**: 2026-08-21

**Status**: Draft

**Input**: Story 12.3 consumes one exact approved two-Repository Prepared Scan Manifest pair, runs bounded resumable Pass 1 work, and commits trustworthy immutable Code Logic Documents without allowing provider outages or peripheral file failures to erase completed work.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Dispatch the Approved Pair Deterministically (Priority: P1)

As a SystemOwner, I can start Pass 1 for exactly two approved Prepared Scan Manifests and know that every prepared File Code Unit has one durable Worker Agent job whose identity and presentation remain stable under concurrency, restart, replay, and different completion orders.

**Why this priority**: Every Document and recovery decision depends on a complete, authorized, revision-bound job set; a partial or unstable queue would make later evidence untrustworthy.

**Independent Test**: Start the same approved pair repeatedly at concurrency settings one, two, and four, deliberately vary completion order and restart mid-run, and verify identical job identities, Document identities, reconciliation candidates, pair ordering, and display ordering with exactly one job per File Code Unit.

**Acceptance Scenarios**:

1. **Given** exactly two complete, mutually consistent, approved Prepared Scan Manifests for distinct authorized Repositories in one coordination, **When** the SystemOwner starts Pass 1, **Then** one deterministic Scan Job Execution is created for every prepared File Code Unit and no other file is scheduled.
2. **Given** the shared concurrency setting is absent, **When** jobs are claimed, **Then** at most two Worker Agent jobs run concurrently across both Repositories.
3. **Given** the shared concurrency setting is any integer from one through four, **When** jobs are claimed, **Then** the configured pair-wide bound is enforced; a value outside that range is rejected before dispatch.
4. **Given** two runs have identical approved inputs but jobs finish in different orders, **When** their results are compared, **Then** job identity, Document identity, reconciliation outcome, pair progress order, and display order are identical.
5. **Given** one manifest is missing, incomplete, unapproved, stale, outside authorization, or not the exact partner in the approved pair, **When** Pass 1 is requested, **Then** no job is created or dispatched.

---

### User Story 2 - Navigate Only Bounded Authorized Evidence (Priority: P1)

As a Worker Agent, I can request narrowly defined code-navigation evidence for my assigned Repository and Profiled Revision, so that I can explain the file accurately without gaining general tool, host, network, or scheduling access.

**Why this priority**: Navigation improves Code Logic Document accuracy, but an open tool loop or stale/cross-Repository evidence would violate the source and authorization boundary.

**Independent Test**: Exercise every allowed `navigate_code` operation against current and missing Document targets, then attempt every excluded access and every call/result/byte/depth boundary; verify bounded immediate evidence, deterministic refusal, no new scheduled file, and no information outside the authorized revision.

**Acceptance Scenarios**:

1. **Given** a valid running job, **When** it requests definition, references, implementations, callers, callees, symbol search, related files, or exact source evidence within its Repository and Profiled Revision, **Then** `navigate_code` returns at most 20 authorized results for that call.
2. **Given** an exact target has a current Code Logic Document for the same Scan Run, Profiled Revision, and current source hash, **When** navigation requests that target, **Then** relevant Document Sections and Source Anchors are preferred.
3. **Given** no eligible current Code Logic Document exists, **When** navigation requests the target, **Then** bounded revision-verified source evidence is returned immediately without waiting for another job.
4. **Given** a Worker has reached eight calls, 64 KiB of cumulative navigation evidence, or explicit navigation depth three, **When** it asks for more evidence, **Then** the request is rejected with a bounded outcome and no evidence or work is added.
5. **Given** a request targets another Repository, another Profiled Revision, a stale source hash, raw language-server messages, raw graph storage/query access, shell, network, or an unrestricted filesystem path, **When** `navigate_code` evaluates it, **Then** the request is rejected without disclosing the target.
6. **Given** evidence remains insufficient at depth three, **When** the Worker finishes analysis, **Then** it records a structured deferred observation and makes no unsupported claim.

---

### User Story 3 - Commit One Authoritative Code Logic Document (Priority: P1)

As a SystemOwner, I receive one evidence-linked Code Logic Document per successful File Code Unit, with all display, retrieval, and search representations derived from one validated authority.

**Why this priority**: Independent generated representations can disagree; one authoritative result plus deterministic projections makes the wiki explainable and replay-safe.

**Independent Test**: Submit valid and systematically mutated Worker results, verify that only valid `code-logic/file-v2` JSON creates an immutable Document Version, and independently reproduce its Markdown, Document Sections, Index Entry, Source Anchors, and deterministic identity.

**Acceptance Scenarios**:

1. **Given** a Worker returns valid bounded `code-logic/file-v2` JSON, **When** Vulgata validates it, **Then** it contains file purpose, internal units, business/code rules, data flow, communication evidence, references, deferred observations, and Source Anchor associations.
2. **Given** a material technical claim in the result, **When** the result is validated, **Then** the claim has at least one exact Source Anchor inside authorized current source.
3. **Given** authoritative JSON is accepted, **When** the Document Version is committed, **Then** Markdown, ordered Document Sections, and the Index Entry are deterministically derived from that JSON without separate LLM authorship, summarization, or paraphrasing.
4. **Given** a result has an invalid or mismatched job, authorization, System, Repository, Scan Run, Profiled Revision, File Code Unit, path, file/source hash, line or byte range, referenced Document, Communication Kind Classification, evidence bound, schema, or source anchor, **When** commit is attempted, **Then** no Document, projection, Edge, anchor, or completed-job state is committed.
5. **Given** the same completed result is submitted after an acknowledgement is lost, **When** atomic commit is retried, **Then** exact replay returns the existing Document Version without mutation or duplication and any changed replay conflicts without mutation.

---

### User Story 4 - Pause and Resume Without Losing Work (Priority: P1)

As a SystemOwner, I can survive provider unavailability, isolated transient failures, process restart, and manual resume while preserving every atomically completed Document.

**Why this priority**: The feature exists to prevent an LLM outage or one peripheral failure from discarding an otherwise useful Scan.

**Independent Test**: Force provider, job, integrity, security, cancellation, and unclassified failures at every durable state; exercise the five provider probes and process restart; then verify the exact pause/fail/retry mapping, Chinese manual-resume action, revalidation, and duplicate-free continuation.

**Acceptance Scenarios**:

1. **Given** a provider-transient failure, **When** recovery is attempted, **Then** new dispatch stops pair-wide while at most five probes become eligible in order after nominal windows of 10 seconds, 30 seconds, one minute, two minutes, and five minutes.
2. **Given** all five probes fail, **When** the fifth probe finishes, **Then** the affected Scan persists `PausedProviderUnavailable`, retains completed Documents and pending jobs, and exposes the Chinese `继续扫描` manual-resume action.
3. **Given** one isolated job-transient failure, **When** its retry becomes eligible, **Then** that same deterministic job is retried once; a second transient failure becomes one ordinary file failure rather than a third attempt.
4. **Given** an integrity or security failure, **When** it is classified, **Then** the Scan fails closed immediately and no further job is dispatched.
5. **Given** a provider-configuration or unclassified systemic failure, **When** it is classified, **Then** new dispatch stops and the Scan pauses for operator inspection or correction.
6. **Given** a process restart or manual resume, **When** the SystemOwner resumes, **Then** authorization, Repository, Profiled Revision, affected source hashes, prompt/schema/runtime identities, provider state, and pair binding are revalidated; committed jobs are skipped, expired leases return to Pending, and only valid remaining work resumes.

---

### User Story 5 - Finish Honestly and Reconcile Exact Local References (Priority: P1)

As a SystemOwner, I can distinguish complete, completed-with-warning, paused, and failed Pass 1 outcomes, and exact local references can be added without rewriting immutable Documents.

**Why this priority**: Partial success is useful only when important gaps and the final graph truth are represented honestly.

**Independent Test**: Run paired manifests with seed and ordinary file failures at, below, and above the exact budget; then reconcile immediate and deferred observations under varied completion order and verify the terminal status, warning inventory, immutable Documents, and exact same-Repository REFERENCES Edges.

**Acceptance Scenarios**:

1. **Given** every job succeeds, **When** all Pass 1 jobs are terminal and reconciliation finishes, **Then** both Scans complete without a warning inventory.
2. **Given** ordinary failures are no more than `min(5, floor(10% of dispatched File Code Units))` and every high-value seed succeeds, **When** all jobs are terminal, **Then** the affected Scan completes as `CompletedWithWarnings` with a bounded failed-file summary.
3. **Given** any high-value seed fails or ordinary failures exceed the budget, **When** terminal accounting runs, **Then** Pass 1 fails and no later-pass work begins.
4. **Given** a deferred observation resolves uniquely to an authorized current Document Version in the same Repository, Scan Run, Profiled Revision, and current source hashes, **When** deterministic post-Pass1 reconciliation runs, **Then** it may create the exact directed REFERENCES Edge without changing either Document Version.
5. **Given** a deferred observation is ambiguous, stale, unsupported, cross-Repository, or has no exact current target, **When** reconciliation runs, **Then** no REFERENCES Edge is created and the immutable deferred observation remains truthful.
6. **Given** identical inputs complete in different orders, **When** reconciliation and display projection finish, **Then** the same ordered Edge set and warning inventory result.

### Edge Cases

- The approved pair contains two manifests for the same Repository, different coordinations, different Systems, or inconsistent Pair Selection Fingerprints.
- A manifest is internally complete but its approved Pre-Scan Handoff no longer passes ownership-filtered authorization or exact currentness checks.
- The shared concurrency setting changes while jobs are already leased; existing leases remain valid while new claims obey the effective persisted setting.
- Two claimers race for the same Pending Scan Job Execution, a lease expires during provider response handling, or an acknowledgement is lost after atomic commit.
- Provider recovery succeeds on any one of the five probes, a valid bounded `Retry-After` delays probe eligibility, or cancellation occurs while probing.
- A job fails transiently after one prior attempt recovered from an expired lease; persisted attempt state, not process memory, governs whether its one retry remains.
- Navigation returns duplicate targets, cyclical relationships, more than 20 candidate results, evidence that would cross 64 KiB, or a caller-supplied depth inconsistent with the server-maintained chain.
- A current Document Version exists for the target path but belongs to another Scan Run, Profiled Revision, Repository, source hash, authorization scope, or superseded Version Chain position.
- Exact source evidence changes between navigation and commit, or a Source Anchor crosses the File Code Unit segment boundary.
- Worker JSON is malformed, over 64 KiB, deeper than 32 levels, contains unknown or duplicate fields, unsafe control/sensitive content, unordered/duplicate identities, or a material claim without an anchor.
- A split File Code Unit shares its parent file with another segment; each job and Document remains segment-bound and cannot claim or anchor sibling source.
- The failure budget is zero, exactly on the threshold, or one over the threshold; attempts and provider probes never count as additional dispatched files.
- A deferred observation points to the source Document itself, would duplicate an existing exact Edge, creates a cycle, or becomes exact only after reconciliation has begun.
- One Repository completes while the paired Repository is paused; committed Documents remain visible and immutable, but pair-wide later-pass eligibility is not granted by this story.

## Requirements *(mandatory)*

### Functional Requirements

#### Approved pair intake and deterministic dispatch

- **FR-001**: Pass 1 MUST accept exactly one pair of two distinct, complete, mutually consistent, approved `prepared-scan-manifest/1.0` Prepared Scan Manifests exposed through the authorized Story 12.2 prepared handoff; one, three, partial, legacy-only, or independently combined manifests MUST NOT be dispatchable.
- **FR-002**: The pair MUST bind one coordination, one System, two distinct authorized Repositories, their exact Scan Runs and Profiled Revisions, the approved pair and manifest fingerprints, exact File Code Unit identities and hashes, analysis-readiness proofs, Communication Kind Classifications, Scan Context Snapshots, and both canonical ordinal domains. Any mismatch MUST reject the whole start before job creation.
- **FR-003**: A Pre-Scan Handoff alone MUST NOT authorize dispatch. A currently authorized SystemOwner start action and a valid pair coordination state MUST authorize Pass 1 without broadening Repository scope.
- **FR-004**: Pass 1 MUST create exactly one deterministic Scan Job Execution for every selected File Code Unit in both manifests, using the accepted Story 12.1 identity contract; it MUST NOT enumerate a different file, split or merge a File Code Unit, or recompute Story 12.2 selection.
- **FR-005**: The complete pair job set MUST become durably visible as one consistent start outcome before any Worker Agent job is claimed. Failure, cancellation, conflict, or stale input during start MUST expose neither a partial queue nor dispatch.
- **FR-006**: The shared concurrency limit MUST apply across both Repositories, default to two, accept only integer values one through four, and prevent more than that number of valid leases from being dispatched simultaneously.
- **FR-007**: Eligible job claim order and user-visible progress order MUST follow Story 12.2 Pair Selection Ordinal; Repository-local display MUST preserve Manifest Ordinal. Provider timing, claim races, completion time, database order, and host scheduling MUST NOT define either order.
- **FR-008**: Completion order MUST NOT change deterministic job identity, Logical Document or Document Version identity, canonical projection order, reconciliation identity/outcome, warning inventory order, or display order.
- **FR-009**: Each Worker input MUST be server-built from the assigned File Code Unit's exact source range, the bound Scan Context Snapshot as untrusted advisory context, and only same-manifest Communication Kind Classifications whose Repository, Scan Run, Profiled Revision, canonical path, and exact Evidence Locator fall within that File Code Unit. The Worker MUST NOT receive unrelated classifications or infer endpoint/provider ownership from a classification.

#### Closed bounded `navigate_code`

- **FR-010**: `navigate_code` MUST expose exactly these operations: definition, references, implementations, callers, callees, symbol search, related files, and exact source evidence. No generic operation or caller-selected underlying tool command is permitted.
- **FR-011**: Every navigation request and result MUST remain inside the job's authorized Repository, Scan Run, Profiled Revision, exact source hashes, and current ownership scope. An absent or unauthorized target MUST be indistinguishable from an unavailable target outside scope.
- **FR-012**: For an exact target, navigation MUST prefer a current Code Logic Document only when its Document Version and Source Anchors are authorized and bind the same Repository, Scan Run, Profiled Revision, and current target source hash; otherwise it MUST return bounded source/navigation evidence immediately and MUST NOT wait for another job.
- **FR-013**: A current-Document response MUST contain only relevant Document Sections and their Source Anchors. A source response MUST contain only contained Repository-relative identities, exact bounded ranges, current hashes, and evidence from the accepted closed analysis capabilities.
- **FR-014**: One Worker Agent job MUST make at most eight accepted navigation calls, receive at most 20 results per call, and receive at most 65,536 UTF-8 bytes of navigation evidence across all calls combined. Rejected or duplicate results MUST NOT be used to evade these bounds.
- **FR-015**: Each relationship-navigation call MUST traverse at most one hop, and the server-maintained explicit chain MUST never exceed depth three from the assigned File Code Unit. Cycles and repeated targets MUST NOT increase evidence or reset depth.
- **FR-016**: Exhausting the call, result, byte, or depth bound MUST return a bounded machine-readable outcome without partial truncation that could change meaning. The Worker MAY record a deferred observation but MUST NOT make an unsupported relationship claim.
- **FR-017**: Navigation MUST NOT create, claim, schedule, start, await, reprioritize, cancel, or mutate another File Code Unit or Scan Job Execution.
- **FR-018**: Navigation MUST NOT expose raw LSP/JSON-RPC, raw CodeGraph query or storage access, CodeGraphContext, shell/process execution, network access, unrestricted filesystem reads, host absolute paths, write access, or evidence from another Repository or Profiled Revision.
- **FR-019**: Exact source evidence MUST reuse the Story 12.1 revision-bound reader semantics: contained canonical path, exact requested range, current expected hash, bounded UTF-8 evidence, no silent clamping/truncation, and no absolute path disclosure.
- **FR-020**: Immediate known-Document evidence and source-fallback evidence for the same exact target MUST carry a stable canonical target/observation identity so that later reconciliation produces the same result regardless of which form was available during Worker execution.

#### Authoritative Code Logic Document result and projections

- **FR-021**: A successful Worker result MUST be one closed, schema-validated `code-logic/file-v2` JSON object of at most 65,536 UTF-8 bytes and JSON depth at most 32; malformed JSON, duplicate or unknown fields, unsupported schema identity, unsafe text, or excess bytes/depth MUST be invalid.
- **FR-022**: The authoritative JSON MUST bind deterministic job, System, Repository, Scan Run, Profiled Revision, File Code Unit, canonical path, exact byte/line range, segment identity, parent File Hash, Source Hash, prompt identity, document schema identity, analysis-runtime identity, and Prepared Scan Manifest identity.
- **FR-023**: The authoritative JSON MUST contain bounded file purpose and responsibilities; important internal units with kind, qualified symbol, signature, responsibility, and exact line range; execution and data flow; business and code rules with decision conditions; data reads/writes; communication evidence; known Document references; deferred observations; assumptions/unresolved evidence; and Source Anchor associations.
- **FR-024**: Every material technical claim, internal unit, rule, data-flow step, communication observation, and reference MUST cite at least one proposed Source Anchor. Advisory context MAY shape explanation but MUST NOT prove a claim, Edge, or source identity.
- **FR-025**: Proposed Source Anchors MUST bind the same Repository, Scan Run, Profiled Revision, canonical path, File Code Unit source range, and current source hash; line and byte ranges MUST be exact, ordered, nonempty, and contained. An anchor MUST NOT cross into a sibling segment.
- **FR-026**: Communication evidence referenced by the Worker result MUST be drawn only from the authorized classifications supplied under FR-009 or exact source/navigation evidence obtained under FR-010 through FR-020, and MUST preserve the distinction between a Communication Kind Classification and a resolved endpoint or REFERENCES Edge.
- **FR-027**: A known Document reference MUST target an allowlisted, authorized, current same-Repository Document Version with valid Source Anchors and current source hashes. The Worker MUST NOT create a Cross-Repository REFERENCES Edge or assert an unresolved cross-Repository target in this story.
- **FR-028**: A deferred observation MUST record the source claim/locator, relationship kind, bounded target evidence or unresolved symbol, navigation operation/depth, exact supporting Source Anchors, and a stable canonical observation identity; it MUST distinguish insufficient, ambiguous, stale, unsupported, and cross-Repository outcomes.
- **FR-029**: `ContentJson` MUST be the sole authoritative persisted representation of the Code Logic Document. Bounded Markdown text inside its fields is content, not an independently authored representation.
- **FR-030**: `ContentMarkdown`, ordered Document Sections, and the Index Entry MUST be deterministic projections of validated `ContentJson`. Their renderer MUST arrange, label, normalize, and index authoritative fields without LLM authorship, summarization, paraphrasing, invented claims, or completion-order input.
- **FR-031**: Projection output MUST remain within the accepted knowledge-store bounds of at most 256 Document Sections, 512 Source Anchors, 1,024 Edges, 256 search terms, and the existing content/Markdown/search byte limits. The stricter 65,536-byte Worker result bound remains authoritative for input.
- **FR-032**: One successful File Code Unit job MUST commit one immutable Code Logic Document Version associated with that File Code Unit. Its Logical Document, Document Version, sections, Index Entry, Source Anchors, direct REFERENCES Edges, commit fingerprint, and completed Scan Job Execution MUST commit atomically.
- **FR-033**: Before commit, Vulgata MUST revalidate current authorization and every identity, Repository, Scan Run, Profiled Revision, path, file/source hash, range, schema, prompt/runtime binding, navigation/evidence bound, referenced Document, classification, and Source Anchor. Any invalid value MUST commit no Document or partial associated record and MUST NOT mark the job Completed.
- **FR-034**: An exact lost-acknowledgement replay of an already Completed job MUST return its one identical persisted Document Version without mutation; any changed, missing, reordered, differently normalized, differently linked, or duplicate value MUST conflict without mutation.

#### Durable execution and classified failure policy

- **FR-035**: Story 12.1 deterministic job identity, leases, StateVersion transitions, atomic result/job commit, exact replay/conflict behavior, restart recovery, revalidation, and manual resume MUST remain authoritative. Story 12.3 MUST supply policy without weakening those contracts.
- **FR-036**: Story 12.2 Prepared Scan Manifest identity, pair/Repository ordering, exact hashes, analysis-readiness proofs, Communication Kind Classifications, File Code Units, canonical audit, and pair-wide atomic replay MUST remain authoritative and MUST NOT be recomputed or reinterpreted during Pass 1.
- **FR-037**: Provider-transient recovery MUST stop all new pair-wide dispatch while running jobs finish or become classified, and MUST allow no more than five probes in this order: 10 seconds, 30 seconds, one minute, two minutes, and five minutes. A valid bounded provider `Retry-After` MAY delay eligibility but MUST NOT add a probe or dispatch work while the circuit is unavailable.
- **FR-038**: Recovery on any probe MUST permit remaining valid Pending work to continue under the same shared concurrency bound. Five unsuccessful probes MUST persist `PausedProviderUnavailable` with completed Documents unchanged and Pending/Retry Scheduled jobs durable.
- **FR-039**: The paused provider-unavailable state MUST expose a Chinese `继续扫描` manual-resume action and a bounded reason/next operator step; no automatic background resume is required.
- **FR-040**: One isolated Job Transient failure MUST schedule at most one retry of the same deterministic job. Persisted attempt and retry state MUST govern the limit across restart; a second Job Transient failure becomes an ordinary terminal file failure.
- **FR-041**: A Job Permanent failure and an exhausted Job Transient failure MUST count once each as ordinary file-specific failures. Attempts, provider probes, recovered leases, exact replays, cancellations, and duplicate delivery MUST NOT inflate the dispatched-file denominator or failure count.
- **FR-042**: A Provider Configuration failure MUST pause immediately for operator correction; an Unclassified systemic failure MUST pause for inspection; a Cancellation MUST stop without automatic resume; and Integrity or Security failure MUST fail the affected Scan closed and stop new dispatch.
- **FR-043**: Manual resume and restart recovery MUST revalidate Repository access, exact Profiled Revision, affected File Code Unit hashes, prompt/schema/runtime identities, provider availability, and exact two-Repository coordination. Committed Documents MUST be preserved and skipped, expired Running leases returned to Pending, due nonexhausted retries recovered, and future/terminal jobs left unchanged.
- **FR-044**: All state changes, failure codes, probe eligibility, lease decisions, progress counts, pause/fail outcomes, and replay decisions MUST be durable and version-conditional. Raw provider responses, credentials, source content, host paths, and unsanitized process/tool diagnostics MUST NOT be persisted as status evidence.

#### Partial completion and deterministic same-Repository reconciliation

- **FR-045**: The dispatched-file count MUST equal the number of distinct prepared File Code Units in the accepted pair job set; each File Code Unit counts once regardless of attempts. The ordinary failure budget MUST equal `min(5, floor(10% of dispatched files))` using integer floor.
- **FR-046**: A high-value seed MUST be recognized only from the accepted Story 12.2 seed rule and manifest evidence: the first at most 20 Repository candidates with direct business score at least 400 under its canonical seed order. Story 12.3 MUST NOT rescore, replace, or expand the seed set.
- **FR-047**: The paired Pass 1 MAY reach a successful terminal outcome with warnings only after every job is terminal, every high-value seed has a committed valid Code Logic Document, pair-wide ordinary failures are at or below the FR-045 budget, and deterministic reconciliation has completed. Each child Scan containing an ordinary failed file MUST persist `CompletedWithWarnings`; a child with no failed file MAY complete without warnings. The pair outcome MUST expose one bounded canonically ordered failed-file inventory without source content or host paths.
- **FR-048**: If any high-value seed fails or ordinary failures exceed the budget, Pass 1 MUST terminate as failed and MUST NOT authorize Story 12.4 planning, Cross-Repository Resolution, Pass 2, or Business Logic Document work. Integrity/security failures retain their stronger failure classification.
- **FR-049**: A warning-free completion MUST require every prepared job to have exactly one committed valid Code Logic Document and deterministic reconciliation to complete without unresolved local commit error.
- **FR-050**: Post-Pass1 reconciliation MUST evaluate direct known references and deferred observations in a canonical order independent of Worker completion. It MAY create only an exact directed REFERENCES Edge whose source and target are authorized current Code Logic Document Versions in the same Repository and Scan Run, bound to the same Profiled Revision and current source hashes.
- **FR-051**: Reconciliation MUST be idempotent, reject self/duplicate/stale/ambiguous/unsupported/cross-Repository targets, and produce the same Edge set for the same authoritative Documents and observations. It MUST NOT rewrite, supersede, or otherwise mutate any immutable Document Version or its `ContentJson`.
- **FR-052**: An observation that cannot create an exact same-Repository Edge MUST remain as truthful immutable deferred content. Story 12.3 MUST NOT perform Cross-Repository Resolution or manufacture a provider, consumer, endpoint, Uncertainty resolution, or Cross-Repository REFERENCES Edge.

#### Runtime and explicit scope boundaries

- **FR-053**: The August runtime boundary MUST remain host-run Vulgata, CodeGraph `@colbymchenry/codegraph@1.5.0`, Java 21, and JDT LS `jdt-language-server-1.61.0-202608141332` over stdio, with PostgreSQL 17 as the only required Docker service. Machine-specific absolute paths MUST remain configuration and MUST NOT enter prompts, domain identity, generated evidence, or Document content.
- **FR-054**: Pass 1 MUST use only the closed Story 12.1 analysis capabilities and the `navigate_code` facade. CodeGraphContext, unrestricted Agent tool loops, raw analysis protocols/storage, shell, network, database tools, and Repository-supplied executable behavior are excluded.
- **FR-055**: This feature MUST NOT implement Story 12.4 Cross-Repository Resolution, endpoint-centered flow grouping, `retrieve_code_logic`, Pass 2 generation, Business Logic Documents, or Story 12.5 Chat, final release UI, evaluation questions, and release run.
- **FR-056**: Existing Story 12.1 and Story 12.2 accepted behavior and historical records MUST remain readable and semantically unchanged. Any proven compatibility correction required to connect this feature MUST be explicitly identified and routed during planning rather than silently altering an accepted contract.

### Key Entities

- **Prepared Scan Manifest Pair**: The exact approved Story 12.2 pair that binds two Repository manifests, pair fingerprint, canonical ordinals, File Code Units, classifications, readiness proofs, hashes, and Scan Context Snapshots; it is input authority, not a queue by itself.
- **Scan Job Execution**: The inherited deterministic durable execution record for one prepared File Code Unit, including state/version, lease, attempt/retry, failure classification, and at most one committed Document Version.
- **Navigation Session**: The server-maintained authorization and budget ledger for one running Worker Agent job, covering allowed operations, current Repository/Profiled Revision, call count, cumulative results/bytes, chain depth, and canonical target identities.
- **Code Logic Document**: The semantic explanation generated in Pass 1 for one bounded File Code Unit in this story. Its immutable Document Version is authoritative through `code-logic/file-v2` `ContentJson` and is associated with exact source through Source Anchors.
- **Document Section / Index Entry**: Deterministic retrieval and search projections derived from authoritative Document JSON; neither is an independent LLM-authored truth.
- **Deferred Observation**: A structured, anchored statement that navigation could not yet resolve exactly; it remains immutable Document content and may later support deterministic same-Repository reconciliation.
- **REFERENCES Edge**: A separate directed relationship between exact current Document Versions. Story 12.3 creates it only for exact same-Repository evidence and never by rewriting either Document.
- **Provider Recovery Circuit**: Pair-wide durable dispatch control that records provider probe eligibility and pauses new dispatch while availability is uncertain; it is distinct from a file job's one isolated retry.
- **Pass 1 Completion Summary**: Canonical terminal accounting of dispatched files, successful high-value seeds, ordinary failures, warning inventory, and reconciliation outcome.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across concurrency settings one, two, and four and at least ten deliberately different completion schedules, 100% of accepted File Code Units produce the same one-to-one job identities, Document identities, display order, and reconciliation result; zero unprepared files are dispatched.
- **SC-002**: In pair-start fault injection before and after every durable boundary, 100% of failures expose either the complete deterministic job set or no new job set, with zero partial cross-Repository queue visibility.
- **SC-003**: Every allowed `navigate_code` operation succeeds on neutral authorized fixtures, while 100% of cross-Repository, stale-revision, raw protocol/storage, shell, network, unrestricted-filesystem, and scheduling attempts are rejected without information leakage.
- **SC-004**: Navigation qualification never exceeds eight accepted calls, 20 results per call, 65,536 cumulative UTF-8 evidence bytes, one hop per call, or depth three, including duplicate, cycle, race, and over-bound fixtures.
- **SC-005**: For a representative valid authoritative JSON fixture, independent projection runs produce byte-identical Markdown, Document Sections, Index Entry, Source Anchor ordering, and commit fingerprint; no projection contains a claim absent from the JSON.
- **SC-006**: Every single-field mutation of job, authorization, Repository, Scan Run, Profiled Revision, File Code Unit, path, hash, range, schema, bound, reference, classification, or Source Anchor causes zero Document/projection/Edge rows and leaves the job non-Completed.
- **SC-007**: In lost-acknowledgement, concurrent-commit, restart, and expired-lease trials, each successful job has exactly one immutable Document Version, exact replay performs zero mutation, changed replay conflicts, and 100% of already committed Documents survive.
- **SC-008**: Provider-outage qualification performs no more than the five ordered probe windows, dispatches zero new jobs while probing, persists `PausedProviderUnavailable` after the fifth failure, and resumes remaining valid work through the one Chinese manual-resume action without duplicating completed Documents.
- **SC-009**: Each isolated transient job is attempted no more than twice across restart, while integrity/security trials stop new dispatch immediately and provider-configuration/unclassified systemic trials persist the required paused state in 100% of cases.
- **SC-010**: Boundary trials at zero, exactly the limit, and one over `min(5, floor(10% of dispatched files))` produce the exact failed/completed-with-warning outcome; any failed high-value seed prevents success regardless of remaining budget.
- **SC-011**: Across at least ten randomized Worker completion orders, deterministic reconciliation creates the same canonically ordered set of exact same-Repository REFERENCES Edges, creates zero stale/ambiguous/cross-Repository Edges, and mutates zero Document Versions.
- **SC-012**: A host-runtime qualification completes Pass 1 for the approved two-Repository scope with Vulgata, the pinned CodeGraph, Java 21, and pinned JDT LS on Windows and PostgreSQL 17 as the only required Docker service, with zero production dependency on CodeGraphContext.

## Assumptions

- Story 12.1 and Story 12.2 are completed authorities. Their accepted schema identities, canonical envelopes, typed analysis boundaries, persistence guarantees, and historical-row compatibility remain available at the pinned boundary.
- The two Prepared Scan Manifests belong to the same approved coordination and System and contain 50 through 150 selected File Code Units across the pair, so the ordinary failure denominator is never inferred from live enumeration.
- Story 12.2's high-value seed definition remains the sole seed authority; Story 12.3 consumes its accepted fields and does not add a new business-ranking algorithm.
- The effective Worker Agent provider is configured for the authorized System before dispatch. Provider probing checks availability only and never changes provider selection or credentials.
- A valid provider `Retry-After` affects when the next one of the five probes becomes eligible; it does not permit an unbounded wait, an additional probe, or dispatch during unavailability.
- A Code Logic Document may differ in explanatory wording when source fallback was used instead of an already-current Document, but its identity and the exact reconciliation outcome cannot depend on completion order; every accepted claim remains source-anchored.
- Existing Chinese management authorization and manual-resume API semantics are reused; this story requires the Chinese action and truthful state but excludes a new final release dashboard.
- Automatic background resume, broad provider failover, and later-pass eligibility are outside this story even when Pass 1 completes successfully.

## Authority Alignment

- **BMad Story 12.3** supplies the exact paired dispatch, `navigate_code`, `code-logic/file-v2`, provider/job failure, partial-completion, and same-Repository reconciliation acceptance boundary reflected in FR-001 through FR-052.
- **Story 12.1** remains authoritative for Scan Context Snapshot, Scan Job Execution identity/state, leases, atomic commit/replay, restart recovery, revalidation, manual resume, typed CodeGraph/JDT LS/source capabilities, and the pinned host analysis runtime. This specification adds Story 12.3 policy without changing those accepted meanings.
- **Story 12.2** remains authoritative for exact revision acquisition, File Code Unit identity and selection, hashes, ordering, readiness proofs, Communication Kind Classifications, Prepared Scan Manifest identity, atomic pair replay, and handoff. This specification consumes rather than recomputes them.
- **August 30 bounded-scanning design and approved change proposal** replace the deadline path for legacy PRD FR-4.1 through FR-4.5, FR-4.9, FR-4.12, FR-5.8, FR-6.1/6.2, FR-11.6, and NFR-1.1 where they prescribe broad orchestration, full graph copying, unrestricted Worker behavior, or general Cross-Repository work. The dated design preserves or strengthens immutable Documents, deterministic projections, Source Anchors, authorization, exact evidence, NFR-3.1 through NFR-3.3 recovery, and the failure budget.
- **August host-runtime correction** supersedes the dated application-container/JDT-LS ambiguity only: Vulgata and analysis tools run on Windows, PostgreSQL 17 is the sole required Docker service, CodeGraph and JDT LS identities are pinned, and long-term PRD NFR-4.1 remains deferred rather than deleted.
- **Ubiquitous Language and Constitution** remain peer authorities. This specification uses Scan, Scan Run, Profiled Revision, Prepared Scan Manifest, File Code Unit, Communication Kind Classification, Pre-Scan Handoff, Code Logic Document, Logical Document, Document Version, Source Anchor, and REFERENCES Edge in their canonical senses; Documents remain immutable and persistence stays behind specific domain ports.
- **No conflict or clarification remains**: Story 12.4 owns every Cross-Repository and Pass 2 behavior, and Story 12.5 owns Chat/UI/release evidence. Any plan that needs to alter an accepted Story 12.1/12.2 seam must identify a proven compatibility correction and route it explicitly.

### Authority Source Traceability

| Specification coverage | Authoritative sources |
| --- | --- |
| Story intent, five acceptance groups, dependencies, exclusions, and planned-change trigger | `docs/bmad/epics.md` Story 12.3; `docs/bmad/implementation-artifacts/12-3-resumable-file-pass-1-and-code-logic-documents.md` |
| Durable jobs, leases, atomic commit/replay, recovery, manual resume, typed analysis, and host runtime | `specs/010-durable-scan-foundation/spec.md`; `specs/010-durable-scan-foundation/contracts/durable-scan-contracts.md`; `specs/010-durable-scan-foundation/contracts/analysis-runtime-contracts.md` |
| Exact two-manifest handoff, File Code Units, hashes, classifications, ordering, high-value seeds, and atomic pair replay | `specs/011-java-xml-prescan-manifest/spec.md`; `specs/011-java-xml-prescan-manifest/contracts/prepared-scan-contracts.md`; `specs/011-java-xml-prescan-manifest/contracts/canonical-envelope.md`; `specs/011-java-xml-prescan-manifest/contracts/preparation-api.md` |
| `navigate_code`, `code-logic/file-v2`, deterministic projections, provider windows, failure budget, and reconciliation | `docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md`; `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md` |
| Exact CodeGraph/JDT LS/Java identities and PostgreSQL-only Docker boundary | `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-18-august-demo-host-runtime-and-jdtls-pin.md` |
| Legacy clauses, reliability/security outcomes, and dated replacements | `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md` FR-4.1 through FR-4.5, FR-4.9, FR-4.12, FR-5.8, FR-6.1/6.2, FR-11.6, NFR-1.1, NFR-2.3, NFR-3.1 through NFR-3.3, and NFR-4.1 |
| Canonical domain language, immutable Documents, authorization/security, project boundaries, and planned-change governance | `docs/UBIQUITOUS_LANGUAGE.md`; `.specify/memory/constitution.md`; `AGENTS.md` |
| Current seams that planning must assess without treating them as intent authority | `src/dotnet/Vulgata.Core/Entities/ScanJobExecution.cs`; `src/dotnet/Vulgata.Core/Entities/ScanExecution.cs`; `src/dotnet/Vulgata.Core/DomainServices/PreparedScanContracts.cs`; `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentJobPacket.cs`; `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentResultPayload.cs`; `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeDocumentMaterializer.cs`; `src/dotnet/Vulgata.Agents/WorkerExecution/WorkerAgentDispatcher.cs`; `src/dotnet/Vulgata.Agents/WorkerExecution/WorkerAgentResultValidator.cs` |

## Scope Boundaries and Planning Decisions

- The plan must use architecture-level review because this story adds a Worker Agent tool and materially changes Scan scheduling, concurrency, retry, pause/resume, dispatch, authoritative result shape, and reconciliation behavior. These are repository planned-change triggers, not unresolved specification intent.
- The plan must make the additive seam for warning-free completion, ordinary-budget failure, pair-wide provider circuit state, navigation-budget durability, and authoritative JSON projection explicit while preserving accepted Story 12.1 state/replay behavior and Story 12.2 immutable manifest behavior.
- The plan must decide exact canonical byte grammar and deterministic identifiers for `code-logic/file-v2`, deferred observations, projection order, and reconciliation Edge identity. Those decisions must implement FR-020 through FR-034 and FR-050 through FR-052 without changing WHAT this specification requires.
- The plan must identify the current legacy Pass 1 Worker/result/materializer seams that are reused, replaced, or isolated, and prove that historical Story 11 rows and accepted Story 12.1/12.2 records remain readable.
- After independent plan-review acceptance, the root must request planned-change authorization before task generation, citing the Worker tool, Scan behavior, dated PRD replacements, implementation intent, affected plan sections, and remaining gates.
