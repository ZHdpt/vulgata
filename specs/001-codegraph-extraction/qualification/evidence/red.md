# Qualification red evidence

Date: 2026-07-15. Fixture: `codegraph-qualification-synthetic-repository`.
The fixture is synthetic and credential-free. No fixture source, absolute
checkout path, credential, token, or raw command output is retained.

## Exact-provider observation

| Command | Exit code | Redacted evidence |
| --- | ---: | --- |
| `python -m pip index versions codegraphcontext` | 0 | The configured package index listed `0.5.1` first. |
| `python -m pip download --no-deps --dest <generated-private-directory> codegraphcontext==0.5.1` | 0 | Download succeeded. The wheel SHA-256 is `3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658`; captured stdout/stderr hashes are `2446a40e7caaba00b24c990544ae5e322e12eebb486309c5883765331147081e` / `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. |
| `docker version --format <server-version-format>` | 0 | Captured stdout/stderr hashes are `10bd0be4ba4a5b28b88fff045d67fa0a0e991f084b44969d1eae657dbb4bee96` / `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`. |
| `docker pull --platform linux/amd64 python:3.12-slim-bookworm` | 1 | The initial Docker Hub base-resolution failure. Captured stdout/stderr hashes are `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` / `0f46cb92c1a7aebbde3d65460ef42c59eaee1cd02e7ae9ff2532183ecc91cf6a`. |
| `docker pull --platform linux/amd64 python:3.12-slim-bookworm` (one user-authorised retry) | 1 | Docker Hub's authorization-token request ended without a response. Captured stdout/stderr hashes are `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` / `cbc3ab0fae514d754e0442ceb758eed413324f71d9f7fdc2d93364c2a3e9915a`. No raw diagnostic is retained. |

Direct inspection of the downloaded 0.5.1 wheel confirms the `cgc` console
entry point and candidate `version`, `doctor`, `context create/delete`, `index`,
and `query` commands. This is command-surface evidence only: it is not evidence
that a non-root Linux image can execute them, produce a stable output grammar,
or clean a private Kuzu context. The wheel also declares dependencies for
features outside this gate; none was installed, configured, or invoked.

## Observed red tests

Each test compiled and ran against the unmodified shim/no-lock image before any
Docker/provider implementation:

| Test command filter | Exit code | Observed missing behaviour |
| --- | ---: | --- |
| `FullyQualifiedName~CodeGraphImageQualificationTests` | 1 | No committed CPython 3.12 lock or hash-verified requirements lock exists. |
| `FullyQualifiedName~CgcCommandQualificationTests` | 1 | No `vulgata-codegraph-qualification:0.5.1` image exists, so the context/create/index/query/delete sequence cannot start. |
| `FullyQualifiedName~QualificationGateTests` | 1 | The inherited test-only writer still emitted the obsolete pin and could not satisfy the new exact-pin contract. |

The minimal green correction changed that test-only writer to emit `0.5.1` and
the same `QualificationGateTests` filter then passed (one test, exit `0`). The
remaining image and command tests stay red because no provider image exists.

The historical `0.5.0` package-resolution failure remains superseded evidence
only; it was not used as the executable provider pin.

## Cached-base continuation

The user provided a local Linux/amd64 `python:3.12` image. Direct local image
inspection identified the supplied immutable
`python@sha256:1a7dbae78e9568b95a8e2934719c4bf984d7ae319b3659ed129f59d8aca6d7a8`
base. No pull was issued. The exact repository lock now selects CPython
`3.12.13` and has SHA-256
`30b0bdae099a4a71e6c07fb606da7b1759329b16b659c5d580ae3365fd1a273b`.

The new whole-Repository command/cleanup contract was observed red before its
runner existed: the focused test build failed with `CS0246` for the absent
`CgcQualificationRunner` and `CgcQualificationExecution`. That proves no
candidate image or provider runner could falsely satisfy the command sequence.
The planned image build used `--pull=false` and the exact cached base. The one
permitted attempt reached the hash-verified package-install layer but did not
finish inside its 900,000-millisecond hard cap. The wrapper returned exit
`124` after 905.3 seconds including process-cleanup overhead; its build timeout
was exactly 900,000 milliseconds. The residual Docker client was terminated.
Docker Buildx still displayed the corresponding backend record as running, so
the shared builder was not stopped: it also held an older, unrelated running
record. No candidate image was created and no provider command was invoked.
Raw build output is intentionally not retained.

## Safe stop

The exact provider wheel is available, but the required digest-pinned CPython
3.12 image cannot currently be resolved from Docker Hub. The one user-authorised
retry has been consumed and also failed. Without that base, there is no honest
requirements lock, image digest, final non-root `cgc`, `doctor`,
context/index/query/delete run, cleanup proof, or observed output grammar. No
substitute Python base, resolver, provider version, provider adapter, structural
mapping, persistence, migration, Git/API/UI/E2E, Worker Agent, MCP, Scan
Coordinator, or Cross-Repository Resolution work was created.

Result: **FAIL -> corrective planning** with category `timeout`.

## Requirements-lock diagnosis and current safe stop

The Docker build reported a mismatch for `falkordblite==0.9.0`: its actual
source archive SHA-256 was
`14eb34f617a847927e1c55d54c02b01115ad3db87f16adae4ec54e2f540dac64`, while
the lock recorded `1a8643adc5c8fbac1081bd0840e4ee44369213e6ddb44588561f47da140f3e0c`.
PyPI release metadata and an independent direct download reproduced the actual
value. The lock was corrected, and all 88 pinned hashes subsequently matched
their PyPI release metadata.

That correction advanced the target build through the hash gate. The next
observed source-build error was the absence of `gcc`; the package setup invokes
both `gcc` and `make`. `build-essential` was therefore installed only in the
private `codegraph` build stage. The next two bounded target builds both failed
before pip because the configured Debian mirror/proxy returned HTTP 502 for
compiler dependencies. The second retry stopped after 176.16 seconds; it did
not run `cgc` or modify any provider contract.

Result: **FAIL -> corrective planning** with category
`image-build-failure`. The requirements lock is repaired; qualification is
blocked by the Debian mirror/proxy availability.

## Corrective capability split RED evidence (2026-07-16)

All commands below were R0, compiled fresh, used exact fully-qualified-name and
`CodeGraphQualificationSynthetic` equality filters, and had no Docker, `cgc`,
HTTP, image, container, or provider reachability.

| Missing behavior | Exit code | Finished (Asia/Shanghai) |
| --- | ---: | --- |
| No-authorization issuance must refuse a provider lease | 1 | `2026-07-16T19:18:53+08:00` |
| A synthetic launcher must refuse provider process creation | 1 | `2026-07-16T19:19:43+08:00` |
| The real launcher must require only the sealed lease capability | 1 | `2026-07-16T19:20:32+08:00` |
| Authorization cannot be a broad object, boolean, string, or static flag | 1 | `2026-07-16T19:21:29+08:00` |
| The qualification runner must require an injected launcher | 1 | `2026-07-16T19:22:09+08:00` |
| Every current qualification fact must have the exact synthetic category | 1 | `2026-07-16T19:23:10+08:00` |

Each RED preceded its minimal implementation. The legacy
`Fixtures/QualificationRepository` path remained absent throughout.

## Append-only attempt ledger RED evidence (2026-07-16)

Both commands compiled fresh and were provider-unreachable R0 invocations with
exact fully-qualified-name plus `CodeGraphQualificationSynthetic` filters.

| Missing behavior | Exit code | Failure |
| --- | ---: | --- |
| `Claim_WhenAuthorizationMatches_CreatesAndFlushesStartBeforeReachabilityAsync` | 1 | `QualificationAttemptLedger` did not exist (`CS0246`). |
| `Complete_WhenClaimIsValid_CreatesOneRedactedTerminalRecordAsync` | 1 | `CompleteAsync` did not exist (`CS1061`). |

No Docker, `cgc`, HTTP, image, container, or provider operation ran. The first
GREEN rerun also exposed and corrected a test-only exclusive-read ordering bug;
the claim itself had already been atomically created and flushed.

## T006-T007 streaming HTTP JSON recovery RED evidence (2026-07-16)

The inherited authentic tracer RED remains the stage RED: at
`2026-07-16T19:44:08+08:00`, the exact fresh-compilation
`Complete_WithChunkedHttpJson_ProducesOnlySchema12StructuralPredicates` command
exited 1 because `HttpQueryResponse` and `StaticStructuralQuery` were missing.
Recovery first reran that exact command against the disconnected implementer's
partial work; it passed 1/1. The first surrounding synthetic-category run then
exited 1 with 72 passed and one obsolete CLI-row test failure, proving that the
old table observer contract had not yet been fully removed.

All recovery commands were R0 and provider-unreachable. No Docker, `cgc`, HTTP
network, image, container, or provider operation occurred.

## T008-T009 fake lifecycle and schema RED evidence (2026-07-16)

All commands compiled fresh and were R0/provider-unreachable. The exact T008
filter first found no matching test at `20:10:44+08:00`; after adding the
behavior test it failed compilation at `20:11:46+08:00` because the launch plan
and fake observation seam did not exist. The cleanup-precedence tracer failed
compilation at `20:12:43+08:00` because the fake lifecycle event record did not
exist. The retired-schema tracer failed 1/1 at `20:13:49+08:00` because schema
1.1 was still accepted. Each RED preceded its minimal corresponding change.

No Docker, `cgc`, network HTTP, image, container, or provider operation ran.

### Interrupted-stage recovery RED

Recovery began with the exact T008 filter and fresh compilation at
`2026-07-16T21:01:40+08:00`. It failed 0/1 because the test searched for an
unquoted shell fragment while the generated cleanup loop correctly quotes
`$gateway_grace_attempt`. The assertion was narrowed to the exact valid shell
predicate; no runtime behavior changed. The exact rerun passed 1/1 at
`2026-07-16T21:02:32+08:00`.

This recovery remained R0/provider-unreachable. It performed no Docker, `cgc`,
network HTTP, image, container, or provider action and did not create or claim
`attempt-0002`.

Final diff review found that the shell form `if ! kill -KILL ...` would read
the inverted command status and could therefore mask a SIGKILL cleanup
failure. The exact T008 command established an additional authentic RED at
`2026-07-16T21:06:08+08:00` (0/1). This preceded the minimal ordinary
`if`/`else` correction that preserves the real failure status.

## T010-T011 provider-entry recovery RED evidence (2026-07-16)

The exact fresh-compilation static tracer
`ProviderEntry_WhenSourceIsInspected_WiresClaimedLeaseThroughRunnerWriterAndTerminalAsync`
failed 0/1 because `CgcProviderQualificationTests.cs` did not call
`ProviderQualificationEntry.ExecuteAsync`. The same source inspection showed
that `CgcQualificationRunner` still constructed the revoked
`Fixtures/QualificationRepository` path. This authentic RED preceded both
bounded seam changes.

No wrapper, script, Docker, `cgc`, HTTP, image, container, or provider action
ran. The RED did not create `attempt-0002` or modify the durable qualification
result.

## T013 authorized replacement execution — terminal failure

- The exact approved wrapper command was invoked once for `attempt-0002` and exited `1` after 8.9 seconds.
- The execution stopped at the authorization boundary because `workflowApprovalSummarySha256` did not match. Docker and CodeGraphContext remained unreachable.
- `authorized.json` is present (764 bytes; SHA-256 `65eb6c190414b253ce651b7688a68a6d94663e9909a73c8c0bcd757f2910daaa`).
- `started.json` and `terminal.json` are absent. Authorization was issued, but the guarded provider attempt never started; this does not make the consumed wrapper invocation reusable.
- Execution count: 1. Retry count: 0. Retry is forbidden.

## T014 command-free terminal hard stop

- Classification: **FAIL → corrective planning**.
- `qualification-result.json` remains unchanged at schema 1.0 with `status: fail`, `reason: unsafe-evidence`, and `nextAction: corrective-planning` (1,096 bytes; SHA-256 `358d5706b3b1472701f45fadbf219e1ae693671ff528022e092794698422d125`).
- No production Story 4.1 Scan implementation or test is authorized or complete from this qualification.
- T014 executed zero commands, tests, Docker actions, CodeGraphContext actions, HTTP requests, or provider actions.
