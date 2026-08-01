# Phase 0 Research: Demo Document Generation

All planning unknowns are resolved at source boundary
`c59171b2e5fb726f0ca40e15dcdeea2749744d01`. No `NEEDS CLARIFICATION`
remains.

## Decision 1: Reuse and deliberately close the Story 11.2 dispatch boundary

**Decision**: Extend the existing `IWorkerAgentDispatcher`,
`WorkerAgentJobPacket`, result envelope, and Sequential Scan Runner. Model job
and valid-result bodies as exhaustive Pass 1/Pass 2 discriminated unions; do
not add dictionaries, `object`, arbitrary JSON, nullable parallel payloads, or
a second dispatcher.

**Rationale**: The accepted spec requires a strict typed union and FR-020
requires the existing sequential dispatch boundary. Exhaustive variants make
kind/payload mismatches and Pass 2 narrative-created base references
unrepresentable before persistence.

**Alternatives considered**: An open extension bag was rejected because it
admits unknown fields and raw-source/diagnostic carriers. Separate dispatchers
were rejected because they split lifecycle and provider policy. Reusing an
opaque result payload was rejected because it cannot prove closed schemas.

## Decision 2: Hydrate from the trusted committed snapshot lease

**Decision**: A Vulgata.Agents hydrator uses the existing committed Repository
snapshot lease seam, verifies the exact Profiled Revision before and after an
archive-only read, validates a normalized Repository-relative path, reads only
the inclusive requested range, and verifies source SHA-256 and File Identity
Hash before any provider call.

**Rationale**: The packet intentionally carries identities/hashes rather than
source. The established committed-snapshot seam binds later consumers to the
Profiled Revision and avoids mutable-working-copy time-of-check/time-of-use
drift. Double revision verification closes mutation/disposal races.

**Alternatives considered**: Reading the current checkout, accepting a path
from the provider, cloning again, or putting raw source in durable packets was
rejected for revision drift, duplicate Git ownership, path exposure, and
source-isolation risk.

## Decision 3: Use the centrally pinned Microsoft Agent Framework adapter

**Decision**: Implement the production dispatcher in `Vulgata.Agents` with
the centrally pinned `Microsoft.Agents.AI.OpenAI` 1.13.0 and
`Microsoft.Extensions.AI.Abstractions` 10.7.0 stack against the single existing
configured OpenAI-compatible endpoint. Qualify its structured-output request
shape against a loopback server, while keeping strict local validation
authoritative.

**Rationale**: The package is already centrally governed, AGENTS/story
authority requires the Microsoft Agent Framework OpenAI-compatible path, and
the existing provider configuration owns System/global task-role assignment.
Loopback qualification tests actual protocol behavior without a live provider
or credential.

**Alternatives considered**: Adding another provider abstraction or directly
using an unpinned SDK was rejected as architectural duplication. Dynamic
fallback/retry was rejected because the August 15 authority explicitly defers
it. Trusting provider-side schema enforcement alone was rejected because
compatible endpoints vary.

## Decision 4: Apply strict JSON preflight plus semantic post-validation

**Decision**: Cap raw response bytes before parsing, stream-preflight JSON for
duplicate keys/depth/trailing content/UTF-8 validity, deserialize with closed
source-generated metadata that rejects unknown members and enum values, then
validate identity equality, deterministic ordering, uniqueness, bounds,
authorization allowlists, source-anchor ranges, graph rules, secret/absolute
path/raw-source carriers, and normalization.

**Rationale**: JSON Schema alone does not consistently reject duplicate
members or express every cross-field/authorization invariant. Post-parse
validation alone can allocate from oversized input and can lose duplicate-key
evidence. Both are necessary for FR-004.

**Alternatives considered**: Permissive deserialization, schema-only
validation, repair/re-prompt, and accepting reordered collections were rejected
because invalid provider output must fail closed and no retry policy is in
scope.

## Decision 5: Use finite adapter bounds no looser than persistence

**Decision**: Bound a hydrated Pass 1 slice to 65,536 UTF-8 bytes and 2,000
lines, total provider request to 131,072 UTF-8 bytes, provider response to
65,536 UTF-8 bytes, JSON depth to 32, and every collection/text field to a
named contract constant. Existing `KnowledgeCommit` canonical limits remain
the final ceiling; any stricter existing field bound wins.

**Rationale**: These limits are large enough for a prepared Code Unit while
making cost, allocation, source transmission, and validation finite. Named
constants let packet, schema, parser, prompt tests, and materializer share one
policy.

**Alternatives considered**: Provider token limits alone were rejected because
they are model-dependent and do not bound UTF-8 input/output. Unlimited
collections and silent truncation were rejected because truncation changes
evidence truth and ordering.

## Decision 6: Resolve one embedded prepared-flow manifest at Pass1Completed

**Decision**: Store one schema-versioned prepared flow JSON resource under
`Vulgata.Agents/PreparedFlows/`. It declares ordered prepared Code Unit/job
identities and finite trace evidence. At `Pass1Completed`, an authorization-
scoped resolver maps every member to the exact current Code Logic Document
Version committed for the same Repository, ScanRun, and Profiled Revision,
then constructs exactly one Pass 2 packet.

