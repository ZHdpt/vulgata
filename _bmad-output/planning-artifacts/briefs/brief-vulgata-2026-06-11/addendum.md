# Addendum: Research & Brainstorming Synthesis

This addendum captures detail from the brainstorming session, domain research, and technical research that informed the brief update but belongs in downstream documents (PRD, architecture, solution design) rather than the brief itself.

---

## Terminology (Ubiquitous Language)

From domain research — terms to adopt in PRD and architecture:

| Term | Definition |
|------|-----------|
| **Living Documentation** | Documentation that automatically synchronizes with code changes (industry standard term) |
| **Code-to-Knowledge Extraction** | Vulgata's core process — coined term, no industry standard exists |
| **Doc Drift** | Gradual divergence between documentation and actual code |
| **Repository-Level Understanding** | AI comprehension spanning entire codebases, not individual files |
| **Orchestrator-Workers Pattern** | Industry-standard multi-agent pattern (formalized by Anthropic, 2024) |
| **Magentic Orchestration** | Microsoft Agent Framework's specific multi-agent pattern |
| **MCP (Model Context Protocol)** | Anthropic's open protocol for AI-tool integration ("USB for AI") |

## Vulgata-Specific Concepts (Coined)

| Term | Definition |
|------|-----------|
| **RPC Detection Layer** | Prompt-driven subsystem identifying cross-system remote calls |
| **Service Topology Resolution** | Resolving detected RPC endpoints to specific systems via MCP tools |
| **3-State Uncertainty Model** | Scanned (linked), Discovered-but-Unscanned (recorded), Unknown |
| **Audition Agent** | Clean-context verification agent (deferred from V1) |
| **Prompt Workbench** | Development-time tool for iterative prompt testing |
| **Pre-Scan Profiling** | Initial repository reconnaissance phase |
| **Document Immutability Principle** | Documents are read-only projections of code |

## Market & Competitive Context

- No direct competitor does cross-system business logic extraction — Vulgata occupies an uncontested niche
- Adjacent tools: Google Code Wiki, Swimm, Mintlify (single-repo); Sourcegraph Cody (code search); SonarQube (static analysis)
- AI code tools market: $128B globally (2026), shifting from code generation to code understanding
- 64% of developers already use AI for documentation (Google DORA 2025)
- LLM inference costs dropping rapidly — DeepSeek V4 at <1 CNY/M input tokens

## Technical Architecture Decisions (for Architecture Document)

### Agent Framework Assessment
- Microsoft Agent Framework provides all core multi-agent capabilities: Magentic orchestration, checkpointing, HITL, MCP, fan-out/fan-in parallel execution, workflow visualization
- Key risk: Magentic orchestration untested for custom agent types → Week 1 validation spike
- NuGet: `Microsoft.Agents.AI.Foundry --prerelease`, `Microsoft.Agents.AI.Workflows --prerelease`, `ModelContextProtocol --prerelease`

### Storage Architecture
- Recommended for demo: SQLite + EF Core for structured data, in-memory graph built on load for visualization
- Production path: PostgreSQL/SQL Server + Neo4j or graph extension

### Graph Visualization
- Blazor.Diagrams (`Z.Blazor.Diagrams`) — native Blazor, interactive node/edge diagrams

### LLM-Wiki Implementation Detail
- index.md: auto-generated after each scan, one entry per document with path, title, one-line summary
- log.md: append-only activity log, parseable format `## [YYYY-MM-DD HH:MM] operation | target`
- Chat agent workflow: read index → select 3-5 docs → read full content → follow cross-system links → synthesize with citations
- Schema layer: `agent-instructions.md` injected into worker prompts for consistent document format
- Production path: add hybrid BM25 + vector search when document count exceeds ~500

### LLM Provider
- DeepSeek V4 via OpenAI-compatible endpoint (cost-effective, competitive performance)
- Embeddings: text-embedding-3-small or local ONNX model for air-gapped deployments

## Brainstorming Decisions (Deferred from V1)

| Item | Decision | Rationale |
|------|----------|-----------|
| Audition Agent | Skip for V1 | Not critical for demo |
| Answer confidence & chat guard | Skip for V1 | Not critical for demo |
| On-demand document history & diff view | Skip for V1 | May drop entire update system if time runs out |

## Implementation Priority Order (from Brainstorming)

1. Week 1: Magentic validation spike
2. Week 1-2: Prompt Workbench
3. Week 2: Pre-Scan Profiling
4. Week 2-3: RPC Detection Layer
5. Week 3: Service Topology MCP
6. Week 3-4: Live Graph Visualization
7. Buffer: Original brief scope (scanning pipeline, dashboard, chat, git monitoring)
