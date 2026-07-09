# 04 — Cross-Repo Communication Endpoints

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Section 04 completed — endpoint and dispatch-site direction agreed and ready to use as input for sections 03, 05, 06, and 08.

---

## Original Open Questions

> These were the questions used to drive review. They are kept for traceability; the resolved position is documented below.

1. **Each cross-repo communication should have its own identifier stored in the database.** The current plan stores endpoint identifiers in two places:
   - `edges.endpoint_identifier` — on resolved edges
   - `uncertainties.endpoint_identifier` — on unresolved cross-repo calls
   - `communication_patterns.endpoint_pattern` — regex/prefix patterns for matching

   **Problem:** There is no single "Cross-Repo Communication Endpoint" entity. An endpoint like `risk-service:RiskService.evaluate` is:
   - Defined as a pattern in `communication_patterns`
   - Referenced by value in `edges` and `uncertainties`
   - No canonical row says "this endpoint EXISTS and is PROVIDED BY repo X"

   **Proposal:** A new `communication_endpoints` table:
   | Column | Type | Notes |
   |--------|------|-------|
   | `id` | UUID PK | |
   | `identifier` | TEXT UNIQUE | Canonical: `repo_slug:endpoint` |
   | `identifier_normalized` | TEXT | For matching |
   | `provider_repo_id` | FK → repositories | Which repo PROVIDES this endpoint |
   | `protocol` | ENUM | rpc, http, mq, file, page_navigation |
   | `pattern_type` | ENUM | exact, prefix, regex — how to match incoming calls |
   | `pattern_value` | TEXT | The matching pattern |
   | `status` | ENUM | **discovered** (agent found it in code), **confirmed** (System Owner verified), **deprecated** |
   | `discovered_by_scan_id` | FK → scans | Which scan discovered it |
   | `source_code_unit_id` | FK → code_units | Which code unit provides it |
   | `created_at` | TIMESTAMPTZ | |
   | `updated_at` | TIMESTAMPTZ | |

   Then `edges.endpoint_identifier` and `uncertainties.endpoint_identifier` become FKs to `communication_endpoints` instead of free-text.

2. **Agents should be able to read code, recognize cross-repo communication behavior, and identify or add endpoints to the database.** Currently:
   - Pass 1 workers detect cross-repo calls and create `uncertainties` rows
   - The `communication_patterns` table is manually populated by System Owner
   - Resolution happens post-scan by matching endpoint strings

   **Proposed flow:**
   - During Pass 1, when a worker detects a cross-repo call, it attempts to **identify the endpoint** from code annotations, framework conventions, or configuration files.
   - If identified: the worker **creates or matches** a `communication_endpoints` row (provider side is unknown until that repo is scanned, but the consumer side is recorded).
   - If not identified: creates an uncertainty as before.
   - When the provider repo is scanned, its workers **discover endpoints they expose** and create/mark `communication_endpoints` rows with `provider_repo_id` set.
   - Resolution then matches consumer-side endpoints to provider-side endpoints — both stored in the same table.

3. **Endpoint discovery from code — what can agents look for?**
   - **HTTP/REST:** Route annotations (`@GetMapping`, `[HttpGet]`, `@app.route`), OpenAPI/Swagger specs, controller class names
   - **gRPC:** `.proto` files, generated service stubs, `@GrpcService` annotations
   - **Message queues:** Topic/queue names in `@KafkaListener`, `@RabbitListener`, queue configuration files
   - **RPC frameworks:** Thrift IDL, Dubbo XML config, Spring Cloud Feign interfaces
   - **GraphQL:** Schema files, `@Query`/`@Mutation` annotated methods

4. **Relationship to `communication_patterns`:** Does `communication_endpoints` replace or complement `communication_patterns`?
   - **Replace:** `communication_endpoints` is the source of truth. `communication_patterns` becomes a view or is removed.
   - **Complement:** `communication_patterns` remains as the System Owner's "intended" catalog; `communication_endpoints` is what agents "discovered." Reconciliation highlights mismatches.

5. **Normalization rules:** The current `normalization_rule` column on `communication_patterns` is a text description of how to normalize. With a dedicated endpoints table, normalization could be per-endpoint or per-protocol, and could be implemented as a deterministic function rather than described in prose.

---

## Extracted Content from Original Plan

### `communication_patterns` table

Per-system configuration of how that system communicates externally. Written by the System Owner.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `system_id` | FK → systems | |
| `pattern_type` | ENUM | rpc, http, mq, file, page_navigation |
| `endpoint_pattern` | TEXT | Regex or prefix pattern |
| `target_system_id` | FK → systems | Nullable |
| `normalization_rule` | TEXT | How to normalize endpoint identifiers |
| `is_active` | BOOL | |
| `created_at` | TIMESTAMPTZ | |
| `updated_at` | TIMESTAMPTZ | |

**How it's used:**
- **Pre-Scan:** Loaded to provide workers with detection patterns.
- **Pass 1 edge creation:** When a worker detects a call matching `endpoint_pattern`, it creates an uncertainty.
- **Cross-Repo Resolution:** `normalization_rule` is applied before matching.
- **Uncertainty promotion:** New patterns trigger re-evaluation of `unknown_target` uncertainties.

