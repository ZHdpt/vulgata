# 02 — Agent Orchestrator Design

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Section 02 completed — workflow direction agreed and ready to use as input for sections 03, 05, and 06.

---

## Original Open Questions

> These were the questions used to drive review. They are kept for traceability; the resolved position is documented below.

1. **Current plan: "Magnet Orchestrator" pattern.** The Orchestrator Agent is an LLM agent that makes decisions about superstep grouping, dispatch ordering, cross-verification coordination, and error handling. Is an LLM really needed for this? The LLM is slower, more expensive, less predictable, and harder to monitor than deterministic code.

2. **Alternative: Non-LLM orchestrator with workflow.** If the orchestrator is deterministic workflow code (like the Scan Coordinator), then:
   - **Agent job dispatch becomes tool-based.** Agents don't need to know orchestration strategy. They receive a job from a queue via a tool call: `get_next_job(repo_id)` returns a `code_unit` + context. They process it and call `submit_document(job_id, result)`.
   - **Monitoring is simpler.** A workflow orchestration can expose progress as database rows. No heartbeats needed for the orchestrator itself — only for workers.
   - **Retry/error handling is deterministic.** The workflow engine decides retry policy, not the LLM.

3. **What should the (LLM) agents actually do vs. what should the (non-LLM) orchestrator do?**

   | Responsibility | Current (Magnet) | Proposed (Workflow) |
   |---------------|------------------|---------------------|
   | Parse code into code_units | Orchestrator (LLM) | CodeGraph (deterministic) |
   | Group into supersteps | Orchestrator (LLM) | Deterministic algorithm (call-graph analysis) |
   | Dispatch workers | Orchestrator (LLM) | Workflow engine |
   | Generate document body | Worker Agent (LLM) | Worker Agent (LLM) — unchanged |
   | Cross-verification | Orchestrator (LLM) coordinates | Workflow provides related drafts; Worker Agent includes verification in output |
   | Error handling | Orchestrator (LLM) retries | Workflow engine retries |
   | Progress tracking | Orchestrator (LLM) updates DB | Workflow engine updates DB |
   | Post-scan rebuilds | Orchestrator (LLM) triggers | Workflow engine triggers |

4. **Tool-based agent interface.** If workers get jobs via tools, the worker agent's tool set could be:
   - `claim_code_unit(repo_id)` → returns next pending code_unit + source text + context
   - `submit_document(code_unit_id, markdown_body, edges, sources, uncertainties)` → writes to DB, returns success/failure
   - `get_related_drafts(code_unit_id)` → returns drafts from same superstep for cross-verification
   - `report_error(code_unit_id, error_details)` → marks document as failed

   The workflow engine handles: queue ordering, superstep grouping, concurrency limits, retry, and post-scan steps.

5. **How does the orchestrator know which code_units are "related" for superstep grouping?** Current plan: the LLM orchestrator decides. Alternative: use CodeGraph's call-graph data — if `code_unit A` calls `code_unit B` (or vice versa), they're in the same superstep. Deterministic, reproducible, no LLM needed.

---

## Extracted Content from Original Plan

### Architecture Components

The scanning system has two layers: a non-LLM Scan Coordinator that manages the operational lifecycle, and per-repo LLM-powered Orchestrator Agents that run scans.

### Scan Coordinator (Non-LLM Background Service)

A singleton service in the ASP.NET Core application. **Not an LLM agent** — it is deterministic application code.

**Responsibilities:**
- **Scan queue:** Maintains a queue of pending scan requests (manual triggers, git webhooks, scheduled). Ensures only one scan runs per repo at a time via `repositories.scan_status` check.
- **Git operations:** Pulls remote changes before scan start. Records `base_commit_hash` and `target_commit_hash` on the `scans` row.
- **Orchestrator lifecycle:** Spawns one Orchestrator Agent per repo scan. Monitors orchestrator health via heartbeat (`scan_agent_logs.last_heartbeat_at`). If the orchestrator stalls (no heartbeat for 5 minutes), marks the scan as `failed` and releases the repo lock.
- **Cross-repo resolution trigger:** When a repo scan completes, notifies the resolution engine to process pending `unresolved/cross_repo` uncertainties targeting that repo.
- **Concurrency control:** Configurable limits on concurrent scans (`MaxConcurrentScans`, default 1 for V1) and workers per scan (`MaxWorkersPerScan`, default 5).

