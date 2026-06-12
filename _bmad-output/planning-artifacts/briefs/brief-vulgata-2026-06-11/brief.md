---
title: "Product Brief: Vulgata"
status: final
created: 2026-06-11
updated: 2026-06-12
---

# Product Brief: Vulgata

## Executive Summary

Vulgata is an LLM-powered knowledge extraction platform that translates source code across multiple systems and repositories into structured, traceable business knowledge — accessible to everyone, not just developers. Named after the Latin Vulgate, the translation that brought sacred texts to ordinary people, it bridges the gap between what code does and what the business needs to know.

In large enterprises, business logic is scattered across dozens of systems in different languages and architectures. Existing tools document single repositories but fail at the cross-system picture. Vulgata solves this with a multi-agent scanning architecture: orchestrator agents dispatch worker agents that traverse codebases, extract business logic into linked documents, and resolve cross-system dependencies through an uncertainty resolution system. Users interact through a Blazor web dashboard and chat interface; AI coding agents consume the knowledge via MCP.

Built on .NET 10, ASP.NET Core, Blazor, and the Microsoft Agent Framework, Vulgata is being developed for an AI programming competition with a two-month timeline, targeting a live demo that traces a real cross-system business flow end-to-end.

## The Problem

In a large bank, business logic is not in one place. It lives in the source code of dozens of systems — each with multiple repositories, written in different languages, built on different architectures, maintained by different teams. A single business flow — say, a risk evaluation in the mobile banking app — can touch six or more systems, each calling the next through RPCs, message queues, or database reads. The code *is* the specification, and the specification is invisible.

The people who need to understand this logic the most are the ones least equipped to read the code. Product managers proposing new features cannot trace how things work today. New hires spend months piecing together business knowledge through oral tradition and scattered wikis. Compliance officers cannot verify that a regulatory rule is correctly implemented across all affected systems. Developers building new features break things they did not know existed because no single person — and no single document — holds the full picture.

Existing tools address fragments of this problem. Single-repository documentation generators produce wiki pages from one codebase at a time, but they cannot connect the dots across systems. Cross-repository analysis tools exist, but they are complex to configure, require expensive licenses, and are difficult to deploy and maintain in a regulated enterprise environment. The result is a persistent knowledge gap: the business logic that actually runs the bank is known only to the code itself, and reading the code is slow, expensive, and error-prone.

The cost is real. Feature proposals start from ignorance. Incidents take longer to resolve because no one can trace the blast radius. Institutional knowledge walks out the door when senior engineers leave. The bank operates systems it does not fully understand — and in a regulated industry, that is not just inefficient, it is dangerous.

## Who This Serves

Vulgata serves a spectrum of users across the enterprise, each interacting with the same knowledge base at different levels of abstraction.

**Product managers and business analysts** are the primary non-technical audience. They need to understand how existing business logic works before proposing changes — "what happens during a risk evaluation?", "which systems are involved in loan approval?", "where is this regulatory rule implemented?" Today they rely on asking developers and waiting days for answers. With Vulgata, they ask the chat interface directly and receive a business-level narrative, not code.

**Compliance officers and auditors** need to verify that regulatory requirements are correctly implemented across all affected systems. A single regulation may touch a dozen repositories; tracing compliance manually is slow and error-prone. Vulgata provides a cross-system view that makes compliance verification auditable and repeatable.

**New team members** — whether business or technical — spend their first months piecing together institutional knowledge through conversations and outdated documentation. Vulgata gives them a self-service way to explore how the systems they will work with actually behave, dramatically shortening onboarding time.

**Developers and architects** consume both code logic and business logic documents. When building a new feature, they can trace the existing flow end-to-end before writing a single line of code. When debugging an incident, they can immediately see which systems are in the blast radius. The MCP integration also allows AI coding assistants to query Vulgata's knowledge base, making it part of the development workflow itself.

**System administrators and operators** manage the Vulgata instance — configuring systems and repositories, managing user access and permissions, monitoring scan progress, and resolving uncertainties that agents could not handle automatically. These are typically IT staff within the department that owns the scanned systems.

## The Solution

