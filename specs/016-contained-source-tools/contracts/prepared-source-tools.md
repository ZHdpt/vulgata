# Prepared Source Read Tool Contract (F0 slice)

This is the Story 18.1 Worker-facing contract for an application-issued prepared source grant. It is an internal Agent tool interface, not an HTTP endpoint, management CLI, Git source application or Scan work-selection interface. The implementation can choose C# type names, but must preserve these fields, semantics and closed authority surface. See [plan.md](../plan.md) IDs I01–I04 and [data-model.md](../data-model.md).

## Trusted issuance and context

The trusted application composition supplies an approved batch ID, Repository ID, subject, Worker-source-read purpose, opaque approval/root-binding identity, operator label and physical prepared-directory root to Infrastructure. An injected current-authority verifier checks that exact tuple at issuance and throughout every operation; it returns current matching approval, revoked/changed approval, or unavailable. A missing verifier or unavailable approval fails issuance closed; an unconditional production allow adapter is not permitted. Infrastructure opens/validates the root and issues one context with configured caps/deadline. The root path and operating-system handles remain private there. The Agent receives only this context through injection; no model-visible field or function can open/replace a grant. Story 18.1 directly qualifies issuance and calls using a controllable in-memory verifier, without database, batch management or model/provider dispatch. Future setup/integration owners may supply the production approval source; they cannot broaden this contract through tool arguments.

Before an operation opens or enumerates source, between entries or bounded read/search chunks, and immediately before releasing any result, Infrastructure awaits the current-authority verifier for the exact batch/Repository/subject/purpose/approval/root-binding tuple. A revocation signal may stop work earlier, but cannot replace these checks. Revoked/changed approval or verifier failure/unavailability latches the context closed across concurrent calls, cancels owned activity, disposes open handles/streams and invalidates all continuations. Buffered entries, counts, text, matches and tokens are discarded. A later restored approval requires a new grant; no cached allow authorizes another response.

Each tool result echoes the context's `repositoryId` and sanitized `outcome`, never a physical root, subject, approval identifier, Git commit, Profiled Revision, Source Snapshot ID, filesystem error message, stack trace or outside-root metadata. The operator label may be exposed only as a label and never as proof of content identity.

## Closed model-visible operations

| Tool | Allowed arguments | Result payload |
| --- | --- | --- |
| `list_prepared_source` | `relativeDirectory` (empty means root), `pageSize` 1–100, optional opaque `continuation` | Ordered entries and relative limitations, outcome, optional next continuation or exhausted limit. |
| `read_prepared_source` | `relativePath`, `byteOffset` >= 0, `byteCount` 1–65,536 | Strict UTF-8 text and exact `[byteStart, byteEndExclusive)` location when successful, observed file byte length when safely known, outcome, remaining range/limit. |
| `search_prepared_source` | `relativeDirectory` (empty means root), `literal` (nonempty single-line UTF-8, at most 256 bytes), `pageSize` 1–64, optional opaque `continuation` | Ordered matches with relative file, byte span, line/column, bounded excerpt, outcome, optional continuation or exhausted limit. |

Unknown JSON fields, alternate selectors, write/command requests, absolute paths, Repository/batch/root IDs and credentials are not part of the schema and are rejected. A caller cannot infer another root through a failure. Tool wrappers forward cancellation and return only the sanitized contract; they do not resolve filesystem paths.

## Paths, text and ordering

