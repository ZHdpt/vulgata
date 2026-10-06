# Imported K0 consumer acknowledgement and W0 proposal

## Authorized preparation boundary

OCT-Integrator dispatch (chat `01a1109d-ec4c-7500-8b73-642a5e22f59b`) authorizes the exact integration import, initial Chat contract/fixture conformance and its exact suite classification, scoped SelfContained execution, planning-symbol alignment and a bounded preparation commit. It does not authorize C0 production, a Chat tool/provider loop, dialogue storage, Web composition, native tools, database/migration/provider calls or host startup. No outbound message is required or authorized.

- Accepted Chat planning/tasks checkpoint: `4fad5314` (before import).
- Imported integration commit: `3bcd815b09cfbac7418d6644a2a75816e0788ec0`.
- Canonical Business candidate: `07c3fe4463e3228a6982fd106b58148ca87974bd`; documentation handoff: `7a189c34aec625202f74846d4907ded787ccbdca`.
- Chat merge commit: `f8127cb9`; no history was reset/rebased and no other checkout was changed.
- K0 acknowledgement here concerns the compiled read surface and controlled fixture only. Integration determines whether all lanes' acknowledgements complete its T006/accepted K. W0 is a separate unresolved prerequisite and does not block this K0 acknowledgement.

## Exact imported symbols

Canonical Core namespace: `Vulgata.Core.DomainServices.OctoberKnowledge`, file `src/dotnet/Vulgata.Core/DomainServices/OctoberKnowledgeContracts.cs`. Shared transport/validators: `Vulgata.Shared.OctoberKnowledge`, files `KnowledgeDtos.cs` and `KnowledgeRequestValidators.cs` in `src/dotnet/Vulgata.Shared/OctoberKnowledge/`.

| Chat planned need | Imported owner symbol and consumer meaning |
| --- | --- |
| Explicit selected database/batch/publication | `KnowledgeBinding`, `PublicationBinding`; server binds them, never a model connection/selection argument. |
| Authenticated ordinary Chat selection | `KnowledgeSubject`, `ReadSelectionRequest` with `KnowledgePurpose.Chat`, `KnowledgeMode.Business` or `KnowledgeMode.IT`, explicit `RepositoryScope`; `IOctoberKnowledgeReader.OpenReadSelectionAsync` returns `ReadSelectionResult` and opaque `ReadSelection`. Do not copy legacy `ChatMode.It` into canonical types. |
| Search/browse | `KnowledgeSearchRequest`, `SearchPage`, `DocumentSummary`; empty query means browse. |
| Exact read and section/chunk continuation | `ExactReadRequest(ReadSelection, DocumentHandle, ContinuationToken?)`; section is `DocumentHandle.SectionId`, not a new separate shared parameter. `ContentPage.Next` is owner-issued continuation; preserve exact handle/selection and pass its value unchanged through `ReadKnowledgeRequest.Continuation`. |
| Related meaning and unknowns | `RelatedReadRequest`, `RelatedPage`, `RelatedDocument`, `RelationshipKind.References/GeneratedFrom`, `Uncertainty`. Related targets alone do not grant current-turn read credit. |
| Disclosure union | `IOctoberKnowledgeDisclosurePolicy.CheckAsync(DisclosureRequest)` using the entire `AllContributors`, selected purpose/mode and optional `DocumentKind`; no new Chat access enum/policy. |
| F4 qualifications | `QualifiedClaimGroup`, `ClaimQualification`, `EvidenceReference`; owner vocabulary SourceSupported/Advisory/Hypothesis/Uncertain/Mixed. Preserve material attribution in future prompts/stream/history. |
| Coverage | `CoverageProjection(IsPartial, PermittedGaps)` is published-corpus coverage, not per-Document consumed-byte accounting. Track successful returned content/continuations in the future Chat ledger; `ContentPage.Next != null` means more exact content remains. A corpus may stay partial when an individual content stream is exhausted. No guessed offsets or alternate cursor type. |
| Shared browser shapes | `OpenKnowledgeSelectionRequest`, `SearchKnowledgeRequest`, `ReadKnowledgeRequest`, `RelatedKnowledgeRequest` and their existing validators; response wrappers retain Core `SearchPage/ContentPage/RelatedPage`. They do not supply host identity or Chat-purpose authority. |
| Bounds | `KnowledgeContract.MaximumContentBytes` is 65,536; canonical page/query limits and continuation lifetime remain owner-defined. Chat's stricter per-turn/page/aggregate limits are future consumer behavior, not changes to these types. |

The initial fixture is `tests/Vulgata.Tests/October/Knowledge/OctoberKnowledgeFixture.cs`, namespace `Vulgata.Tests.October.Knowledge`: `OctoberKnowledgeFixture`, `ControlledKnowledgeReader`, `ControlledDisclosurePolicy`. Tests reference these actual types; no copied fixture, competing DTO or access policy was added.

The fixture uses small fixed two-chunk text and in-memory token dictionaries. It proves call/DTO compatibility, exact identity, modelled live rechecks and qualification transport. It does not prove cryptographic cursor protection/expiry, cancellation semantics, >64-KiB reading, production Identity/authorization, durable dialogue, real streaming or answer quality. Its claims cover source/advisory/hypothesis groups; future Mixed-prose and large-content conformance remains required in T010/T014/T024. Those limitations do not turn initial K0 conformance into C1 completion.

`IOctoberExecutionScope` and `ProviderAttemptRequest` are generator Business-finalization contracts with `BusinessWorkKind.Resolution/Synthesis`, not Chat provider admission. Chat must not invent a WorkKind or repurpose a finalization operation key. W1 will supply Web's separate process-local admission under agreed policy. No provider scope was invoked here.

## Bounded consumer checks

