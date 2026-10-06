---
title: October first deliverable - four feature inputs and integration
date: 2026-09-30
updated: 2026-10-06
status: first-deliverable-planning-inputs
scope: october-14-first-deliverable
implementation_authorized: false
---

# October first deliverable

The October 6 user instruction replaces the earlier full-story packaging: build
the first useful version of four features from integrated 18.2-18.4, reuse valuable
18.5 design, and reserve an explicit integration phase. Full 18.5 execution is
removed from this delivery path. Its original plan/evidence remain history; its
planning stop is not a prerequisite for implementing the selected ideas elsewhere.

These briefs define the selected first deliverable. Earlier October stories are
requirement/provenance references; their full completion dependencies, skipped tool
tasks and unselected acceptance matrices must not silently expand these inputs.
The [October proposal](sprint-change-proposal-2026-09-26-october-batch-release.md)
continues to supply compatible scope, integrity and presentation rules. Original
story and generic-product completion is not claimed by delivering this subset.
The October 5 contingency remains a dated proposal; this plan updates its baseline,
18.5 disposition and working dates. No skills are activated by this document.

## Integrated starting point

Verified main baseline: 57fad124bb0904bf51788e85f4823271291fcfe1, branch oct14/main.
Vulgata Master Saver (019fcbe8-f5db-73b3-914c-b29f75688a22) reports the integration
of 18.4, narrowed JDT and narrowed CodeGraph, with 2,281 SelfContained and 13 workflow
regression passes plus successful Web/generator builds. Native/PostgreSQL evidence
remains the workers' scoped results; no tests were rerun to write these inputs.

- **Source/tools:** 18.1 source access plus independent dedicated JDT navigation
  and in-place CodeGraph preparation/returned MCP descriptor are available. Source
  authority, ordinary-reader/tool-state coexistence and useful batch session lifetimes
  still need consumer integration. Existing short leases/session limits are not proof
  that tools can serve a multi-file LLM batch. OCT-SCAN owns the bounded adaptation.
- **Setup:** 18.4 supplies independent operator authentication, batch/context/provider
  binding, prepared-root mapping, Prepare/Select, and OctoberDbContext. The CLI
  does not yet generate Knowledge; the context has no CL/BL/publication/dialogue store.
- **Web:** ordinary startup uses frozen DefaultConnection, Identity/domain migrations
  and existing administration. It does not register the new wiki, select a publication
  or execute October migrations. Integration must add the consumer composition.
- **18.5:** accepted plan ed4f0205538339ce6812b4d9bc2c15611c092662 is design input,
  not a delivered API. OCT-BUSINESS absorbs its minimum acceptance/read foundation.

## Feature map and first outcomes

| Feature | First deliverable | Original scope used selectively |
| --- | --- | --- |
| [OCT-SCAN](october-delivery/scan-batch-feature.md) | One host analyzes a declared registration/login source slice across two prepared Repositories, accepts complete-file results, then calls Business finalization with honest coverage. | 18.7/18.8 and batch composition from 18.12; integrated 18.1-18.4 are reuse inputs. |
| [OCT-BUSINESS](october-delivery/business-knowledge-feature.md) | First the shared CL store and exact reads; then supported relationships, distinct rooted BL and a fixed published wiki. | Selected 18.5 invariants plus 18.9-18.11. No separate 18.5 run. |
| [OCT-CHAT](october-delivery/streaming-chat-feature.md) | Useful multi-document questions/follow-ups, real tool rounds and immediate provider text, saved outcomes and exact references in the existing UI. | First useful portion of 19.1-19.3. |
| [OCT-EXPLORE](october-delivery/reader-graph-feature.md) | Ordinary-user exact reader first, then a bounded graph with inspect/open, direction/meaning and basic movement. | 18.6 and first useful portion of 20.1/20.2. |

Minimum data target: one coherent source-backed business scenario crossing the two
prepared Repositories, useful CL/BL, a supported cross-Repository link and a real
unknown/uncertainty. Input selection is operator-configured, with explicit coverage;
names and repository count do not become eligibility rules. Broader useful coverage
follows only after the connected case works. Large graph controls, broad language/
framework generality, update scanning, durable batch restart and generic tool
ownership/reclamation are outside this first deliverable.

## Minimum shared contract

The Knowledge owner publishes a small compiled contract and representative fixture
on the integration branch first. All four consumers use the same types and examples.
These are contract responsibilities; they are not claims of existing public APIs.