### Cross-Repo Resolution (from original plan)

After a repo's scan completes:
1. Query `uncertainties` with `status = 'unresolved'`, `sub_status = 'cross_repo'`, `target_repo_id = <completed_repo_id>`
2. Match on `(target_repo_id, endpoint_identifier_normalized)` composite key
3. If one match → create REFERENCES edge, mark resolved
4. If multiple matches → pick best, lower confidence
5. If no match → stays unresolved, retry on next scan

### Uncertainty State Transitions (abbreviated)

```
Agent discovers cross-repo call → unresolved/cross_repo
  → Target repo scanned + matched → resolved
  → Target repo never scanned → stays unresolved (retried)
  → Human marks as known unknown → wontfix

Agent cannot identify target → unresolved/unknown_target
  → New Communication Pattern added → promoted to cross_repo
  → HITL identifies target → resolved
  → HITL marks as known unknown → wontfix
```

---

## Notes for Revision

- The `communication_endpoints` table proposal makes cross-repo edges more structured. Instead of "endpoint_identifier is a string that might match another string," it becomes "endpoint_id is an FK that must match a provider's endpoint."
- This also helps the chat agent: when a user asks "what calls the RiskService?", the agent can query `communication_endpoints` for the canonical identifier, then find all edges referencing it.
- The `communication_patterns` table was designed for the System Owner to pre-configure patterns. If agents can auto-discover endpoints, the System Owner's role shifts from "define patterns" to "review and confirm discovered endpoints."

---

## Proposed Direction for Section 04

This section should now be interpreted together with the refined section-03 dispatch-site model.

### 1. Core distinction: endpoints are provider entities, dispatch sites are consumer-side abstract invocations

The design should distinguish two different things.

#### Communication endpoint

A persisted canonical provider-side interface such as:

- HTTP route
- RPC operation
- gRPC service method
- message topic or queue contract
- GraphQL operation surface

This is what another repo may ultimately call.

#### Dispatch site

A persisted consumer-side abstract invocation record created when a worker sees a cross-repo communication action but the concrete target depends on:

- caller path
- DI binding
- generated-client configuration
- environment configuration
- framework runtime behavior

So section 04 should not treat all cross-repo calls as immediate endpoint matches. Some of them are only endpoint-**hinted** dispatch sites at Pass 1 time.

### 2. Recommended V1 model

V1 should persist both:

1. **communication_endpoints** as canonical provider-side entities
2. **dispatch_sites** as canonical consumer-side abstract invocation records when local evidence is insufficient for a stable edge

This keeps section 04 aligned with section 03 and section 02.

Deadline decision:

- `dispatch_sites` is a first-class relational table in V1, not a generic artifact store
- endpoint hints that can have many candidates use a join table
- trace-specific dispatch bindings belong to the Pass 2 flow-trace tables described in section 07, not on the base dispatch-site row

### 3. Provider-side endpoint entity

The `communication_endpoints` table is still the right direction, but it should be understood as a provider/interface catalog, not as the only abstraction for all cross-repo communication behavior.

Recommended V1 purpose:

- store canonical provider-side callable surfaces
- support normalization and matching
- support resolution of consumer observations
- support retrieval queries like "what calls this interface?"

Recommended additional semantics for the entity:

- endpoint entity may exist before all caller relationships are known
- endpoint entity may be discovered from code, config, specs, or framework metadata
- endpoint entity may represent an operation contract even if no cross-repo caller has been resolved yet

### 4. Consumer-side outcomes in Pass 1

When a Pass 1 worker sees cross-repo communication behavior, it should choose one of four outcomes.

#### Case A — exact endpoint known locally

If local evidence identifies a concrete endpoint strongly enough:

- create or match the `communication_endpoints` row
- create a `REFERENCES` edge if the target document is already known, or create the endpoint-linked unresolved record needed for later resolution

Examples:

- explicit REST route call to one known service path
- gRPC call bound to one known service method
- queue publish to one known topic contract

#### Case B — small bounded endpoint set known locally

If local evidence yields a small bounded set:

- create or match the candidate endpoint entities
- persist candidate-set `REFERENCES` edges only when the bounded set is justified from the local unit itself

#### Case C — caller-bound or environment-bound endpoint resolution

If the concrete endpoint depends on caller path, DI binding, generated-client config, or runtime environment:

- persist a **dispatch site**
- store protocol and endpoint/contract hints on that dispatch site
- do **not** force speculative base edges or fake exact endpoint matches

This is the key refinement needed for generated clients and RPC proxy behavior.

#### Case D — too little evidence even for a meaningful dispatch site

If the worker cannot identify a credible contract, operation, endpoint family, or runtime mechanism:

- create an `uncertainties` row
- do not create endpoint matches or dispatch-site bindings that are too weak to defend

### 5. Relationship between dispatch sites and endpoint entities

Dispatch sites should be allowed to carry endpoint-facing hints such as:

- `protocol`
- `operation_name`
- `contract_symbol`
- `endpoint_identifier_hint`
- `candidate_endpoint_ids`
- `normalization_context`

This means a dispatch site is not itself an endpoint. It is a consumer-side abstract invocation that may later bind to one or more endpoint entities.

Recommended V1 schema:

```text
dispatch_sites
  id
  scan_id
  repository_id
  source_document_id
  source_document_version_id
  source_code_unit_id
  call_symbol
  receiver_kind
  receiver_type_hint
  dispatch_kind
  resolution_scope        -- local, caller_bound, config_bound, runtime_open
  protocol                -- http, rpc, mq, file, page_navigation, unknown
  endpoint_identifier_hint
  contract_symbol
  operation_name
  normalization_context_jsonb
  confidence
  status                  -- unresolved, candidate, bound_in_trace, promoted_to_edge, wontfix
  created_at

dispatch_site_endpoint_candidates
  id
  dispatch_site_id
  communication_endpoint_id
  rank
  confidence
  evidence_jsonb
```

Implementation notes:

- `source_document_version_id` points at the generated Document Version that observed the dispatch site.
- `source_document_id` or logical identity is retained for current-version lookup.
- `dispatch_site_endpoint_candidates` is optional per row; weak hints can stay in `endpoint_identifier_hint` until provider endpoints exist.
- A trace binding may select one candidate for one flow trace without promoting that choice to a base `REFERENCES` edge.
- Promotion to an exact `REFERENCES` edge is allowed only when later Pass 1 evidence makes the target stable from the source unit itself.

### 6. Resolution flow with dispatch sites

Recommended flow:

1. consumer worker sees proxy/client call
2. worker extracts local evidence
3. worker chooses:
   - exact endpoint reference
   - bounded candidate endpoint set
   - dispatch site with endpoint hints
   - uncertainty
4. provider repo scan discovers or confirms `communication_endpoints`
5. matching logic connects endpoint entities to unresolved consumer records
6. Pass 2 may use caller-path and config evidence to bind dispatch sites to concrete endpoint targets for a specific trace

So section 04 should support both:

- **base-layer endpoint resolution**
- **trace-time dispatch-site binding**

### 7. Relationship to `communication_patterns`

The refined recommendation for V1 is:

- `communication_endpoints` are the canonical discovered/confirmed interface entities
- `communication_patterns` are supporting normalization/detection rules
- System Owner review applies to both discovered endpoints and important patterns

So `communication_patterns` should become a helper layer, not the primary source of truth.

### 8. Generated clients and RPC proxies

Generated clients are exactly the place where section 04 must align with section 03.

Recommended rule:

- if a generated client maps locally and deterministically to one endpoint, treat it like Case A
- if the generated client exposes a stable contract but the actual binding is caller-/config-dependent, treat it like Case C and record a dispatch site
- if the generated client is too opaque to support either, treat it like Case D and record uncertainty

### 9. What section 04 should hand to other sections

Section 04 should hand these assumptions forward:

- section 03 can rely on endpoint entities as the provider-side resolution target for RPC-like references
- section 05 can use CodeGraph and framework metadata to populate endpoint evidence and dispatch-site hints
- section 06 can bind dispatch sites to concrete endpoint targets at trace time when caller/config evidence allows it
- section 08 can query endpoints as stable integration surfaces without confusing them with every consumer-side invocation form

### 10. Remaining non-blocking implementation choices

Section 04 does not appear to have remaining **blocking design questions**. The remaining choices are implementation-level and can be decided later without reopening the core communication model.

Examples:

1. the exact normalization function library per protocol
2. the exact promotion flow from discovered endpoint to confirmed endpoint after review
3. exact index tuning for `dispatch_sites` and `dispatch_site_endpoint_candidates`

These are important, but they do not change the agreed direction:

- provider-side endpoint entities are canonical
- consumer-side abstract invocations may become dispatch sites
- generated clients and RPC proxies do not always map directly to stable base edges
- `communication_patterns` is a helper layer, not the primary source of truth

### 11. Resolved decisions summary

The completed decisions for section 04 are:

1. `communication_endpoints` are canonical provider-side interface entities
2. dispatch sites are the consumer-side abstraction for caller-/environment-bound communication behavior
3. Pass 1 has four allowed cross-repo outcomes:
   - exact endpoint
   - bounded endpoint set
   - dispatch site with endpoint hints
   - uncertainty
4. generated clients and RPC proxies may resolve to endpoints directly, to dispatch sites, or to uncertainty depending on evidence quality
5. `communication_patterns` becomes a supporting normalization/detection layer rather than the primary source of truth
6. Pass 2 may bind dispatch sites to concrete endpoint targets at trace time when caller/config evidence is strong enough
7. V1 stores `dispatch_sites` and candidate endpoint associations relationally

### 12. Practical V1 stance

Section 04 should now be treated as complete enough for downstream design work.

That means later sections may assume:

- exact endpoint mappings are possible but not mandatory for every cross-repo invocation
- some cross-repo communication will only exist as dispatch sites in the base layer
- endpoint resolution and trace-time dispatch-site binding are complementary, not competing, mechanisms
