# Ubiquitous Language

*Extracted from the AnchorDB+ document graph architecture plan and cross-referenced with the existing PRD glossary.*

## Scanning & Agents

| Term | Definition | Aliases to avoid |
|------|------------|------------------|
| **Scan Coordinator** | A non-LLM background service that manages the operational lifecycle of scans: queuing requests, enforcing concurrency limits, triggering Cross-Repository Resolution, and monitoring scan workflow health. | Scan Scheduler, Scan Manager |
| **Orchestrator** | A deterministic per-Repository workflow service that plans scan states, creates jobs, groups supersteps and flow groups, enforces retry/concurrency policy, and dispatches Worker Agents through typed tools. | Orchestrator Agent, Magnet Orchestrator |
| **Worker Agent** | An LLM agent dispatched by the Orchestrator to process a single Code Unit (Pass 1) or synthesize a Business Logic Document for one flow group (Pass 2). | Scanner, Document Generator |
| **Superstep** | A deterministic batch of related Code Units dispatched concurrently to Worker Agents, enabling bounded cross-verification within the batch before the next superstep begins. | Batch, Wave |
| **Pass 1** | The first phase of document generation: Code Logic Documents are produced per Code Unit, with intra-repo REFERENCES edges and cross-repo uncertainties. | CL Doc Generation |
| **Pass 2** | The second phase: deterministic planning traces path-aware graph slices from entry points, clusters them into flow groups, and dispatches one Worker Agent per flow group to synthesize Business Logic Documents from Code Logic Documents and trace evidence. | BL Doc Generation |
| **Document Pre-Allocation** | Creating logical Document slots or planned version rows for filtered Code Units before Worker Agents run, so same-scan references can target stable identities without treating unfinished placeholders as generated Documents. | Pre-seeding |
| **Profiled Revision** | The immutable Git commit identity from which one ScanRun profile and its Pre-Scan Result are derived. Later consumers must use that exact revision rather than whichever commit is currently checked out. | Current Revision, Working-Copy Revision |
| **Scan Context Snapshot** | The immutable Global, System, and Repository advisory context captured once for one Repository Scan Run at one Profiled Revision. It is context for later analysis, not source evidence, and cannot be edited after capture. | Context Bundle, Scan Context |
| **Prepared Scan Manifest** | The immutable, schema-versioned, canonically ordered and fingerprinted result of complete deterministic PreScan for one Repository. It binds the exact source inventory, selection and audit outcomes, File Code Units, analysis-readiness proof, Communication Kind Classification references, and Scan Context Snapshot. | Work Manifest, Scan Queue |
| **File Code Unit** | A `CodeUnitType.File` scheduling unit representing one complete bounded file or one exact safe segment of an oversized file at one Profiled Revision. It is not a method-level Code Unit and does not imply Worker execution or a Document. | File Job, File Chunk |
| **Pre-Scan Result** | The immutable, ScanRun-scoped deterministic result produced after Repository profiling. It records `pre_scan_approved`, confirmed communication kinds, bounded evidence samples, warnings, and integrity metadata without dispatching any Worker Agent. | Pre-Scan Report, Scan Approval |
| **Communication Kind Classification** | A conservative classification of an observed prepared communication kind as Confirmed, Hint, or Warning. It identifies only the kind of communication and does not resolve providers, consumers, endpoints, or Cross-Repository relationships. | Communication Type Detection, Endpoint Detection |
| **Evidence Locator** | A bounded Repository-relative source location plus rule and provenance metadata supporting a Communication Kind Classification. It never contains source content or an absolute path. | Evidence Record, Source Snippet |
| **Pre-Scan Handoff** | The versioned, authorized view of an approved Pre-Scan Result supplied to a downstream scan runner. It is bound to the Profiled Revision and grants no dispatch authority by itself. | Typed Pre-Scan Handoff, Dispatch Handoff |
| **Two-Repository Scan Coordination** | A lightweight persisted operational record that binds exactly two independent Repository Scan lifecycles within one System to one bounded release outcome. It is not a Scan, does not duplicate per-file job state, and does not replace the Scan Coordinator. | System-level Scan, Multi-Repository Scan, Paired Scan |

## Graph & Relationships

