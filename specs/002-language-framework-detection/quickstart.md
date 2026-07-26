# Quickstart: Validate Language and Framework Detection Remediation

This is a validation guide, not implementation code. It starts from the
evidence-contract correction boundary and does not claim that an earlier
implementation, plan review, T001 observation, source manifest, image, or
review boundary remains accepted.

## Prerequisites

- .NET 10 SDK
- Docker for the mandatory exact final-image gate
- PostgreSQL 17 for application smoke validation; SQLite is used by the test
  harness
- exact `docker/codegraph/requirements.lock`
- a fresh independently accepted plan and regenerated tasks
- the existing
  `specs/002-language-framework-detection/evidence/planned-change-authorization-2.json`,
  verified as authorization for the unchanged lock-owner filtering design;
  this evidence-only correction introduces no new planned-change trigger
- one clean immutable correction candidate and one unused accepted-correction
  namespace

## 1. Verify the corrected planning boundary

The plan judge must pin the complete corrected plan set. The task author copies
that exact accepted checkpoint, verdict path, diff identity, and artifact-hash
map into regenerated `tasks.md`; T001 must not guess or select a verdict by file
timestamp.

For this uncommitted correction, the fresh verdict uses the pinned base commit,
`candidate_commit: null`, the exact binary diff SHA-256, reviewed artifact
hashes, and the three owned paths. The root then creates the plan-accepted
checkpoint with a stable `Workflow-Checkpoint` ID and a null commit SHA/pending
binding. After commit, resolve exactly one matching trailer and use that
trailer-derived commit; never precompute or embed the commit in the checkpoint
that will contain itself.

```pwsh
$CorrectionSourceCommit = 'cfc65b7dc67bf84102fb16bef45f3f62b6d616b0'

git merge-base --is-ancestor $CorrectionSourceCommit HEAD
if ($LASTEXITCODE -ne 0) {
  throw "Evidence-contract correction source is not an ancestor of HEAD."
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

Every computed hash must equal the hash in the fresh accepted plan-review
boundary. `quickstart.md` is intentionally omitted from its own embedded
command to avoid self-reference; the plan-review boundary must hash it
externally. `tasks.md`, workflow state, BMad lifecycle artifacts, and all prior
plan verdicts remain downstream or invalidated and are not accepted by the old
hash list.

## 2. Record corrected T001 as current verification

The pre-correction baseline observation required by the superseded T001 was not
captured before T002-T049. It is temporally unavailable. Do not run these
commands and describe them as historical, pre-correction, RED, or evidence that
T001 occurred in the old order.

After fresh plan acceptance and task regeneration, pin one clean immutable
commit containing the accepted plan set and regenerated tasks:

```pwsh
$CurrentVerificationCommit = (git rev-parse HEAD).Trim()
$CurrentVerificationTree = (git rev-parse "$CurrentVerificationCommit^{tree}").Trim()
$CurrentVerificationObservedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')

if (@(git status --porcelain=v1 -uall).Count -ne 0) {
  throw "Current T001 verification requires a clean immutable candidate."
}

# Use the exact accepted checkpoint recorded by the fresh plan verdict/tasks.
git merge-base --is-ancestor $AcceptedPlanCheckpoint $CurrentVerificationCommit
if ($LASTEXITCODE -ne 0) {
  throw "Accepted corrected plan checkpoint is not an ancestor."
}

dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~CodeGraphContextProvider|FullyQualifiedName~RepositoryProfiling"
if ($LASTEXITCODE -ne 0) { throw "Current provider/profiling verification failed." }

dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~FrameworkDetection"
if ($LASTEXITCODE -ne 0) { throw "Current framework verification failed." }
```

`$AcceptedPlanCheckpoint` is not inferred: the root supplies the exact immutable
checkpoint from the fresh accepted plan verdict and regenerated tasks. Preserve
the actual commit, tree, UTC observation time, commands, exit codes, raw-output
paths, accepted plan-review `ACCEPT`, accepted artifact hashes, and verified
`planned-change-authorization-2.json` in one T001 record labelled **“Current
verification”**. In the same record, preserve the exact separate statement
**“Pre-correction T001 baseline observation: unavailable”**.

## 3. Refresh executable accepted-plan bindings

Do not update the stale constants first. Preserve focused RED evidence:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~CodeGraphProviderContractTests.HashBoundFile_WindowsCheckout_PreservesCanonicalLfBytes"
```

The actual RED must identify the changed `plan.md`, `research.md`, and
`quickstart.md` hashes. Add a focused assertion to
`AcceptedCorrectionManifest_IsCanonicalAndArtifactBound` that rejects
`user-approved-story-4-2-plan-2026-07-20` and requires the exact fresh accepted
plan verdict/checkpoint/diff identity, then run it before changing
`_reviewIdentity` or `CreateAcceptedCorrectionManifest`. It must fail for the
stale claim shape; a compile failure is not RED.

For GREEN, update exactly the three Story 4.2 `CanonicalLfFiles` hash values
from the accepted verdict's reviewed artifact map. Replace the static
`reviewIdentity` claim with these explicit fields:

```text
planReviewVerdictPath
planReviewVerdictSha256
planReviewDiffSha256
planCheckpointId
planCheckpointCommit
```

