# August 30 Bounded Scanning and LLM-Wiki Design

**Date:** 2026-08-18
**Status:** Approved by Sprint Change Proposals on 2026-08-17 and 2026-08-18
**Target:** August 30 competition release  
**Authority:** This document is the approved correction to the current PRD,
canonical architecture, Epic 4/Epic 11 implementation, and the broader Epics
5–10 plan for the August 30 delivery boundary. Story planning remains subject
to independent plan review and planned-change authorization.

The 2026-08-18 host-runtime correction supersedes only this document's Docker
application-image, container-restart, and floating JDT-LS selection details.
The scanning, evidence, persistence, authorization, and quality gates remain.

## 1. Outcome and Defensible Claim

The August 30 release shall demonstrate one bounded, real, repeatable path:

```text
two Java/XML Repositories
-> lightweight deterministic PreScan
-> 50-150 automatically selected File Code Units
-> real Pass 1 Code Logic Documents
-> deterministic exact Cross-Repository Resolution
-> endpoint-centered Pass 2 Business Logic Documents
-> grounded Business/IT Chat with Document and Source Anchor citations
```

The release claim is:

> Vulgata automatically scans bounded Java/XML Repository scopes, generates
> source-anchored Code Logic and Business Logic Documents, resolves selected
> exact Cross-Repository communication, and answers questions from the
> generated LLM-Wiki with citations.

The release does not claim general 10,000-file validation, arbitrary protocol
resolution, autonomous multi-hop RAG, complete Epics 5–10, or unrestricted
multi-Repository orchestration.

## 2. Architectural Principles

1. Git Profiled Revision, File Code Units, Source Anchors, immutable Document
   Versions, Edges, Uncertainties, and persisted Scan state are authoritative.
2. CodeGraph and Eclipse JDT LS are rebuildable, revision-bound analysis
   services. Their private storage and raw protocols are never domain state.
3. Deterministic application code owns discovery, ordering, scheduling,
   authorization, retry, relationship decisions, and evidence validation.
4. LLM Worker Agents perform bounded analysis and synthesis. They do not own
   queue state, authorization, graph traversal policy, or relationship truth.
5. Source evidence has higher authority than generated Documents. Current
   Documents may accelerate analysis but never override the bound source.
6. Documents remain immutable. Resolution and later reconciliation create
   Edges or Uncertainties instead of rewriting historical content.
7. User-provided context is untrusted advisory input. It cannot establish an
   Edge, override authorization, or serve as a citation.
8. The release prefers closed contracts, explicit bounds, and truthful partial
   results over broad but unstable behavior.

## 3. Remove CodeGraphContext from the Release Critical Path

CodeGraphContext (CGC) shall not be installed, started, queried, or qualified
by the August 30 production path. Existing CGC code may remain temporarily to
avoid deadline-driven cleanup risk, but production composition and release
verification must not depend on it.

The replacement shall not imitate CGC's monolithic query protocol:

- CodeGraph supplies fast structural indexing and bounded symbol/relationship
  queries through a Vulgata adapter.
- Eclipse JDT LS supplies Java definition, reference, implementation, and
  document-symbol navigation through a Vulgata adapter.
- A revision-bound source reader supplies exact bounded source excerpts.
- A safe generic XML parser plus Communication Pattern Catalog extracts
  configured communication evidence.

No adapter may expose raw shell, raw LSP messages, raw CodeGraph database
access, arbitrary graph queries, or unrestricted filesystem reads to an Agent.

### 3.1 August demonstration runtime topology

Vulgata.Web, CodeGraph, Java 21, and Eclipse JDT LS run directly on the
documented Windows demonstration host. PostgreSQL 17 is the only required
Docker service. Application and tool installation paths are machine
configuration and never enter domain identity, prompts, or generated evidence.

The application Docker image remains a post-deadline NFR-4.1 deliverable. An
image build may be captured as optional evidence, but its failure does not fail
the August 30 release candidate. Application-process restart against the same
PostgreSQL database replaces application-container restart proof.

## 4. Complete Lightweight PreScan

PreScan completes before Pass 1 for each Repository:

