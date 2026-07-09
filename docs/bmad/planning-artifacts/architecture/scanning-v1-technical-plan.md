# Vulgata V1 Scanning Technical Plan

**Date:** 2026-07-09
**Status:** Technical planning handoff for August 15 V1 implementation
**Source sections:** `docs/scanning_plan_july/00-cross-reference.md` through `08-context-retrieval.md`

This document merges the July scanning-plan sections into one implementation-oriented technical plan. It supersedes older June scanning-process details where they conflict with the July decisions.

## 1. V1 Goal

Vulgata scans one Repository at a time, extracts Code Logic Documents from source Code Units, synthesizes Business Logic Documents from deterministic flow groups, resolves Cross-Repository references when possible, and exposes database-backed retrieval for Business Mode and IT Mode chat.

The V1 deadline target is buildable by August 15:

- deterministic orchestration, not LLM-led scheduling
- PostgreSQL-first document and graph storage
- CodeGraph-backed source structure
- LLM Worker Agents for bounded analysis and synthesis only
- automatic but authorization-aware Cross-Repository retrieval
- explicit uncertainty and stale-resolution handling

## 2. V1 Scope

Included:

- Pre-Scan Discovery and Review with sampled LLM repository profiling
- CodeGraph indexing and Code Unit extraction
- Pass 1 Code Logic Document generation per Code Unit
- Pass 1 bounded refinement using deterministic predicates
- dispatch-site, endpoint, uncertainty, and edge emission
- Pass 2 path-aware flow tracing and deterministic flow-group clustering
- Business Logic Document generation per flow group
- PostgreSQL `content_jsonb`, `content_markdown`, `document_sections`, graph tables, and flow-trace tables
- database-backed Chat Agent retrieval through typed tools
- `resolution_outdated_at` or equivalent stale notices when Cross-Repository Resolution changes BL generation assumptions
- simple V1 concurrency: one Repository scan at a time, up to five workers per scan

Deferred:

- advanced multi-repo scan overlap
- full tokenizer or embedding-based Chinese semantic search
- elaborate cross-verification review workflows
- deep incremental re-scan refinement replay outside the affected set
- runtime trace ingestion
- event sourcing

## 3. Canonical Architecture

### Scan Coordinator

The Scan Coordinator is deterministic application code. It owns:

- scan queue
- repository locks
- git refresh
- global scan concurrency
- triggering the per-Repository workflow
- triggering Cross-Repository Resolution after scan finalization

### Repo Workflow Orchestrator

The Orchestrator is a deterministic per-Repository workflow service, not an LLM agent. It owns:

- scan state machine
- pre-scan handoff
- job creation
- superstep grouping
- Worker Agent leases
- retry and failure policy
- material-enrichment predicates
- Pass 2 trace planning and flow-group clustering
- post-scan index rebuilds

### Worker Agents

Worker Agents are LLM agents. They do bounded analysis only:

- Pass 1 worker: analyzes one Code Unit and emits a Code Logic Document plus structured graph facts
- Pass 1 refinement worker: updates one Document Version using one satisfied dependency
- Pass 2 worker: explains one prepared flow group and emits one Business Logic Document
- Chat Agent: retrieves through typed tools and synthesizes answers

Worker prompts live under `src/dotnet/Vulgata.Agents/Prompts/` and should be embedded resources. Prompts are not hardcoded in orchestration code.

## 4. Workflow

Recommended scan states:

1. `created`
2. `pre_scan_running`
3. `pre_scan_review_pending`
4. `pre_scan_approved`
5. `pre_scan_failed`
6. `pass1_planning`
7. `pass1_running`
8. `pass1_partial_failed`
9. `pass1_completed`
10. `pass2_planning`
11. `pass2_running`
12. `pass2_partial_failed`
13. `post_scan_running`
14. `cross_repo_resolution_running`
15. `completed`
16. `failed`
17. `cancelled`

## 5. Pre-Scan Discovery and Review

Pre-scan uses one bounded LLM pass plus CodeGraph signals. It is sampled, not exhaustive.

Pre-scan outputs:

- repository profile
- framework and language hypotheses
- scan-scope buckets
- hotspot files/directories
- likely communication kinds
- reviewable plain-text resource files
- unsupported binary resource notices

Scope buckets:

- must scan deeply
- scan lightly / context only
- reviewable resource files
- exclude or low priority

V1 auto-approval rule:

- auto-approve when pre-scan identifies usable scope buckets and framework/communication hypotheses
- do not block on ordinary low-confidence hints
- require human review only for blocking ambiguity that would skip important source, exclude large areas, fail framework detection, or require unsupported resource interpretation
- if review exceeds the demo-time SLA, continue with conservative scope and record a warning

## 6. Pass 1 Code Logic Generation

Pass 1 creates Code Logic Documents from Code Units.

Each job packet includes:

- `code_unit_id`
- exact source slice
- file path and symbol name
- neighboring symbols
- CodeGraph callers/callees/imports/implementations
- relevant resource files
- pre-scan hints
- related outputs for lightweight cross-verification

Allowed graph outputs:

- exact `REFERENCES` edge when target is locally justified
- bounded candidate-set `REFERENCES` edges when the source unit itself gives a credible bounded set
- `dispatch_site` when invocation is caller-bound or environment-bound
- `uncertainty` when evidence is too weak even for a defensible dispatch site

Pass 1 must also emit:

- Source Anchoring payloads
- entry-point tags where applicable, including:
   - `entry_point_kind`
   - `entry_point_label`
   - protocol or framework hints
   - route, topic, schedule, command, component, or event metadata where applicable
   - source confidence
- `verification_notes`
- `disagreements`
- deferred enrichment dependencies only when a later unit may materially improve this Document

V1 cross-verification is lightweight:

- superstep grouping should prefer direct caller/callee, same file/module, same endpoint surface, and shared resource/config references so related outputs are likely available
- workers use related outputs to populate `verification_notes` and `disagreements`
- disagreements become scan warnings or low-confidence notes unless they block a stable edge, source anchor, or dispatch-site decision

## 7. Bounded Refinement

Pass 1 may run a bounded refinement wave. The Orchestrator schedules refinement only when a prior job declared a dependency and a deterministic predicate matches.

Material-enrichment predicates:

- uncertainty can become a dispatch site, candidate set, or exact `REFERENCES` edge
- dispatch site can gain stronger binding hints or bounded candidates
- endpoint, resource, or configuration artifact named by the earlier job becomes available
- direct callee/provider/resource relationship marked important by the earlier job becomes available

No LLM decides whether enrichment is material. If no predicate matches, the dependency is satisfied without refinement.

V1 limits:

- one refinement per Code Unit per scan unless explicitly configured lower or higher
- no recursive refinement chains outside policy
- unresolved cases remain explicit uncertainties

## 8. Pass 2 Flow Planning

Pass 2 is graph-first, not file-first.

The Orchestrator:

1. starts trace sets from Pass 1 entry points
2. traverses `REFERENCES`, candidate groups, dispatch sites, endpoint records, and uncertainties with `(doc_id, path_state)`
3. persists any trace used for BL generation
4. clusters traces into flow groups
5. dispatches one Pass 2 Worker Agent per flow group

Trace state includes:

- `trace_id`
- entry point Document Version
- current Document Version
- visited Documents
- active conditions
- candidate selections
- unresolved candidate groups
- dispatch-site bindings
- unresolved dispatch sites
- endpoint bindings
- confidence floor
- termination reason

Trace expansion rules:

- allow revisiting a Document only when the path state materially differs
- cap recursion depth and repeated-state count
- summarize repeated loops as repeated behavior instead of expanding forever

Examples of repeated behavior to summarize rather than expand forever:

- retry loops
- polling loops
- state-machine transitions
- recursive tree traversal

Recommended termination boundaries:

- response returned to caller
- durable state change completed
- database write or transaction boundary
- message published to another async flow
- external communication handoff
- cross-repo endpoint handoff
- page navigation handoff
- known framework lifecycle boundary
- max traversal budget reached
- unresolved dispatch site that blocks further traversal

## 9. Flow-Group Clustering

V1 clustering is deterministic.

Algorithm:

1. build one initial cluster per entry-point trace set
2. remove utility-only CL Documents from overlap scoring
3. compute pairwise score:
   - `+4` same normalized endpoint/topic/route/action family
   - `+4` same terminal business effect, durable write, message, or provider endpoint
   - `+3` non-utility CL overlap >= 50%
   - `+2` non-utility CL overlap between 25% and 49%
   - `+2` same aggregate/workflow/business-event identifier
   - `+1` strong shared domain terms
   - `-4` different terminal effects
   - `-3` overlap only utility/helper Documents
4. merge pairs with score `>= 5`
5. recompute after each merge until no pair qualifies
6. do not exceed configured trace-count or token-estimate caps

V1 favors under-merging over bloated Business Logic Documents.

For V1, **utility-only** means validation, mapping, logging, serialization, simple DTO access, or infrastructure helpers with no durable business effect.

If a merge would exceed the cap, keep separate groups and record the shared sub-flow as repeated support instead of forcing one oversized Business Logic Document.

Shared sub-flow BL Documents are optional in V1 and should only be created when all of these are true:

- the shared segment has a meaningful business name
- the segment is used by multiple flow groups
- the segment is large enough that repeating it would make top-level BL Documents confusing
- the segment has a stable boundary in the graph

## 10. Pass 2 Business Logic Generation

The Pass 2 Worker receives a prepared flow-group packet:

- flow group ID
- ordered traces
- trace steps
- trace termination reasons and loop summaries
- candidate selections and unresolved candidate groups
- dispatch bindings and unresolved dispatch sites
- endpoint bindings and unresolved endpoint hints
- relevant Code Logic Documents in traversal order
- relevant resource/config Documents
- Cross-Repository uncertainties

