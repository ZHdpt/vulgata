# 03 — Document Graph Design: Edge Types & Dynamic References

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Review pending — extracting from original plan; questions raised below.

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
