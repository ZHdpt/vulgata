# Cross-Reference Analysis — Scanning Plan July

**Date:** 2026-07-07
**Scope:** Cross-referencing all 8 question documents under `docs/scanning_plan_july/`

---

## Dependency Matrix

Each cell shows how a decision in the row-document affects or constrains questions in the column-document.

|  | 01-PreScan | 02-Orch | 03-Graph | 04-XRepo | 05-CG | 06-Pass2 | 07-Storage | 08-Retrieval |
|--|-----------|---------|---------|---------|-------|---------|-----------|-------------|
| **01** | — | ⚡⚡⚡ | ○ | ⚡⚡ | ⚡ | ⚡⚡ | ○ | ○ |
| **02** | ⚡⚡⚡ | — | ⚡⚡ | ⚡ | ⚡⚡⚡ | ⚡⚡⚡ | ○ | ⚡ |
| **03** | ○ | ○ | — | ⚡⚡⚡ | ⚡⚡ | ⚡⚡ | ○ | ⚡ |
| **04** | ⚡ | ○ | ⚡⚡ | — | ⚡ | ○ | ○ | ⚡⚡ |
| **05** | ⚡⚡ | ⚡⚡ | ⚡⚡ | ⚡ | — | ⚡⚡ | ○ | ⚡ |
| **06** | ⚡⚡ | ⚡⚡ | ⚡⚡ | ○ | ⚡ | — | ○ | ⚡ |
| **07** | ○ | ○ | ○ | ○ | ○ | ○ | — | ⚡⚡⚡ |
| **08** | ○ | ○ | ○ | ⚡ | ⚡ | ○ | ⚡⚡ | — |

> ⚡⚡⚡ = Critical dependency — decision in row MUST be resolved before column can be finalized  
> ⚡⚡ = Strong dependency — decision in row significantly shapes options in column  
> ⚡ = Notable dependency — decision in row affects edge cases or specific questions in column  
> ○ = No meaningful dependency

---

## Critical Chains (Decisions That Cascade)

> Update after completing sections 01, 02, 03, 04, 05, 06, 07, and 08: the scanning pipeline, canonical Document storage, and retrieval direction are now sufficiently resolved for downstream implementation planning.

### Chain 1: Orchestrator Design → Everything Else

**02 (Agent Orchestrator) is the hub.** The decision between "LLM magnet orchestrator" and "non-LLM workflow orchestrator" radiates to:

| Affected Document | Specific Question Affected | Why |
|------------------|---------------------------|-----|
| **01 Q2** | Single agent for pre-scanning? | If orchestrator is non-LLM, pre-scanning becomes deterministic tooling, not an agent task |
| **01 Q1** | What pre-scanning must produce? | Non-LLM orchestrator calls CodeGraph directly, owns `code_units` production |
| **02 Q3** | Responsibility table | This IS the question — the table defines the split |
| **05 Q1** | CodeGraph's role in pipeline | Non-LLM orchestrator owns CodeGraph indexing; workers use it as a tool |
| **05 Q2** | CodeGraph as worker tool | Tool-based agent interface (02 Q4) enables `find_callers`/`find_callees` as worker tools |
| **05 Q5** | Performance/caching | Non-LLM orchestrator manages a single CodeGraph index instance per repo |
| **06 Q5** | Pass 2 dispatch granularity | Workflow engine can deterministically group entry points, dispatch multiple workers |
| **06 Q1** | Entry point recording in Pass 1 | Workflow engine collects entry points from worker outputs; no LLM coordination needed |

**Recommendation:** Section 02 is now resolved enough to treat as a fixed assumption. Sections 05 and 06 now build on it with deterministic Pass 1 and Pass 2 planning.

---

### Chain 2: Edge Types + Communication Endpoints → Cross-Repo Resolution

**03 (Document Graph) and 04 (Cross-Repo Communication) are deeply intertwined.**

