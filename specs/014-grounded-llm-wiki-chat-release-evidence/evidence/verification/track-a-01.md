# Story 12.5 Track A verification

## Boundary

- Candidate product boundary: `1eba1f85f32177b3e288fc20a9f5feb6e60170af`.
- Independent quality verdict: `ACCEPT` in `reviews/quality-review/track-a-quality-review-04.json`.
- Track A only: dependency-independent UI, evaluation fixtures, question corpus, and release-evidence preparation.
- Tracks B and C remain closed pending the accepted Story 12.4 final checkpoint and integration of its final commit. Streaming LLM chat remains mandatory for Track B.
- No Story 12.4 worktree, database, or temporary-directory content was accessed or changed. No EF migration, `VulgataDbContext`, or model snapshot change is present.

## Focused verification

| Evidence | Result |
|---|---:|
| `Vulgata.Tests.ReleaseGate.Story125` | 75 passed |
| `Vulgata.Tests.ProductionScanStatusReaderTests` | 22 passed |
| `Vulgata.Tests.ProductionScanPageTests` | 7 passed |
| `Vulgata.Tests.DurableScanning.ManualResumeApiTests` | 13 passed |
| Total Track A checks | 117 passed, 0 failed |

The web build passed with 0 errors and 19 pre-existing warnings (package-source/audit availability and EF assembly-version conflict warnings).

## Conditional E2E

The independent E2E decision is `skipped-covered`. Existing component, WebApplicationFactory, SQLite persistence, manual-resume, composition, and release-gate tests cover every observable Track A predicate. The decision is recorded in `e2e-decision-track-a.json`. Track B streaming/retrieval and Track C real-LLM restart, screenshot, and release execution are deliberately excluded.

## Repository-wide diagnostic run

Command:

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --nologo --verbosity minimal
```

Result: 2,324 passed, 509 failed, 51 skipped, 2,884 total in 5 minutes 8 seconds.

The failures are outside the accepted Track A boundary and cluster into existing prerequisites/environment conditions: absent `VULGATA_TEST_POSTGRES_ADMIN`, Story 12.1/12.4 authoritative schema-identity drift, missing CodeGraph qualification image configuration, and pre-existing composition/UI assertions. The run rewrote two qualification inspection artifacts; both were restored exactly to `HEAD` and are not part of this checkpoint. Track A focused verification remains green.

## Residual finding

The accepted quality review records one nonblocking performance observation: the bounded management history reader uses an N+1 query shape. It does not affect correctness or the current bounded UI, but is a future optimization candidate.
