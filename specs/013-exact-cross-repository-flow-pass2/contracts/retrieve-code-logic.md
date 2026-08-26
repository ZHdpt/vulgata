# Contract: `retrieve_code_logic`

## Function declaration

`retrieve_code_logic` is a strict, closed Worker Agent function. It is the only Pass 2 evidence-expansion tool.

The caller supplies one operation and operation-specific selectors. It cannot supply System, Repository, Scan Run, Profiled Revision, authorization scope, provider, endpoint, Flow Group membership, source root, absolute path, SQL, graph query, network target, shell command, CodeGraph request, or JDT-LS request.

## Operations

### `Search`

Input: bounded normalized query text and optional closed section-kind filter.

Behavior: deterministic lexical matching over current authorized Code Logic Document Index Entries and Document Sections inside the job's System/two Profiled Revisions. Exact symbol/path/endpoint/heading matches precede prose matches; ties use canonical Document/section identity.

### `Sections`

Input: one or more opaque current Document handles and optional opaque section handles.

Behavior: return at most the requested current sections, in section order, subject to four sections per Document and all job-wide bounds.

### `SourceAnchors`

Input: one or more opaque Document/section handles.

Behavior: return current Source Anchor descriptors authorized by those handles. Absolute paths are never returned.

### `SourceExcerpt`

Input: one opaque Source Anchor handle and optional contained byte subrange.

Behavior: read exact Profiled Revision source through `IRevisionBoundSourceReader`; require path/range/hash equality with the current anchor; return only the contained excerpt.

### `RelatedDocuments`

Input: one opaque current Document handle and direction `Outgoing` or `Incoming`.

Behavior: follow only one hop of current persisted REFERENCES Edges within the authorized System/pair. GENERATED_FROM, raw graph relationships, uncertainties and a second hop are not traversal inputs for this tool.

## Job-wide bounds

Inclusive maxima:

- 8 reserved calls;
- 10 returned result descriptors;
- 4 distinct additional Document Versions outside the initial Flow Group;
- 4 returned sections per Document Version;
- 65,536 cumulative provider-visible returned UTF-8 bytes.

The initial Flow Group evidence is not charged as retrieval, but every retrieved result is charged by its exact serialized provider-visible bytes.

## Reservation and admission

1. Revalidate current job lease/state, Flow Group/pair binding and authorization.
2. Atomically reserve the next call ordinal. Reservation consumes call capacity before I/O.
3. Read only through the closed operation.
4. Revalidate every candidate result currentness/authorization/revision/hash.
5. Canonically order and deduplicate the candidate response.
6. Project result count, distinct additional Documents, per-Document sections and exact serialized UTF-8 bytes.
7. If any inclusive bound would be exceeded, persist a `BoundExceeded` receipt with zero results/bytes/Documents/sections and return the constant refusal.
8. Otherwise persist one content-dark receipt and result descriptors atomically, then return opaque handles plus bounded content.

## Duplicate and replay semantics

- Same job/call ordinal/request fingerprint reconstructs the same descriptor set and returns it without double charging.
- Same ordinal with a different request conflicts and discloses no evidence.
- A later call with the same arguments consumes another call and returned result/byte budget; already-counted distinct Documents do not increment the distinct-Document count again.
- A reserved call lost before receipt remains consumed and returns no handle.
- Retry/restart does not reset any counter.

## Opaque evidence handles

Every accepted initial/retrieved item has a deterministic opaque handle bound to:

- Pass 2 Job and provider attempt;
- exact Document Version/Repository/Scan Run/Profiled Revision;
- optional Document Section, Source Anchor, source byte range or REFERENCES Edge;
- complete evidence fingerprint and returned-byte count.

Handles carry no decodable scope or source. A handle from another job/attempt, stale Document, changed anchor/hash, or unauthorized scope is invalid and yields the same closed rejection.

## Refusal outcomes

Closed responses are `NoResults`, `BoundExceeded`, `Refused`, and `Unavailable`. They contain a constant machine code, current remaining numeric budget, and zero hidden-resource detail. They never reveal whether an excluded Document exists.

## Provider-decision immutability

Tool output cannot add or replace the exact resolution decision, Provider, shared document Edge, endpoint, Flow Group member, execution-instance group fingerprint/step, consumer-entry stable key, or cross-scan Logical Document stable key. Another decision sharing the same Edge is not interchangeable evidence. If retrieved content suggests a different relationship, it may be cited only as an unresolved statement and the final result cannot assert it as the deterministic flow.

## Persistence/privacy

Receipts/results persist only fingerprints, identities, bounds and content length. They do not persist query text, section/source text, endpoint text, absolute paths, provider request/response, tool arguments, secrets, reasoning or logs. Provider-visible content exists only in the bounded current attempt.
