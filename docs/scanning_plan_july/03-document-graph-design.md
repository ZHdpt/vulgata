# 03 — Document Graph Design: Edge Types & Dynamic References

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Section 03 completed - dispatch-site graph model agreed and ready to use as input for sections 06 and 08.

---

## TODO — Open Questions from July Review

> These are concerns raised that need to be addressed before finalizing this section.

1. **REFERENCES edges assume static call graphs.** In reality, references can be:
   - **Dynamic dispatch (virtual methods / interfaces):** `processor.Process()` could call any of 5 implementations depending on runtime type. The worker agent reading the source sees only the interface call, not the concrete implementation.
   - **Conditional invocations:** `if (config.useNewService) { newService.call() } else { oldService.call() }` — both paths exist statically, but only one runs at a time.
   - **Reflection-based calls:** `Class.forName(name).getMethod("handle").invoke(obj)` — no static reference to trace.
   - **Callback registrations / event handlers:** `eventBus.subscribe("order.created", this::handleOrder)` — the call site is the event bus, not the handler.
   - **Dependency injection:** Constructor injection means the concrete type is resolved at runtime from a DI container, invisible to static analysis.

   **Question:** How should the document graph represent dynamic references? Options:
   - Create REFERENCES edges to ALL possible targets (with lower confidence)?
   - Create a special `DYNAMIC_REFERENCE` edge type?
   - Use `uncertainties` with `type = 'ambiguous_logic'`?
   - Defer to HITL?

2. **RPC behavior compounds the problem.** For cross-repo RPC calls:
   - The client code may call `riskClient.evaluate(request)` where `riskClient` is a generated proxy.
   - The actual endpoint (`RiskService.evaluate`) may be discoverable from annotations or configuration, but the mapping from proxy method to endpoint is framework-specific.
   - **How does the agent connect the proxy call to the actual endpoint identifier?**

3. **GENERATED_FROM direction and semantics.** Current plan: `source = BL Doc, target = CL Doc, edge_type = GENERATED_FROM`. This means "BL Doc was generated from CL Doc." But:
   - What about BL Docs that synthesize from multiple CL Docs? Multiple GENERATED_FROM edges.
   - What about BL Docs that reference other BL Docs? Those are REFERENCES edges.
   - What about CL Docs that are "updated by" a BL Doc discovery (e.g., the BL Doc identifies that a CL Doc actually participates in a flow the CL Doc's own worker didn't recognize)? Should there be a reverse provenance edge?

4. **Edge confidence is currently a scalar.** But a `REFERENCES` edge could be uncertain for different reasons:
   - The target might be wrong (ambiguous dispatch).
   - The relationship might be conditional (only in certain configs).
   - The relationship might be indirect (through middleware/event bus).
   Should confidence decompose into multiple dimensions?

5. **New edge types to consider:**
   - `IMPLEMENTS` — a CL Doc implements a BL Doc's described behavior (reverse of GENERATED_FROM, but more intentional)
   - `DEPENDS_ON_DATA` — a document's logic depends on a database schema or data format
   - `CONDITIONALLY_REFERENCES` — references that only hold under certain conditions
   - `PUBLISHES` / `SUBSCRIBES` — for event-driven architectures
   - `OVERRIDES` — for virtual method overrides (subclass doc overrides parent class doc behavior)

---

## Extracted Content from Original Plan

### `edges` — THE GRAPH TABLE

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `source_document_id` | FK → documents | |
| `target_document_id` | FK → documents | |
| `edge_type` | ENUM | **REFERENCES** (doc cites/calls/links to another doc), **GENERATED_FROM** (Business Logic Doc was built from a Code Logic Doc — the two-pass provenance chain) |
| `protocol` | ENUM | rpc, http, mq, file, page_navigation |
| `endpoint_identifier` | TEXT | System-qualified: `system_slug:endpoint` |
| `endpoint_identifier_normalized` | TEXT | Normalized form for matching |
| `provider_repo_id` | FK → repositories | Repository that exposes the interface |
| `consumer_repo_id` | FK → repositories | Repository that calls the interface |
| `confidence` | REAL | 0.0–1.0 |
| `metadata_json` | TEXT | Edge-specific metadata |
| `scan_id` | FK → scans | |
| `created_at` | TIMESTAMPTZ | |

### Design note — what lives where and why:

