---
title: "PRFAQ: Vulgata"
stage: verdict
created: 2026-06-12
updated: 2026-06-12
inputs:
  - _bmad-output/planning-artifacts/briefs/brief-vulgata-2026-06-11/brief.md
  - _bmad-output/planning-artifacts/briefs/brief-vulgata-2026-06-11/addendum.md
  - _bmad-output/planning-artifacts/briefs/brief-vulgata-2026-06-11/.decision-log.md
  - _bmad-output/brainstorming/brainstorming-session-2026-06-12-2359.md
  - _bmad-output/planning-artifacts/research/domain-enterprise-code-analysis-llm-business-logic-extraction-research-2026-06-12.md
  - _bmad-output/planning-artifacts/research/technical-vulgata-core-technologies-research-2026-06-12.md
  - docs/requirement-draft.md
  - docs/judge-rubric.txt
---

# PRFAQ: Vulgata

## Press Release

**FOR IMMEDIATE RELEASE**

### Introducing Vulgata: Know What Your Systems Actually Do

**June 2026 —** Today, we're announcing Vulgata, a platform that reads source code across every system in the organization and produces business knowledge that anyone can understand — product managers, compliance officers, new team members, and developers alike.

For years, understanding how business logic actually works across our systems has meant one thing: finding a senior developer who knows the code, and hoping they remember. When the business team redesigned the fund details page last quarter — new UI, new data source — nobody could trace what the existing page actually did across the systems it touched. They shipped from mockups. Functionality was lost. It took two weeks to discover what broke.

That problem is now solved.

Vulgata scans every repository connected to a business flow — mobile apps, backend services, middleware — and produces a complete, linked map of how business logic flows across systems. Product managers ask "what happens during a risk evaluation?" and receive a step-by-step business narrative, with every claim linked to the source code that backs it. Compliance officers trace a regulatory rule across all twelve affected systems in minutes, not weeks. New team members explore how systems actually behave on their first day, not their third month.

**How it works.** Connect a git repository. Vulgata's AI agents read the source code — function by function, file by file — and produce structured documents that explain both what the code does and what business rules it implements. When one system calls another, Vulgata traces the connection, resolves the dependency, and links the documents together. The result is a living map of business logic that updates when code changes.

**Built for everyone.** Every document comes in two forms: a business-level narrative for non-technical readers, and a full technical view for developers. Both are grounded in the same source of truth — the code itself. When Vulgata makes a claim about business logic, it links directly to the code that implements it. You can verify. You can trace. You are never asked to trust a black box.

**What Vulgata does not do.** Vulgata reads source code, not production data. It does not access live databases, customer records, or transaction logs. It analyzes the logic written in code — not the data that flows through it at runtime. For systems whose behavior depends on runtime state or deployment topology, Vulgata surfaces what it can determine from code and flags what it cannot — so you know the boundary between known and unknown. *(Future: agents may access deployment or runtime data via MCP when explicitly permitted and authorized.)*

**Available today.** Vulgata runs as a single web application. No enterprise license. No multi-week onboarding. Connect a repository, start a scan, ask a question. The knowledge that was locked in code is now accessible to everyone who needs it.

Vulgata takes its name from the Latin *vulgata editio* — the "common edition," a translation that made knowledge accessible to all. That is our mission: to make the business logic embedded in code accessible to everyone in the organization.

## Customer FAQ

### For Product Managers

**Q: You say every claim links back to source code. But how do I know the AI didn't misunderstand the code? If I make a business decision based on what Vulgata tells me, and it's wrong — who's responsible?**

Every document can be traced back to its source code, and documents are generated with cross-verification between agents. However, like all LLM-powered applications, Vulgata's answers should be treated with appropriate caution. Vulgata provides traceability — you can always follow the link to the source code and verify for yourself — but it does not provide infallibility. The responsible party for business decisions remains the human making them. Vulgata is a tool for understanding, not a substitute for judgment.

**Q: I don't want to read code. I don't want to see code. If I ask "what happens during a risk evaluation," will I get a business answer or will I get a stack trace with some commentary?**

Vulgata's chat interface provides two modes: Business Mode and Developer Mode. In Business Mode, answers are framed as business process narratives — step-by-step descriptions of what happens, which systems are involved, and what business rules apply, without code-level detail. Developer Mode provides the full technical view with code references. You choose the mode that matches your needs.

