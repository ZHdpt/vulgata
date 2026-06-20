---
stepsCompleted: [1, 2]
inputDocuments:
  - brief-vulgata-2026-06-11/brief.md
  - brainstorming-session-2026-06-12-2359.md
workflowType: 'research'
lastStep: 2
research_type: 'domain'
research_topic: 'Enterprise code analysis and LLM-powered business logic extraction from multi-system codebases'
research_goals: 'Establish a well-defined terminology and ubiquitous language for the domain of LLM-powered code-to-knowledge extraction across multi-system enterprise environments'
user_name: 'zhdpt'
date: '2026-06-12'
web_research_enabled: true
source_verification: true
---

# Domain Research Report: Enterprise Code-to-Knowledge Extraction

**Date:** 2026-06-12
**Author:** zhdpt
**Research Type:** Domain

---

## 1. Executive Summary

This domain research establishes the terminology, concepts, and landscape for **LLM-powered business logic extraction from multi-system enterprise codebases** — the problem space Vulgata operates in. The research draws from market data, academic papers, industry tools, and emerging trends to build a ubiquitous language for the PRD and architecture.

**Key findings:**
- The domain sits at the intersection of three mature markets: AI Code Tools ($128B global, 2026), Static Code Analysis ($2.4B, 2025), and Enterprise Knowledge Management — but no existing product fully addresses cross-system business logic extraction
- The industry is shifting from "AI as code generator" to "AI as code understander" — with 64% of developers already using AI for documentation
- The term "Living Documentation" has emerged as the industry standard for code-synchronized, auto-updating documentation
- Multi-agent orchestration patterns (Orchestrator-Workers) are well-established in both research and production systems
- MCP (Model Context Protocol) is becoming the standard for connecting AI agents to external knowledge sources

---

## 2. Domain Terminology & Ubiquitous Language

### 2.1 Core Concepts

| Term | Definition | Source / Confidence |
|------|-----------|-------------------|
| **Code-to-Knowledge Extraction** | The process of analyzing source code to produce structured, human-readable knowledge artifacts (documents, graphs, summaries) describing what the code does at a business level | **Coined for Vulgata** — no standard industry term exists; closest is "code documentation generation" |
| **Living Documentation** | Documentation that automatically synchronizes with code changes, staying current without manual maintenance. Coined by Gojko Adzic; now widely adopted in AI documentation tools | **Established** — used by Google Code Wiki, Swimm, and academic literature |
| **Code Logic vs. Business Logic** | *Code Logic*: structural description of what code does (classes, methods, data flow). *Business Logic*: the business rules, processes, and domain decisions the code implements | **Vulgata-specific** — no industry standard bifurcation exists; most tools produce only technical documentation |
| **Cross-System Trace** | Following a business process across multiple independent systems/repositories, linking each step to the code that implements it | **Vulgata-specific** — related to "distributed tracing" in observability but applied to static code analysis |
| **Doc Drift** | The gradual divergence between documentation and the actual code it describes, as code evolves but documentation doesn't | **Established** — widely used in software engineering literature |
| **Specification as Code / Spec as Code** | Treating specifications (requirements, designs) as version-controlled, executable artifacts rather than static documents | **Emerging** — popularized by Spec-Driven Development (SDD) movement in 2025-2026 |
| **Repository-Level Understanding** | AI comprehension that spans an entire codebase rather than individual files — understanding cross-file dependencies, architecture, and module relationships | **Established** — used by Sourcegraph, Google Code Wiki, and academic papers |
| **Uncertainty Resolution** | The process of identifying, tracking, and resolving ambiguities discovered during automated code analysis — including cross-system calls, unknown patterns, and unclear business intent | **Vulgata-specific** — related to "confidence scoring" in ML but applied to code understanding |

### 2.2 Multi-Agent Architecture Terms

| Term | Definition | Source / Confidence |
|------|-----------|-------------------|
| **Orchestrator-Workers Pattern** | A multi-agent architecture where a central orchestrator decomposes tasks and dispatches them to specialized worker agents, then aggregates results. Formalized by Anthropic in "Building Effective Agents" (2024) | **Established** — industry standard pattern |
| **Magentic Orchestration** | A specific multi-agent pattern in Microsoft Agent Framework where a manager agent dynamically coordinates specialized agents with planning, progress tracking, and stall detection | **Established** — built into Microsoft Agent Framework; based on Magentic-One research |
| **Agent Session** | Isolated conversation context for a single agent, maintaining its own message history and tool state. In multi-agent systems, sessions are synchronized but not shared | **Established** — Microsoft Agent Framework concept |
| **Human-in-the-Loop (HITL)** | A workflow pattern where agent execution pauses for human review, approval, or input before continuing | **Established** — industry standard |
| **Checkpointing** | Saving workflow state at defined points to enable recovery and resumption of long-running agent processes | **Established** — Microsoft Agent Framework feature |
| **Tool Approval** | A HITL mechanism where agent tool calls require explicit human approval before execution | **Established** — Microsoft Agent Framework feature |