| 03 Question | 04 Question | Relationship |
|------------|------------|-------------|
| 03 Q2 — RPC proxy → endpoint mapping | 04 Q1 — `communication_endpoints` table | The `communication_endpoints` table is the provider-side half of the solution to 03 Q2. Exact proxy calls may map directly to endpoint entities, while caller-/environment-bound proxy behavior should be stored first as dispatch sites with endpoint hints. |
| 03 Q1 — Dynamic references (virtual methods) | 04 Q2 — Agent endpoint discovery | Some abstract invocations are not endpoint problems at all; they stay as local dispatch sites or CodeGraph-resolved structure. But for cross-repo dynamic calls, endpoint discovery provides the provider-side targets that dispatch sites may later bind to. |
| 03 Q5 — New edge types (PUBLISHES/SUBSCRIBES) | 04 Q3 — Endpoint discovery (message queues) | Event-driven architectures still need canonical endpoint entities for topics/queues, but in V1 they do not require dedicated top-level edge types; message-driven references can remain `REFERENCES` plus protocol and dispatch metadata. |
| 03 Q4 — Edge confidence decomposition | 04 Q5 — Normalization rules | If normalization is deterministic (not prose-based), endpoint-linked references and dispatch-site hints matched through normalization have higher confidence than edges or bindings matched by weak heuristics. |

**Recommendation:** Treat sections 03 and 04 as resolved together. The `communication_endpoints` table is the provider-side bridge, while dispatch sites are the consumer-side abstraction for caller-/environment-bound communication.

---

### Chain 3: Document Storage → Context Retrieval

**07 (Storage) decisions dictate what 08 (Retrieval) can do.**

| 07 Question | 08 Question | Relationship |
|------------|------------|-------------|
| 07 Q2 — Filesystem vs JSONB vs MongoDB | 08 Q2 — LLM-wiki retrieval | PostgreSQL-first storage means retrieval can query database indexes and read rendered Markdown without treating `index.md` or filesystem files as canonical. |
| 07 Q3 — Structured document sections | 08 Q5 — Context window management | Section-level retrieval means the agent loads 3 sections of 200 words each instead of 3 documents of 2000 words each. Context window management becomes trivial. |
| 07 Q3 — Structured document sections | 08 Q6 — Cross-repo retrieval | JSONB section refs help explain why context was selected, but cross-document traversal still uses relational `edges`, `uncertainties`, and endpoint records. |
| 07 Q5 — Structured versioning | 08 Q4 — log.md indexing | Full JSONB/Markdown snapshots plus structured scan/event tables make RAG over `log.md` unnecessary for operational questions. |

**Recommendation:** Treat sections 07 and 08 as resolved together. Section 08 uses PostgreSQL-first storage, relational graph traversal, `content_markdown`, `content_jsonb`, and typed retrieval tools.

---

### Chain 4: CodeGraph Usage → Edge Accuracy → Pass 2 Quality

**05 (CodeGraph) determines the quality floor for 03 (edges) and 06 (Pass 2).**

| 05 Question | Affected Area | Relationship |
|------------|--------------|-------------|
| 05 Q2 — CodeGraph as worker tool | 03 Q1 — Dynamic references | If workers can call `find_callees(symbol)`, they get a deterministic list of static calls. The LLM's job becomes: (a) identify which of these are dynamic, (b) tag them with lower confidence, (c) create uncertainties for the ambiguous ones. Without CodeGraph as a tool, the LLM reads source and guesses — producing both false positives and false negatives. |
| 05 Q3 — Traversal depth | 06 Q2 — Invocation chain traversal | If CodeGraph provides pre-computed call paths up to depth N, Pass 2 workflow can deterministically trace from entry point to termination point. No LLM needed for the traversal — only for the narrative. |
| 05 Q4 — Cross-file references | 03 Q1 — DI/virtual method resolution | The leap from `@Autowired PaymentProcessor processor` to the concrete `StripePaymentProcessor` class requires cross-file symbol resolution. CodeGraph can do this deterministically; an LLM reading only the current file cannot. |
| 05 Q6 — CodeGraph vs tree-sitter | 01 Q1 — Pre-scanning output | If CodeGraph MCP tools are used, pre-scanning produces `code_units` + call graph + symbol index in one pass. If only tree-sitter, pre-scanning produces only `code_units` — call relationships must be discovered by workers or not at all. |

