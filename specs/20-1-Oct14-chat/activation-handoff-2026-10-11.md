# October Chat activation handoff — 2026-10-11

This is an implementation/activation assessment within the accepted first-deliverable
scope, not a new plan or runtime grant. The human user now owns management and
coordination. No other Codex chat was contacted for this assessment.

## Checked boundaries

- Assigned checkout: `oct14/chat`, starting at `fa69381595c472579ad29cf6b11ebe2733b74577`.
- Available integration ref inspected through Git objects: `oct14/main` at
  `d623c509` (pin this ref before applying this handoff). No other checkout was accessed.
- Chat parent, application, runner, registry and tests match integration for the
  existing owned implementation. Integration additionally has shared
  `OctoberChatHostRegistration.cs` and `OctoberChatAttachmentContext.cs`, plus shared
  reader/Program composition and host-composition tests. Those are absent from this
  lane baseline; building this lane alone is not proof of integrated activation.
- Existing unrelated `.editorconfig` change is preserved, not adopted into a Chat commit.
- No runtime settings, database, provider credential or retained publication were
  inspected. The existence of a branch/configuration class is not retained-data readiness.

## Ordinary-user execution path

| Step | Implemented path | Required external binding / failure boundary |
| --- | --- | --- |
| Login and page | Authorized `/` ChatPage; optional October application/entry/context resolution in `ChatPage.October.cs` | Ordinary authenticated Identity user and original security-stamp claim. No Scan operator identity substitution. |
| Selection | `OctoberChatEntryViewModel` → `OctoberChatSelectionService` → live disclosure policy, canonical `OpenReadSelectionAsync` and catalog search | Exact host-selected publication, repository scope, purpose `Chat` and mode. October selection is fixed by host configuration; there is no user-facing publication picker in this accepted slice. |
| Open or reopen | `OctoberChatApplication.OpenAsync` / open-only `OpenExistingAsync` → finite registry reservation → actual operation scope → canonical store open → transcript projection | Complete canonical persistence and live context required. Conversation/context query IDs are lookup inputs only. Unknown/foreign/active/unresolved locators cannot create replacements or dispatch. |
| Submit | Fresh operation scope, exact embedded prompt digest, canonical completed history, owner-produced dependency union, frozen provider snapshot | Never reconstruct contributors. Configuration/authority/preparation failure prevents dispatch. |
| Admit and run | `OctoberChatCoordinator` → canonical Begin and Running transition → actual client session → `OctoberChatTurnRunner` | Actual task observation before physical send; fresh original-user authority and selected profile after request-permit admission. |
| Provider/tools | Plain selected ChatCompletions client, zero SDK retry, bounded Agent/tool loop; SearchWiki/BrowseWiki, ReadWiki, GetRelatedDocuments, ReportAnswerDocuments | Explicit configured provider ID, protected credential and full-contributor disclosure. No Scan, source read or generation fallback. Business can consume BL only. |
| Streaming | Raw SSE guard → immediate authorized text/progress → bounded presentation queue → renderer rechecks → Chinese turn component | No whole-answer buffering or simulated reveal. Tool activity never exposes arguments, raw IDs or reasoning. |
| Save/display | Running → Saving → canonical exact terminal save; lookup-only uncertain reconciliation; authoritative saved transcript and reference footer | Physical transport, store operations and scope cleanup remain owned until actual settlement. Exact references appear after terminal save and open Explorer's exact Document version route. |
| Follow-up/reopen | Canonical completed-history projection and dependency reauthorization; reload uses durable IDs with fresh user/context checks | No regeneration on reload or uncertain acknowledgement. Interrupted/failed turns are not silently promoted to completed inference history. |

## Why “对话服务准备中” appeared

Before this wave, the textarea used that literal whenever
`OctoberEntry.Conversation == null || application.IsReady != true`. It also used it
in the explicit-October/missing-entry error branch. A separate paragraph used it
when an entry existed but no application was attached. Consequently it did **not**
identify one missing dependency: active generation, uncertain persistence, invalid
locator, denied selection, missing application and genuinely pending open all
looked like preparation. The application status paragraph sometimes supplied the
real reason, but the placeholder contradicted it.

The Chat-owned correction distinguishes actual entry/open preparation, current-turn
processing, uncertain state, invalid links and service unavailability. It does not
claim to know a hidden configuration/authorization cause or bypass disabled Send.

Diagnosis for operators, without exposing secrets in the UI:

1. Missing application with a supplied entry: application composition is absent.
2. Integrated valid Wiki with missing/disabled/invalid Chat configuration: shared
   registration deliberately supplies an inert application whose factory returns
   null; waiting does not activate it.
3. Entry has no conversation: canonical selection/access is unavailable or invalid.
4. Entry exists but application open fails: inspect the host's validated selection,
   live subject/stamp, canonical publication/persistence and selected provider
   readiness. The public status intentionally does not distinguish sensitive denials.
5. Busy/uncertain state: wait for owned settlement or use exact reconciliation.
   Never release an active turn because a row is temporarily absent.

## Minimal shared composition and configuration

These files remain owned by the human's shared-composition assignee. No changes to
them were made in this wave. The inspected integration branch already implements
the following; do not create competing registrations in Chat:

- `Program.cs`: calls `AddOctoberWikiReader(configuration, frozenConnection)` using
  the frozen `ConnectionStrings:DefaultConnection`; supplies Identity, protected
  secrets and Data Protection. Startup can migrate/seed, so startup needs its own grant.
- `Explore/OctoberWikiRegistration.cs`: validates frozen `October:Wiki`, registers
  `IDbContextFactory<OctoberDbContext>` with October migration history, canonical
  published source/selection policy/readers/protectors/dependency authority, the
  Chat attachment context, feature entry services and `AddOctoberChatHost`.
- `Services/OctoberChat/OctoberChatHostRegistration.cs`: singleton canonical codec,
  finite execution registry (`IOctoberDialogueExecutionAuthority`) and its
  `IOctoberDialogueCommitGuard`, singleton request admission; scoped application
  and operation-scope factory. Invalid explicit configuration stays inert.
- Per-operation factory: unique canonical `OctoberDialogueStore`, reader/disclosed
  reader, dependency authority and policy bound to the original authenticated
  user's accessor and original stamp. Same registry, original budget, selected
  provider and prompt-digest adapter. Await the same idempotent store cleanup and
  remaining scope disposal; faults retain capacity.
- `OctoberChatAttachmentContext`: fresh `KnowledgePurpose.Chat`, confirmed mode
  changes within frozen publication/scope, identity invalidation. Explorer uses its
  independent Reader-purpose context. Never refresh a captured stale stamp into a
  new authorization grant.

Exact required configuration keys (values must come from the owner, not fixtures):

| Section | Keys / constraints |
| --- | --- |
| `October:Wiki` | `Enabled=true`; exact nonempty `BatchId`, `PublicationId`; `CanonicalDatabaseIdentity`, `TargetId`, `SelectionDigest`; `Mode=IT` or `Business`; explicit `RepositoryIds` array; absolute existing writable/readable `LogDirectory`. |
| `October:Chat` | `Enabled=true`; explicit nonempty `ProviderId`; `MaximumConcurrentRequests` omitted or 1–4; `MaximumConcurrentExecutions` omitted or 1–16. Invalid values leave an inert application. |
| Host | Correct `ConnectionStrings:DefaultConnection`, stable Data Protection keys, ordinary-user Identity setup and required canonical migrations. Do not put credentials/real paths in tracked examples. |

The selected provider record must have ChatCompletions support, a valid configured
endpoint/model and decryptable protected API key. The explicit Chat provider ID is
not a Worker/Reader/default-provider fallback. Embedded October prompts and
`OctoberChatInputDigests.CreateAsync` are used unchanged. No configuration value
authorizes a provider call by itself.

For an application deployed from the inspected integration checkpoint, no new shared
code is currently proposed: supply/validate the above configuration and exact data.
For deployment from this lane alone, the shared owner must import the existing
integration composition (including its tests), not copy fixture factories into Web.
Any newly observed shared-code gap should return to the human with a scoped diff.

## Retained publication and mode boundary

Business owns the exact accepted publication, complete disclosure dependencies,
dialogue persistence and migrations. Required: retained non-disposable database;
matching canonical binding/batch/publication/digest; exact repository IDs; published
immutable Documents and lineage; ordinary-user access to every contributor; retained
Identity/provider/Data Protection bindings. No assumption is made that Scan has
produced these yet. Do not use deleted qualification databases or rescan to fabricate
an activation prerequisite.

CL-only data supports an **IT-mode** read/tool/reference/follow-up qualification.
Business mode must not reveal CL titles, bodies, references or source metadata.
Business useful-answer qualification requires accepted BL and its real complete
dependencies. An empty Business catalog is an empty result, not fabricated BL.

