# Story 12.5 Track B quality correction 01

- Checkpoint ID: `bmad-12-5-implement-20260829T032456Z-quality-correction-01`
- Starting commit: `5c9b8b9584a8689a81f1ffdadf85192bd299c600`
- Source verdict: `track-b-quality-review-01.json`
- Scope: T076-T078 only

## Corrected findings

- QR-TB-001 / T076: reveal authority now compares the server-owned transaction packet's advisory text and canonical authority fingerprint to the reveal reread. The A-to-B-to-A race is withheld without Answer, citation, or conversation append.
- QR-TB-002 / T077: one caller-linked 29-second timer created from injected `TimeProvider` governs validation, authorization, scope, advisory, retrieval, dispatch, currentness, reveal, final authorization, answer construction, append, and publication checks. Internal expiry maps to timeout; caller cancellation remains cancelled.
- QR-TB-003 / T078: only locally classified invalid JSON syntax may receive the one repair stream. Repair binds closed category `invalid-json-syntax` and at most 2,048 characters of invalid response. Valid-JSON semantic, handle, fingerprint, evidence, and citation-invalid outputs do not repair.

## TDD and verification

- T076-T077 RED: **0/4**; GREEN: **4/4**.
- Prepared Chat coordinator: **43/43**; Chat page: **27/27**; caller cancellation GREEN.
- T078 focused GREEN: **4/4**; dispatcher: **26/26**; assembler: **31/31**; streaming transport: **16/16**.
- Root combined verification: **143 passed, 0 failed, 0 skipped**.
- Web build: **0 errors** with 19 existing warnings.
- `git diff --check`: clean; informational line-ending notices only.

No EF/model/migration, project, central package, Story 12.4 contract, or Track C change is included. Convergence and independent quality review are invalidated and must rerun before E2E or Track C.

## Explicit owned-path manifest

```text
specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md
specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-quality-correction-01.md
src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs
src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs
tests/Vulgata.Tests/ChatAgentDispatcherTests.cs
tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs
```