### 2.3 Protocol & Integration Terms

| Term | Definition | Source / Confidence |
|------|-----------|-------------------|
| **MCP (Model Context Protocol)** | An open protocol by Anthropic that standardizes how AI models connect with external tools, data sources, and services. Analogous to "USB for AI" | **Established** — Anthropic standard, adopted by Microsoft, VS Code, and major AI tools |
| **MCP Server** | A service that exposes tools, data, or capabilities via the MCP protocol for AI clients to discover and use | **Established** |
| **MCP Client** | An AI application (IDE, agent, chatbot) that connects to MCP servers to access external tools and data | **Established** |
| **RAG (Retrieval-Augmented Generation)** | A technique where LLM responses are grounded in retrieved documents from a knowledge base, reducing hallucination | **Established** — industry standard |
| **Repo-Level RAG** | RAG applied to entire code repositories — indexing code structure, semantics, and dependencies for AI retrieval | **Emerging** — used by Sourcegraph Cody, Google Code Wiki |

### 2.4 Vulgata-Specific Concepts (Coined)

| Term | Definition |
|------|-----------|
| **RPC Detection Layer** | A prompt-driven subsystem that identifies cross-system remote procedure calls in source code, using per-system pattern catalogs and object flow analysis |
| **Service Topology Resolution** | Resolving detected RPC endpoints to specific systems/services via external registries (MCP tools or global context) |
| **3-State Uncertainty Model** | Scanned (linked), Unscanned-but-Discovered (recorded, unlinked), Unknown (no evidence) — for tracking cross-system dependencies |
| **Audition Agent** | A clean-context verification agent that independently checks worker-produced documents against raw source code |
| **Prompt Workbench** | A development-time tool for iterative prompt testing and validation against known code patterns, separate from production scanning |
| **Pre-Scan Profiling** | Initial repository reconnaissance phase that identifies languages, frameworks, conventions, and valid source files before dispatching worker agents |
| **Document Immutability Principle** | Documents are read-only projections of code; the only mutation path is code change → re-scan → regenerate |

---

## 3. Market Landscape

### 3.1 Market Size & Growth

The domain spans three overlapping markets:

| Market | 2025-2026 Size | Growth (CAGR) | Source |
|--------|---------------|---------------|--------|
| **AI Code Tools (broad)** | $128B (2026 global) | 24.5% | Grand View Research / Gartner |
| **AI Code Assistants (narrow)** | ~$7.4B (2025) | ~25% | Multiple research firms |
| **Static Code Analysis** | $2.4B (2025) | 12.4% | QYResearch |
| **AI Code Documentation** | No standalone market size | — | Subsumed within AI Code Tools |

**Key trend:** The AI code tools market is shifting from "code generation" (Copilot-style completion) to "code understanding" (agent-based analysis, documentation, review). 64% of developers already use AI for documentation (Google DORA 2025). Gartner predicts 75% of enterprise engineers will use AI code assistants by 2028.

### 3.2 Competitive Landscape

The domain has no direct competitor doing cross-system business logic extraction, but adjacent tools exist:

| Category | Tools | What They Do | Gap vs. Vulgata |
|----------|-------|-------------|-----------------|
| **AI Code Documentation** | Google Code Wiki, Swimm, Mintlify, Zread.ai | Auto-generate docs from single repos; some support auto-update | Single-repo only; no cross-system tracing; no business logic classification |
| **Code Search & Understanding** | Sourcegraph Cody, CodeGraph | Semantic code search with AI Q&A; dependency graph visualization | Search-oriented, not document-generation; no business logic layer |
| **Static Analysis** | SonarQube, CodeQL, Snyk Code | Rule-based code quality and security scanning | No business logic extraction; rule-based, not LLM-native |
| **Enterprise Knowledge Management** | Confluence, Gitee Wiki, PingCode Wiki | Document storage and collaboration | Manual documentation; not code-derived |
| **AI Coding Agents** | Cursor, Claude Code, Devin | Code generation and editing with repo-level context | Generate code, don't extract business knowledge |