**Update after the invocation-chain discussion:** sections 03, 05, and 06 now rely on a stronger distinction between:

- ordinary `REFERENCES` edges,
- caller-bound **dispatch sites**, and
- trace-time bindings in Pass 2.

Section 05 also now assumes Pass 1 may run a **bounded refinement wave**, where later-scanned units can materially enrich earlier Code Logic documents without triggering uncontrolled rescans.

**Recommendation:** Treat section 05 as resolved enough for downstream design. Workers consume CodeGraph-backed structural context through orchestrator tools, while Pass 1 still lets Worker Agents classify evidence into edges, candidate sets, dispatch sites, or uncertainties.

---

### Chain 5: Pre-Scan Scope + CodeGraph Signals → Pass 1 Entry Points → Pass 2 Flow Discovery

**01 (Pre-Scanning), 05 (CodeGraph), and 06 (Pass 2) form a pipeline dependency.**

```
01 (Pre-scan classifies scope and hotspots)
    +
05 Q2 (CodeGraph detects entry-point annotations like @Controller, @KafkaListener)
    ↓
06 Q1 (Entry points recorded during Pass 1 in metadata_json)
    ↓
06 Q2 (Pass 2 traces invocation chain from each entry point)
    ↓
06 Q3 (Overlapping flows resolved algorithmically)
```

**Key insight:** If pre-scan narrows the file scope and CodeGraph provides structural hints, then Pass 1 can identify entry points without requiring the System Owner to enumerate them upfront. The workflow engine can:
1. Collect all entry points
2. Trace REFERENCES from each while carrying candidate-group and dispatch-site path state
3. Cluster overlapping traces into flow groups
4. Dispatch one worker per flow group

This eliminates the circular dependency that forced the original "one worker per system" design.

---

## Question-to-Question Cross-Reference Table

Every question in every document, mapped to related questions in other documents.