**Q: Our systems change every sprint. If I read a Vulgata document today, how do I know it's still accurate next week?**

Vulgata monitors connected git repositories. When changes are pushed, it incrementally re-scans only the affected code units and updates the linked documents. You can see when each document was last updated and what triggered the update. The knowledge stays current with the code.

### For Compliance Officers

**Q: You say Vulgata doesn't access production data. But it reads source code. Source code contains database schemas, table names, field names, API endpoints. Is that not sensitive information? Where does the scanned data live? Who can access it?**

Vulgata can be deployed with a self-hosted LLM backend, meaning all scanning and query processing happens within the organization's own infrastructure. No source code or generated documents leave the organization's network. Access to scanned documents is controlled through Vulgata's authentication and authorization system — only users with appropriate permissions can view documents for a given system or repository.

**Q: If I use Vulgata to verify that a regulatory rule is implemented across twelve systems, can I export that verification as an audit artifact? Or is it just a chat conversation that disappears?**

Vulgata's output should not be used directly as audit reports without human review. The documents and chat responses are LLM-generated and carry inherent uncertainty. They are a starting point for compliance verification — showing you where to look and what to verify — not a replacement for formal audit procedures. Chat conversations and generated documents are persisted and can be referenced, but formal audit artifacts require human screening and validation.

### For Developers

**Q: Our codebase is a mess. Generated code, legacy spaghetti, frameworks doing magic through annotations and reflection. Can your agents actually make sense of that?**

Users can provide supplementary context before scanning — documentation, architecture notes, framework descriptions — to help agents understand the codebase. During scanning, the human-in-the-loop system allows developers to answer agent questions and clarify ambiguities in real time. Vulgata does not claim to perfectly understand every codebase on the first pass. It gets better with human input, and it flags what it cannot determine.

**Q: We have systems that call each other through six different RPC mechanisms — some documented, some not. How does Vulgata even know that System A is calling System B?**

Vulgata includes a dedicated RPC Detection Layer. Before scanning, you configure a per-system pattern catalog that tells agents how each system makes remote calls — SOFA RPC, HTTP, message queues, shared databases. The detection layer combines pattern-based detection with object flow analysis (tracing how RPC client objects are created) and service topology resolution (mapping service codes to target systems). This is configurable per system, and developers can refine the patterns during the human-in-the-loop process.

### For New Team Members

**Q: I just joined. I don't even know which systems exist, let alone which repositories to scan. Where do I start?**

Vulgata's dashboard provides a system overview once repositories are configured by your team's administrator. Pre-scan profiling identifies the languages, frameworks, and structure of each repository. You don't need to know the codebase to start — you can ask the chat interface a business question like "how does loan approval work?" and Vulgata will guide you to the relevant systems and documents. The auto-generated index (index.md) catalogs every document with a one-line summary, so you can browse before you ask.

### For System Administrators

**Q: You say "connect a repository, start a scan." How long does a scan take? Our main repo has 200,000+ files.**

Vulgata uses CodeGraph — a pre-indexed code intelligence system — to accelerate scanning by providing structural information (call graphs, symbol resolution, dependency maps) without requiring agents to re-discover it from raw files. Pre-scan profiling filters out non-code files before agents are dispatched. For very large repositories, scanning is incremental and parallelized across multiple worker agents. Exact scan time depends on repository size, complexity, and available compute, but the architecture is designed to scale horizontally.

**Q: This thing uses LLMs. Every query costs tokens. Who pays for that? What happens if someone runs a thousand queries in an hour?**

Vulgata uses DeepSeek V4, which costs less than 1 CNY per million input tokens — making per-query costs negligible for normal usage. For organizations with stricter requirements, Vulgata supports self-hosted LLM backends, eliminating per-token costs entirely. Rate limiting and usage monitoring are built into the platform to prevent abuse. The primary cost driver is the initial scan, not ongoing queries — and that cost is one-time per code change.

## Internal FAQ

### Feasibility & Architecture

**Q: The Microsoft Agent Framework is prerelease. Magentic orchestration is untested for custom agent types. What happens if the Week 1 spike fails?**

