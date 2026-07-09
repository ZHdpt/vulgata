# 06 — Pass 2 Scanning: Business Logic Documents

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Section 06 completed - path-state Pass 2 design agreed and ready to use as input for sections 07 and 08.

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

---

## Clarification Notes for Current Review

### 1. Pass 2 traversal cannot treat all `REFERENCES` edges as equally active on one runtime path

Pass 2 should not assume that every reachable `REFERENCES` edge from an entry point belongs to the same concrete execution path.

This matters for cases like:

```text
A -> B -> (C or D)
```

If the base graph stores `B -> C` and `B -> D` as a candidate set, Pass 2 must understand that:

- both are graph-level possibilities
- only one may be active on a given branch
- upstream context from `A` may narrow the choice

### 2. Pass 2 should traverse with branch state, not plain reachability alone

The recommended model is that Pass 2 builds a **flow trace state** while traversing.

That state should carry at least:

- current document
- active path notes or branch conditions
- candidate-group selections already narrowed
- unresolved candidate groups still open

So traversal is conceptually over:

```text
(doc_id, path_state)
```

not just:

```text
doc_id
```

The `path_state` should include both:

- branch selections for locally bounded candidate sets
- dispatch-site bindings for caller-bound abstract invocations

### 3. Recommended Pass 2 behavior for candidate-set edges

When Pass 2 reaches a candidate-set edge group, it should apply this rule set.

#### Case A — upstream evidence narrows the branch

If upstream context from the entry point or earlier nodes narrows the candidate group, then:

- select the narrowed target for this trace
- record the basis for the selection in flow metadata
- continue traversal on the selected branch

Example:

- base graph has `B -> C` and `B -> D`
- caller `A` implies config or state that selects `C`
- Pass 2 trace records `candidate_group_id cg-b-001 -> C`

#### Case B — branch remains unresolved but bounded

If Pass 2 cannot choose between a small bounded set, then it should:

- keep both branches in the trace model
- mark the flow as branched or ambiguous
- narrate the ambiguity explicitly in the BL document

The BL document may say, in effect:

- "After B, execution continues to C or D depending on runtime condition X, which could not be fully resolved from available evidence."

#### Case C — branch is too weakly justified

If the candidate set is too weak, too broad, or poorly justified, Pass 2 should prefer uncertainty over pretending to have a branching flow.

### 3A. Recommended Pass 2 behavior for dispatch sites

When Pass 2 reaches a dispatch site, it should not treat it like an already-known downstream edge.

Instead it should try to resolve the dispatch site from the current path state.

#### Case A — caller path provides a concrete binding

If upstream path context shows what concrete implementation was supplied, then:

- bind the dispatch site to that target for this trace
- record the binding basis in trace metadata
- continue traversal to the selected target

#### Case B — environment/config narrows the binding

If config, DI registration, or framework metadata narrows the target enough, then:

- bind the dispatch site to the bounded target or candidate set
- continue traversal with appropriate confidence

#### Case C — binding remains unresolved

If the dispatch site cannot be resolved from caller path, config, or bounded structural evidence, then:

- do not invent a concrete downstream node
- keep the dispatch site unresolved in the trace state
- narrate the unresolved abstract handoff in the BL document if it matters to the flow

### 4. Business-logic output should preserve branch semantics

When a business flow contains a conditional branch, the BL document should not flatten the branch into a false linear chain.

Instead it should describe one of these outcomes:

- exact path selected
- bounded alternative paths
- unresolved runtime branch

This is necessary to avoid producing misleading business narratives.

### 5. Relationship between Pass 1 and Pass 2 in these cases

Pass 1 and Pass 2 should have different responsibilities.

#### Pass 1 responsibility

- identify local candidate sets
- identify dispatch sites where the callee is caller-bound or environment-bound
- record candidate-group metadata on ambiguous edges
- emit uncertainty when the target set cannot be bounded credibly

#### Pass 2 responsibility

- use upstream context to narrow candidate sets when possible
- use upstream bindings to resolve dispatch sites when possible
- preserve ambiguity when narrowing is not possible
- synthesize business logic from path-aware traces rather than naive graph reachability

### 6. Practical implication for flow extraction

The original simplified idea of:

1. start from entry point
2. follow all `REFERENCES` transitively
3. give subgraph to worker

is not sufficient by itself.

It should be refined to:

1. start from entry point
2. follow `REFERENCES` transitively **with candidate-group, dispatch-site, and path-state tracking**
3. resolve dispatch sites from caller-bound bindings or config evidence where possible
4. build one or more flow traces, not just one undifferentiated reachable subgraph
5. give the worker the trace set plus ambiguity metadata

This still keeps Pass 2 graph-first, but avoids turning all possible branches into one misleading path.

---

## Finalized Direction for Section 06

### 1. Core decision

Pass 2 should be a **deterministic planning step plus LLM synthesis**, not one monolithic Worker Agent that rediscovers all business flows from scratch.

The Repo Workflow Orchestrator should:

- wait until Pass 1 and its bounded refinement wave are complete or explicitly cut off
- collect entry points, `REFERENCES` edges, dispatch sites, candidate groups, endpoint records, and unresolved uncertainties
- build path-aware traces from entry points
- cluster those traces into flow groups
- dispatch one Pass 2 Worker Agent per flow group

The Pass 2 Worker Agent should:

- read the prepared graph slice and trace set
- synthesize Business Logic Documents
- preserve branch, dispatch, and uncertainty semantics in the narrative
- submit BL Documents plus `GENERATED_FROM` edges

So Pass 2 operates over:

```text
(doc_id, path_state)
```

not plain `doc_id` reachability.

### 2. Pass 2 planning inputs

Pass 2 planning should consume these artifacts from Pass 1 and earlier stages:

- completed Code Logic Documents
- failed or partial Code Logic Document markers
- `REFERENCES` edges with confidence and metadata
- candidate-group metadata on ambiguous edges
- persisted dispatch sites
- communication endpoint records
- unresolved uncertainties
- entry-point metadata
- relevant pre-scan context and plain-text resource files
- CodeGraph structural facts needed for binding checks

Pass 2 should not begin by reading raw repository files. It is **graph-first**. Raw source should only be pulled if a Worker Agent needs a bounded citation check or small local clarification.

### 3. Entry point model

Entry points should be recorded during Pass 1, not rediscovered in Pass 2.

Recommended metadata:

- `entry_point = true`
- `entry_point_kind`
- `entry_point_label`
- protocol or framework hints
- route, topic, schedule, command, component, or event metadata where applicable
- source confidence

Recommended `entry_point_kind` values for V1:

- `http_route`
- `rpc_endpoint`
- `queue_consumer`
- `scheduled_task`
- `cli_command`
- `event_handler`
- `page_route`
- `ui_interaction`
- `app_bootstrap`
- `workflow_trigger`

Provider-side communication endpoints can also be entry points. For example, an HTTP controller action may be both:

- a Code Logic Document entry point for a provider repository
- the source code anchor for a `communication_endpoint`

### 4. Flow trace state

The orchestrator should build flow traces by carrying a path state.

A practical V1 trace state should include:

- `trace_id`
- `entry_point_document_id`
- `current_document_id`
- visited document IDs
- active path conditions
- selected candidate groups
- unresolved candidate groups
- dispatch-site bindings
- unresolved dispatch sites
- endpoint bindings
- confidence floor for the trace
- termination reasons

Candidate-group selections and dispatch-site bindings should be trace-local. They should not rewrite the base graph unless a later Pass 1 refinement establishes that the target is stable from the source unit itself.

### 5. Trace transition rules

For each `(doc_id, path_state)` state, the planner should evaluate outgoing graph facts in this order.

#### Exact `REFERENCES`

If an outgoing `REFERENCES` edge is exact and not excluded by the current path state:

- append the target document to the trace
- carry forward the path state
- lower the trace confidence floor if the edge confidence is lower than the current floor

#### Candidate-group `REFERENCES`

If outgoing edges share a `candidate_group_id`:

- select one branch when current path evidence clearly narrows the group
- fork bounded branches when the group remains plausible but unresolved
- keep the candidate group marked unresolved when the planner cannot choose

The planner should avoid exploding branches. V1 should cap candidate branching per trace and preserve unresolved branches as narrative ambiguity when the cap is reached.

#### Dispatch sites

If the current document has dispatch sites:

- call or use the orchestrator's binding-context service for the current job and dispatch site
- bind the dispatch site when caller path, config, DI, endpoint, or framework evidence is strong enough
- fork a bounded candidate set when evidence narrows but does not select one target
- leave the dispatch site unresolved when evidence is insufficient

An unresolved dispatch site is not a failed trace. It is a known abstract handoff that the BL Document should explain when business-relevant.

#### Cycles and repeated helpers

Pass 2 should detect cycles and repeated helpers.

Recommended V1 behavior:

- allow revisiting a document only when the path state materially differs
- cap recursion depth and repeated-state count
- summarize loops as repeated behavior rather than expanding forever

Examples:

- retry loops
- polling loops
- state-machine transitions
- recursive tree traversal

### 6. Flow-group construction

Pass 2 should dispatch one worker per **flow group**, not one worker per System.

The orchestrator should first build traces from entry points, then cluster traces into flow groups.

Recommended construction:

1. Start one trace set from each entry point.
2. Traverse path-aware `REFERENCES`, candidate groups, and dispatch sites.
3. Stop traces at termination boundaries.
4. Compare traces for overlap and semantic relatedness.
5. Merge related traces into flow groups.
6. Create one Pass 2 job per flow group.

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

Recommended grouping signals:

- shared entry-point family, such as related routes or handlers
- high overlap in non-trivial CL Documents
- shared terminal effect, such as the same database write or outgoing endpoint
- shared domain terms in CL document summaries
- shared endpoint, topic, aggregate, or workflow identifier
- same user action or business event inferred from entry-point metadata

Do not merge two flows merely because they share a common helper. Shared utility, validation, logging, mapping, or repository methods should usually appear in multiple BL Documents through multiple `GENERATED_FROM` edges.

### 7. Shared sub-flows

V1 should allow the same CL Document to support multiple BL Documents.

That means:

- a shared downstream CL Document may have incoming `GENERATED_FROM` edges from several BL Documents
- duplicate use is acceptable when the business flows are distinct
- Pass 2 does not need to create a shared sub-flow BL Document by default

A shared sub-flow BL Document is optional in V1 and should only be created when all of these are true:

- the shared segment has a meaningful business name
- the segment is used by multiple flow groups
- the segment is large enough that repeating it would make top-level BL Documents confusing
- the segment has a stable boundary in the graph

Otherwise, keep the model simple: one BL Document per flow group.

### 8. Pass 2 Worker job packet

Each Pass 2 job should include:

- flow group ID
- entry point documents
- ordered flow traces
- trace-state metadata
- CL Documents in traversal order
- relevant `REFERENCES` edges
- candidate-group decisions and unresolved candidate groups
- dispatch-site bindings and unresolved dispatch sites
- communication endpoint matches and unresolved endpoint hints
- relevant uncertainties
- relevant resource or config documents
- related outputs from overlapping flow clusters when already available

The worker should not be asked to discover the graph. It should be asked to explain the prepared graph slice accurately.

### 9. Business Logic Document output rules

A BL Document should preserve the trace semantics it was generated from.

The document body should distinguish:

- confirmed path steps
- bounded alternatives
- unresolved runtime choices
- unresolved dispatch sites
- cross-repo handoffs
- external or unscanned dependencies

The document metadata should include:

- source flow group ID
- entry point document IDs
- trace IDs used
- selected candidate groups
- unresolved candidate groups
- dispatch-site bindings
- unresolved dispatch sites
- endpoint IDs referenced
- unresolved uncertainty IDs
- confidence summary

`GENERATED_FROM` edges should point from the BL Document to the CL Documents actually used in synthesis.

Do not create `GENERATED_FROM` edges to:

- unscanned external systems
- unresolved uncertainties without target documents
- endpoint records that are not Documents
- dispatch sites themselves

Those should be referenced in metadata until a Document target exists.

### 10. Cross-repo and unresolved output behavior

Pass 2 should align with section 04's endpoint model.

If a cross-repo `REFERENCES` edge is resolved:

- include the linked target in the trace when traversal budget allows
- describe the cross-repo handoff in the BL Document
- include the target CL Document in `GENERATED_FROM` only if the BL Document actually used it

If an endpoint entity exists but the provider Document is unscanned or unresolved:

- do not invent a provider-side flow
- describe the endpoint or contract as known but not fully scanned
- carry the endpoint ID and uncertainty ID in metadata

If a dispatch site has endpoint hints but no concrete binding:

- describe the abstract handoff if business-relevant
- keep it unresolved in trace metadata

This preserves the known V1 limitation: Cross-Repository Resolution may later create new `REFERENCES` edges, but it does not automatically regenerate stale BL Document prose in V1.

### 11. Frontend and page-navigation handling

Frontend repositories should not skip Pass 2. They should use frontend-specific entry points and trace rules.

For SPAs, Pass 2 should treat these as likely entry points:

- routes
- page components
- form submit handlers
- button or menu action handlers
- store actions
- reducer/action pairs
- effect hooks that call services or mutate state

