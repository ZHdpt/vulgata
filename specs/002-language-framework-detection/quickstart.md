# Quickstart: Validate Language and Framework Detection Remediation

This is a validation guide, not implementation code. It starts from the
iteration-2 remediation planning boundary and does not claim that an earlier
implementation or review boundary remains accepted.

## Prerequisites

- .NET 10 SDK
- Docker for the mandatory exact final-image gate
- PostgreSQL 17 for application smoke validation; SQLite is used by the test
  harness
- exact `docker/codegraph/requirements.lock`
- a fresh independently accepted plan, regenerated tasks, and the new
  `AGENTS.md` planned-change authorization
- one clean immutable correction candidate and one unused accepted-correction
  namespace

## 1. Verify the corrected planning boundary

```pwsh
$PlanningSourceCommit = 'eefa1794a57227428376a6d838593485e491a79b'

git merge-base --is-ancestor $PlanningSourceCommit HEAD
if ($LASTEXITCODE -ne 0) {
  throw "Iteration-2 remediation planning source is not an ancestor of HEAD."
}

Get-FileHash `
  specs/002-language-framework-detection/spec.md,`
  specs/002-language-framework-detection/plan.md,`
  specs/002-language-framework-detection/research.md,`
  specs/002-language-framework-detection/data-model.md,`
  specs/002-language-framework-detection/contracts/framework-detection.md,`
  specs/002-language-framework-detection/contracts/provider-language-projection.md,`
  specs/002-language-framework-detection/contracts/repository-scan-overview.md,`
  specs/001-codegraph-extraction/contracts/repository-profiling-api.md `
  -Algorithm SHA256
```

Expected corrected content hashes. `quickstart.md` is intentionally omitted
from its own embedded list to avoid a self-referential hash; record its final
SHA-256 in the external plan-review boundary:

```text
924d9d5d1f104e4206f145f565eb7f3e8c813c67fdd88da2abf55cb08456f956  specs/002-language-framework-detection/spec.md
1ff1d9cdcc715826229bcbf2981f18d221f3d067a0d03de19f1e79b50d0f18df  specs/002-language-framework-detection/plan.md
306d52e498537b4fe9c4ed4478731dbac9569f34df0aebf8f9291337ca41dec9  specs/002-language-framework-detection/research.md
76130d3cd76e029e0e112041be05b4822dcd15e430f38ff3502b53a228b3288c  specs/002-language-framework-detection/data-model.md
74f71a218adca654b79d24bd1558f585b9ae7ff7ec95ca5370f5d153d7826181  specs/002-language-framework-detection/contracts/framework-detection.md
b5503794a0b05076b441f684b50948b419bcfdd7d21736f0f5430de7eaf04d9b  specs/002-language-framework-detection/contracts/provider-language-projection.md
b3b7d0c3cfe6a236d4b25b54239cf207fe546d2518956afb9d54abdae7c94dad  specs/002-language-framework-detection/contracts/repository-scan-overview.md
ca2086166c91820e48f3e5869374ffa1c5fcb8fdddf20480e402194856238124  specs/001-codegraph-extraction/contracts/repository-profiling-api.md
```

`tasks.md`, workflow state, and BMad lifecycle artifacts are intentionally not
accepted by these hashes. They remain downstream of fresh plan review and task
regeneration.

## 2. Observe iteration-2 RED before corrections

Add focused runnable tests before changing production behavior. Preserve raw
output for each new RED and GREEN run. The RED assertions must prove:

- an absolute/rooted/escape provider path can reach a captured log before
  normalization;
- a NuGet lock can pair to one tracked but malformed, unsupported,
  conditional/dynamic, unsafe, or inapplicable `.csproj`;
- an npm lock can pair to one tracked but malformed, unsupported, unsafe, or
  inapplicable `package.json`;
- the Python runner and C# verifier accept field names that differ from the
  owned Story 4.1 v2 evidence schema;
- a pre-correction or unlabelled image cannot prove the exact source candidate
  that produced it.

Do not accept a compile failure, broken fixture, fake historical output, or a
current rerun relabelled as an earlier RED.

## 3. Provider path fail-closed gate

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~CodeGraphContextProvider|FullyQualifiedName~RepositoryProfiling"
```

Required GREEN:

- raw provider path scalars are normalized within the parser stack frame;
- only a bounded safe Repository-relative path or `(unknown)` can construct an
  owned provider outcome;
- the coordinator revalidates before its first log call and uses the same safe
  value for log, audit key, persistence, response, and UI;
- captured logs, exceptions, durable state, API output, and rendered output
  contain no raw Windows drive, UNC, POSIX-rooted, escape, or host-path
  fragment;
- provider atomicity, two-query behavior, and local candidate audit remain
  unchanged.

