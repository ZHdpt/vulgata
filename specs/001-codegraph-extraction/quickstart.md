# Quickstart: Validate CodeGraph Code Unit Extraction

This guide validates the accepted production Story 4.1 boundary. It does not
authorize provider qualification retries, task generation, or implementation
outside an accepted plan.

## Prerequisites

- .NET 10 SDK.
- PostgreSQL/Docker as documented in `AGENTS.md`.
- exact-pinned CodeGraphContext `0.5.1` in the application image;
- local credential-free Git fixtures;
- no external network, LLM, MCP/tool route, Worker Agent, dispatch, Document,
  Uncertainty, or Cross-Repository Resolution dependency.

The provider gateway must bind only to unpublished `127.0.0.1`. The private
provider home, Context, and Kuzu database must be unique to the `ScanRun`.

Controlled fixtures prove parser and orchestration behavior only. They do not
prove the installed provider envelope or coordinate properties.

Current gate state is failed closed: HTTP 200/top-level object did not match
the candidate envelope, no property/type evidence was published, and
directory-only cleanup raised `NotADirectoryError` because `graph.kuzu` is a
regular file. Container and temporary image tags were removed. Do not rerun
until this observer/cleanup correction is independently accepted.

## Focused parser validation

Run the focused profiling tests with fresh compilation:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter RepositoryProfiling
```

Required scenarios:

1. A fixture contains an isolated Function, isolated Class, a File, and a
   Module. The Code Unit projection returns each valid node without requiring
   any relationship row.
2. A separate relationship projection returns `CONTAINS`, `IMPORTS`, `CALLS`,
   and `INHERITS`. Endpoint identities match Projection A identities.
3. A pathless Module remains a stable endpoint but is not persisted as a Code
   Unit; a relationship to it becomes one Unresolved Structural Relationship
   Candidate when the source exists.
4. A resolved relationship creates one Code Unit Edge and no target-null Edge.
5. Malformed individual node/relationship rows produce bounded warnings while
   valid independent rows remain.
6. Invalid UTF-8/JSON, query echo/count mismatch, collision, cap overflow, or
   failure of either projection returns no provider-derived structural result.

Expected owned identity format:

```text
cgc-v1:<64 lowercase SHA-256 hex characters>
```

## Production lifecycle validation

Use the controlled CodeGraph process fixture. Assert:

- one whole-Repository `cgc index`;
- one unpublished loopback gateway;
- bounded `GET /health`;
- exactly two read-only `POST /api/v1/query` calls, Code Units first and
  relationships second;
- no MCP/tool/index HTTP route;
- one 1,800-second deadline from index through both projections;
- cleanup of gateway, Context, private database, and provider home;
- no per-file provider process, retry, bisection, or continuation.

Inject failure at index, gateway readiness, each query, response parsing,
deadline, process exit, and cleanup. Every case must retain pre-provider
filtered audit rows and commit zero provider-derived Code Units, Code Unit
Edges, or Unresolved Structural Relationship Candidates.

## Mandatory exact-pinned real-provider contract gate

Build the final application image, then run only the exact provider-contract
test with fresh compilation:

```pwsh
New-Item -ItemType Directory -Force TestResults/codegraph-provider-contract | Out-Null
docker build --target runtime --iidfile TestResults/codegraph-provider-contract/final-image.iid --tag vulgata-story41-final:provider-contract -f docker/Dockerfile .
$env:VULGATA_PROVIDER_CONTRACT_IMAGE = "vulgata-story41-final:provider-contract"
$env:VULGATA_PROVIDER_CONTRACT_ACCEPTED_CORRECTION_SHA256 = "<fresh-accepted-iteration-4-sha256>"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName=Vulgata.Tests.CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract"
```

The test must create a verifier image whose `FROM` base resolves to that exact
final-image ID. It may add the already-built contract-test runner and
credential-free fixture, but it must install, replace, or resolve no provider
package. Before indexing, it proves the base-image identity and exact installed
`codegraphcontext==0.5.1` distribution/lock identity:

```text
requirements.lock SHA-256       10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95
codegraphcontext 0.5.1 SHA-256  3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658
```

The local fixture must require no credential or network and contain:

- isolated File, Module, Class, and Function nodes;
- observable `CONTAINS`, `IMPORTS`, `CALLS`, and `INHERITS` relationships;
- endpoints shared across Projection A and B for `cgc-v1` equality;
- one locally filtered target whose provider relationship maps to exactly one
  Unresolved Structural Relationship Candidate and no Code Unit Edge.

The runner must consume the same immutable Projection A/B constants as
production. It verifies their canonical hashes before dispatch, executes each
exact query against the one immutable 0.5.1 Context, and records only the
bounded evidence schema in
`TestResults/codegraph-provider-contract/<AcceptedCorrectionSha256>/evidence.json`.
It must not persist
source, fixture paths, filenames, symbols, rows, raw response/query text,
credentials, tokens, or diagnostics. Evidence must be closed-schema and no
larger than 16 KiB.

Expected canonical UTF-8/LF/no-trailing-LF query hashes:

```text
Projection A  07b13dcb0b95a2ab26da930e9fcbafef7cde27488df99b0634d860ef0767d9a8
Projection B  40d2da3b9b520b3c4eca3fd7c3bad4f4fb10643736e3a9e9b4ddf62c92adc096

