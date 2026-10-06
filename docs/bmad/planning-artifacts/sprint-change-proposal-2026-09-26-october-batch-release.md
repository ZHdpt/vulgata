---
title: October 14 batch wiki, conversational Chat and document graph
date: 2026-09-26
updated: 2026-10-06
status: proposed-selection
scope: october-14-release-only
source_commit: 2d8b280f921507abf986ba11e439ec57c555accc
implementation_authorized: false
---

# October release selection and course-correction proposal

## Decision and authority

**October 6 first-deliverable selection:** the user requested revised inputs for
the four feature groups, using integrated narrowed 18.2/18.3 and completed 18.4,
selectively absorbing 18.5's design without executing that full story. The
[current delivery plan](october-delivery-plan-2026-09-30.md) and its four briefs
govern this smaller delivery scope; the [integration plan](october-integration-plan-2026-10-06.md)
reserves combined verification and presentation time. Earlier breadth, story
dependencies and package outlines below remain broader planning history where the
new selection defers them. Compatible source integrity, authority and evidence
rules continue to apply; this selection is not a full-story completion claim.

The user's twelve September 26 decisions replace the earlier optional-cross-repository,
snapshot-first and Docker-first October suggestions. Deliver a separate host-side
batch generator over prepared directories, a real database-backed wiki, natural
multi-round tool-using Chat in the existing Web application, and a real document
graph. New management/CRUD screens and a complete ordinary Web Scan workflow are
outside this release.

This proposal maps those decisions to existing story obligations and suggests ten
coherent work packages. Package labels are discussion references, not new story IDs
or accepted implementation plans. The PRD, architecture, canonical epics and tracker
have not been rewritten or activated. Formal story definitions follow selection
approval and the installed planning gates.

The user's subsequent direction adds two constraints: isolate October-specific
behavior from normal Git-bound and legacy pair-bound paths, and maximize development
parallelism after a small contract agreement. This revision adds that isolation and
a contract-first delivery map; it does not dispatch implementation.

Subsequent user decisions select mobile-banking registration/login as the presentation
case, with two operator-prepared Repositories from a 16-Repository source estate.
Aim for a broad wiki over the admitted source in both selected Repositories, not
only handpicked presentation entry points. This is not a commitment to scan all 16
Repositories or a return to hardcoded pair eligibility. Repository identities/paths
will arrive from the user's separate source-preparation session. Two small source
samples may support early tests; they do not replace qualification on the real inputs.

In this proposal, **offline batch** means a separate host-side generation program,
outside the main Web application. It does not mean disconnected LLM execution. The
presentation need not explain process packaging: show the genuinely generated wiki,
Chat and graph. Do not describe unimplemented Web Scan initiation as demonstrated,
fabricate its progress, or misstate how results were generated if asked. Organizer
submission/disclosure requirements, if any, still need confirmation.

Story 13.1 remains completed history. Its capture/snapshot/Git workflow is not an
October prerequisite. Story 13.2 is backlog in the current tracker; the user reports
its unfinished work is backed up elsewhere. Do not restore or resume that work as
part of this selection. Current checkout: `G:\source\repos\vulgata`, branch `dev`.

Current inputs are the user's twelve decisions, `docs/bmad/epics.md`, the canonical
PRD, architecture gateway/AD-1–AD-32 spine, glossary, both UX documents, root AGENTS,
and current source. Historical demo/specification evidence is reuse evidence only.
The long-term generic design and historical evidence remain intact.

## Twelve decisions translated into release requirements

| User item | October requirement |
| --- | --- |
| 1 | Read operator-prepared repository directories. No clone, fetch, checkout, Git inspection, managed Source Snapshot capture or retention workflow. |
| 2 | Keep native CodeGraph MCP preparation and JDT-LS only. JDT-LS uses the whole prepared repository with automatic Maven/Gradle imports, builds and dependency restoration disabled. |
| 3 | Give Workers read-only directory listing, file reading and text search across authorized prepared roots. These remain available when LSP is limited or unavailable. |
| 4 | No source monitoring, incremental analysis, re-scan or change-propagation feature. Future update capability may appear on roadmap slides, not as delivered functionality. |
| 5 | Use a simple bounded Worker pool with an easily edited concurrency value in one place. No distributed scheduler or multi-run fairness/admission platform. |
| 6 | Keep real cross-repository resolution for the prepared batch. Resolve after Pass 1 outcomes settle; omit late-arrival and re-scan reconciliation. |
| 7 | Generate and publish a wiki dataset in a batch. No wiki refresh, replacement, retirement or update UI. |
| 8 | Chat may perform multiple rounds of wiki search/read/navigation tool calls before and while composing a useful answer. |
| 9 | Use natural model-generated conversation, not selection from allowlisted candidate sentences. Reuse August Business Chat ideas without its weakened access or fixture shortcuts. |
| 10 | A model tool reports used documents; Web displays a deduplicated end-of-answer document list. No sentence-level source proof or citation requirement. Existing evidence retrieval may be a secondary input. |
| 11 | Run generator and Web directly on the host. Docker application/tool packaging and qualification are excluded. Existing PostgreSQL deployment need not be moved. |
| 12 | Deliver a real navigable document graph suitable for screenshots, sourced from accepted wiki data rather than fabricated nodes/edges. |

The latest decisions additionally retain database-backed Global/System/Repository
context, prioritize JDT-LS risk reduction, use context-assisted LLM interpretation
and resolution for the selected inputs, and start Pass 2 without source/navigation
tools. The sections below define these scoped changes and their limits.

## Simplified execution contract

1. Configure a batch, its logical Repositories, prepared roots, operator, provider, database and selected Global/System/Repository context.
2. Inventory inputs; bind supplied context and the simple analysis policy; prepare CodeGraph MCP and the declared JDT-LS capability.
3. Analyze admitted files with bounded concurrency; accept valid CL and communication observations.
4. On natural finish or operator stop, settle active writes and fix the accepted corpus; mark unprocessed work as a visible coverage gap.
5. Use normalized identifiers and framework context to retrieve candidates, then bounded LLM matching over caller/provider source evidence. Knowledge accepts eligible source-supported relationship proposals; otherwise retain an Uncertainty with the available-evidence limitation.
6. Accept source-supported local references as well as cross-repository links; plan distinct source-backed local or cross-repository flow roots. Initially synthesize BL from accepted Documents, relationships, uncertainties and supplied context without source/navigation tools; validate and publish the available wiki.
7. Use the selected published batch from Web Chat, its document reader and graph.

