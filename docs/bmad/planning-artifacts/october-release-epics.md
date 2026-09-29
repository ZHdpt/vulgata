---
title: Vulgata October release - requirements and epic breakdown
created: 2026-09-27
updated: 2026-09-29
scope: october-14-release-only
status: final
stepsCompleted: [1, 2, 3, 4]
currentStep: complete
inputSetConfirmed: 2026-09-27
requirementsConfirmed: true
requirementsConfirmedOn: 2026-09-27
requirementsExtractedOn: 2026-09-27
awaiting: null
epicDesignConfirmed: true
epicDesignConfirmedOn: 2026-09-27
approvedEpicIds: [18, 19, 20]
storiesConfirmed: true
storiesConfirmedOn: 2026-09-27
approvedStoryIds: ['18.1', '18.2', '18.3', '18.4', '18.5', '18.6', '18.7', '18.8', '18.9', '18.10', '18.11', '18.12', '19.1', '19.2', '19.3', '20.1', '20.2']
storyCreationMode: user-authorized-single-pass
storyCreationAuthorizedOn: 2026-09-27
storiesDraftedOn: 2026-09-27
draftedStoryIds: ['18.1', '18.2', '18.3', '18.4', '18.5', '18.6', '18.7', '18.8', '18.9', '18.10', '18.11', '18.12', '19.1', '19.2', '19.3', '20.1', '20.2']
storyCounts: {epic18: 12, epic19: 3, epic20: 2, total: 17}
lastDocumentReview: 2026-09-27
reviewStatus: corrected-and-final-validation-passed
finalValidation: passed
finalValidationOn: 2026-09-27
workflowComplete: true
workflowCompletedOn: 2026-09-27
implementation_authorized: false
latestDefinitionAmendment: docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-29-october-tool-state-consumers.md
latestDefinitionAmendmentApprovedOn: 2026-09-29
primaryInput: docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md
inputDocuments:
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md
  - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md
  - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md
referenceDocuments:
  - docs/bmad/epics.md
  - docs/bmad/implementation-artifacts/sprint-status.yaml
sourceCommit: 2d8b280f921507abf986ba11e439ec57c555accc
canonicalQueueChanged: true
canonicalTrackingAdoptedOn: 2026-09-27
---

# Vulgata October Release - Epic Breakdown

## Sprint tracking adoption — September 27, 2026

After definition completion, the user approved adding Epics 18–20 to the canonical
`docs/bmad/implementation-artifacts/sprint-status.yaml`, preserving Epics 13–17.
The canonical epic index now routes October stories here; this file remains their
definition authority. Read the earlier no-queue-change statements below as the
boundary at definition completion, superseded only for routing/tracking by this
adoption. All 17 October stories remain backlog; no feature or implementation is
activated. Governed story review and approval gates remain unchanged.

## Scoped definition amendment — September 29, 2026

The user approved the [tool-state consumer correction](sprint-change-proposal-2026-09-29-october-tool-state-consumers.md).
It adds only the application-owned tool-state admission/accounting criterion in
18.8 and the integrated source/tool-state qualification criterion in 18.12, with
their scope notes. Existing story identities, completion dependencies and lifecycle
statuses are unchanged. Shared source/tool repairs remain with the coordinated
upstream owners; this amendment neither activates a story nor grants implementation
or runtime authority. September 27 validation below describes its original revision.

## Overview

This finalized October story definition decomposes the selected presentation release,
not the full-product backlog. Step 1 requirements were confirmed on September 27;
Step 2 epic boundaries were approved on September 27; Step 3 authored all 17 stories
under the user's single-pass creation authorization. The user confirmed the corrected
story set and requested final validation on September 27.
Final validation passed on the same date, and the user selected [C] Complete
Workflow. This is definition-level completion, not implementation evidence.
The user confirmed this filename and input set on September 27. No implementation,
story activation, runtime operation or canonical queue change is authorized here.

### Delivery at a Glance