- A path is canonical `/`-separated Repository-relative text. Root is represented only by empty `relativeDirectory`. File paths are nonempty. Reject `.`/`..`, duplicate separators, backslashes, NUL, drive/UNC/rooted forms, Windows ADS/device aliases, unsupported platform names and any spelling that would normalize to a different target. No case-insensitive alias is used to broaden a grant.
- `list_prepared_source` returns immediate children of the requested directory, ordered by canonical relative path. Recursive discovery is caller-directed through returned directories. A symlink/reparse point may appear with `kind: non-traversable-link` and a limitation; neither tool follows it.
- `read_prepared_source` returns a bounded strict UTF-8 slice by byte offset. The requested slice must start/end at code-point boundaries; a UTF-8 BOM is accepted only at file start. Invalid UTF-8, embedded NUL or another encoding is `unsupported-text`; an invalid split is `invalid-range`. Offset exactly at EOF is `complete-empty`, including an empty file at offset zero; beyond EOF is `invalid-range`. A response capped before requested EOF is `limited` and gives `nextByteOffset` if capacity remains.
- `search_prepared_source` traverses regular files regardless of extension, matching the literal case-sensitively and ordinally in decoded text. Match locations include byte offset/length plus 1-based line and Unicode-scalar column; CRLF is one line break. Search does not execute regular expressions or source contents. An unfamiliar extension containing valid UTF-8 is searchable. Unsupported or unreadable file prevents a complete no-match claim for its scope.
- Ordering for search is canonical relative file path, then byte offset; a continuation resumes after the last returned match. A page-size stop is `limited`, even if it returns no match after consuming its work allowance.

## Outcome shape

The result envelope has `repositoryId`, `operation`, `outcome`, optional authorized `entries | text | matches`, optional `continuation | nextByteOffset`, optional `stoppingLimit`, and optional `limitations` with only relative locations and stable reason codes. `complete` and `complete-empty` are mutually exclusive. `limited` reports a page/work/byte stop and either a same-scope continuation/remaining range or an explicit exhausted cumulative limit. `incomplete` reports an unavailable source portion; already authorized partial items may be present, but are not full coverage. `denied`, `authority-unavailable`, `missing`, `unreadable`, `unsupported-text`, `invalid-range`, `changed-input`, `cancelled`, and `deadline-exceeded` remain distinct. Revoked/changed approval returns sanitized `denied`; verifier failure/unavailability returns sanitized `authority-unavailable`; both have empty payloads and no continuation. Invalid/foreign/replayed tokens yield the same sanitized denied shape.

The private continuation binds context lifetime, exact approval/root-binding identity, operation, exact path/query/page-size and observed traversal identities. It is one-use. It cannot select another batch, Repository, subject, purpose or root; changing any scope field is denied. Current authority is rechecked before a continuation is consumed and before its resulting page is released. A revoked/changed/unavailable approval invalidates all continuations and closes the context; a detected source change invalidates the affected continuation with `changed-input`. Deadline/disposal also invalidates all continuations. No cursor is durable or reusable across host restart.

## Representative examples

Permitted root list (illustrative, sanitized):

```json
{"repositoryId":"11111111-1111-1111-1111-111111111111","operation":"list","outcome":"limited","entries":[{"relativePath":"src/odd.dslx","kind":"regular-file"}],"continuation":"opaque-context-token","stoppingLimit":"page-size"}
```

Complete search with no match:

```json
{"repositoryId":"11111111-1111-1111-1111-111111111111","operation":"search","outcome":"complete-empty","matches":[],"continuation":null}
```

Invalid or foreign continuation:

```json
{"repositoryId":"11111111-1111-1111-1111-111111111111","operation":"search","outcome":"denied","matches":[],"reasonCode":"source-denied"}
```

The examples specify public shape, not token encoding or an implementation body. Any partial unreadable search has `outcome: incomplete` and a relative limitation; it cannot use the complete-empty example.

## Safety and completion invariants

1. The source of each returned byte is opened from a held root through handle-relative no-follow child opens, with symlink/reparse traversal rejected. A lexical path check alone does not establish containment.
2. Cumulative grant budgets and per-call caps apply to enumeration, bytes examined, bytes returned and operations; concurrent calls reserve atomically. Search over an unfamiliar file is bounded like any other file.
3. Revoked/changed/unavailable current approval, cancellation, deadline and disposal stop further disclosure and release owned handles/streams/iterators. A stopped call reports its distinct sanitized outcome, never complete; authority loss discards buffered partial payload and continuation.
4. Source is live. The adapter detects observed identity/metadata drift and refuses stale continuation; it cannot claim an immutable snapshot or repository-wide completeness across unobserved concurrent mutation.
5. Public errors are stable, sanitized and rooted in the granted Repository scope. No raw path, source fragment, outside metadata, subject, approval identifier, secret or stack trace appears in an error.
