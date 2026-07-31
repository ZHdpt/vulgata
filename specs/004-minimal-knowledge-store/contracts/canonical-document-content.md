# Internal Contract: Canonical Document Content

Schema identity: `vulgata.knowledge-document/1.0`

This is an internal Core→Infrastructure contract, not a public API, Agent tool,
Worker Agent result, or Scan input.

## Exact typed shape

```text
CanonicalDocumentContent
  schemaIdentity: exactly "vulgata.knowledge-document/1.0"
  documentType: CodeLogic | BusinessLogic
  stableKey: non-empty UTF-8 <= 4096
  title: non-empty UTF-8 <= 4096
  repositoryRelativePath: safe UTF-8 <= 2048
  summary: non-empty UTF-8 <= 4096
  keySymbols: distinct ordinal string[0..256], each <= 4096 UTF-8
  businessTerms: distinct ordinal string[0..256], each <= 4096 UTF-8
  technicalTerms: distinct ordinal string[0..256], each <= 4096 UTF-8
  sections: CanonicalSection[1..256], ordered by order

CanonicalSection
  stableSectionId: non-empty Guid
  order: contiguous 1..N
  kind: Overview | BusinessRule | ProcessStep | TechnicalDetail | Evidence | Other
  heading: non-empty UTF-8 <= 4096
  markdown: non-empty UTF-8 <= 65536
  searchText: non-empty UTF-8 <= 131072
  sourceAnchorIds: distinct Guid[0..512]
  documentVersionIds: distinct Guid[0..1024]
```

The JSON root permits exactly the ten listed top-level properties. A section
permits exactly its eight listed properties. There is no arbitrary `payload`,
metadata/extension bag, source/snippet/code carrier, credential field,
diagnostic field, or open discriminator.

Parsing uses maximum depth 4, disallows comments/trailing commas, rejects a
non-object root, duplicate property, unknown property, missing property,
unknown enum/schema value, invalid GUID, and non-canonical collection order.
After canonical Form-C normalization and UTF-8 serialization, the complete
object must be 1,048,576 bytes or fewer.

## Deterministic sensitive-content guard

Every string is Form-C normalized before validation. NUL and C0/C1 controls are
rejected except LF and TAB inside Markdown/search text. The following closed,
case-insensitive marker families are rejected in every free-text value:

| Family | Rejected deterministic markers |
| --- | --- |
| Credential/secret | `password=`, `pwd=`, `api_key=`, `apikey=`, `client_secret=`, `secret=`, `access_token=`, `authorization: bearer`, `-----BEGIN PRIVATE KEY-----`, `-----BEGIN RSA PRIVATE KEY-----`, connection-string segments containing both `Host=` and `Password=` |
| Host path | drive-rooted `[A-Za-z]:\` or `[A-Za-z]:/`; UNC `\\server\share` or `//server/share`; `file://`; Unix host prefixes `/home/`, `/Users/`, `/var/`, `/tmp/`, `/etc/`, `/root/` |
| Raw source carrier | any JSON property outside the allowlist; a three-backtick or three-tilde Markdown fence; `<pre`, `<code`; a four-space-indented nonblank Markdown line |
| Raw diagnostic | `Stack trace:`, `Traceback (most recent call last):`, `InnerException`, `SqlState`, a line beginning `Exception:`, or a stack frame line beginning ` at ` and followed by a qualified method name plus `(` |

Repository-relative paths are separately normalized with `/`, reject rooted,
escaping (`..`), empty/control-bearing segments, and are never inferred from
free text. HTTP routes such as `/api/orders` are allowed because they do not
match a listed Unix host prefix.

These rules define the acceptance proof for FR-019. The feature does not claim
semantic detection of every imaginable secret, source fragment, path, or
diagnostic. Inputs outside the exact typed schema or matching a prohibited
marker fail closed with `InvalidContent`; no raw value is returned in the
rejection.

## Required rejection fixtures

`KnowledgeStoreDomainTests` must include, at minimum:

- unknown `payload`, `sourceText`, `snippet`, `code`, `diagnostic`,
  `stackTrace`, `password`, and `apiKey` properties;
- a duplicate `title`, unknown document/section kind, excessive JSON depth,
  comment, trailing comma, and non-object root;
- `C:\agent\_work\repo`, `\\server\share\repo`, `file:///tmp/repo`,
  `/home/user/repo`, `/var/lib/repo`, `/tmp/repo`, and `/Users/name/repo`;
- `password=hunter2`, `api_key=abc`, `Authorization: Bearer abc`,
  a PEM private-key header, and `Host=db;Password=p`;
- fenced/indented source blocks and `<pre><code>`;
- .NET-style stack frame, `Exception:`, Python traceback, `InnerException`, and
  `SqlState`;
- every exact accepted byte/count limit and one-byte/one-item over-limit;
- safe counterexamples: Repository-relative `src/orders.cs`, HTTP route
  `/api/orders`, identifier `PasswordValidator`, and prose mentioning
  “authorization” without the rejected marker.

## Deterministic Markdown and projections

Markdown is rendered as one H1 title followed by each section's H2 heading and
Markdown in ascending order, with LF separators. It must be non-empty and at
most 1,048,576 UTF-8 bytes and pass the same guard.

Source-anchor IDs must belong to the committed version's supplied anchors.
Document Version references must be the source version itself or an authorized
exact endpoint represented by a permitted Edge. Rebuild parses this canonical
JSON and never reads `index.md`.
