# Story 12.5 Track B convergence correction checkpoint 13

- Checkpoint ID: `bmad-12-5-implement-20260829T025303Z-t074t075`
- Starting commit: `a67d27900d3971c07e33ee2329d774b5d1ce2206`
- Approval: `story-12-5-convergence-continuation-03`
- Continuation failure counter: **1 of 3**
- Scope: Track B convergence tasks T074-T075 only

## Delivered boundary

- T074 constructor-composes `IChatAdvisoryContextReader` in the retriever, reads the applied Context once after authorization inside the serializable transaction, maps closed results, and binds only the returned snapshot into lexical fallback and packet authority. Caller-carried advisory Context is ignored.
- T075 bounds exact-decision and provider-contribution loading independently per included Cross-Repository Edge. Each Edge probes at most 257 decisions and 65,537 contributions, rejecting an over-bound Edge while preserving per-Edge canonical fingerprints and decision-owned Flow Groups.

## TDD and verification

- T074 RED: **0/4** at the missing constructor-composed reader seam; GREEN T074 plus IT advisory fallback: **5/5**.
- T075 RED: two Edges with 128+129 decisions returned missing/stale; GREEN exact boundary: **2/2**, including continued refusal of 257 decisions on one Edge.
- Composition: **8/8**; retrieval: **51/51**; currentness: **49/49**.
- Root combined verification: **108 passed, 0 failed, 0 skipped**.
- Web build: **0 errors** with 19 existing warnings.
- `git diff --check`: clean; informational line-ending notices only.

No EF/model/migration, project, central package, Story 12.4 contract, or Track C change is included. The next fresh convergence pass is failure-counter pass 2 only if it remains non-converged.

## Explicit owned-path manifest

```text
specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md
specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-convergence-correction-13.md
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs
tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs
tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs
tests/Vulgata.Tests/ChatPipelineCompositionTests.cs
```
