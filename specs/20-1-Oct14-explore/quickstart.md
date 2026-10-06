# OCT-EXPLORE future validation guide

These are planned checks, not executed results or runtime permission. Implementation must first be authorized, B0 checkpoint supplied and feature code/tests delivered. All commands run from this assigned worktree. Never run the Web host against ambient/shared databases: startup migrates existing Identity/domain schemas.

## Prerequisites

- Same compiled B0 contracts/fixture consumed by all lanes; Business real store/live policy for real-data checks.
- Integration-owned Web composition and explicit permitted batch/publication, existing ordinary IT/Business accounts, declared loopback port and owned database resource. Business owns October schema application; Web must not migrate October.
- Feature test files classified in existing `tests/Vulgata.Tests/TestInfrastructure/suites.json`. Proposed test names below must exist and select nonzero tests before interpreting results.
- Real source-backed publication from Scan/Business for combined proof; same publication in Chat, graph and reader. Provider/native tool execution remains with owning lanes and requires its own authority.

## Controlled verification

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Explore'
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
```

Expected cases: ordinary non-manager exact CL/BL reading; older exact version and section preserved; invalid/missing section and unknown/wrong/unpublished target never substitute; Business exposes no CL; contributor revocation/outage clears safe state; untrusted markup cannot execute; coherent refresh and obsolete response suppression; no-wiki feature remains unavailable.

F3 continuation checks (planned, not run): use the Business owner's shared types and labelled fixtures for a Document >64 KiB and an individual section >64 KiB, plus boundary-sized projections around 65,536 UTF-8 bytes including multibyte characters. Each response must respect the owner's cap and preserve valid UTF-8/structured chunk semantics; following explicit next actions to the owner's terminal result must reproduce the full authorized narrative text in order, without gaps or duplicate narrative fragments. Intentionally repeated qualifier context beside continued claims is required, not duplicate narrative traversal. Count bytes of the owner-defined text/JSON projection, not characters; framing/qualification metadata must not justify exceeding that declared projection bound.

Open the oversized exact section directly and confirm first-portion focus, visible `本节尚未加载完`, keyboard-accessible `下一部分`, preserved adjacent qualifications on continued claims and section-scoped terminal feedback. The whole-Document path must likewise remain visibly partial until terminal. Verify one request per action, one displayed chunk only, no auto-drain, and duplicate-click/late-response suppression. Make a newer version available mid-read: every next request still addresses the original exact publication/version/section.

Between chunks, exercise mode/scope/subject/purpose/selection/version/section change, cancel, contributor revocation, authority outage, timeout and tampered/expired/wrong-operation cursors. Expected: no partial cancelled page, old display/qualification/cursor cleared, late results discarded, no mixed retained chunks or completion claim. Explicit restart uses a fresh authorized selection at the first chunk of the same exact target; no old cursor or latest-version fallback. Repeat persisted continuation behavior under the owned PostgreSQL qualification below after implementation. Initial shared-contract compile/conformance probes may establish B0 before dependent production wiring when separately authorized; this document correction runs none.

Graph tests cover 39/40/41 nodes, 79/80/81 Edges, empty/sparse data, cycles/self-loops, bounded acquisition and cancellation. Every displayed Edge preserves accepted identity/endpoints/type/direction. Compare paired fixtures differing only in denied records: visible payload, counts, continuation/incomplete signal and empty-state text must be identical. Unknown permitted coverage is labelled incomplete, not repaired by invented Edges.

After final feature correction, run the full SelfContained suite once through `eng/test.ps1`. Further repeats require changed behavior or unresolved failures. Controlled tests do not prove provider usefulness, actual authorization data binding or real PostgreSQL behavior.

## Owned persisted-read qualification

After runtime authorization and integration prerequisites:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~Explore'
pwsh -NoProfile -File eng/test.ps1 -Suite E2E -WithPostgreSql -Filter 'FullyQualifiedName~Explore'
```

The runner creates owned resources; do not supply a shared production connection or run cleanup against ambient targets. Zero selected/all-skipped tests, setup failure or missing TRX outcomes are non-success. If the future E2E implementation has a different classified name, use that verified filter and report it rather than pretending these proposed tests ran.

## Browser and combined case

Use integration's approved host start/configuration command only after confirming its database/port/process boundaries. No guessed production launch command is provided here.

1. Log in as a permitted ordinary non-manager. Navigate from the integration-provided entry to `/knowledge/documents`; IT opens real published CL and its exact section.
2. Open real BL in Business/light. Attempt an exact CL link without switching mode: no CL metadata appears. Verify no-BL empty state on a controlled selected publication.
3. Open `/knowledge/graph`; inspect real accepted direction/meaning and genuine Uncertainty. Open a node, then the matching Chat footer reference. Compare exact publication/version/section identities, not only similar titles.
4. Pan/zoom/fit, select and open with keyboard controls; verify focus, readable Chinese loading/error messages, light/dark boundaries and the accessible list alternative.
5. Refresh and switch scope/mode while delayed responses arrive. Verify a coherent selected publication and no stale protected results. Test unknown publication, authorization outage/revocation and missing data using owned fixtures/resources; restore only the owned state changed by the check.
6. Show a bounded result with visible limits. Business graph may be sparse or edge-free: confirm no hidden CL structure or manufactured BL-to-BL links.

Record code checkpoint, contract checkpoint, explicit publication, safe exact handles, permitted account roles, source-slice coverage, accepted-edge comparison and known limits. Keep credentials/raw source out of evidence. Distinguish controlled layout/policy tests, persisted reads and real generated cross-Repository usefulness. Integration owns final combined build/SelfContained and affected runtime checks; do not rerun unrelated native tools from Explore.