1. acquire and verify the exact Git Profiled Revision;
2. enumerate contained files and apply deterministic filtering;
3. identify supported Java and XML files;
4. build and verify the complete CodeGraph index;
5. start Eclipse JDT LS for Java Repositories and establish workspace readiness;
6. parse relevant XML safely using the Communication Pattern Catalog;
7. create one File Code Unit for each eligible bounded file;
8. split only oversized files at safe top-level boundaries;
9. calculate exact revision-bound file and source hashes;
10. classify communication evidence as Confirmed, Hint, or Warning;
11. assign deterministic business-seeded file order;
12. capture the immutable Scan Context Snapshot;
13. atomically persist the Prepared Scan Manifest and approved handoff;
14. begin Pass 1.

PreScan performs no LLM call and does not create final Cross-Repository Edges.
Partial preparation is never dispatchable.

### 4.0 Production Prepared Scan initiation

The August two-Repository production entrance begins at the authenticated
Prepared Scan endpoint, not the historical Repository profiling endpoint. A
narrow application coordinator validates one visible System, exactly two
visible distinct Repositories, safe full Git refs, exact expected Profiled
Revisions, and the current prompt, Document schema, and Analysis Runtime
identities.

An Infrastructure-owned short transaction then creates or replays exactly two
revision-bound Scan Runs, two `ScanExecution.CreateNewPath` records, and their
canonical `TwoRepositoryScanCoordination.Create` record in `Preparing`. It uses
only existing tables, domain factories, and constraints. The transaction
exposes the entire initiation tuple or none of it and closes before source
acquisition or any Git, CodeGraph, JDT-LS, XML, Worker, LLM, or other
long-running operation.

An exact authorized retry returns the same tuple. A mismatch in System,
Repository pair, Profiled Revision, runtime identity, prompt identity, Document
schema identity, or authorization scope conflicts or fails closed. Concurrent
identical requests cannot publish duplicate or cross-paired tuples. If later
preparation fails, the tuple remains safely replayable and the existing
Prepared Scan pipeline resumes or replays its own work.

After initiation commits, the existing `PreparedScanCoordinator` owns source
acquisition, CodeGraph 1.5, JDT-LS, safe XML, deterministic selection, the
authorization recheck immediately before publication, and atomic Prepared Scan
Manifest/handoff publication. No old v1 profiling result can satisfy this
entrance. CodeGraphContext remains excluded.

### 4.1 File Code Units

Pass 1 operates on `CodeUnitType.File`. One bounded file produces one Worker
job and one Code Logic Document. Internal classes, methods, endpoints, and
rules become structured Document sections and Source Anchors, not additional
scheduled Code Units.

Initial bounds:

- source input: existing 65,536-byte and 2,000-line bounds unless the accepted
  plan establishes a smaller safe value;
- internal units: at most 64;
- rendered sections: at most 32;
- communication observations: at most 256.

Oversized Java files may split at JDT-LS top-level symbol boundaries. Oversized
XML may split only at safely parsed top-level elements. A file with no safe
bounded split is explicitly skipped with a reason; it is never silently
truncated.

For the exact two-Repository pair that completely matches the embedded reviewed
exact-resolution profile, one required Java unsupported-observation owner is a
closed exception to the generic path-valued `SymbolName` rule. The server-owned
profile authority supplies its fully qualified Java type symbol and reviewed
selector line. Prepared Scan must obtain current revision-bound JDT-LS document-
symbol evidence even when that file fits the whole-file bounds and require
exactly one top-level type on the same Repository-relative path whose exact
range contains the selector line and whose reported type name matches the
terminal segment of the profile symbol ordinally. The profile and JDT-LS
evidence must agree; neither can establish the binding alone.

The scheduled unit remains `CodeUnitType.File`. A bounded owner keeps its whole-
file range; for an oversized owner, only the exact safe segment containing the
reviewed line receives the fully qualified semantic symbol. That symbol is
bound through the File Code Unit's versioned canonical identity, persistence,
replay, prepared handoff, Pass 1 Worker packet, and resulting Source Anchor.
Path-valued units continue to use their existing identity bytes. Missing,
duplicate, ambiguous, wrong-path, wrong-name, non-containing, stale, malformed,
or unavailable JDT-LS evidence rejects the matched preparation before either
Manifest publishes. Nonmatching pairs do not perform this validation and retain
their existing selection and fingerprint behavior.

