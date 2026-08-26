# Contract: Pass 2 Runtime, Submission, and Atomic Commit

## Start and activation

Start accepts only the server-resolved identifier of one terminal Story 12.3 pair. The server reconstructs the current authorization scope, Two-Repository Scan Coordination, Prepared Scan Manifests, exact Profiled Revisions, Code Logic Document Versions, Source Anchors, classifications, exact same-Repository REFERENCES Edges, and closed Communication Pattern Catalog. Caller-supplied System, Repository, revision, endpoint, provider, Document, Edge, prompt, schema, model, or source identity is never authoritative.

One serializable PostgreSQL transaction performs the exact-resolution batch and Flow Group planning described by the companion contract, then creates or replays one `Pass2Execution`, all bounded groups/omissions/traces, one job and retrieval budget per complete group, and the coordination transition to Pass2. A job cannot be claimed until that complete set is committed. Exact relational replay writes nothing; changed replay conflicts with zero mutation.

The authenticated activation surface is:

- `POST /api/pass2-executions` with the Story 12.3 pair identifier only;
- `GET /api/pass2-executions/{id}` for sanitized state/counts/warnings;
- `POST /api/pass2-executions/{id}/resume` for explicit revalidation and wake-up after a manual pause.

Authentication failures are 401, policy failures are 403, and missing/outside-ownership resources are indistinguishable 404. Validation and state conflicts use ProblemDetails. Responses contain no source, endpoint text, catalog contents, provider payload, secret, host path, lease token, or hidden-resource distinction.

## Claim and recovery

PostgreSQL is the authority. An in-process activation is only a wake-up. For the dated release, one execution claims jobs serially in canonical Flow Group order and permits at most one active lease for that execution.

Claim locks the execution and eligible job, revalidates dispatch/circuit/currentness, increments the attempt, writes a bounded 30-minute lease, and returns an opaque claim token. It skips incomplete/excluded/failed/published groups and never rebuilds membership. A claimed runner revalidates the execution, group, its exact-match decision and independent contributions, that decision's reusable document Edge, current Documents, Profiled Revisions, authorization, prompt/schema/tool identities, and lease immediately before provider transport.

Restart performs no broad Repository discovery. Explicit activation may recover expired leases and due retry jobs for the named authorized execution. Published jobs replay; incomplete/excluded groups never become eligible because of restart. Existing retrieval reservations and admitted results remain charged.

## Prepared packet

The packet is projected from the durable Flow Group and current graph. It contains:

- exact job/execution/group and state-version binding;
- System, two Repository, Scan Run and Profiled Revision bindings;
- protocol, endpoint fingerprint, exact resolution decision and its reusable Cross-Repository Edge, pair-specific Flow Group identity, cross-version consumer-entry stable key, cross-scan Logical Document stable key/schema, group plan and membership fingerprints;
- canonical bounded evidence for the current consumer, provider XML and provider implementation Code Logic Document Versions;
- zero or more validated one-hop members;
- opaque initial evidence handles for every included section/Source Anchor/relationship fact;
- prompt, submission-schema and strict tool-declaration identities;
- current numeric retrieval budget.

The packet is at most 131,072 serialized UTF-8 bytes. It contains repository-relative paths only where required to explain a Source Anchor; it contains no absolute path, source root, credential, provider configuration, database connection, raw graph response, hidden Document, or unrestricted query capability. Packet construction reuses current Code Logic projections and `IRevisionBoundSourceReader`; direct JDT-LS/CodeGraph is absent by default and is never required.

## Strict streaming function loop

The executor reuses the characterized Story 12.3 streaming transport, including raw/request/response byte accounting, inactivity and nonresetting absolute deadlines, strict OpenAI-compatible declaration projection, DeepSeek-compatible request coercion, disabled-thinking policy where configured, zero SDK retry, sanitized status mapping, and terminal correction.

Exactly two local functions are declared:

1. `retrieve_code_logic`, with the closed operations and durable bounds in its companion contract;
2. `submit_business_logic`, whose single argument conforms to `business-logic-flow-v1.schema.json` and which has no evidence-access side effect.

Turns one through eight may contain only retrieval calls or one submission call. A multi-call batch is legal only when every call is `retrieve_code_logic`, the whole batch fits the remaining durable call budget, and all calls pass structural preflight before the first callback. Calls execute serially in provider order after preflight. Mixed, unknown, empty, duplicate-ID, over-capacity, or structurally invalid batches invoke nothing.

If no candidate is accepted, request nine advertises only named `submit_business_logic`. A locally rejected first complete candidate permits one correction on request ten using only a closed rejection category/repair code plus the exact original packet and server-owned retrieval result context. There is no request eleven. Local tool work and buffered replay do not reset the streaming inactivity deadline or absolute cap.

## Submission shape and local authority

The provider candidate must validate against `business-logic/flow-v1`, the exact job/group binding, the size limit of 65,536 serialized UTF-8 bytes, and these semantic rules:

- title, summary, participants, ordered flow steps, business rules, data movements, failure paths, and unresolved statements describe only the bounded group;
- every material claim cites one or more admitted opaque evidence handles;
- every cited handle belongs to the current attempt's initial packet or an admitted retrieval result and rehydrates to current authorized evidence;
- the asserted consumer, provider, protocol, exact decision, its REFERENCES Edge, pair-specific Flow Group and cross-scan Logical Document stable-key fingerprints equal the durable group binding;
- no unsupported relationship, additional endpoint/provider, changed membership, second-hop inference, or resolved claim from an Uncertainty is asserted;
- all ordinal domains are contiguous, unique, and canonical after normalization;
- all strings are valid bounded UTF-8 without control characters or secret-bearing fields.

The provider does not declare GENERATED_FROM Document IDs. Vulgata derives each section's backing Code Logic Document Version set and the global distinct used set from the cited handles. Eligible, grouped, retrieved, or returned-but-uncited Documents are not used. A cited additional Document must have an accepted durable retrieval descriptor.

The first complete rejected submission consumes the one local correction opportunity. Shape, binding, evidence, semantic, or provenance rejection has zero graph/job-completion mutation and returns only a closed category and deterministic repair code to the correction turn.

## Atomic publication

Under the current lease, one PostgreSQL transaction revalidates:

1. execution/job/group state and versions;
2. the group's first-class exact-match decision, its distinct provider identity, complete aggregate fingerprint and every decision-owned provider-evidence-contribution row, plus its shared-or-new Edge, immutable execution-instance group identity, cross-scan logical key, membership and trace;
3. current authorization, System, Repositories, Scan Runs and Profiled Revisions;
4. every cited initial/retrieved handle, Code Logic Document Version, Source Anchor, source range/hash, and REFERENCES Edge;
5. prompt/schema/tool identities and complete normalized candidate;
6. the handle-derived per-section and global used Document sets.

The transaction acquires a lock scoped to consumer Repository, `BusinessLogic`, and the exact persisted `business-flow/v1/<digest>` stable key. It resolves or creates the single Logical Document protected by the existing unique Repository/type/stable-key index, locks/revalidates its concurrency-token current pointer, and then creates or replays:

- the consumer-Repository-owned stable Logical Document identified by the cross-scan key, never by the pair-specific Flow Group ID;
- one immutable Business Logic Document Version and its ordered sections/index entries;
- exactly one GENERATED_FROM Edge to every distinct used Code Logic Document Version and no other target;
- attachment/publication of the already planned Flow Group and its unchanged Flow Trace;
- the job `Completed` state, completion fingerprint, committed Document Version and released lease;
- the execution published count/state version and, when exhausted, terminal summary.

Visibility is all or nothing. The current-version pointer and predecessor `SupersededById` move only with the complete version/provenance graph. A later scan pair with unchanged System/Repositories/protocol/normalized endpoint/consumer semantic entry appends a successor to that same Logical Document; changed endpoint or other stable-key input creates a separate Logical Document and leaves the old chain intact. Competing cross-pair publications serialize on the key; a loser reloads/revalidates the winner's pointer before a permitted successor append. No prior Document body, sections, index or provenance is edited or deleted.

Exact replay first reconstructs and compares the exact decision-to-Edge/contribution binding, stable-key preimage/schema, unique Logical Document, predecessor/current pointer, complete Document Version, sections, index, GENERATED_FROM set, execution-instance Flow Group/trace, job and execution counter graph. Another decision sharing the same Edge is never replay-equivalent. It returns the existing version with zero writes even after lease expiry or acknowledgement loss. A changed, reordered, partial, stale, semantic-key-mismatched, or concurrently mismatched replay conflicts with zero mutation until an independently valid serialized successor append is revalidated.

## Failure policy and terminal accounting

- One job-local invalid output or isolated transient failure permits at most one durable retry of the same job identity. Retrieval budgets do not reset.
- Provider transient/configuration/request-capability outcomes reuse the Story 12.3 source-free probe, circuit and manual-resume policy. No new job is claimed while dispatch is closed.
- Integrity or security failure closes dispatch and publishes no new partial result from the failing transaction.
- A terminal ordinary job failure marks only that Flow Group `Failed`; already published groups remain committed and later eligible groups continue canonically.
- `Incomplete` and `BoundExcluded` groups and `Pass2Omission` rows are terminal without jobs.
- Resolution REFERENCES Edges and Uncertainties are never rewritten by a later group/job outcome.

When every candidate is published, failed, incomplete, bound-excluded, or omitted, one transaction records exact denominator/counts and a canonical content-dark warning inventory. The coordination becomes `Completed` only when no warnings remain, otherwise `CompletedWithWarnings`. Zero complete groups or zero successful publications is an honest `CompletedWithWarnings` with zero Business Logic Documents unless a stronger integrity/security state applies.

## Logging and data stewardship

Operational logs/receipts may contain stable IDs/fingerprints, closed state/reason/category codes, counts, byte sizes, duration, retry/probe ordinal, sanitized route/model classification, and timestamps. They must not contain source or section text, endpoint text, catalog values, provider request/response/tool arguments, reasoning, prompt bodies, retrieval queries, repository roots, absolute paths, credentials, headers, database connections, or secrets.

Provider content crosses only the already configured provider boundary. This feature does not change provider selection, endpoint, key, failover, source-data policy, or host configuration.