| Relationship | Stored in | Reason |
|-------------|-----------|--------|
| Doc → Doc dependency | `edges` (REFERENCES) | The graph. Drives impact analysis. |
| BL Doc → CL Doc provenance | `edges` (GENERATED_FROM) | Doc→doc. Drives two-pass re-scan. |
| Doc → Code Unit | `document_sources` | M:N join with role semantics. |
| Tree hierarchy | `documents.parent_document_id` | Single parent, no edge type needed. |

### Why CONTAINS is NOT an edge type
`documents.parent_document_id` already defines the tree. Storing the same hierarchy again as CONTAINS edges doubles the write surface and guarantees drift. The tree is a column, not an edge type.

### Why CROSS_REPO is NOT an edge_type
Cross-repo-ness is a property of the endpoints, not the edge. Any edge where `source.repo_id != target.repo_id` IS cross-repo — discovered at query time.

### Why bidirectional edges are NOT stored
Reverse traversal is achieved at query time by swapping source/target in the JOIN.

### Why `edges.target_document_id` is NOT NULL
Pending cross-repo references are stored as `uncertainties` rows, not as edges with NULL targets.

### Cross-verification disputed edge handling
If Worker A inserted a `REFERENCES` edge before Worker B disputed it, the edge is kept with `confidence` lowered to `0.5` and the uncertainty ID recorded in `metadata_json`.

---

## Notes for Revision

- The current edge model assumes a relatively static, analyzable call graph. For dynamic languages (Python, Ruby, JavaScript) or DI-heavy frameworks (Spring, ASP.NET Core), static analysis is fundamentally limited. The agent might need runtime traces or configuration analysis to disambiguate.
- The `metadata_json` column on edges could store structured disambiguation data: `{"dispatch_type": "virtual", "possible_targets": ["doc-1", "doc-2"], "resolution": "static_analysis_best_guess"}`.
- For RPC: the disconnect between client proxy and server endpoint is a well-known problem. Service meshes and API gateways solve this at the infrastructure level. Should Vulgata integrate with OpenAPI/Swagger specs or gRPC proto files to bridge this gap?

---

## Proposed Direction for Section 03

This section proposes the working edge-model design to use with the decisions already made in sections 01, 02, and 05.

### 1. Core principles for the document graph

The document graph should stay a **document dependency graph**, not become a full program-semantics graph.

That means:

- the graph should remain centered on **document-to-document** relationships
- structural code facts should come from CodeGraph and code-unit metadata
- the edge model should stay small enough for impact analysis and retrieval to remain simple
- ambiguity should be represented explicitly, not hidden behind overconfident edges

The main design rule for section 03 is therefore:

**Keep a small number of edge types in V1, and carry dynamic/conditional semantics in structured edge metadata plus uncertainties.**

### 2. Dynamic references should not introduce a generic `DYNAMIC_REFERENCE` edge type

The recommended direction is:

- keep `REFERENCES` as the main dependency edge type
- do **not** add a generic `DYNAMIC_REFERENCE` edge type in V1
- represent dynamic behavior using:
   - `REFERENCES` edges when there is a bounded, evidence-backed target or target set
   - `uncertainties` rows when the target cannot be bounded well enough
   - `metadata_json` on the edge to describe why the reference is dynamic

Why not a separate `DYNAMIC_REFERENCE` edge type?

- it mixes certainty semantics with relationship semantics
- it complicates traversal and impact analysis
- most queries still want to know "what might this depend on?", which is still a reference question

Instead, a `REFERENCES` edge should be enriched with metadata such as:

- `reference_kind`: `direct_call`, `virtual_dispatch`, `conditional_call`, `event_publish`, `event_subscribe`, `rpc_proxy`, `page_navigation`, `data_dependency`
- `resolution_kind`: `static_exact`, `candidate_set`, `config_resolved`, `endpoint_resolved`, `heuristic`, `human_confirmed`
- `condition_text`: optional condition expression or config dependency
- `candidate_group_id`: optional identifier for mutually exclusive alternatives
- `requires_runtime_confirmation`: boolean

### 3. Rules for creating edges under dynamic behavior

The worker should use these rules.

#### Case A — Exact target known

Create one `REFERENCES` edge when:

- CodeGraph resolves the target exactly
- framework configuration resolves the target exactly
- endpoint mapping resolves the provider exactly

Examples:

- direct method call to a concrete symbol
- HTTP client call resolved to one controller or provider endpoint
- queue subscriber resolved to one handler document