This exception adds no class- or method-level scheduling, public profile/path/
symbol input, new bound, database shape, or Story 12.4 behavior. It executes
after the accepted initiation transaction and exact revision/inventory checks,
before CodeGraph expansion, deterministic selection, and atomic Manifest
publication; no external analysis runs inside a database transaction.

### 4.2 XML Communication Evidence

XML parsing is generic and safe, not sample-Repository code:

- input must be a contained revision-bound path;
- DTD, external entities, XInclude, schema-network fetches, and custom
  resolvers are prohibited;
- file size, depth, element count, and attribute count are bounded;
- the file is parsed once and queried through a closed selector grammar;
- no XPath, XSLT, scripts, plugins, arbitrary regular expressions, or network
  access are accepted from the catalog.

The data-driven Communication Pattern Catalog may specify bounded globs,
namespace/local names, attributes, optional ancestors, fixed prefix
normalization, and endpoint/implementation extraction. Output contains the
file audit, File Code Unit, protocol classification, normalized endpoint
identifier where exact, evidence locator/range, and provenance.

The catalog must be proven with a neutral Java/XML fixture as well as the two
sample Repositories. No sample names, paths, endpoints, or line numbers may be
hardcoded in production code.

### 4.3 Deterministic File Ordering

High-value seeds are derived from general evidence:

- confirmed communication consumer/provider evidence;
- endpoints, controllers, and application entry points;
- XML communication declarations;
- JDT-LS-resolved implementations;
- conservative communication hints.

The ordering algorithm uses at most 20 seeds per Repository, two CodeGraph
hops over Calls/Imports/Inherits, 200 expanded files, and five seconds after
index readiness. Fixed scores and normalized path order break ties. Failure or
timeout falls back to deterministic production-path ordering and does not fail
PreScan.

The fixed reviewed exact-resolution release profile is the sole bounded
exception to the general-evidence seed rule. A server-owned adapter may derive
its required consumer, provider-declaration, provider-implementation, and
unsupported-observation paths from the existing embedded profile only after the
complete pair matches the profile's exact two Repository stable identities,
full Git refs, and Profiled Revisions. Matching safe eligible File Code Units
become mandatory high-value seeds within the existing 20-per-Repository and
150-pair bounds. Activation is all-or-none: missing, filtered, unsafe,
ambiguous, or out-of-bound required sources fail before Manifest publication.
A nonmatching pair receives no profile effect. No caller-supplied or arbitrary
sample profile, LLM, PageRank, or clustering chooses seeds, and production code
contains no sample Repository name, path, endpoint, or line number.

## 5. CodeGraph and Java LSP Runtime Contract

CodeGraph indexing is required for every prepared Repository. Eclipse JDT LS
is required when the prepared scope contains Java.

JDT LS readiness means:

- the server initialized successfully;
- the client sent `initialized`;
- project import reached a usable state; and
- `textDocument/documentSymbol` succeeded for a known eligible Java file.

Readiness does not require a clean compilation or zero diagnostics. Initial
timeout is 60 seconds. A readiness failure stops PreScan before LLM cost.

Typed adapter operations are limited to:

| Adapter | Operations |
| --- | --- |
| CodeGraph | index/rebuild, status, symbol search, callers, callees, related files |
| Java LSP | document symbols, definition, references, implementations |
| Source reader | exact revision-bound ranges and bounded excerpts |

CodeGraph means the MIT-licensed npm package
`@colbymchenry/codegraph@1.5.0`, published from
`https://github.com/colbymchenry/codegraph`, invoked by command `codegraph`,
with Windows x64 bundle `@colbymchenry/codegraph-win32-x64@1.5.0`. The qualified
installation must match these SHA-256 values: `npm-shim.js`
`80419260F06862D7A422D13EF69E3B24C7A21DBEF4309BCE41DB02AD642B1877`, platform
`node.exe` `B3094D0B49F9AD602262A9921551737BB97637C05DD357A06AE98188D7290AA3`, and
platform `lib/dist/bin/codegraph.js`
`03E4C791CC0DD91ED264278461BF9A56C0278AA0670D5942FC4732311C66DE03`.