| Boundary | Owner and required meaning |
| --- | --- |
| Completed-file proposal | Scan owns admission and trusted completion of one selected file; Knowledge accepts atomically, derives semantic identities and returns exact handles. Worker JSON cannot assert trusted completion. |
| Stored/publication identity | Knowledge owns batch, exact Document Version/section and explicit fixed publication membership. No implicit latest-batch, legacy ScanRun or fabricated Git origin. |
| Shared reads | Knowledge owns bounded list/search, exact content and accepted-related reads, including limits, provenance, unknowns and live subject/purpose/mode/all-contributor checks. Business returns BL only. |
| Relationships/BL | Business accepts source-supported endpoint/version references and rooted BL with actual dependencies. Graph projects these meanings; it cannot derive accepted edges from layout or text similarity. |
| Chat stream/references | Chat owns answer/tool/status events and turn state; references use exact handles actually read this turn. Explore supplies one reader destination shared by footer and graph. |

Keep one writer for OctoberDbContext, October migrations and its snapshot, including
Chat's requested dialogue fields. The integration owner coordinates shared registration,
Web routes and packages. One consumer cannot redefine publication or reader authority.
Setup's operator-only selection/authentication is not ordinary reader authorization.

The common fixture should contain CL, BL, a real-shaped accepted relationship,
an uncertainty and a disallowed contributor. It enables early development; real
provider/source outputs replace it for combined delivery evidence. Use focused
checks of these meanings, not an extra specification framework or approval queue.

## Parallel work and calendar

Assume four implementation owners with integration capacity reserved. Roles are
assignments for dispatch, not newly created chats. Every lane starts from the same
baseline and shared contract checkpoint; owners commit usable increments early.
Use one branch/worktree per feature and keep oct14/main under the integration
owner. Share the first compiled contract checkpoint before parallel implementation;
definition work can start together now. Contract changes go through the Knowledge
owner and reach all consumers as small shared commits, not independent DTO forks.
Keep build outputs and owned test resources separate between lanes.

| Date (Asia/Shanghai) | Parallel work and observable result |
| --- | --- |
| October 6 | Business defines the shared contract and minimal store; Scan composes the real tool caller; Chat connects an event-driven model/tool path; Explore builds the exact reader using the shared fixture. Agree source inputs, existing accounts, provider configuration and owned targets. |
| October 7 | Merge Knowledge foundation and reader; run one real analyzed file into PostgreSQL and open its CL. Business starts focused matching/BL; Chat demonstrates tool rounds and live text against accepted fixture data. |
| October 8 | Small two-Repository case produces accepted relationships and real BL. Connect Chat and the initial graph immediately to that publication. Feature owners finish their minimum routes and focused verification. |
| October 9-10 | Execute the explicit [integration phase](october-integration-plan-2026-10-06.md): combine all branches, qualify real tools/store/Chat/reader/graph together, fix observed interface defects, generate the selected dataset and freeze the passing scenario by October 10 evening. |
| October 11-13 | Record the working scenario, prepare screenshots/slides and rehearse ten minutes. Restrict code work to necessary fixes to the frozen scenario. |
| October 14 | Submit the required material and retain the runnable build/data/configuration instructions. |

These are working targets, not completion estimates. If the October 7 CL path fails,
prioritize its concrete missing seam; further source coverage is not the remedy.
If the October 8 case fails, reduce input breadth and finish its missing behavior.
Keep JDT's existing file-tool fallback for navigation limitations. CodeGraph omission
or fabricated Knowledge is not an implied fallback. First-deliverable success needs
useful real results, while incomplete work remains explicitly incomplete.

## Execution boundary

These are revised inputs for four feature-sized specification/planning sessions.
The user has authorized this planning update, not production execution in this chat.
Each subsequent dispatch should name one brief, its first increment and its owner;
invoke SpecKit only on explicit instruction. No multi-story extension to the previous
runner is a prerequisite to using these inputs. Do not reactivate old 18.2/18.3
governed state or the abandoned full 18.5 run to obtain permission for the new scope.

Retain proportionate independent design/code checks, meaningful tests, explicit
runtime/resource authority and a truthful completion claim. Reuse unchanged evidence
within its actual boundary. No new workflow engine or overnight management team is
part of this delivery work. Process-local tool environment is preferred; user/machine
PATH or global tool changes require a separate direct user instruction.

## Handoff

Dispatch all four feature definitions against these inputs, with OCT-BUSINESS's
foundation first within its own plan. Use the integration plan as a separately
scheduled phase with one accountable owner; distribute discovered defects back to
their feature owners. Historical story criteria remain traceable, but deferred
breadth and old workflow obligations are not automatically copied into new tasks.
