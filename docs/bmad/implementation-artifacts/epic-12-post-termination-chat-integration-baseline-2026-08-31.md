# Epic 12 post-termination Chat integration baseline — 2026-08-31

## Status and authority

- Branch: `demo0831/story-12-5-trackC`
- Integration base: `5a81283c13b342753f5ba6f69ec7fdb513dbb5d3`
- Story 12.5 blocked-termination commit: `8d56f460`
- Corrected termination-preflight commit: `5a81283c`
- Selective source commit: `afd2d903c15019489b3b4ab682434e2939677f41`
- Story 12.5 remains `terminated-blocked-incomplete`; this checkpoint does not reopen,
  complete, release, or replace the terminated story.
- The user-owned `.env.example` modification remained untouched and uncommitted.

## Included production slice

Only the following post-termination Chat hardening was integrated:

- `src/dotnet/Vulgata.Agents/ChatExecution/ChatProviderReasoningOptions.cs`
  defines an optional validated `ChatProvider:ReasoningEffort` setting.
- `src/dotnet/Vulgata.Core/DomainServices/ChatStreamingContracts.cs` carries the
  nullable setting on the server-owned streaming request.
- `src/dotnet/Vulgata.Agents/ChatExecution/OpenAiCompatibleChatProviderStreamingTransport.cs`
  serializes `reasoning_effort` only when configured, validates supported values,
  and retains mandatory `stream: true`.
- `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs` resolves and
  normalizes the option, propagates it to transport, adds it to request/provider
  fingerprints only when configured, classifies cancellation/deadline-originated
  provider errors accurately, and maps a missing required provider property to a
  fail-closed `InvalidSelection` result.
- `src/dotnet/Vulgata.Agents/DependencyInjection.cs` registers the options type.
- `src/dotnet/Vulgata.Web/Program.cs` manually binds and validates only the
  reasoning option while preserving all current Track C composition.
- `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs` covers configured propagation,
  deadline error classification, and strict missing-echo rejection.
- `tests/Vulgata.Tests/OpenAiCompatibleChatProviderStreamingTransportTests.cs`
  covers configured serialization and default omission.

With the setting omitted, the accepted Track B request and provider fingerprints
remain byte-for-byte unchanged.

## Explicit exclusions

No demo or recording behavior from `afd2d903` was integrated. Exclusions include:

- `demos/epic12-backup/**`, clone/database bootstrapping, recording launchers and
  checklists;
- demo Business narrative options, prompts, policies, query expansion and provider
  missing-echo reconstruction;
- partial-Repository evidence, retrieval/currentness changes, grounded-rescue
  suppression, and provider-authored narrative relaxation;
- screenshots and `docs/superpowers/evidence/**` backup evidence;
- the separate 262,144-byte reasoning allowance and its test;
- the change that delayed inactivity enforcement until the first answer delta;
- any change to authorization, currentness, citation, semantic validation,
  publication, `ChatEvidenceRetriever`, `PreparedChatCoordinator`,
  `ChatAgentResultValidator`, or `ChatProviderStreamAssembler`.

The accepted 65,536-byte aggregate Provider Chunk limit, ten-second inactivity
contract, mandatory primary/repair streaming, whole-answer validation, sole-result
publication, authorization, currentness and citations remain unchanged.

## Verification

One bounded affected-suite command was run:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatAgentResultValidatorTests|FullyQualifiedName~ChatContractTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~OpenAiCompatibleChatProviderStreamingTransportTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~DemoBusinessRetrievalQueryBuilderTests"
```

Result: `175 passed, 0 failed, 0 skipped`. The same command compiled
`src/dotnet/Vulgata.Web/Vulgata.Web.csproj` successfully and produced
`Vulgata.Web.dll`, so no duplicate standalone Web build was run. Existing package,
analyzer and repository-wide warning noise remained; no integration compile error
occurred.

Additional bounded checks:

- selected-path `git diff --check`: pass; only configured LF-to-CRLF conversion
  warnings were emitted;
- path-scope review: zero unexpected modified/untracked paths beyond the explicitly
  selected slice and the preserved `.env.example`;
- selected production/test added-line scan: zero credential, demo narrative,
  partial-scope, widened-response-limit or recording-only matches; this baseline
  document necessarily names the excluded boundaries and the historical bounded
  filter class.

## Known JDT-LS architecture blocker

The Story 12.5 termination boundary remains authoritative. Production composition
requires the pinned JDT-LS distribution archive and checksum, but the host retained
only an extracted installation. A bounded historical experiment showed that the
extracted Java 21/JDT-LS process could start; the neutral Maven fixture then activated
Maven import and failed on unavailable plugin resolution. Treating the workspace as
unmanaged preserved `documentSymbol` and local definition behavior but not complete
Find References behavior.

Supporting extracted-installation deployment while limiting JDT-LS to source
navigation still requires an architecture and Scan-strategy decision covering
build-system import, offline project/classpath authority, completeness and fail-closed
semantics. No such correction is included here. T048 production-composed real JDT-LS
qualification remains unaccepted, and later Story 12.5 release tasks remain closed.

## No-mission proof

This integration performed source/test/document edits, one local bounded test build,
and Git read-only checks only. It did not:

- start a Vulgata service or bind a network port;
- initiate or resume a Scan, Pass 1, Cross-Repository Resolution or Pass 2;
- call a local or remote LLM/provider;
- create, clone, query, migrate or delete a database;
- start Docker, CodeGraph, JDT-LS or any external analysis runtime;
- collect screenshots or create release evidence;
- modify the backup worktree.
