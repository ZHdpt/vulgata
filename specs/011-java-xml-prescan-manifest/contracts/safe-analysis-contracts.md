# Contract: Exact Source, Safe XML, and Closed Analysis Use

`File Code Unit` and `Prepared Scan Manifest` use their current authoritative glossary meanings. This contract's XML dependency is normative for task ordering, not merely implementation guidance.

## Exact acquisition

The exact snapshot service accepts only trusted Repository identity, nonempty Scan Run, canonical full `refs/heads/...` or `refs/tags/...`, and exact lowercase 40-hex commit. It verifies remote origin and ref resolution before and after archive materialization. The lease exposes a trusted binding token and logical snapshot SHA-256; absolute roots remain Infrastructure-only.

## Candidate artifact

The Story 12.2 enumerator returns canonical ordered audit records and bounded raw-byte artifacts. It never follows links/reparse points, never guesses encoding/language, and fails the Repository on path collision/escape. Java is strict UTF-8; XML is strict accepted UTF-8/UTF-16. File bytes above 4,194,304 are audited/skipped before parsing.

## CodeGraph proof

`ICodeGraphAnalysisPort` retains fixed operations and adds typed completeness fields plus one PreScan-related-files request. Complete requires initialized, state `complete`, zero pending added/modified/removed, no worktree mismatch, no reindex recommendation, zero pending references, exact logical bindings, trusted analysis paths, and matching Repository/Scan Run/revision/snapshot/runtime/index identities. Stable approved fields use canonical-envelope §4.2 and produce `CodeGraphProofIdentityHash`; duration and volatile telemetry are separate bounded audit metadata. Any failure/timeout before or within that hard-readiness proof rejects approval. Only after complete readiness may the related-file operation accept the at most 20 corrected high-value seeds, fixed `Calls|Imports|Inherits`, two hops, 200 unique eligible files, and five seconds; only that operation may Warning/fallback. Raw query/flags/database/shell are impossible.

## Java readiness

The JDT-LS factory accepts only the Story 12.1 `AnalysisScope`. Ready requires pinned release/checksum, Java 21, StreamJsonRpc 2.25.29 stdio, exclusive Repository/Profiled Revision workspace, initialize response, `initialized`, usable import state, and at least one document symbol for the ordinal-first eligible readiness file within 60 seconds. Stable approved fields use canonical-envelope §4.3 and produce `JavaReadinessIdentityHash`; XML-only uses its explicit `NotApplicable` Null/false/zero shape. Duration and volatile telemetry are separate bounded audit metadata. Diagnostics do not veto readiness.

Both proof hashes are reconstructed from persisted typed fields, never accepted as opaque caller/tool-provided digests. Changing a stable predicate/binding conflicts; changing measured duration cannot change proof/manifest identity. Failed readiness produces no approved proof envelope.

PreScan may use document symbols and implementations only to prove safe ranges and deterministic score evidence. It exposes no Worker tool and no `navigate_code`.

## Safe XML/catalog

The loader rejects any catalog value outside the exact FR-019 grammar. XML processing uses a strict encoding detector, byte map, bounded safe reader, immutable node projection, and non-evaluating exact range index. Its sole split-facing output is an immutable `SafeXmlDirectChildRangeSet` bound to canonical path, FileHash, encoding identity, document-element range, and ordered direct-child raw byte/line ranges. It is returned only after the safe parse and lexical index agree exactly; no split component can construct or amend ranges. DTD/entity declarations, XInclude, schema validation/network fetch, resolver, XPath/XSLT, scripts/plugins, executable expressions, and network access are absent or rejected.

Hard limits: 4,194,304 raw bytes; depth 64; 100,000 elements; 200,000 total attributes; 128 attributes/element; 16,384 decoded characters per evidence value. Any exceeded bound stops that file and emits one bounded deterministic outcome.

All rules evaluate the one parsed projection. A retained classification contains only closed kind/level/confidence/rule/provenance, canonical path, exact locator, occurrence count, and fingerprint.

After all behavioral GREEN evidence, `PreparedScanProductionLiteralQualificationTests` must inspect production assemblies, embedded resources/catalogs, and public output for fixture/release Repository names, canonical paths, endpoints, and line-number literals. Its JSON zero-match record is SC-006 evidence. A match routes to a new behavioral regression RED/GREEN and cannot be waived or repaired by changing the inspection denylist alone.

Production-literal and workflow/security qualification records, together with mandatory PostgreSQL migration/atomic-replay evidence, are prerequisites of one physically last final quickstart/build/full-test verification task. This contract has no qualification scheduled after that barrier.

## Split validation

An unsplit unit must be at most 65,536 bytes and 2,000 logical lines. Java split ranges are exact top-level JDT-LS ranges mapped from UTF-16 positions to strict UTF-8 byte positions. XML split planning accepts only a previously validated `SafeXmlDirectChildRangeSet`. The mandatory nonparallelizable order is: safe XML RED → projection/range-index GREEN → XML split RED → XML split GREEN → every US4 planner/manifest/store/coordinator/API consumer → every qualification observing XML ranges, including three-run release and final quickstart. Java split work may proceed after JDT readiness without that dependency. Missing/approximate/oversized/ambiguous ranges skip the entire file; no consumer constructs ranges or runs before XML split GREEN.