The implementation shall pin Eclipse JDT LS to
`jdt-language-server-1.61.0-202608141332`, record and verify its exact installed
archive checksum, and use a Java 21 runtime. CodeGraph is likewise version- and
checksum-pinned. Each Repository revision receives an isolated JDT LS
workspace. Host-runtime verification checks both tools without building an
index during ordinary application startup.

The Java LSP adapter uses StreamJsonRpc `2.25.29` over JDT-LS stdio. The
Vulgata-owned adapter—not Worker or Core code—owns LSP header framing,
initialization and required notifications, cancellation, bounded timeouts,
sanitized stderr, graceful shutdown, and forced process cleanup. It is a closed
JDT-LS adapter rather than a general LSP client and exposes only the operations
listed above.

## 6. Worker Navigation

Pass 1 receives a small initial packet plus a typed `navigate_code` tool.
Allowed operations are definition, references, implementations, callers,
callees, symbol search, and related files. Calls are constrained to the same
authorized Repository and Profiled Revision.

Initial bounds:

- at most eight calls per Worker;
- at most 20 results per call;
- at most 64 KiB total navigation evidence;
- one relationship hop per call;
- explicit chaining to a maximum depth of three.

Navigation never schedules or starts another File Code Unit.

If a target already has a current Code Logic Document for the same Scan Run,
Profiled Revision, and source hash, navigation returns relevant Document
sections plus Source Anchors. Otherwise it immediately returns source and
navigation evidence; a Worker never waits for another file to finish.

If evidence remains insufficient at depth three, the Worker emits a structured
deferred observation and makes no claim. Deterministic post-Pass1
reconciliation may create a `REFERENCES` Edge when an exact current target
Document becomes available. It cannot rewrite the original Document.

## 7. Pass 1 Document Contract

The Worker returns a bounded `code-logic/file-v2` JSON result containing:

- file purpose and responsibilities;
- important internal units with kind, qualified symbol, signature,
  responsibility, and exact line range;
- execution and data-flow sections;
- business rules and decision conditions;
- data reads and writes;
- communication observations;
- known Document references;
- deferred navigation observations; and
- assumptions and unresolved evidence.

Every material technical claim references one or more proposed Source Anchors.
Before commit, Vulgata validates Repository, Scan Run, Profiled Revision,
paths, hashes, ranges, referenced Document identities, authorization, bounds,
and the closed schema.

`ContentJson` is the authoritative persisted representation. Prose fields may
contain bounded Markdown. Vulgata deterministically materializes:

- `ContentMarkdown` for display/export;
- `DocumentSection` rows for retrieval; and
- `IndexEntry` rows for search.

These are projections, not independent LLM-authored authorities. The renderer
assembles headings, ordered prose, and citation labels; it never summarizes or
paraphrases.

Deferred observations remain in immutable `ContentJson`. Post-Pass1
reconciliation creates separate Edges or Uncertainties. An unresolved local
observation remains a truthful navigation note.

## 8. Scan Context Snapshot

PreScan captures one immutable `ScanContextSnapshot` per Repository Scan Run.
It contains Global Context, System Context, Repository Context, effective
content hash, schema version, and capture time. It is an additive persistence
change.

Worker, Pass2, and Chat inputs use the same snapshot. Full bounded context is
stored, while prompt composition is capped near 12 KiB with priority:
Repository, then System, then Global. The context is clearly delimited as
untrusted advisory material and cannot prove relationships or citations.

## 9. Scheduling, Failure, Pause, and Resume

The Prepared Scan Manifest feeds a deterministic bounded queue:

- default concurrency: two Workers;
- configurable range: one through four;
- one shared concurrency limit across the two demonstration Repositories;
- Pass2 begins only after every Pass1 job is terminal;
- completion order does not change identity, grouping, or display ordering.

