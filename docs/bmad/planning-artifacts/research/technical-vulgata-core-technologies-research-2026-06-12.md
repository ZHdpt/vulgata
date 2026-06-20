---
stepsCompleted: [1, 2, 3]
inputDocuments:
  - brief-vulgata-2026-06-11/brief.md
  - brainstorming-session-2026-06-12-2359.md
  - domain-enterprise-code-analysis-llm-business-logic-extraction-research-2026-06-12.md
workflowType: 'research'
lastStep: 3
research_type: 'technical'
research_topic: 'Microsoft Agent Framework capabilities, document graph storage, and search/indexing for Vulgata'
research_goals: '1. Assess Microsoft Agent Framework for Vulgata needs and identify gaps. 2. Evaluate document graph storage and persistence approaches. 3. Evaluate search/indexing technologies (RAG, vector DB, semantic search) for document retrieval.'
user_name: 'zhdpt'
date: '2026-06-12'
web_research_enabled: true
source_verification: true
---

# Technical Research Report: Vulgata Core Technologies

**Date:** 2026-06-12
**Author:** zhdpt
**Research Type:** Technical

---

## 1. Executive Summary

This technical research evaluates three core technology areas for Vulgata's implementation: (1) Microsoft Agent Framework as the multi-agent orchestration backbone, (2) document graph storage and persistence strategies, and (3) search/indexing technologies for document retrieval. All research is grounded in official Microsoft documentation, current market data, and open-source library analysis.

**Key findings:**
- Microsoft Agent Framework provides **all core multi-agent capabilities** Vulgata needs — Magentic orchestration, checkpointing, HITL, MCP integration, and graph-based workflows — with only minor gaps
- The framework has built-in **workflow visualization** (Mermaid/DOT export) and **superstep-based parallel execution** that maps directly to Vulgata's scan architecture
- For document graph storage, a **hybrid approach** is recommended: structured documents in SQL/NoSQL + graph relationships in a dedicated graph structure
- **Blazor.Diagrams** is the best-fit library for live knowledge graph visualization in the Blazor dashboard
- For search/indexing, the **LLM-wiki pattern** (Section 8) is recommended over traditional RAG — it aligns naturally with Vulgata's document structure and avoids unnecessary infrastructure

---

## 2. Microsoft Agent Framework Assessment

### 2.1 Capability Matrix: What Vulgata Needs vs. What the Framework Provides

| Vulgata Requirement | Agent Framework Feature | Status | Notes |
|---------------------|------------------------|--------|-------|
| **Orchestrator dispatches worker agents** | Magentic orchestration (manager + specialized agents) | ✅ Built-in | Manager dynamically selects agents, tracks progress, detects stalls |
| **Parallel worker execution** | Concurrent orchestration + Fan-out/Fan-in edges | ✅ Built-in | `AddFanOutEdge` + `AddFanInBarrierEdge` for parallel dispatch and result aggregation |
| **Long-running scan with recovery** | Checkpointing (superstep boundaries) | ✅ Built-in | `FileCheckpointStorage`, saves executor state, message queues, workflow position |
| **Human-in-the-loop for uncertainties** | Tool Approval + Request/Response | ✅ Built-in | `ApprovalRequiredAIFunction`, `RequestInfoEvent`, pause-resume cycle |
| **MCP tool integration** | Local MCP tools + Hosted MCP tools | ✅ Built-in | Native MCP C# SDK integration, `McpClientFactory`, `InvokeMcpTool` |
| **Agent session isolation** | `AgentSession` per agent | ✅ Built-in | Each agent has independent session; sessions synchronized in group chat |
| **Streaming progress events** | `WorkflowEvent` streaming | ✅ Built-in | `WatchStreamAsync()`, per-executor `AgentResponseUpdateEvent` |
| **Workflow visualization** | `ToMermaidString()` / `ToDotString()` | ✅ Built-in | Export to Mermaid/Graphviz for debugging workflow topology |
| **Workflow-as-Agent** | `AsAIAgent()` extension | ✅ Built-in | Wrap entire scan workflow as a single agent for chat interface |
| **Type-safe message routing** | Strongly-typed edges + `WorkflowBuilder` | ✅ Built-in | Compile-time validation of message types between executors |
| **Multiple LLM providers** | Foundry, Azure OpenAI, OpenAI, Anthropic, Ollama | ✅ Built-in | DeepSeek via OpenAI-compatible endpoint |
| **RAG context providers** | `ContextProvider` for agent memory | ✅ Built-in | Inject relevant documents into agent prompts at runtime |
| **Declarative workflow definition** | Declarative Workflows (YAML/JSON) | ✅ Built-in | Alternative to code-based workflow definition |

