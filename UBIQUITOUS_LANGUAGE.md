# Ubiquitous Language

*Extracted from the AnchorDB+ document graph architecture plan and cross-referenced with the existing PRD glossary.*

## Scanning & Agents

| Term | Definition | Aliases to avoid |
|------|------------|------------------|
| **Scan Coordinator** | A non-LLM background service that manages the operational lifecycle of scans: queuing requests, enforcing concurrency limits, triggering cross-repo resolution, and monitoring orchestrator health. | Scan Scheduler, Scan Manager |
| **Orchestrator Agent** | An LLM-powered agent scoped to a single repository scan. Dispatches Worker Agents in superstep batches, coordinates cross-verification, and triggers post-scan steps. | Scan Orchestrator |
| **Worker Agent** | An LLM agent dispatched by the Orchestrator to process a single Code Unit (Pass 1) or synthesize Business Logic Documents from Code Logic Documents (Pass 2). | Scanner, Document Generator |
| **Superstep** | A batch of related Code Units dispatched concurrently to Worker Agents, enabling cross-verification within the batch before the next superstep begins. | Batch, Wave |
| **Pass 1** | The first phase of document generation: Code Logic Documents are produced per Code Unit, with intra-repo REFERENCES edges and cross-repo uncertainties. | CL Doc Generation |
| **Pass 2** | The second phase: one Worker per System synthesizes Business Logic Documents from the completed Code Logic Documents, identifying business flows autonomously. | BL Doc Generation |
| **Document Pre-Allocation** | Bulk-creating `documents` rows for every filtered Code Unit before any Worker runs, so all document IDs are known and intra-repo REFERENCES edges can be created immediately. | Pre-seeding |

## Graph & Relationships

| Term | Definition | Aliases to avoid |
|------|------------|------------------|
| **Edge** | A typed, directed relationship between two Documents in the graph. | Link, Connection |
| **REFERENCES Edge** | A doc→doc dependency edge: Document A's content references or depends on Document B. Used for both intra-repo and cross-repo relationships. | Dependency Edge, Call Edge |
| **GENERATED_FROM Edge** | A provenance edge from a Business Logic Document to the Code Logic Document(s) it was synthesized from. Drives two-pass re-scan impact analysis. | Provenance Edge, Derivation Edge |
| **Cross-Repo Edge** | Not a separate edge type — any REFERENCES edge where `source.repo_id != target.repo_id` is cross-repo. Discovered at query time, not stored as a schema concept. | Cross-System Edge (deprecated) |
| **Dangling Link** | A cross-repo uncertainty that never resolves — a known unknown. Recorded with available identifying information; does not block scan completion. | Orphan Link, Unresolved Reference |
| **Version Chain** | A linked list of document versions via `superseded_by_id`, preserving full history. Old versions are never deleted. | Document History, Revision Chain |

## Uncertainty & Resolution

| Term | Definition | Aliases to avoid |
|------|------------|------------------|
| **Cross-Repository Resolution** | Deterministic post-processing that runs after a repository scan completes: matches unresolved cross-repo uncertainties against the newly-scanned repo's documents and creates REFERENCES edges. | Cross-System Resolution (deprecated), Link Resolution |
| **Deadlock-Breaking Protocol** | When two repos have mutual pending cross-repo uncertainties, the repo with fewer pending resolves first; if tied, the older scan yields. | Circular Dependency Resolution |
| **Uncertainty sub_status** | Internal refinement of the `unresolved` state: `cross_repo` (target repo known, waiting for scan), `unknown_target` (no repo identified), `ambiguous` (multiple possible targets). | Uncertainty subtype |

## Re-Scan & Impact

