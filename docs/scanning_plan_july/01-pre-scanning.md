# 01 — Pre-Scanning

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Section 01 completed — working direction agreed and ready to use as input for sections 02 and 05.

---

## Original Open Questions

> These were the questions used to drive review. They are kept for traceability; the resolved position is documented below.

1. **Goals of pre-scanning need to be explicitly stated.** What exactly must pre-scanning produce before Pass 1 can begin? Is it just `code_units` rows and a Communication Pattern Catalog snapshot, or is there more?

2. **General strategy — single agent approach.** Current plan has the Orchestrator Agent (LLM) doing pre-scan profiling. Should pre-scanning be done by a single non-LLM agent/tool instead? What questions should it ask the System Owner? The System Owner knows their repo — can we capture that knowledge upfront?

3. **Questions to ask the System Owner (pre-scan interview):**
   - What language(s)/framework(s) does this repo use?
   - What are the entry points? (controllers, queue consumers, scheduled tasks, etc.)
   - What external systems/services does this repo call?
   - What communication patterns are used? (RPC, HTTP, message queues, file-based)
   - Are there any directories/files to exclude from scanning?
   - What are the key business domains or bounded contexts?
   - Any known dead code or legacy sections to skip?

4. **One-shot vs. iterative.** Should the System Owner interview be a single pass, or should it iterate (e.g., "Here's what I found — does this look right?")?

5. **Communication Pattern Catalog population.** Current plan says written by the System Owner. Should pre-scanning attempt to auto-detect patterns from imports/annotations and present candidates for approval?

---

## Extracted Content from Original Plan

### Scan Lifecycle (excerpt)

```
Pre-Scan Profiling
  → CodeGraph parses repos into code_units
  → Scan filter excludes non-code files
  → Communication Pattern Catalog loaded
```

### Pre-Scan Profiling

Before any worker is dispatched, the orchestrator performs reconnaissance over the repository:

1. **CodeGraph parses source files** into `code_units` rows: one per function, method, class, or file-level scope, with exact `line_start`/`line_end` from the AST. No LLM involvement — this is deterministic tree-sitter parsing. `range_confidence` is set to `high` for AST-verified ranges, `medium` for regex-fallback ranges, `low` for heuristic ranges.

2. **Language and framework detection** populates `repositories.language` and identifies which Communication Pattern Catalog entries apply.

3. **Scan filter** excludes build artifacts, generated code, test fixtures, and configuration data from the `code_units` pool. Only filtered code_units are dispatched to workers.

4. **Batch planning:** The orchestrator groups `code_units` into supersteps. Related code_units (caller/callee pairs within the same file, classes in the same module) are batched together so that cross-verification (FR-4.12) can run within the same superstep.

### Document Pre-Allocation

After profiling but before dispatching any workers, the orchestrator bulk-creates `documents` rows for every filtered `code_unit`:

1. **One document row per filtered code_unit:** `doc_type = 'code_logic'`, `status = 'pending'`, `title` derived from `code_unit.symbol_name`, `content_md_path = NULL`, `is_current = true`, `version = 1`.

2. **Tree structure:** All CL Docs have `parent_document_id = NULL` initially. The source-mirroring tree is computed from document `path` strings at display time. No synthetic directory-document rows are created.

3. **Document IDs are now known across the entire scan.** Every worker can create `REFERENCES` edges to any other document in the same repo — the target `document_id` exists in the database and satisfies the FK constraint.

### Relevant Tables

- `systems` — top-level business systems
- `repositories` — git repos belonging to a system, with `scan_status`, `language`, `last_commit_hash`
- `communication_patterns` — per-system configuration of how that system communicates externally
- `code_units` — source code anchors determined by CodeGraph at pre-scan profiling

### Scan Coordinator (Non-LLM Background Service)

A singleton service. **Not an LLM agent** — deterministic application code.

- **Scan queue:** Maintains pending scan requests. One scan per repo at a time.
- **Git operations:** Pulls remote changes. Records commit hashes.
- **Orchestrator lifecycle:** Spawns one Orchestrator Agent per repo scan. Monitors via heartbeat.
- **Concurrency control:** `MaxConcurrentScans` (default 1), `MaxWorkersPerScan` (default 5).

---

## Notes for Revision

- The term "pre-scanning" conflates two distinct phases: (a) System Owner interview/metadata gathering, and (b) deterministic CodeGraph parsing. Should these be separate sections?
- Current plan has the LLM Orchestrator doing profiling. If we move to a non-LLM orchestrator (see [02-agent-orchestrator.md](./02-agent-orchestrator.md)), pre-scan profiling would be entirely deterministic — no LLM involvement needed.
- The `communication_patterns` table is per-system. Should the System Owner interview happen once per system, or once per repo?

---

## Finalized Direction for Section 01

This section now records the agreed working direction for **Pre-Scan Discovery and Review**.

### 1. Use one LLM in Pre-Scan Discovery and Review

