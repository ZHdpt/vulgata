---
stepsCompleted: [1, 2, 3]
inputDocuments:
  - brief-vulgata-2026-06-11/brief.md
session_topic: 'Gaps, blind spots, and unaddressed areas in the Vulgata product brief'
session_goals: 'Identify missing features, risks & failure modes, and technical blind spots'
selected_approach: 'progressive-flow'
techniques_used:
  - 'What If Scenarios'
  - 'Mind Mapping'
  - 'Five Whys'
  - 'Decision Tree Mapping'
ideas_generated: 18
context_file: g:\source\repos\vulgata\docs/bmad\planning-artifacts\briefs\brief-vulgata-2026-06-11\brief.md
---

# Brainstorming Session Results

**Facilitator:** zhdpt
**Date:** 2026-06-12

## Session Overview

**Topic:** Gaps, blind spots, and unaddressed areas in the Vulgata product brief
**Goals:** Identify missing features, risks & failure modes, and technical blind spots

### Context Guidance

Loaded the Vulgata Product Brief (brief-vulgata-2026-06-11/brief.md) as context. The brief covers: multi-agent scanning architecture, cross-system uncertainty resolution, document classification (code logic vs. business logic), git monitoring for incremental re-scans, human-in-the-loop ambiguity resolution, third-party library handling, MCP integration, and a Blazor web dashboard with chat interface. Competition demo target: August 15.

### Session Setup

Brainstorming focus on three lenses: missing features, risks & failure modes, and technical blind spots. Established via conversation with the user (zhdpt).

## Technique Selection

**Approach:** Progressive Technique Flow
**Journey Design:** Systematic development from exploration to action

**Progressive Techniques:**

- **Phase 1 - Exploration:** What If Scenarios for maximum idea generation across missing features, risks, and technical blind spots
- **Phase 2 - Pattern Recognition:** Mind Mapping for organizing insights and identifying priority clusters
- **Phase 3 - Development:** Five Whys for drilling down to root causes of critical gaps
- **Phase 4 - Action Planning:** Decision Tree Mapping for concrete next steps before the August 15 demo deadline

**Journey Rationale:** The brief is well-developed but likely has blind spots. Starting with expansive "what if" thinking surfaces the widest range of potential gaps. Mind mapping reveals which patterns are most significant. Five Whys gets beneath symptoms to root causes. Decision tree mapping converts findings into actionable priorities given the tight competition timeline.

## Phase 1 Results: What If Scenarios

**18 ideas generated across missing features, risks, technical blind spots, and architecture principles.**

### Missing Features