| Doc.Question | Short Title | Related Questions | Nature of Dependency |
|-------------|------------|-------------------|---------------------|
| 01.Q1 | Goals of pre-scanning | 02.Q3, 05.Q1, 05.Q6 | What pre-scanning produces depends on who runs it and what tools they have |
| 01.Q2 | Single agent approach | 02.Q1, 02.Q2, 02.Q3 | "Single agent" is undefined until orchestrator design is decided |
| 01.Q3 | System Owner interview | 04.Q4, 06.Q1, 04.Q2 | Owner provides repo boundaries, exclusions, and external integration context; entry points are still discovered in Pass 1 |
| 01.Q4 | One-shot vs iterative | 02.Q2 | Iterative interview needs an agent; one-shot could be a form. Orchestrator design affects this |
| 01.Q5 | Auto-detect comm patterns | 04.Q2, 04.Q4, 05.Q2 | Auto-detection = agents use CodeGraph to find annotations. Complements/replaces owner config |
| 02.Q1 | Magnet vs workflow | 01.Q1, 01.Q2, 05.Q1, 06.Q5 | **Architectural keystone.** Affects who does everything in the pipeline |
| 02.Q2 | Tool-based dispatch | 05.Q2, 05.Q5, 03.Q1 | Tool interface design depends on what tools CodeGraph exposes to workers |
| 02.Q3 | Responsibility table | ALL other docs | Defines LLM vs non-LLM boundary for every pipeline step |
| 02.Q4 | Tool interface design | 05.Q2, 03.Q1, 04.Q2 | Worker tools must align with what CodeGraph, edges, and endpoints provide |
| 02.Q5 | Superstep grouping | 05.Q2, 05.Q4 | Call-graph-based grouping needs CodeGraph cross-file resolution |
| 03.Q1 | Dynamic references | 04.Q2, 05.Q2, 05.Q4, 06.Q2, 06.Q4 | Dynamic dispatch affects Pass 1 edge creation, Pass 2 traversal, and frontend handling |
| 03.Q2 | RPC proxy mapping | 04.Q1, 04.Q2, 04.Q3 | The `communication_endpoints` table plus dispatch-site-aware endpoint discovery is the answer to "how does the agent map proxy→endpoint" |
| 03.Q3 | GENERATED_FROM semantics | 06.Q3, 06.Q2 | Direction and multiplicity of provenance edges depends on how Pass 2 groups flows |
| 03.Q4 | Edge confidence decomposition | 04.Q5, 05.Q2 | Confidence dimensions: structural (CodeGraph) > endpoint-matched (normalized) > heuristic (LLM) |
| 03.Q5 | New edge types | 04.Q3, 06.Q4 | Message-driven references still need mq endpoint entities and protocol metadata; override behavior still depends on inheritance resolution; page_navigation still needs frontend-aware Pass 2 handling |
| 04.Q1 | communication_endpoints table | 03.Q2, 03.Q1, 08.Q1, 08.Q6 | Canonical endpoint entities bridge provider-side interfaces, graph edges, dispatch-site binding, and chat retrieval |
| 04.Q2 | Agent endpoint discovery | 01.Q5, 05.Q2, 03.Q1, 03.Q2 | Agents need CodeGraph to detect annotations; discovery feeds into communication_endpoints |
| 04.Q3 | Endpoint discovery sources | 03.Q5, 05.Q2 | Each protocol (HTTP, gRPC, MQ, etc.) needs different CodeGraph detection patterns |
| 04.Q4 | Replace vs complement comm_patterns | 01.Q3, 01.Q5 | If agents auto-discover, System Owner role shifts from "define" to "review" |
| 04.Q5 | Normalization rules | 03.Q4 | Deterministic normalization → higher confidence edges; prose-based → lower |
| 05.Q1 | CodeGraph role in pipeline | 01.Q1, 02.Q3, 06.Q1 | Current: pre-scan only. Proposed: also during Pass 1 as worker tool |
| 05.Q2 | CodeGraph as worker tool | 02.Q2, 02.Q4, 03.Q1, 03.Q2, 04.Q2, 04.Q3 | Enables deterministic call tracing, annotation detection, cross-file resolution |
| 05.Q3 | Traversal depth | 03.Q1, 06.Q2 | How deep CodeGraph traces affects both edge creation, dispatch-site evidence gathering, and flow tracing |
| 05.Q4 | Cross-file references | 03.Q1, 02.Q5, 06.Q2 | Needed for DI resolution, superstep grouping, virtual method → implementation mapping, and caller-bound dispatch resolution |
| 05.Q5 | Performance/caching | 02.Q2, 02.Q5 | Who runs CodeGraph index, when, and how workers access it |
| 05.Q6 | CodeGraph vs tree-sitter | 01.Q1, 02.Q3 | Determines whether workers have call-graph tools at all |
| 06.Q1 | Entry points in Pass 1 | 01.Q3, 05.Q1, 02.Q3, 03.Q1 | Entry point detection = System Owner input + CodeGraph annotation scanning + worker tagging |
| 06.Q2 | Invocation chain traversal | 05.Q3, 05.Q4, 03.Q1, 03.Q3 | Traversal depth, cross-file resolution, candidate groups, and dispatch-site bindings determine flow completeness |
| 06.Q3 | Overlapping flows | 03.Q3, 03.Q5 | Shared CL Docs → multiple GENERATED_FROM edges or a shared sub-flow BL Doc |
| 06.Q4 | Frontend approach | 03.Q1, 03.Q5, 04.Q3, 05.Q2 | Frontend needs: different entry point concept, page_navigation edges, component-level docs |
| 06.Q5 | Pass 2 dispatch granularity | 02.Q3, 02.Q1 | Non-LLM orchestrator → deterministic grouping → parallel workers per flow group |
| 07.Q1 | Filesystem markdown | 08.Q2, 08.Q5 | Filesystem Markdown is export-only; retrieval reads database-backed rendered Markdown or sections |
| 07.Q2 | MongoDB vs JSONB vs filesystem | 08.Q2, 08.Q3, 08.Q5 | PostgreSQL JSONB plus rendered Markdown is canonical; MongoDB rejected for V1; filesystem export only |
| 07.Q3 | Structured document design | 08.Q2, 08.Q5, 08.Q6 | Section-level retrieval, trace metadata, context window efficiency |
| 07.Q4 | Migration complexity | — | No cross-doc dependency; internal trade-off |
| 07.Q5 | Structured versioning | 08.Q4, 06.Q2 | Diff-based versions enable "what changed?" queries |
| 08.Q1 | Repo-scoped retrieval | 04.Q1, 06.Q4 | `communication_endpoints` helps cross-repo linking; frontend needs page_navigation scoping |
| 08.Q2 | LLM-wiki limitations | 07.Q1, 07.Q2, 07.Q3, 05.Q2 | Resolved as database-backed agentic LLM-wiki through typed retrieval tools, not literal `index.md` parsing |
| 08.Q3 | RAG / embeddings | 07.Q2, 07.Q3 | PostgreSQL full-text plus graph expansion first; `pgvector` remains an optional upgrade path |
| 08.Q4 | log.md indexing | 07.Q5 | Structured scan/event tables make `log.md` RAG unnecessary |
| 08.Q5 | Context window management | 07.Q3, 03.Q5, 06.Q3 | Budgeted retrieval, section-level loading, and hierarchical BL→CL expansion reduce token usage |
| 08.Q6 | Cross-repo retrieval | 04.Q1, 03.Q1 | Typed graph tools follow authorized, relevant cross-repo `REFERENCES` and endpoint context |