### 2.2 Framework Architecture Deep-Dive

**Execution Model: Supersteps (Bulk Synchronous Parallel)**

The framework uses a Pregel-based BSP model. Each superstep:
1. Collects all pending messages from the previous superstep
2. Routes messages to target executors based on edge definitions
3. Runs all target executors concurrently within the superstep
4. Waits for all executors to complete (synchronization barrier)
5. Queues new messages for the next superstep

**Implication for Vulgata:** This maps perfectly to the scan architecture — one superstep = one batch of code units dispatched to workers. The barrier ensures all workers finish before the next batch.

**Magentic Orchestration:**

The Magentic pattern provides:
- **Manager agent**: dynamically selects which worker acts next
- **Progress ledger**: tracks task satisfaction, loop detection, stall detection
- **Plan creation/replanning**: manager creates and revises task plans
- **Max rounds/stalls/resets**: configurable limits to prevent infinite loops
- **HITL plan review**: optional human approval of the manager's plan

**Fan-out/Fan-in for Worker Dispatch:**

```csharp
// Vulgata's scan workflow pattern
var builder = new WorkflowBuilder(profiler);           // Pre-scan profiling
builder.AddEdge(profiler, orchestrator);               // Profile → Orchestrator
builder.AddFanOutEdge(orchestrator, workers);          // Dispatch to N workers
builder.AddFanInBarrierEdge(workers, aggregator);      // Collect all results
builder.AddEdge(aggregator, uncertaintyResolver);      // Resolve cross-system links
```

### 2.3 Gaps and Limitations

| Gap | Severity | Mitigation |
|-----|----------|------------|
| **Magentic untested for custom agent types** | Medium | Week 1 validation spike (already planned) |
| **No built-in document storage** | Expected | Framework is agent orchestration, not data storage — Vulgata handles this |
| **No built-in graph database** | Expected | Same as above — Vulgata's own persistence layer |
| **Superstep barrier blocks fast paths** | Low | Consolidate sequential steps into single executors where needed |
| **.NET packages are prerelease** | Low | `Microsoft.Agents.AI.Foundry --prerelease` — stable enough for competition demo |
| **DeepSeek not natively listed** | Low | Use OpenAI-compatible endpoint; framework supports custom `IChatClient` |
| **No built-in rate limiting** | Low | Implement at application level if needed |

### 2.4 Framework Verdict

**✅ Microsoft Agent Framework is a strong fit for Vulgata.** It provides all core multi-agent capabilities out of the box. The only significant risk is the Magentic orchestration's untested status for custom agent types — which is already addressed by the planned Week 1 validation spike.

**NuGet packages needed:**
```
Microsoft.Agents.AI.Foundry --prerelease
Microsoft.Agents.AI.Workflows --prerelease
ModelContextProtocol --prerelease
```

---

## 3. Document Graph Storage & Persistence

### 3.1 Document Structure

Vulgata documents are structured artifacts with:
- **Metadata**: source file path, code unit name, language, system/repo ID, version, scan timestamp
- **Content**: business logic description, code logic description, cross-system links
- **Relationships**: parent-child (directory tree), cross-reference (RPC links), version history (old → new)

### 3.2 Storage Architecture Options

| Approach | Pros | Cons | Recommendation |
|----------|------|------|---------------|
| **SQL + JSON columns** | Simple, single DB, good querying | Graph queries are expensive (recursive CTEs) | Good for demo |
| **SQL + Graph DB (Neo4j)** | Best graph queries, native path traversal | Two databases, operational complexity | Overkill for demo |
| **Document DB (MongoDB/CosmosDB)** | Flexible schema, good for hierarchical docs | Graph queries require application code | Viable |
| **SQLite + In-Memory Graph** | Zero setup, fast, single process | Not distributed, memory-bound | **Best for demo** |

### 3.3 Recommended Approach: Hybrid SQLite + In-Memory Graph

For the competition demo, a pragmatic approach:

```
┌─────────────────────────────────────────┐
│              SQLite (EF Core)            │
│  ┌─────────────┐  ┌──────────────────┐  │
│  │ Documents    │  │ ScanRuns         │  │
│  │ - Id         │  │ - Id             │  │
│  │ - Path       │  │ - StartedAt      │  │
│  │ - Content    │  │ - CompletedAt    │  │
│  │ - Type       │  │ - Status         │  │
│  │ - CodeUnitId │  └──────────────────┘  │
│  │ - Version    │                        │
│  │ - SystemId   │  ┌──────────────────┐  │
│  │ - RepoId     │  │ DocumentLinks    │  │
│  └─────────────┘  │ - SourceDocId    │  │
│                    │ - TargetDocId    │  │
│  ┌─────────────┐  │ - LinkType       │  │
│  │ CodeUnits    │  │ - RpcEndpoint    │  │
│  │ - Id         │  └──────────────────┘  │
│  │ - FilePath   │                        │
│  │ - Language   │  ┌──────────────────┐  │
│  │ - Checksum   │  │ Uncertainties    │  │
│  └─────────────┘  │ - Id             │  │
│                    │ - Status         │  │
│                    │ - Question       │  │
│                    └──────────────────┘  │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│       In-Memory Graph (Build on Load)    │
│  Nodes: Documents, CodeUnits, Systems    │
│  Edges: parentOf, callsInto, dependsOn   │
│  Built from SQLite on app startup        │
│  Used for: live visualization, queries   │
└─────────────────────────────────────────┘
```

**Why this works for the demo:**
- SQLite is zero-config, embedded, and fast
- EF Core + SQLite is a standard .NET pattern
- The graph is rebuilt from relational data on startup (demo repos are small)
- No external database dependencies — single deployable

**Production path:** Swap SQLite for PostgreSQL/SQL Server; add Neo4j or a graph extension for larger scale.

### 3.4 Blazor Graph Visualization

For the live knowledge graph in the Blazor dashboard:

| Library | Type | Stars | Blazor Support | Best For |
|---------|------|-------|---------------|----------|
| **Blazor.Diagrams** | Interactive node/edge diagram | 1.5k+ | Native Blazor | **Live graph visualization** |
| LiveCharts2 | Chart library | 5k+ | Blazor WASM | Charts, not graphs |
| GraphControl | WinForms graph control | — | Via bridge | Desktop only |

**Recommendation: Blazor.Diagrams** (`Z.Blazor.Diagrams`)

Features that map to Vulgata's needs:
- **Nodes**: represent documents and code units
- **Links/Edges**: represent cross-system RPC calls and dependencies
- **Ports**: connection points for directed relationships
- **Drag & zoom**: interactive exploration of the knowledge graph
- **Custom templates**: style nodes by type (business logic vs. code logic)
- **Event handling**: click a node → navigate to document detail
- **Real-time updates**: add nodes/links as scan progresses

**NuGet packages:**
```
Z.Blazor.Diagrams
Z.Blazor.Diagrams.Core
Z.Blazor.Diagrams.Algorithms
```

---

## 4. Search & Indexing Technologies

### 4.1 Search Requirements

Vulgata needs:
1. **Full-text search**: find documents by keyword
2. **Semantic search**: find documents by meaning (for chat RAG)
3. **Cross-document retrieval**: given a question, find relevant documents across systems
4. **Code-to-document lookup**: given a code location, find its document

### 4.2 Technology Options

| Approach | Description | .NET Support | Complexity | Best For |
|----------|------------|-------------|------------|----------|
| **SQLite FTS5** | Full-text search extension | Built-in (EF Core) | Low | Keyword search |
| **Semantic Kernel Vector Store** | Microsoft's RAG framework | Native .NET | Medium | Semantic search + RAG |
| **Qdrant / Milvus** | Dedicated vector DB | Client SDK | High | Large-scale semantic search |
| **LlamaIndex / LangChain** | Python RAG frameworks | No native .NET | High | Python ecosystem |
| **Simple TF-IDF + Cosine** | Custom lightweight approach | DIY | Low-Medium | Demo-scale semantic search |
| **LLM-Wiki (index.md)** | Structured index + full-page reading | Zero infrastructure | Very Low | **Recommended for demo** |

### 4.3 Recommended Approach: LLM-Wiki Pattern

See **Section 8** for the detailed LLM-wiki analysis. For the demo, the LLM-wiki pattern is the recommended approach — it avoids vector DB infrastructure entirely while providing better answer quality for Vulgata's structured document format.

For production scale, a hybrid approach combining LLM-wiki with lightweight search (BM25 + optional vector) provides a clean migration path.

---

## 5. Technology Stack Summary

### 5.1 Recommended Stack