Lane-owned file: `tests/Vulgata.Tests/October/Chat/OctoberChatKnowledgeConformanceTests.cs`; exact added inventory entry: `October/Chat/OctoberChatKnowledgeConformanceTests.cs` -> `SelfContained`. The dispatch expressly permits this classification edit; integration serializes later inventory merges. No existing classification or owner fixture changed.

Ten controlled cases consume actual owner interfaces/Shared validators: IT and Business Chat-purpose exact section/continuation DTOs with UTF-8 and qualifications; changed token/section/selection rejection; revoked contributor between chunks; Business metadata and uncited contributor exclusion; exact related direction/meaning/Uncertainty; wrong database unavailable; and whole-contributor disclosure denial. This is conformance of an existing delivered surface, not new production behavior; no artificial RED or product implementation is claimed. Test evidence is recorded in quickstart.md after the isolated launcher finishes.

## W0 — minimal proposed Web composition seam (not implemented)

Static inspection: `Program.cs` composes services through `VulgataWebComposition.ConfigureServices`, freezes `DefaultConnection` in `ConfigureContainer` via `DefaultConnectionSnapshot`, and registers only the legacy prepared Chat stack. `ChatPage.razor.cs` still selects demo repository names and legacy scope. There is no `Web/Services/OctoberChat` or October Web reader module in this imported checkpoint. K0 is declarations/validators plus controlled test services, not a production reader implementation.

**Proposed integration-owned seam**, final naming to be settled by integration: `src/dotnet/Vulgata.Web/Services/October/IOctoberWebReadContextProvider.cs` with one async resolve operation taking canonical `KnowledgeMode`, an optional explicitly requested Repository ID subset and cancellation, returning `ReadSelectionRequest?`. Null means not enabled/available, with no selected-object metadata disclosure. The host, not Chat/browser/model, supplies `KnowledgeSubject`, configured `PublicationBinding`, trusted `KnowledgePurpose.Chat` and live-permitted scope. Absent requested subset means the host's explicit permitted publication scope, not latest-batch discovery. The concrete provider must validate live authority and fixed identity; a returned request is not lasting permission, and every reader/disclosure call still rechecks.

**Integration-owned implementation/composition paths**: `src/dotnet/Vulgata.Web/Services/October/OctoberWebReadContextProvider.cs`, `OctoberWebReadOptions.cs`, `OctoberWebReadRegistration.cs` and the existing `src/dotnet/Vulgata.Web/Program.cs` registration point. Use the frozen DefaultConnection, explicit selected binding, real server authentication/security-stamp resolution and Business-owned reader/disclosure implementations when available. Keep a truthful unavailable state before that implementation exists; never register ControlledFixture in production. Any scope-picker labels must come from the existing Repository metadata surface under the same current selection/disclosure checks, not demo names or unfiltered metadata. Integration coordinates this label projection without defining another Knowledge identity/schema.

**Chat-owned later consumption paths**: the accepted `Services/OctoberChat/OctoberChatSelectionService.cs`, `OctoberChatModule.cs`, `Web.ViewModels/OctoberChat/OctoberChatEntryViewModel.cs` and feature-local `Components/Pages/ChatPage.razor(.cs)` bindings. No edits to these production paths in preparation. No separate route or new public API is necessary for W0. Explore's reader remains E0. Selected provider/credentials/admission are W1, and durable dialogue is D0; neither is silently included in this minimal W0 request.

**W0 acceptance checks (future, owner-coordinated)**:

1. A component/test harness consumes the seam with canonical types and an ordinary authenticated subject, purpose Chat and explicit publication; no operator-only selector or management role required. Mode/subset input cannot change database/publication/subject/purpose.
2. Missing, disabled, unpublished or mismatched selected data returns unavailable with no fallback; administration remains available. No production controlled fixture registration.
3. The exact frozen startup connection is used; runtime config mutation cannot silently switch it. No October migration, generator credential or provider call is triggered by opening Chat. Existing startup Identity/domain side effects require owned resource authority before any actual host test.
4. Business/IT and one/several/standalone requested scope use current owner policy. Denied contributor/account metadata and unsafe picker labels cannot leak; host context is not a cached access grant.
5. No durable turn/provider prerequisite is introduced just to open unavailable/selected Chat entry. D0/W1 still block live C1; W0 readiness does not authorize C0 implementation in this bounded dispatch.

## Task accounting and stop

- T001: after the authorized ordering clarification, this task is **K0-only and complete** from prior passing preparation. W0 verification moves to T007, where it still blocks integrated C0 completion. This accounting change does not start T003–T006.
- T002: exact prep test inventory classified under this dispatch; broader test/production ownership and later-file inventory remain unexecuted, so T002 remains unchecked.
- T003–T038: not executed; specifically no C0 Chat implementation or T007 UI checkpoint claim. The conformance test file is a bounded T001 consumer probe, not a replacement for future Chat behavior tests.
- Pause after the preparation commit and local final report. No message back to the integrator, merge of this branch elsewhere or further Wave 2 work is implied.

### Scheduling clarification (planning only)

The integration owner's proposed correction is sound: requiring W0 before T006 would block the module author on the composition that must consume its registration entry point. K0 permits controlled feature-local T003–T006 after separate implementation authorization and T002 ownership/classification; those tasks consume canonical ports and harness inputs, not a presumed accepted W0 API. Integration then settles/wires W0; T007 verifies its actual commit/composition and unchanged C0 acceptance. The W0 interface above remains only a proposal, unimplemented and unaccepted until its owner settles it. Missing production readers, D0/W1 and runtime authority remain explicit boundaries. The reported integration checkpoint `12d92d80339ea622cd26c593ebdb74da59401f91` was not merged or independently qualified by this planning-only dispatch.