Each job has one normal attempt and bounded handling based on failure scope:

| Category | Behaviour |
| --- | --- |
| Provider transient | provider-level backoff, then pause Scan |
| Provider configuration | pause immediately for operator correction |
| Job transient | retry that job once, then count as file failure |
| Job permanent | count against partial-failure budget |
| Integrity/security | fail Scan |
| Cancellation | cancel without automatic resume |
| Unclassified | pause for operator inspection |

Provider-transient retry windows begin at 10 seconds, 30 seconds, one minute,
two minutes, and five minutes, with small jitter and `Retry-After` precedence.
New jobs stop while the circuit is probing. After five unsuccessful probes,
the Scan becomes `PausedProviderUnavailable`.

A Scan may finish `CompletedWithWarnings` only when every high-value seed
succeeds and ordinary failed files do not exceed
`min(5, floor(10% of dispatched files))`. Otherwise Pass1 fails and Pass2 does
not start. Pass2 excludes incomplete groups and reports them; it never
synthesizes around missing required Documents.

### 9.1 Durable Job Recovery

An additive `ScanJobExecution` record persists deterministic Job ID, Scan
Execution, File Code Unit, manifest ordinal, state, attempt count,
next-attempt time, bounded failure category/code, lease expiry, and committed
Document Version ID.

Result commit and job completion are atomic. On restart or manual resume,
Vulgata revalidates Repository access, Profiled Revision, file hashes, prompt
and schema identity; preserves committed Documents; returns expired Running
leases to Pending; skips atomically committed jobs; probes the configured
provider; and resumes the remaining manifest.

The August release provides an explicit Chinese `Resume scan` action. Automatic
background recovery is deferred. A lost response may cause an LLM job to run
again, but deterministic Job identity and atomic commit prevent duplicate
Document Versions.

## 10. System-Level Two-Repository Coordination

A lightweight persisted operational record owns the bounded System-level
execution. Its final ubiquitous-language name must be added to
`docs/UBIQUITOUS_LANGUAGE.md` during specification.

It records System, selected Repositories, bound Scan Run/Scan Execution IDs,
Profiled Revisions, current phase, pause/failure reason, and timestamps. It
does not duplicate per-file state.

The release proves exactly two Repositories:

```text
freeze revisions and complete PreScan
-> shared Pass1 queue
-> deterministic Cross-Repository Resolution
-> endpoint-centered flow grouping
-> Pass2
-> retrieval projections
-> Completed or CompletedWithWarnings
```

PreScan may prepare Repositories sequentially to bound CodeGraph/JDT LS
resources. A provider outage pauses shared dispatch. A Repository integrity
failure fails the combined Cross-Repository outcome, while already committed
valid Documents remain available. Every phase is idempotent on resume.

The Preparing coordination and its two bound new-path Scan Executions are
created only by the bounded production initiation boundary in Section 4.0 or
by explicit test fixtures. General Scan initiation, standalone profiling, and
arbitrary N-Repository coordination remain deferred.

## 11. Cross-Repository Resolution

Resolution is catalog-driven and deterministic, using the key:

```text
(protocol, normalized endpoint identifier)
```

A unique current authorized provider creates a `REFERENCES` Edge. No provider,
multiple providers, conflicting evidence, stale evidence, or unsupported kind
creates or retains an `Uncertainty`. Custom RPC is mandatory for the release;
exact HTTP is included only where normalization remains safe. MQ, file/storage,
page navigation, fuzzy matching, and indirect object flow are deferred.

The existing prepared profile becomes a regression oracle, not the production
resolver configuration. Pass2 cannot invent or change a provider decision.

## 12. Pass 2 Flow Grouping and Tools

Pass2 groups are endpoint-centered. The deterministic key contains System,
protocol, normalized endpoint, consumer entry, and provider Repository.

A group may contain:

- the consumer Code Logic Document;
- the exact Cross-Repository Edge;
- the provider XML Document;
- the provider implementation Document; and
- at most one hop of validated same-Repository references.