Operator stop is sufficient to enter finalization; the release adds no threshold
requiring every file or the presentation's preferred flow to succeed. Finalization
uses committed accepted results, fences or settles in-flight writes and records
unprocessed/failed files as gaps. It cannot fabricate knowledge for unfinished work.
The bounded resolution and BL stages are part of finishing this batch, not a second
approval or content-quality gate. There are no refresh generations, coalescing or
concurrent re-scan semantics. Static cycles, ambiguous targets, multiple valid
targets and unknown external routing still need finite, truthful outcomes.
Stopping before any Business Logic has been accepted may leave a sparse or empty
business wiki. Finalization cannot turn unfinished analysis into Documents; it may
still need to run resolution and BL synthesis over the accepted corpus before Web
can display those results. Report what is available without adding a publication
threshold or presenting absent knowledge as complete.

Use one Uncertainty outcome for an unresolved provider, with a short reason and a
reference to this batch's coverage; no extra absence-status taxonomy or stop gate
is required. The operational outcome is the same whether the provider is outside
the selected Repositories, in unprocessed source, ambiguous or not identified.
Describe it as unresolved in the available evidence, not proof that no provider
exists. A supported positive relationship can still be accepted from a partial
corpus without claiming universal uniqueness or completeness.

## Preserved safeguards and deliberately smaller contracts

### Prepared input is not a Git snapshot

Use an honest prepared-dataset/source-origin identity: batch/dataset ID, Repository
ID, operator label and repository-relative path/range. A supplied label is not a
verified Git revision. Introduce an October prepared-origin contract rather than
relaxing the normal Git-bound contract. Never manufacture a commit string to pass a
validator. Preserve existing Git-origin history and its validators unchanged.

The operator keeps prepared directories unchanged and available while generating
and presenting this dataset. There is no new directory-copying, snapshot service,
repository-wide hashing or artifact-integrity ceremony in this scope. Without
captured/verified bytes, do not promise exact historical source reopening or automatic
change detection. Stored Document identities survive independently; absent or known
changed inputs invalidate source-inspection claims. Chat source inspection/viewer is
not required for the October story selection.

### Whole-root tools remain contained

Directory lists must be complete through pagination; file reads may use ranges and
continuations; text search returns locations and explicit limit/continuation state.
Bounded responses must not silently exclude source coverage. Ordinary Worker tools
grant no shell/command execution, writes, arbitrary host paths or path/symlink escape.
Tool Runtime binds CodeGraph/JDT-LS to configured roots outside model arguments.
Use one JDT-LS session
per selected Repository where applicable and serialize operations when concurrent
LSP access is unsafe. List/read/grep is an LSP fallback, not permission to omit the
retained CodeGraph MCP requirement.

Put JDT-LS first among tool-risk checks: exercise representative navigation in the
declared no-import/no-build/no-restore profile, not merely process initialization.
CodeGraph preparation remains a CLI invocation followed by native MCP connection
and Worker use. Keep its verification small: actual discovery/invocation and correct
root binding, not a new research spike or protocol framework. Availability of MCP
on both products is useful integration evidence, not proof of this composition.

#### Approved trusted CodeGraph runtime clarification — September 30, 2026

The October release may use the pinned trusted CodeGraph adapter delivered by
Story 18.3, including through its downstream October consumers, under the same
declared runtime profile and restrictions. This carries the existing authorized
exception with that adapter; it is not a blanket O-NFR01 waiver.

Under this exception, the trusted Infrastructure-launched CodeGraph 1.5.0 child may
run directly in an explicitly dedicated Vulgata-managed prepared workspace selected by the
application. It retains the launching account's permissions; preventive operating
system filesystem or network isolation is not claimed. Only declared, positively
bound CodeGraph tool state (normally `.codegraph`) is intended to change. Prepared
source, project configuration and Git hooks must remain unchanged. Observed effects
outside that footprint stop native readiness; this is not proof that every effect
is detected. Runtime flags and controlled smoke results do not prove OS isolation.

Application-selected exact roots, live authorization, ordinary Worker path/write/
command denials, bounded work, cancellation and owned-process cleanup remain
obligations. Existing workspace/ownership capability and integrated qualification
dependencies remain. This clarification grants no other executable or workspace,
broader Worker tool, credential/provider/database access, or normal retained-source
production deployment. It does not extend to JDT-LS or change its restricted profile,
the generic PRD/AD-3 source guarantees, or the September 29 tool-state consumer
criteria. Story gates and separately authorized runtime resources still apply.

Authority: the user's Story 18.3 decision `18-3-o-nfr01-exception-01`, recorded at
`2026-09-30T02:35:41Z` in `evidence/direct-process-human-authority-01.json` under
`specs/18-3-use-native-codegraph-mcp-against-prepared-source` in **Worker 18.3**
(chat `01a0eae9-657d-7f90-80e0-6bf7d83a5c7c`), followed by the user's September 30
instruction in **Vulgata Master** (chat `019fcbe8-f5db-73b3-914c-b29f75688a22`) to
implement this downstream October applicability clarification. The original
story-local decision and its evidence remain unchanged; this paragraph does not
claim that branch's implementation or qualification has been integrated.

### Context-assisted coverage and cross-repository interpretation

Add selected **Global, System and Repository user context** to October scope. Reuse
compatible existing context records/setup rules in the owned test database; do not
build a new context-management UI. Bind the actual context values and their origin
to the batch so another edit cannot silently change analysis halfway through it.
Workers receive relevant context about vocabulary, framework conventions, custom
file types and how communication declarations are expressed in source. Do not require an owner-maintained universal
endpoint catalog or a new generic DSL parsing framework.
Keep Global/System/Repository origin labels when combining context; do not silently
overwrite contradictory statements. The LLM may interpret them, but contradictions
with other context or source must remain visible rather than become verified facts.

Inventory the prepared roots deterministically. Let the LLM use source and context
to interpret unfamiliar formats and propose file treatment/ordering. **Scan Control
owns the final admission decision**, applying the explicit application-owned batch
policy automatically; the Worker is a proposer and Knowledge owns result acceptance,
not file eligibility. Record simple admitted/supporting/excluded outcomes, the
policy identity and reasons/evidence for exclusions or primary-to-supporting demotion.
No per-file human approval or general-purpose classifier framework is required.

Context is not a substitute for listing inputs. Unknown extensions, model confidence
or CodeGraph omission alone cannot reduce source coverage. If a proposed exclusion
has no policy-supported reason, retain the conservative admitted treatment and its
limitation. No semantic Code Unit preallocation moves into PreScan. Failed/stopped
primary work remains a visible gap; stopping does not retroactively demote it to
supporting/excluded work. This retains flexible LLM interpretation without claiming
universal DSL coverage.

For cross-repository work, context describes **how to recognize and interpret a
mechanism**, not which Repository or service a call must target. For example, context
may explain where an RPC call expression carries its interface identifier and how
the framework declares a provider. The Worker must locate the actual call and
provider declarations in source and extract the relevant identifiers/namespaces.
Those source facts must support the match; a supplied A-to-B mapping, a lone visible
candidate or model confidence is not a substitute.

