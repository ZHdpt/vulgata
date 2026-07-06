# 04 — Cross-Repo Communication Endpoints

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Review pending — extracting from original plan; questions raised below.

---

## TODO — Open Questions from July Review

> These are concerns raised that need to be addressed before finalizing this section.

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
