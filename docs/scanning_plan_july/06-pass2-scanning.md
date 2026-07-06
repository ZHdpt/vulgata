# 06 — Pass 2 Scanning: Business Logic Documents

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Review pending — extracting from original plan; questions raised below.

---

## TODO — Open Questions from July Review

> These are concerns raised that need to be addressed before finalizing this section.

1. **Entry points should be recorded during Pass 1.** The current Pass 2 design dispatches a single worker per system and asks it to "identify business flows autonomously from the CL Doc graph." This is fragile:
   - The worker has to re-discover entry points by analyzing all CL Docs
   - Entry point identification (controller handlers, queue consumers, scheduled tasks) is structural and should be done during Pass 1
   - **Proposal:** During Pass 1, each worker tags its document with `entry_point = true` in `metadata_json` if the code_unit is an entry point. The orchestrator collects all entry points and provides them to the Pass 2 worker as starting points for flow tracing.

2. **Following the invocation chain from each entry point.** Once entry points are known, Pass 2 should:
   - Start from each entry point document
   - Follow REFERENCES edges transitively to trace the full invocation chain
   - Group related chains into business flows
   - This is a graph traversal problem, not an LLM judgment call — the LLM's job is to narrate the flow, not to discover its structure

   **Proposed Pass 2 workflow:**
   ```
   For each entry_point in system:
     1. Trace REFERENCES edges transitively (deterministic, via SQL CTE)
     2. Collect all CL Docs reachable from this entry point
     3. Pass this subgraph to a worker agent
     4. Worker synthesizes a BL Doc narrative from the subgraph
     5. Worker inserts GENERATED_FROM edges to all CL Docs used
   ```

3. **Overlapping flows.** Multiple entry points may share downstream CL Docs (e.g., both "Checkout" and "Refund" flows call `PaymentProcessor`). How should this be represented?
   - One BL Doc per entry point, with shared CL Docs appearing in multiple BL Docs
   - A shared "sub-flow" BL Doc for the shared segment, referenced by both top-level BL Docs
   - Current plan: one worker decides autonomously. Should this be algorithmic?

4. **Frontend applications need a different approach.** The current scanning model assumes backend code with clear function boundaries and call chains. Frontend apps are different:
   - **No clear "entry points" in the same sense.** A React component tree has many possible entry paths (routes, user interactions, event handlers).
   - **State-driven flows.** Business logic is distributed across components, hooks, stores, and reducers — not a linear call chain.
   - **Rendering paths, not invocation chains.** The "flow" is the sequence of state changes and re-renders, not function calls.
   - **Page navigation as a communication pattern.** `page_navigation` is already in the Communication Pattern Catalog, but it's fundamentally different from RPC/HTTP — looser coupling, user-driven, no structured endpoint.

   **Questions for frontend Pass 2:**
   - Should frontend repos skip Pass 2 entirely, or have a different Pass 2 strategy?
   - For SPAs: identify routes → trace component hierarchy → describe user flows per route?
   - For server-rendered apps (Blazor Server, Next.js SSR): treat like backend with page-level entry points?
   - How do `page_navigation` edges between frontend apps get resolved? A "navigate to /dashboard" in App A and a "route /dashboard" in App B — how do they match?

5. **Pass 2 dispatch granularity.** Current plan: one worker per system. This limits parallelism and creates a single point of failure. Alternatives:
   - One worker per entry point (parallel, but may produce overlapping/contradictory BL Docs)
   - One worker per cluster of related entry points (balance between parallelism and coherence)
   - Two-phase: first cluster entry points into flow groups (algorithmic), then dispatch one worker per group

---

## Extracted Content from Original Plan

### Pass 2: Business Logic Documents

After all CL Docs for a system are generated (or failed), the orchestrator begins Pass 2.

**Dispatch strategy — one worker per system (not per flow):** The original design proposed dispatching per "business flow" identified from the graph, but this has a circular dependency: flows can't be identified without analyzing the graph, which is the Pass 2 work itself. Instead, V1 dispatches a single Pass 2 worker per system. The worker receives:
- The full set of CL Docs in the system (their markdown bodies, metadata, and REFERENCES edges)
- The Communication Pattern Catalog for the system
- A prompt instructing it to identify business flows autonomously from the CL Doc graph

**Flow identification (done by the worker, not the orchestrator):** The worker analyzes the REFERENCES graph between CL Docs and identifies clusters that form business processes. Heuristics: entry points (controller handlers, queue consumers, scheduled tasks), termination points (database writes, external API calls, response returns), path coherence (a sequence of CL Docs that together process a single user action or business event). The worker names each flow (e.g., "Risk Evaluation," "Loan Approval") and produces one BL Doc per flow.

**Generation:** For each identified flow, the worker synthesizes a business-level narrative: validation rules, decision flows, regulatory checks, process steps. Each BL Doc is written to `content_md_path` with `doc_type = 'business_logic'`, `status = 'generated'`. Its `parent_document_id` points to a logical path (e.g., `business-flows/risk-evaluation/`).

**Provenance edges:** For each CL Doc the BL Doc was built from, the worker inserts a `GENERATED_FROM` edge:
```
source = <BL Doc ID>
target = <CL Doc ID>
edge_type = 'GENERATED_FROM'
confidence = 0.85  (LLM-generated, not deterministic)
```

These edges drive two-pass re-scan: when a CL Doc changes, any BL Doc with a `GENERATED_FROM` edge to it is flagged stale.

**Cross-BL-Doc references:** If the worker identifies that one business flow feeds into another, it inserts a `REFERENCES` edge between the two BL Docs. BL-to-BL REFERENCES edges are structurally identical to CL-to-CL REFERENCES edges.

**BL Doc regeneration on re-scan:** During incremental re-scan, the impact analysis recursive CTE walks both `REFERENCES` and `GENERATED_FROM` edges. CL Doc changes propagate through `GENERATED_FROM` to flag BL Docs stale.

### Known V1 Limitation — BL Doc stale markers after cross-repo resolution

When a BL Doc is generated before its cross-repo targets have been scanned, the BL Doc body may contain "unscanned" or placeholder references. Cross-repo resolution creates the REFERENCES edge but does NOT trigger BL Doc regeneration. The LLM-wiki chat agent compensates at query time by following the now-resolved cross-repo edge and synthesizing a complete answer. Full BL Doc regeneration on cross-repo resolution is deferred to V2.

---

## Notes for Revision

- The entry-points-during-Pass-1 proposal eliminates the circular dependency that forced the "one worker per system" compromise. If entry points are tagged during Pass 1, the orchestrator can deterministically group CL Docs by flow reachability before Pass 2 starts.
- Frontend scanning is a significant gap. The current plan was designed for backend codebases. A separate "Frontend Document Generation" section may be needed, covering component documentation, route-to-component mapping, state flow diagrams, and user interaction paths.
- The `page_navigation` protocol in edges and communication_patterns was included for frontend cross-app linking but was never fully spec'd out.