The table is a navigation/dispatch aid, not permission to start implementation.
Dependencies are **completion** dependencies; development can start earlier at the
agreed F0 contract with labelled adapters and isolated ownership. Story numbers do
not prescribe serial execution. Definitions are linked below; see also the
[coverage map](#fr-coverage-map) and [shared delivery rules](#shared-delivery-rules).

| Story | Bounded outcome | Completion dependencies |
| --- | --- | --- |
| [18.1](#story-181-access-prepared-source-through-contained-read-only-tools) | Prepared-root list/read/search | None |
| [18.2](#story-182-navigate-prepared-java-source-with-restricted-jdt-ls) | Restricted real JDT-LS navigation | [18.1](#story-181-access-prepared-source-through-contained-read-only-tools) |
| [18.3](#story-183-use-native-codegraph-mcp-against-prepared-source) | Native CodeGraph MCP preparation/use | [18.1](#story-181-access-prepared-source-through-contained-read-only-tools) |
| [18.4](#story-184-prepare-an-owned-october-batch-with-valid-context-and-provider-bindings) | Valid owned batch, context and provider setup | [18.1](#story-181-access-prepared-source-through-contained-read-only-tools) |
| [18.5](#story-185-accept-semantic-file-knowledge-and-read-exact-published-results) | Atomic semantic CL acceptance and shared reads | [18.4](#story-184-prepare-an-owned-october-batch-with-valid-context-and-provider-bindings) |
| [18.6](#story-186-read-exact-knowledge-documents-without-management-permissions) | Minimum ordinary-user exact reader | [18.5](#story-185-accept-semantic-file-knowledge-and-read-exact-published-results) |
| [18.7](#story-187-generate-whole-file-code-logic-with-a-real-worker) | Real bounded whole-file Worker analysis | [18.2](#story-182-navigate-prepared-java-source-with-restricted-jdt-ls), [18.3](#story-183-use-native-codegraph-mcp-against-prepared-source), [18.5](#story-185-accept-semantic-file-knowledge-and-read-exact-published-results) |
| [18.8](#story-188-analyze-a-batch-with-explicit-admission-bounded-concurrency-and-safe-stop) | Admission, simple pool and accepted-corpus stop | [18.7](#story-187-generate-whole-file-code-logic-with-a-real-worker) |
| [18.9](#story-189-resolve-source-supported-local-and-cross-repository-relationships) | Static source-supported relationship resolution | [18.5](#story-185-accept-semantic-file-knowledge-and-read-exact-published-results) |
| [18.10](#story-1810-accept-distinct-rooted-flows-and-coherent-business-knowledge) | Rooted flow and coherent BL acceptance/publication | [18.9](#story-189-resolve-source-supported-local-and-cross-repository-relationships) |
| [18.11](#story-1811-generate-useful-document-first-business-explanations) | Real document-first BL generation | [18.10](#story-1810-accept-distinct-rooted-flows-and-coherent-business-knowledge) |
| [18.12](#story-1812-finish-a-prepared-batch-into-a-real-readable-wiki) | Complete host batch and real readable business wiki | [18.6](#story-186-read-exact-knowledge-documents-without-management-permissions), [18.8](#story-188-analyze-a-batch-with-explicit-admission-bounded-concurrency-and-safe-stop), [18.11](#story-1811-generate-useful-document-first-business-explanations) |
| [19.1](#story-191-use-permitted-wiki-tools-and-report-answer-documents) | Authorized wiki and reported-document tools | [18.10](#story-1810-accept-distinct-rooted-flows-and-coherent-business-knowledge) |
| [19.2](#story-192-run-natural-streaming-conversations-with-durable-outcomes) | Natural streaming turn engine and durable outcomes | [19.1](#story-191-use-permitted-wiki-tools-and-report-answer-documents) |
| [19.3](#story-193-use-genuine-streaming-chat-in-the-existing-interface) | Existing Chat UI and real selected-wiki qualification | [19.2](#story-192-run-natural-streaming-conversations-with-durable-outcomes), [18.6](#story-186-read-exact-knowledge-documents-without-management-permissions), [18.12](#story-1812-finish-a-prepared-batch-into-a-real-readable-wiki) |
| [20.1](#story-201-query-an-authorized-graph-of-the-published-wiki) | Authorized published-document graph queries | [18.10](#story-1810-accept-distinct-rooted-flows-and-coherent-business-knowledge) |
| [20.2](#story-202-explore-the-document-graph-and-open-its-evidence) | Usable graph UI and real selected-wiki qualification | [20.1](#story-201-query-an-authorized-graph-of-the-published-wiki), [18.6](#story-186-read-exact-knowledge-documents-without-management-permissions), [18.12](#story-1812-finish-a-prepared-batch-into-a-real-readable-wiki) |

**Deadline posture:** prioritize real tool/read/provider integration, then source
quality, before optional polish. The allowed recovery/transition fallbacks are the
baseline; no separate optional-feature stories consume the critical path. These
17 definitions are not 17 promised days or a delivery guarantee.

Suggested working targets, not additional acceptance criteria or user commitments:

- By **September 30**: agree F0's two small contract slices, expose the JDT risk and
  native MCP readiness, and establish the first stored/readable controlled fixture.
- By **October 3**: first real file -> accepted CL -> shared read/reader; start the
  real-data feedback loop while Chat/graph work continues against agreed contracts.
- By **October 6**: a small real cross-Repository BL case is published; start real
  Chat/graph integration immediately, not after broad batch completion.
- Aim for **October 9-10**: selected business batch and combined C2 checks. Freeze
  useful data/features by October 10 when feasible; reserve **October 11-13** for
  necessary corrections, UI polish within scope, recordings/slides and contingency.
- **October 14** is the user's deadline. If upstream evidence misses these targets,
  surface the remaining scope/time tradeoff promptly rather than silently omit
  required CodeGraph, cross-Repository behavior, real Chat or graph, or fabricate
  readiness. Do not add expensive optional work to compensate for elapsed time.

This schedule assumes enough parallel owners, promptly supplied prepared inputs,
provider/DB authorization and available runtime capacity. None is established by
writing this plan. Measure the first real runs before treating dates as achievable;
keep source preparation and presentation narrative moving alongside development.

### Input precedence and chronology

1. The user's latest explicit October decisions control this release. In particular,
   the September 27 confirmation makes the **October proposal the primary source**.
2. The [September 26 proposal, updated September 27](sprint-change-proposal-2026-09-26-october-batch-release.md)
   records the selected scope and its later refinements. Later accepted refinements
   supersede earlier alternatives within the same subject; draft questions remaining
   in its prose do not undo decisions subsequently confirmed in this conversation.
3. The September 11 PRD, architecture, glossary and UX pair supply compatible terms,
   integrity rules and existing styling only. A full-product requirement is not an
   October prerequisite merely because it is marked Current in those documents.
4. Original epics map retained backlog, not inherited October acceptance criteria.
   July/August specs, demos and evidence are reuse/history leads only. Neither an
   older passing test nor a newer file timestamp grants competing scope authority.

Apply explicit October overrides before extracting older requirements. Where no
October exception exists, preserve compatible safeguards; surface a real conflict
instead of silently expanding this release or weakening safety. Do not restore cut
features through dependencies, non-functional requirements, tests or UX states.

## Requirements Inventory

### Functional Requirements

`O-FR` identifiers belong only to this October release definition; they do not renumber the PRD.
Every item below is selected release scope, not a claim of existing implementation.
The primary source is the October proposal; section references below locate its detail.

#### Prepared inputs and real analysis tools

- **O-FR01 — Host batch and prepared origin.** Run a separate host-side generator over explicitly configured Repository directories. Bind batch ID, Repository identities, operator label and relative paths/ranges; no Git operation, fabricated commit, managed snapshot or repository-wide hash prerequisite. Operators keep inputs unchanged and available during generation/presentation. (Prepared input)
- **O-FR02 — Bound advisory context.** Load valid Global/System/Repository context from the owned setup; retain selected values, scope and provenance for the batch. Preserve conflicting statements and distinguish interpretation guidance from source facts. No new context-management UI or human-maintained counterpart map. (Context-assisted coverage)
- **O-FR03 — Explicit file admission.** Inventory roots deterministically. LLMs may propose treatment/ordering; Scan Control applies the explicit admission policy and records its identity with admitted/supporting/excluded dispositions and supported exclusion/demotion reasons. Unknown extensions, confidence or index omission alone cannot remove coverage; unsupported exclusion stays conservatively admitted. No semantic-unit preallocation or universal DSL framework. (Context-assisted coverage)
- **O-FR04 — Whole-root read tools.** Give Workers contained read-only listing, ranged file reading and text search with locations and explicit pagination/continuations/limits. No silent truncation of coverage. These tools remain available when LSP is limited; they are not a replacement for required CodeGraph. (Whole-root tools)
- **O-FR05 — Native CodeGraph.** Prepare the configured source through CLI, connect to native CodeGraph MCP and let Workers discover/invoke its granted tools with roots bound by the application. Prove actual use; do not substitute CGC, a CLI-only wrapper or silent source-only fallback. (Whole-root tools)
- **O-FR06 — Offline JDT-LS.** Support JDT-LS over the whole prepared Repository with automatic Maven/Gradle imports, builds and dependency restoration disabled. Use at most one session per selected Repository where applicable, normalize results/limitations and serialize unsafe shared access. Qualify representative navigation first; list/read/search handles LSP limitations. No other LSP or dependency-download feature. (Whole-root tools)

#### File Knowledge, static resolution and business wiki

- **O-FR07 — Semantic file analysis.** A bounded Worker analyzes a logical whole-file workstream, using other granted files as context; continued windows may handle large files without becoming semantic identities. Discover meaningful Code Units, each with its own CL Logical Document and application-owned identity. No mandatory per-unit model call. (Isolated acceptance; A3)
- **O-FR08 — Accepted-file boundary.** Validate and atomically accept a completed file's units, CL, observations and truthful coverage disposition. Only its Accepted File Manifest exposes Knowledge; unfinished/failed drafts remain invisible even after operator stop. Record legitimate zero-unit outcomes and gaps rather than fake Documents. (Isolated acceptance)
- **O-FR09 — Source observations and qualifications.** Preserve caller/provider roles, mechanism, operation, original/normalized identifiers, available namespaces, source locations and unknowns. Keep recognition context, observed facts, hypotheses and accepted relationships distinct; material qualifications survive CL, BL and Chat at section/statement-group level. (Context-assisted interpretation; Pass 2)
- **O-FR10 — Small deterministic Worker pool.** Scan Control dispatches ready file work through one bounded pool with one easily edited concurrency value and one active writer per file. Workers do not create arbitrary scans or wait indefinitely for unscanned targets. Bound calls/retries and report tool/model/persistence waits. (Simple concurrency)
- **O-FR11 — Natural finish or explicit stop.** On natural finish or graceful operator stop, settle/fence active writes, fix accepted Pass 1 input and run bounded resolution/BL finalization. Unprocessed or failed primary files remain gaps, not retroactively excluded work. No all-files, preferred-flow or content-quality threshold is added; an empty/sparse business wiki is valid. Crashes/retry exhaustion are not successful finalization. (Simplified execution)
- **O-FR12 — LLM-assisted static matching.** Retrieve bounded candidates from the fixed accepted corpus using selected normalized/original identifiers and relevant context/source signals. A matching Worker uses caller surroundings, provider declarations, available bindings and CL versions to propose supported counterparts and supporting/opposing evidence. Knowledge validates scope, identities, versions, references, explicit constraints and conflicting/duplicate writes; it does not impose universal identifier equality or treat confidence as proof. Record inputs and matching policy/prompt identity. (LLM-assisted matching)
- **O-FR13 — Scoped unresolved outcomes.** Retain one Uncertainty outcome with a reason and batch-coverage limitation for absent, ambiguous, unprocessed or otherwise unestablished targets. Do not claim global provider nonexistence. Positive source-supported links may coexist with partial coverage, multiple valid targets and bounded cycles; missing decisive external facts stay unknown. (Simplified execution; matching)
- **O-FR14 — Local and cross-Repository flow roots.** Accept supported local REFERENCES as well as cross-Repository links. Plan bounded directed evidence for distinct source-backed operation/handler/schedule/workflow/selected-unit roots, with branches and uncertain frontiers. Local-only flows qualify; shared helpers, folders and cycles do not merge business purposes or prove runtime execution order. (Simplified execution; A4)
- **O-FR15 — Document-first BL.** Initially synthesize useful business conditions, alternatives, effects and gaps from accepted CL, relationship evidence, Uncertainties and selected context. Pin actual dependencies and preserve qualifications. Pass 2 neither rewrites CL nor creates authoritative links or recursively schedules scans. Source tools are conditional, not an initial dependency; see O-OPT03. (Pass 2)
- **O-FR16 — Coherent structured publication.** Store canonical CL/BL as structured JSON with derived readable views. Validate identities, prepared origins, sections, anchors, relationship endpoints and actual BL dependencies; publish coherent records atomically with immutable exact Document Versions and replay-safe acceptance. Explicit published-dataset eligibility excludes drafts and requires no update/retirement/current-head competition across batches. (Isolated acceptance)
- **O-FR17 — Exact database/batch selection.** Use one owned PostgreSQL database per batch with identical fixed schemas/models. Generator and October Web bind the same exact database plus batch ID at startup; switching requires host restart. Missing/mismatched selection fails visibly, with no latest-batch or earlier-wiki fallback. (Selected storage layout)
- **O-FR18 — Valid shared setup and storage.** Both hosts use one October acceptance/publication implementation and valid Identity/System/Repository/context setup rules. Operator identity is real. No dummy pair records, forced ScanRun completion, raw legacy-content import, model-issued SQL or fabricated Administrator claim. Programmatic setup is allowed; new CRUD screens are not required. (Isolation)
- **O-FR19 — Explicit provider binding.** Select one declared configuration source/precedence per process for endpoint/model/key, API type and Worker/Chat role profiles. A shared profile must be deliberate. Use user-instructed environment loading or implemented DB loading without silent field mixing/failover; normal provider administration stays unchanged. (Local LLM configuration)

#### Conversational wiki use

- **O-FR20 — Shared authorized wiki tools.** Supply search/browse, content/section reading and existing-related-document navigation with provenance, gaps and bounded pagination. Chat, reader and graph consume the selected published October data, not independent imports. No vector service, per-record tool catalogue or mandatory legacy evidence adapter. (Chat; F0)
- **O-FR21 — Natural multi-round conversation.** Use a bounded real model/tool loop for multi-document explanations and useful follow-ups, not fixed candidate sentences. Trust available wiki content with its stated qualifications; do not turn Chat into routine source re-proof, Scan initiation or wiki repair. Permitted legacy evidence may be a labelled secondary input. (Chat)
- **O-FR22 — Business/IT boundaries.** Business reads BL only; IT may read authorized CL/BL. Apply live user, purpose, mode, scope and all-contributor checks across tools, generated text, history, references and graph metadata. Footer selection is not the authorization boundary. Source inspection is not required. (Chat; Graph)
- **O-FR23 — Dialogue and stable reads.** Persist ordered conversations and use bounded permitted completed history for follow-ups. Bind each turn to its originating conversation and exact published-batch read selection, including later tool reads; one answer is active per conversation. Earlier references remain exact; inference reuse rechecks contributor authorization. (Chat; F0)
- **O-FR24 — Immediate genuine answer deltas.** Forward user-facing provider text through the Chat backend to the existing UI while generation is unfinished, including interleaved tool rounds. Completion, JSON validation, saving and footer reporting must not gate prose. No full-answer buffering or artificial typewriter replay. (Explicit streaming)
- **O-FR25 — Real tool activity.** Emit turn/tool identifiers and actual running/completed/failed progress for compact UI activity. Sanitize descriptions; do not expose raw tool inputs/results, hidden reasoning, credentials or invented percentages. (Explicit streaming)
- **O-FR26 — Reported-document footer.** A report-used-documents tool accepts server-issued exact-version handles actually read through permitted turn tools. Derive destinations/titles server-side, reject invalid/unread/disallowed handles and deduplicate. Show the list after prose without reordering prose; empty lists are valid for greetings/no-results, and missing reporting is disclosed rather than fabricated or repaired by regeneration. (Chat footer)
- **O-FR27 — One durable completion.** Completed means valid provider finish, no truncation or unfinished tools and one idempotent content/reference/status save. A lost save acknowledgement is resolved by checking/replaying that save, not generating again; telemetry failure cannot undo accepted completion. (Explicit streaming)
- **O-FR28 — Honest interruption.** Preserve useful authorized partial text with failed/interrupted status; exclude it from ordinary completed-answer inference history. Require explicit retry, prevent overlapping retry while original work is unresolved and settle owned work. Full same-turn reconnect/reattachment is excluded. (Chat)
- **O-FR29 — Simple conversation scope fallback.** Mode and authorized Repository scope may be fixed per conversation. A change starts an empty conversation with no prior transcript, summary or model context; the UI may wait for the active answer to finish/stop. Use actual authorized October Repositories, not a demo catalogue. Fixed scope does not freeze permissions. (September 27 transition exception)

#### Reader, graph and qualification

- **O-FR30 — One minimal exact-version reader.** Chat footer and graph open the same authorized readable CL/BL Document Version destination, with sections and material gaps/qualifications. Ordinary permitted users need no management role. No source viewer, compulsory inline preview or full history explorer. (B2; Graph)
- **O-FR31 — Usable graph navigation.** Show the selected batch/context and bounded neighborhoods; filter permitted Repository/document kind, search titles/focus, pan/zoom/fit/reset, inspect nodes/highlight neighbors, explicitly expand within limits and open exact Documents. Include loading, empty/no-results, failure/retry and manual refresh states. (Graph)
- **O-FR32 — Truthful graph semantics.** Project real CL/BL and supported REFERENCES/GENERATED_FROM relationships, with direction/type/Uncertainty legends and visible bounds. Keep hypotheses separate; no invented targets, shared-CL-derived BL-to-BL edges, domain editing or deployed-execution claims. Enforce Business filtering on the server, including labels/counts/tooltips; sparse graphs are valid. (Graph)
- **O-FR33 — Useful operating evidence.** Report actual file admission/acceptance/failure/unprocessed coverage separately from unit/Document counts and publication. Retain sanitized append-only log.md entries using `## [YYYY-MM-DD HH:MM] operation | target` plus useful timing/error diagnostics; no fictional progress or raw sensitive content. (Simple concurrency; evaluation)
- **O-FR34 — Real batch-to-Web chain.** Qualify actual generation, atomic publication, ordinary-user multi-round Chat/footer navigation and graph from the same selected dataset. Integrate labelled fixtures first, one real analyzed file next, then a small cross-Repository case and the prepared registration/login inputs. Fixtures and prior demo success are not generated-knowledge evidence. (Continuous integration; qualification)
- **O-FR35 — Broad selected-source wiki.** Generate useful knowledge across admitted source in the two user-prepared registration/login Repositories, not just handpicked presentation entrypoints. This is not all 16 Repositories, a required pair eligibility rule or a guarantee of complete coverage after stop/failure. Actual names, roots and context come from source preparation. (Decision and authority; evaluation)

### NonFunctional Requirements

- **O-NFR01 — Containment.** Bind source/tool roots outside model arguments; deny path/symlink escapes, arbitrary host files, writes and shell/command tools. Bound tool responses/work without disguising limits as coverage. (Proposal: Whole-root tools)
- **O-NFR02 — Live disclosure control.** Recheck every composite contributor before acceptance/disclosure and history reuse. No restricted metadata leakage; revocation stops further unauthorized output. Preserve real Identity and existing password/security rules, without redesigning them. (Proposal: Isolation; Chat; Graph)
- **O-NFR03 — Secrets and provider boundaries.** Never discover unspecified .env files, reuse old secrets, or expose credentials in prompts/logs. Follow valid encryption rules for DB configuration; copied protected rows require compatible keys/app identity/purpose only if that optional route is used. Self-hosted source/Documents stay within organization infrastructure. (Proposal: Provider configuration; compatible NFR-2)
- **O-NFR04 — Atomicity and replay safety.** Completed-file acceptance, coherent publication and Chat terminal saves resist partial visibility and duplicate replay. Accepted content survives aborts; no failed drafts, previous-batch substitutions or fabricated completion. This does not require durable batch restart. (Proposal: Acceptance; Chat completion)
- **O-NFR05 — Bounded failure handling.** Classify failures sufficiently for bounded same-input retries and truthful failure/interruption; settle owned writes/processes/capacity and preserve unrelated work. Transient Scan-provider failures use increasing bounded waits; configuration/authorization/integrity failures require correction rather than blind retry. Chat may use bounded increasing transport retries only where no visible answer or uncertain generation/tool execution would be replayed; otherwise interrupt and require explicit retry after settlement. If durable batch recovery is omitted, use an explicit fresh batch/new owned DB rather than silent resume/merge. No distributed admission service. (Proposal: Simple concurrency; Chat completion; September 27 exception)
- **O-NFR06 — Measured batch throughput.** Record configuration, model/tool/persistence waits, duration, coverage and available usage. Tune the simple cap using observations; four Workers is an experiment, not a requirement or provider guarantee. No new universal completion-time target or coverage quota. (Proposal: Simple concurrency; evaluation)
- **O-NFR07 — Measured live Chat.** Verify visible answer text before provider completion, tool activity across rounds and truthful interrupted output; measure first useful output and total time on the declared provider. SSE ingestion alone is insufficient; footer/save latency must not delay prose. (Proposal: Explicit streaming)
- **O-NFR08 — Retrieved content is data.** Source, context and wiki text cannot override tool permissions, mode, purpose or application instructions. Include a bounded adversarial retrieved-document case; no additional semantic-auditor platform. (Proposal: Chat)
- **O-NFR09 — Usable existing presentation.** Preserve Simplified Chinese, Fluent/Library Oak, Business/light and IT/dark, keyboard basics and accessible status text. No visual redesign or new accessibility certification programme. (Proposal: UI/Graph; compatible UX)
- **O-NFR10 — Externalized prompts.** Keep production prompts in configurable externalized resources with supported overrides. Record matching policy/prompt identity; this is not a Prompt Workbench feature. (Proposal: Simple concurrency)
- **O-NFR11 — Content quality distinct from structure.** Establish a compact source-checked registration/login expected-fact set, including key rules/branches/effects, cross-Repository responsibilities and a genuine unknown. Evaluate CL/BL separately from Chat with roughly 10–15 questions, follow-ups and paraphrased/held-out wording. Assess search on a real wiki; do not turn this into a publication quality gate, automated entailment service or competitor benchmark. (Proposal: Evaluation)
- **O-NFR12 — Honest owned-environment qualification.** Use authorized databases, provider/source grants, ports and processes; protect actual demo data and secrets. Separate controlled tests, real tools/provider use and presentation evidence. Host execution is the October target; full Docker, generic beyond-pair and backup/restore qualification are not October prerequisites. Do not portray unimplemented Web Scan initiation or simulated output as demonstrated. (Proposal: Execution/isolation/qualification)

### Additional Requirements

- **O-AR01 — Additive composition, not a greenfield starter.** Use dedicated October folders/namespaces and one explicit composition boundary within existing projects unless an actual dependency justifies otherwise. Keep .NET/Blazor/MAF/PostgreSQL foundations; no starter template or automatic package upgrade.
- **O-AR02 — Respect module ownership.** Core owns domain contracts; Infrastructure owns adapters/persistence. Scan Control schedules, Knowledge accepts/publishes/selects, Chat owns dialogue, MAF executes bounded conversations/tools. Agents do not access EF or arbitrary host resources directly.
- **O-AR03 — Separate October persistence.** Own a dedicated context/fixed schema, migration folder/history and snapshot. October mappings must not enter normal assembly-scanned EF configuration; ordinary startup must not run October migrations. Preserve normal model, Git/pair validators and disabled-October routing.
- **O-AR04 — Shared contract, one writer policy.** Batch and October Web share acceptance/publication/read interfaces. No per-consumer persistence copy, raw SQL data-preparation shortcut or relabelled legacy Knowledge. Compatible existing validation/rendering/security mechanics may be reused after checkout-specific verification.
- **O-AR05 — Small F0 agreement.** Before dependent builders split, settle only prepared identity/root/tool/context/admission, analysis proposals and accepted-file semantics, local/cross flow identity, publication/exact-version reads, wiki/graph/footer/reader contracts and Chat events/completion. Include representative valid/invalid examples; F0 is a coordination milestone, not a new framework or completed implementation.
- **O-AR06 — Release source/tools contracts early.** List/read/search, native CodeGraph and JDT-LS work need no wiki DB. Once wiki/UI contracts are agreed, store, scanner, Chat and reader/graph builders may use labelled test adapters in parallel; real acceptance still requires integrated upstream results.
- **O-AR07 — Explicit integration ownership.** One owner controls shared contracts, package/project changes, DI/tool registrations and Web composition; one Knowledge owner writes October migrations/model snapshot. One reader owner supplies the shared destination. Coordinate changes rather than let consumers silently fork contracts.
- **O-AR08 — Isolate concurrent delivery resources.** Assign separate checkouts/branches and owned databases, ports and tool workspaces when implementation is authorized. No concurrent destructive reset/migration of the demonstration DB; an exclusive agreed window is an alternative. Planning creates none of these resources.
- **O-AR09 — Prioritize real JDT risk.** Qualify navigation in the declared restricted profile before relying on it. CodeGraph needs a small native discovery/invocation/root-binding check, not a renewed research spike. Tool readiness is distinct from semantic completeness.
- **O-AR10 — Small concrete matching contract.** Once samples arrive, the owning plan defines the initial mechanism's positive source-evidence checklist and examples for aliases, namespace/operation conflicts, harmless missing optional fields and missing provider evidence. No fixed Repository mappings, universal equality engine, exhaustive generic matrix, per-link human approval or extra semantic-auditor agent.
- **O-AR11 — Continuous integration evidence.** Follow the fixture -> one real file -> small cross-Repository wiki -> selected real batch sequence. Parallel development does not change runtime order: fixed accepted Pass 1 corpus -> static resolution/flows -> BL/publication -> Web reads.
- **O-AR12 — Preserve formal authorization and backlog.** Planning identities do not activate stories or grant implementation approval. Current governed-delivery/model escalation and independent plan/tool/Scan/PRD gates remain. Story 13.1 stays done; 13.2 and unselected original obligations stay backlog. Original acceptance criteria are not copied wholesale into October.

#### Secondary and conditional scope — not hidden prerequisites

| ID | Decision | Required low-cost boundary |
| --- | --- | --- |
| O-OPT01 | Durable batch pause/resume/checkpoint restart is secondary; omit if costly to implement or qualify. | Record unsupported recovery; safely stop, preserve accepted data and require an explicit fresh batch with new identity/owned DB. Graceful operator-stop finalization and atomic writes remain. |
| O-OPT02 | In-place mode/scope/history transitions are secondary; omit if time is short. | Fixed conversation mode/scope; changing either opens an empty conversation, optionally after current answer settles. Keep live authorization; import no old inference content. |
| O-OPT03 | Pass 2 source tools only if concrete quality evaluation shows an important source-access gap. | Add existing read/list/search first under a static authorized prepared-root grant; record actual added source dependencies. No arbitrary host access, recursive scans or CL/link mutation. Additional navigation needs demonstrated need and applicable story tool approval. |

O-OPT01/02 omissions are user-authorized October cuts, not failing prerequisites or
automatic return-to-plan triggers merely because older documents require more.
Their fallback safeguards remain testable. O-OPT03 is conditional scope, not a
standing authorization to expose tools. Other failures receive normal risk-based handling.

### UX Design Requirements

Older UX is used only where compatible with October. Existing components/tokens are
preserved, not rebuilt as a new workstream. The October proposal overrides its
inline citations, source/history browsing, Scan dashboard and reconnect examples.

- **O-UX01 — Preserve shell and themes.** Keep chat-first layout, current navigation, typography, assistant/user bubbles, composer and picker appearance; Business/light and IT/dark remain coupled. No new dashboard or theme redesign.
- **O-UX02 — Reuse visual tokens.** Keep Library Oak primary/accent/surface colors, inherited Fluent controls/spacing/radii and Noto Serif SC only for existing display moments. Use Fluent semantic status colors, not new brand colors.
- **O-UX03 — Chinese and mode-appropriate prose.** Localize new labels/errors/placeholders. Business explanations avoid technical jargon; IT explanations may include permitted technical detail. State unknowns neutrally and provide a truthful next action.
- **O-UX04 — Actual scope selector.** Populate the existing picker from authorized October Repositories. Under the fallback, selection/mode configure a new conversation rather than rebind existing history; explain this behavior and avoid carrying old content across modes.
- **O-UX05 — Compact tool activity.** Start a progress placeholder at turn initiation, then show actual sanitized searching/reading activity with running/completed/failed status and stable tool-call identity above/within the existing answer bubble. No diagnostics console or invented percentage.
- **O-UX06 — Growing answer prose.** Render genuine authorized deltas immediately; permit interleaved tool activity. Small render coalescing is allowed, buffered replay is not. Do not gate visible prose on references or saving.
- **O-UX07 — Footer after answer.** Show deduplicated real document titles/links in stable reported order unless feature design explicitly selects another order. Footer updates neither renumber nor rearrange already streamed prose. Missing references have an honest limitation, not a fabricated citation.
- **O-UX08 — Truthful terminal states.** Distinguish generating, saving, completed, interrupted/failed and save-unconfirmed outcomes. Retain useful permitted partial text and an explicit safe retry; never promise same-turn reconnect or recovery that is not implemented.
- **O-UX09 — Shared reader destination.** Open exact permitted Document Versions from both footer and graph. Render derived structured content/sections with readable titles and type cues. Business receives no CL/source paths, line ranges or Git metadata, including previews/headers.
- **O-UX10 — Ordinary-reader access.** Make reader/graph reachable by Knowledge-authorized users without requiring the existing management-only graph policy. Reuse the existing shell instead of a separate visual application.
- **O-UX11 — Graph scope and focus.** Display selected batch/context, permitted Repository/type filters and title search/focus. Business must not offer a hidden CL filter. Exact layout/default grouping remains feature design work.
- **O-UX12 — Graph movement and inspection.** Support pan, zoom, fit/reset, node selection, immediate-neighbor highlighting and title/kind/Repository/available-summary inspection. Provide explicit bounded neighbor expansion with limit/load-more feedback.
- **O-UX13 — Graph meaning and navigation.** Explain document types, relationship direction/meaning and Uncertainties in a compact legend; open selected exact Documents. Preserve established permitted CL/BL cues; do not distort edges for visual density or imply an executed sequence.
- **O-UX14 — Graph/reader loading states.** Distinguish initial loading, refreshing, empty, no-results and error/retry with manual graph refresh. Keep useful content visible during refresh where safe. Sparse Business graphs and coverage gaps are legitimate, not placeholders to fill.
- **O-UX15 — Keyboard/accessibility floor.** Preserve Enter to send, Shift+Enter for newline, Escape for the top dialog/panel, reading-order focus and existing Fluent accessibility. Status uses text rather than color alone; announce meaningful state changes without reading every token.
- **O-UX16 — No hidden full-product UI work.** Do not add management/context CRUD, Scan controls/dashboard, HITL, attachments, compulsory citation previews, source viewer, full history explorer, live graph animation or graph editing from old UX examples. No new standalone component library or accessibility certification task.

### Older requirement disposition — completeness without scope restoration

The entire canonical PRD was considered. This ledger accounts for all original
FR/NFR identifiers; it is **not** another selected feature list. "Sliced" means only
the explicit October behavior above survives. "Existing" means preserve compatible
setup/security, not rebuild or claim it tested. "Outside" and "Deferred/History"
create no October task, prerequisite or completion claim. Original prose remains
in the linked PRD; copying it here would obscure the controlling October changes.

| Original FRs | October disposition |
| --- | --- |
| FR-1.1, FR-1.2, FR-1.3, FR-1.4 | Existing Identity/roles/security; real authenticated operator/readers. No new user-management work. |
| FR-2.1, FR-2.2, FR-2.3, FR-2.4, FR-2.5 | Existing valid organizational setup where compatible; prepared-origin exception replaces Git acquisition/reachability for October. New CRUD, standalone-library journey and organizational mutation features are outside. |
| FR-3.1, FR-3.2, FR-3.3, FR-3.4 | Sliced explicit provider/API/role loading and sanitized readiness; preserve normal administration, no new registry UI or automatic switching. |
| FR-4.1 | Sliced deterministic inventory/policy and context-assisted interpretation; no Git snapshot, separate overview feature or generic content-analysis framework. |
| FR-4.2 | Replaced initiation/scheduling with explicit host batch and small pool; ordinary Web initiation/fair multi-run orchestration outside. |
| FR-4.3, FR-4.4 | Selected semantic-unit CL, file work and atomic completed-file acceptance; durable window recovery secondary. |
| FR-4.5 | Sliced distinct local/cross roots and supported BL; fixed partial corpus allowed, initial Pass 2 document-first. |
| FR-4.6 | Sliced semantic identities/grouping needed for reader/graph, not full hierarchy/history UX. |
| FR-4.7 | Selected structured JSON, atomic publication and authorized exact reads in isolated October storage; no competing replacement generations. |
| FR-4.8 | Selected sanitized append-only log.md. |
| FR-4.9 | Sliced native CodeGraph/JDT/contained reads; prepared origin replaces retained Git source. No CGC or silent CodeGraph fallback. |
| FR-4.10 | Preserve immutable accepted versions/dependencies; replacement, retirement and refresh outside. |
| FR-4.11, FR-4.12 | History; no old spike, superstep, confidence threshold or peer-verification workflow. |
| FR-5.1, FR-5.8 | Selected advisory framework context/provenance, not authoritative maps. |
| FR-5.2, FR-5.3, FR-5.4, FR-5.5, FR-5.6, FR-5.7, FR-5.10, FR-5.11, FR-5.12 | Sliced source observations/roles/identifiers, static matching and scoped unknowns for the prepared inputs. No guaranteed implementation or exhaustive qualification of every protocol, indirect-dispatch technique or deployment topology. |
| FR-5.9 | Outside: dedicated Prompt Workbench. |
| FR-5.13 | Deferred/History: external operational-information adapters and mock topology. |
| FR-5.14 | Sliced documented advisory-context usage; no general parser/dialect/plugin framework. |
| FR-6.1, FR-6.2, FR-6.3, FR-6.4, FR-6.5 | Sliced finite static matching, safe cycles, source-supported multiple targets and readable scoped Uncertainties; no late-target engine or owner-response workflow. |
| FR-6.6 | Outside: later-evidence refresh/coalescing. Preserve truthful current batch gaps. |
| FR-6.7 | Deferred: incremental impact/reuse. |
| FR-7.1, FR-7.2, FR-7.4 | Deferred: product HITL/inbox/actions; no automatic repair substitute. |
| FR-7.3 | Selected lower-authority context and material qualifications through CL/BL/Chat. |
| FR-8.1 | Outside new standalone-library onboarding/scan journey; no fixed-pair restriction introduced. |
| FR-8.2, FR-8.3 | Deferred: partial/on-demand library scans. |
| FR-8.4 | Preserve truthful library assumptions/limits; imports alone are not communication gaps or behavior proof. |
| FR-8.5 | Outside/deferred: dedicated unknown-library inference/notification. |
| FR-9.1, FR-9.2, FR-9.3, FR-9.5 | Deferred/excluded: monitoring, pulling, incremental work and update coordination. |
| FR-9.4 | Sliced exact immutable Document destinations; full version-history explorer outside. |
| FR-10.1 | Preserve existing protected configuration; no new DB management or agent SQL surface. |
| FR-10.2, FR-10.3 | Deferred: agent schema/sample-data access. |
| FR-11.1, FR-11.2 | Selected mode-safe BL-only Business and permitted CL/BL IT; source inspection outside. |
| FR-11.3 | Sliced durable useful dialogue and one active answer; advanced transitions secondary, empty-conversation fallback sufficient. |
| FR-11.4 | Sliced authorized actual-batch selection/stable reads with the fixed-conversation fallback; no hot database switching or wider dataset discovery. |
| FR-11.8 | Outside new automatic empty-selection relevance behavior; keep scope selection explicit and safe rather than importing the fuller PRD feature. |
| FR-11.5 | Deferred: attachments. |
| FR-11.6 | Selected natural wiki tool synthesis and follow-ups. |
| FR-11.7 | Sliced genuine deltas/completion/interruption and exact footer; sentence-linked proof, compulsory previews/source viewer and full reattachment excluded. |
| FR-12.1, FR-12.2, FR-12.3, FR-12.4 | Sliced real batch status/Worker/coverage/error reporting, not full Web Scan dashboard or mandatory recovery UI. |
| FR-12.5 | Deferred: dashboard HITL. |
| FR-12.6 | Sliced minimum exact reader and navigable static graph; history explorer/live portfolio outside. |
| FR-13.1 | Selected native CodeGraph MCP only; general registered-MCP product backlog stays outside. |
| FR-13.2, FR-13.5 | Outside: general externally configured MCP consumption/management. |
| FR-13.3 | Preserve purpose-specific tool authority; Scan grants are not Chat grants. |
| FR-13.4 | Deferred: outward wiki MCP. |
| FR-14.1, FR-14.2, FR-14.3, FR-14.4 | Selected valid three-level context with fixed batch values/provenance; no editing-during-scan management workflow. |
| FR-15.1, FR-15.2, FR-15.3 | Sliced offline whole-root JDT-LS and declared limitations; no additional adapters, sessions-per-repo expansion or dependency restoration. |

| Original NFRs | October disposition |
| --- | --- |
| NFR-1.1, NFR-1.2 | Measure selected batch and genuine Chat performance/usefulness; no universal SLA or fixed file/Document quota. |
| NFR-1.3 | Deferred: live-graph timing/animation. |
| NFR-2.1 | Existing password security preserved, not redesigned. |
| NFR-2.2, NFR-2.3 | Preserve secrets, purpose/scope, live all-contributor access and required encryption compatibility; no full product backup/restore expansion. |
| NFR-2.4 | Agent DB tools remain excluded; retained read-only/non-production safeguards are not permission to add them. |
| NFR-3.1, NFR-3.2, NFR-3.3 | Sliced bounded failures, atomic acceptance and durable Chat save/replay. Batch checkpoint restart secondary; shared cross-process admission and same-turn reattachment excluded. |
| NFR-4.1 | October host generator/Web plus existing PostgreSQL; Docker, snapshot retention and coherent product backup/restore outside. |
| NFR-5.1, NFR-5.2 | Preserve business usability/basic accessibility and assess explanation quality; no new certification. |
| NFR-6.1, NFR-6.2 | Externalized prompts and documented simple advisory context; no Workbench, detailed service maps or executable dialect extension. |

### Outstanding implementation inputs, not planning blockers

- User-prepared Repository identities/roots and Global/System/Repository context;
  two small fixtures may precede them, but cannot replace real qualification.
- User instructions for provider configuration/secret access and owned runtime/DB
  authorizations before execution; this phase reads no credentials or sources.
- F0 detailed contracts, graph layout/route and initial mechanism examples belong
  to owning planning work. Do not turn pending detail into revived generic scope.
- Presentation narrative belongs to the user/team; organizer disclosure requirements
  remain to be confirmed. No fabricated benchmark, runtime claim or Web Scan demo.

### FR Coverage Map

#### Approved Functional Coverage

Each selected FR is assigned below. Shared rows state the producer/consumer split;
they do not create multiple writers or allow one epic to delegate its own safeguards.

| Requirement | Epic ownership and purpose |
| --- | --- |
| O-FR01, O-FR02, O-FR03, O-FR04, O-FR05, O-FR06 | 18: prepared inputs/context/admission and real source/CodeGraph/JDT tools. |
| O-FR07, O-FR08, O-FR09, O-FR10, O-FR11 | 18: semantic file work, atomic acceptance, bounded dispatch and partial-stop finalization. |
| O-FR12, O-FR13, O-FR14, O-FR15, O-FR16 | 18: static LLM-assisted matching, uncertainties, local/cross roots, BL and structured publication. |
| O-FR17, O-FR18, O-FR19 | 18: owned batch/database setup, shared integrity implementation and explicit provider bindings; consumers retain their purpose checks. |
| O-FR20 | 18 owns shared Knowledge search/content/related reads; 19 exposes Chat-purpose tools over them. Graph reads never require the Chat tool layer. |
| O-FR21 | 19: natural multi-round wiki synthesis. |
| O-FR22 | 18 owns base Knowledge/reader authorization; 19 enforces it over dialogue/tools/output; 20 enforces it over graph data/metadata. |
| O-FR23, O-FR24, O-FR25, O-FR26, O-FR27, O-FR28, O-FR29 | 19: conversation, genuine streaming/activity, footer, durable completion, interruption and fixed-scope fallback. |
| O-FR30 | 18: one exact-version reader; 19/20 supply authorized navigation into it. |
| O-FR31, O-FR32 | 20: usable truthful graph and its projections. |
| O-FR33 | 18: batch coverage/operating evidence and shared log mechanism; 19/20 report their own applicable operations without building another logging platform. |
| O-FR34 | 18, 19, 20 each qualify their real portion; the integration owner combines them at the existing C2 release milestone. No standalone qualification epic or deferred first integration. |
| O-FR35 | 18: broad admitted-source wiki for the selected real inputs. |

#### Cross-Cutting Requirements and Optional Targets

| Requirements | Delivery responsibility |
| --- | --- |
| O-NFR01 | 18's source/tools; conditional Pass 2 tools remain in that boundary. |
| O-NFR02, O-NFR03, O-NFR04, O-NFR05, O-NFR08 | All applicable producer/consumer surfaces preserve access, secrets, integrity, bounded failure and data-not-instructions rules; prove changed behavior, not three copies of the same infrastructure. |
| O-NFR06 | 18 measures batch/tool/model behavior. |
| O-NFR07 | 19 measures genuine end-to-end Chat streaming. |
| O-NFR09 | 18 reader, 19 Chat and 20 graph preserve existing UX/accessibility. |
| O-NFR10 | 18/19 externalize their production prompts; graph has no required model agent. |
| O-NFR11 | 18 evaluates CL/BL; 19 evaluates answers/search; 20 verifies graph meaning/navigation; combine real evidence at C2 without a publication-quality threshold. |
| O-NFR12 | Each epic uses owned resources and honest environment-qualified evidence. |
| O-AR01, O-AR02, O-AR03, O-AR04 | 18 establishes additive composition/storage/read foundations; all consumers preserve module boundaries. |
| O-AR05, O-AR06, O-AR07, O-AR08 | Integration owner coordinates F0, shared files and isolated parallel resources across the epics. |
| O-AR09, O-AR10 | 18 prioritizes JDT risk and the bounded initial matching contract. |
| O-AR11, O-AR12 | All epics integrate early and preserve formal gates/backlog. |
| O-UX01, O-UX02, O-UX03, O-UX15, O-UX16 | All changed Web surfaces preserve styling, language, accessibility and exclusions. |
| O-UX04, O-UX05, O-UX06, O-UX07, O-UX08 | 19 owns scope-picker binding, tool activity, deltas, footer and terminal UI. |
| O-UX09, O-UX10 | 18 owns shared reader/ordinary-reader access; 19/20 use the destination, and 20 also owns graph-route access. |
| O-UX11, O-UX12, O-UX13, O-UX14 | 20 owns graph interaction/states; 18 shares applicable reader loading/error states. |
| O-OPT01, O-OPT03 | 18: batch recovery secondary, Pass 2 tools conditional; neither is an initial completion prerequisite. |
| O-OPT02 | 19: advanced transitions secondary; fixed-conversation fallback is sufficient. |

## Epic List

The user approved this structure on September 27. IDs 18-20 continue after existing
canonical Epics 13-17 and are October-only planning identities, not activated queue
entries or replacements for the existing backlog. The 17 story definitions below were
authored under single-pass authorization; epic approval neither completes them nor authorizes implementation.

### Epic 18: Generate and Read a Source-Backed Batch Wiki

**User outcome:** An operator generates a real local/cross-Repository wiki from
prepared source, and an authorized reader can inspect its exact CL/BL Documents,
supported relationships, qualifications and coverage gaps in the existing Web app.
This is useful without Chat or a graph page.

**Scope:** A1-A5 plus B2's minimum shared reader. Prepared roots/context, file tools,
native CodeGraph MCP, restricted JDT-LS, bounded file analysis, accepted-file storage,
source-supported static matching, rooted flows, document-first BL and coherent
publication form one end-to-end outcome. Include owned database/batch binding,
ordinary-reader authorization and the shared search/read/related-document ports.
Do not implement separate consumer persistence or make this a database-only epic.

**FRs covered:** O-FR01-O-FR19, O-FR30, O-FR33, O-FR35; shared foundations of O-FR20
and O-FR22; generation/publication/reader portion of O-FR34.

**Boundary:** No Git/snapshot/Docker prerequisite, ordinary Web Scan initiation,
update/reconciliation engine, management-UI rebuild or mandatory durable batch
recovery. O-OPT01 and O-OPT03 retain their secondary/conditional dispositions.

**Dependencies and evidence:** Requires authorized prepared inputs/provider/runtime
only for real execution, not to begin planning. It depends on no later epic.
Prove useful accepted generation and ordinary authorized reading in small integrated
slices; later Chat/graph consumers do not determine whether its own outcome works.

### Epic 19: Have Useful Streaming Conversations with the Wiki

**User outcome:** Business and IT users ask questions and follow-ups, receive natural
multi-document answers with genuine live text and tool activity, and open the exact
permitted documents reported below the answer.

**Scope:** B1/B3 plus B2's footer. Own the Chat-purpose adapters over shared wiki reads,
bounded model/tool loop, useful dialogue, mode/scope enforcement, streaming events,
terminal save/interruption, document-report tool and integration into the existing UI.
Keep the answer as natural prose and the footer as navigation, not sentence proof.

**FRs covered:** O-FR21-O-FR29; Chat-tool portion of O-FR20 and Chat portion of O-FR34.
Consume O-FR19 provider binding and O-FR30 reader rather than duplicate them.

**Boundary:** No graph dependency, Chat source-scanning feature, full reconnect,
visual redesign or mandatory advanced mode/scope transition machinery. O-OPT02's
fixed-conversation/empty-new-conversation fallback is sufficient. Business remains
BL-only with live contributor authorization.

**Dependencies and evidence:** Can start after wiki/read and Chat event contracts
are agreed, using labelled test adapters. Real qualification needs Epic 18's actual
published Knowledge and reader, not all scanner work to be finished before coding.
The graph is neither a runtime prerequisite nor an acceptance dependency for Chat.

### Epic 20: Explore Real Document Relationships

**User outcome:** Authorized users visually inspect how generated Documents relate,
find relevant nodes, explore a bounded neighborhood and open exact Documents while
understanding relationship meanings and remaining uncertainty.

**Scope:** C1. Own graph projection/query and its page: actual selected-batch data,
permitted filters, title search/focus, pan/zoom/fit/reset, node inspection/neighbors,
bounded expansion, legends and loading/empty/error/manual-refresh states. Consume
the common publication/read contract and the single Epic 18 reader destination.

**FRs covered:** O-FR31-O-FR32; graph enforcement of O-FR22 and graph portion of O-FR34.
Consume O-FR30 without implementing another reader.

**Boundary:** No Chat-session dependency, new graph database, graph editing, invented
business edges, live update engine or full history explorer. Preserve ordinary-user
Knowledge access, Chinese styling and BL-only Business projections; sparse graphs
are legitimate.

**Dependencies and evidence:** Can start after wiki relationship/read contracts are
agreed. Fixtures enable UI/query development, not generated-graph acceptance. Real
qualification needs accepted REFERENCES/GENERATED_FROM, actual BL dependencies and
the shared reader from Epic 18; it does not wait for Epic 19's Chat implementation.

### Why Three Epics

The October direction is sufficiently settled to keep fewer cohesive epics. Scanner,
acceptance/storage and minimum reading are tightly coupled around one useful wiki;
splitting them into technical-layer epics would not make them independently useful.
Chat and graph have different user outcomes and UI ownership, so keeping them
separate supports parallel delivery. Epic 18 is larger, but its tool, persistence,
analysis and reader lanes need not run serially. The story-sized slices below retain
these user outcomes and explicit parallel-start boundaries.

## Shared Delivery Rules

### Parallel Starts Versus Runtime Dependencies

```text
F0 source/tool agreement -> list/read/search | CodeGraph | JDT-LS
F0 wiki/read agreement  -> store | scanner | shared reader | Chat | graph
                           (labelled adapters permit parallel development)

Real generation/publication + shared reader (Epic 18)
                         +-> real Chat integration (Epic 19)
                         +-> real graph integration (Epic 20)
All real portions -------> C2 selected-dataset release qualification
```

F0 is the existing short coordination milestone, not a fourth epic, completed
implementation or permission to start work. Release its source/tool contract first
and wiki/read contract as soon as the necessary examples agree; do not wait for a
complete database or design every downstream detail. Chat events and graph projection
details are owned by their respective consumers against that common contract.

No dependency above means "wait for every story in Epic 18 to close before starting
Epic 19 or 20." Independently implementable lanes can start at their contract boundary.
Real integration replaces fixtures progressively, following the already selected
one-file/small-cross-Repository/real-batch sequence. Graph does not wait for Chat,
and Chat does not wait for graph. Source tools do not wait for the Knowledge database.

**C2 ownership:** the contract/integration owner maintains the combined evidence and
selected dataset binding; each lane supplies its real checks. C2 aggregates the
already requested release qualification, not a new quality gate or the first time
the system is integrated. An epic's native outcome can be useful before C2; whole
October release completion cannot be claimed until the real chain is demonstrated.

### Shared-File Ownership and Adoption Boundary

Use one owner for shared contracts, package/project changes, DI/tool registrations
and October Web selection/routes; one Knowledge owner for October mappings/migrations.
Keep scanner/agent code, Chat UI/turn lifecycle and graph UI/projection changes with
their respective lane owners. The shared reader has one owner within Epic 18 and
is consumed by both later epics. Incidental common composition files require
coordinated integration, not a mandatory merger of all work into one writer.

The read-only overlap check at the recorded checkout identified these existing
integration points; these paths are coordination evidence, not edit instructions:

| Shared area | Existing hotspot and ownership implication |
| --- | --- |
| Host/API registration | Web `Program.cs`, `Api/ApiEndpointRegistrationExtensions.cs` and Agent/Infrastructure DI: integration owner coordinates opt-in wiring. |
| Model isolation | Infrastructure `Data/VulgataDbContext.cs` scans assembly configurations; normal Web startup applies migrations. Knowledge owns separate October mappings/history, integration owns opt-in startup, and the normal model stays unchanged. |
| Version/read contracts | Existing `KnowledgeStoreContracts.cs`, `ChatContracts.cs` and `PreparedChatQuestionRequest.cs` are legacy counterparts, not proof that October exact-version handles already exist. Agree additive shared contracts once. |
| Authorization | Web `Data/RepositoryAuthorizationVerifier.cs` is still pair/common-System constrained. Reuse real Identity context, but supply the October-authorized read boundary without relaxing that normal verifier or copying consumer-specific bypasses. |
| Reader/routes/navigation | Management `DocumentsPage.razor` and `GraphPage.razor` are management-only placeholders; layout/navigation is shared. One reader owner and coordinated routes prevent accidental exclusion of ordinary users. |
| Packages/global assets | `Directory.Packages.props`, Web project, `App.razor` and `_Imports.razor` are shared entry points. Integration owns coordinated changes; Chat and graph keep distinct feature-local UI/assets. |

No October production composition was found or qualified by this planning check.
The overlap is shared wiring/contracts, not evidence that Chat and graph must be
implemented serially or that a generic refactor is needed first.

The confirmed stories define earlier-story or contract prerequisites without
future-story cycles, retaining end-to-end slices and parallel lanes rather than
three serial monolithic tasks. Formal queue adoption/activation is still separate: governed
delivery currently reads `docs/bmad/epics.md`, not this document, so no runner should
be pointed at an October story until deliberate routing/tracker adoption.

**Decision recorded:** The 17-story set in Epics 18-20 and its ownership/dependency
structure are confirmed; the definition workflow is complete. Requirements and
October exclusions are unchanged. This approval does not authorize implementation.

### Authorization and Common Story Boundaries

On September 27 the user authorized creation of all stories in one pass with the
October 14 deadline in mind. This replaces individual story/epic drafting pauses,
not governed feature plan review, planned-change authorization, implementation
approval or verification. These are newly authored definitions, not individually
approved implementations. The canonical epic file/tracker remain unchanged.

Each story is one bounded delivery outcome, not a separate rewrite of its entire
module. Introduce only storage records needed by that story. All stories inherit
the October scope/optional-target rules above, real resource authorization, normal
composition isolation and live disclosure safeguards. Reuse existing compatible
helpers; do not repeat platform work or the same broad test matrix in every story.
Do not interpret a fixture's successful read as evidence of real model generation.

**Parallel start** below means work may begin against an agreed F0 contract with
labelled test adapters. **Completion dependencies** name the earlier implementations
needed to claim that story's own outcome; a fake does not complete those obligations.
Dependent feature branches may integrate earlier stories before their whole epic
closes. The ownership and F0 rules in this section apply throughout.

**Coordination requirements:** O-AR05, O-AR06, O-AR07, O-AR08, O-AR11, O-AR12 are
owned across the story set by the integration owner: release the source F0 slice
with 18.1, wiki/read slice with 18.4/18.5 and its flow/BL extensions before consumers
diverge; coordinate shared files/migrations and isolated resources throughout;
integrate real slices through 18.7/18.11/18.12 and 19.3/20.2. This adds no coordination
epic, independent platform implementation or duplicated approval ceremony.

Deadline baseline: use the allowed fresh-batch recovery fallback and fixed
conversation mode/scope. No dedicated story is assigned to optional checkpoint
restart, advanced transitions or initial Pass 2 source/navigation tools. Add the
latter only on demonstrated quality need and through the owning tool-change gate.

## Epic 18: Generate and Read a Source-Backed Batch Wiki

An operator produces a real source-backed wiki from prepared Repositories and an
ordinary authorized user reads its CL/BL Documents and limitations. Delivery includes
source/tool capability, valid batch setup, semantic acceptance, analysis, static
resolution, BL publication and the shared reader. No Chat/graph implementation is
required to complete this epic's outcome.

### Story 18.1: Access Prepared Source Through Contained Read-Only Tools

As a batch operator,
I want approved prepared directories exposed through bounded list, read and search operations,
So that analysis can inspect selected source without Git acquisition or arbitrary host access.

**Requirements:** O-FR01, O-FR04; O-NFR01.  **Completion dependencies:** none.
**Parallel start:** immediately after the source/tool F0 contract is agreed in this plan; no database/provider prerequisite.

**Acceptance Criteria:**

**Given** an application/operator-approved batch, Repository and root binding,
**When** source access is requested,
**Then** roots are selected outside model arguments and public locations are Repository-relative,
**And** labels are not verified Git revisions; no copying, Git inspection, hashing or retention service is introduced.

**Given** files of known or unfamiliar extensions under the granted root,
**When** listing, ranged reading or text search exceeds one response,
**Then** accurate locations and explicit continuation/limit information permit further access,
**And** incomplete/unreadable results are distinct from complete empty results; listing makes no admission decision.

**Given** a foreign binding, path/symlink escape or requested write/command,
**When** the caller invokes the capability,
**Then** it is denied without access outside the root,
**And** missing inputs and cancellation return sanitized truthful outcomes and release owned resources.

**Given** small owned prepared fixtures,
**When** the production capability is invoked directly,
**Then** its normal and containment paths are observable without DB, LLM, CodeGraph or LSP,
**And** existing normal Git-bound source behavior is unchanged; no UI or management CLI is required.

### Story 18.2: Navigate Prepared Java Source with Restricted JDT-LS

As a source-analysis operator,
I want real whole-Repository Java navigation under the declared restricted profile,
So that Workers can use local definitions/references and understand its limitations.

**Requirements:** O-FR06; O-AR09, O-NFR01.  **Completion dependencies:** 18.1.
**Parallel start:** source/tool F0; independent of 18.3 and all database work. Prioritize this tool risk.

**Acceptance Criteria:**

**Given** a granted prepared Java root and explicitly configured local JDT-LS/Java installation,
**When** a session starts,
**Then** it opens the whole bound Repository with separate tool state and automatic Maven/Gradle import, build and dependency restore disabled,
**And** at most one session per Repository is owned; no model-selected workspace or arbitrary adapter is accepted.

**Given** representative declarations and callers in a small prepared source case,
**When** definition/reference navigation is exercised through the production adapter,
**Then** real protocol results map to granted relative locations with capability/classpath limitations,
**And** missing/out-of-scope results are not proof of absence or permission for an outside-root read.

**Given** concurrent requests, cancellation or failed readiness/navigation,
**When** the adapter handles them,
**Then** unsafe shared operations are serialized, time is bounded and owned processes/state are settled without stopping unrelated services,
**And** the caller receives explicit limitations and can use 18.1 file tools; this file-tool fallback does not replace required CodeGraph.

**Given** qualification of this declared host profile,
**When** evidence is recorded,
**Then** actual navigation is distinguished from initialization alone,
**And** no other LSP, dependency-aware import, Docker packaging or universal Java completeness claim is added.

### Story 18.3: Use Native CodeGraph MCP Against Prepared Source

As a Worker-tool integrator,
I want CodeGraph preparation and native MCP tools bound to the prepared Repository,
So that agents can explore the index without a custom query-protocol replacement.

**Requirements:** O-FR05; O-AR09, O-NFR01.  **Completion dependencies:** 18.1.
**Parallel start:** source/tool F0; independent of JDT-LS, Knowledge storage and real model generation.

**Acceptance Criteria:**

**Given** an approved root and declared CodeGraph installation,
**When** preparation runs and native MCP connects,
**Then** real discovery and invocation are available through the application's agent-tool integration,
**And** project/root selection is application-bound rather than a caller-controlled physical path.

**Given** a small indexed fixture and a foreign-root invocation attempt,
**When** tools are exercised,
**Then** genuine native results and correct-root binding are observable and the foreign request is rejected,
**And** neither CGC nor a CLI-only response wrapper is reported as native MCP.

**Given** preparation/connection/invocation failure or cancellation,
**When** readiness is reported,
**Then** it is explicit and owned processes are settled,
**And** required-CodeGraph failure cannot silently select source-only analysis; file tools remain independently usable.

**Given** the selected host composition,
**When** qualification is completed,
**Then** evidence records the actual tool version and small native smoke result,
**And** it does not require a new research spike, protocol framework or provider call merely to establish MCP connectivity.

### Story 18.4: Prepare an Owned October Batch with Valid Context and Provider Bindings

As a batch operator,
I want one valid isolated batch setup shared by the generator and October Web,
So that generation and later readers address the intended data without polluting normal or other batch data.

**Requirements:** O-FR01, O-FR02, O-FR17, O-FR18, O-FR19; O-AR01-O-AR04, O-NFR03.
**Completion dependencies:** 18.1.
**Parallel start:** source identity and wiki/setup F0; no JDT/CodeGraph or model analysis dependency.

**Acceptance Criteria:**

**Given** an explicitly authorized owned database target and real operator identity,
**When** programmatic October setup is requested,
**Then** valid System/Repository/context bindings and October batch metadata are created through shared rules,
**And** only this story's setup records/mappings are introduced; no fake administrator, dummy pair/FlowGroup, forced ScanRun or new CRUD UI is used.

**Given** two distinct owned batch databases with identical fixed schema names,
**When** generator and October Web start with an exact database and batch ID,
**Then** both bind the selected setup and reject missing/mismatched selection without previous/latest-batch fallback,
**And** a switch requires restart; no read/write crosses into the other target.

**Given** selected Global/System/Repository context and explicit provider configuration,
**When** setup is bound,
**Then** actual context values/origins and conflicts are retained, and API type/Worker/Chat role profiles and configuration precedence are explicit,
**And** no unspecified .env discovery, borrowed credentials, silent field mixing or automatic failover occurs; protected-row copying requires compatibility only if that route is used.

**Given** October enabled versus ordinary startup,
**When** mappings/migrations and registration are evaluated,
**Then** the October persistence context, migration history and model snapshot are isolated; ordinary startup and the normal model remain unchanged,
**And** setup emits sanitized diagnostics without claiming tool readiness, paid-provider readiness or completed Knowledge generation.

### Story 18.5: Accept Semantic File Knowledge and Read Exact Published Results

As a Knowledge consumer,
I want validated whole-file results accepted once and read through shared exact-version interfaces,
So that drafts and invalid provenance cannot contaminate the wiki used by later consumers.

**Requirements:** O-FR07, O-FR08, O-FR09, O-FR16, O-FR20, O-FR22, O-FR33; O-NFR02, O-NFR04.
**Completion dependencies:** 18.4.
**Parallel start:** wiki acceptance/read F0 with representative labelled proposals; no real Worker required.

**Acceptance Criteria:**

**Given** a completed file proposal with several meaningful units or a justified zero-unit result,
**When** Knowledge accepts it,
**Then** the application derives identities and atomically commits the Accepted File Manifest, immutable structured CL versions, observations and coverage disposition,
**And** source windows/tool symbols are not Document identities; invalid anchors, foreign identities or incomplete drafts cannot partially become accepted results. A valid admitted-file binding comes from the agreed contract, not a future scheduler or completed legacy ScanRun.

**Given** a repeated acceptance, lost acknowledgement or conflicting payload,
**When** the same operation is applied,
**Then** an identical replay returns the accepted result and a conflict leaves prior data unchanged,
**And** failed/stopped drafts remain invisible while material context/hypothesis qualifications survive in accepted structure.

**Given** an explicitly published CL-only controlled fixture and a real authorized read context,
**When** search, content/section and related-document ports are used,
**Then** exact handles, provenance, pagination and gaps are returned with live scope/mode/contributor checks,
**And** Business receives no CL or hidden metadata, unpublished drafts are ineligible and no BL or confirmed relationship is invented.

**Given** a logical search/read request through the shared Knowledge boundary,
**When** it is served or denied,
**Then** the existing/shared logging mechanism emits a sanitized append-only log.md operation/target/timestamp entry,
**And** reader and later graph consumers use this mechanism rather than separate logging implementations; raw source, credentials and unnecessary sensitive query content are excluded.

**Given** existing normal model and separate October setup,
**When** this story's persistence and read implementation is integrated,
**Then** only file/Document/read-publication records needed here are added through the one Knowledge owner,
**And** controlled data uses the real acceptance/publication route and remains labelled fixture evidence, not real analysis; flow/BL acceptance is not prebuilt wholesale.

### Story 18.6: Read Exact Knowledge Documents Without Management Permissions

As an authorized Knowledge reader,
I want one readable exact-version Document destination in the existing Web shell,
So that I can inspect available Knowledge and later follow Chat or graph links without administrative access.

**Requirements:** O-FR30, O-FR22, O-FR33; O-UX01-O-UX03, O-UX09, O-UX10, O-UX14-O-UX16.
**Completion dependencies:** 18.5.
**Parallel start:** wiki/read/reader-destination F0; isolated reader UI owner, independent of analysis tools and Chat.

**Acceptance Criteria:**

**Given** a permitted exact Document handle and section,
**When** an ordinary authenticated user opens the destination,
**Then** the shared reader renders derived structured content, useful type/title cues and material limitations in the current Chinese Fluent/Library Oak style,
**And** no management role, source viewer, compulsory inline preview or history explorer is required.

**Given** live denial, an unknown/unpublished handle, empty permitted scope or a failed load,
**When** the reader responds,
**Then** it shows a truthful mode-appropriate empty/unavailable/error/retry state without leaking content or technical metadata,
**And** Business can correctly be empty before BL exists; neither current checkout nor another batch is substituted.

**Given** actual accepted CL from 18.5,
**When** the page is exercised end-to-end,
**Then** authorized IT reading and Business denial/empty behavior work with keyboard/basic accessibility,
**And** labelled component examples can exercise the agreed BL shape without claiming generation. Story 18.10 supplies accepted BL read data, 18.11 supplies real BL generation and 18.12 verifies their integration with this same reader; this story needs no later BL implementation to finish its own CL/empty-Business boundary.

**Given** a reader open or permitted content query,
**When** its shared read request completes or is denied,
**Then** the shared log.md path records the sanitized operation and target,
**And** browser rendering does not add a second raw-content/query log or expose restricted target details.

### Story 18.7: Generate Whole-File Code Logic with a Real Worker

As a batch operator,
I want a bounded Worker to analyze an admitted file using the prepared-source tools and context,
So that useful semantic CL and communication observations are generated rather than imported or preallocated.

**Requirements:** O-FR07-O-FR09, O-FR19; O-NFR01, O-NFR05, O-NFR08, O-NFR10, O-NFR11.
**Completion dependencies:** 18.2, 18.3, 18.5.
**Parallel start:** file-proposal/source-tool F0 with labelled adapters; real completion integrates the earlier adapters/store.

**Acceptance Criteria:**

**Given** one authorized file deliberately admitted by its caller under a declared policy, fixed context/provider and required CodeGraph readiness,
**When** the real MAF Worker runs,
**Then** it can use scoped file tools, native MCP and declared JDT capabilities and proposes meaningful units/CL/observations through the externalized prompt,
**And** it does not accept Knowledge, choose credentials/roots or schedule another file scan; JDT limitations retain the file-tool fallback.

**Given** a file needing more than one bounded processing window,
**When** work continues or exhausts its allowance,
**Then** continuation preserves the logical whole-file result and avoids duplicate window Documents, or records an explicit unfinished/failed gap,
**And** no partial draft becomes Knowledge or claims complete coverage; durable restart is not required.

**Given** unfamiliar source conventions and supplied recognition context,
**When** observations/explanations are proposed,
**Then** original identifiers, caller/provider source facts and material qualifications remain distinct from context-led hypotheses,
**And** ordinary imports do not automatically become communication gaps or source proof; retrieved text cannot change tool policy.

**Given** an authorized small real provider run,
**When** its result is accepted and read through 18.5,
**Then** real model/tool activity and useful source-checked CL are recorded separately from schema/test success,
**And** absent required external authority remains an honest qualification blocker; source/provider identity cannot silently change.

**Given** a transient provider connection/throttling failure under a declared attempt/elapsed limit,
**When** Scan retries the same logical file input,
**Then** increasing bounded waits and the classified failure are observable without duplicate acceptance,
**And** exhaustion returns failed/interrupted work with drafts unaccepted; configuration, authorization or integrity errors are not blindly retried and no durable-restart promise is added.

### Story 18.8: Analyze a Batch with Explicit Admission, Bounded Concurrency and Safe Stop

As a batch operator,
I want an easily tuned Worker pool and truthful file coverage with graceful stop,
So that prepared source can be processed in parallel and accepted work can be retained within available time.

**Requirements:** O-FR03, O-FR10, O-FR11, O-FR33; O-NFR05, O-NFR06; O-OPT01 fallback.
**Completion dependencies:** 18.7.
**Parallel start:** admission/Worker-result F0; controlled Worker adapters support scheduler development.

**Acceptance Criteria:**

**Given** a deterministic inventory and batch-bound context/policy,
**When** any LLM treatment/ordering proposal is applied,
**Then** Scan Control records the policy identity used, final admitted/supporting/excluded decision and supported exclusion/demotion reasons,
**And** unknown extensions, low confidence or index omission alone cannot remove primary coverage; unsupported exclusions stay admitted.

**Given** the accepted upstream source/tool-state binding for the selected prepared Repository and batch,
**When** Scan Control builds source work and coverage accounting,
**Then** positively identified application-owned tool state creates no source-analysis jobs and contributes nothing to source-coverage counts or denominators; record its location, owning tool and exclusion basis separately through existing policy/diagnostic records,
**And** a directory name such as `.codegraph` alone cannot establish ownership or justify hiding source. Unproven exclusions retain the existing conservative admission treatment; preparation ownership collisions are returned to the tool owner, not silently overwritten or reclassified.

Consume the one accepted upstream distinction; do not introduce another reader
filter, directory-copying service, repository-wide hash pass, index or persistence
schema. A location-level exclusion record is sufficient; do not recursively enumerate
cache contents merely to count excluded files. Missing upstream agreement is an
explicit integration dependency, not permission to infer it from names or invent a
consumer-local contract. This admission rule does not itself authorize tool-state
writes or repair source-tool traversal/continuation.

**Given** more ready files than available slots,
**When** the host batch dispatches work,
**Then** dispatch respects one easily edited local concurrency cap and permits one active writer per file while independent work progresses,
**And** provider calls/retries and unsafe shared tool access stay bounded; no fairness platform, cross-process admission service or dependency-waiting Workers are introduced.

**Given** natural batch completion, with terminal outcomes for all admitted file work, or an explicit graceful operator stop,
**When** dispatch closes and active writes settle or are fenced,
**Then** a fixed accepted corpus and explicit failed/unprocessed gaps are available to later finalization,
**And** one file finishing cannot freeze the batch; unfinished drafts are excluded and this checkpoint is not a published BL wiki. Operator stop needs no all-files-success or content-quality threshold.

**Given** a local file failure, provider outage, cancellation or timeout,
**When** the applicable bounded policy finishes,
**Then** owned work/capacity is settled, unrelated accepted data is preserved and actual outcomes are visible,
**And** unsupported restart requires an explicitly requested fresh batch/new owned DB; crashes and exhausted retries do not auto-finalize or auto-resume.

**Given** transient provider failures or a non-retryable configuration/authority error,
**When** retry classification is applied,
**Then** transient Scan attempts use increasing waits within the same declared attempt/elapsed budget,
**And** exhausted provider retries or non-retryable failures stop affected dispatch and settle owned work with failed/interrupted status, not natural successful batch completion; ordinary isolated file errors remain explicit coverage outcomes.

**Given** controlled concurrent work and one small real batch,
**When** evidence is recorded,
**Then** file coverage, unit/Document counts, tool/model/store waits and available usage remain distinct,
**And** sanitized append-only log.md retains the required operation/target/timestamp format without new observability UI or universal throughput claims.

### Story 18.9: Resolve Source-Supported Local and Cross-Repository Relationships

As a Knowledge reader,
I want supported relationships and unresolved limits recorded over the fixed batch corpus,
So that business explanations can connect actual source behavior without invented counterpart mappings.

**Requirements:** O-FR09, O-FR12, O-FR13, O-FR14; O-AR10, O-NFR05, O-NFR08, O-NFR10, O-NFR11.
**Completion dependencies:** 18.5.
**Parallel start:** accepted-observation/relationship F0; use a fixed controlled corpus without waiting for the full batch runner.

**Acceptance Criteria:**

**Given** fixed accepted CL/observations and prepared caller/provider evidence,
**When** local links and cross-Repository candidates are considered,
**Then** source-supported local REFERENCES are retained and bounded retrieval uses original/normalized identifiers and relevant signals,
**And** exact equality is not the sole filter; candidate limits, unknown routing and available-evidence coverage remain visible.

**Given** a bounded candidate set and advisory framework context,
**When** the matching Worker proposes counterpart(s),
**Then** it supplies supporting/opposing source references, selected inputs, concise decision basis, limitations and matching policy/prompt identity,
**And** context-only maps, lone candidates, confidence, missing decisive provider evidence or material conflicts cannot establish a supported link; declaration-only support does not imply an implementation binding.

**Given** a relationship/Uncertainty proposal,
**When** Knowledge accepts it,
**Then** scope, candidate identities, exact versions, references, explicit constraints and conflicting/duplicate writes are checked before atomic acceptance,
**And** accepted relationships, hypotheses and one scoped unresolved outcome stay distinct; validation does not claim independent semantic proof or a universal hardcoded matcher.

**Given** the initial prepared mechanism and a small real matching exercise,
**When** aliases, harmless optional omissions, namespace/operation conflicts and missing-provider evidence are tested,
**Then** supported multiple targets and bounded cycles remain finite and unresolved cases state their evidence/coverage limitation,
**And** no global-absence claim, fixed Repository-pair profile, late-evidence refresh, extra auditor agent or exhaustive protocol matrix is required.

The matching check may use clearly labelled accepted CL/observation fixtures tied
to real small prepared source. It proves this resolver, not Pass 1 generation;
18.12 separately qualifies resolver inputs produced by the real file Workers.

Matching uses O-NFR05's classified, increasing bounded Scan retries over the same
pinned candidates/evidence. Exhaustion records failed/incomplete investigation,
not source proof that a provider is absent; non-retryable errors require correction.

### Story 18.10: Accept Distinct Rooted Flows and Coherent Business Knowledge

As a business reader,
I want each business explanation published with one source-backed flow and its exact dependencies,
So that I can read coherent accepted Knowledge without mixed or invented provenance.

**Requirements:** O-FR14, O-FR16, O-FR20, O-FR22; O-NFR02, O-NFR04.
**Completion dependencies:** 18.9.
**Parallel start:** flow/BL/publication F0; controlled accepted evidence suffices for this acceptance/read capability.

**Acceptance Criteria:**

**Given** fixed accepted units, source-supported references and resolution outcomes,
**When** flow roots/evidence are selected,
**Then** distinct source-backed operations/handlers/schedules/workflows/selected units retain their own Logical Flow and bounded directed evidence,
**And** local-only roots qualify, branches/cycles/uncertain frontiers are explicit, and helpers/folders/shared cycles never merge business purposes.

**Given** a valid BL proposal and unchanged authorized inputs,
**When** Knowledge publishes it,
**Then** structured content, exact dependencies/GENERATED_FROM, flow selection and relationship/Uncertainty evidence become coherently visible once,
**And** invalid dependencies/conflicting replay leave no partial graph/content; older accepted content is not overwritten and unrelated future flow/update tables are not added.

**Given** controlled BL proposals for accepted local and cross-Repository evidence,
**When** Business/IT content is requested through shared read ports,
**Then** exact-version content/section handles retain mode-appropriate structure, live contributor checks and context/hypothesis qualifications,
**And** test data is labelled and accepted through production rules, not imported as model-generated Knowledge; no separate reader or finished reader UI is required for acceptance qualification.

**Given** sparse evidence, partial coverage or no eligible root,
**When** flow/publication outcomes are recorded,
**Then** bounds, gaps and absent BL remain truthful without inventing roots/edges,
**And** publication adds no all-file or semantic-score gate and needs neither a live provider nor later Chat/graph code to verify its contract.

### Story 18.11: Generate Useful Document-First Business Explanations

As a business reader,
I want local and cross-Repository flows explained in understandable business terms,
So that source-backed technical Knowledge reveals rules, alternatives and effects.

**Requirements:** O-FR15, O-FR16, O-FR30; O-NFR05, O-NFR08, O-NFR10, O-NFR11; O-OPT03 conditional.
**Completion dependencies:** 18.10.
**Parallel start:** flow/BL proposal F0, independent of the batch dispatcher and Chat/graph work.

**Acceptance Criteria:**

**Given** a fixed rooted evidence set and selected advisory context,
**When** the real Pass 2 Worker runs its externalized prompt,
**Then** it explains supported conditions, alternatives, data/state effects and unknowns using accepted Documents/relationships,
**And** initial execution has no source/LSP/CodeGraph tools; actual extra CL used is recorded in the resulting dependency set.

**Given** context-only assertions, mixed source/context claims or relationship uncertainty,
**When** BL is synthesized and submitted,
**Then** material qualifications/references survive at section or statement-group level,
**And** fluency cannot create accepted source proof, rewrite CL/Edges or trigger recursive scans; retrieved content remains data rather than authority.

**Given** an accepted proposal or failed/retried attempt,
**When** the Worker invokes 18.10's publication boundary,
**Then** exact dependencies and coherent acceptance rules apply with bounded failure handling,
**And** transient Scan-provider retries use increasing bounded waits over the same pinned evidence; configuration/authorization/integrity failures require correction. Exhaustion leaves an explicit failed/interrupted synthesis outcome, never partial BL or forced successful publication.

**Given** an authorized small real local/cross-Repository case,
**When** published BL is inspected through the shared read contract against source-checked expectations,
**Then** business usefulness and missing details are recorded separately from structural success,
**And** only a demonstrated important gap justifies considering O-OPT03's scoped source-tool addition; no extra quality gate or semantic-auditor platform is introduced.

### Story 18.12: Finish a Prepared Batch into a Real Readable Wiki

As a presentation-data operator,
I want one host batch to run preparation through finalization and publish the selected wiki,
So that the Web app has genuine broad Knowledge from the prepared business inputs before presentation work begins.

**Requirements:** O-FR01, O-FR11, O-FR17-O-FR19, O-FR33-O-FR35; O-NFR06, O-NFR11, O-NFR12.
**Completion dependencies:** 18.6, 18.8, 18.11.
**Parallel start:** stage/result contracts; real completion integrates all earlier producer/read capabilities.

**Acceptance Criteria:**

**Given** authorized prepared inputs, exact owned batch/database, configured provider/context and declared tools,
**When** the operator starts the host batch,
**Then** one composition runs preparation, admission/file work, fixed-corpus resolution, rooted BL and publication with no manual intermediate stage-start calls,
**And** missing prerequisites fail explicitly rather than invoking legacy pair routing, restoring CGC or substituting old wiki content.

**Given** the qualified upstream prepared-source/tool-state contract and the selected real CodeGraph adapter in an owned small prepared fixture,
**When** native preparation/MCP use and representative tool-state activity are exercised together with production source listing, ranged reading, search and multi-page continuation,
**Then** source results remain contained and accurately located; tool-state contents are not returned as ordinary source evidence, dispatched as source jobs or counted as analyzed source, and useful source access works under the declared lifecycle and limits,
**And** observed drift, cancellation, authority loss and limits retain their existing truthful outcomes rather than becoming empty/completed reads or silent truncation. A permitted `ChangedInput` outcome is not itself a failure, but repeated inability to obtain useful source results cannot qualify the integration as successful.

Reuse the existing small-tool and batch qualification work; this is not a new
benchmark, additional provider run or exhaustive failure matrix. Include a bounded
supported continuation/drift case, not a guarantee that every old cursor survives
changes or that unobserved source mutation is detected. Do not suppress genuine
source/authority failures to accommodate cache writes, introduce automatic
retry/update scanning, or implement a second filter in the host. Route any missing
shared capability to its upstream owner. Controlled small-fixture evidence remains
distinct from the already-required real registration/login batch evidence.

**Given** natural finish or a graceful stop after partial accepted work,
**When** finalization runs,
**Then** it uses only the settled corpus, finishes bounded matching/synthesis outcomes and exposes available published Knowledge with coverage gaps,
**And** it requires no all-file/preferred-flow/content threshold; empty BL is truthful, while crashes/retry exhaustion remain failures/interruption rather than fabricated completion.

**Given** the two separately prepared registration/login Repositories and a compact source-checked expected-fact set,
**When** the real batch and ordinary-user reader are exercised,
**Then** evidence records actual broad admitted coverage, timings/available usage, representative CL/BL rules/branches/effects and at least one source-supported cross-Repository link plus a genuine unresolved/conflicting case,
**And** it distinguishes small fixture checks from real outputs without imposing an all-16-Repositories or fixed-pair eligibility rule.

**Given** that published dataset,
**When** the October Web host starts with the same binding,
**Then** authorized reading works independently of Chat/graph completion and the operator has a concise host start/select/stop/fresh-batch runbook,
**And** actual data/keys remain protected; no Web Scan demo, Docker/restore qualification, new dashboard or deadline-specific attestation platform is added.

## Epic 19: Have Useful Streaming Conversations with the Wiki

Business/IT users ask useful multi-document questions and follow-ups, see genuine
answer deltas and tool activity, and navigate the exact permitted Documents used.
This epic consumes Epic 18's read/publication contracts; it neither depends on graph
completion nor creates a second wiki, source scanner or acceptance policy.

### Story 19.1: Use Permitted Wiki Tools and Report Answer Documents

As a Chat user,
I want the agent to explore permitted wiki content and identify documents it used,
So that answers can combine useful Knowledge with honest navigable references.

**Requirements:** O-FR20-O-FR22, O-FR26; O-NFR02, O-NFR08; O-UX07, O-UX09.
**Completion dependencies:** 18.10.
**Parallel start:** wiki/read/Chat-purpose F0; no provider or completed real batch required.

**Acceptance Criteria:**

**Given** an authenticated user, selected published batch and server-bound mode/scope,
**When** search/browse, content/section or related-document tools execute,
**Then** they use shared Knowledge ports and return exact handles, useful content/provenance, material qualifications, gaps and explicit limits,
**And** the model cannot replace the batch, broaden access, inspect source, initiate scans or write Knowledge.

**Given** Business/IT mode and live contributor permissions,
**When** any tool returns content or metadata,
**Then** Business receives only permitted BL and IT permitted CL/BL,
**And** restricted titles, paths, counts and composite contributors cannot leak; an empty/denied result is truthful and no legacy evidence fallback broadens access.

**Given** handles actually read through permitted content/section tools in this turn,
**When** report-used-documents receives selected handles,
**Then** titles/links are derived server-side and duplicates removed in a stable order,
**And** invented, unread, foreign-turn or newly disallowed handles are rejected; not every accessed Document must be reported, and reporting is not sentence-level proof.

**Given** controlled CL/BL published through real acceptance rules,
**When** this tool layer is exercised without a provider,
**Then** bounded navigation, report validation and live authorization work against actual persistence,
**And** fixtures stay labelled; graph and ordinary-reader access remain independent of these Chat adapters.

### Story 19.2: Run Natural Streaming Conversations with Durable Outcomes

As a Chat user,
I want bounded multi-round answers and follow-ups with truthful durable outcomes,
So that useful conversation is not lost, duplicated or reduced to fixed evidence sentences.

**Requirements:** O-FR21-O-FR29, O-FR33; O-NFR02-O-NFR05, O-NFR07, O-NFR08, O-NFR10; O-OPT02 fallback.
**Completion dependencies:** 19.1.
**Parallel start:** Chat event/history/tool F0; test through a typed stream API, not a future UI implementation.

**Acceptance Criteria:**

**Given** a conversation with fixed mode, Repository scope and selected batch,
**When** a question or follow-up starts,
**Then** the turn uses one read context and bounded permitted completed history with its original qualifications/references, and dialogue is durably stored in order,
**And** only one answer is active; new mode/scope means an empty conversation, not reused technical history. No automatic-summary subsystem is required.

**Given** the declared Chat provider and externalized prompts,
**When** several wiki searches/reads/navigation rounds are useful,
**Then** a bounded real model/tool loop synthesizes natural prose,
**And** retrieved instructions cannot override application policy; there is no candidate-sentence allowlist, automatic source re-proof, Scan/repair or provider failover.

**Given** provider text interleaved with tool rounds,
**When** generation is still active,
**Then** typed events expose actual answer deltas, stable turn/tool identities, sanitized activity, reported-document updates and terminal outcomes,
**And** complete JSON, provider finish, save or footer readiness never gate prose; raw reasoning, credentials and raw tool arguments/results are not exposed.

**Given** valid untruncated provider completion with no unfinished tools,
**When** the terminal result is saved,
**Then** content/reference/status are accepted once through an idempotent durable save,
**And** duplicate turn submission or lost acknowledgement does not generate again; resolve the original save, and do not let telemetry undo completion. The schema owner adds only needed dialogue records.

**Given** provider/tool failure, limit exhaustion, cancellation, host interruption or revoked contributor access,
**When** the turn cannot continue safely,
**Then** further unauthorized disclosure stops, owned work settles and useful available permitted text keeps its interrupted/failed status,
**And** explicit retry cannot overlap unresolved work, interrupted text is not ordinary completed inference history, and no same-turn reattachment or token-replay ledger is required.

**Given** a transient Chat transport failure before visible answer output,
**When** retry safety is classified,
**Then** increasing bounded transport waits are allowed only if retry does not replay an uncertain generation or tool execution,
**And** after visible output or uncertain execution, interrupt rather than regenerate automatically; configuration/authority errors require correction. A lost terminal-save acknowledgement follows the same-save recovery rule instead of this generation retry path.

**Given** controlled failure/save-race streams and an authorized real provider using stored wiki data,
**When** the typed API is qualified,
**Then** separate evidence shows lifecycle correctness and a real multi-round answer/follow-up, timings and bounded adversarial-document handling,
**And** fixtures are not called generated Knowledge; append-only sanitized query/operation logging remains, without a new observability or semantic-auditor system.

### Story 19.3: Use Genuine Streaming Chat in the Existing Interface

As a Business or IT user,
I want natural live answers, concise tool activity and document references in the familiar Chat page,
So that I can understand and investigate the generated wiki without learning another interface.

**Requirements:** O-FR24-O-FR30, O-FR34; O-NFR07, O-NFR09, O-NFR11, O-NFR12; O-UX01-O-UX10, O-UX15, O-UX16.
**Completion dependencies:** 19.2, 18.6, 18.12.
**Parallel start:** Chat events/reader destination F0. Integrate a small real wiki from 18.11 immediately when available; only final selected-input qualification waits for 18.12.

**Acceptance Criteria:**

**Given** the current Chat shell and actual authorized October Repositories,
**When** an ordinary user creates a Business/IT conversation,
**Then** the existing picker binds its fixed scope, with Chinese Fluent/Library Oak and Business/light or IT/dark preserved,
**And** scope/mode changes open an empty conversation, optionally after current work settles; no visual redesign or advanced transition machinery is required.

**Given** an active streamed turn,
**When** real events arrive,
**Then** the assistant area shows compact initial progress, actual running/completed/failed tool activity and immediately growing prose through interleaved rounds,
**And** an end-to-end check observes visible text before generation finishes, without fake percentages, post-completion replay or footer/save gating.

**Given** reported document handles, an absent report or an empty-reference greeting,
**When** the footer renders,
**Then** deduplicated server-derived entries remain below prose and open the shared exact reader without management rights,
**And** reference updates never rearrange streamed prose; missing reporting is qualified honestly and legitimate empty reports are not failures.

**Given** generating, saving, completed, interrupted/failed or save-unconfirmed outcomes,
**When** status changes,
**Then** accessible Chinese feedback and useful permitted partial text remain with an explicit safe retry where applicable,
**And** keyboard/focus basics are preserved; no same-turn reconnect promise, hidden technical metadata or false completed state is shown.

**Given** 18.12's genuinely generated wiki, its exact selected database/batch and ordinary authorized users,
**When** roughly 10-15 representative questions including multi-document follow-ups, paraphrases and a genuine unknown are exercised,
**Then** real evidence shows useful mode-appropriate explanations, immediate streaming, tool activity and exact footer navigation,
**And** Chat quality is assessed separately from CL/BL correctness with actual timings/limitations; graph completion, source viewer and a new evaluation platform are not dependencies.

**Given** a small subset of that same real-wiki evaluation containing known-target queries, paraphrases and expected misses,
**When** search is assessed before answer synthesis,
**Then** evidence records which permitted expected Documents were found, missed or appropriately unavailable and any coverage/limit explanation,
**And** retrieval quality is distinguished from model answer quality; reuse the existing question set rather than add another benchmark platform or publication threshold.

## Epic 20: Explore Real Document Relationships

Users explore a readable, authorized graph of accepted Documents and their actual
relationships, then open exact content through the shared reader. The graph consumes
Knowledge directly and has no dependency on a Chat session or Chat implementation.

### Story 20.1: Query an Authorized Graph of the Published Wiki

As a Knowledge reader,
I want bounded graph data selected from the same published wiki I can read,
So that the visualization reflects real evidence without exposing restricted Documents.

**Requirements:** O-FR22, O-FR31, O-FR32, O-FR33; O-NFR02, O-NFR04; O-UX10, O-UX11, O-UX13, O-UX14.
**Completion dependencies:** 18.10.
**Parallel start:** wiki relationship/graph F0; labelled controlled evidence enables query development before real scanning.

**Acceptance Criteria:**

**Given** exact selected batch/read context and an authorized view mode/scope,
**When** a bounded neighborhood, title search or permitted filter is queried,
**Then** real CL/BL nodes and accepted REFERENCES/GENERATED_FROM with exact destinations, directions and available summaries are returned,
**And** limits/continuation are explicit; no CodeGraph source index or invented business topology substitutes for the accepted Document graph.

**Given** Business mode or changed contributor access,
**When** nodes, edges, labels, counts, tooltips and destinations are selected,
**Then** the server returns only permitted data, including BL-only Business output,
**And** no hidden CL payload, restricted aggregate or management-only prerequisite remains; graph mode/scope needs no Chat conversation.

**Given** hypotheses, dangling frontiers, shared helpers or sparse BL,
**When** relationships are projected,
**Then** supported links remain distinguishable from uncertainty and absent eligible edges remain absent,
**And** shared CL creates no inferred BL-to-BL dependency or deployed-execution claim, and layout never edits domain relationships.

**Given** controlled data accepted through 18.10,
**When** graph reads and reader destinations are verified,
**Then** query/filter/limit/auth behavior matches the selected persisted evidence,
**And** no new graph database, graph-write model, live refresh engine or current/history competition is introduced.

**Given** a logical graph query, expansion or refresh request,
**When** the shared Knowledge read boundary serves or denies it,
**Then** the shared log.md mechanism records the sanitized operation/target/timestamp,
**And** it does not log a second raw query/graph payload or leak denied node identities through user-visible diagnostics.

### Story 20.2: Explore the Document Graph and Open Its Evidence

As a Business or IT reader,
I want a usable graph page for finding, inspecting and navigating Documents,
So that I can see how the generated Knowledge connects and investigate it directly.

**Requirements:** O-FR30-O-FR32, O-FR34; O-NFR09, O-NFR11, O-NFR12; O-UX01-O-UX03, O-UX09-O-UX16.
**Completion dependencies:** 20.1, 18.6, 18.12.
**Parallel start:** graph projection/reader F0; UI development uses labelled fixtures without waiting for Chat or real source generation.

**Acceptance Criteria:**

**Given** an ordinary authorized reader,
**When** the graph page opens,
**Then** it shows the selected batch/context, permitted Repository/document-kind filters, title search/focus and an initially bounded neighborhood,
**And** feature design chooses a simple readable layout within existing Chinese Fluent/Library Oak themes, not a management-only route or visual redesign.

**Given** a populated permitted graph,
**When** the user pans, zooms, fits/resets, selects or expands a node,
**Then** immediate neighbors highlight, node title/kind/Repository/summary are inspectable and bounded expansion shows its limit/load-more state,
**And** the exact selected Document opens in the shared reader; Business receives no hidden technical filter or metadata.

**Given** relationship types, uncertain frontiers or sparse data,
**When** the page explains its graph,
**Then** readable labels/legend preserve direction, REFERENCES/GENERATED_FROM meaning and uncertainty,
**And** attractive layout adds no fake edges, BL business dependency or runtime-sequence claim; no editing or advanced/live portfolio feature is required.

**Given** loading, manual refresh, empty/no-results, denied access or failure,
**When** the page changes state,
**Then** truthful Chinese feedback, permitted retry/refresh and keyboard/basic accessibility remain usable,
**And** useful content stays visible during refresh where safe; color alone is not status and sparse graphs are accepted outcomes.

**Given** the real selected wiki from 18.12,
**When** representative permitted graph neighborhoods and reader links are exercised,
**Then** the displayed data can be traced to that exact published dataset and useful screenshots/recording can be captured,
**And** evidence distinguishes fixture layout tests from real generated Knowledge, with no Chat dependency or fabricated density. C2 combines this with independently qualified Chat later.

## Final Validation — September 27, 2026

**Result: PASS at story-definition level.** The corrected set contains 17 stories
across Epics 18–20. Validation used the October proposal as the primary scope
authority, with the compatible PRD, architecture and UX constraints retained.
Two read-only cross-checks independently traced producer and consumer requirements
to substantive acceptance criteria; neither found an omitted October obligation.

| BMad final-validation check | Result and basis |
| --- | --- |
| Requirements coverage | All 35 O-FR, 12 O-NFR, 12 O-AR, 16 O-UX and 3 O-OPT items have story or explicit shared-owner coverage. All 89 original PRD FR and 15 NFR identifiers have an October disposition; excluded obligations are not restored as prerequisites. |
| Architecture and incremental storage | Existing-project additive composition applies; no starter-template setup is required. Setup records belong to 18.4, file/CL/read records to 18.5, relationships to 18.9, flow/BL publication to 18.10 and dialogue to 19.2, under the designated schema owner. Normal models and composition remain protected. |
| Story quality | All 17 have a user outcome, requirement references, bounded ownership, completion dependencies and testable acceptance criteria: 79 Given/When/Then cases, plus explicit shared rules. Each is a coherent single-story delivery scope; this is not a duration estimate or a one-session completion promise. |
| Epic value and file churn | Epic 18 delivers a readable wiki, Epic 19 useful conversations and Epic 20 truthful graph exploration. Shared contracts, DI, migrations, routes and assets have designated owners; distinct consumers do not justify duplicate stores or readers. Separate Chat/graph ownership supports parallel work without a generic refactor prerequisite. |
| Dependency correctness | Every declared completion dependency names an earlier story; there are no forward edges or cycles. Epic 18 needs neither 19 nor 20; Chat and graph independently consume 18. Early fixture-based development is not substituted for real upstream acceptance. |
| Document integrity | Story counts remain 12/3/2; all 43 internal navigation links resolve, the local proposal link resolves, template placeholders are absent and whitespace checks pass. |

No further requirement or acceptance-criterion change was necessary in this
validation. Largest delivery risks remain real restricted-profile JDT behavior,
isolated Knowledge integration, generated-content usefulness/throughput and the
streaming Chat lifecycle; the existing early integration targets address them
without adding gates or restoring cut scope. Dates remain working targets.

The user selected **[C] Complete Workflow** on September 27; the epics-and-stories
workflow is complete. Canonical queue adoption/routing is a separate next step:
`docs/bmad/epics.md` and sprint status are
unchanged, no feature is active, and governed story plan/tool/Scan/PRD and
implementation approvals remain required. No build, runtime, database, provider or
production-code verification is claimed by this document check.