Fallback to a simpler agent architecture — either a different multi-agent pattern within the framework or even a single-agent approach. The core value of Vulgata is in the document generation pipeline and cross-system tracing, not in the specific orchestration pattern. Magentic is the preferred path because it maps naturally to the orchestrator-workers model, but it is not the only path. The Week 1 spike exists precisely to validate or reject this dependency before we commit.

**Q: You're building a multi-agent system, a Blazor web app, an RPC detection layer, a live graph visualization, and an LLM-wiki search system — in 9 weeks, with two people. What gets cut first when the timeline slips?**

The web application features — authentication, dashboard, system management, chat interface — are common patterns that AI coding assistants can handle rapidly, especially since we are not building complex custom UI. The majority of our time will be spent on prompt engineering and tuning: the RPC detection prompts, the document generation prompts, the uncertainty resolution prompts. If the timeline slips, the live graph visualization and MCP integration are the first candidates for reduction — they enhance the demo but are not core to the scanning pipeline. The core scanning pipeline (profile → scan → classify → link) is non-negotiable.

**Q: The RPC Detection Layer depends on per-system pattern catalogs. Who writes those? How many patterns does a typical system need? What if the demo systems use RPC mechanisms you haven't seen before?**

The pattern catalog starts as a configuration file per system, written by the developer setting up the scan. For the demo, this is us — we know our own systems. A typical system may need 3-5 patterns covering its primary RPC mechanisms. If a system uses an RPC mechanism not covered by the catalog, the LLM may misclassify it as a normal third-party library invocation. To mitigate this, global-level background knowledge about the organization's common RPC frameworks can be injected into all agent prompts, providing a baseline even when per-system catalogs are incomplete. The Prompt Workbench exists to iterate on these patterns before production scans.

**Q: LLMs hallucinate. Your agents are generating business logic documents from code. How do you know the documents are correct? What's your verification strategy?**

We don't have mathematical guarantees of correctness — no LLM-based system does. Our verification strategy is multi-layered: (1) every claim in a document links to the source code line that generated it, enabling human verification; (2) documents are generated with cross-verification between agents where possible; (3) the human-in-the-loop system surfaces ambiguities for developer review during scanning; (4) the Document Immutability Principle means documents are read-only projections — they can only change when code changes and is re-scanned, creating a clear audit trail. For V1, we accept that some documents will contain errors and focus on making those errors traceable and correctable.

### Quality & Trust

**Q: The 3-state uncertainty model sounds elegant, but what happens when a "discovered-but-unscanned" dependency never gets resolved? How many dangling links is too many?**

Dangling links are acceptable. The 3-state model's value is not in resolving every dependency — it's in recording that a dependency exists even when it cannot be resolved. A "discovered-but-unscanned" link tells the user: "System A calls something at this endpoint. We don't know what yet, but we know the call exists." This is strictly better than the alternative — silently missing the cross-system call entirely. There is no threshold for "too many" dangling links; each one is a known unknown, which is more valuable than an unknown unknown.

### Competition & Differentiation

**Q: Google launched Code Wiki in November 2025. Cognition AI launched DeepWiki. Both do "AI documentation you can talk to." What stops them from adding cross-repo support and eating your niche?**

Cross-system boundary handling is genuinely difficult — it's not a feature you can bolt onto a single-repo documentation tool. It requires: detecting that a call crosses a system boundary (RPC Detection Layer), resolving which system is being called (Service Topology), managing scan dependencies across systems (3-state uncertainty model with deadlock avoidance), and linking documents across repository boundaries. Most competitors will not want to handle this complexity because their target market — individual development teams documenting their own repos — doesn't need it. Our target market — large enterprises with dozens of interconnected systems — does. The complexity is the moat.

**Q: The competition rubric allocates 30 points to innovation. If another team builds something similar — enterprise knowledge extraction with LLMs — how do you differentiate in a 10-minute demo?**

The cross-system trace is the differentiator. Anyone can demo "ask a question about one repo and get an answer." Vulgata's demo shows a question that spans multiple systems — "what happens during a risk evaluation?" — and traces the flow across repositories, with each step linked to source code. The live knowledge graph visualization makes this tangible: as the trace resolves, nodes appear and cross-system links light up. The demo doesn't need to explain why cross-system is hard; it shows the result. The complexity is invisible to the audience but unmistakable to technical judges.

### Demo & Presentation