Keep recognition context, observed source facts, candidate hypotheses and accepted
source-supported relationships distinct. Missing/ambiguous/conflicting evidence
stays Uncertainty. Carry that status through BL, Chat and graph; neither synthesis
nor a fluent answer may turn a context-led hypothesis into an established link.
Source-backed static linkage still does not prove deployed execution. The selected
case may bound interpretation/matching work, but there is no October exception
allowing context-only counterpart bindings to become accepted relationships.

This corrects the earlier proposal's overinterpretation of user context. It restores
the current FR-5.1/5.2/5.8/6.2 and AD-9/22 evidence rules; it does not restore the
fixed August profile, mandatory manual topology or a new exhaustive generic resolver.

**LLM-assisted matching is the proposed October execution route.** Candidate
retrieval uses selected normalized identifiers together with source/context signals;
exact normalized equality is not the sole filter or an obligatory final decision
rule. Preserve original identifiers so aliases and missing optional fields can be
interpreted rather than erased by normalization. Retrieve a bounded candidate set
from the fixed accepted corpus; do not run an unconstrained all-pairs LLM comparison.
Retrieval limits remain visible when they prevent a conclusion.

The matching Worker receives the caller's surrounding source/evidence, candidate
provider declarations and available implementation bindings, accepted CL versions,
normalized/original identifiers and relevant advisory framework context. It proposes
the supported counterpart(s), supporting/opposing source references, a concise
decision basis and remaining uncertainty. Context can explain naming conventions;
it cannot supply a missing provider declaration or an authoritative A-to-B mapping.
Absent optional fields do not automatically defeat a match; absent evidence material
to that mechanism, unresolved contradictions or merely high confidence do not
establish one. A declaration-level relationship must not imply a known implementation.

Knowledge/application code validates the proposal's scope, candidate identity,
versions, source references, applicable explicit constraints and conflicting/duplicate
writes before applying it. It does not rerun semantic matching as a universal
hardcoded equality engine, and structural validation is not independent proof that
the model's interpretation is correct. Record the selected inputs and matching
policy/prompt identity with the decision. No extra semantic-auditor agent, per-link
human approval or publication-quality gate is added.

A4's accepted feature plan must make the small positive-evidence checklist concrete
for the initial mechanisms once prepared samples arrive. Qualify supported aliases,
material namespace/operation conflicts, harmless missing optional fields and missing
provider evidence. These examples test LLM-assisted decisions; they do not encode
fixed Repository pairs or make partial coverage a failure of the whole batch.

Backup B's successful sample behavior is a reuse lead for the relevant patterns,
not proof that its fixed profile covers these newly prepared inputs. Qualify the
chosen registration/login cross-repository link and at least one unresolved or
conflicting example without building an exhaustive generic matching test matrix.

### Pass 2 starts from Documents; source tools are conditional

Initially A5 uses accepted CL, relationship evidence, uncertainties and the selected
context only. No source, CodeGraph or LSP tools are required in the initial Pass 2
implementation. Assess business explanation quality before adding those tools.

Preserve material evidence qualifications through **CL -> BL -> Chat**. Accepted
Documents may contain source-supported explanations, explicitly attributed supplied
context and hypotheses/Uncertainties; acceptance does not make all statements equally
source-proven. Retain the relevant qualification and context/source references at
section or statement-group level, including when context reaches BL indirectly
through CL. Split or qualify materially mixed content rather than silently upgrading
an advisory business assertion. Knowledge validates the required structure/references;
there is no new sentence-level entailment or semantic-verification engine.

Terminology assistance need not qualify every sentence, and synthesis supported by
several accepted source-backed facts is legitimate. Qualify material unsupported
assertions: for example, a context-supplied lock duration cannot become a source-proven
duration when the selected code only establishes that a lock operation occurs.

If the concrete evaluation identifies important details absent from CL but available
in prepared source, first add the existing read/list/search capability to Pass 2.
The isolated batch may use a static operator-authorized grant over both prepared
Repositories; do not build fine-grained per-flow/user source permission machinery
for it. Access remains read-only and contained to the configured roots, not arbitrary
host files or credentials. Additional navigation tools need an observed need.

Record extra source/CL actually used with the resulting BL dependencies. Tools do
not grant permission to rewrite CL, invent authoritative source-derived links or
start recursive scans. This batch simplification does not weaken main Web's live
Knowledge authorization or Business Mode's BL-only boundary. Any conditional tool
addition still goes through the owning story's planned-change authorization gate.

### Simple concurrency, useful diagnosis

Use one small Worker pool with one easily edited default, not settings UI or a new
scheduling framework. Exact initial value is an implementation choice; four is a
reasonable starting experiment, not an asserted capacity guarantee. A file has one
active writer; provider calls and retries remain bounded. Capture enough timing to
distinguish model wait, tool wait and persistence. More Workers cannot fix a serialized
tool, provider throttling or a stalled operation. Bounded same-input retries remain;
durable pause/resume and checkpoint-based restart are secondary October targets,
not delivery prerequisites. The user permits skipping them during development if
their implementation or qualification takes too much time; record the omission and
its operating consequence rather than claiming recovery support.

The minimal fallback is to report failure/interruption, settle or terminate owned
work and start an explicitly requested fresh batch with a new identity and owned
database. This accepts repeated generation cost. Preserve previous accepted data;
do not silently resume, merge batches, select another dataset or force completion.
An explicit graceful operator stop still finalizes its accepted partial corpus;
a crash or exhausted provider retry does not itself mean successful finalization.
Already committed publication remains valid. Atomic acceptance and safe write
termination stay required; a durable recovery framework does not.

Do not add a distributed/cross-process provider admission system for this release.
Retain the simple local Worker cap, bounded retries and explicit failure/interruption
(or pause when implemented). The
user expects development traffic to remain within provider limits; treat that as
an operating assumption, not a guarantee from the provider. Handle observed
throttling/timeouts without unbounded retries; adjust the cap if measurements require
it. Controlled development runs can avoid overlapping a full batch with Chat tests.

Preserve sanitized append-only `log.md` operation/target/timestamp output alongside
diagnostics (FR-4.8). Keep production prompts externalized with supported overrides
(NFR-6.1); this does not introduce Prompt Workbench. The existing A1/A3/A4/A5/B1
owners carry these requirements, not a separate platform workstream.

### Explicit local LLM configuration; reading instructions pending

Local generation/Chat executions requiring an LLM will obtain endpoint, model and
key from the user's forthcoming `.env` instructions, or use an explicitly implemented
database-configuration route. The user has not supplied the file-reading instructions:
do not infer a path, discover/open `.env` files, print their contents or borrow old
demo credentials. This pending input does not block requirements/story drafting.