### Orchestrator Agent (LLM Agent, Per-Repo)

An LLM-powered agent spawned by the Scan Coordinator for each repo scan. Uses the Microsoft Agent Framework.

**Responsibilities:**
- **Pre-Scan Profiling:** Runs CodeGraph on the repo, produces `code_units` rows, applies scan filter.
- **Document Pre-Allocation:** Bulk-creates `documents` rows for all code_units before dispatching workers.
- **Superstep dispatch:** Groups related code_units into supersteps, dispatches Worker Agents concurrently, fans in results.
- **Cross-verification coordination:** Provides workers with related drafts within the same superstep.
- **Progress tracking:** Updates `scan_agent_logs` with current status, heartbeat timestamps.
- **Error handling:** Retries failed workers once, marks documents as `failed` on second failure, continues scan.
- **Post-scan:** Triggers reverse_index rebuild, index_entries regeneration, index.md write, cross_repo_stale_notices.

### Worker Agent (LLM Agent)

Dispatched by the Orchestrator to process a single code_unit (Pass 1) or a set of CL Docs (Pass 2).

### Concurrency Model

| Limit | V1 Default | Mechanism |
|-------|-----------|-----------|
| Concurrent scans (global) | 1 | Scan Coordinator queue + `repositories.scan_status` guard |
| Workers per scan | 5 | Orchestrator superstep fan-out limit |
| PostgreSQL writers | Unlimited (MVCC) | Multi-Version Concurrency Control — no write serialization |
| PostgreSQL readers during scan | Unlimited (MVCC) | Concurrent reads never blocked by writes |

---

## Notes for Revision

- The "magnet orchestrator" term refers to the LLM orchestrator acting as a central coordinator that "attracts" decisions. A workflow-based approach would invert this: agents are autonomous workers that pull jobs from a queue.
- If the orchestrator becomes deterministic, the Scan Coordinator and Orchestrator could potentially merge into a single service. The distinction (Coordinator = cross-repo lifecycle, Orchestrator = per-repo scan management) may be unnecessary if both are non-LLM.
- Tool-based agent interfaces align with the Microsoft Agent Framework pattern where agents use tools to interact with external systems.

---

## Finalized Direction for Section 02

This section defines the detailed workflow design for scanning orchestration.

### 1. Core decision

The orchestrator should be a **deterministic workflow service**, not an LLM coordinator.

In this document, **workflow** means the logical scan state machine and job-orchestration model. It does **not** require a specific product or framework.

That means:

- it can be implemented as ordinary application workflow code
- it can be implemented on top of a durable job runner
- it can be implemented using MAF workflow primitives if those primitives are a good fit

But the design intent is architectural, not product-specific. The plan should not depend on Microsoft Agent Framework workflow features unless they are later chosen as the implementation vehicle.

Section 01 already established that one bounded LLM is allowed in **Pre-Scan Discovery and Review**. Section 02 therefore does **not** decide whether LLMs exist at all. It decides whether runtime scan orchestration is managed by:

- an LLM central dispatcher, or
- deterministic workflow code with LLM workers

The chosen direction is:

- **deterministic workflow for orchestration**
- **LLM agents for document analysis and generation**
- **CodeGraph as the structural substrate used before and during scanning**

### 2. Design goals for the workflow

The workflow must provide:

1. reproducible task dispatch
2. observable scan progress
3. explicit retry and failure handling
4. bounded concurrency
5. resumability after partial failure
6. typed interfaces between orchestrator and LLM workers
7. reuse of pre-scan and CodeGraph artifacts

### 3. Workflow actors

The workflow has four actors.

#### 3.1 Scan Coordinator

The top-level non-LLM service responsible for:

- accepting scan requests
- enforcing one active scan per repo
- creating the scan record and lifecycle state
- invoking pre-scan
- starting the repo workflow
- handling cancellation and global concurrency

#### 3.2 Repo Workflow Orchestrator

A deterministic per-repo workflow engine responsible for:

- converting pre-scan output into runnable jobs
- grouping work into stages and supersteps
- dispatching jobs to workers
- applying retries and failure rules
- collecting outputs
- triggering post-scan rebuilds and resolution tasks

This can be implemented as:

- a background-service state machine
- a durable workflow engine
- or a database-backed job runner

The important point is the behavior, not the specific framework.

#### 3.3 LLM Scan Workers

LLM agents that do analysis work only:

- Pass 1 code-unit analysis
- Pass 2 flow-level synthesis
- targeted ambiguity resolution when explicitly dispatched

They do **not** own queueing, retries, or scheduling.

#### 3.4 Structural Services

Non-LLM supporting services used by the workflow:

- CodeGraph index/query service
- source file/content service
- document persistence service
- graph persistence service
- scan logging/progress service

### 4. Workflow states

The scan workflow should be modeled as an explicit state machine.

Recommended states:

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

### 5. Workflow steps

#### Step 1 — Scan request accepted

Input:

- repo identifier
- trigger type (`manual`, `git_webhook`, `scheduled`)

Actions:

- create `scans` row
- verify no conflicting active scan exists for the repo
- move workflow to `created`

#### Step 2 — Pre-scan starts

Actions:

- move workflow to `pre_scan_running`
- run or refresh CodeGraph index
- invoke the bounded pre-scan LLM
- persist:
   - repo profile
   - scope buckets
   - hotspot files/directories
   - communication-kind hypotheses
   - reviewable resource files
   - unsupported binary resource notices

Output:

- pre-scan report
- structural index availability
- initial scan scope

#### Step 3 — Pre-scan review gate

Actions:

- if no reviewable ambiguities exist, auto-approve
- if human review is required, move to `pre_scan_review_pending`
- present a review packet containing:
   - exclusions
   - reviewable plain-text resources
   - uncertain framework/config interpretations
   - important communication-kind hypotheses

Transition:

- on approval → `pre_scan_approved`
- on rejection or blocking ambiguity → `pre_scan_failed` or rerun pre-scan with overrides

#### Step 4 — Pass 1 planning

Actions:

- move workflow to `pass1_planning`
- create `code_units` workset from pre-scan scope and CodeGraph output
- exclude files in low-priority/excluded buckets from standalone doc generation
- pre-allocate document records for selected code units
- compute deterministic supersteps

Superstep grouping inputs:

- same-file proximity
- same-module/folder locality
- direct caller/callee relationships from CodeGraph
- bootstrap/config references
- communication provider/client surfaces
- pre-scan hotspot markers

Output:

- ordered list of Pass 1 job groups

#### Step 5 — Pass 1 execution

Actions:

- move workflow to `pass1_running`
- dispatch jobs from the current superstep up to concurrency limit
- workers claim jobs through orchestrator tools
- workers read assigned code and context
- workers submit documents, edges, uncertainties, and metadata
- workers may also emit **deferred enrichment dependencies** when their document quality depends on another code unit being scanned first or later

Failure policy:

- retry once on transient failure
- mark job failed on second failure
- continue workflow unless failure threshold is exceeded

Deferred enrichment policy:

- Pass 1 should allow **bounded back-propagation of context**
- if job `A` cannot fully explain an important behavior until job `B` is scanned, `A` should not immediately trigger an unrestricted rescan loop
- instead, `A` records a deferred enrichment dependency on `B`
- after the relevant superstep or after Pass 1 initial completion, the orchestrator evaluates whether `B`'s output materially improves `A`
- if yes, the orchestrator creates a **refinement job** for `A`

This means Pass 1 has two logical phases:

1. **initial local analysis wave**
2. **bounded refinement wave** for documents whose pending dependencies were satisfied later

Refinement should be limited by policy, for example:

- at most one or two revisit waves per scan
- only for materially useful enrichments
- no infinite ping-pong between mutually referencing units

Examples of material enrichment:

- `A` calls `B`, and `B`'s finished document clarifies a major downstream effect that materially improves `A`'s Code Logic document
- a dispatch site in `A` can only be bound after a later-scanned provider or callee document becomes available
- a framework resource or injected implementation was unresolved when `A` ran but becomes bounded later in Pass 1

