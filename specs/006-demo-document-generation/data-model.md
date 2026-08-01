# Data Model: Demo Document Generation

This feature adds closed in-memory contracts and reuses the durable Story 11.1
and 11.2 model. No table, EF configuration, migration, or model-snapshot change
is planned.

## Closed values

| Type | Values |
| --- | --- |
| `WorkerAgentJobKind` | `Pass1CodeLogicDocument`, `Pass2BusinessLogicDocument` |
| `WorkerAgentJobPayload` | `Pass1CodeLogicJobPayload` or `Pass2BusinessLogicJobPayload` |
| `WorkerAgentResultPayload` | `Pass1CodeLogicDocumentResultPayload` or `Pass2BusinessLogicDocumentResultPayload` |
| Result status | existing closed valid/non-valid statuses; only `Valid` carries a payload |
| Commit outcome | `Committed`, `Conflict`, `Rejected`, `Cancelled`, `Failed`; only `Committed` permits progress |

Unknown enum values, a kind/variant mismatch, zero/multiple variants, unknown
members, duplicate identifiers, and non-canonical ordering are invalid.

## Worker Agent Job Packet

Common immutable header:

| Field | Rule |
| --- | --- |
| Job/Repository/System/ScanRun IDs | non-empty; equal authorized durable parents |
| `ProfiledRevision` | normalized Git object ID, equal ScanRun and snapshot lease |
| kind/payload | exactly one matching closed variant |
| schema version | exact supported version |

### Pass 1 payload

Contains Code Unit ID, normalized Repository-relative path, positive inclusive
line range (at most 2,000 lines), source SHA-256, File Identity Hash, bounded
structural context, bounded communication evidence, and a sorted unique
allowlist of same-Repository target Document Version IDs. Source bytes are not
stored in the packet.

### Pass 2 payload

Contains prepared-flow ID/version, ordered finite trace evidence, and a sorted
unique non-empty set of exact current Code Logic Document Version projections.
Every member belongs to the authorized Repository, ScanRun, and Profiled
Revision. It contains no graph query, discovery rule, credential, or arbitrary
source carrier.

## Hydrated Worker Context (transient)

Created only after authorization and committed-snapshot verification. Pass 1
contains the exact source slice plus allowlisted job context; Pass 2 contains
the exact authorized Code Logic sections and prepared trace evidence. Maximum
Pass 1 source is 65,536 UTF-8 bytes; total request is 131,072 bytes. This object
is never logged or persisted and is disposed after dispatch.

## Valid Worker Agent Results

### Code Logic result

Contains packet identity echo, one bounded canonical Code Logic content model,
Source Anchors within the dispatched path/range/hash, same-Repository reference
requests whose targets are in the packet allowlist, and bounded communication
evidence. Collections are unique and canonically ordered.

### Business Logic result

Contains packet identity echo, one bounded non-technical canonical Business
Logic content model (purpose, ordered steps, outcome, evidence-linked
sections), Source Anchors, prepared flow evidence references, and a set of
Code Logic Document Version IDs exactly equal to the synthesis input. There is
no base `REFERENCES` request field.

Provider response size is at most 65,536 UTF-8 bytes and JSON depth at most 32.
Existing knowledge-store bounds for titles, sections, evidence text, anchors,
Edges, and projections remain authoritative and may be stricter.

## Prepared Flow Manifest (embedded, not durable)

| Field | Rule |
| --- | --- |
| schema/manifest/flow IDs | non-empty, fixed supported schema, one allowlisted manifest |
| expected System/Repository fixture | exact configured demo identity |
| members | non-empty ordered unique Pass 1 job/Code Unit identities |
| trace steps | finite, contiguous order; members only |
| evidence | bounded locators/labels; no raw source, credential, absolute path, or query |

At `Pass1Completed` each member resolves to exactly one current Code Logic
Document Version from the same ScanRun/Profiled Revision. Zero, missing, extra,
stale, duplicate, unauthorized, or ambiguous resolution is invalid.

## Existing durable aggregates reused

- `KnowledgeCommit`: one atomic aggregate with Logical Document, immutable
  Document Version, canonical content, Source Anchors, Edges, flow evidence,
  sections, and retrieval projection.
- Code Logic commit: only permitted same-Repository `REFERENCES` Edges.
- Business Logic commit: `GENERATED_FROM` targets exactly synthesis versions;
  no narrative-created base `REFERENCES` Edge.
- `ScanExecution`: existing phase/in-flight/progress/failure state, including
  nonterminal `Pass1Completed` and terminal `Completed`.

Stable operation/aggregate IDs derive from immutable job/flow identity so
identical replay maps to an identical `KnowledgeCommit`; supersession creates a
new immutable Document Version and preserves the Version Chain.

## State transitions

```text
Pass 1 job dispatched
  -> validated
  -> KnowledgeCommit Committed
  -> valid progress recorded

all prepared Pass 1 progress committed
  -> Pass1Completed
  -> exactly one prepared Pass 2 job dispatched
  -> validated
  -> KnowledgeCommit Committed
  -> Completed
```

Any hydration, dispatch, schema, safety, authorization, conflict, rejection,
cancellation, or persistence failure produces the existing bounded failure
transition, zero valid progress for that result, and never fabricates
`Completed`.

## Relational concurrency invariant (F003)

SQLite begins a supported serializable transaction and obtains its database-
wide write reservation through a parameterized no-op update of deterministically
ordered owner rows before reading/revalidating execution state. PostgreSQL keeps
row locking. This changes transaction order only and requires no migration.