Choose one explicit configuration source/precedence for each process. Resolve each
selected provider profile's endpoint/model/key consistently; do not silently mix
environment and database fields or fall back to a different provider when incomplete.
If importing the supplied configuration into the owned batch database is implemented,
use the application's valid provider setup and encryption rules. Never commit/log
plaintext credentials; record only sanitized configuration presence/identity.
This exception changes October configuration loading, not all of FR-3. Keep the
selected API type and Worker/Chat role-profile bindings explicit; a shared profile
may be selected deliberately, not inferred by collapsing roles. Normal provider
registry/administration behavior remains unchanged; no new configuration UI is required.

Copying encrypted provider rows is optional, not a prerequisite of database-per-batch.
If that route is used, the process decrypting them needs compatible Data Protection
keys, application-isolation identity and protector purpose. A cloned database alone
does not establish those conditions. Verify compatibility without printing secrets;
the direct environment-configuration route avoids this encrypted-row-copy dependency.
Current code anchors are `Vulgata.Web/Program.cs:196` and
`Vulgata.Web/Data/SecretEncryptionService.cs:17`. See Microsoft's
[Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)
and [purpose isolation](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/purpose-strings?view=aspnetcore-10.0).

### Isolate October behavior; share its integrity implementation

Recommended design: an additive October module with its own Knowledge persistence
context/schema, prepared by the independent October batch and available to ordinary Web
composition. Keep it within the existing project/process boundaries unless an actual
dependency requires a separate assembly. Dedicated namespaces/folders and a single
composition boundary are sufficient; do not scatter deadline conditionals through
normal validators, entities, tools or retrieval.

**Selected storage layout: one owned database per batch on the existing PostgreSQL
instance, with identical fixed schema names and model in every batch database.**
This user-approved choice supersedes the earlier schema-per-batch option. Prepare
each database with its approved setup/context data while preserving real identity
references and October publication rules. The generator selects the exact database
and batch ID. Under the user's 2026-10-02 clarification, Web starts ordinarily with
only its database configuration changed; October mode, batch ID, setup-operator
credentials and generator provider binding are not Web startup prerequisites.
Later Chat, reader and graph consume that configured database through their owning
shared contracts; this clarification does not implement their read selection.

Bind the generator's exact batch and each host's database at process startup, not
to whichever batch was created most recently. Ordinary Web does not perform a batch
selection operation at startup.
Switch batches by stopping/restarting the relevant host with the new configuration;
do not switch an active turn's data source. Check that System/Repository/context
identifiers belong to the authorized prepared setup. A missing or mismatched
selection is an error, not permission to display an earlier run's wiki.

**Why changing the database is different from changing the schema search path.**
Changing a connection string's `Database` selects a different PostgreSQL database;
changing its Npgsql `Search Path` selects the lookup path for unqualified names,
not a replacement for schema-qualified EF mappings. The current normal
`VulgataDbContext` explicitly sets `HasDefaultSchema("vulgata")`; some mappings and
migration history also specify schemas. `Search Path` alone cannot redirect that
existing model into a cloned schema, and normal mappings must not be weakened.

The generator receives the selected database connection plus exact batch ID at
startup. Web uses only its ordinary database configuration at startup. No live
per-user database switcher, dynamic schema mapping or schema-varying
EF model cache is required for this release. Pin data-source/factory configuration
to that selection and keep the mapping fixed. Database creation/prepared-data setup
still requires an owned authorized target during implementation; this planning
approval does not perform or authorize arbitrary database administration.
The first persistence integration check uses two distinct batches, verifies the
selected batch's data, and proves no cross-batch read/write or missing-target fallback.