Initial bounds are two Repositories, eight Code Logic Document Versions,
16 evidence steps, ten groups, and one relationship hop. Every member must be
current, anchored, authorized, and bound to the relevant Profiled Revision.

Pass2 always receives typed access to current Code Logic Document sections and
Source Anchor-backed source. The shared `retrieve_code_logic` service supports
search, sections, Source Anchors, and related Documents within the authorized
System scope. Initial bounds are eight calls, ten results, four additional
Documents, four sections per Document, and 64 KiB.

Any additional Document actually used is revalidated and added to
`GENERATED_FROM`. Retrieval cannot change the deterministic endpoint/provider
Edge. Direct Pass2 JDT LS and CodeGraph verification is optional and is the
first tool capability cut if unstable.

## 13. Non-Vector LLM-Wiki and Multi-Turn Chat

The August release does not implement embeddings, a vector database, a general
agentic RAG loop, or autonomous multi-hop retrieval.

### 13.1 Section Retrieval

The server extracts query symbols, endpoints, Repository names, ASCII terms,
CJK bigrams, and mode-specific business/technical terms. It scores current
authorized `IndexEntry` and `DocumentSection` projections deterministically.
Exact symbol/path/title/heading matches outrank prose content matches.

Retrieval selects at most five Documents and approximately 12 primary sections,
with at most four sections per Document. It takes the strongest section per
Document before strong extras. If no useful match exists, it falls back to
Business Logic summaries and context terms, then returns insufficient evidence
instead of fabricating an answer.

### 13.2 Relationship Expansion

Only persisted current `REFERENCES`, `GENERATED_FROM`, exact Cross-Repository
Edges, and Flow Groups expand evidence. The top two or three Documents may
expand one hop, at most two related Documents per seed and five Documents
overall. Business mode prefers Business Logic Documents and follows
`GENERATED_FROM` for grounding. IT mode begins with Code Logic Documents,
Source Anchors, and `REFERENCES`.

Expansion is capped at 32 relationships and 12 flow steps, rejects cycles, and
never uses raw CodeGraph proximity or LLM authorization decisions.

### 13.3 Bounded Evidence Packet

The server creates a closed JSON packet with opaque evidence handles. It packs
high-ranked sections, relationship-required support, flow steps, supporting
Code Logic sections, Source Anchors, bounded context, and bounded conversation
state. Low-ranked whole sections are dropped when the size cap is reached;
supporting evidence is never partially truncated.

Existing evidence cardinalities remain: one to five Documents, one to 20
sections, one to 32 Source Anchors, at most 32 relationships, and at most 12
flow steps. Proposed byte bounds are 64 KiB evidence, 4 KiB conversation state,
and 4-8 KiB user context. A canonical SHA-256 fingerprint binds the packet.

The LLM authors a grounded synthesis using only packet handles. Vulgata accepts
the answer only when every material technical claim cites valid packet
evidence. One syntax repair is allowed only when time remains; semantic
validation failure rejects the whole answer.

### 13.4 Multi-Turn Conversation

Conversation state contains the last two user questions, last two validated
answers, previously cited handles, selected scope, and mode. A follow-up gives
previously cited current Documents a small retrieval boost, but Vulgata
re-fetches and revalidates every underlying Document and Source Anchor. A
previous answer is context, never evidence.

## 14. Verification and Release Boundary

### Tier 1: every implementation checkpoint

No network LLM: unit/focused integration tests, adapter doubles, contract and
projection validation, ordering, failure classification, pause/resume,
authorization/currentness, retrieval, expansion, packet, and citation tests.
No failing test may be committed.

### Combined host integration and real-LLM release evidence

- host-run Vulgata and analysis tools with a clean PostgreSQL database;
- PostgreSQL 17 as the only required Docker service;
- two real sample Repositories;
- real CodeGraph, JDT LS, and configured LLM;
- at least 50 and at most 150 automatically selected File Code Units;
- every high-value seed succeeds and ordinary failures stay within budget;
- at least one exact Cross-Repository `REFERENCES` Edge;
- at least one honest `Uncertainty`;
- at least three Business Logic Documents, including one Cross-Repository flow;
- twelve representative Chat questions, at least 80% useful, 100% accepted technical claims
  cited, zero unauthorized evidence, and each answer under 30 seconds;