**Q: A live demo is your primary success criterion. What's your backup plan if the scan crashes, the LLM API is down, or the cross-system trace produces wrong results during the presentation?**

Scanning is not a quick operation and will not be performed live during the demo. The demo will use pre-scanned repositories with verified documents. The live portion of the demo is the chat interaction — asking questions and receiving answers grounded in the pre-scanned knowledge base. This eliminates the risk of scan failures during the presentation. For LLM API failures, we maintain a fallback to a secondary provider or a cached set of pre-generated answers for the demo questions. The demo script includes 3-5 verified questions with known-good answers as a safety net.

**Q: The demo needs to impress non-technical judges (UX, business value) and technical judges (architecture, complexity). How do you structure a 10-minute demo to satisfy both?**

The demo is structured in three acts: (1) **The Problem** (1 min) — the fund details page story, establishing why this matters; (2) **The Experience** (4 min) — a non-technical user asks a business question in Business Mode, receives a narrative answer, and the live knowledge graph shows the cross-system trace resolving; (3) **Under the Hood** (5 min) — switch to Developer Mode, show the same question with full code references, walk through the document tree, demonstrate the RPC Detection Layer configuration, and show the scan dashboard. Non-technical judges are satisfied by Act 2; technical judges by Act 3. Both see the same system from different angles.

## The Verdict

### What Survived the Gauntlet

**The cross-system trace is the real differentiator.** Every round of questioning — customer FAQ, internal FAQ, competitive analysis — reinforced that cross-system boundary handling is the hard problem nobody else wants to solve. Google Code Wiki and DeepWiki document single repos. Vulgata traces business logic across them. The complexity is the moat.

**The problem is real and specific.** The fund details page story — business team redesigns a page from mockups, functionality is lost, two weeks to discover what broke — is not hypothetical. It happened. It will happen again. This is the emotional anchor of the entire concept.

**The architecture is honest about its limitations.** The PRFAQ process forced us to articulate what Vulgata cannot do: it cannot guarantee correctness (no LLM system can), it cannot access production data (by design), it cannot resolve every cross-system dependency (dangling links are acceptable). These are not weaknesses to hide — they are boundaries that build trust.

**The demo strategy is solid.** Pre-scanned repositories eliminate the risk of live scan failures. The three-act structure (Problem → Experience → Under the Hood) satisfies both non-technical and technical judges. The live knowledge graph visualization makes the cross-system trace tangible without requiring explanation.

### What Keeps Us Up at Night

**Nine weeks is tight.** The majority of development time will be spent on prompt engineering — tuning the RPC detection prompts, the document generation prompts, the uncertainty resolution prompts. This is inherently iterative and unpredictable. The web application and infrastructure are the easy parts; the prompts are the product.

**The Magentic spike is a binary gate.** If the Week 1 validation fails, we fall back to a simpler agent architecture. The fallback exists, but it changes the development trajectory. This is the single highest-risk decision in the first week.

**RPC detection quality is unknown until tested.** The Prompt Workbench exists to iterate rapidly, but we won't know if the RPC Detection Layer works on our actual demo repositories until we build it and run it. The global background knowledge injection is a mitigation, not a guarantee.

**Hallucination is systemic.** We have no mathematical guarantee that generated documents are correct. Our mitigation — source code links, cross-verification, human-in-the-loop — makes errors traceable but does not prevent them. For a competition demo with verified questions, this is acceptable. For production deployment, it requires ongoing vigilance.

### Open Questions for the PRD

- What are the specific demo repositories, and do they contain compelling cross-system call patterns?
- What RPC mechanisms do the demo systems use, and can the RPC Detection Layer handle them?
- Will the business-level documents produced by the scanning pipeline actually be readable by a non-technical reviewer?
- What is the fallback LLM provider if DeepSeek V4 is unavailable during the demo?

### Verdict: Ready for PRD

The concept has survived the Working Backwards process. The press release is compelling. The customer FAQ addresses the hardest questions honestly. The internal FAQ surfaces real risks with real mitigations. The concept is not perfect — no concept is — but it is battle-tested. The team knows what they're building, why it matters, what could go wrong, and what they'll do about it.

**Next step:** Move to PRD creation (`bmad-prd`) to formalize requirements, acceptance criteria, and the implementation roadmap.