| Layer | Technology | Rationale |
|-------|-----------|----------|
| **Agent Orchestration** | Microsoft Agent Framework (Magentic) | Built-in multi-agent patterns, checkpointing, HITL, MCP |
| **LLM Provider** | DeepSeek V4 (OpenAI-compatible endpoint) | Low cost, competitive performance |
| **Embeddings** | text-embedding-3-small or local ONNX | Cost-effective for demo scale |
| **Web Framework** | ASP.NET Core 10 + Blazor | Per brief requirements |
| **Database** | SQLite + EF Core | Zero-config, embedded, fast |
| **Graph Visualization** | Blazor.Diagrams | Native Blazor, interactive node/edge diagrams |
| **Full-Text Search** | SQLite FTS5 | Built-in, no extra dependency |
| **Search & Retrieval** | LLM-Wiki (index.md + log.md) | Zero infrastructure, perfect fit for structured docs |
| **MCP Integration** | Agent Framework MCP + MCP C# SDK | Native framework support |

### 5.2 NuGet Package List

```xml
<!-- Agent Framework -->
<PackageReference Include="Microsoft.Agents.AI.Foundry" Version="*-*" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" Version="*-*" />

<!-- MCP -->
<PackageReference Include="ModelContextProtocol" Version="*-*" />

<!-- Data -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.*" />

<!-- UI -->
<PackageReference Include="Z.Blazor.Diagrams" Version="3.*" />
<PackageReference Include="Z.Blazor.Diagrams.Core" Version="3.*" />
<PackageReference Include="Z.Blazor.Diagrams.Algorithms" Version="3.*" />
```

---

## 6. Key Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|----------|
| Agent orchestration pattern | Magentic (Agent Framework) | Built-in manager + workers, progress tracking, stall detection |
| Scan execution model | Superstep-based fan-out/fan-in | Maps to batch dispatch of code units to workers |
| Document storage | SQLite + in-memory graph | Simple, fast, zero-config for demo |
| Graph visualization | Blazor.Diagrams | Native Blazor, interactive, customizable |
| Search strategy | LLM-Wiki (index.md + log.md) | Zero infrastructure, aligned with document structure |
| Embedding model | text-embedding-3-small or local ONNX | Cost-effective; local option for air-gapped |
| MCP tool implementation | Agent Framework MCP + custom MCP server | Framework-native; custom server for service topology |

---

## 7. Research Methodology

- **Primary sources**: Microsoft Learn documentation (agent-framework, azure, durable-task), NuGet package documentation, GitHub repositories
- **Secondary sources**: Technical blog posts, open-source library documentation, CSDN technical articles
- **Date range**: 2025-2026, with emphasis on current (June 2026) prerelease versions
- **Verification**: Official Microsoft documentation cross-referenced with GitHub samples and community usage

---

## 8. LLM-Wiki Approach for Search & Indexing

### 8.1 The LLM-Wiki Pattern (Karpathy, 2025)

The LLM-wiki pattern (described by Andrej Karpathy at [gist.github.com/karpathy](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)) proposes a fundamentally different approach to LLM-powered knowledge management than RAG. Instead of retrieving raw document chunks at query time and re-deriving knowledge from scratch, the LLM **incrementally builds and maintains a persistent wiki** — a structured, interlinked collection of markdown files.

**Core architecture (three layers):**

| Layer | Description | Vulgata Equivalent |
|-------|-------------|-------------------|
| **Raw sources** | Immutable source documents — the ground truth | Source code repositories (immutable, version-controlled) |
| **The wiki** | LLM-generated markdown files — summaries, entity pages, concept pages, cross-references | Generated documents (business logic + code logic), organized in source-tree hierarchy |
| **The schema** | Configuration telling the LLM how the wiki is structured, conventions, workflows | RPC pattern catalog + agent prompts + document templates + pre-scan profiling rules |

**Key insight:** "The wiki is a persistent, compounding artifact. The cross-references are already there. The contradictions have already been flagged. The synthesis already reflects everything you've read. The wiki keeps getting richer with every source you add."

### 8.2 The Three Core Operations

Karpathy defines three operations that map directly to Vulgata's workflow:

| Operation | LLM-Wiki Description | Vulgata Equivalent |
|-----------|---------------------|-------------------|
| **Ingest** | Read source → discuss key takeaways → write summary → update index → update entity/concept pages → append log entry | Scan operation: orchestrator dispatches workers → workers read code → produce documents → update index.md → append log.md |
| **Query** | Ask question → LLM searches index → reads relevant pages → synthesizes answer with citations → optionally files answer as new wiki page | Chat interface: user asks question → chat agent reads index.md → selects relevant documents → reads full pages → answers with citations → optionally files answer |
| **Lint** | Health-check wiki: contradictions, stale claims, orphan pages, missing cross-references, data gaps | Audition Agent: verifies documents against source, flags contradictions, identifies missing cross-system links |