Frontend flow tracing should follow:

- route to page/component hierarchy
- component to event handler
- event handler to state/store/service call
- state change to visible user outcome when structurally evident
- outgoing API, message, file, or page-navigation handoff

For server-rendered or hybrid frameworks such as Blazor Server, Next.js SSR, MVC, or Razor Pages:

- page routes and handlers are entry points
- server-side handler logic can be traced like backend code
- component event handlers can be traced like UI interactions

`page_navigation` should be treated as a loose communication protocol.

Recommended rule:

- provider side: page route or route pattern may become a `communication_endpoint` with `protocol = page_navigation`
- consumer side: navigation action may become an exact reference, candidate endpoint set, dispatch site, or uncertainty using the same Pass 1 rubric
- matching should use normalized route, app/base path, route parameters, and Service Topology evidence when available
- confidence should usually be lower than RPC or HTTP API matching because the coupling is user-driven and less contractual

If a user action in App A navigates to `/dashboard` and App B owns a matching route:

- create a cross-repo `REFERENCES` edge only when route ownership and target repository evidence are strong
- otherwise create an endpoint-linked uncertainty or dispatch site with route hints

### 12. V1 simplifications

To keep Pass 2 practical for V1:

- no runtime trace ingestion
- no complete frontend state-machine reconstruction
- bounded traversal depth and branch count
- one BL Document per flow group by default
- shared sub-flow BL Documents only when clearly justified
- no automatic BL prose regeneration on later Cross-Repository Resolution
- unresolved dispatch sites are allowed in BL Documents
- failed CL Documents should appear as gaps, not block all Pass 2 output

### 13. Practical answers to the original five questions

#### Q1 - Entry points in Pass 1

Answer:

- yes, Pass 1 should tag entry points in document metadata
- Pass 2 planning should consume those tags directly
- the Worker Agent should not rediscover all entry points from scratch

#### Q2 - Invocation chain traversal

Answer:

- Pass 2 should traverse from entry points with `(doc_id, path_state)`
- plain transitive reachability is insufficient
- candidate groups and dispatch sites must be path-state aware

#### Q3 - Overlapping flows

Answer:

- the orchestrator should cluster traces into flow groups
- shared CL Documents may feed multiple BL Documents
- shared sub-flow BL Documents are optional and should be rare in V1

#### Q4 - Frontend approach

Answer:

- frontend repos should still run Pass 2
- entry points are routes, UI interactions, store actions, and page/component handlers
- `page_navigation` uses the same endpoint/dispatch/uncertainty model as other cross-repo communication, but usually with lower confidence

#### Q5 - Pass 2 dispatch granularity

Answer:

- dispatch one Pass 2 Worker Agent per flow group
- do not dispatch one Worker per entire System
- do not dispatch blindly one Worker per entry point when clustering shows the entry points are part of the same business flow

### 14. What section 06 assumes from earlier sections

Section 06 now assumes:

- section 02 provides deterministic workflow orchestration and Pass 2 flow-group jobs
- section 03 provides candidate groups, dispatch sites, and trace-time binding semantics
- section 04 provides communication endpoint entities and endpoint-hinted dispatch sites
- section 05 provides the Pass 1 edge-emission rubric and bounded refinement model

### 15. What section 06 hands to later sections

Section 06 hands these assumptions forward:

- section 07 should store enough structured BL metadata to preserve flow traces, branch decisions, dispatch bindings, and unresolved states
- section 08 should retrieve BL Documents together with their supporting CL Documents, endpoint records, and unresolved trace metadata
- chat should not flatten unresolved dispatch or candidate branches into certain business facts

### 16. Resolved decisions summary

The completed decisions for section 06 are:

1. Pass 2 is graph-first and path-state aware.
2. Entry points are emitted in Pass 1 and consumed by Pass 2 planning.
3. The orchestrator builds flow traces over `(doc_id, path_state)`.
4. Candidate-group choices and dispatch-site bindings are trace-local.
5. Flow groups are constructed deterministically before Worker Agent synthesis.
6. Pass 2 dispatches one Worker Agent per flow group.
7. BL Documents must preserve confirmed, branched, and unresolved semantics.
8. Shared CL Documents may support multiple BL Documents through multiple `GENERATED_FROM` edges.
9. Frontend repositories use route, interaction, state/store, and page-navigation-aware Pass 2 rules.
10. `page_navigation` follows the endpoint/dispatch/uncertainty model with lower default confidence.
