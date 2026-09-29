---
governed_delivery_classification: architecture
governed_delivery_reason: Binds a material Worker navigation tool and application authority for restricted JDT-LS over an October prepared Repository; AGENTS.md planned-change authorization applies.
---

# Implementation Plan: Navigate Prepared Java Source with Restricted JDT-LS

**Branch**: `codex/18-2-plan` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Story 18.2 of `docs/bmad/planning-artifacts/october-release-epics.md`, the September 26 October proposal, this feature's specification, and the integrated Story 18.1 contract at `specs/016-contained-source-tools/contracts/prepared-source-tools.md`. The earlier plan-review-02 is invalidated by this integration and must be repeated.

## Summary

Offer bounded definition and reference navigation through an application-bound JDT-LS tool over one whole operator-prepared Java Repository. Preserve Story 18.1's `PreparedSourceContextIssuer` and `IPreparedSourceReadContext` unchanged: they provide only list/read/search and do not expose a physical root. Story 18.2 owns the smallest shared trusted physical-workspace issuer/lease for JDT-LS and Story 18.3 CodeGraph, with a separately authorized tool purpose and the same exact current-authority tuple. Keep JDT-LS state separate from source; disable automatic Maven/Gradle import, build and dependency restoration. Return only contained Repository-relative observations with truthful limits, after a fresh pre-disclosure check. Qualify real navigation, source no-write behavior and subprocess containment in the declared host profile.

This is a material agent-tool change under `AGENTS.md`: independent plan review and planned-change authorization precede tasks. The 18.1 integration correction is incorporated here; subsequent delivery depends on accepted review, a separately authorized shared-workspace checkpoint for 18.3 consumption, and ordinary downstream gates.

## Technical Context

**Language/Version**: .NET 10/C#; JDT-LS and Java commands assumed available on the host `PATH`, subject to later runtime/profile qualification. Command visibility does not establish Java version, navigation or restricted behavior.

**Primary Dependencies**: Integrated 18.1 `PreparedSourceApproval`, `IPreparedSourceCurrentAuthorityVerifier`, `PreparedSourceContextIssuer`, `IPreparedSourceReadContext` and `PreparedSourceLocation` semantics; existing Infrastructure StreamJsonRpc/JDT-LS mechanisms. The shared trusted workspace seam is Story 18.2-owned and separately released to Story 18.3 after a verified checkpoint. Trusted Infrastructure resolves the JDT-LS and Java commands from the host `PATH`; neither config-file executable selection nor test-local `.env`/`JDT_LS_BIN` is required.

**Storage**: Disposable JDT-LS data/configuration only, separate from prepared source. A trusted physical-root lease is process-local and non-durable. No database, Knowledge schema, source copy, Source Snapshot, Git revision identity or repository-wide hash.

**Testing**: Honest behavioral RED/GREEN in `tests/Vulgata.Tests`, controlled seam tests, then real JDT-LS definition/reference and subprocess qualification under the accepted implementation route.

**Target Platform**: October host-run profile; generic Docker deployment target remains outside this October delivery slice.

**Project Type**: Internal Worker tool and Infrastructure adapter in the existing modular monolith.

**Performance Goals**: Bounded readiness/request/queue/shutdown, one owned session per selected Repository, serialized unsafe protocol work. No completeness or throughput promise.

**Constraints**: Prepared directories remain operator-maintained and unchanged; no arbitrary path, source write, command tool, dependency restoration or silent result truncation. A physical path lease is not subprocess containment. Required native CodeGraph remains separate.

**Scale/Scope**: One whole Java Repository per grant, with concurrent Workers sharing at most one owned session. No other LSP or multi-session policy.

## Constitution Check

**Pre-research**: Constitution I/V require Core to own contracts, Infrastructure to own filesystem/process adapters and Agents to use injected capabilities, never arbitrary host access. IV requires honest RED/GREEN and actual tool qualification later. AD-15 and PRD FR-15.1–15.3 permit the temporary restricted profile. The October proposal specifically narrows source origin and deployment to operator-prepared host directories. No Constitution exception is proposed.

**Post-design**: These boundaries remain intact. Integrated 18.1 read context is preserved as a closed Worker capability. The new trusted workspace issuer has a separate tool-purpose check, exact current-authority checks, and no model-visible path. Direct-root JDT-LS no-write and subprocess containment remain real qualification dependencies. Failure returns to definition/architecture and affected gates.