### 8.3 Vulgata as an LLM-Wiki for Source Code

Vulgata's architecture is essentially an LLM-wiki where the "raw sources" are code repositories and the "wiki" is the generated document collection. The mapping is remarkably clean:

```
LLM-Wiki Pattern              Vulgata Implementation
─────────────────────────────────────────────────────
Raw Sources (immutable)   →   Git repositories (code)
Wiki (markdown files)     →   Generated documents (business + code logic)
Schema (conventions)      →   RPC Pattern Catalog + Agent Prompts
Ingest operation          →   Scan operation (orchestrator → workers)
Query operation           →   Chat interface
Lint operation            →   Audition Agent (document health-check)
index.md                  →   Auto-generated document index
log.md                    →   Scan run log + change log
Cross-references          →   Cross-system RPC links
```

### 8.4 DIY Wiki Architecture for Vulgata

**How it works for Vulgata's search/indexing:**

Instead of building a full RAG pipeline with vector embeddings, Vulgata can adopt the wiki-style approach:

```
┌─────────────────────────────────────────────────────────┐
│              LLM-Wiki Search Architecture                │
│                                                          │
│  Chat Question                                           │
│       │                                                  │
│       ├──→ Read index.md (document catalog)              │
│       │    └── Each entry: title, path, one-line summary │
│       │    └── Organized by: system → repo → category    │
│       │                                                  │
│       ├──→ LLM selects relevant documents from index     │
│       │    └── "Risk evaluation" → finds 3-5 docs        │
│       │                                                  │
│       ├──→ Read selected documents (full content)        │
│       │    └── Documents are markdown, already structured │
│       │    └── Cross-system links are inline references  │
│       │                                                  │
│       └──→ Synthesize answer with citations              │
│            └── Links back to specific documents           │
│            └── Optionally: file answer as new wiki page   │
└─────────────────────────────────────────────────────────┘
```

**Why this works for the demo:**

1. **Scale is manageable**: 2 demo repos → ~100-200 documents. The index.md approach works well at this scale (Karpathy reports it works for "~100 sources, ~hundreds of pages").

2. **Documents are already structured**: Unlike raw text chunks in RAG, Vulgata's documents are purpose-written markdown with clear headings, cross-references, and metadata. The LLM reads them as coherent pages, not fragments.

3. **Cross-references are pre-built**: The scan already resolves cross-system links. When the chat agent reads a document about System A's risk evaluation, it already contains a link to System B's risk service document. No need to re-discover this at query time.

4. **No embedding infrastructure**: Skip the vector database, embedding generation, and chunking pipeline. The index.md file is the only "search index" needed.

5. **Compounding knowledge**: Every scan makes the wiki richer. Every cross-system link resolved is permanently available. The knowledge accumulates rather than being re-derived.

### 8.5 Concrete DIY Implementation Plan

#### 8.5.1 Directory Structure

```
vulgata/
├── wiki/                          # The LLM-maintained wiki (generated)
│   ├── index.md                   # Document catalog (auto-generated)
│   ├── log.md                     # Activity log (append-only)
│   ├── systems/
│   │   ├── mbank/
│   │   │   ├── mbank-core/
│   │   │   │   ├── RiskEvaluator.md
│   │   │   │   ├── LoanController.md
│   │   │   │   └── CreditCheckService.md
│   │   │   └── mbank-api/
│   │   │       └── RiskApiGateway.md
│   │   └── riskeng/
│   │       └── riskeng-service/
│   │           ├── RiskService.md
│   │           └── ScoreCalculator.md
│   ├── cross-system/              # Cross-system synthesis pages
│   │   ├── risk-evaluation-flow.md
│   │   └── loan-approval-flow.md
│   └── queries/                   # Filed query results (optional)
│       └── what-happens-during-risk-evaluation.md
├── schema/                        # The schema layer (configuration)
│   ├── agent-instructions.md      # How agents write wiki pages
│   ├── document-template.md       # Template for generated documents
│   ├── rpc-pattern-catalog.yaml   # Per-system RPC detection patterns
│   └── pre-scan-profiling-rules.yaml
└── raw/                           # Raw sources (git repos, cloned)
    ├── mbank-core/                # Immutable — never modified
    └── riskeng-service/
```

#### 8.5.2 index.md Design (Auto-Generated)

The index.md is generated by the orchestrator after each scan completes. It serves as the primary navigation and search tool for the chat agent.