These are single heterogeneous-match projections with closed label
allowlists; they do not use the pinned Kuzu UNION form that stalled.
```

This gate passes only when:

- the test result is Passed—not skipped or merely discovered;
- the final/verifier image IDs and exact package pin agree;
- the observed real envelope and only the coordinate properties successfully
  evaluated by 0.5.1 match the production parser grammar;
- both exact query hashes, Projection B request/echo hash, isolated-node and
  four-kind coverage, filtered-target behavior, `cgc-v1` equality, immutable
  snapshot, redaction, and cleanup predicates pass;
- the evidence artifact exists, validates, and has `verdict = "pass"`.

Any mismatch fails closed before Story completion. Correct the owned
grammar/query/parser together and rerun this exact gate after review; do not
accept speculative aliases and do not invoke, modify, or retry attempt-0003.

### Failure evidence, cleanup, and rerun checks

`cgc-provider-contract-evidence-v2` must be atomically published for pass or
fail. On strict-parser failure it may contain only HTTP status, byte
count/hash, JSON value kinds, raw allowlisted names or unknown-name HMAC
metadata, structural counts,
query/image/package hashes, cleanup facts, and failure category. Assert no
scalar value, path, filename, symbol, row, raw JSON/query/source, credential,
token, endpoint value, error text, stdout/stderr, or diagnostic is retained.

Enforce: 16 KiB; depth 4; 32 containers; 128 property observations; 32 array
summaries; 64 sorted aliases of at most 64 UTF-8 bytes; 32 coordinate
bindings. Overflow publishes fail with
`observation-bound-exceeded` and `shapeComplete=false`.

After Context deletion, inspect generated targets under the private root
without following links: absent is success; regular file (`graph.kuzu`) is
unlinked; directory is recursively removed without link traversal;
link/reparse/special entries fail cleanup. Preserve primary and cleanup facts
separately.

Pass and fail use a unique same-directory temporary file, closed-schema
validation, durable flush, and atomic create-new/no-replace rename to
`TestResults/codegraph-provider-contract/<AcceptedCorrectionSha256>/evidence.json`.
Existing, partial, corrupt, missing, or over-bound evidence fails the gate.

After independent acceptance and implementation of this correction, permit
exactly one execution. There is no automatic retry. Failure publishes v2
evidence and keeps Story 4.1 blocked. Only an evidence-led grammar/query
correction with fresh independent acceptance permits one further execution.
Projection hashes remain unchanged unless evidence proves a query change is
necessary. Attempt-0003 remains untouched.

### Per-correction claim checks

Before image/provider reachability, validate the accepted-correction hash as
64 lowercase hex, atomically create its no-replace directory, and sync the
parent. Directory creation consumes the correction. Durable create-new
`claim.json` must bind execution ordinal 1, correction/query/image/package
hashes, and evidence schema; flush, no-replace rename, sync the directory,
reread, and validate before provider work.

Test two-run and multi-process races: exactly one directory creator may
continue and every loser exits before reachability. Inject crashes before and
after directory sync, claim temporary write, file flush, rename, directory
sync, provider start, cleanup, evidence temporary write/flush/rename, and final
directory sync. Every state after directory creation remains consumed; an
empty/partial/corrupt directory or missing evidence is failure and is never
deleted/repaired/resumed. A later accepted hash gets a new directory while all
old claim/evidence/temp history remains byte-unchanged.

### Secret-safe property-name checks

Raw names are permitted only for:

```text
status message error data success query record_count results
record_kind provider_kind path name start_line end_line relationship_kind
source_provider_kind source_path source_name source_start_line source_end_line
target_provider_kind target_path target_name target_start_line target_end_line
```

For every other decoded property name, use a fresh per-execution 256-bit
memory-only key and HMAC-SHA256 over the domain-separated UTF-8 name plus its
uint64 big-endian byte length. Evidence/logs retain only lowercase digest,
length, value kind, container ordinal, and first-seen ordinal. Unkeyed hashes,
raw unknown names, and key persistence/logging are forbidden. Any unknown or
oversize name fails.

Adversarial tests place filesystem paths, symbols, source fragments, bearer/
API tokens, and Unicode/escaped canaries in property names and assert none
appear in evidence, logs, exceptions, stdout/stderr, or test output. Control
tests prove every allowlisted name remains readable. Repeated unknowns must
share a digest within one run; identical names under different run keys must
not. Oversize names must be streamed to HMAC/length without materializing or
logging them and must fail closed.

## Integration and full validation

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
docker compose build
```

