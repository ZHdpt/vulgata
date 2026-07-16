# Quickstart Validation: CodeGraph Code Unit Extraction

## Mandatory safe preflight and authorization

Before any qualification command, move the real provider fixture away from the
legacy path embedded in the pre-incident assembly and prove that path is absent.
Build the test project explicitly, then run only the
`CodeGraphQualificationSynthetic` category with compilation enabled. Do not use
`--no-build` or a broad namespace/class filter. This preflight validates source
hashes, locks, schemas, ledger state, and synthetic streams only; it must not
call Docker, inspect/build/pull an image, start a container, probe `/health`, or
invoke `cgc`.

The accidental execution is durable `attempt-0001` and cannot qualify HTTP
JSON. Exactly one replacement, `attempt-0002`, is required to finish Gate 1.
After the safe preflight is green, stop for explicit human authorization. The
provider gate is run only through its dedicated wrapper, exact provider test
identity, and process-scoped attempt ID/token. The wrapper validates and
atomically claims the authorised ledger record before provider reachability.
Without authorization it is inert; after a claim it never retries.

This guide has two gates. Gate 1 qualifies the local provider against the
already approved Story/PRD baseline and the approved qualification-only HTTP
JSON transport boundary. Gate 2 is available only after Gate
1 passes and a follow-up plan review records the exact provider projection
grammar.

## Prerequisites

- Docker/Compose and PostgreSQL as documented in `AGENTS.md`.
- Git for local bare-Repository fixtures.
- A digest-pinned Docker build with locked CPython 3.12.x,
  `codegraphcontext==0.5.1`, Kuzu, and all transitive Python distributions.
- A non-root `app` runtime with generated app-owned private `HOME` and Kuzu
  storage (mode `0700`), plus a credential-free synthetic fixture mounted
  read-only.
- Docker must run the named qualification container with `--network none`, no
  published port, and only its internal loopback available. The
  CodeGraphContext gateway is permitted solely for this qualification; it is
  not a production Scan transport or Vulgata endpoint. No remote backend,
  watcher, bundle, registry, provider MCP/tool invocation, Vulgata MCP tool, or
  Worker Agent tool is allowed.

## Gate 1: qualify the provider before structural persistence

1. Build the image using the committed lock. Assert the final image's
   `python --version`, `cgc version` reporting `0.5.1`, and `cgc doctor` as
   `app`. The historical 0.5.0 download failure is not a passing or executable
   requirement.
2. Start one named qualification container with `--network none`, no published
   port, and the synthetic Repository mounted read-only. Create one generated
   private `HOME`, Context name, and Kuzu path; pass the same `HOME` to every
   provider and cleanup process. Execute this lifecycle exactly once:

   ```text
   cgc context create <run-context> --database kuzudb --db-path <private-db-dir>
   cgc context mode named
   cgc context default <run-context>
   cgc --context <run-context> index <fixture> --force
   cgc api start --host 127.0.0.1 --port <private-port>
   GET http://127.0.0.1:<private-port>/health
   POST http://127.0.0.1:<private-port>/api/v1/query
   gateway-stop
   cgc context delete <run-context>
   ```

   `gateway-stop` is the test runner's lifecycle action: send `SIGTERM` to the
   recorded gateway process group, wait at most five seconds, and use
   `SIGKILL` only if necessary. A forced kill fails the gate. Delete the named
   Context, private Kuzu database, provider home, and named container in
   cleanup; any cleanup failure takes precedence over an otherwise valid
   response.
3. Poll only `/health` for at most ten seconds at 100-millisecond intervals.
   Stream, discard, and append every readiness body in attempt order to the
   single logical `cgc-api-health` channel. Readiness is not structural
   evidence and is not a query retry. After readiness, send exactly one static
   read-only request using the query and `{ "params": {} }` body fixed in
   [plan.md](plan.md); its response is the single `cgc-api-query` channel.
   Never call the provider's `/api/v1/mcp/*` or `/api/v1/tools/*` routes.
4. Apply the approved 1,800-second whole-process limit from index through
   projection. Incrementally frame, hash, and discard every provider-process
   stdout/stderr stream and both HTTP logical channels under one shared
   inclusive **65,536-byte** raw-byte cap. Byte 65,537 is the overflow
   sentinel: count/hash it once, stop observation, terminate the process tree,
   force private cleanup, and make no recovery command or request. Verify byte
   conservation and separate deterministic channel hashes; never retain raw
   output, JSON, chunks, paths, symbols, endpoints, source, credentials, or
   tokens.
5. Parse the query response with strict incremental UTF-8 and a streaming JSON
   reader. Require the exact envelope and twelve-scalar result-object grammar
   defined in [plan.md](plan.md) and
   [repository-profiling-api.md](contracts/repository-profiling-api.md); do not
   use `ReadAsString`, replacement decoding, cross-record joins, or fallback
   provider fields. Prove complete correlated records for `CALLS`, `CONTAINS`,
   `IMPORTS`, and `INHERITS`, positive ordered relationship-specific ranges,
   non-empty endpoint identities, and the fixture-filtered target candidate.
   Reject an unknown, missing, duplicate, nested, wrong-typed, null-disallowed,
   source-bearing, or greater-than-4,096-byte result object.
6. A pass requires the exact one-entry lifecycle, zero exits, Context-isolation
   proof, no overflow/termination/forced cleanup, all structural predicates,
   and successful cleanup. Persist only schema-1.2 redacted channel counts and
   hashes, lifecycle/relationship/record counts, booleans, cleanup facts, and
   the canonical summary hash. If any assertion fails, stop and return only
   those safe identities and a failure category for corrective planning. Do
   not add a production provider adapter, Code Unit EF model, migration, API,
   or UI. If all assertions pass, revise the parser/integration plan and obtain
   fresh acceptance before structural persistence tasks are generated.

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