#### Case B — Small bounded candidate set

Create multiple low-confidence `REFERENCES` edges only when the candidate set is:

- finite
- small
- structurally justified

Examples:

- interface call with three known implementations
- configuration switch with two known handlers

Each edge should carry:

- lower confidence
- same `candidate_group_id`
- `resolution_kind = candidate_set`
- dispatch or condition metadata

This allows the graph to show plausible reachability without pretending the runtime target is certain.

#### Case C — Unbounded or weakly justified target

Do **not** create speculative edges when:

- reflection is unconstrained
- plugin loading is open-ended
- runtime registry resolution is opaque
- target space is too broad to bound credibly

In these cases, create an `uncertainties` row instead.

This is the preferred approach for:

- unrestricted reflection
- dynamic import by string with no reliable mapping
- unsupported framework magic
- binary-resource-driven dispatch

### 4. RPC and generated client behavior should resolve through endpoint entities, not ad hoc guesswork

For RPC-style or generated-client references, the worker should not directly invent target document IDs from code text alone.

The proposed direction is:

- worker identifies a candidate communication action
- worker resolves it against a canonical endpoint entity
- graph edge is created only after endpoint-level evidence exists

In practice, that means section 03 depends on section 04 introducing a canonical communication endpoint model.

Recommended flow:

1. worker sees a client/proxy call such as `riskClient.evaluate(request)`
2. worker uses framework/config/spec evidence to infer the canonical endpoint identifier
3. if one endpoint entity matches, create `REFERENCES` with:
    - `protocol`
    - endpoint linkage metadata
    - normal confidence based on evidence strength
4. if the endpoint is only partially known, emit uncertainty or candidate endpoint association

This means RPC resolution should be **endpoint-first**, not **string-guess-first**.

### 5. `GENERATED_FROM` should remain one-way and many-to-many

The recommended semantics are:

- `source_document_id = BL Doc`
- `target_document_id = CL Doc`
- `edge_type = GENERATED_FROM`

This means:

- a BL document may have many `GENERATED_FROM` edges
- a CL document may feed many BL documents
- BL-to-BL semantic dependencies remain `REFERENCES`

The design should **not** add a reverse provenance edge in V1.

If Pass 2 discovers additional semantic participation for a CL doc, that should be recorded as:

- CL document metadata updates
- flow-participation tags
- or future auxiliary tables

but not as a reverse provenance edge.

Why:

- provenance is directional and should stay simple
- reverse provenance would duplicate meaning and complicate impact traversal
- the graph already supports reverse lookup by querying incoming `GENERATED_FROM` edges

### 6. Confidence should stay scalar in the schema, with richer evidence in metadata

The recommendation is to **keep `confidence` as a scalar** in V1.

Do not split it into many database columns yet.

Why:

- most ranking, filtering, and rendering decisions only need one sortable score
- multi-column certainty models add schema complexity too early
- the nuanced reasons for uncertainty can live in structured metadata

Recommended pattern:

- `confidence` remains `0.0–1.0`
- `metadata_json` stores the reason model

Example metadata fields:

- `evidence_kind`
- `dispatch_kind`
- `condition_text`
- `candidate_count`
- `endpoint_resolution_source`
- `human_confirmed`
- `runtime_unknown`

This gives the UI and future analysis enough detail without forcing a hard schema choice too early.

### 7. Recommended confidence bands

The following bands are a practical V1 convention.

- `0.95–1.00` — exact structural or endpoint-confirmed reference
- `0.75–0.94` — strong config-backed or framework-backed inferred reference
- `0.50–0.74` — bounded candidate-set or disputed reference
- `0.25–0.49` — weak heuristic reference that should be visually marked as uncertain
- below `0.25` — usually prefer uncertainty over persisted edge

This keeps the graph from filling with low-value speculative edges.

### 8. V1 should keep the edge-type vocabulary minimal

The recommendation for V1 is to keep only these graph edge types:

1. `REFERENCES`
2. `GENERATED_FROM`

Do **not** add the following as top-level edge types in V1:

- `DYNAMIC_REFERENCE`
- `CONDITIONALLY_REFERENCES`
- `IMPLEMENTS`
- `OVERRIDES`
- `DEPENDS_ON_DATA`
- `PUBLISHES`
- `SUBSCRIBES`

These should instead be represented in one of three ways:

- as `REFERENCES` plus metadata
- as CodeGraph/code-unit structural facts outside the document graph
- as future specialized entities if retrieval and analysis later prove they are needed

Examples:

- event publish/subscribe: `REFERENCES` plus `protocol = mq` and `reference_kind`
- override relationships: CodeGraph or code-unit metadata, not a document-graph edge
- conditional behavior: `REFERENCES` plus `condition_text`
- data dependency: future schema-doc strategy, not a premature V1 edge type

### 9. Proposed metadata contract for `REFERENCES`

The `metadata_json` for `REFERENCES` edges should be treated as structured, not arbitrary prose.

Suggested fields:

- `reference_kind`
- `resolution_kind`
- `dispatch_kind`
- `condition_text`
- `candidate_group_id`
- `candidate_rank`
- `candidate_count`
- `endpoint_resolution_source`
- `human_confirmed`
- `notes`

This should be produced by workers through typed result contracts rather than freeform strings whenever possible.

### 10. Impact-analysis behavior under ambiguous references

Impact analysis should include low-confidence candidate-set references, but the UI should distinguish them from exact references.

Recommended behavior:

- traversal includes all persisted `REFERENCES` edges
- visualization and reports should surface confidence and candidate-group semantics
- dispatch sites are **not** traversed as ordinary downstream edges in the base graph
- dispatch sites may still contribute **advisory impact notices** when their binding hints match a changed provider, endpoint, contract, or implementation surface
- uncertainty-only cases are not traversed as edges because no target is yet known

This preserves recall while avoiding fake certainty.

### 10A. Generic dispatch-site model for caller-bound resolution

The earlier candidate-set example only works for **local bounded ambiguity** inside `B`, such as a visible condition expression.

It does **not** cover the more general case where `B` contains an abstract invocation and the actual callee is a property of the caller path.

Example:

```text
A  -> B
A' -> B
B invokes IX.Foo(x)
A  always passes implementation C for x
A' may pass D, or something more complex, or something unresolved
```

In this case, `B` by itself does not justify stable base-graph edges like:

- `B -> C`
- `B -> D`

because those are not universal properties of `B`. They are **binding-dependent** properties of a specific invocation path.

#### Recommended distinction

The model should distinguish three kinds of facts:

1. **local references** that can be justified from the current unit alone
2. **dispatch sites** where an abstract invocation exists but the target depends on caller or environment binding
3. **trace-level bindings** that resolve a dispatch site on a specific path such as `A -> B -> C`

#### Dispatch site concept

When `B` performs an abstract invocation that cannot be resolved from `B` alone, the system should record a **dispatch site**, not speculative base edges.

A dispatch site represents facts like:

- `B` invokes `Foo`
- the receiver is abstract, interface-based, injected, reflective, registry-based, or otherwise unresolved locally
- the receiver source may be:
   - method parameter
   - field or property
   - constructor-injected dependency
   - service locator lookup
   - reflection string
   - dynamic runtime object

Recommended analysis record shape:

- `dispatch_site_id`
- `source_document_id`
- `source_code_unit_id`
- `call_symbol`
- `receiver_kind`
- `receiver_type_hint`
- `resolution_scope` (`local`, `caller_bound`, `config_bound`, `runtime_open`)
- `confidence`
- `metadata_json`

For consistency with section 02's worker-tool contract, dispatch sites should be treated as a **persisted first-class entity in V1**.

That does not require a large or fully normalized subsystem, but it does require:

- stable `dispatch_site_id` values
- queryability by source document and source code unit
- storage of binding hints needed later by Pass 2 and cross-repo resolution

Conceptually and operationally, a dispatch site is different from a `REFERENCES` edge.

#### Generic recording rule

Use these rules.

##### Case 1 — local target known from `B`

Persist ordinary `REFERENCES` edges.

##### Case 2 — small bounded target set known from `B`

Persist candidate-set `REFERENCES` edges only if `B` itself provides a credible bounded set.

This still covers explicit local branching such as a visible `if/else` or a small configuration switch inside `B`.

##### Case 3 — caller-bound or environment-bound resolution

Do **not** persist `B -> C` or `B -> D` as base edges when those targets only emerge from caller-provided binding.

Instead:

- persist `A -> B` if known
- record a dispatch site on `B`
- let later trace analysis bind that dispatch site to `C`, `D`, or leave it unresolved

This is the generic solution for:

- interface parameter dispatch
- virtual dispatch driven by caller-chosen instances
- DI-bound resolution
- service-locator lookup
- reflection and dynamic invoke

### 10B. Two layers of truth

This produces a cleaner two-layer model.

#### Layer 1 — Base dependency graph plus abstract dispatch records

Stores:

- stable document-to-document references
- candidate-set references only when locally justified
- dispatch-site records for unresolved abstract invocations

This layer is used for:

- impact analysis
- retrieval expansion
- document browsing

#### Layer 2 — Trace-specific binding layer

Stores or computes:

- which candidate-set branch was selected on a path
- which dispatch site was bound on a path
- what caller or config evidence caused the binding
- where ambiguity remained unresolved

This layer is used for:

- Pass 2 flow extraction
- BL document synthesis
- explanation of actual or likely execution paths

### 10C. Practical graph shape for the generic case

For the caller-bound example, the base layer should look more like:

```text
A  -> B
A' -> B
B  -> [dispatch_site ds-b-001: IX.Foo(parameter x)]
```

and not necessarily like:

```text
B -> C
B -> D
```

unless `B` itself justifies that bounded candidate set.

Then a trace-level binding may record something like:

```json
{
   "trace_start": "A",
   "dispatch_bindings": {
      "ds-b-001": {
         "selected_target": "C",
         "binding_basis": "A passes implementation C as argument x"
      }
   }
}
```

and another trace may record:

```json
{
   "trace_start": "A'",
   "dispatch_bindings": {
      "ds-b-001": {
         "selected_target": null,
         "binding_basis": "caller path does not bound the concrete implementation strongly enough",
         "status": "unresolved"
      }
   }
}
```

### 10D. Why this is the more generic solution

This model handles all of these with one abstraction:

- explicit local branch
- interface parameter dispatch
- virtual dispatch
- DI-bound resolution
- service-locator lookup
- reflection and dynamic invoke

The rule becomes:

- if the callee can be justified from the current unit, record `REFERENCES`
- if the callee is only justified from caller or environment binding, record a **dispatch site** and resolve it at trace time

### 10E. Impact-analysis semantics for dispatch sites

Dispatch sites should participate in impact analysis as **abstract dependency markers**, not as hard traversable edges.

Recommended V1 rule:

1. **Hard transitive impact traversal** uses persisted `REFERENCES` and `GENERATED_FROM` edges.
2. **Dispatch sites do not automatically expand reachability** to downstream documents because no path-insensitive target is guaranteed.
3. **Dispatch sites may generate advisory impact notices** when a changed implementation, endpoint, or contract matches the dispatch site's stored hints.

This means a report may say:

- "Document B contains a dispatch site that may bind to changed implementation C under some caller/config paths"

without pretending that `B -> C` is a stable base-graph edge.

### 11. Practical answers to the original five questions

#### Q1 — Dynamic references

Answer:

- no generic `DYNAMIC_REFERENCE` edge type in V1
- use `REFERENCES` plus metadata for locally justified bounded dynamic cases
- use **dispatch sites** for caller-bound or environment-bound abstract invocations
- use `uncertainties` for unbounded or weakly justified cases that cannot even support a dispatch site

#### Q2 — RPC mapping

Answer:

- resolve through canonical communication endpoint entities
- do not guess target docs directly from proxy calls
- if the generated client or proxy target is caller-bound or environment-bound, record a dispatch site with endpoint/contract hints instead of forcing a base edge

#### Q3 — `GENERATED_FROM`

Answer:

- keep direction BL -> CL
- allow many-to-many
- do not add reverse provenance in V1

#### Q4 — Confidence model

Answer:

- keep one scalar confidence column
- attach richer reason/evidence metadata

#### Q5 — New edge types

Answer:

- keep the vocabulary minimal in V1
- carry most specialization in metadata or structural services

### 12. What section 03 now assumes from other sections

Section 03 now assumes:

- section 02 provides deterministic orchestration and typed worker result contracts
- section 05 provides CodeGraph-backed bounded structural expansion
- section 04 will introduce canonical communication endpoint handling

### 13. What section 03 hands to later sections

Section 03 hands these assumptions forward:

- section 04 can design endpoint entities around endpoint-first resolution plus dispatch-site-aware RPC binding
- section 06 can traverse `REFERENCES` with path-state tracking while binding candidate groups and dispatch sites at trace time
- section 08 can use a stable, minimal graph vocabulary for retrieval and explanation
