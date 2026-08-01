# Quickstart Validation: Demo Document Generation

## Prerequisites

- Checkout `codex/story-11-3` at the accepted implementation candidate.
- .NET 10 SDK.
- Existing test PostgreSQL prerequisites where the knowledge-store concurrency
  harness requests them; no live LLM provider or real credential.
- The embedded Pass 1/Pass 2 prompts and single prepared-flow manifest.

## Validation order

Run focused contract/security/lifecycle tests first (use the final test class
names created from the groups in `plan.md`):

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~WorkerAgent|FullyQualifiedName~PreparedPass2|FullyQualifiedName~SequentialScanRunner"
```

Expected: closed union/schema/bounds, exact snapshot hydration, zero-provider-
call failures, prompt/resource discovery, provider assignment, commit-before-
progress, exact Pass 2 provenance, and F003 SQLite reservation tests pass.

Build the production composition and run the full non-final-image suite:

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
```

Expected: zero build/test failures; production DI resolves the sequential
runner, production dispatcher, committer, existing source/provider/store seams;
no migration or model-snapshot diff exists.

## Current-boundary integrated scenario

Use a temporary Git Repository with a known commit plus a changed working copy,
exact prepared Code Unit hashes/ranges, authorized Repository scope, encrypted
test provider configuration, the loopback OpenAI-compatible fixture, the one
embedded manifest, and relational storage.

Prove:

1. only the exact committed-revision slices reach the loopback provider;
2. each recorded-valid Pass 1 job has one prior complete immutable Code Logic
   aggregate and retrieval projection;
3. `Pass1Completed` is observable before exactly one Pass 2 dispatch;
4. Pass 2 contains the exact current Code Logic Document Version set;
5. `Completed` occurs only after the Business Logic commit is observable;
6. `GENERATED_FROM` is set-equal and no narrative-created base `REFERENCES`
   Edge exists;
7. invalid/unsafe/stale/unauthorized/conflict/rejection/cancellation cases yield
   bounded Chinese failure, zero affected knowledge rows, and zero valid
   progress;
8. captured HTTP/logs/exceptions/durable rows contain no plaintext credential,
   raw diagnostic, unrelated source, raw-source carrier, or absolute host path.

The final clean-database two-Repository rehearsal and final Docker image build
belong to Story 11.5. Do not claim them here.

## Scope/metadata checks

```pwsh
git diff --name-only
rg -n "NEEDS CLARIFICATION|\[FEATURE\]|\[DATE\]|\[###|TODO|TBD" specs/006-demo-document-generation/plan.md specs/006-demo-document-generation/research.md specs/006-demo-document-generation/data-model.md specs/006-demo-document-generation/contracts specs/006-demo-document-generation/quickstart.md
```

Expected: no placeholder hit; implementation later changes no public API/UI,
Chat Agent tool, migration/model snapshot, retry/failover, concurrent scheduler,
or general flow planner. After independent plan-review acceptance, stop for the
two planned-change approvals recorded in `plan.md` before generating tasks.
