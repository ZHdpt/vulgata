# Contract: `code-logic/file-v2` Validation and Projections

The JSON Schema is necessary but not sufficient. This contract defines semantic validation, canonical writing, deterministic projections, and the atomic commit preimage.

## Semantic validation

Before canonical writing, Vulgata requires:

- UTF-8 without BOM; at most 65,536 bytes; JSON depth at most 32; no duplicate/unknown fields;
- every string NFC, LF-only, within its UTF-8 bound, free of control characters, credentials, raw diagnostics, host/UNC paths, fenced/indented source dumps, and unsafe markup;
- root binding exactly equals the server packet and durable pair/manifest/job/source/prompt/schema/runtime bindings;
- `byteStart < byteEnd`, `lineStart <= lineEnd`, every internal/anchor range contained in the assigned File Code Unit, and no sibling-segment crossing;
- source anchor keys exactly `a001..aNNN` in canonical byte/range/locator order, with no gaps/duplicates;
- every `order` array exactly `1..N` in stored order;
- every anchor reference exists; all material purpose/responsibility/unit/flow/rule/data/communication/reference claims have at least one anchor;
- assumptions with zero anchors remain explicitly `Unresolved` and are never rendered as established claims;
- `communicationKind` is exactly `Rpc` or `Http`. Every classification-evidence object exactly echoes one supplied contained Story 12.2 classification's ID, fingerprint, non-null kind, level, confidence, evidence rule, provenance, Catalog Rule Identity, and Evidence Locator; its kind must equal the enclosing authored kind. A null-kind classification, mixed kind, changed strength/rule/provenance/catalog/locator/fingerprint, or unsupplied identity rejects. Navigation-only communication evidence must cite a source-bearing current receipt whose exact anchors support the authored closed kind; `Unavailable`/`BoundExceeded` cannot prove a positive communication claim;
- navigation receipt references are accepted durable receipts for this job. A deferred observation with outcome `BoundExceeded` must cite at least one exact metadata-only `BoundExceeded` receipt/marker with matching operation/depth when present and cannot use that receipt as relationship evidence;
- known references target allowlisted current same-Repository Document Versions whose anchors still match current source;
- deferred target fields and outcome are consistent (`CrossRepository` never includes a disclosed Repository identity; `Stale` cannot be promoted to a known reference);
- observation, Source Anchor, section, Index Entry, Edge, content, and commit identities recompute exactly through the canonical-envelope contract;
- all derived projection bounds pass before any write.

Invalid trusted durable bindings classify as Integrity; forbidden scope/content classifies as Security; an ordinary provider-authored schema/semantic defect follows the bounded job-transient policy. Every class commits zero Document graph rows.

## Canonical ContentJson

The writer emits root properties in schema `required` order and object properties in their displayed schema order. It writes compact JSON, lowercase UUID/hash/revision text, invariant decimal integers, JSON `null` only for nullable fields, and no optional omitted fields. Arrays retain their validated canonical semantic order. There is no serializer/reflection/property-order authority.

The exact bytes become `DocumentVersion.ContentJson` and their SHA-256 becomes `ContentFingerprint`. PostgreSQL JSONB may normalize physical storage; replay uses the stored text semantic object to reproduce canonical bytes and compares the recomputed fingerprint.

## ContentMarkdown

The pure renderer uses fixed Simplified Chinese headings and authored text verbatim after escaping structural Markdown characters. It never summarizes or paraphrases:

1. `# {canonical path}`
2. `## 文件用途`
3. `## 内部单元`
4. `## 执行与数据流`
5. `## 业务与代码规则`
6. `## 数据读写`
7. `## 通信证据`
8. `## 引用`
9. `## 延后观察`
10. `## 假设与未决证据`

Empty groups are omitted. Items retain canonical order. Citation labels are deterministic `[A001]`, `[A002]`, and so on from anchor-key order. The renderer adds labels only; it invents no claim. Output must fit the existing 1 MiB Markdown bound.

## Document Sections

Create one section for each nonempty fixed group in canonical-envelope group order; maximum nine. Overview always exists because file purpose is required. `StableSectionId` derives from Document Version/group, and compact `SectionOrder` is 1-based after omissions.

| Group | `DocumentSectionKind` | Search/body source |
| --- | --- | --- |
| Overview | `Overview` | purpose + responsibilities |
| InternalUnits | `TechnicalDetail` | kind/symbol/signature/responsibility |
| ExecutionAndDataFlow | `ProcessStep` | execution + data flow |
| BusinessAndCodeRules | `BusinessRule` | kind/condition/effect |
| DataAccess | `TechnicalDetail` | mode/subject/description |
| CommunicationEvidence | `Evidence` | closed kind/observation only, with exact classification/receipt labels |
| References | `Evidence` | source locator + authorized target identity |
| DeferredObservations | `Other` | outcome/source locator/target locator; explicitly unresolved |
| AssumptionsAndUnresolvedEvidence | `Other` | unresolved authored text |

Section Source Anchor IDs are the canonical distinct union referenced by the group's items. Authorized Document Version IDs contain only known-reference targets for the References group. Each group's Markdown uses the same renderer fragment as ContentMarkdown; search text is a deterministic plain-text projection, not LLM output. Existing section byte limits apply.

## Index Entry

- Title: canonical path filename plus segment suffix when segment count is greater than one.
- Summary: file-purpose summary.
- Key symbols: distinct qualified internal-unit symbols, ordinal-sorted, at most 256.
- Business terms: distinct tokens from Business rule conditions/effects selected by the repository's existing bounded text tokenizer; ordinal-sorted, at most 256.
- Technical terms: internal-unit kinds, data subjects, communication kinds, and referenced symbols; distinct ordinal-sorted, at most 256.
- Search text: deterministic LF-joined plain text from purpose, responsibilities, unit symbols/responsibilities, rules, data access, communication observations, and deferred target symbols; no UUID/hash/raw source. Existing search byte limits apply.

Tokenizer output is a projection and cannot introduce a term absent from authoritative JSON. Culture-sensitive casing/order is forbidden.

## Source Anchors and Edges

The server derives Source Anchor UUIDs from exact fields; local anchor keys are not persisted. New v2 anchors require non-null byte and line bounds. Direct known references create only same-Repository, same-Scan-Run, current-source REFERENCES Edge drafts. Deferred observations create no Edge inside the Document transaction.

## Commit and Version Chain

Logical Document identity is server-derived from Repository/path/segment ordinal. The committer loads its current version. When different and authorized, the new immutable version is inserted and the old version is superseded through the existing Version Chain transaction. A job replay with the same deterministic Document Version ID never creates another version number or advances the chain.

Atomic write scope is exactly: Logical Document when absent, immutable Document Version, previous superseded pointer when applicable, Source Anchors, direct REFERENCES Edges, Document Sections, Index Entry, and Completed Scan Job Execution. No flow group, Uncertainty, Cross-Repository evidence, Pass 2, or Business Logic Document is written.

## Independent replay reconstruction

Replay loads authoritative ContentJson and relational graph, regenerates canonical bytes, Markdown, sections, index, anchors, direct Edges, all identities and CommitFingerprint, and compares them to the incoming result and persisted values. JSONB property order is irrelevant because the canonical writer is rerun; array order and authored text are material. Any missing/extra/reordered array item, changed normalization, projection drift, identity mismatch, or graph difference is `Conflict` with no mutation.