| Term | Definition | Aliases to avoid |
|------|------------|------------------|
| **Impact Analysis** | A 4-step SQL cascade that finds all Documents affected by a set of changed source files: direct impact → intra-repo transitive closure → cross-repo reachability → uncertainty reopening. | Blast Radius, Change Propagation |
| **Two-Phase Change Detection** | Phase 1 (line-overlap filter via reverse_index) eliminates most candidates; Phase 2 (hash comparison) verifies the remaining candidates. Fast AND correct. | Sub-File Change Detection |
| **Source Anchoring** | Three-layer traceability: CodeGraph parses source into Code Units (structural), document_sources links Documents to Code Units (semantic), reverse_index provides O(1) line→document lookup (fast). | Provenance, Traceability |
| **File Identity Hash** | SHA-256 of the first 4096 bytes of a source file, used to detect renames — if a file disappears and another appears with the same hash, it's a rename, not a delete+create. | File Fingerprint |
| **Cross-Repo Stale Notice** | An informational record posted when a document changes, notifying other repos that link to it. No automatic cascade — each repo's owner decides when to re-scan. | Stale Notification |

## Resource Management

| Term | Definition | Aliases to avoid |
|------|------------|------------------|
| **Concurrency Control** | Configurable limits on concurrent scans (global) and workers per scan, enforced by the Scan Coordinator. Prevents resource exhaustion. | Rate Limiting, Throttling |

## Relationships

- A **System** contains one or more **Repositories** (organizational grouping only — no detection significance).
- A **Scan** targets exactly one **Repository**.
- An **Orchestrator Agent** manages one **Scan** and dispatches N **Worker Agents**.
- A **Worker Agent** processes one **Code Unit** (Pass 1) or one **System's** CL Docs (Pass 2).
- A **Code Logic Document** is generated from one or more **Code Units** (via `document_sources`).
- A **Business Logic Document** is generated from one or more **Code Logic Documents** (via `GENERATED_FROM` edges).
- A **REFERENCES Edge** connects two **Documents** — intra-repo or cross-repo, determined at query time.
- An **Uncertainty** belongs to one **Document** and may resolve to a **Document** in another **Repository**.
- **Cross-Repository Resolution** runs after each **Repository Scan** completes and is bidirectional (resolves both outgoing and incoming uncertainties).

## Example Dialogue

> **Dev:** "When we scan `mbank-core` and it detects a call to `riskeng-service`, what happens if `riskeng-service` hasn't been scanned yet?"

> **Domain expert:** "The Worker Agent creates an **Uncertainty** with `status = 'unresolved'` and `sub_status = 'cross_repo'`. The **REFERENCES Edge** is NOT created — you can't have an edge to a document that doesn't exist. Instead, the uncertainty IS the placeholder. Blazor.Diagrams renders it as a dashed edge to a diamond node."

> **Dev:** "And when `riskeng-service` is scanned later?"

> **Domain expert:** "**Cross-Repository Resolution** runs. It queries all uncertainties targeting `riskeng-service`, normalizes the endpoint identifiers using the **Communication Pattern Catalog**, and matches against the newly-generated documents. If it finds a match, it creates the **REFERENCES Edge** and marks the uncertainty `resolved`. The dashed edge becomes solid."

> **Dev:** "What if both repos have pending uncertainties pointing at each other?"

> **Domain expert:** "That's a **Deadlock**. The **Deadlock-Breaking Protocol** kicks in: the repo with fewer pending uncertainties resolves first. If tied, the older scan yields. Once the first side resolves, the second side may now have matches."

> **Dev:** "And if a Business Logic Document was generated before the cross-repo target was scanned?"

> **Domain expert:** "Known V1 limitation. The BL Doc body may say 'unscanned.' The chat agent compensates at query time by following the now-resolved cross-repo edge. Full BL Doc regeneration on cross-repo resolution is V2."

## Flagged Ambiguities

- "Cross-System" was used throughout the original PRD to mean "Cross-Repository." The architecture plan has renamed this to **Cross-Repository** because detection granularity is at the repository level, and intra-System cross-repo calls were invisible under the old terminology. "System" is now purely an organizational grouping.
- "Scan" previously meant "either a single repository or an entire system." Now it always means "a single repository." There is no System-level scan — scanning all repos in a System is a convenience trigger that queues N independent repo scans.
- "Uncertainty" previously had 3 user-facing states. The architecture plan adds `sub_status` for internal refinement and a `wontfix` terminal state for human-accepted dangling links.
