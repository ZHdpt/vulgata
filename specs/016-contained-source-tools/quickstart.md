# Story 18.1 Validation Guide

This guide describes the commands and observations required after implementation. The plan stage has not run them and has no production/runtime qualification. The [contract](contracts/prepared-source-tools.md) defines exact outcomes; the [data model](data-model.md) defines scope and lifetime.

## Prerequisites

- .NET 10 SDK matching project targets; run from the repository root.
- An explicitly owned temporary prepared-directory fixture created and cleaned by the tests, containing a small tree, an unfamiliar extension with valid UTF-8, a long text file, empty file/directory, invalid UTF-8, and a sibling outside the grant. Inject a controllable in-memory current-authority verifier for the approved batch, Repository, subject, Worker-source-read purpose and approval/root-binding identity. No database, Git repository, provider, CodeGraph or LSP is needed for the prepared-source tests.
- On the October Windows host, run reparse-point/alias/race cases with the privileges and filesystem support needed to create the fixture. If a Linux adapter is enabled later, run equivalent symlink/no-follow cases there. If fixture creation is unavailable, report that platform's containment qualification unverified rather than marking it passed.

## Build and focused controlled tests

```powershell
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~PreparedSource
```

The test suite must construct public `Vulgata.Infrastructure.Source.PreparedSourceContextIssuer` with the fixture's `IPreparedSourceCurrentAuthorityVerifier`, call `IssueAsync(PreparedSourceGrantRequest, CancellationToken)` and assert `PreparedSourceIssueResult` status/context invariants before invoking the Core read context and Agent tool facade. Cover approved exact tuple, missing verifier, absent/mismatched/unavailable approval, malformed grant, unavailable root, cancellation and cleanup; failed issuance returns no context or host-path detail. It should then prove live rechecks, no model root selector, finite caps, correct relative locations and sanitized errors. Agent wrapper/schema tests reject unknown fields, writes and commands. Direct tests work without database/model/tool readiness.

## Behavior scenarios

1. Grant one owned root. List its immediate entries, including an unfamiliar extension; read that file and search its literal content. All locations carry the bound Repository ID and relative path/range/match location. No physical path or admission category is emitted.
2. Use more entries, matches and file bytes than one page/range. Follow list/search continuations and read `nextByteOffset` until complete. Compare with fixture ground truth. Every interim response is `limited` with a valid same-scope continuation/range or exhausted-limit detail; none is complete. Empty directory, empty file at EOF and complete no-match search are explicit `complete-empty`.
3. Supply a foreign/replayed/altered cursor; path traversal, absolute/UNC/ADS aliases; a sibling path; escaping and in-root symlink/reparse routes; and write/command fields. Every attempted escape or unsupported action is denied, discloses no outside bytes/metadata and changes no outside file. On each platform, repeat while replacing/retargeting entries between checks and opens; containment must rely on held handles/no-follow, not timing luck.
4. Remove or change an observed entry between pages; make a permitted file unreadable or invalid UTF-8; request a code-point-splitting range; cancel during enumeration/search; expire the grant; call concurrently near budget exhaustion. Assert distinct outcomes, no false complete-empty, no over-budget disclosure, no content after interruption, and closure of owned activity.
5. On a platform without a qualified handle-relative no-follow adapter, grant open or operation fails closed with sanitized outcome. It must never switch to lexical-prefix traversal.
6. With the same production context and in-memory verifier, revoke approval or change its subject, purpose, batch, Repository, approval identity or root-binding identity before list/read/search and after a limited page. Deny further disclosure, invalidate every continuation, settle owned activity, and require a fresh approved grant after restoration. Repeat revocation and verifier unavailability between entries/chunks and at the final pre-release check; assert buffered entries, match counts, text and tokens are discarded, even during concurrent calls. A verifier error is `authority-unavailable`, never complete-empty or cached success. No physical path, subject or approval identifier appears in the sanitized result.

## Git-bound regression

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~RetainedSourceReadContextTests
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~RetainedSourceRangeReadTests
```

These selected existing tests check the normal retained Git-read boundary. If a selected test requires a database or other unavailable fixture, report that prerequisite honestly and still run the DB-free prepared tests; do not claim a skipped or blocked regression passed. The final governed verification gate may require wider build/tests and owned runtime qualification based on actual implementation risk.

## Evidence to record

Record OS/runtime, fixture ownership, exact test/build command and result, tested assembly boundary, caps and statuses observed, current-authority revocation/unavailability timing and outcome, platform-specific link/race result, resource settlement, and Git-bound regression result. Distinguish controlled tests from real Worker/model/tool invocation; this story's direct capability does not claim the later integrated Worker pipeline is qualified.