---

## Contradictions & Tensions

These are places where two documents propose approaches that may conflict.

### Tension 1: "LLM decides" vs "deterministic algorithm"

| Doc | Position | Doc | Position |
|-----|---------|-----|---------|
| **02 (current plan)** | LLM orchestrator decides superstep grouping, dispatch ordering, cross-verification coordination | **02 (proposed)** | Deterministic workflow engine does all of the above |
| **03 (current plan)** | Workers read source and LLM-decide what edges to create | **05 (proposed)** | CodeGraph provides deterministic call graph; workers annotate with meaning |
| **06 (current plan)** | One LLM worker identifies flows autonomously | **06 (proposed)** | Workflow traces REFERENCES deterministically; LLM narrates |

**Resolution adopted:** The proposed approach is now the working direction: deterministic for structure, LLM for semantics.

### Tension 1A: Stable base graph vs path-specific truth

| Doc | Position |
|-----|---------|
| **03 (current refined plan)** | The base graph should only store stable references or dispatch sites justified from the local unit |
| **06 (current refined plan)** | Pass 2 should resolve candidate branches and dispatch-site bindings per trace |
| **05 (current refined plan)** | Pass 1 may provisionally document a unit, then refine it once later context materially improves it |

**Resolution adopted:** Keep the base graph path-insensitive but ambiguity-aware. Store caller-bound invocations as dispatch sites, not speculative base edges. Let Pass 2 own path-specific binding, and let Pass 1 use only bounded refinement for materially better local documentation.

### Tension 2: `communication_patterns` (System Owner) vs `communication_endpoints` (agent-discovered)

| Doc | Position |
|-----|---------|
| **04 Q4** | Two options: replace `communication_patterns` entirely, or keep both and reconcile |
| **01 Q5** | Auto-detect patterns from code and present for approval |
| **01 Q3** | System Owner provides high-level integration context, not detailed pattern authoring |