Transition:

- if all jobs succeed and no refinement jobs remain → `pass1_completed`
- if initial jobs succeed but refinement jobs remain → continue Pass 1 until refinement wave completes
- if some jobs fail but threshold not exceeded → `pass1_partial_failed` then continue
- if threshold exceeded → `failed`

#### Step 6 — Pass 2 planning

Actions:

- move workflow to `pass2_planning`
- collect Pass 1 outputs
- ensure bounded Pass 1 refinement has completed or been explicitly cut off
- discover entry points from Pass 1 metadata
- build flow candidates by traversing REFERENCES edges from entry points and carrying dispatch-site state
- cluster overlapping traces into flow groups
- create Pass 2 synthesis jobs

Output:

- ordered flow-subgraph job list

#### Step 7 — Pass 2 execution

Actions:

- move workflow to `pass2_running`
- dispatch one worker per flow group within concurrency limits
- workers synthesize Business Logic docs from subgraphs
- workers submit BL docs and GENERATED_FROM edges

Failure policy:

- same as Pass 1

Transition:

- success → `post_scan_running`
- partial failure → `pass2_partial_failed` then continue if acceptable
- systemic failure → `failed`

#### Step 8 — Post-scan rebuilds

Actions:

- rebuild reverse index
- regenerate index entries
- write or export document views if needed
- post stale notices
- update scan counters and completion metadata

Transition:

- move to `cross_repo_resolution_running`

#### Step 9 — Cross-repo resolution

Actions:

- resolve pending cross-repo uncertainties that target repos already scanned
- create resolved REFERENCES edges where possible
- leave unresolved items in tracked state when not resolvable

Transition:

- move to `completed`

### 6. Workflow edges and transitions

Key workflow edges:

- `created -> pre_scan_running`
- `pre_scan_running -> pre_scan_review_pending`
- `pre_scan_running -> pre_scan_approved`
- `pre_scan_review_pending -> pre_scan_approved`
- `pre_scan_review_pending -> pre_scan_failed`
- `pre_scan_approved -> pass1_planning`
- `pass1_planning -> pass1_running`
- `pass1_running -> pass1_running` (bounded refinement wave)
- `pass1_running -> pass1_completed`
- `pass1_running -> pass1_partial_failed`
- `pass1_partial_failed -> pass2_planning`
- `pass1_completed -> pass2_planning`
- `pass2_planning -> pass2_running`
- `pass2_running -> post_scan_running`
- `pass2_running -> pass2_partial_failed`
- `pass2_partial_failed -> post_scan_running`
- `post_scan_running -> cross_repo_resolution_running`
- `cross_repo_resolution_running -> completed`
- any active state -> `cancelled`
- unrecoverable state -> `failed`

### 7. Orchestrator-provided tools for LLM workers

LLM workers should interact with the workflow only through typed tools.

#### 7.1 Job acquisition tools

- `claim_job(scan_id, worker_kind)`
   - claims the next eligible job for a worker
   - worker kinds: `pass1_code_unit`, `pass2_flow`, `ambiguity_review`

- `get_job_context(job_id)`
   - returns the job payload and all orchestrator-prepared context

#### 7.2 Code and structure reading tools

- `get_source_slice(code_unit_id)`
   - returns exact source text for the code unit range

- `get_source_file(file_path)`
   - returns full file content when needed for local context

- `get_neighboring_code_units(code_unit_id)`
   - returns structurally nearby symbols in the same file or module

- `get_codegraph_context(code_unit_id)`
   - returns symbol metadata, callers, callees, imports, implementations, and related structural hints

- `get_dispatch_sites(code_unit_id)`
   - returns abstract invocation sites discovered for the code unit

- `get_binding_context(job_id, dispatch_site_id)`
   - returns caller-path, config, or DI evidence that may resolve the dispatch site for this job

- `get_related_resource_files(job_id)`
   - returns relevant plain-text config/resources identified during pre-scan

#### 7.3 Collaboration and cross-verification tools

- `get_related_outputs(job_id)`
   - returns completed outputs from the same superstep or same flow cluster

- `get_existing_document(document_id)`
   - returns the current document if regenerating or cross-checking