The current preferred direction is to use **one LLM agent** in pre-scan rather than a fully non-LLM scanner.

Reasoning:

- A fully non-LLM pre-scan would require many language/framework-specific adapters to detect frameworks, config conventions, communication styles, and repository type.
- That is expensive to implement early and likely too rigid for mixed or unfamiliar repositories.
- A single LLM can read repository structure, local docs, prompts, and codebase instructions, then form a practical repo profile without requiring full framework-specific infrastructure first.

This means the key decision for section 01 is no longer "LLM vs non-LLM everywhere," but rather **how much work the single pre-scan LLM should do before Pass 1 starts**.

### 2. Keep pre-scan scoped and sampled, not exhaustive

Pre-scan should **not** attempt to traverse every corner of the codebase and fully enumerate all endpoints or communication patterns.

Preferred scope:

- read repository structure
- read root and architecture docs when present
- read codebase-specific LLM instructions and prompts when present
- inspect configuration files and dependency manifests
- sample a small number of code files per important directory

The working idea is a **sampled scan**, not a full semantic sweep. A reasonable initial heuristic is:

- all top-level manifests/configs/docs
- all repo-specific AI instructions/prompts/docs relevant to architecture
- up to a handful of representative files per major code directory
- escalation only when sampled evidence shows a special framework or unusual communication mechanism

CodeGraph indexing can still happen during pre-scan even if the LLM does not read the whole repository deeply. In that model:

- **CodeGraph runs early** to build a structural index for the repo
- the pre-scan LLM uses lightweight CodeGraph-derived signals such as file-level symbol density, symbol kinds, and likely architectural roles
- those signals help decide which source files belong in **Tier 1 representative sampling**

This keeps pre-scan practical while still giving the LLM enough signal to infer language, framework, repo type, and likely communication kinds.

### 3. Move entry point discovery to Pass 1

Entry point discovery should **not** be a pre-scan responsibility.

Reasoning:

- entry point detection is closer to code-logic analysis than repo profiling
- it often requires deeper reading than pre-scan should do
- forcing pre-scan to discover all entry points would push it toward full traversal

So pre-scan should only identify **candidate communication kinds and architectural hotspots**, while **Pass 1 discovers concrete entry points** during detailed code analysis.

### 4. System Owner questions should stay high-level

The current feedback is accepted: the System Owner interview should not ask detailed business-logic or code-logic questions. Those belong to Pass 1.

The interview should stay limited to:

- repo role and boundaries
- known exclusions or generated areas
- known external integrations
- naming conventions or domain conventions that help later scanning

### 5. Communication discovery should be split across pre-scan and Pass 1

Pre-scan should **not** try to propagate all patterns/endpoints across the full repo. That is too expensive and too close to full scanning.

Preferred split:

- **Pre-scan:** infer likely communication kinds, candidate integration surfaces, relevant frameworks, and likely endpoint/config locations
- **Pass 1:** confirm concrete endpoints, concrete callers/providers, and concrete communication edges while reading code in detail

This means section 01 should frame pre-scan output as:

- repo profile
- framework/language guess with evidence
- communication-kind hypotheses
- candidate files/directories worth deeper attention in Pass 1
- exclusions and scan hints

not as a complete catalog of endpoints.

### 5A. Pre-scan should also define scan scope

Pre-scan should determine not only repo profile but also the **initial scan scope** for later stages.

That means pre-scan should classify files into four buckets:

1. **Must scan deeply**
  - source files likely to produce Code Logic documents
  - framework entry surfaces
  - communication clients/providers
  - orchestration or startup files

2. **Scan lightly / context only**
  - files useful for understanding architecture but not likely to need standalone document generation
  - examples: dependency registration, shared constants, simple DTOs, mapping files, small support utilities

3. **Reviewable resource files**
  - plain-text non-source resources that may contain business or integration meaning
  - examples: XML, JSON, YAML, DSL-like config, route definitions, workflow specs, schema files

4. **Exclude or low-priority**
  - generated outputs
  - build artifacts
  - vendor folders
  - obviously repetitive boilerplate
  - files with little documentation value

The goal is not just to say "what the repo is," but to say **what later scanning should spend effort on**.

### 5B. Resource-file handling

Pre-scan should explicitly support framework-specific resource files when they are plain text.

Recommended rule:

- if a resource file is plain text and appears architecturally meaningful, pre-scan should try to understand it
- if its meaning affects integration, flow, routing, workflow, or configuration semantics, pre-scan should mark it for later review or contextual use
- if the file appears important but interpretation is uncertain, it should be surfaced in the end-of-phase review packet for human approval

Examples:

- XML-based framework configuration
- JSON workflow/configuration definitions
- routing declarations
- schema or contract files
- DSL-like text resources introduced by specific frameworks

**Binary files are out of scope for now.** If a framework relies on binary resources, pre-scan should record their presence as an unsupported dependency and continue.

### 6. Renamed section