**Resolution adopted:** `communication_endpoints` are the canonical provider-side entities. `communication_patterns` remain a supporting normalization/detection layer and review surface.

### Tension 3: One Pass 2 worker vs many

| Doc | Position |
|-----|---------|
| **06 (current plan)** | One worker per system — forced by circular dependency (flows unknown before analysis) |
| **06 Q1 + Q2 (proposed)** | Entry points tagged in Pass 1 → deterministic flow tracing → one worker per flow group |
| **02 Q3** | If orchestrator is non-LLM workflow, it can deterministically group and dispatch |

**Resolution adopted:** Entry points are emitted during Pass 1, the workflow builds path-state traces and flow groups, and Pass 2 dispatches one Worker Agent per flow group.

### Tension 4: Filesystem simplicity vs structured retrieval power

| Doc | Position |
|-----|---------|
| **07 Q1** | Filesystem: simple, git-friendly, LLM-native. Good enough for demo |
| **07 Q2-Q3** | JSONB: structured, queryable, ACID, no new infra. Better for retrieval |
| **08 Q2** | Current LLM-wiki retrieval is fragile; needs semantic search |
| **08 Q3** | RAG + pgvector works naturally with JSONB documents |

**Resolution adopted:** PostgreSQL is the canonical Document store. `content_jsonb` stores structured payloads, `content_markdown` stores deterministic rendered Markdown, and filesystem Markdown is export-only. Section 08 uses database-backed agentic LLM-wiki retrieval with PostgreSQL full-text/metadata search plus graph expansion first; `pgvector` remains an optional upgrade path.

---

## Suggested Resolution Order

Based on the dependency analysis, here's the recommended order to resolve these questions:

1. **05 — CodeGraph During Pass 1** (completed enough for downstream work)
   - Q6: CodeGraph MCP vs direct tree-sitter → determines available tools
   - Q1-Q2: CodeGraph's role in pipeline and as worker tool
   - Q3-Q4: Traversal depth, cross-file resolution, and evidence for dispatch-site handling

2. **03 — Document Graph Design** (completed enough for downstream work)
   - Q1-Q2: Dynamic references, dispatch sites, and RPC mapping (solved by 04 + 05)
   - Q3-Q5: GENERATED_FROM, confidence, new edge types

3. **06 — Pass 2 Scanning** (completed enough for downstream work)
   - Q1: Entry points (depends on 01 + 05)
   - Q2: Invocation chain and dispatch-site binding (depends on 03 + 05)
   - Q3-Q5: Flows, frontend, dispatch (depends on 02 + 03)

4. **07 — Document Storage** (completed enough for downstream work)
   - Q1-Q5: PostgreSQL-first hybrid storage, JSONB structure, Markdown render cache, export-only filesystem

5. **08 — Context Retrieval** (completed enough for downstream work)
   - Q1-Q6: database-backed agentic LLM-wiki retrieval, typed tools, full-text/metadata search, graph expansion, optional embeddings

Sections 01 through 08 are now complete enough that they no longer block downstream implementation planning.

> **Note:** 07 and 08 had a mutual dependency. The resolved pair is: PostgreSQL-first document storage plus database-backed agentic LLM-wiki retrieval.

---

## Summary Statistics

- **Total questions across all docs:** 39
- **Questions with cross-doc dependencies:** 35 (90%)
- **Original questions with no cross-doc dependencies:** 4 (07.Q4 migration complexity was the clearest standalone and is now resolved in section 07)
- **Critical chains:** 5 (Orchestrator, Edge+Endpoint, Storage+Retrieval, CodeGraph→Pass2, PreScan→Pass2)
- **Remaining tensions requiring resolution:** 0 at the scanning-plan design level; implementation details remain for later stories.
- **Documents that should be designed together:** 03+04 (edges+endpoints), 03+05+06 (dispatch sites + CodeGraph + Pass 2 traces), 07+08 (storage+retrieval), 01+05+06 (pre-scan + CodeGraph + Pass 2 pipeline)