No owner-death proof is introduced: unresolved prior-host execution stays blocked.
Already-sanitized output lacking a complete generated capsule permits only the
verified whole-selection refusal closure; unknown document-specific lineage stays
fail-closed. These limitations are distinct from a composer configuration failure.

## Smallest real-browser qualification (not executed)

After the permissions below are granted, use one retained publication and one ordinary
user, one conversation and two purposeful questions. Do not regenerate Documents.

1. The shared owner configures/selects the exact retained publication and mode, then
   starts only the authorized host. Login as the ordinary user and open Chat; verify
   the selected mode/scope and ready composer. There is no publication dropdown in
   this bounded slice; do not simulate one or accept browser-supplied authority IDs.
2. Ask a question grounded in the selected Documents: for example, “这个流程的主要步骤
   和前置条件是什么？请说明哪些内容仍不确定。” Tailor it to the actual selected domain.
3. Record first visible answer text while the turn is still active, actual tool
   activity placeholders and final saved status. A precomputed answer, controlled
   transcript or component event ordering is not browser proof.
4. Ask a useful follow-up about one stated condition. Verify retained context and
   genuine tool results as needed; do not force an irrelevant tool call for a demo.
5. From the final footer open a reported Document; verify its exact immutable version,
   mode-appropriate content and access. Return to the conversation.
6. Copy its conversation/context locator, reload or reopen while still authenticated,
   and verify saved turns/references with no new generation. Separately verify one
   explicitly authorized cancellation if included in the runtime grant.

Record IDs/version identities and timing/status evidence securely; redact credentials,
hidden reasoning and unapproved source. Synthetic fixture answers never become page
content for the real presentation.

## Permissions needed together for that operation

- Exact retained database/publication/repository scope and ordinary-user account;
  permission to read it and write only its canonical dialogue records.
- Exact selected provider/model/endpoint and protected credential binding, approval
  to disclose the selected Documents plus follow-up history, finite request/round/time
  and spend bounds. Specify whether cancellation is an additional turn.
- Named local Web host/port, owned process lifetime and log/recording location;
  explicitly resolve startup migration/role-seeding effects before launch.
- Browser ordinary-user interaction and exact Document navigation. Retain the dataset;
  stop only owned processes. No deletion, source Scan, BL generation or retry grant is
  implied. A failure is reported before any newly needed operation.

Current authority covers only source-free controlled verification. The earlier
reported PostgreSQL consumer qualification is historical evidence; it does not grant
new database access. Controlled tests and a compiled host are not live Chat evidence.

## This wave's controlled evidence

- RED: 35 application cases selected, 22 passed and 13 failed on misleading composer
  placeholders; zero skipped. `TestResults/isolation-40f93487d89943ec915f95ae67db1398/results.trx`.
- GREEN: affected `OctoberChat|OctoberWiki|ChatPageTests` selection through
  `eng/test.ps1 -Suite SelfContained`: **603/603 passed**, zero failures/skips,
  discovery 5,220. `TestResults/isolation-b6f156d9e3544688838848d8ed619b52/results.trx`.
- Actual controlled SDK/canonical fixture flow: first question and follow-up each
  perform ReadWiki → ReportAnswerDocuments → final text (six physical fixture
  requests total). Text appears before held completion; tool placeholders are visible;
  footer is absent before terminal save and exact authorized references appear after.
  Follow-up uses the exact canonical completed-history projection. Existing cancellation,
  physical cleanup, unknown/uncertain lookup, saved reopening and disclosure cases pass.
- Independent review accepted the UI, tests and handoff with no actionable findings;
  the GREEN run satisfies its verification condition. No shared composition, prompt,
  contract, schema or runtime behavior was changed. Existing Chinese layout/styles and
  Send/authorization predicates are preserved.
- The unrelated `.editorconfig` hash remained
  `A9BF0C62C4DCE27EBF739B452988E26F0B4B72E728E1B79AE0F80BFDE8AF7C52`.
  The launcher used only process-local PATH deduplication and restored it; no persistent
  settings changed. No other chat was messaged; no application host, live provider,
  PostgreSQL or retained-data operation was started.

This is controlled consumer evidence at the assigned lane boundary, not a new full
integration-suite result. The management owner can integrate the reviewed Chat diff
into the inspected shared-composition line, then authorize the smallest real operation
described above when a retained publication exists.