**Key insight:** No existing product combines (a) multi-repo scanning, (b) cross-system dependency tracing, (c) business logic classification, and (d) LLM-native understanding. Vulgata occupies a unique niche.

### 3.3 Technology Trends

1. **From Code Completion to Code Understanding**: The AI coding market is in its third generation — from inline completion (2021-2023) to chat-based assistance (2023-2024) to autonomous agents (2025-2026). Vulgata aligns with the agent generation.

2. **LLM vs. Static Analysis Convergence**: Academic research (Gnieciak & Szandala, 2025) shows LLMs outperform static analysis tools in recall (finding real issues) but have higher false positive rates. The industry trend is hybrid approaches — LLM for semantic understanding, static analysis for deterministic checks.

3. **Spec-Driven Development (SDD)**: A 2026 paradigm shift where "specifications are the primary artifact, code is derived from them." Tools like OpenSpec and Kiro treat specs as version-controlled, executable artifacts. Vulgata inverts this — extracting specs *from* existing code.

4. **MCP as Universal Connector**: MCP is becoming the standard protocol for AI-tool integration. Microsoft Agent Framework, VS Code, and major AI tools have adopted it. Vulgata's MCP integration for external AI agents aligns with this trend.

5. **Multi-Agent Architectures**: The Orchestrator-Workers pattern (formalized by Anthropic) and Magentic orchestration (Microsoft) are production-proven patterns. Code Broker (Attrah, 2026) demonstrates a similar 5-agent architecture for code quality assessment using Google ADK.

---

## 4. Technology Deep-Dive

### 4.1 LLM-Powered Code Understanding

**Current State (2026):**
- Claude Opus 4.6 achieves 80.8% on SWE-bench (software engineering tasks)
- Long context windows (200K-1M tokens) enable repository-level understanding
- DeepSeek V4 offers competitive performance at significantly lower cost (<1 CNY/M input tokens)
- LLMs outperform static analysis in semantic understanding but have higher hallucination risk

**Key Research Findings:**
- LLMs achieve higher F1 scores and recall than static analysis tools for vulnerability detection, but produce more false positives (Gnieciak & Szandala, 2025)
- Multi-LLM orchestration (PerfOrch, Chen et al., 2025) shows that different models have complementary strengths — no single model dominates across all code understanding tasks
- Hybrid architectures (Tree-sitter static parsing + LLM semantic reasoning) are emerging as best practice (CodeGraph + Understand-Anything)

### 4.2 Multi-Agent Code Analysis Architecture

The dominant pattern for code analysis agents is **Orchestrator-Workers**:

```
Orchestrator (Manager)
  ├── Plans: decomposes codebase into code units
  ├── Dispatches: assigns units to worker agents
  ├── Tracks: monitors progress, detects stalls
  └── Aggregates: collects results, resolves cross-references

Worker Agents (Specialized)
  ├── Code Reader: reads and understands individual code units
  ├── Document Producer: generates structured documentation
  ├── Uncertainty Resolver: identifies and tracks ambiguities
  └── Auditor (optional): verifies document quality
```

**Production examples:**
- **Code Broker** (Attrah, 2026): 5-agent system (Orchestrator → Pipeline → 3 parallel assessors → Recommender) for code quality assessment
- **Claude Code**: Multi-agent with Orchestrator-Workers, independent agent sessions, parallel dispatch
- **Microsoft Agent Framework**: Built-in Magentic orchestration with manager, planning, progress tracking, and HITL

### 4.3 Static Analysis vs. LLM-Based Analysis

| Dimension | Static Analysis | LLM-Based Analysis |
|-----------|----------------|-------------------|
| **Precision** | High (deterministic rules) | Medium (generative, variable) |
| **Recall** | Low-Medium (rule coverage limited) | High (semantic understanding) |
| **Business Logic** | Cannot extract | Can extract and explain |
| **Cross-File Reasoning** | Limited (data flow analysis) | Strong (contextual understanding) |
| **False Positives** | Low-Medium | Higher |
| **Speed** | Fast (milliseconds) | Slow (seconds per unit) |
| **Cost** | Low (compute only) | Medium-High (LLM API costs) |

**Best practice:** Hybrid approach — static analysis for deterministic structure extraction, LLM for semantic understanding and business logic inference.

---

## 5. Industry Patterns & Enterprise Context

### 5.1 How Enterprises Document Business Logic Today

