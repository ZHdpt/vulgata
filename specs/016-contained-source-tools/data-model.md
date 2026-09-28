# Contained Prepared Source Data Model

These are transient contract values, not database entities. See [contracts/prepared-source-tools.md](contracts/prepared-source-tools.md) for operation fields and [plan.md](plan.md) for ownership decisions.

## Prepared source grant

| Field | Meaning | Authority/validation |
| --- | --- | --- |
| Batch ID | Current operator-approved batch scope | Trusted application issuer only; never model supplied. |
| Repository ID | One approved Repository | Trusted issuer binds; echoed in public locations. |
| Subject and purpose | Approved caller identity and Worker-source-read purpose | Trusted issuer binds; no model selection. The current-authority verifier checks both for every disclosure. |
| Approval identity/epoch | Exact currently approved grant generation | Trusted issuer captures and current-authority verifier returns the same binding; changed generation closes the context. |
| Opaque root-binding identity | Approval's exact prepared-root binding without exposing its host path | Trusted issuer/verifier compare it; a changed binding closes the context. |
| Operator label | Descriptive prepared-input label | Trusted issuer binds; not a Profiled Revision/Source Snapshot or authenticity claim. |
| Physical root and held root identity | Directory object allowed for this grant | Infrastructure private; absolute trusted configuration, opened without following links; never serialized to Worker/public result. |
| Limits/deadline | Cumulative operations, metadata entries, examined/returned bytes and time | Issuer may lower application maxima; atomic usage across concurrent calls. |
| Lifetime | Open, authority-closed, expired/cancelled or disposed | Revocation, changed approval or verifier unavailability latches authority-closed, invalidates cursors and settles owned handles; restoration needs a fresh grant. |

An open grant is immutable in scope: neither operation arguments nor a continuation can change its batch, Repository, subject, purpose, approval/root-binding identity or root. Its approval is not assumed immortal: the injected current-authority verifier returns the present approval binding, revoked or unavailable for that exact tuple. Issuance and every operation use this seam without requiring a database; an in-memory adapter supplies direct tests. There is no persisted grant, tree hash or source-version entity.

## Canonical source location

`repositoryId` plus a `/`-separated path relative to the granted root. A root listing uses the empty path; file operations require a nonempty path. Paths reject absolute/drive/UNC forms, empty or dot segments, backslashes, NUL, Windows alternate streams, device aliases and invalid platform names. A read location adds `[byteStart, byteEndExclusive)`; a search match adds zero-based byte start/length and 1-based line/Unicode-scalar column. Physical host paths never appear.

## Entry and match

**Entry**: relative path, `directory | regular-file | non-traversable-link`, optional observed byte length for a regular file, and sanitized limitation if an entry cannot be inspected. Filename extension does not change discoverability or admission. Links may be listed but cannot be traversed, read or searched.

**Match**: relative file location, exact byte span, line/column and a bounded text excerpt only if within the same grant's returned-byte cap. No result may contain an outside path or source from a different Repository.

## Request and continuation

List: relative directory (empty for root), positive page size up to cap, optional opaque continuation. Read: nonempty relative file, nonnegative byte offset, positive requested byte count at/below cap. Search: optional relative subtree root, nonempty bounded single-line literal query, positive match page size and optional opaque continuation. The continuation is context-local, one-use, exact approval/operation/request/limit bound and expires with the grant; Infrastructure privately holds bounded traversal state and observed identities. Revocation, changed approval or verifier unavailability invalidates every continuation before another page can disclose. Reject altered/foreign/replayed continuation as denied. Do not put a physical root in the cursor or public result.

## Result and state transitions

All responses carry `repositoryId`, operation, one outcome, sanitized reason/limit, returned authorized items/content if applicable, and a continuation or remaining range when further permitted work is possible. Outcomes are:

| Outcome | Meaning | Further access |
| --- | --- | --- |
| `complete` | Requested nonempty scope fully inspected | A new request may inspect again. |
| `complete-empty` | Requested scope fully inspected and has no entries/content/matches | A new request may inspect again. |
| `limited` | Page/work/byte cap stopped coverage without error | Same-scope continuation/remaining range if grant has capacity; otherwise explicit exhausted limit. |
| `incomplete` | A permitted entry/scope could not be fully inspected | Relative limitation; retry with fresh operation after cause resolves. |
| `denied` | Foreign/malformed authority/path/token or link traversal; revoked/changed approval | No content; authority loss closes grant and requires new approved grant. |
| `authority-unavailable` | Current-authority verifier failed or cannot establish approval | No content or continuation; grant closes and a new grant is required after authority recovers. |
| `missing` | Permitted entry absent | No claim about other scope. |
| `unreadable` | Permission/I/O prevented reading a permitted entry | No complete-empty inference. |
| `unsupported-text` | Byte content cannot be safely decoded/searched as strict UTF-8 | No no-match inference. |
| `invalid-range` | Requested range invalid or splits UTF-8 code point | Caller may choose a valid fresh range. |
| `changed-input` | Observed identity/metadata changed within operation or continuation | Cursor invalid; fresh operation/grant after source stabilizes. |
| `cancelled` | Caller or grant cancelled | No later content; owned activity settles. |
| `deadline-exceeded` | Grant deadline expired | No later content; owned activity settles. |

For a list/search page, `limited` means more observed scope is pending. `incomplete` from a source I/O gap can carry partial already-authorized results, but never a `complete` assertion. Authority loss or verifier unavailability discards any buffered partial results and continuation before the response. A failed read does not return an unverified partial text fragment as complete. Offset exactly at EOF is `complete-empty`, including a zero-length file at offset zero; beyond EOF is `invalid-range`.

## Invariants

1. Every public location is relative to the one bound Repository; root path and outside metadata are private.
2. Successful content comes from a no-follow handle-descendant of the held root, not a path reopened after a lexical check.
3. A page never reports complete if pending entries, unknown unreadable entries, a budget stop, cancellation or observed drift remains.
4. In-memory continuation and observed identity provide drift detection, not Source Snapshot equivalence.
5. Input labels never validate Git/source history; existing Git-bound contracts retain their original meaning.
6. An awaited current-authority check occurs at issuance, operation start, between entries/chunks and immediately before releasing any entries, counts, text, matches or continuation. A changed/revoked/unavailable approval latches the context closed across concurrent calls and settles owned activity; no cached allow or prior result substitutes for live authorization.