Technical references: [Npgsql connection parameters](https://www.npgsql.org/doc/connection-string-parameters.html),
[PostgreSQL 17 schemas/search path](https://www.postgresql.org/docs/17/ddl-schemas.html),
[EF model cache](https://learn.microsoft.com/en-us/ef/core/modeling/dynamic-model),
[EF database selection and Blazor factory lifetime](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy).

The shared wiki contract is shared between these October consumers. It is not a
claim that existing Git/pair-bound tables already support prepared datasets. The
earlier phrase "same schema/migrations as Web" means Batch and October Web must
consume the same valid stored dataset, not that October must write legacy tables.
Both hosts use one October acceptance/publication implementation. Scan, Chat and
graph must not each build their own persistence rules or import/copy pipeline.

Validate structured CL/BL, identities, prepared source origin, sections, anchors,
relationship endpoints and BL dependencies; publish coherent records atomically.
Retain one CL Logical Document per meaningful discovered Code Unit, with
application-owned identity; a file may yield several units. Processing windows,
tool symbols and whole-file jobs are not alternative Document identities. A
completed Accepted File Manifest is the visibility boundary: checkpointed drafts
from an unfinished file are not accepted Knowledge, including after operator stop.
Preserve immutable accepted versions and operation replay safety without building
replacement/current-selection competition between several datasets. Reuse compatible
content materialization, rendering, authorization and validation helpers; do not
reuse a legacy orchestration contract by disabling its preconditions.

| Boundary | October composition | Existing normal/default composition |
| --- | --- | --- |
| Source access | Explicit prepared roots and honest batch/source identities | Retained Git-bound contracts and Story 13.1 behavior remain unchanged |
| Knowledge | Dedicated context and fixed schema in each owned batch database, with one writer and published-wiki read interfaces | Normal Knowledge model, validators and migration snapshot remain unchanged |
| Eligibility | Explicitly selected published batch, with authorized contributors | Existing pair/currentness rules are not relaxed or silently bypassed |
| Chat and graph | New consumers of the October wiki contract | Existing routes/registrations retain their behavior when October is disabled |
| Legacy evidence | Optional read-only secondary adapter with original identities and provenance | No copying or relabelling legacy rows as October output |

Enable independent batch setup deliberately; no fallback into it when ordinary Web
fails. Ordinary Web remains its normal composition with only database configuration
changed. Later October consumer integration is owned by its respective stories and
does not reinstate an October-specific Web startup prerequisite. Keep October's
migration folder, model snapshot and migration history separate.
October migrations must not run during ordinary Web startup. The normal
`VulgataDbContext` scans its assembly for entity configurations, so merely adding a
new context is not enough: October mappings must not be accidentally discovered by
that scan. Use explicitly scoped configuration and verify the normal model/composition
is unchanged. This is isolation from existing behavior, not evidence that the
unfinished normal workflow is fully operational.

An isolated October database per batch on the existing PostgreSQL instance is the
selected target layout. Do not touch an unrelated database. Setup may be programmatic through
shared rules; no new CRUD screens are required. The operator identity and authorization
must be real, not a fabricated Administrator claim. Do not make the LLM issue SQL.
Do not force ScanRun completion or insert dummy repositories/FlowGroups to satisfy
legacy retrieval. New prepared-source publication and Web eligibility must agree.

### Chat is synthesis; the footer is navigation, not proof

Use a bounded real model/tool loop over authorized wiki search, content/section reads
and related documents. Business Mode remains BL-only. Existing evidence-selection
logic can retrieve an additional permitted excerpt, but cannot restrict the answer
to candidate sentences or become an automatic CL/source fallback for Business.

A `report_used_documents`-style tool accepts server-issued DocumentVersion handles
returned by the turn's permitted read tools. Derive titles/links server-side; reject
invented/unread/disallowed IDs and deduplicate. Accessed documents need not all be
claimed as used; reported references are not proof for every sentence. Preserve
authorization over all context contributors, not just displayed references. Empty
references for greetings/no-result answers are valid; a missing report is a truthful
reference limitation, not an excuse to fabricate references or retry generation.

Retrieved wiki/source/context remains evidence, not governing instructions. Its
content cannot change application-bound tool permissions, purpose or scope. Include
a bounded adversarial retrieved-document case in B1 qualification.

Retain genuine streaming, useful follow-up context, saved answer/status and explicit
failure/interruption. No complete-candidate JSON gate, sentence-level entailment
checker or hidden-reasoning display. A static wiki selection simplifies consistency;
it does not remove live user/mode authorization. Full same-turn disconnect/reattach
engineering is excluded from this October slice. Disclose interruption, preserve
useful saved content and require explicit retry instead of silently regenerating.

In-place mode/scope changes and their history/summary transition handling are also
secondary October targets and may be omitted if time is short. The recommended
low-cost fallback fixes mode and authorized Repository scope for each conversation;
changing either starts an empty conversation without importing the old transcript,
summary or model context. The UI may require the active answer to finish or stop
first, avoiding advanced overlapping-transition handling. Do not implement the
full transition machinery merely to satisfy the cross-check recommendation.
This feature cut does not relax Business BL-only reads or live all-contributor
authorization: fixed conversation scope is not permanently frozen permission.

Keep the existing Chat page layout, visual style and interaction structure. Update
only the rendering and state binding needed to show actual streamed text, turn status
and the permitted document footer in that UI. The current claim-by-claim reveal
markup cannot simply be reused as a live prose stream. Do not schedule a Chat visual
redesign for October.

**Explicit streaming and tool-activity behavior:**

- Preserve the existing page shell, typography, colors/themes, message styling,
  composer and scope-picker appearance. Necessary changes are within the assistant
  turn: compact tool activity, growing answer prose and document footer ordering.
  The picker must bind to actual authorized October Repositories, not a demo catalog.
  If in-place changes are omitted, it selects the new conversation's fixed scope
  rather than silently rebinding an existing conversation.
- Start a compact progress placeholder when a turn begins. Show truthful sanitized
  tool activity such as searching/reading Documents, with running/completed/failed
  states from actual tool events. Tool call identity keeps overlapping/repeated
  activity consistent. Do not expose raw arguments/results, hidden reasoning or
  credentials, and do not invent progress or a percentage.
- As soon as the provider emits user-facing answer text, relay the deltas through
  the October agent/coordinator interface to the UI. Do not wait for provider
  completion, complete JSON validation, conversation save or the document-report
  tool before displaying authorized provisional answer text. Small rendering
  coalescing is acceptable; full-answer buffering and artificial typewriter delays
  are not. Real provider SSE alone does not establish this end-to-end behavior.
- Tool progress and answer text may interleave across rounds. Do not require all
  tool activity to finish before showing available answer text. Use a compact
  progress row above/within the existing assistant bubble, not a new diagnostics UI.
- Keep the document list after the answer. Derive and deduplicate its entries from
  the document-report contract; use stable reported order unless feature design
  chooses another explicit ordering. Reordering references must not renumber or
  rearrange already streamed prose. Footer readiness must not delay answer text.
- Receiving text is not completion. Save the content/reference/status outcome at
  normal terminal completion; preserve useful partial text with an interrupted or
  failed status when needed. Never replay buffered text as though it were a live
  generation. Full same-turn reconnect remains excluded.
  Completed status requires valid provider completion without truncation or
  unfinished tools and one idempotent durable save. Recover a lost save
  acknowledgement by checking/replaying the same save, not generating another answer.
  Do not admit an overlapping retry while the originating answer is unresolved;
  interrupted output is not ordinary completed-answer inference history.

The shared October Chat interface therefore needs turn/tool progress, answer-delta,
document-reference and terminal-outcome events. Existing `AskAsync` returning only
the completed answer plus `RevealAnswerAsync` cannot satisfy this contract. The
first end-to-end streaming check must observe visible answer text while generation
is still unfinished, tool progress across rounds and interruption without losing
already visible text. This is a behavior change through backend and UI, not CSS
or prompt changes alone.

### Graph uses accepted wiki relationships

Read the published October wiki through a scoped graph projection. Show real CL/BL
Documents and supported REFERENCES/GENERATED_FROM relationships with readable labels
and navigation to the selected Document. The user now requests the minimum usable
graph-page functions; retain current Vulgata styling and design their exact layout
during C1 feature design, before implementation:

- Show the exact selected batch/context and a readable bounded document neighborhood.
- Filter by permitted Repository and document kind; search document titles to focus
  a node. Business Mode never offers a hidden CL scope as an available filter.
- Pan/zoom and fit/reset the view; select a node to highlight its immediate neighbors
  and inspect its title, kind, Repository and available summary.
- Open the selected exact Document Version in the shared reader. Allow explicit
  bounded neighbor expansion; show a limit/load-more state rather than implying a
  truncated neighborhood is the full corpus.
- Provide a small legend for document types, relationship direction/meaning and
  visible uncertainty indicators. Graph arrows are not
  unqualified claims about deployed execution order.
- Include initial loading, empty/no-results, failure/retry and manual refresh states.
  An empty or sparse Business graph is valid; never add invented edges for density.

These functions define the minimum scope, not a pixel layout, new visual identity
or a graph-editing platform. Use existing Chinese Fluent/Library Oak styling and
Business-light/IT-dark themes. Exact default grouping/layout remains C1 design work.
The feature design must also choose a Knowledge-authorized reader/graph route for
permitted Business and IT users. The existing management graph placeholder uses a
management-only policy; merely filling that page would exclude ordinary readers.
Keep accepted relationships separate from hypotheses/Uncertainties in the projection;
do not draw a context-only candidate as a confirmed consumer/provider connection.
An authorized IT view can show the rich CL/BL graph; Business must not receive hidden
technical nodes/metadata. Do not invent BL-to-BL business edges merely because two BL
documents share CL. No new graph database, graph editing, live refresh engine, or
full version-history explorer. Attractive layout must not distort edge meaning.

## Proposed regrouping: three workstreams, ten packages

These outlines are historical course-correction selection packages, not formal
story IDs. The [first-deliverable plan, updated October 6](october-delivery-plan-2026-09-30.md)
now supplies engineering feature packaging and selected scope. Preserve prior
implementation/evidence and apply its explicit disposition of full 18.5 execution;
broader original story obligations remain traceable beyond this first deliverable.

| Package | User-visible or operator outcome | Existing obligations reused/reworked |
| --- | --- | --- |
| A1 Prepared inputs, context and host analysis tools | Bind prepared roots and Global/System/Repository context; Workers can list/read/grep and use native CodeGraph MCP/offline JDT-LS; investigate JDT-LS first. | 13.3–13.6, source-access mechanics only from 13.1, host configuration from 17.1; preserved FR-14 context |
| A2 Isolated October Knowledge acceptance and storage | Batch and October Web share prepared-origin acceptance, valid context binding, CL/BL/relationship storage and published-dataset eligibility without weakening normal contracts. | 13.9, 13.14 persistence portions, 14.1, read-contract portions of 13.10/15.1 |
| A3 Real file analysis with simple concurrency | Scan Control admits work under the explicit policy; Workers analyze files using source/context, preserving material context qualifications in semantic CL/observations and visible failure/continuation outcomes. | 13.7 execution subset, 13.11–13.12, 14.1 |
| A4 LLM-assisted static cross-repository resolution and flow planning | Accept source-supported local references; retrieve bounded cross-repository candidates and use LLM interpretation to propose links. Knowledge accepts eligible proposals; unresolved results retain scoped Uncertainty. Plan distinct local or cross-repository roots; shared helpers/cycles do not merge business purposes. | Reworked 13.13, 14.2–14.3 and bounded 14.4 work; preserve AD-9/11/20/22 evidence, flow identity and acceptance ownership |
| A5 Document-first BL synthesis and wiki publication | Initially synthesize from CL/relationships/context; preserve material qualifications and exact dependencies through business explanations and gaps; add source tools only for demonstrated quality need. | Reworked 13.14–13.15, 14.6, batch integration portion of 13.17 |
| B1 Wiki tool loop and natural conversation | Main Chat searches and reads multiple wiki documents across rounds and answers follow-ups naturally with genuine streaming. | 15.1–15.4, ordinary integration subset of 15.8 |
| B2 Document footer and minimal wiki reader | Users open the real documents reported as used by an answer; no per-sentence source proof or source viewer. | Reworked 15.7, reader portions of 13.10/13.14/16.1 |
| B3 Existing Chat UI integration | Keep current styling; show real tool-call progress and immediate answer deltas, with document footer below and truthful terminal/interrupted state; remove post-completion fake reveal from the October path. | 15.2–15.3 lifecycle portions, minimum 15.6 integration |
| C1 Minimum usable document graph | Design the UI, then let authorized readers filter/search/focus real documents, pan/zoom/reset, inspect/expand a bounded neighborhood and open exact Documents; preserve existing styling and truthful edge legends/states. | 16.2–16.3, document navigation from 16.1 |
| C2 Real batch-to-Web qualification | Host batch generates the chosen business wiki; ordinary Web consumes it for multi-round Chat, footer navigation and graph screenshots. | Selected integration checks from 13.17/14.7/15.8/17.1/17.4 |

### Contract-first, eager parallel development

The September 27 cross-check corrections are accepted planning inputs, except that
durable batch pause/restart recovery and in-place Chat mode/scope/history transitions
are secondary targets. Their owners may skip costly implementation or qualification
to protect delivery, record that decision and use the bounded fallbacks above; they
are not release blockers or new mandatory gates. This is a specific October scope
exception, not a blanket permission to ignore unrelated failures or weaken normal
product requirements. The other accepted integrity/security corrections remain in
scope. Formal story approval gates remain unchanged.

Extract a short **F0 contract agreement** from A1/A2/B1/B2/C1. F0 is a coordination
milestone, not an additional product feature, a complete persistence implementation
or a new generic framework. Agree only the interfaces needed for parallel delivery:

- Prepared-source identity, root registration, relative locations, list/read/search
  results and continuation/failure semantics; tool session binding and readiness;
  batch-bound Global/System/Repository context values and provenance; Scan Control's
  final admission policy/dispositions, including exclusion/demotion reasons.
- Analysis proposals and accepted-write results: per-Code-Unit CL identities,
  accepted-file visibility distinct from drafts/windows, distinct local or
  cross-repository flow roots and BL identities,
  observations, source-supported relationships, separate hypotheses/uncertainties and
  flow dependencies; material section/statement-group evidence qualifications that
  survive CL/BL/Chat reuse. Resolution carries its selected input versions and scoped
  limitations without a new absence-status taxonomy. Document-first Pass 2 does not
  require navigation-tool integration.
- Wiki acceptance/publication/replay semantics and structured content; explicit
  dataset publication state, exact DocumentVersion handles and read eligibility.
- Authorized search/read/related-document and graph projections, Business filtering,
  reported-document handles and shared reader destinations.
- Chat stream/tool-progress/completion events needed by its UI; errors and saved
  terminal outcomes, with answer deltas delivered before generation completes.
  Define valid completion, idempotent save and non-overlapping retry; use fixed
  conversation mode/scope if transition support is omitted.
  Document-footer updates never gate visible prose. Do not expose provider-specific
  transport to graph or reader.
- October composition/storage ownership and representative valid/invalid examples.

The source/tool contract can be agreed and released first, while the wiki contract
is being settled. Tools do not depend on a wiki database. Once the wiki contract is
agreed, all its consumers may start against labelled test adapters; they do not wait
for A2's database implementation or for a complete real scan.

The user's source-preparation session is separate from these implementation lanes.
Prepared source and context may be delivered later; begin with the agreed small test
samples where needed. Do not infer authority to create or message that session.

```text
Source/tool contract ──┬─ List/read/search ──────────┐
                      ├─ Native CodeGraph MCP ─────┤
                      └─ Offline JDT-LS ───────────┤
                                                 │
Wiki + UI contracts ──┬─ Knowledge store ───────────┤
                      ├─ Batch analysis/resolve/BL ┼─ Early integrated slices
                      ├─ Chat engine + UI ─────────┤           │
                      └─ Shared reader + graph ────┘           v
                                                 Real published-batch qualification
```

Batch analysis also consumes the source/tool contract. This diagram describes
development start dependencies, not runtime ordering or a promise of seven available
developers. More sessions help only while their edits and resources remain isolated.

| Parallel lane | Exclusive implementation responsibility | Does not wait for |
| --- | --- | --- |
| Source tools, A1 slice | Contained roots and complete paginated list/read/search | Knowledge DB, CodeGraph, JDT-LS or scanning orchestration |
| CodeGraph, A1 slice | Preparation and native MCP discovery/invocation for bound roots | DB, JDT-LS or completed scanner |
| JDT-LS, A1 slice | Offline sessions, navigation normalization and truthful limitations | DB, CodeGraph or completed scanner |
| Knowledge, A2 | October acceptance, transactions, schema/migrations and read projections | Real model analysis or finished UI |
| Scanner, A3–A5 | File work, bounded pool, observations, static resolution, flows and BL proposals | Finished tool/store adapters to begin implementation |
| Chat, B1/B3 and B2 footer | Real tool loop, dialogue, streaming and footer consumer | Completed scanner, real dataset or graph UI to begin implementation |
| Reader/graph, B2 reader and C1 | One shared document reader, graph consumer and visual navigation | Completed Chat engine or scanner to begin implementation |

If more developers are available, A3 analysis, A4 resolution/flow planning and A5
BL synthesis can be separate owners once their proposal/result contracts are agreed.
Likewise Chat engine and UI can split against the event contract. Their acceptance
still depends on the integrated upstream outputs; parallel coding does not remove
those dependencies. Do not split shared files between concurrent writers merely to
increase the number of sessions.

One contract/integration owner controls shared interfaces, package/project changes,
DI, tool registration and Web route composition. The Knowledge lane is the sole
author of October mappings/migrations/snapshot; integration coordinates adoption of
that schema rather than independently editing it. Normal model/snapshot changes are
outside this release proposal. The reader owner supplies one navigation destination
to both Chat and graph. Cross-lane contract changes are explicitly coordinated, not
silently fixed in each consumer.

Use separately assigned checkouts/branches and owned runtime resources when actual
parallel delivery is authorized. Each runtime-testing lane needs its own database
or agreed exclusive access window; no concurrent migrations or destructive reset of
a shared demonstration database. Assign ports/processes and tool workspaces as well.
Planning this arrangement does not create worktrees or authorize external resources.

### Integrate continuously, not after every lane is complete

1. Feed a clearly labelled representative fixture through real October persistence
   into the reader, graph and Chat read tools. This proves contract/wiring behavior
   only, not generated knowledge or real answer quality.
2. Integrate one genuinely analyzed file through publication and reads as soon as
   the required tools, provider and store are available.
3. Run a small real cross-repository case through resolution and BL synthesis, then
   exercise the main Web Chat and graph against the same published dataset.
4. Generate and assess the chosen published business dataset with its actual coverage; then prepare its
   screenshots/recording. Do not use synthetic test content as generation evidence.

The runtime sequence remains accepted Pass 1 corpus -> cross-repository resolution ->
flow/BL publication. Operator stop can fix a partial accepted corpus for those later
stages. A4/A5 may be developed concurrently with A3, but they cannot claim real
runtime acceptance before its inputs exist. B3 integrates B1/B2; C2 verifies the
complete real chain. All formal planning, tool/Scan/PRD authorization and implementation
gates still apply. This dependency map is not authorization to dispatch stories now.

### Content evaluation and presentation responsibilities

The user accepted concrete content evaluation. Establish a compact expected-fact
set for the selected registration/login process, covering main rules, important
branches/effects, cross-repository responsibilities and a genuine unknown. Assess
generated CL/BL separately from Chat: a faithful answer can still repeat a faulty wiki.
Use roughly 10–15 representative questions as an initial working size, including a
meaningful follow-up sequence and some paraphrased/held-out wording. This is a small
source-checked evaluation, not a new automated semantic-verification platform.

Retain useful wider knowledge from both prepared Repositories; the evaluated
presentation flow does not redefine all other source as out of scope. Report actual
admitted/accepted/failed coverage and do not call a partial batch complete coverage.
Human context may explain the framework and vocabulary; distinguish it from facts
independently checked against code. Record actual timings and available usage,
without inventing comparative accuracy, savings or a universal performance claim.

The intended distinction is a centralized, reusable cross-repository **business
wiki**, consumed by Chat, reader and graph, rather than repeated ad hoc code-agent
exploration. Demonstrate this property directly; do not require a broad competitor
benchmark or assert that no coding agent can ever generate documentation.

Evaluate search quality once a representative real wiki is available. The graph's
minimum functions are listed above; decide its detailed UI in C1 design and refine
presentation choices after the feature exists. The user and teammates own narrative.
Earlier suggested calendar milestones remain planning advice, not accepted delivery
commitments. Monitor actual progress; formal story gates still apply.

## Original story disposition

- Preserve 13.1 as done; exclude its Git/capture/retention execution from October.
- Keep full 13.2 backlog; do not retrieve its external backup automatically.
- Split/rework the selected outcomes of 13.3–13.7, 13.9–13.15 and 13.17 as above.
- Defer 13.8's separate advisory overview feature; ordinary supplied context and
  simple deterministic file ordering may support analysis without that feature.
- Exclude 13.16 replacement, 13.18 dashboard and 13.19 multi-run fairness/control.
- Keep static 14.1–14.3/14.6; retain only bounded ambiguity handling needed for the
  chosen business, with context-guided interpretation and source-evidence matching. Exclude
  dynamic 14.5 and full ordinary beyond-pair 14.7 qualification.
- Select/rework 15.1–15.4/15.6–15.8. Exclude full 15.5 reconnect machinery from
  October under the explicit interruption contract above. In-place mode/scope/history
  transitions are secondary and may be omitted using the fixed-conversation fallback.
- Split 16.1 to minimum document reading/navigation; include 16.2–16.3 graph outcomes.
- Take only necessary host runbook/diagnostic/integration checks from 17.1/17.4.
  Exclude full backup/restore product work in 17.2–17.3; still protect the actual demo
  data and credentials through normal owned-resource handling.
- No extraction of an outcome marks an original story complete. Long-term obligations
  remain visible and historical acceptance/evidence is not rewritten.

## Exact authority deltas to formalize after selection approval

| Authority / old requirement | October replacement | Preserved rule |
| --- | --- | --- |
| FR-4.1/4.9, AD-3/4: committed Git and retained Source Snapshots | Isolated prepared-directory composition with explicit non-Git dataset provenance | Normal Git contracts unchanged; contained authorized reads; no false revision/coverage claims |
| FR-4.2, AD-1/14/23: ordinary independent initiation and fair durable scheduling | Explicit host batch and small bounded pool; operator stop fixes accepted corpus for finalization | Real outcomes, single file writer, safe acceptance and visible coverage gaps |
| FR-4.1/5.8/14.1–14.4, AD-4/5/22: generic preparation and advisory context | LLM-assisted source/file proposals with batch-bound context; Scan Control applies the final declared admission policy; no new universal DSL classifier | Complete inventory, justified exclusions/demotion, explicit coverage outcomes and material context provenance |
| FR-5.1/5.2/5.8/6.2, AD-9/20/22: generic source-evidence resolution | Bounded source-grounded LLM matching over candidates/identifiers/calling context; application acceptance checks; one scoped Uncertainty outcome when unresolved | No context-only mappings or confidence-as-proof; hypotheses stay distinct; no deployed-execution or global-absence claim |
| FR-7.3, AD-12/24: advisory context versus accepted behavior | Preserve material context qualifications at section/statement-group level through CL, BL and Chat | Accepted storage is not equal evidential authority; no sentence-level semantic-verification engine |
| FR-4.5, AD-12/21: Pass 2 verification tools | Start document/context-only; add scoped prepared-source reads only when evaluation shows a quality need | Exact accepted-document dependencies; additional source provenance if tools are added; no arbitrary host access |
| FR-4.10, FR-6.2/6.6, AD-10/18/19: later-evidence and replacement selection | One static batch, one resolved corpus and wiki dataset | Immutable accepted content, supported links, uncertainties and finite cycles |
| FR-11.7, AD-27/29 and UX inline claim references | End-of-answer reported document list plus minimal reader | Real generated answers, authorized actual DocumentVersion destinations |
| FR-11.3/11.7, AD-26–28 and UX reconnect/transitions | No full reattachment; in-place mode/scope/history transitions are secondary and may be omitted with fixed conversation mode/scope | BL-only Business tools/context, live contributor authorization, valid completion, idempotent save and non-overlapping explicit retry |
| NFR-3.1–3.3, AD-15: durable batch pause/restart recovery | Secondary target; if costly, visible abort/interruption and an explicitly requested fresh batch replace checkpoint-based restart | Atomic accepted-file/publication boundaries, safe owned-work termination, no false success or silent dataset substitution; graceful operator-stop finalization retained |
| NFR-4.1, AD-15/30–32, Story 13.2 | Native host generator/Web with existing database deployment | Protected credentials and data, explicit resource ownership, truthful supported environment |
| AD-23 and NFR-3.2: shared provider admission | Simple local cap and controlled execution; no new cross-process provider coordination | Bounded retries, visible throttling/failure/interruption, no automatic provider switching; durable pause/restart is secondary as above |
| FR-3 configuration-loading boundary and NFR-2.2/2.3 secrets | User-instructed environment or explicitly implemented DB loading with declared precedence per process; not wholesale replacement of provider management | Explicit API type and role/profile bindings; normal registry/admin unchanged; no secret discovery/disclosure or silent configuration mixing; copied protected rows require encryption compatibility |
| FR-12.6, AD-29, 16.2–16.3 | Include static published-dataset document graph now | Authorized real nodes/edges, no invented topology or unsupported live-update claims |
| FR-9.4, FR-12.1–12.4/12.6, AD-29: history reader and Scan dashboard | Minimal exact-version document navigation and batch coverage/error reporting; full history explorer and Web Scan dashboard remain outside October | No erased product backlog, fabricated coverage or misleading document destinations |

These are scoped October deviations, not retrospective edits to generic acceptance.
Worker tools, Scan strategy and PRD changes still trigger independent plan review
and explicit planned-change approval before tasks. Implementation approval remains.
Use installed governed-delivery 0.7.2 and current profiles; no old deadline waiver.

## Reuse evidence and the first integration risk

Backup B's `demos/backup-b-direct-llm/BackupBDemo/Program.cs` is an orchestration
reference, not a safe unmodified runner. Prior inspection found real generation,
but also a localhost fixture interceptor, assumed RPC classifications, manually
selected units, mutable-working-tree reads and weak run-specific success reporting.
Those shortcuts must not become accepted October Knowledge.

Current `ScanRun.BindProfiledRevision`/`KnowledgeStoreContracts` expect Git-shaped
source identities; current `PreparedChatScopeResolver`/`ChatEvidenceRetriever`
expect legacy pair eligibility and completed revision-bound runs. Existing
DocumentVersion persistence also references ScanRuns. The recommended isolation is
a dedicated October store and explicit consumers, not a one-line adapter pretending
those conditions no longer apply. The early proof must establish prepared-origin
publication and retrieval through the October shared contract. A fixed-pair alias,
fake Git hash, forced completion or raw SQL import is not an acceptable workaround.
Compatible transaction/validation mechanics are reusable; legacy orchestration
conditions are neither weakened nor adopted as October eligibility.

Static code anchors for this boundary:

- `src/dotnet/Vulgata.Core/DomainServices/KnowledgeStoreContracts.cs:409` — Git-shaped source validation.
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/DocumentVersionConfiguration.cs:62` — existing ScanRun relationship.
- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs:101` — assembly-wide entity configuration discovery.
- `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeDocumentMaterializer.cs:16` — compatible content materialization seam.

These findings justify the proposed seam, not a runtime-qualified new implementation.
Keep post-competition adoption selective: reusable tools and wiki contracts may be
retained later, while prepared-origin eligibility and release composition remain
explicit until a separate normal-product design accepts them.

Qualification must separately establish: (1) real provider analysis and useful CL/BL;
(2) valid atomic wiki publication/replay without partial graph contamination;
(3) main-Web tool-loop answers with actual permitted document footers; and
(4) a graph built from that same dataset. Focus tests/reviews on changed behavior
and risk; do not add verification machinery solely for ceremony.

## Scope, risk and next decision

This is a significant but bounded release reslice, not a whole-product redesign.
The largest unknowns are the cost of isolated acceptance/storage, real analysis
quality/performance, useful restricted-profile JDT-LS behavior and replacing the
current candidate-selection Chat protocol. CodeGraph integration remains required
but receives a small direct integration check, not the primary research allocation.
The selected scope is smaller than Epics 13–17, but ten package labels are not a
delivery-time estimate or guarantee for October 14. Prove the shared data chain
early; avoid a late all-at-once integration.

Trigger, current lifecycle and requirement deltas are identified. Rollback of
completed 13.1 is unnecessary. Recommended path: bounded release reslicing; preserve
the longer-term backlog. Selection approval, formal story definitions and independent
feature planning/implementation gates remain outstanding. No runtime, provider,
database, package acquisition, story activation, Git commit or task message was
performed to produce this proposal.

Requested next decision: confirm or amend the remaining package selection and
contract-first parallel map. The user has selected database-per-batch with fixed
schemas and startup-bound selection, replacing the earlier schema-per-batch option.
The user has also accepted operator-stop finalization without an additional
content-quality threshold and the October exclusion of full Chat reconnect.
The September 27 cross-check corrections are accepted with two time-saving
exceptions: costly batch recovery and in-place Chat mode/scope/history transitions
may be skipped as secondary targets. Record actual supported behavior and use the
bounded fallbacks; this does not activate implementation or waive other gates.
Then formalize the scoped authority
overlay and replacement story definitions without overwriting historical records.

Database-per-batch/startup selection, UI preservation, real tool placeholders,
immediate answer streaming and minimum graph functions are recorded planning inputs.
No UI implementation, database creation or runtime connection switch was performed
to record them.

The latest user decisions above are accepted planning inputs, not blanket approval
of every earlier recommendation or implementation authority. Pending source names,
roots and context arrive through the user's preparation work. No database seeding,
source acquisition or provider execution was performed to record these decisions.