Vulgata scans source code across multiple systems and repositories, using a team of LLM-powered agents to read, understand, and document the business logic embedded in the code. The output is a structured, linked knowledge graph where every business rule traces back to the code that implements it, and every cross-system dependency is explicitly mapped.

### How Scanning Works

For each repository, an orchestrator agent dispatches worker agents to process code units — functions, methods, classes, files — one by one. Each agent reads the code, identifies the business logic, and produces a structured document linked to its source location. Documents are organized in the same tree structure as the source code, making navigation intuitive. Documents fall into two categories: *code logic documents* describe what the code does structurally; *business logic documents* describe the business rules and processes built on top of that code. Non-technical users can focus on business logic documents; developers and AI agents can consume both.

### Cross-System Intelligence

When an agent encounters code that calls another system — an RPC, a message queue, a shared database — it records an uncertainty. A dedicated uncertainty resolution system picks this up: if the target system has already been scanned, it retrieves the relevant document and links it; if the target system is currently being scanned, the source system waits for that scan to complete (avoiding deadlocks) but continues responding to queries from other systems in the meantime; if not yet scanned, the question is queued or surfaced to the user. The result is that Vulgata can answer questions like "what happens during a risk evaluation?" by tracing the flow across every system involved, linking each step to the code that implements it.

### Third-Party Libraries

Dependencies on external libraries are handled in two categories. For libraries where the user owns the source code, the library can be added as a standalone repository not belonging to any system. Scanning is partial and on-demand: only the code units actually called by other repositories are scanned, triggered by the agent responsible for the caller, with a lock mechanism preventing concurrent scanning conflicts. For libraries without available source code, well-known common libraries are treated as understood and do not count as cross-system boundaries. Unknown libraries trigger an inference attempt; if inference fails, the user is notified to provide context manually.

### Human in the Loop

During scanning, agents may encounter ambiguities they cannot resolve from code alone — undocumented business rules, external system behaviors, domain-specific conventions. These are surfaced to users as questions. User answers are marked as human input and carry lower priority than code-derived facts when conflicts arise. Users can also proactively supplement systems and repositories with context documents, database connections, and custom MCP tools to improve scan quality.

### Living Knowledge

Vulgata monitors connected git repositories. When changes are pushed, it incrementally re-scans only the affected code units, updates linked documents, and re-evaluates related uncertainties. Document history is preserved, allowing users to see what changed and when.

### Interaction

Users access Vulgata through a Blazor web application with two primary interfaces: a management dashboard for configuring systems, repositories, and scans; and a chat interface where users ask natural-language questions and receive answers grounded in the extracted knowledge. Users can specify which systems or repositories to query, upload supplementary files, and view scan progress in real time. External AI coding agents can also query Vulgata's knowledge base via MCP, making it a knowledge backend for the entire development toolchain.

## What Makes This Different

Most code documentation tools operate within a single repository. They generate API docs, call graphs, or wiki pages — useful, but blind to the reality that enterprise business logic spans many systems. The few tools that attempt cross-repository analysis are heavyweight enterprise platforms: expensive, complex to configure, and difficult to integrate into existing workflows.

Vulgata takes a fundamentally different approach on four fronts:

**Cross-system by design, not by accident.** Vulgata's scanning architecture treats cross-system dependencies as first-class citizens. When code in System A calls System B, that is not a dead end — it is the start of a trace. The uncertainty resolution system actively pursues these connections, linking documents across repository boundaries to build a complete picture of how business logic flows.

**LLM-native, not LLM-wrapped.** Vulgata does not use LLMs as a thin summarization layer on top of static analysis. The agents *are* the analysis engine — reading code, reasoning about business intent, asking clarifying questions, and resolving ambiguities through conversation with users and other agents. This allows Vulgata to handle the messy, convention-driven, poorly-documented reality of enterprise codebases in ways that rule-based static analysis cannot.

**Knowledge for everyone, not just developers.** The document classification system — code logic vs. business logic — means that a product manager asking "what happens during risk evaluation?" sees a business process narrative, not a stack trace. A developer tracing the same flow sees the full technical detail. Both views are grounded in the same source of truth. And with MCP integration, even other AI agents can consume this knowledge, making Vulgata a knowledge layer for the entire toolchain.