```markdown
# Vulgata Knowledge Index
*Generated: 2026-08-10 14:32 UTC | Last scan: mbank-core (full)*

## System: Mobile Banking (mbank)

### Repository: mbank-core
- [RiskEvaluator.java](wiki/systems/mbank/mbank-core/RiskEvaluator.md) — Evaluates credit risk for loan applications, calls RiskService via SOFA RPC
- [LoanController.java](wiki/systems/mbank/mbank-core/LoanController.md) — REST endpoint for loan application submission
- [CreditCheckService.java](wiki/systems/mbank/mbank-core/CreditCheckService.md) — Orchestrates credit check workflow across internal + external systems

### Repository: mbank-api
- [RiskApiGateway.java](wiki/systems/mbank/mbank-api/RiskApiGateway.md) — API gateway for risk evaluation, routes to mbank-core

## System: Risk Engine (riskeng)

### Repository: riskeng-service
- [RiskService.java](wiki/systems/riskeng/riskeng-service/RiskService.md) — Core risk calculation service, called by mbank-core, ibank-core
- [ScoreCalculator.java](wiki/systems/riskeng/riskeng-service/ScoreCalculator.md) — Credit score calculation with configurable thresholds

## Cross-System Links
- mbank-core/RiskEvaluator → riskeng-service/RiskService (SOFA RPC) ✅ RESOLVED
- ibank-core/LoanProcessor → riskeng-service/RiskService (SOFA RPC) ⚠️ UNVERIFIED — provider not scanned

## Cross-System Synthesis Pages
- [Risk Evaluation End-to-End Flow](wiki/cross-system/risk-evaluation-flow.md) — Complete trace from mobile app to risk engine

## Uncertainties
- [UNC-001] RiskService threshold configuration source unknown
- [UNC-002] ESG adapter between mbank and legacy scoring system not scanned

## Filed Queries
- [What happens during a risk evaluation?](wiki/queries/what-happens-during-risk-evaluation.md) — Filed 2026-08-10
```

