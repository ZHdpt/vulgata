# OCT-EXPLORE consumer boundary

Status: required semantic agreement; B0 compiled checkpoint pending. This document does not prescribe competing Knowledge interfaces, DTOs or schemas. OCT-BUSINESS supplies actual symbols/types/fixtures in Core/Shared and serializes changes. No checkpoint commit is fabricated.

## Knowledge responsibilities required before wiring

1. Open an explicit authorized batch/publication selection for the real ordinary user, purpose, effective mode and scope. This is distinct from generator operator authentication/IOctoberBatchSelector.
2. Bounded permitted browse and exact content/section reads with membership checks, safe titles, structured content and qualifications. Exact-content responses are limited to 64 KiB UTF-8 text/JSON projection and use the Business owner's opaque section/chunk continuation without dropping text. Exact read never calls implicit current-version selection.
3. Bounded accepted-related reads using the same selection, typed directed exact endpoints and selected relationship evidence. Each returned field is authorized, including Uncertainties and contributor-derived prose.
4. Revalidate accumulated results at disclosure time using the same live all-contributor policy, or provide an owner operation that performs equivalent coherent authorized projection. Explore cannot invent the verifier or treat an old authorization result as permanent.
5. Fail closed for missing/mismatched publication, unrecognized handles, authorization outage or revocation. Safe public outcomes may distinguish malformed input, authentication requirement and temporary unavailability; unknown/unpublished/forbidden exact targets share a non-enumerating message with no target metadata. Owner may retain sanitized internal reason codes.
6. Bind pagination to selection/subject/mode/scope, enforce limits and cancellation, and return safe permitted incompleteness without total denied records or hidden topology. Adding a denied Document/Edge must not change visible counts, truncation, continuation behavior or empty-state content.

Business has specified the exact-content 64-KiB cap and protected continuation semantics; B0 still supplies the actual compiling types. Agreement must settle remaining page/bulk-related details. Explore's graph budgets (40 nodes/80 Edges, at most 8 reads/10 seconds) are upper bounds; lower shared caps win. If bounded acquisition ends early, label the permitted result incomplete. Do not keep fetching to fill a quota after an owner reports an uncertain bound.

## Exact-content continuation (F3)

Reference inspected: `C:/Users/13728/.codex/worktrees/f081/vulgata/specs/20-1-Oct14-business/contracts/knowledge-contract.md`, Consumer reads lines 52–57, SHA-256 `370A4F5DC8393C0BDBED2D286694EF4B0CD7540A91E5E867248472D1D98AA687` at inspection. This is a counterpart planning snapshot, not an implemented API or Chat concurrence.

- Consume the owner's ReadExact result and protected cursor unchanged. It binds database/batch/publication, subject/purpose/mode/scope, normalized query, operation/order/page limit, exact section/chunk position and expiry (owner default 30 minutes). Never inspect, mint or transplant a cursor, define local offsets or reuse browse/related cursors for content.
- Open returns the first chunk for the exact version and, if requested, exact section. `下一部分` makes exactly one bounded request with the same selection/handle and opaque cursor, with fresh live entry and pre-disclosure authority checks. Disable duplicate next actions; no automatic full-content drain or prefetch. Replace the one displayed chunk in owner order, carrying its adjacent material qualifications/provenance rather than accumulating all prior chunks. A claim continuing across chunks keeps its visible qualification on every fragment; B0 must provide structurally valid chunks and adequate safe qualifier context within the bound.
- Show partial-content status and next action while continuation exists; only owner-reported terminal state establishes full Document/section traversal. Do not treat a short chunk, failure or unavailable next request as completion. An exact section larger than 64 KiB is directly opened/focused at its first portion, visibly incomplete, then continued within that section. Missing section never falls back to the Document or another section. Valid UTF-8 and original narrative order/text are preserved across chunks; no silent drop, repeated append, split raw-JSON rendering or current-version substitution.
- Bind each request to view generation plus exact version/section and the full selection context. Clear the prior displayed chunk before loading next; release in-flight cursor on completion. Cancel, context change, access denial, service failure, timeout or invalid/tampered/expired cursor clears displayed content/qualifications/cursors and invalidates pending results; late responses cannot restore or mix chunks. Cancellation returns no partial chunk. Use metadata-free safe errors; offer an explicit restart of the same exact target with fresh selection/live authority, starting at the first chunk. Never auto-reopen a newer publication or reuse old continuation after restart.
- No new continuation types/framework, content store, source viewer or graph changes. Inability to meet these semantics is a B0 dependency and stops dependent reader wiring.

## Shared checkpoint and conformance

B0 publishes a compiling commit on the integration branch with symbol paths and a labelled shared fixture: exact CL/BL, at least two versions and sections, REFERENCES/GENERATED_FROM, Uncertainty, denied contributor and wrong-publication examples. Conformance demonstrates ordinary-user access, BL-only output, exact membership, section mismatch, contributor revocation and outage, safe pagination and relationship semantics. All consumers use these same types/fixtures; no local Knowledge DTO copy.

F3 fixture additions: a >64-KiB Document across sections and an individual >64-KiB section, multibyte text/claim qualifications across chunk boundaries, terminal and invalid/expired cursor outcomes, and revocation between chunks. Check ordered lossless traversal, exact section focus and zero mixed chunks after context changes. Minimal compile/conformance probes may validate the initial owner contract before dependent production implementation; no consumer DTO forks and no probe execution is authorized by these planning corrections.

Missing behavior is an owner dependency. Fixture-specific mocks implement published interfaces only, remain in the test/controlled host and are never selected by production on failure. Real-store readiness is a separate checkpoint from compiled fixture readiness.

## Reader destination owned by Explore

- `/knowledge/documents` — bounded browse of integration-selected publication.
- `/knowledge/documents/read` — exact reader, with encoded query fields `batch`, `publication`, `version`, optional `section`, optional requested `mode`. The shared owner's concrete scalar/opaque handle representation determines validation/encoding; no local GUID/hash assumption is imposed. If the owner uses a single opaque reference, adapt encoding in the one link builder at checkpoint, not separate per consumer.
- `/knowledge/graph` — bounded view of the selected publication, with explicit permitted scope from the shared selection.

Finalize encoding with B0/Chat/integration before E0 implementation. ExactDocumentLinkBuilder lives in Explore Web feature code and returns local destinations only. Chat passes an exact handle actually read during its turn; graph passes an exact displayed node handle. Reader rechecks current user/purpose/mode and owner membership. A requested mode cannot elevate server-effective mode; links never silently switch Business to IT. The reader rejects a CL reference while Business is effective without exposing its title/type.

Graph opening omits a section unless an accepted exact section reference exists. Chat section references must focus the exact section or fail explicitly. New publication availability never retargets an old link; if host policy disallows the old publication, return unavailable rather than substituting current data. No reader version-history browser is implied.

## Integration handoff

Integration wires the feature-local registration method and Business read services into ordinary Web using frozen DefaultConnection and explicit selection. Missing wiki remains an unavailable feature while login/admin work. No October migration, operator credential or generator provider configuration is required at Web startup. It adds ordinary-user entry points within existing Chat-first navigation and replaces management placeholders with links; it does not create a new top-level product area or weaken management authorization.

Explore supplies the registration signature and route/link contract; integration owns Program/navigation/shared route and package changes. Chat owns its footer use of that link builder. Safe display data must never be logged as a fallback error. This initial lane uses Blazor in-process calls and exposes no new agent tool or public REST API.