The focused tests must parse the named verdict and require `plan-review`,
`ACCEPT`, the exact reviewed artifact hashes/diff, one matching
`Workflow-Checkpoint` trailer, its trailer-resolved commit as an ancestor of
the current candidate, and absence of the old static identity. They must reject
an alternate/stale verdict, hash or diff drift, duplicate/missing checkpoint
trailers, and ancestry failure.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~CodeGraphProviderContractTests.HashBoundFile_WindowsCheckout_PreservesCanonicalLfBytes|FullyQualifiedName~CodeGraphProviderContractTests.AcceptedCorrectionManifest_IsCanonicalAndArtifactBound"

dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~CodeGraphProviderContractTests&FullyQualifiedName!~FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract"
```

Record separate RED/GREEN commands, exit codes, immutable sources, assertions,
and raw-output paths. This tracked test/evidence-contract change invalidates
the current reconstruction ledger, both Full implementation reviews, E2E
decision/result, verification, candidate manifest/image/namespace, T049, and
T050.

## 4. Preserve iteration-2 RED/GREEN evidence

The already completed iteration-2 corrections retain their immutable runnable
RED/GREEN evidence. Regenerated tasks and the reconstruction ledger must verify
the evidence paths and hashes; they must not recreate, backdate, or relabel
them. The RED assertions prove:

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

## 5. Provider path fail-closed gate

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

## 6. Lock owner eligibility gate

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

## 7. Current migrations and persistence

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

## 8. Exact source-to-image and closed-schema gate

At the immutable correction candidate:

```pwsh
$CandidateCommit = (git rev-parse HEAD).Trim()
$CandidateTree = (git rev-parse "$CandidateCommit^{tree}").Trim()
$WorktreeStatus = @(git status --porcelain)
if ($WorktreeStatus.Count -ne 0) {
  throw "Final-image build requires a clean immutable candidate."
}
```

The prior manifest
`d928b4e1a3dd49a2a482095968bce80903262be041097c32a115f222756a31e7`
and image
`sha256:f9983d6bf74f298b2e1d654085df4da73824834b4a73cadb631c535645eecedb`
are invalid for the corrected plan boundary. `.dockerignore` does not exclude
`specs/`; `docker/Dockerfile` copies the remaining full context; and the
executable manifest contract hashes every tracked non-ignored context file.
Therefore the edited plan, research, quickstart, and later tracked
accepted-plan binding test/evidence bytes require a new committed candidate,
manifest, image labels/ID, and correction namespace even though product code
is unchanged.

The verifier must then:

1. create canonical `production-source-manifest-v1` from the Dockerfile,
   `.dockerignore`, and every tracked Docker-context file not excluded by
   `.dockerignore`, sorted by Repository-relative path with SHA-256;
2. build a fresh image from that exact context with labels binding
   `$CandidateCommit`, `$CandidateTree`, and the source-manifest SHA-256;
3. inspect and compare those labels before provider execution;
4. consume a new correction namespace and bind source identities, image ID,
   provider 0.5.1 lock/distribution, canonical query hashes, verifier base
   image, and the exact fresh plan verdict/checkpoint/diff identity in the
   claim;
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
- the accepted-correction claim names the exact fresh `ACCEPT` verdict path and
  content hash, its reviewed diff SHA-256, the stable plan checkpoint ID, and
  its unique trailer-resolved ancestor commit; the static 2026-07-20 identity
  is absent;
- both projections return more than 100 complete rows, the empty supported file
  produces one `Filtered/UnknownLanguage`, cleanup is complete, and all
  forbidden-retention fields are false;
- no prior image, evidence hash, namespace, or stale accepted-boundary claim is
  reused.

## 9. Story 4.2 historical reconstruction and T050

Regenerated tasks must regenerate and revalidate
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
The ledger must additionally record **“Pre-correction T001 baseline
observation: unavailable”** for superseded T001 and link the separately timed
**“Current verification”** record without merging their temporal meaning.

T050 remains `[ ]` until corrected T001, verified existing iteration-2
RED/GREEN, the regenerated ledger, source-bound fresh final image, full current
gates, scope audit, and a new T049 freeze pass against one immutable candidate.
This guide does not authorize editing `tasks.md`, workflow state, BMad lifecycle
state, or checkpoints during planning.

## 10. Final current-candidate gates

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
corrected plan set. Regenerated tasks must verify that
`planned-change-authorization-2.json` remains bound to the unchanged accepted
lock-owner filtering design. This evidence-only correction does not require a
new planned-change authorization. If fresh review finds an actual Worker
Agent/tool, Scan strategy, or PRD change, stop for the repository's authorization
gate instead of expanding this remediation.

Execution order after plan acceptance is mandatory: regenerate tasks, rerun
read-only analysis, observe focused stale-binding RED, implement binding GREEN,
rerun surrounding contract tests, pin the changed candidate, rerun both Full
implementation reviews, rerun the E2E decision/result, regenerate and validate
the reconstruction ledger, then build the new manifest/image/namespace and run
the exact final-image gate plus full suite. Only a subsequent unchanged T049
freeze may unblock T050.