## Project Structure

### Documentation (this feature)

```text
specs/18-2-navigate-prepared-java-source-with-restricted-jdt-ls/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/navigation-tool.md
└── quickstart.md
```

### Source Code (existing and planned seams)

```text
src/dotnet/Vulgata.Core/DomainServices/AnalysisRuntimeContracts.cs
src/dotnet/Vulgata.Core/DomainServices/PreparedSourceContracts.cs
src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs
src/dotnet/Vulgata.Infrastructure/Source/PreparedToolWorkspaceIssuer.cs (planned shared seam)
src/dotnet/Vulgata.Infrastructure/Analysis/Java/JdtLsAnalysisAdapter.cs
src/dotnet/Vulgata.Infrastructure/Analysis/Java/PreparedJavaNavigationContextFactory.cs (planned October seam)
src/dotnet/Vulgata.Agents/
src/dotnet/Vulgata.Web/Program.cs
tests/Vulgata.Tests/AnalysisRuntime/
```

**Structure Decision**: Preserve 18.1's public read issuer/context and source DTOs unchanged. Story 18.2 owns one small Infrastructure trusted-workspace issuer/lease and navigation-specific adapter/tool composition; Story 18.3 consumes the verified shared seam without a JDT-LS dependency. Do not create a parallel source registry, mirror, new project or general workspace framework.

## Implementation Design

### Responsibilities

- **ID-R01**: Trusted application composition selects the exact `PreparedSourceApproval` tuple (batch, Repository, subject, `WorkerSourceRead` purpose, approval/root-binding identities, operator label), physical root and JDT-LS profile. An additional application-selected tool purpose must authorize navigation; `WorkerSourceRead` alone is not unrestricted tool permission. Infrastructure owns workspace/process authority; Agent functions receive no physical root. This follows integrated 18.1, O-AR02/O-NFR01 and Constitution I/V.
- **ID-R02**: The navigation module translates requests and JDT-LS replies to bounded Repository-relative observations and limitations. It does not create source identity, accept Knowledge, infer a missing relationship from no result, own 18.1 file tools or replace CodeGraph (O-FR04–O-FR06; PRD FR-4.9/15.2).

### Interfaces