The revised section name is accepted:

**Pre-Scan Discovery and Review**

This better matches the intended job:

- discovery of repo shape and likely technical patterns
- limited human/system-owner review
- preparation for deeper Pass 1 analysis

### 7. Final working position for Section 01

Section 01 should assume:

1. one LLM agent is allowed in pre-scan
2. pre-scan is sampled, not exhaustive
3. entry point discovery moves to Pass 1
4. communication pattern authoring is not manual-first
5. pre-scan proposes candidate communication kinds and hotspots; Pass 1 confirms concrete endpoints

### 8. Sampling strategy for the single LLM pre-scan

The current proposal is a **tiered sampled scan** with escalation rules.

#### Tier 0 — Always read

These files or surfaces should always be read when present:

- repository root directory structure
- solution/workspace manifests
- dependency manifests
- build files
- docker and deployment files
- architecture and README-style docs
- codebase-specific agent instructions, prompts, and AI guidance
- major application configuration files

Purpose:

- detect language and framework with high confidence
- detect repo type
- detect likely runtime model
- detect likely communication mechanisms before reading code deeply

CodeGraph indexing should also be initiated in pre-scan so later sampling and scope decisions can use structural signals rather than filename guesswork alone.

#### Tier 1 — Representative code sampling

For each major source directory, read only a small representative sample first.

Suggested rule:

- sample a few files per major directory
- prefer files whose names suggest architectural role, for example:
  - controllers
  - handlers
  - services
  - routes
  - consumers
  - jobs
  - clients
  - providers
  - startup or program bootstrap files

Tier 1 selection should also use **CodeGraph hints** where available. Useful signals include:

- files with many top-level symbols
- files containing symbols that match common entry or provider shapes
- files that appear central in local call relationships
- files exporting many public symbols
- files referenced by framework bootstrap or configuration surfaces

Selection priority should favor files that most likely reveal:

- entry-point style constructs
- framework annotations or decorators
- dependency injection style
- external client usage
- queue/topic usage
- route declarations

Purpose:

- infer communication kinds
- identify hotspots for Pass 1
- avoid full repo traversal too early

#### Tier 2 — Targeted escalation only

The pre-scan LLM should only widen its read scope when sampled evidence indicates ambiguity or an important hidden mechanism.

Escalation triggers:

- framework still unclear after Tier 0 and Tier 1
- communication mechanism discovered but not understandable from sampled files
- generated client or proxy usage suggests hidden RPC/HTTP integration
- unusual repo layout hides the actual runtime entry surface
- frontend state or routing model is still unclear

Escalation actions:

- read neighboring files in the same hotspot directory
- read the defining configuration or schema file
- read one additional file from each suspicious subdirectory
- stop once the repo profile is good enough for Pass 1 preparation

Tier 2 should also allow escalation into **plain-text resource files** when those files likely carry framework behavior or integration semantics.

#### Explicit anti-goals

Pre-scan should not try to:

- enumerate every endpoint in the repo
- enumerate every entry point in the repo
- build the full call graph
- produce final communication edges
- answer business-logic questions

Pre-scan should also not attempt to interpret binary resource formats in V1.

Those belong to Pass 1 and later stages.

#### Expected output contract from the sampled pre-scan

The single LLM pre-scan should emit:

- repo type guess with evidence
- language/framework guess with evidence
- likely communication kinds with evidence
- initial scan-scope classification for files/directories
- candidate hotspot directories and files for Pass 1
- reviewable plain-text resource files
- exclusions or low-priority areas
- notable uncertainties requiring deeper Pass 1 confirmation

Where pre-scan had to infer meaning from framework-specific plain-text resources, those inferences should be included in the review packet for user confirmation.

This keeps pre-scan cheap enough to run broadly while still useful enough to shape the later scan.

### 9. Resolved decisions summary

The completed decisions for section 01 are:

1. **One bounded LLM is allowed in pre-scan.**
2. **CodeGraph indexing starts in pre-scan** and is reused later.
3. **Pre-scan is sampled, not exhaustive.**
4. **Entry-point discovery belongs to Pass 1, not pre-scan.**
5. **Pre-scan sets scan scope** by classifying files into deep-scan, light-scan, reviewable-resource, and excluded buckets.
6. **Communication discovery is split**: pre-scan finds candidate communication kinds and hotspots, while Pass 1 confirms concrete endpoints and edges.
7. **Plain-text resource files are supported for review/context use** when architecturally meaningful.
8. **Binary files are unsupported in V1** and only recorded as unsupported dependencies.

### 10. What Section 01 hands to later sections

Section 01 now hands the following assumptions to the rest of the plan:

- section 02 can assume a bounded LLM pre-scan already exists
- section 05 can assume CodeGraph indexing already starts before Pass 1
- section 06 can assume entry points are not promised by pre-scan and must be discovered in Pass 1
- section 04 can assume pre-scan provides communication hypotheses, not a full endpoint inventory
