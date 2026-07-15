# Quickstart Validation: CodeGraph Code Unit Extraction

This guide has two gates. Gate 1 qualifies the local provider after the user
approves the proposed Story/PRD correction. Gate 2 is available only after Gate
1 passes and a follow-up plan review records the exact provider projection
grammar.

## Prerequisites

- Docker/Compose and PostgreSQL as documented in `AGENTS.md`.
- Git for local bare-Repository fixtures.
- A digest-pinned Docker build with locked CPython 3.12.x,
  `codegraphcontext==0.5.1`, Kuzu, and all transitive Python distributions.
- A non-root `app` runtime with private writable provider context/database
  storage. No remote backend, API gateway, MCP server, watcher, bundle, or
  registry process is allowed.

## Gate 1: qualify the provider before structural persistence

1. Build the image using the lock. Assert the final image's `python --version`,
   `cgc version` reporting `0.5.1`, and `cgc doctor` as `app` against the
   committed lock. The historical 0.5.0 download failure is not a passing or
   executable requirement.
2. In a synthetic whole-Repository fixture, create a unique Kuzu context with a
   private `--db-path`; run `cgc index <fixture> --force`; execute static
   read-only structural projections; then delete the context.
3. Apply the configured whole-process timeout (proposed default: 1,800 seconds)
   to the complete index/projection. Verify cancellation, timeout, and non-zero
   exit terminate the process tree and leave no provider-derived structural
   records.
4. Verify captured bounded output deterministically supplies path/kind/symbol/
   range/endpoint fields, every supported relationship kind, and a
   filtered-target relationship candidate. Verify evidence contains no source
   secrets, credentials, tokens, or absolute working-copy paths.
5. If any assertion fails, stop and return lock/image/command/evidence identities
   for corrective planning. Do not add a provider adapter, Code Unit EF model,
   migration, or production endpoint. If all pass, revise plan/data-model/
   contract with observed grammar and obtain fresh plan acceptance before
   structural persistence tasks are generated.

### Provider-pin evidence

`python -m pip index versions codegraphcontext` observed `0.5.1` as the latest
available configured-index version on 2026-07-15, after exact `0.5.0` could not
be downloaded. Qualification must record the 0.5.1 lock hash, image digest,
and observed `cgc version` output before it can pass. The public documentation
observed on that date is labelled 0.5.0, so the 0.5.1 command behavior and
output grammar must be proved rather than inferred. This correction changes no
scan-strategy or PRD decision.

## Gate 2: validate the approved implementation

```
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter RepositoryProfiling
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
docker compose build
```

Expected coverage after Gate 2:

- authorised SystemOwner/Administrator starts profiling from `开始扫描` and sees
  `已解析 {parsed}/{total} 个文件`; hidden or unauthorised attempts use the
  established access outcome and create zero Scan Runs;
- local Git clone/update and missing-CLI/clone failures are safe and Chinese;
- a successful whole graph persists qualified Code Units and Code Unit Edges; a
  filtered target produces one unresolved structural candidate and no target-null
  edge;
- every Vulgata-filtered file has a `Filtered` audit outcome; malformed rows of
  a completed qualified projection are warned without exposing source;
- whole-provider timeout/crash aborts/fails the Scan Run with no partial
  provider-derived results (not per-file continuation);
- >=500 eligible candidates with zero Code Units after a successful graph flags
  human review; migration applies under normal startup/SQLite model; and the
  required E2E covers UI, authorization, API, persistence, migration, and
  multiple modules.

## Manual smoke check

1. Sign in as a SystemOwner for a System with a validated local test Repository.
2. Open its repository details, select `开始扫描`, and wait for the terminal
   summary.
3. Confirm count/warning text is Simplified Chinese and exposes no source,
   private path, credential, or raw provider output.
4. Repeat with an unauthorised account; verify no Scan Run or result is exposed.