#### 7.4 Submission tools

- `submit_pass1_result(job_id, document, document_sources, edges, uncertainties, metadata)`

- `submit_pass1_result_with_dependencies(job_id, document, document_sources, edges, uncertainties, metadata, deferred_dependencies)`
   - same as `submit_pass1_result`, but allows the worker to declare that this document should be reconsidered if specific later job outputs become available

- `submit_pass2_result(job_id, documents, generated_from_edges, references_edges, metadata)`

- `report_job_failure(job_id, error_type, details)`

#### 7.5 Workflow utility tools

- `heartbeat(job_id)`
   - optional worker liveness update

- `request_additional_context(job_id, reason)`
   - asks the orchestrator to expand context for the job in a bounded way

- `request_refinement_dependency(job_id, depends_on_code_unit_id, reason)`
   - records that this job can complete provisionally now, but should be eligible for one later refinement if the dependent code unit yields materially useful context

### 8. How workflow dispatches scanning tasks to agents

Task dispatch should be **pull-based** at the worker boundary.

That means:

1. orchestrator creates jobs in the database or durable queue
2. workers ask for work via `claim_job`
3. orchestrator returns only jobs that are:
    - in the active stage
    - unlocked
    - dependency-satisfied
    - within concurrency rules
4. once claimed, the job is locked to that worker for a lease period
5. if the worker does not submit or heartbeat before lease expiry, the orchestrator requeues the job

This model is better than push-dispatch because it simplifies:

- worker scaling
- retries
- lease expiration
- mixed worker availability

### 8A. What happens when an agent sees references outside its job?

At the start of scanning, a worker will often see references to code outside its assigned code unit. That is expected.

The worker should follow this rule:

1. **Read the assigned code unit first.**
2. **Use bounded structural expansion** through orchestrator tools.
3. **Do not freely crawl the repository.**

Allowed expansion outside the assigned job:

- direct callers/callees from CodeGraph
- immediate implementations/overrides where needed
- neighboring code units in the same file or module
- relevant plain-text resource/config files already attached by the orchestrator

Not allowed as default behavior:

- recursively chasing long invocation chains across the repo
- reading arbitrary unrelated files without structural justification
- silently expanding job scope until the worker is effectively doing multi-unit analysis

The reason is that Pass 1 jobs are meant to produce stable, local Code Logic documents. Flow-wide tracing belongs mainly to later deterministic grouping and Pass 2 synthesis.

### 8B. What if the agent cannot get enough information?

If the worker cannot confidently produce a document from the assigned code unit and bounded context, it should not guess.

It should take one of three actions:

1. **Request bounded additional context**
   - use `request_additional_context(job_id, reason)`
   - the orchestrator may attach:
     - one more ring of CodeGraph context
     - one more related source file
     - one more relevant resource/config file

2. **Produce the document with explicit uncertainty**
   - generate the document for what is known
   - emit `uncertainties` rows for what cannot be resolved
   - lower confidence for ambiguous edges

3. **Escalate as analysis-blocked**
   - use `report_job_failure(job_id, error_type, details)` only when the worker truly cannot produce a minimally useful result
   - examples:
     - the code unit is only a thin proxy with no readable target
     - required semantics are hidden in unsupported binary resources
     - required framework behavior cannot be understood from bounded context

The default preference is:

- **request bounded context first**
- **emit explicit uncertainty second**
- **hard-fail only when analysis is genuinely blocked**

### 9. How agents read the code

Agents should not begin by crawling the repository freely. They should read code through an orchestrator-prepared context packet.

#### 9.1 Pass 1 code-reading model

For a Pass 1 job, the worker receives:

- the target `code_unit_id`
- exact source slice
- file path and symbol name
- neighbor symbols in the same file
- CodeGraph context:
   - callers
   - callees
   - imports
   - implementations/overrides where available
- relevant plain-text resource files
- pre-scan repo hints

Recommended reading sequence for the worker:

1. read the exact source slice
2. read local file context if needed
3. inspect CodeGraph structural context
4. inspect relevant resource/config files
5. only then form the semantic interpretation and output

If references outside the job remain important after this sequence, the worker should request bounded additional context rather than continue wandering outward.