[MF #1] **Service Topology Intelligence**: Agents need MCP-based service registry integration to resolve cross-system RPC targets when the target isn't named in code.

[MF #2] **RPC Pattern Catalog & Agent Prompting System**: Per-system configurable catalog of "how this system makes RPC calls" — agents dynamically prompted with the right detection patterns.

[MF #3] **Prompt Development Workbench / Agent Test Harness**: Iterative prompt testing against known code patterns during development, separate from full repo scanning.

[MF #4] **On-Demand Document History & Diff View**: Version timeline with per-revision diffs, shown only on explicit request to avoid information overload.

[MF #5] **Answer Confidence & Boundary Guard**: Chat interface detects when questions exceed what code documents can answer and surfaces uncertainty boundaries.

[MF #6] **Live Scan Observatory Dashboard**: Per-agent visibility + live dependency graph showing documents being built and cross-system links resolving in real time.

[MF #7] **Audition Agent — Independent Document Verification**: Clean-context LLM call verifies worker-produced documents against raw source code to catch subtle hallucinations.

[MF #8] **Pre-Scan Repository Profiling Phase**: Agent scans repo structure first — identify languages, frameworks, conventions — to generate a scan filter for valid source files.

### Technical Blind Spots

[TB #1] **Object Flow Analysis for RPC Resolution**: SOFA RPC looks like normal method calls; agents need backward reference tracing (how was this object created?) to detect cross-system boundaries.

[TB #2] **Cross-Document Dependency Graph & Change Propagation**: Incremental re-scan needs dependency tracking — when a renamed class breaks links, dependent docs in other systems need notification, not just local re-process.

### Risks

[R #1] **Prompt Sensitivity to RPC Detection**: Cross-system detection quality depends on per-system prompt quality; validation cycle (scan → validate → adjust) takes hours, risking demo deadline.

[R #2] **Unprovable Faithfulness to Source**: No mathematical guarantee documents are correct; mitigation via citation anchors (source line links) and spot-check audition agent.

[R #3] **Magentic Orchestration Untested Beyond Original Design**: Microsoft Agent Framework has built-in Magentic orchestration, but Microsoft warns it's untested for custom agent types beyond the original 4 Magentic-One agents.

### Architecture Principles Discovered

[AP #1] **Documents Are Immutable Code Projections**: Documents are read-only artifacts derived from code. Human edits not allowed — only code change + re-scan mutates documents.

[AP #2] **3-State Uncertainty Model**: Instead of binary scanned/unscanned, record RPC endpoints even when provider isn't scanned — creating a traceable-but-unlinked middle state.

[AP #3] **On-Premise LLM Fallback Consideration**: Architecture should support swappable LLM backends for enterprise data residency compliance.

## Phase 2 Results: Mind Mapping

**9 clusters identified from 18 raw ideas:**

| Cluster | Contents | Theme |
|---------|----------|-------|
| **A: RPC Detection System** | MF#2 + TB#1 | Pattern catalog + object flow analysis — purely scanning-system |
| **B: Service Topology Resolution** | MF#1 | External MCP registry, bridges scanning ↔ service platform |
| **C: Prompt Engineering Tooling** | MF#3 + R#1 | Development-phase, rapid iteration, demo deadline mitigation |
| **D: Scan-Time Quality** | MF#7 | Audition agent verifies docs during scan |
| **E: Response-Time Trust** | MF#5 + R#2 | Chat guard + confidence boundaries during Q&A |
| **F: Change & History** | MF#4 + TB#2 + AP#1 | Archive, dependency notification, immutable projections |
| **G: Live Knowledge Graph** | MF#6 | Cross-document + code relationship graph |
| **H: Scan Preparation** | MF#8 + AP#2 | Pre-scan profiling + 3-state uncertainty model |
| **I: Architecture Foundation** | R#3 + AP#3 | Magentic validation spike + swappable LLM |

**Key insight:** RPC is the dominant theme — Clusters A, B, C all orbit around cross-system call detection. Cluster C (Prompt Workbench) is the development tool that populates Cluster A (RPC Detection Layer) with validated patterns.

## Phase 3 Results: Five Whys

### Root Cause Analysis — Cluster A (RPC Detection)

**Root cause:** The brief conflates "agents can understand code" with "agents can detect system boundaries." These are fundamentally different capabilities. Understanding code is an LLM strength. Detecting system boundaries requires domain-specific knowledge (ESB/SOFA/ESG patterns, object provenance tracing, service code resolution).

**Fix:** Architecture needs an explicit **RPC Detection Layer** — a named subsystem with pattern catalog, object flow analysis, and service topology queries.

### Root Cause Analysis — Cluster C (Prompt Engineering)

**Root cause:** Prompt quality for RPC detection is the single highest-risk variable in the system, and the brief provides no mechanism for controlling it. Without a fast iteration loop, detection quality is unknown until the final scan — days before the demo deadline.

**Fix:** Prompt Workbench (Cluster C) → RPC Pattern Catalog (Cluster A) pipeline:
1. Workbench: iterative prompt testing against sample code (seconds, not hours)
2. Validated prompts feed into the RPC Pattern Catalog
3. Catalog is what scanning agents use at runtime
4. Workbench also produces test cases for Audition Agent (MF#7)

**Critical insight:** Cluster C is not a nice-to-have — it's a critical-path dependency for Cluster A to work at all.

## Phase 4 Results: Decision Tree Mapping

### Summary of All Decisions

| Cluster | Decision | Effort | Rationale |
|---------|----------|--------|-----------|
| **A: RPC Detection System** | ✅ Build full layer | ~1 week | 3+ RPC methods even in test repos; embedding patterns in prompts is unmanageable |
| **B: Service Topology Resolution** | ✅ Build MCP tool | 2-3 days | Mock service platform makes demo more compelling than static config |
| **C: Prompt Engineering Tooling** | ✅ Build full workbench | 3-5 days | Critical-path dependency for Cluster A; rapid iteration needed before deadline |
| **D: Scan-Time Quality (Audition)** | ❌ Skip | — | Not critical for demo |
| **E: Response-Time Trust (Chat Guard)** | ❌ Skip | — | Not critical for demo |
| **F: Change & History** | ❌ Skip for now | — | May drop entire update system if time runs out |
| **G: Live Knowledge Graph** | ✅ Build live visualization | 3-5 days | Visually compelling demo as documents link up in real time |
| **H: Scan Preparation** | ✅ Build full profiling phase | 2-3 days | Smart file filtering, framework detection, feeds RPC catalog |
| **I: Architecture Foundation** | ✅ Magentic validation spike | 2-3 days | Week 1 spike to validate framework before committing to architecture |

**Total new scope:** ~3-4 weeks of additional work beyond the original brief scope.

### Decision Rationale Per Cluster

**Cluster A — RPC Detection Layer (Build Full):**
The RPC Detection Layer is a prompt-driven phase within the agent workflow, not a static analysis system. Components: (1) RPC Pattern Catalog — per-repo config of detection patterns, (2) Prompt Enrichment — template that injects catalog into agent instructions, (3) Service Topology Tool — resolves service codes to target systems. Build effort is prompt engineering + config schema + mock tool, not compiler-level analysis.

**Cluster B — Service Topology (MCP Tool):**
MCP tool queries a mock service platform by service code, returning target system and endpoint info. More compelling demo than static config — shows agents actively resolving cross-system targets.

**Cluster C — Prompt Workbench (Full Build):**
Critical-path dependency: without fast prompt iteration, RPC detection quality is unknown until final scan. Workbench enables: load sample code → run agent with enriched prompt → compare output to expected → adjust → re-run in seconds.

**Clusters D, E, F — Skipped:**
Audition agent, chat guard, and change management are deferred. Demo scope prioritizes the core scanning pipeline and cross-system trace. These are important for production but not demo-critical.

**Cluster G — Live Graph (Full Build):**
Transforms scan progress from a progress bar into a visually engaging experience. Documents appear as nodes, cross-system links light up as they're resolved. Demo audience watches the knowledge graph grow.

**Cluster H — Pre-Scan Profiling (Full Build):**
Agent does quick repo reconnaissance before dispatching workers: identifies languages, frameworks, conventions, source directories. Produces scan filter and feeds framework info into RPC Pattern Catalog.

**Cluster I — Magentic Spike (Week 1):**
Microsoft Agent Framework provides built-in Magentic orchestration matching Vulgata's needs, but Microsoft warns it's untested for custom agent types. Week 1 spike validates the pattern with Vulgata-like agents before committing to the full architecture.

### Implementation Priority Order

1. **Week 1: Cluster I (Magentic Spike)** — validate framework before anything else
2. **Week 1-2: Cluster C (Prompt Workbench)** — enable rapid prompt iteration immediately
3. **Week 2: Cluster H (Pre-Scan Profiling)** — feeds into RPC catalog setup
4. **Week 2-3: Cluster A (RPC Detection Layer)** — core cross-system detection
5. **Week 3: Cluster B (Service Topology MCP)** — resolves detected RPCs to systems
6. **Week 3-4: Cluster G (Live Graph)** — visual polish for demo
7. **Buffer: Original brief scope** — scanning pipeline, dashboard, chat, git monitoring