**Lightweight and self-contained.** Vulgata runs as a single deployable web application. No enterprise license, no multi-week onboarding, no consultant-led configuration. Connect a git repository, start a scan, ask questions. This makes it viable for teams and departments that would never get budget approval for a heavyweight enterprise platform — and makes it a compelling competition demo that can be shown working end-to-end in minutes.

## Success Criteria

**Competition demo.** The primary success criterion for V1 is a working end-to-end demo: connect multiple real repositories from the bank's systems, run a full scan, and answer a cross-system business question — such as "what happens during a risk evaluation in the mobile app?" — with a trace that links every step to the source code that implements it. The demo must run live, not as a recording.

**Scan quality.** For the demo repositories, the scan must produce documents that a non-technical reviewer (a product manager or business analyst) can read and understand without developer assistance. Cross-system links must be accurate: when System A calls System B, the document for System A must reference the correct document in System B.

**Incremental update.** After an initial scan, pushing a change to one of the connected repositories must trigger an incremental re-scan that updates only the affected documents, completing within minutes rather than hours, and preserving document history.

**User experience.** A non-technical user must be able to log in, select a system, and ask a business question in the chat interface without training or documentation. The management dashboard must show scan progress in real time and surface agent questions clearly.

## Scope

**In scope for V1 (competition demo, August 15):**

- Blazor web application with authentication, a management dashboard, and a chat interface
- System and repository management: create, configure, and link systems and repositories
- Multi-agent scanning: orchestrator dispatches worker agents to process code units, producing structured documents in a source-tree-aligned hierarchy
- Document classification: code logic documents and business logic documents, with cross-system links
- Cross-system uncertainty resolution: detect calls across system boundaries, link to scanned targets, queue unresolved targets, surface questions to users
- Third-party library handling: standalone repos with on-demand partial scanning and lock mechanism; inference for unknown libraries without source
- Human-in-the-loop: agents surface ambiguities as questions; user answers marked as human input with lower priority than code-derived facts
- Git monitoring and incremental re-scan: detect remote changes, re-scan only affected code units, preserve document history
- Database connection tools: LLM can inspect schema and query sample data from configured databases
- Document search and retrieval: beyond basic keyword search, candidates include LLM-powered semantic search and vector database indexing to support effective cross-document queries
- Supplementary context: users can upload files and add context to systems and repositories
- Real-time scan progress: dashboard shows agent count, files processed, errors, and pending questions
- MCP integration: external AI coding agents can query Vulgata's knowledge base; MCP tools customizable at repo, system, or global level *(lowest priority — may be deferred if time is tight)*

**Explicitly out of scope for V1:**

- Full RBAC with fine-grained permissions (basic authentication and authorization is sufficient)
- Multi-language scanning beyond the languages present in the demo repositories
- Automated compliance reporting or audit trail generation
- High-availability deployment or multi-instance orchestration
- Integration with enterprise SSO or identity providers

## Vision

Vulgata's competition demo is a proof of concept — two systems, a handful of repositories, one compelling cross-system trace. The vision is larger.

### Near Term

Vulgata becomes the knowledge backbone for the department that builds it. Every system the department owns is scanned and continuously updated. Product managers, developers, and new hires all use Vulgata as their first stop when they need to understand how something works. The days of "ask the senior engineer who built it five years ago" are over — the knowledge is in the system, not in people's heads. The architecture is designed for extension — supporting additional repositories, systems, languages, and deployment scenarios beyond the demo scope.

### Medium Term

Vulgata expands across the organization. Other departments connect their repositories. The knowledge graph grows to span dozens of systems, and the cross-system traces become richer and more valuable with every new connection. Compliance teams use Vulgata to verify regulatory implementation across the entire technology estate. Incident response teams use it to map blast radius in seconds. Architecture decisions are informed by a complete, living map of how business logic actually flows — not how people think it flows.

### Long Term

Vulgata becomes a standard part of the enterprise development lifecycle. New projects are scanned from day one. Code reviews are augmented by Vulgata's understanding of cross-system impact. AI coding agents query Vulgata as naturally as they query a language server — it is simply part of the toolchain. The gap between "what the business thinks the systems do" and "what the code actually does" closes, and stays closed.
