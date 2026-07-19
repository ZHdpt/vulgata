# Research: CodeGraph Code Unit Extraction

**Date accessed**: 2026-07-18
**Provider target**: exact `codegraphcontext==0.5.1`

## Decision: the loopback HTTP JSON gateway is the production provider boundary

**Decision**: Use `cgc api start` on unpublished `127.0.0.1` and read-only
`POST /api/v1/query` as the production projection transport after one
whole-Repository index.

**Rationale**: The official HTTP reference documents the gateway, loopback
host/port selection, health endpoint, and read-only query endpoint. It does not
publish the response envelope, is labelled 0.5.0, and does not establish the
exact 0.5.1 coordinate properties. The accepted Story 4.1 implementation
direction uses this local boundary, but a final-image real-provider contract
E2E must observe the exact envelope and properties before they become parser
authority. Vulgata still owns the projection and parser rather than treating
the provider's internal graph-store schema as a domain contract.

**Alternatives considered**:

- CLI-table parsing: rejected because it is presentation output without a
  machine-readable grammar.
- direct Kuzu access: rejected because it couples Vulgata to provider storage
  internals.
- provider MCP/tool routes: rejected because Story 4.1 adds no MCP or Worker
  Agent tool.
- renewed production blocking on qualification: rejected because the accepted
  production direction explicitly continues despite the immutable harness
  defect.
- controlled fake JSON as provider-contract proof: rejected because it proves
  only Vulgata's parser behavior, not the installed 0.5.1 provider contract.

**Official evidence**:

- [HTTP API reference](https://codegraphcontext.github.io/reference/api/) —
  documents `cgc api start`, `GET /health`, and read-only
  `POST /api/v1/query`.
- [How ingestion works](https://codegraphcontext.github.io/concepts/how-it-works/)
  — documents graph commit followed by Cypher query serving.

## Decision: enumerate Code Units independently from relationships

**Decision**: Use two queries. Projection A returns all `File`, `Module`,
`Class`, and `Function` nodes without matching an edge. Projection B returns
only `CONTAINS`, `IMPORTS`, `CALLS`, and `INHERITS` endpoint tuples.

**Rationale**: A relationship-only match can return only nodes incident to at
least one selected edge. It necessarily omits isolated Functions, Classes,
Files, or Modules, contradicting the requirement to extract structural Code
Units. Separate projections make Code Unit completeness independent of edge
presence and allow relationships to be mapped after the complete node set is
known.

**Alternatives considered**:

- relationship rows that also create endpoint Code Units: rejected because
  isolated nodes remain invisible and node validation becomes relationship
  dependent.
- one tagged-union query: feasible, but rejected because independent responses
  make node coverage, relationship mapping, bounds, and tests clearer while
  retaining one immutable graph snapshot.
- one query per node/edge type: rejected because it expands request count and
  failure surface without adding contract value.

**Official evidence**:

- [Code graph model](https://codegraphcontext.github.io/concepts/graph-model/)
  — documents distinct File, Module, Class, and Function nodes and directed
  structural edges.
- [How ingestion works](https://codegraphcontext.github.io/concepts/how-it-works/)
  — documents declaration extraction before containment/import/call/inheritance
  linking, which supports treating node enumeration and edge enumeration as
  separate projections of the same graph.

## Decision: own the projection grammar and endpoint identity

**Decision**: Normalize every endpoint as
`(provider kind, relative path, name, start line, end line)`. Derive
`cgc-v1:<sha256>` over the NUL-delimited UTF-8 tuple and use that identity in
both projections and unresolved candidates.

**Rationale**: The official model supplies structural path/name/range
properties but does not establish a portable provider database ID. A
Vulgata-owned key built from provider-supplied structural metadata is stable
within and reproducible across the two projections, contains no source or
secret, and avoids Kuzu coupling. It also satisfies FR-010's requirement for a
non-empty provider target endpoint identity derived from provider structural
metadata.

**Alternatives considered**:

- provider `uid`: rejected because it is not part of the official graph-model
  contract and creates graph-store coupling.
- path plus symbol only: rejected because overloads/nested declarations can
  collide.
- persistence UUID: rejected because relationships need an identity before
  domain entities are created.

## Decision: File and Module normalization is explicit

**Decision**: A File requires a provider path; Vulgata derives absent display
name and exact full-file range from local source bytes. Class and Function
require path/name/range. A Module always contributes an endpoint tuple when it
has a name, but becomes a persisted Code Unit only when it also has a local
eligible path and valid range.

**Rationale**: The official graph model documents File path and Module name,
while declaration nodes have source coordinates. Domain Code Units require a
repository-relative file range. This rule preserves provider endpoints without
inventing a local Code Unit for an external or pathless module.

**Alternatives considered**:

- reject every pathless Module row: rejected because IMPORTS targets would lose
  a stable unresolved endpoint.
- invent a synthetic file/range for Module: rejected because it would violate
  Code Unit traceability.
- use import-line range as the Module's declaration range: rejected because an
  import site is not the target Module declaration.

## Decision: malformed rows are isolated; top-level failures are atomic

**Decision**: Invalid independent rows produce bounded warnings and are
skipped. Invalid UTF-8/JSON/envelope/query echo/counts, response or row-limit
overflow, endpoint-identity collision, process failure, timeout, or cleanup
failure rejects both projections atomically.

**Rationale**: Story AC-2 requires valid independent records to survive
malformed entries, but provider/process/top-level failure must persist no
provider-derived structure. A shared 64 MiB body cap, 250,000-row cap,
4,096-byte row cap, and 100-warning-detail sample bound memory and diagnostics
without silently truncating the graph.

**Alternatives considered**:

- persist Projection A before Projection B: rejected because a failed second
  query would create a partial provider result.
- silently truncate: rejected because missing nodes/relationships would look
  complete.
- fail the entire result for one malformed row: rejected because it conflicts
  with AC-2's independent-row behavior.

## Decision: attempt-0003 is immutable harness history, not a production gate

**Decision**: Preserve the exact boundary hash
`b9b5487b844d1c4ce89e2f03636e8b0c64f99f4dcbf6723a4f1e152ef138853b`
and terminal `failed / provider-contract-failure`. Do not retry, rewrite, or
use its empty structural evidence as provider incapability evidence.

**Rationale**: The recorded attempt successfully reached provider lifecycle
steps, but the qualification runner misrouted health/query payloads, lacked
dedicated HTTP command evidence, discarded structural evidence on failure, and
substituted empty evidence. The Story authority records the user's explicit
direction to continue production completion because this was a harness defect.

**Alternatives considered**:

- retry attempt-0003: rejected by its no-retry immutable boundary.
- retain the former production block: rejected because it contradicts the
  accepted production direction.
- reclassify it as passing: rejected because immutable failed evidence must
  remain truthful.

## Decision: exact final-image real-provider observation is mandatory

**Decision**: Add a separate mandatory production provider-contract E2E using
the exact final application image as its verifier base, the exact installed
`codegraphcontext==0.5.1` distribution, and a credential-free local Repository
fixture. The test invokes the two exact production query constants and emits
only the current closed provider-contract evidence schema.

**Rationale**: Q41-009/raw P1 correctly identifies that controlled fixtures
cannot establish a real provider envelope or resolve the
`start_line`/`end_line` versus `line_number` discrepancy. The smallest feasible
real-provider correction uses one heterogeneous graph match per projection,
because the pinned Kuzu multi-column UNION stalled, and the exact HTTP root
envelope is `status,message,data,error`.
The authority is direct observation of the exact runtime and exact queries. It
does not change Scan strategy, expose a tool, change the PRD, or revive the
qualification ledger.

**Required observations**:

- final-image identity and exact 0.5.1 package/lock identity;
- exact Projection A and Projection B request hashes and, when present in the
  observed envelope, exact query-echo hash equality;
- the envelope field names/types/required-null behavior actually returned;
- the coordinate source properties successfully evaluated for every returned
  provider kind and relationship endpoint—no speculative alias fallback;
- isolated node enumeration, all four supported relationship kinds, one
  locally filtered target mapped only as unresolved, and `cgc-v1` equality
  for endpoint tuples shared by both projections;
- no graph mutation between projections and successful bounded cleanup.

**Fail-closed rule**: A skip, absent/invalid/over-bound evidence file, query
drift, package/image drift, unrecognized envelope/property, identity mismatch,
missing coverage predicate, source-bearing evidence, or cleanup failure blocks
Story completion. Correct the owned grammar and implementation under review,
then rerun this new test. Never modify or retry attempt-0003.

**Alternatives considered**:

- official documentation alone: rejected because it does not publish the
  envelope and is not exact-version evidence;
- fake HTTP/process fixtures: retained for deterministic parser tests but
  rejected as provider compatibility evidence;
- direct Kuzu schema inspection: rejected because it creates the coupling
  FR-003 forbids;
- another qualification-ledger attempt: rejected because attempt-0003 is
  immutable and this is a distinct production contract gate.

## Decision: publish bounded structural evidence on failure

**Decision**: Publish closed `cgc-provider-contract-evidence-v2` for pass and
fail. Strict parsing still rejects the observed HTTP-200 top-level object.
A parallel streaming observer retains only status, JSON kinds,
raw allowlisted names or unknown-name HMAC metadata, counts, hashes, cleanup
facts, and failure category.

**Rationale**: The first real-provider failure discarded property names/types
and published nothing, making evidence-led grammar correction impossible.
Structural names and kinds reveal schema without retaining values, rows,
source, paths, symbols, raw JSON/query, credentials, or diagnostics.

**Alternatives considered**: raw payload/error retention (rejected as
source/path/diagnostic risk); category-only evidence (rejected because it
repeats evidence starvation); accepting any object or automatic alias probing
(rejected because both weaken fresh review).

## Decision: clean provider state by no-follow entry type

**Decision**: Under the prevalidated private root, accept absence, unlink a
regular file, recursively remove a directory without following links, and fail
on symlink/reparse/special entries.

**Rationale**: Exact 0.5.1 created `graph.kuzu` as a regular file; directory
removal caused `NotADirectoryError` after Context deletion.

**Alternatives considered**: always-directory or always-file deletion
(rejected because either assumes storage shape); following links (rejected as
an escape risk); ignoring cleanup failures (rejected because cleanup remains a
completion gate).

## Decision: one execution per accepted correction

**Decision**: No immediate rerun. Independent acceptance of this
observer/cleanup correction permits one execution. Failure publishes v2
evidence and blocks Story completion; a further run requires a fresh
evidence-led grammar/query correction and fresh acceptance.

**Rationale**: This creates a bounded observation/correction loop without
automatic probing. Projection hashes remain unchanged unless published
evidence proves a query change is necessary. Attempt-0003 is never involved.

## Decision: claim immutable history before provider reachability

**Decision**: Use
`TestResults/codegraph-provider-contract/<AcceptedCorrectionSha256>/` as the
immutable claim/history namespace. Atomic no-replace directory creation,
parent-directory sync, and durable create-new `claim.json` publication must
finish before image/provider reachability.

**Rationale**: A shared `evidence.json` cannot preserve later corrections or
enforce one execution. Directory creation is the earliest durable claim: every
crash, race, or partial state after it consumes the correction.

**Alternatives considered**: overwrite shared evidence (loses history); claim
after provider start (permits duplicates); delete/repair incomplete claims
(cannot distinguish pre/post-provider crashes); mutable claim status (creates
resume ambiguity).

## Decision: HMAC unknown names with an ephemeral key

**Decision**: Raw property names are permitted only for fixed safe envelope
vocabulary and exact production aliases. Represent every other decoded name by
HMAC-SHA256 under a fresh per-execution 256-bit CSPRNG key, UTF-8 length, JSON
kind, container ordinal, and first-seen ordinal. Never persist or log the key.

**Rationale**: Arbitrary JSON keys can contain paths, symbols, source, or
tokens. Unkeyed hashes permit dictionary recovery. Ephemeral keyed HMAC allows
same-run repetition analysis while preventing offline recovery and cross-run
linkage.

**Alternatives considered**: raw names (leakage); unkeyed SHA-256 (dictionary
risk); stored/global HMAC key (cross-run linkage/key-management risk); dropping
all unknown-name evidence (recreates evidence starvation).

## Resolved research outcome

There is no remaining `NEEDS CLARIFICATION`. The official graph schema supports
the two projections, while the owned grammar supplies the stable identity,
bounds, mapping, and malformed-row rules that the provider documentation does
not define. The exact envelope and coordinate source properties remain
candidate technical grammar until the mandatory real-provider gate observes
them; this is a fail-closed completion condition, not an unresolved product
decision.