**Rationale**: Version control makes the demonstration input reviewable while
runtime resolution pins the immutable Document Versions that did not exist
when the manifest was authored. Exact set equality prevents missing, extra,
stale, or provider-selected provenance.

**Alternatives considered**: General graph traversal/clustering was rejected
as deferred Story 5.8 behavior. A mutable database/user-supplied manifest was
rejected because it would widen scope and require API/authorization design. A
manifest containing pre-known Document Version GUIDs was rejected because the
versions are created by Pass 1.

## Decision 7: Add a narrow result committer and reuse KnowledgeCommit

**Decision**: Add one Core `IWorkerAgentResultCommitter` port taking the
dispatched packet, validated envelope, caller-supplied `RepositoryAccessScope`,
and cancellation token. Its Infrastructure implementation maps exactly one
variant to one existing `KnowledgeCommit` and calls
`IKnowledgeStoreRepository`. The runner awaits `Committed` before progress.

**Rationale**: This separates provider parsing from persistence without
creating a second Document repository. Story 11.1 already owns atomic
aggregate validation, authorization, idempotency, current-version concurrency,
Version Chain, and retrieval materialization.

**Alternatives considered**: Having the agent write EF rows, letting the
runner build database entities, or committing progress first was rejected for
layering, atomicity, and fabricated-progress risk. A distributed transaction
across knowledge and execution stores was rejected as unnecessary scope; stable
idempotent replay plus strict ordering fits the existing single runner.

## Decision 8: Make Pass 2 base-reference creation impossible

**Decision**: The Pass 2 result variant has only exact
`generatedFromDocumentVersionIds` and prepared flow evidence references. It
does not expose a `references`, semantic base-edge, or arbitrary graph-fact
field. The committer requires set equality with the server-built synthesis
input before mapping `GENERATED_FROM` Edges.

**Rationale**: The canonical scanning plan and data contract expressly forbid
Pass 2 narrative interpretation from creating base Code Logic `REFERENCES`
Edges. Omitting the capability is stronger than filtering narrative later.

**Alternatives considered**: Accepting reference suggestions then filtering
was rejected because it expands attack and test surface. Parsing narrative for
dependencies was rejected as a direct authority conflict.

## Decision 9: Correct F003 before production composition

**Decision**: For SQLite, begin the supported serializable transaction and
execute a parameterized no-op update on deterministically ordered Scan
execution owner rows before reading/revalidating state. Keep PostgreSQL's
existing row-lock path. Gate production DI regression on a real relational
SQLite two-context race test.

**Rationale**: Story 11.2 quality finding F003 identifies a production safety
precondition. Story 11.1 already established the supported provider-specific
write-reservation pattern. Acquiring the reservation after reading does not
close the stale-read window.

**Alternatives considered**: SQLite `FOR UPDATE` is unsupported. In-memory
locks do not coordinate processes/DbContexts. Serializing only test callers
would hide rather than fix the production seam.

## Decision 10: Reuse existing provider assignment and encryption boundaries

**Decision**: Resolve the System Worker Agent override or existing global
Worker Agent assignment, decrypt the key only in a scoped execution factory,
and use exactly that endpoint. Do not put credentials in Core contracts or
durable evidence and do not fall back to another provider.

**Rationale**: This satisfies PRD FR-3.1/FR-3.3 and NFR-2.2/NFR-2.3 without
inventing configuration. It also makes self-hosted isolation testable by
capturing the only outbound endpoint.

**Alternatives considered**: Storing a key in packets, passing decrypted
configuration through the runner, environment fallback, or automatic external
fallback was rejected for secret leakage and scope violation.

## Decision 11: No migration and no final-slice Docker/E2E in this story

**Decision**: Reuse Story 11.1 tables/materializations and Story 11.2 execution
state. Add only in-memory contracts, resources, services, transaction-order
correction, and composition. Run current-boundary integration, Web build, and
full tests; retain the clean-database two-Repository rehearsal and final Docker
image build in Story 11.5.

**Rationale**: All required durable concepts already have owners, this story
does not change container/runtime packaging, and the expedited exception
assigns final-slice checks to Story 11.5. F003 changes locking order, not schema.

**Alternatives considered**: Adding provider-response or prompt tables,
persisting hydrated source, or moving final-slice verification earlier was
rejected as unauthorized scope and duplicate evidence.

## Decision 12: Planned-change authorization is triggered twice

**Decision**: After an independent plan review accepts these artifacts, pause
before task generation for explicit user approval of (a) the production Worker
Agent adapter/tool execution change and (b) the prepared Pass 2 Scan strategy
extension.

**Rationale**: AGENTS.md and the expedited exception require the pause. The
effects, implementation intent, and exact plan/PRD/spec citations are recorded
in `plan.md#planned-change-authorization-mandatory-pause-after-accepted-plan-review`.

**Alternatives considered**: Treating the changes as ordinary internal wiring
or relying on deadline approval alone was rejected because the August 15
exception explicitly preserves planned-change authorization.