This prevents uncontrolled repo wandering while still allowing targeted expansion.

#### 9.2 Pass 2 code-reading model

For a Pass 2 job, the worker should not read raw repository files first. It should begin from the already-built graph slice.

The worker receives:

- a set of CL documents in the flow group
- the flow subgraph
- entry point markers
- relevant cross-repo uncertainties or resource files

The Pass 2 worker reads:

1. entry-point documents
2. referenced CL docs in traversal order
3. supporting resource/config documents if relevant
4. related outputs from overlapping flow clusters if needed

Pass 2 is therefore **graph-first**, not file-first.

### 10. Deterministic vs LLM responsibilities

The split should be explicit.

#### Deterministic responsibilities

- scan state machine
- queueing and leasing
- retries and failure thresholds
- CodeGraph indexing
- scope filtering
- code unit selection
- document pre-allocation
- superstep grouping
- flow-cluster construction
- post-scan rebuilds
- cross-repo resolution execution

#### LLM responsibilities

- interpret source meaning
- explain control flow and business meaning
- identify semantically important references
- propose uncertainties
- synthesize business-level narratives
- cross-check peer outputs when requested

### 11. V1 simplifications

To keep V1 realistic, use the following simplifications:

1. one active scan per repo
2. bounded worker concurrency
3. simple deterministic superstep rules
4. one retry per failed job
5. no binary-resource interpretation
6. no dynamic runtime tracing
7. no adaptive global optimization of job ordering

### 11A. HITL in Pass 1

HITL should exist in Pass 1, but only as a **bounded exception path**, not as a normal part of every job.

Recommended rule:

- Pass 1 should normally complete autonomously.
- HITL should be triggered only for blocking or high-risk ambiguity.

Good HITL cases in Pass 1:

- a plain-text framework resource appears important but the interpretation is uncertain
- multiple plausible targets exist for a critical reference and the difference materially affects the document
- the worker finds unsupported binary dependencies that block correct explanation
- a generated proxy/client hides behavior that cannot be resolved through CodeGraph plus bounded context

Poor HITL cases in Pass 1:

- ordinary missing details that can be represented as uncertainty
- routine low-confidence references
- cases where Pass 2 can later resolve business meaning from graph context

So HITL is allowed, but it should be rare, explicit, and routed through review packets or blocking-ambiguity queues rather than interrupting every worker flow.

### 12. What section 02 hands to later sections

Section 02 now hands these assumptions forward:

- section 05 can assume workers consume CodeGraph through orchestrator tools
- section 03 can assume edges are proposed by workers but staged within a deterministic workflow
- section 06 can assume Pass 2 jobs are built from entry-point-derived flow groups, not from one monolithic system prompt
- section 04 can assume endpoint discovery and submission happen through typed worker result contracts

### 13. Remaining non-blocking implementation choices

Section 02 does not appear to have remaining **blocking design questions**. The remaining choices are implementation-level and can be decided later without reopening the orchestration model.

Examples:

1. whether the workflow runtime is implemented as:
   - background-service state machine
   - durable workflow engine
   - database-backed job runner

2. exact lease duration and heartbeat policy for claimed jobs

3. exact failure threshold values for allowing partial completion vs terminating the scan

4. exact concurrency defaults per worker kind in V1

5. whether the Scan Coordinator and Repo Workflow Orchestrator remain separate components in code or are merged into one service

These are important, but they do not change the agreed direction:

- deterministic orchestration
- pull-based typed job dispatch
- bounded LLM worker responsibilities
- graph-first and context-packet-based scanning behavior

### 14. Resolved decisions summary

The completed decisions for section 02 are:

1. orchestration is deterministic, not LLM-led
2. workflow means the scan state machine and job model, not a specific product dependency
3. dispatch is pull-based through typed orchestrator tools
4. workers do analysis only and do not own scheduling or retries
5. workers read code through bounded orchestrator-prepared context packets
6. off-job references are handled through bounded expansion, uncertainty emission, or explicit escalation
7. HITL exists only as a bounded exception path in Pass 1
8. Pass 2 jobs are built from deterministic flow-group planning, not one monolithic system prompt