Current enterprise practice for business logic documentation:

1. **Oral Tradition** (most common): Knowledge lives in senior engineers' heads. Lost when they leave.
2. **Scattered Wikis**: Confluence, Notion, or SharePoint pages — manually written, rarely updated.
3. **Code Comments**: Inline documentation that developers write (or don't). Only technical, not business-facing.
4. **Architecture Diagrams**: High-level system diagrams, rarely connected to specific code.
5. **Compliance Documents**: Regulatory mapping documents, manually maintained, often outdated.

**The gap:** None of these connect business logic to the actual running code in a verifiable, auto-updating way. A typical enterprise has:
- 68%+ of organizations undergoing engineering transformation (2024 DevSecOps Whitepaper)
- Documentation update latency exceeding 40% behind code changes
- New team members spending 2-6 weeks understanding codebases
- 23% of production incidents caused by outdated documentation

### 5.2 The "Code as Specification" Reality

In large enterprises, especially regulated ones (banking, finance), the code *is* the specification:
- Business rules are encoded in code, not in documents
- Compliance verification requires tracing code to regulations
- System behavior can only be definitively known by reading the code
- Multiple systems implement parts of the same business process

This is the core problem Vulgata addresses — making the implicit specification (code) explicit and accessible.

---

## 6. Emerging Trends Relevant to Vulgata

### 6.1 AI-Native Knowledge Management

- **"Code as Knowledge" movement**: Treating codebases as knowledge assets, not just executable artifacts
- **AI Code Wiki**: Google's Code Wiki automatically generates and maintains documentation from code — closest existing analog to Vulgata's document generation
- **Doc Drift elimination**: Tools like Swimm and Mintlify focus on keeping docs in sync with code

### 6.2 Spec-Driven Development (SDD)

The 2026 trend of "Specification as the primary artifact" is complementary to Vulgata:
- SDD: Write specs → generate code (forward direction)
- Vulgata: Read code → extract specs (reverse direction)
- Together they form a complete loop: specs → code → verified specs

### 6.3 MCP Ecosystem Growth

MCP is rapidly becoming the standard for AI-tool integration:
- Microsoft Agent Framework has native MCP support with `InvokeMcpTool` action
- VS Code, Cursor, and Claude Desktop all support MCP servers
- Microsoft Fabric exposes data agents as MCP servers
- This validates Vulgata's MCP integration strategy

### 6.4 Cost Trajectory

LLM inference costs are dropping rapidly:
- DeepSeek V4: <1 CNY per million input tokens
- Input caching further reduces costs for repeated code analysis
- On-premise/offline models (32B parameter class) can run on local GPUs for air-gapped deployments
- Cost is increasingly not a barrier for code analysis at enterprise scale

---

## 7. Research Methodology

- **Sources**: Market research reports (Grand View Research, Gartner, QYResearch, YHResearch), academic papers (arXiv), industry documentation (Microsoft Learn, IBM Think), product documentation, and technical blog posts
- **Date range**: 2024-2026, with emphasis on current (2026) data
- **Verification**: Multi-source triangulation for market sizes; primary sources (Microsoft docs, academic papers) for technical claims
- **Confidence levels**: "Established" = widely adopted industry term; "Emerging" = gaining traction but not yet standard; "Vulgata-specific" = coined for this project

---

## 8. Key Takeaways for Vulgata

1. **Unique positioning**: No existing product does cross-system business logic extraction — Vulgata occupies an uncontested niche at the intersection of AI code tools, static analysis, and enterprise knowledge management

2. **Terminology gap**: The industry has no standard term for "code-to-business-knowledge extraction." Vulgata can define this category. The coined terms in Section 2.4 should be adopted as the project's ubiquitous language.

3. **Architecture validation**: The Orchestrator-Workers pattern and Magentic orchestration are production-proven. Vulgata's multi-agent architecture aligns with industry best practices.

4. **MCP alignment**: MCP is the right protocol choice for external integration. It's becoming the industry standard.

5. **Hybrid approach**: Combining LLM semantic understanding with deterministic structure extraction (pre-scan profiling, pattern catalogs) is the emerging best practice — validating the RPC Detection Layer approach from the brainstorming session.

6. **Cost is not a barrier**: With DeepSeek V4 pricing and input caching, LLM costs for demo-scale code analysis are negligible.

7. **Living Documentation** is the right paradigm: Vulgata's auto-updating, code-synchronized documents align with the industry's move toward living documentation.