**Generation logic (C# pseudocode):**

```csharp
public async Task<string> GenerateIndexAsync(List<Document> documents, List<CrossSystemLink> links)
{
    var sb = new StringBuilder();
    sb.AppendLine($"# Vulgata Knowledge Index");
    sb.AppendLine($"*Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | Last scan: {lastScanRepo} ({lastScanType})*");
    sb.AppendLine();

    // Group by system → repo
    foreach (var system in documents.GroupBy(d => d.SystemId))
    {
        sb.AppendLine($"## System: {system.Key}");
        sb.AppendLine();
        foreach (var repo in system.GroupBy(d => d.RepoId))
        {
            sb.AppendLine($"### Repository: {repo.Key}");
            foreach (var doc in repo.OrderBy(d => d.Title))
            {
                sb.AppendLine($"- [{doc.Title}]({doc.WikiPath}) — {doc.OneLineSummary}");
            }
            sb.AppendLine();
        }
    }

    // Cross-system links section
    sb.AppendLine("## Cross-System Links");
    foreach (var link in links)
    {
        var status = link.Resolved ? "✅ RESOLVED" : "⚠️ UNVERIFIED — provider not scanned";
        sb.AppendLine($"- {link.SourceDoc} → {link.TargetDoc} ({link.Protocol}) {status}");
    }
    sb.AppendLine();

    // Cross-system synthesis pages (generated by chat agent from filed queries)
    // ...

    // Uncertainties
    sb.AppendLine("## Uncertainties");
    foreach (var unc in uncertainties)
    {
        sb.AppendLine($"- [{unc.Id}] {unc.Question}");
    }

    return sb.ToString();
}
```

#### 8.5.3 log.md Design (Append-Only)

The log.md is append-only — the orchestrator appends entries after each scan, and the chat agent appends entries after significant queries.

```markdown
# Vulgata Activity Log

## [2026-08-10 14:32] scan | mbank-core (full scan)
- 47 code units processed
- 12 business logic documents generated
- 8 code logic documents generated
- 3 cross-system links detected
- 2 uncertainties created
- index.md updated

## [2026-08-10 14:15] query | "What happens during a risk evaluation?"
- Read index.md → selected 5 documents
- Read: RiskEvaluator.md, RiskApiGateway.md, RiskService.md, CreditCheckService.md, ScoreCalculator.md
- Answer filed as: wiki/queries/what-happens-during-risk-evaluation.md
- Cross-system synthesis page created: wiki/cross-system/risk-evaluation-flow.md

## [2026-08-09 16:00] scan | riskeng-service (full scan)
- 23 code units processed
- 5 business logic documents generated
- 1 cross-system link resolved (mbank-core/RiskEvaluator → riskeng-service/RiskService)
- index.md updated

## [2026-08-08 10:00] ingest | global-context (service topology)
- Added service topology map: 5 systems, 12 services
- Resolved 3 pending uncertainties
```

**Key design property:** Each entry starts with a consistent prefix `## [YYYY-MM-DD HH:MM] operation | target`. This makes the log parseable — `grep "^## \[" log.md | tail -5` gives the last 5 entries. The chat agent can read the last few log entries to understand recent activity before answering questions.

#### 8.5.4 Chat Agent Workflow (Query Operation)

The chat agent follows a structured workflow for every user question:

```
1. Read index.md (full file — ~200 lines for 200 docs, fits in context)
2. Identify 3-5 most relevant documents based on the question
3. Read selected documents in full (structured markdown, not chunks)
4. If cross-system links are referenced, follow them to read linked documents
5. Synthesize answer with citations (links to specific documents)
6. Optionally: file the answer as a new wiki page under wiki/queries/
7. If the answer reveals a cross-system flow, create a synthesis page under wiki/cross-system/
8. Append query entry to log.md
```

**C# implementation sketch:**

```csharp
public async Task<string> AnswerQuestionAsync(string question)
{
    // Step 1: Read the index
    var index = await fileSystem.ReadAllTextAsync("wiki/index.md");

    // Step 2: Ask LLM to select relevant documents from the index
    var selectionPrompt = $"""
        You are a Vulgata chat agent. Given the question and the document index below,
        select the 3-5 most relevant documents to answer the question.
        Return only the document paths, one per line.

        Question: {question}

        Index:
        {index}
        """;

    var selectedPaths = await llm.CompleteAsync(selectionPrompt);

    // Step 3: Read selected documents in full
    var documents = new List<string>();
    foreach (var path in selectedPaths)
    {
        var content = await fileSystem.ReadAllTextAsync(path);
        documents.Add($"## {path}\n\n{content}");
    }

    // Step 4: Follow cross-system links if referenced
    // (LLM identifies linked documents in the content, agent reads them)

    // Step 5: Synthesize answer
    var answerPrompt = $"""
        You are a Vulgata chat agent. Answer the user's question using the documents below.
        Cite specific documents using their paths. If the answer traces a cross-system flow,
        note it explicitly.

        Question: {question}

        Documents:
        {string.Join("\n\n---\n\n", documents)}
        """;

    var answer = await llm.CompleteAsync(answerPrompt);

    // Step 6-7: Optionally file the answer (LLM decides if it's worth keeping)
    // Step 8: Append to log.md

    return answer;
}
```

#### 8.5.5 Schema Layer: Agent Instructions

Following Karpathy's pattern, the schema layer tells agents *how* to write wiki pages. This is the `agent-instructions.md` file that gets injected into worker agent prompts:

```markdown
# Vulgata Wiki Conventions

## Document Format
Every document must have:
- A YAML frontmatter block with: title, source_file, language, system, repo, scan_date, doc_type (business_logic | code_logic)
- A one-line summary as the first paragraph
- Clear section headings
- Cross-system links in the format: → [TargetDoc](wiki/systems/{system}/{repo}/{TargetDoc}.md)

## Cross-System Link Format
When detecting an RPC call to another system:
- Format: `→ [RiskService](wiki/systems/riskeng/riskeng-service/RiskService.md) via SOFA RPC`
- If the target system is unscanned: `→ RiskService (SOFA RPC) ⚠️ UNVERIFIED — riskeng not scanned`

## Uncertainty Format
When encountering an ambiguity:
- Format: `[UNC-{id}] {question}`
- Add to the document's "Uncertainties" section
- The orchestrator will pick these up for resolution

## index.md Update Rules
After each scan:
- Add/update entries for all generated documents
- Update cross-system link status (RESOLVED vs UNVERIFIED)
- Add new uncertainties
- Update the "Generated" timestamp at the top
```

#### 8.5.6 Lint Operation: Wiki Health Check

The lint operation (mapped to Vulgata's Audition Agent or a dedicated maintenance pass) checks:

| Check | Description | Vulgata Implementation |
|-------|-------------|----------------------|
| **Contradictions** | Two documents describe the same behavior differently | Compare documents linked by cross-system references; flag inconsistencies |
| **Stale claims** | A document references code that has since changed | Compare document scan_date with git commit timestamps; flag outdated docs |
| **Orphan pages** | Documents with no inbound links from other documents | Query the document graph for nodes with in-degree = 0 |
| **Missing pages** | Cross-system links reference documents that don't exist | Validate all cross-system link targets exist in the wiki |
| **Missing cross-references** | Documents that call other systems but don't link to them | Compare detected RPC endpoints with existing cross-system links |
| **Data gaps** | Uncertainties that could be resolved with additional context | Review unresolved uncertainties; suggest context to provide |

### 8.6 Comparison: LLM-Wiki vs. RAG for Vulgata

| Dimension | LLM-Wiki Approach | Traditional RAG |
|-----------|------------------|-----------------|
| **Query mechanism** | Read index → select pages → read full pages | Embed query → vector search → retrieve chunks |
| **Knowledge state** | Pre-compiled, persistent, cross-referenced | Re-derived from chunks on every query |
| **Infrastructure** | index.md + markdown files | Vector DB + embedding pipeline + chunking |
| **Demo scale (100-200 docs)** | Excellent fit | Over-engineered |
| **Cross-system links** | Already resolved in documents | Must be re-discovered from chunks |
| **Answer quality** | Reads coherent pages, not fragments | Reads fragments, may miss context |
| **Maintenance** | LLM updates index.md on each scan | Re-index on each scan |
| **Scale ceiling** | ~hundreds of pages (index.md approach) | Thousands to millions (vector DB) |
| **Production path** | Add hybrid search (BM25 + vector) as scale grows | Already has vector infrastructure |
| **Query results compound** | Answers can be filed as new wiki pages | Answers are ephemeral (chat history only) |
| **Observability** | log.md provides timeline of all activity | Requires separate logging infrastructure |

### 8.7 Recommended Approach

**For the demo: Pure LLM-Wiki approach.**

1. **index.md** — auto-generated from the document graph after each scan. One entry per document with path, title, one-line summary, and category tags. Fits in context (~200 lines for 200 docs).

2. **Chat agent workflow:**
   - Read index.md (full file in context)
   - Select 3-5 most relevant documents based on question
   - Read full document content (structured markdown, not chunks)
   - Follow cross-system links to read linked documents
   - Synthesize answer with citations (links to specific documents)
   - Optionally file the answer as a new wiki page (query results compound)

3. **log.md** — append-only scan/query log for timeline visibility. Parseable format: `## [YYYY-MM-DD HH:MM] operation | target`.

4. **No vector DB, no embeddings, no chunking pipeline.** The structured documents + index are sufficient for the demo.

5. **Schema layer** — `agent-instructions.md` injected into worker prompts to ensure consistent document format, cross-system link conventions, and index update rules.

**Production path:** When document count exceeds ~500, add a lightweight hybrid search layer:
- BM25 (keyword) for exact matches
- Optional: small local embedding model for semantic search (e.g., all-MiniLM-L6-v2 via ONNX)
- The LLM-wiki approach remains the primary query pattern; search just helps find the right pages faster
- Consider [qmd](https://github.com/tobi/qmd) — a local search engine for markdown files with hybrid BM25/vector search and LLM re-ranking, all on-device, with both CLI and MCP server interfaces

### 8.8 Key Insight

Vulgata is not building a RAG system — it's building an **LLM-maintained wiki for source code**. The documents are the wiki pages. The cross-system links are the cross-references. The scan is the ingest operation. The chat is the query operation. The audition agent is the lint operation. This framing simplifies the architecture significantly and aligns perfectly with the LLM-wiki pattern.

The most important architectural implication: **the search/indexing problem is already solved by the document structure itself.** The documents are not raw text that needs chunking and embedding — they are purpose-written, interlinked, structured knowledge artifacts. The index.md file is the only additional infrastructure needed.

### 8.9 Why This Beats RAG for Vulgata Specifically

1. **Vulgata documents are wiki pages by design.** They're not raw source code dumps — they're structured markdown with headings, summaries, and cross-references. RAG would chop this structure into meaningless chunks.

2. **Cross-system traces require coherence.** A question like "what happens during risk evaluation?" spans 5+ documents across 2+ systems. RAG retrieves fragments from each; the LLM-wiki reads the full pages and follows the links. The difference in answer quality is dramatic.

3. **The index.md is the perfect search index.** At demo scale, the entire document catalog fits in context. The LLM can "search" by reading the index — no embeddings needed.

4. **Compounding is the killer feature.** Every cross-system link resolved during a scan is permanently available. Every filed query enriches the wiki. The knowledge base gets better over time without any additional infrastructure. RAG systems don't compound — they re-derive from scratch every time.