| Term | Definition | Aliases to avoid |
|------|------------|------------------|
| **Edge** | A typed, directed relationship between two Document Versions in the graph, usually surfaced through a current-version view of Logical Documents. | Link, Connection |
| **Logical Document** | A stable semantic slot for a Document across scans or refinements, such as "this Code Unit's Code Logic Document" or "this flow group's Business Logic Document." | Mutable Document |
| **Document Version** | An immutable generated snapshot of a Logical Document. Version Chain links preserve history, while retrieval normally reads the current version for an authorized Logical Document. | Revision |
| **REFERENCES Edge** | A dependency edge: one Document Version's content references or depends on another Document Version. Used for both intra-repo and cross-repo relationships. | Dependency Edge, Call Edge |
| **GENERATED_FROM Edge** | A provenance edge from a Business Logic Document Version to the Code Logic Document Version(s) it was synthesized from. Drives two-pass re-scan impact analysis. | Provenance Edge, Derivation Edge |
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
- A **Two-Repository Scan Coordination** binds exactly two independent Repository Scan lifecycles within one **System** without changing either Scan's Repository scope.
- An **Orchestrator** manages one **Scan** and dispatches N **Worker Agents**.
- A **ScanRun** profiles exactly one **Profiled Revision** and produces at most one **Pre-Scan Result** for that revision.
- A **Scan Context Snapshot** captures immutable advisory context for that **ScanRun** and **Profiled Revision**.
- A complete **Prepared Scan Manifest** orders the bounded **File Code Units** selected for one Repository without authorizing Worker Agent dispatch.
- A **Pre-Scan Result** contains zero or more **Communication Kind Classifications**, each supported by bounded **Evidence Locators**.
- An authorized **Pre-Scan Handoff** exposes an approved **Pre-Scan Result** to a downstream runner, which must verify the same **Profiled Revision** before continuing.
- A **Worker Agent** processes one **Code Unit** (Pass 1) or one flow group's Code Logic Documents and trace evidence (Pass 2).
- A **Code Logic Document** is generated from one or more **Code Units** (via `document_sources`).
- A **Business Logic Document** is generated from one or more **Code Logic Documents** (via `GENERATED_FROM` edges).
- A **REFERENCES Edge** connects two **Document Versions** — intra-repo or cross-repo, determined at query time.
- An **Uncertainty** belongs to one **Document** and may resolve to a **Document** in another **Repository**.
- **Cross-Repository Resolution** runs after each **Repository Scan** completes and is bidirectional (resolves both outgoing and incoming uncertainties).

## Example Dialogue

> **Dev:** "The working copy advanced after profiling. Can the scan runner use the approved pre-scan data with the newer commit?"

> **Domain expert:** "No. The **Pre-Scan Result** and **Pre-Scan Handoff** are bound to the original **Profiled Revision**. A downstream runner must acquire and verify that exact commit or fail closed."

> **Dev:** "Does a confirmed HTTP classification mean we have already found the provider, consumer, and endpoint?"

> **Domain expert:** "No. A **Communication Kind Classification** only says that direct evidence confirmed the prepared HTTP kind. Endpoint roles and **Cross-Repository Resolution** happen later, and the handoff itself does not authorize dispatch."

> **Dev:** "When we scan `mbank-core` and it detects a call to `riskeng-service`, what happens if `riskeng-service` hasn't been scanned yet?"

> **Domain expert:** "The Worker Agent creates an **Uncertainty** with `status = 'unresolved'` and `sub_status = 'cross_repo'`. The **REFERENCES Edge** is NOT created — you can't have an edge to a document that doesn't exist. Instead, the uncertainty IS the placeholder. Blazor.Diagrams renders it as a dashed edge to a diamond node."

> **Dev:** "And when `riskeng-service` is scanned later?"

> **Domain expert:** "**Cross-Repository Resolution** runs. It queries all uncertainties targeting `riskeng-service`, normalizes the endpoint identifiers using the **Communication Pattern Catalog**, and matches against the newly-generated documents. If it finds a match, it creates the **REFERENCES Edge** and marks the uncertainty `resolved`. The dashed edge becomes solid."

> **Dev:** "What if both repos have pending uncertainties pointing at each other?"

> **Domain expert:** "That's a **Deadlock**. The **Deadlock-Breaking Protocol** kicks in: the repo with fewer pending uncertainties resolves first. If tied, the older scan yields. Once the first side resolves, the second side may now have matches."

> **Dev:** "And if a Business Logic Document was generated before the cross-repo target was scanned?"

> **Domain expert:** "Known V1 limitation. The BL Doc body may say 'unscanned.' The Chat Agent may use the now-resolved Cross-Repository edge at query time, but it must mark the BL Doc as generated before that resolution and cite both the stale BL Doc and the newer target evidence. Full BL Doc regeneration on Cross-Repository Resolution is V2."

## Flagged Ambiguities

- "Cross-System" was used throughout the original PRD to mean "Cross-Repository." The architecture plan has renamed this to **Cross-Repository** because detection granularity is at the repository level, and intra-System cross-repo calls were invisible under the old terminology. "System" is now purely an organizational grouping.
- "Scan" previously meant "either a single repository or an entire system." Now it always means "a single repository." There is no System-level scan — scanning all repos in a System is a convenience trigger that queues N independent repo scans.
- "Uncertainty" previously had 3 user-facing states. The architecture plan adds `sub_status` for internal refinement and a `wontfix` terminal state for human-accepted dangling links.
- A **Communication Kind Classification** is a pre-scan hypothesis about prepared RPC or HTTP communication. It is not an endpoint, provider/consumer role, REFERENCES Edge, or completed Cross-Repository detection.
- A **Pre-Scan Handoff** means that deterministic pre-scan checks were approved for a specific Profiled Revision. It does not authorize or perform Worker Agent dispatch.
