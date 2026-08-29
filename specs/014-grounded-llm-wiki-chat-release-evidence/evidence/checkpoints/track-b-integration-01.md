# Story 12.5 Track B Integration Checkpoint 01

- Observed at: `2026-08-29T09:37:51.8816828Z`
- Source commit: `65ffe6986217849105b7a4493182ff835932f0a8`
- Branch: `demo0831/story-12-5`
- Checkpoint ID: `bmad-12-5-implement-20260829T093751Z-bint`
- Tasks: T033-T037
- Accepted dependency: Story 12.4 commit `4798b97d9f23aef587829175df7ab7ebc3daf995`

## Bound independent evidence

The ordered B1-B4/B-INT evidence-set SHA-256 is `f887461b59fc9c498263c857181689f5c7a27e92462e65d4a024610d76be5f1f`. It is SHA-256 over UTF-8 lines `path=lowercase-file-sha256` in this order:

| Boundary | Path | SHA-256 |
|---|---|---|
| B1 | `evidence/checkpoints/track-b-b1-reacceptance-01.md` | `475ad6061a677705e9825e6b9379f08cc8c6c0aec75a99173829b4324e60f139` |
| B2 | `evidence/checkpoints/track-b-b2-reacceptance-01.md` | `4056a0f6a4f55bf7630e05df24510357613d47dd4b0478dddab4432d84754070` |
| B3 | `evidence/checkpoints/track-b-b3-reacceptance-01.md` | `9284a2c28fd5738ecb836fdfc3bccf9684e351fed11315fda53928bc0c62aa2c` |
| B4 | `evidence/checkpoints/track-b-b4-implementation-01.md` | `bdba3e0ab2f1e1539274dd4a4b749aa01a435aead81d801c76debfe44cf52c33` |
| B-INT | `evidence/integration/track-b-int-01.md` | `d618d7f1c0f26fd4236d03792ba4dea37a18cf3b5837553d2e3e81904d478b70` |

The pre-evidence implementation/test working diff Git object hash is `39f2fd91012e8e7f4015c293cf4b42b1ff38085e`. Exact B-INT candidate file SHA-256 values are:

| Path | SHA-256 |
|---|---|
| `src/dotnet/Vulgata.Web/Program.cs` | `e9be44da3a4e5f4ed23278900867a584cfc87be755600f4f7bba2e6345ce1f1d` |
| `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` | `5dcded60d5ae30cd005458423d9c13ca2970777abe51f6fa1ac44d2379fa81de` |
| `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` | `5c0ea060b40e97260e98612817f4981d7fafefe666179d3d5b0a73c068e3d70c` |
| `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` | `4288a7fffbc0749b493591a307ba03435c4d70fb503f818e2c45f860b9ac364e` |
| `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs` | `20904e4313dba45a636b36fc813ce10014e01a9c7a5072e390e6bcf615e2a7d5` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs` | `8c8f06376bce2f5d5934e7c78aa4d073a0e73606c250f530d4dc803ec7b22d6f` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs` | `637b726a84022a5a69121fb4dd90cbe39e460e9f619dfe937a4079c2a66e04b6` |

## T033-T035 RED/GREEN

- T033 authenticated Business initial/follow-up executed exactly two lexical projections, relationship projections, packet builds, raw provider attempts, currentness checks, final authorization checks, and publication commits, plus exactly one follow-up Conversation State re-fetch. Its composition RED failed because `IPreparedChatPublicationOwner` was not registered.
- The same named interaction-test owner completed T034 only after T033. Authenticated IT initial/follow-up passed the same topology. Lexical-primary replacement and relationship-skip mutations rejected; follow-up re-fetch is observed exactly once; production dispatcher composition is bound to `OpenAiCompatibleChatProviderStreamingTransport`; no alternate live Conversation State owner or Track C sink is composed.
- Two initial hostile-double assertions were corrected before GREEN because they demanded that the coordinator introspect behavior hidden inside a malicious retriever/dispatcher double. The accepted mutation guards instead observe the named port invocation and exact production raw-adapter composition.
- T035 changed only `Program.cs`: one scoped `IPreparedChatPublicationOwner -> PreparedChatPublicationOwner` registration and one explicit `PreparedChatCoordinator` factory bind the exact same scoped publication owner and completion-observation recorder. Agents DI already supplied the accepted recorder and typed raw streaming transport; it was not changed.
- Focused T033/T034 transitioned from **7 passed / 1 honest RED** to **8/8 passed**. Full `PreparedChatIntegrationTests` plus `ChatPipelineCompositionTests` passed **21/21**.

## T036 verification

- Focused B1-B4+B-INT: **409/409 passed** after excluding exactly two default Data Protection characterizations that cannot write the sandbox-external user key directory. Both controlled-key-ring production vectors pass.
- Story 12.5 Scan status/UI/corpus/evidence: **103/103 passed**.
- Web build: **0 errors**, with 19 unchanged NU1507/NU1900/MSB3277 warnings.
- `git diff --check`: clean apart from informational LF-to-CRLF notices.
- Static guards: one packet builder; one publication-owner implementation; one scoped owner registration; no legacy exact Chat authority; no Track C sink/release fields; no uncommitted EF/migration/snapshot/Story 12.4 path; no live `ChatConversationState` field; one candidate-only `AppendValidatedTurn`; one follow-up read from the committed composite.

The diagnostic repository-wide run was not used as acceptance evidence: it recorded 2,451 passes, 275 failures, and 4 skips across unrelated environment-gated PostgreSQL/provider/CodeGraph/JDT/Data Protection tests and a pre-existing Pass 1 schema-identity baseline. No Tier 2/3 prerequisite was fabricated to make that run green.

## Checkpoint verdict

**Track B implementation is checkpoint-ready.** B1, B2, B3, B4, and B-INT are independently bound and the exact production topology is green. This checkpoint proposes implementation completion only; it does not claim convergence, quality-review acceptance, an E2E decision, final Track B verification, Track C readiness, or Story 12.5 completion. T038 convergence is the next mandatory gate.