- **ID-I01**: One Worker-facing `PreparedJavaNavigationFunctions` construction surface borrows an issued navigation context and creates only definition/reference functions. Calls accept canonical Repository-relative Java path, one-based position and bounded result controls; return `repositoryId` and sanitized navigation outcome. Neither calls nor constructor can issue/change grants, choose a physical root, adapter or executable. The trusted caller owns async disposal after calls settle; cancellation is per call. See `contracts/navigation-tool.md` for callable shape. Preserve 18.1's three-function `PreparedSourceFunctions.Create()` exactly.
- **ID-I02**: Return observed relative path/range locations, request kind, explicit result bound/limit status and capability/classpath/failure classification. Zero locations mean `no result observed`, never absence proof. A capped LSP response is limited without an implied continuation. Withhold outside-grant URIs, physical paths and source content.
- **ID-I03**: Keep JDT-LS stdio, command resolution and the fixed restricted profile behind Infrastructure. Explicitly walk the host `PATH` entries in order for only `jdtls.exe`/`jdtls.bat` and `java.exe`; do not invoke a bare command or Windows ambient executable search. Treat empty/relative/current-directory entries or two supported JDT names in the same first matching entry as invalid resolution, not fallback lookup roots. Reject the first matching candidate if its canonical directory or any reparse-point ancestor lies in or can redirect into prepared source, disposable tool state, an unintended current directory or another host-untrusted entry; do not skip it for a later candidate. Require a regular non-reparse file of the expected extension under a host-approved executable directory protected from Worker/tool writes. Open and pin canonical path, file identity and content digest for the session, hold protection against replacement where supported, recheck identity immediately before absolute-path launch, and fail closed on mismatch. The approved process profile also governs any fixed `cmd.exe` interpreter and its children when the selected JDT command is a batch file; child `PATH` is constrained to approved resolved tools/system inputs. No model-selected command, config-file executable override, user-profile fallback or `.env`/`JDT_LS_BIN` discovery. If either command is invisible in the running host (including a stale process environment after a recent `PATH` edit) and blocks qualification, pause and report; do not guess another path. Resolved visibility is not runtime qualification. Models cannot select adapter, executable, workspace, import/build mode or host path (PRD FR-15.1/15.3; AD-15).
- **ID-I04**: Story 18.2 supplies `PreparedToolWorkspaceIssuer` in Infrastructure with a mandatory current-authority/tool-purpose verifier and `IssueAsync` taking trusted application-held grant/root, selected tool purpose and cancellation; it returns a status plus nullable process-local lease. The lease exposes lifetime/Repository ID to trusted composition and its physical root only to Infrastructure adapters, with bounded `CheckCurrentAsync` and `IAsyncDisposable`. A missing verifier, unauthorized purpose or unmatched tuple fails closed. Story 18.3 consumes this JDT-independent seam after its separately authorized verified checkpoint; exact shape is in `contracts/navigation-tool.md`.
- **ID-I05**: Add one October-specific `PreparedJavaNavigationContextFactory` in Infrastructure, constructed by trusted composition with a fixed `PreparedJavaNavigationProfile` and injected `IPreparedJdtLsRuntime` (controlled test implementation or later qualified production implementation). `OpenAsync(PreparedToolWorkspaceLease, CancellationToken)` accepts only a current `JdtNavigation` lease, returns `PreparedJavaNavigationOpenResult` with exactly one `IPreparedJavaNavigationContext` on `Opened` and null otherwise, and never accepts `AnalysisScope`, a model path, adapter name or executable. The runtime seam lets controlled tests observe process start/disposal and supply protocol replies without a real launch; pre-return failure reports sanitized status and cleanup confirmation through the trusted-only `PreparedJdtLsOpenFailureException` so the factory can release or quarantine its slot honestly. Production composition remains fail-closed until the accepted trusted-PATH/process-profile work. See `contracts/navigation-tool.md` for callable shape/statuses.
- **ID-I06**: Add one `internal` Infrastructure-only `PreparedToolWorkspaceLease.UseContainedRootAsync(expectedPurpose, callback, cancellationToken)` operation, returning only `PreparedToolWorkspaceCheckStatus`. Its short-lived `PreparedToolRootScope` callback view offers trusted root path for immediate tool binding, state-root separation validation, and raw file-URI to `PreparedSourceLocation` mapping through no-follow contained traversal; no handle, root identity or physical path becomes a public/model property or callback result. The operation checks exact live approval, tool purpose, deadline and pinned root identity before and after the bounded callback. This is the shared 18.2-owned seam used by 18.3 only after a new verified checkpoint; callable shape is in `contracts/navigation-tool.md`.

### State and Lifetime

- **ID-S01**: Key session ownership by the selected Repository binding from F0; permit at most one live JDT-LS process per selected Repository in the host. Root/profile conflict cannot reuse it. Acquisition and release include protocol, queue, process and disposable data/configuration; waits or busy results are bounded.
- **ID-S02**: Use the prepared Repository as source project root with separate disposable JDT-LS `-data` and configuration. Do not mirror/copy source, invent Git identity or use `PreparedRevisionJdtLsWorkspaceLeaseProvider` as an October grant. Qualification must establish no writes to prepared source; a failure returns for reviewed design correction.
- **ID-S03**: Each trusted lease binds the exact approval tuple, one selected tool purpose and observed root identity. JDT-LS may retain its `JdtNavigation` lease for one bounded session; 18.3 obtains a separate `CodeGraphPreparation` lease for its own operation lifetime, never the same lease or another purpose's permission. Either closes on revoke/mismatch/unavailable verifier, deadline, cancellation or disposal, cancels owned work and is never cached/reopened under restored approval. Process termination and tool-state cleanup remain adapter-owned; the shared issuer adds no JDT dependency for 18.3.
- **ID-S04**: Ownership changes only at a successful return from injected `IPreparedJdtLsRuntime.OpenAsync`: until then the runtime owns and settles every process, protocol and disposable-state resource it acquires, including on exception/cancellation before return. The factory owns the one-session slot, maps failed/cancelled open to sanitized null-context status and releases that slot only after runtime settlement is confirmed; it cannot dispose runtime partials it never received. A trusted `PreparedJdtLsOpenFailureException` with `CleanupConfirmed=false`, or an unexpected exception with no cleanup attestation, maps to `PreparedJavaNavigationOpenStatus.CleanupUnconfirmed` with null context, quarantines the Repository slot and blocks further runtime open/retry until owned-resource resolution. If surfaced to a Worker, map to existing `ConfigurationUnavailable` with empty payload. If a session is returned but later lease/profile validation, cancellation or context construction fails, the factory disposes that returned session before releasing the slot. After successful context handoff, the context owns/disposes the returned session and releases the slot after Worker calls settle. The context borrows the separately issued workspace lease; the trusted caller always disposes the lease after the context, or after failed open. No failed/disposed context or partial session is reusable.
- **ID-S05**: A `PreparedToolRootScope` is valid only inside the awaited callback and is invalidated on return, cancellation or lease closure. The lease serializes this short root operation with disposal and rechecks current authority/root identity after callback completion before returning `Current`; if it changed, the caller discards every derived location/binding and settles any started owned session. Long-running navigation and Worker disclosure occur outside the callback, with separate existing during-use and pre-disclosure checks. A copied physical path is confined to a trusted adapter launch binding and is revalidated immediately before launch; it is never returned to Agent code.