- Vulgata application-process restart and successful access to the persisted
  wiki and Chat through the same PostgreSQL database; and
- a lightweight preflight recording the candidate commit, exact Repository
  Profiled Revisions, tool/provider versions, non-secret provider configuration
  identity, database ownership, commands, and exit results;
- straightforward database queries proving the minimum outputs; and
- a human-readable Markdown report, manual question-evaluation table, and
  manually captured redacted real screenshots.

This one run replaces a separate controlled-provider Tier 2 orchestration run
for the August 30 boundary. Live outage injection is not required during the
release run. Track C does not add an ECDSA attestation sink, recompute the
accepted Track B raw/SSE/provider-chunk/decoded/final counters, require a fixed
ten-check release envelope, construct immutable supersession/closure graphs,
hash every evidence cross-link, automate screenshot candidate binding, or add
a release-summary product feature. Those proof-automation capabilities are
post-competition backlog. Existing Track B streaming and security tests remain
mandatory Tier 1 evidence.

Targets on the documented demonstration machine are CodeGraph under 30 seconds
per Repository, JDT LS readiness under 60 seconds, PreScan under two minutes
per Repository, bounded two-Repository scan under 60 minutes, and Chat under
30 seconds. These are release targets, not general scalability claims.

## 15. Deadline Cut Order

Cut in this order if schedule pressure returns:

1. direct Pass2 JDT LS access;
2. direct Pass2 CodeGraph access;
3. automatic background recovery while retaining manual durable resume;
4. support beyond two Repositories;
5. protocols beyond Custom RPC and safe exact HTTP;
6. advanced graph visualization and nonessential UI animation.

Do not cut revision/hash binding, Source Anchors, citation validation, real
CodeGraph/JDT-LS Java PreScan, real Pass1/Pass2 generation, deterministic
Cross-Repository Resolution, authorization, durable job recovery,
clean isolated database verification, mandatory provider streaming, or one real
host-run two-Repository run.

## 16. Explicit Deferrals

- CodeGraphContext production integration and qualification;
- method-level Code Unit scheduling and general supersteps;
- Worker cross-verification/refinement waves and HITL disputed-edge workflow;
- dynamic LLM PreScan questions and approval lifecycle;
- arbitrary Communication Pattern Catalog plugins;
- XML LSP;
- MQ, file/storage, page navigation, fuzzy, and indirect object-flow resolution;
- general graph-first clustering and unrestricted path tracing;
- database tools and source access outside bounded authorized contracts;
- embeddings, vector search, semantic reranking, cross-encoders, arbitrary
  multi-hop RAG, autonomous Chat tool loops, long conversation memory, and
  Chat uploads;
- general 10,000-file performance acceptance;
- live graph/dashboard completeness and automatic background Scan recovery.
- Track C cryptographic attestation and sealed raw-adapter admission;
- exhaustive Track C transport-counter recomputation and fixed ten-check
  release-envelope semantics;
- immutable run supersession/closure graphs and exhaustive artifact cross-linking;
- a duplicate controlled-provider release orchestration run and live outage injection;
- automated candidate-bound screenshot validation and a release-summary product feature.

## 17. Planned-Change Authorization

This design triggers all three repository authorization categories:

1. it adds/materially changes Worker Agent tools (`navigate_code` and
   `retrieve_code_logic`);
2. it materially changes parsing, File Code Unit creation, filtering,
   ordering, scheduling, retry, grouping, and dispatch; and
3. it temporarily weakens or defers portions of PRD FR-4.1 through FR-4.5,
   FR-4.9, FR-4.12, FR-5.8, FR-6.1/6.2, FR-11.6, and NFR-1.1 while strengthening
   NFR-3.1 through NFR-3.3 recovery behavior.

After an independent plan review accepts the implementation plan and before
task generation, the orchestrator must pause and request explicit user
authorization citing that plan, this design, and the affected PRD clauses.
