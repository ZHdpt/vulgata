# 02 — Agent Orchestrator Design

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Review pending — extracting from original plan; questions raised below.

---

## TODO — Open Questions from July Review

> These are concerns raised that need to be addressed before finalizing this section.

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