The Worker explains the prepared graph slice. It must not rediscover the graph.

Pass 2 may create:

- one Business Logic Document for the flow group
- `GENERATED_FROM` edges to Code Logic Document Versions actually used
- semantic reference requests for BL-to-BL relationships

Pass 2 must not create new base Code Logic `REFERENCES` edges from narrative interpretation.

## 11. Cross-Repository Communication

Provider-side surfaces are `communication_endpoints`.

Consumer-side abstract invocations are `dispatch_sites`.

Pass 1 outcomes for cross-repo behavior:

- exact endpoint reference
- bounded candidate endpoint set
- dispatch site with endpoint hints
- uncertainty

`page_navigation` follows the same endpoint/dispatch/uncertainty model but requires stronger ownership evidence:

- normalized route
- app/base path
- host or tenant context
- deployment topology
- Service Topology evidence when available

Identical route strings across apps default to uncertainty or candidate endpoints.

## 12. Document Storage

PostgreSQL is canonical.

Each generated Document Version stores:

- relational identity and graph facts
- `content_jsonb` canonical structured payload
- `content_markdown` deterministic render cache
- optional export path

Filesystem Markdown is export-only.

Generated Document Versions are immutable. Re-scan or refinement creates a new version and links the previous version through the Version Chain.

Logical Document identity is separate from immutable Document Version identity.

## 13. Retrieval

The Chat Agent uses typed retrieval tools over database-backed indexes. It does not read `index.md` as the canonical retrieval source.

V1 tools:

- `search_documents`
- `get_document`
- `get_document_sections`
- `expand_related_documents`
- `get_uncertainty_summary`
- `get_endpoint_context`
- `get_flow_trace_context`
- `get_source_anchor_context`
- `get_scan_status`

V1 search signals:

- `index_entries`
- `document_sections`
- metadata filters
- exact symbols
- endpoint identifiers
- business-term aliases
- graph expansion
- trigram fallback for Chinese and mixed Chinese/English search

PostgreSQL built-in full-text search must not be treated as a complete Chinese tokenizer. Proper tokenizers, embeddings, or `pgvector` are upgrade paths.

## 14. Authorization

Retrieval is authorization-aware at every level:

- Repository authorization is the outer boundary
- Document authorization inherits from Repository unless later narrowed
- Edge authorization requires both endpoints to be authorized before target details are disclosed
- endpoint names are redacted when target Repository is unauthorized
- Uncertainty summaries must not leak restricted Repository, endpoint, route, or business behavior
- flow trace records are returned only with authorized or redacted details

## 15. Staleness

Business Logic Documents record generation-time resolution state.

When Cross-Repository Resolution later changes graph truth, V1 does not regenerate stale BL prose automatically. Instead, the affected BL Document Version receives `resolution_outdated_at` or an equivalent stale-notice row.

Retrieval must separate:

- generation-time BL prose
- query-time graph evidence

Answers should cite both when they differ.

## 16. Incremental Re-Scan

V1 incremental re-scan uses predictable regeneration:

1. git diff identifies changed files
2. CodeGraph reparses changed files
3. reverse index and source hashes identify affected Document Versions
4. affected Code Logic Documents are regenerated
5. direct declared refinements inside the affected set may run once, including one bounded refinement of other Documents that explicitly declared a dependency on a re-scanned Document inside that affected set
6. affected Business Logic Documents are marked stale and regenerated through normal Pass 2 flow planning
7. Cross-Repository stale notices are created where appropriate

V1 does not replay deep refinement waves outside the affected set.

## 17. Concurrency

V1 defaults:

- `MaxConcurrentScans = 1`
- `MaxWorkersPerScan = 5`
- no overlap between Pass 2 for one Repository and Pass 1 for another Repository
- Cross-Repository Resolution runs after each Repository scan completes

This is deliberately simple for deadline reliability.

## 18. LLM Usage Budgeting

The workflow estimates cost before dispatch and records actual usage after calls.

Track:

- agent type
- call count
- input tokens
- output tokens
- configurable estimated cost units

Planning formulas:

- pre-scan calls = repositories scanned
- Pass 1 worker calls ~= Code Units in scope
- Pass 1 refinement calls <= Code Units with satisfied material dependencies
- Pass 2 worker calls = flow groups
- chat calls per question = retrieval tool rounds + synthesis call

Provider prices remain configuration.

## 19. Implementation Order

Recommended order:

1. domain schema and EF configuration for Documents, graph facts, dispatch sites, flow traces, and sections
2. scan state machine and job leases
3. CodeGraph indexing and source anchoring
4. pre-scan discovery and scope buckets
5. Pass 1 Worker result contract and persistence
6. bounded refinement
7. endpoint and Cross-Repository Resolution
8. Pass 2 trace planning and flow-group clustering
9. Pass 2 BL generation
10. retrieval tools and Chat Agent
11. graph/dashboard integration