### Async and Cancellation

- **ID-A01**: Bound issuance, readiness, queue wait, navigation call, authority check and shutdown. Serialize shared protocol operations while concurrent safety is unproven. Cancellation removes a waiter or settles the active operation without delivering late results to another request. Retire an unhealthy/timed-out session before reuse.

### Failure and Retry

- **ID-F01**: At issuance, before source/protocol use, between bounded work chunks and immediately before response disclosure, verify the exact current approval and separate navigation purpose. Revocation, changed approval, expired grant, unavailable verifier or disposal latches the navigation authority closed, cancels owned activity and discards every buffered location/count/limit detail across concurrent calls. A restored approval requires new issuance. Distinguish these from host `PATH` command invisibility, readiness/protocol failure, cancellation, timeout and classpath/capability limit; sanitize outcomes without source, physical path, approval/subject or outside-root metadata. Command invisibility that blocks qualification is a pause/report condition, not a silent alternate launch.
- **ID-F02**: Retry only a new bounded attempt after the failed owned session is fully settled and the same grant remains valid. Never auto-restore dependencies, switch root/adapter or replay uncertain results. Separately granted 18.1 file tools remain usable when navigation is limited; CodeGraph remains required (O-FR04–O-FR06).
- **ID-F03**: A trusted path lease, trusted command candidate and relative-URI filter do not by themselves constrain JDT-LS process reads/writes or child launches. The host profile must prove a separate least-privilege process boundary: selected prepared root read-only, writable disposable tool state separate, and unrelated host files inaccessible except declared Java/JDT runtime inputs. Disable automatic Maven/Gradle import/build/restore. Reject candidate/identity changes before launch, unsafe interpreter/child behavior and any unsafe launch profile; never fall back to a different PATH candidate or broader process permission. If this cannot be established, navigation is unavailable pending architecture correction.

### Reuse and Locality

- **ID-L01**: Reuse current StreamJsonRpc stdio, bounded request, process cleanup and relative mapping only after direct-root assessment. Current `AnalysisScope`/`ProfiledRevision`, revision lease, project mirror/copy, Windows configuration and empty-as-success parsing are not accepted October semantics. Concentrate binding/containment/normalization in the navigation module, not Worker callers.
- **ID-L02**: Reuse integrated 18.1 `PreparedSourceApproval`, `IPreparedSourceCurrentAuthorityVerifier`, `PreparedSourceLocation` semantics and fail-closed issuance/check cadence, without modifying `PreparedSourceContextIssuer`, `IPreparedSourceReadContext` or list/read/search. Keep CodeGraph native MCP and Scan Control scheduling with their owners. Only the shared trusted workspace lease is delivered for 18.3 reuse (O-AR06/O-AR07).
- **ID-L03**: The current `JdtLsAnalysisSessionFactory.OpenAsync(AnalysisScope)` and its prepared-revision lease remain historical and must not be wrapped as an October grant. Reuse only its verified stdio/protocol/process mechanics behind the new injected runtime; keep its old callers intact and concentrate October grant validation, result mapping and lifecycle in the new factory/context seam.