## 4. Lock owner eligibility gate

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~FrameworkDetection"
```

Required GREEN for both NuGet and npm:

- the complete same-root direct-manifest owner-candidate set is evaluated
  before lock entries;
- exactly one candidate exists and it is safely read, successfully parsed,
  supported, and applicable;
- a unique valid owner may justify a catalog-matched transitive lock fact even
  when the owner has no direct catalog match;
- zero/multiple owners or a malformed, unsupported, conditional/dynamic,
  unsafe, oversize, or inapplicable owner yields no `Medium` fact and one
  bounded closed audit outcome;
- an invalid candidate is not ignored to manufacture a unique owner;
- direct `High` still beats eligible lock `Medium`, and no build, restore,
  install, script, network, external package lookup, or LLM runs.

## 5. Current migrations and persistence

The current schema authority is exactly:

```text
20260720030657_AddLanguageFrameworkDetection
20260720060442_FrameworkEvidenceAuditCount
```

Run:

```pwsh
dotnet ef migrations script `
  --project src/dotnet/Vulgata.Infrastructure `
  --startup-project src/dotnet/Vulgata.Web `
  --context VulgataDbContext

dotnet ef migrations has-pending-model-changes `
  --project src/dotnet/Vulgata.Infrastructure `
  --startup-project src/dotnet/Vulgata.Web `
  --context VulgataDbContext
```

Required result:

- `20260720030657_AddLanguageFrameworkDetection` adds only
  `CodeUnitFrameworks`, `ScanRunLanguageSummaries`, and their accepted
  constraints/indexes;
- `20260720060442_FrameworkEvidenceAuditCount` adds only the non-negative
  framework-audit total column/check;
- rollback reverses the audit-count check/column first, then the two Story 4.2
  tables/indexes;
- no guessed language backfill, historical rewrite, or third iteration-2
  migration exists;
- the current model has no pending changes.

## 6. Exact source-to-image and closed-schema gate

At the immutable correction candidate:

```pwsh
$CandidateCommit = (git rev-parse HEAD).Trim()
$CandidateTree = (git rev-parse "$CandidateCommit^{tree}").Trim()
$WorktreeStatus = @(git status --porcelain)
if ($WorktreeStatus.Count -ne 0) {
  throw "Final-image build requires a clean immutable candidate."
}
```

The verifier must then:

1. create canonical `production-source-manifest-v1` from the Dockerfile,
   `.dockerignore`, and every tracked Docker-context file not excluded by
   `.dockerignore`, sorted by Repository-relative path with SHA-256;
2. build a fresh image from that exact context with labels binding
   `$CandidateCommit`, `$CandidateTree`, and the source-manifest SHA-256;
3. inspect and compare those labels before provider execution;
4. consume a new correction namespace and bind source identities, image ID,
   provider 0.5.1 lock/distribution, canonical query hashes, and verifier base
   image in the claim;
5. run the Python observer once and validate only the exact Story 4.1
   `cgc-provider-contract-evidence-v2` schema in C#.

Run the mandatory gate:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName=Vulgata.Tests.CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract"
```

Required GREEN:

- canonical root names are `schemaVersion`, `primaryFailureCategory`, and
  `projectionObservations`; alternate runner-era names fail;
- the three retained row counts appear exactly once at
  `coverage.rowCounts.projectionA`, `.projectionB`, and `.combined`, with the
  contract's integer bounds and exact-sum/pass-cap invariants;
- the five non-projection completeness facts appear exactly once under
  `coverage.completeness`; the two projection truncation-absence facts are
  represented only by the stricter
  `projectionObservations.A.shapeComplete` and `.B.shapeComplete`;
- root `rowCounts`, root `completeness`, the two old
  `projection*TruncationMetadataAbsent` names, duplicate
  `coverage.nodeKinds`, and every unknown/duplicate/missing/wrong-typed nested
  field fail;
- the safe-name set exactly includes `parser_language`, `truncated`, and
  `result_limit`, while completeness-field presence still forces failure;
- claim, inspected image labels, v2 evidence, final image ID, and verifier base
  all bind the same candidate/source manifest;
- both projections return more than 100 complete rows, the empty supported file
  produces one `Filtered/UnknownLanguage`, cleanup is complete, and all
  forbidden-retention fields are false;
- no prior image, evidence hash, namespace, or stale accepted-boundary claim is
  reused.

## 7. Story 4.2 historical reconstruction and T050

Regenerated tasks must create
`specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json`.
For every prior task group, inspect immutable Git history and existing workflow
records and record only what they prove:

```pwsh
git log --reverse --format="%H %P %s%n%b" `
  e9824df7300634c19d815bdd7ad591b9fb5366d5..HEAD
git diff --name-status `
  e9824df7300634c19d815bdd7ad591b9fb5366d5..HEAD
```

The ledger must identify earliest provable test/implementation commits,
existing checkpoint trailers, current candidate-bound GREEN evidence, and
explicit `historicalRawRedAvailable` /
`checkpointTrailerAvailable` booleans. `false` is valid under the approved
Story 4.2 exception. Fabricated output, backdating, commit amendment, inferred
RED claims, or relabelling current GREEN as historical evidence is a blocker.

T050 remains `[ ]` until all iteration-2 corrections, fresh RED/GREEN, source-
bound final image, full current gates, scope audit, and truthful reconstruction
pass against one immutable candidate. This guide does not authorize editing
`tasks.md`, workflow state, BMad lifecycle state, or checkpoints during
planning.

## 8. Final current-candidate gates

Run all commands against the same clean candidate used for the fresh image:

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
dotnet ef migrations has-pending-model-changes `
  --project src/dotnet/Vulgata.Infrastructure `
  --startup-project src/dotnet/Vulgata.Web `
  --context VulgataDbContext
git diff --check
git status --short
```

Before task regeneration, fresh independent plan review must accept the entire
corrected plan set. Because validated lock ownership materially changes Scan
evidence filtering, the orchestrator must then obtain a new explicit
planned-change authorization. The iteration-2 remediation approval does not
substitute for that post-review gate.