Expected coverage:

- authorised SystemOwner/Administrator starts `开始扫描`; hidden/unauthorised
  access creates no `ScanRun`;
- local clone/update works and clone/provider failures return safe
  Simplified-Chinese `ProblemDetails`;
- filtered outcomes persist before provider execution;
- isolated Code Units persist with zero Edges;
- resolved Edges and unresolved target candidates satisfy same-ScanRun and
  uniqueness invariants;
- File Identity Hash uses the first `min(4096, file length)` bytes and Source
  Hash uses the complete local file;
- >=500 candidates and zero Code Units after successful projections flags human
  review;
- API summary exposes parsed/total counts only;
- migration and SQLite test model remain valid.

## Deterministic end-to-end fixture

1. Create a local bare Git fixture containing:
   - one isolated Function;
   - one Class with a contained Function;
   - one call, import, and inheritance relationship;
   - one target that cannot become a persisted local Code Unit;
   - one filtered generated/vendor candidate.
2. Start profiling through the authorised Repository details/API path.
3. Verify one terminal `ScanRun`, all expected independent Code Units, resolved
   Code Unit Edges, one unresolved candidate, filtered audit, and parsed/total
   counts.
4. Verify no source/raw JSON/private path/credential/token is present in logs or
   API output.
5. Verify zero Worker Agent jobs, dispatch records, Documents, Uncertainties,
   or Cross-Repository Resolution results.

## Historical evidence check

Do not rerun or modify `attempt-0003`. Its exact boundary SHA-256 is
`b9b5487b844d1c4ce89e2f03636e8b0c64f99f4dcbf6723a4f1e152ef138853b`
and its immutable terminal is `failed / provider-contract-failure`. It records
the qualification harness defect only; production validation uses the owned
two-projection contract above.

## Expected completion

All focused/full/build/container checks pass; isolated nodes are covered;
relationship mapping is independent; bounds and atomic failure are proved; and
the mandatory exact-pinned final-image provider-contract evidence validates as
`pass`. The accepted whole-Repository Scan strategy remains unchanged.