### Test Seams

- **ID-T01**: Exercise the public navigation interface with a representative Java fixture, real declarations/callers, actual definition/reference locations and classpath limitations under the no-import/no-build/no-restore profile. Do not count initialization alone as navigation (O-AR09).
- **ID-T02**: Through the public issuer/tool seams with injected authority/process/protocol and owned roots, exercise missing/wrong tool purpose, foreign roots, traversal/symlink escape, unmappable URI, overlap, cancellation, timeout, source unavailability and cleanup. Test issuance, mid-use and pre-disclosure revocation/mismatch/unavailable verifier with multiple buffered calls; each releases zero payload and cannot resume without a new grant. Verify no source writes or unrelated process termination. Fakes do not replace real JDT-LS qualification.
- **ID-T03**: Verify the JDT-independent shared workspace issuer/lease through focused public-seam tests and an identifiable checkpoint before Story 18.3 consumes it. Exercise PATH selection with a trusted candidate, a prepared-source or tool-state `jdtls.bat`/`java.exe` ahead of it, an empty/relative/current-directory entry, reparse redirection, wrong file type and candidate replacement between resolution and launch; every untrusted first match must fail closed without starting a process or searching later PATH entries. Verify pinned identity and the fixed interpreter/children share the approved process profile. Separately qualify real restricted imports/builds, source no-write and subprocess containment; a path lease or `Get-Command` result alone is insufficient.
- **ID-T04**: Construct the October factory through its documented trusted profile/runtime inputs and a current JDT-purpose lease; use a controlled injected runtime that records process/protocol lifecycle and supplies definition/reference raw replies. On a throw/cancel before session return, verify the fake runtime cleans its own partial resources while the factory returns a sanitized null-context status and frees the slot. Inject `PreparedJdtLsOpenFailureException(CleanupConfirmed=false)` and an unexpected exception: both yield `CleanupUnconfirmed`/null context, leave the slot unavailable, and a second open for that Repository must not invoke runtime; any Worker projection is empty `ConfigurationUnavailable`. On failure after session return, verify the factory disposes that returned session; on successful context handoff, verify context disposal owns it and the caller separately disposes the lease. Check wrong-purpose/closed-lease rejection and zero real process starts. A composition check keeps production-capable runtime unavailable until trusted PATH selection/process containment has accepted behavioral evidence; controlled fake checks cannot prove real rootUri or restricted JDT settings, which belong to T022–T024. No T010/T011/T013 controlled test calls the legacy `AnalysisScope` factory.
- **ID-T05**: Through the internal shared lease operation, exercise current JDT and CodeGraph purposes separately, wrong purpose, revoked/unavailable approval, deadline, root replacement, disposal/cancellation during callback, state root overlap/reparse, non-file/outside/encoded-traversal URI and symlink-escaping result. Assert only contained relative locations leave the callback, no public root/path/handle is added, changed authority discards buffered mapping, and 18.3 can use the JDT-independent operation after a separately verified checkpoint. Controlled root tests do not prove JDT-LS subprocess containment or real navigation.

### Alternatives

- **ID-X01**: Reuse of the existing prepared-revision lease/project mirror unchanged was rejected: it verifies Git-style revision identity and copies source into a JDT-LS project, contrary to the October prepared-input/no-copy contract.
- **ID-X02**: Worker-chosen workspace/executable and per-Worker sessions were rejected because they defeat application authority and one-session ownership (O-FR06/O-NFR01). Dependency-aware import/restore or another LSP requires later scope under AD-15.

### Consequences

- **ID-C01**: No-import navigation may miss definitions/references across unresolved classpaths; every result must retain its observed limits, while 18.1 file tools permit continued inspection and native CodeGraph remains separately required.
- **ID-C02**: Direct prepared-root use avoids duplicated source but exposes a real JDT-LS no-write risk. Failure requires architecture correction and affected review/authorization, not silent copying or operator-source changes.
- **ID-C03**: Integrated, verified 18.1 closes the earlier contract uncertainty, but this corrected plan and prior plan review must be independently reaccepted before any planned-change authorization or downstream delivery. Real restricted-profile and subprocess evidence remain pending.
- **ID-C04**: Story 18.2 now owns the small trusted physical-workspace seam for 18.3. This improves reuse but makes its separately verified checkpoint and integration authorization prerequisites for 18.3; 18.2 navigation-specific protocol/outcome planning can proceed independently.

## Dependencies and Verification Limits

1. **18.1 integration**: The accepted `PreparedSourceApproval`, current verifier, issuer, read context, location and fail-closed outcome semantics are now integrated. Its Worker context has only list/read/search, no physical workspace. This corrected plan consumes that baseline without changing it.
2. **Shared workspace checkpoint**: Parallel Development assigned Story 18.2 the smallest trusted physical-workspace contract, implementation and focused tests for JDT-LS and 18.3 CodeGraph. Story 18.3 must wait for an identifiable verified checkpoint and separately authorized integration before consumption. Its JDT-independent use must not inherit navigation permission from `WorkerSourceRead` alone. This coordination decision is not plan or implementation approval.
3. **Restricted JDT-LS feasibility**: Present `initialize` has no evidenced Maven/Gradle disabling settings and the revision lease copies source. A trusted path lease does not prove subprocess containment. Future qualification must establish fixed settings, least-privilege process access, useful navigation and no source writes. This plan claims none.
4. **Qualification sequence**: The user chose a small owned representative fixture first, then the prepared registration/login Repository when supplied. Evidence from the fixture cannot be promoted to the later Repository or universal Java completeness. Real JDT-LS qualification resolves JDT-LS and Java on the host `PATH`; a missing command that blocks the run is paused and reported. The currently observed `jdtls.bat` and `java.exe` visibility is static host evidence only, not navigation or containment proof.
5. **Evidence**: This turn inspected repository authorities/source only. No JDT-LS process, provider, build, test, database or application runtime was run.

## Self-Review

- **Feasibility**: Existing protocol/process code is a mechanism candidate. The new factory uses the already implemented JDT-independent lease and Core navigation interface, while an injected runtime keeps controlled tests independent of real launch. The lease currently holds a private root path/handle and checks identity, but no callable internal access operation; ID-I06 supplies only that missing operation. P-PARTIAL-OPEN-OWNERSHIP-01 established runtime/factory/context cleanup handoff and remains intact. Explicit ordered PATH inspection and absolute launch remain feasible design seams, but trusted-directory qualification, replacement protection, restricted settings, subprocess isolation, direct-root no-write behavior and real definition/reference quality remain unqualified.
- **Invariants**: P-PATH-TRUST-01 is addressed by excluding unintended current-directory/prepared/tool-state candidates, rejecting an untrusted first match, validating exact file type/location, pinning identity and using the approved interpreter/child process profile. This preserves PATH as the user's command source and adds no model path selector. Exact current approval plus explicit tool purpose at issuance/during use/pre-disclosure, zero buffered payload on loss, one Repository/session, bounded serialized calls, contained relative observations, independent 18.1 file-tool fallback and no outside-root disclosure/source write remain defined.
- **Migration/compatibility**: Preserve integrated 18.1 public issuer/context/functions and historical Git/revision adapter/validators. The new root callback is internal to the existing 18.2-owned shared lease and JDT-independent for 18.3; the earlier shared checkpoint must be refreshed and separately authorized before 18.3 consumes it. No migration, package upgrade, source copy, Docker change or 18.1 contract rewrite is proposed.
- **Constitution/ADR alignment**: Core/Infrastructure/Agents ownership follows Constitution I/V and O-AR02; restricted profile follows AD-15/PRD FR-15.1–15.3; truthful limitations follow FR-15.2/O-AR09. October host/source scope does not claim the generic Docker/Source Snapshot design is implemented.
- **Lessons**: The current `docs/project-lessons.md` requires explicit callable public seams before behavioral TDD; `contracts/navigation-tool.md` now names the Infrastructure factory construction, open result/status, runtime injection, context/lease disposal, cancellation and permitted argument surface missing at T010. Its native filesystem test-permission lesson requires matching launcher/permission boundaries before interpreting a root-open failure. Historical Story 12.1 JDT-LS notes inform mechanisms but do not qualify October runtime.

No Constitution violation is proposed, so Complexity Tracking is not needed.
