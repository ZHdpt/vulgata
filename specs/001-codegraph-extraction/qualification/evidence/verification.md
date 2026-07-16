# Qualification verification

Date: 2026-07-15.

| Command | Exit code | Observed result |
| --- | ---: | --- |
| `python -m pip index versions codegraphcontext` | 0 | The configured package index reported `0.5.1` as the latest available release. |
| `python -m pip download --no-deps --dest <generated-private-directory> codegraphcontext==0.5.1` | 0 | The exact wheel is available; its identity is recorded only as a SHA-256 in [red.md](red.md). |
| `docker version --format <server-version-format>` | 0 | The Docker daemon is available. |
| `docker pull --platform linux/amd64 python:3.12-slim-bookworm` | 1 | The initial required CPython 3.12 base-image resolution failed through Docker Hub. Raw diagnostics are intentionally not retained. |
| `docker pull --platform linux/amd64 python:3.12-slim-bookworm` (one user-authorised retry) | 1 | The only authorised retry also failed at Docker Hub's authorization-token request; bounded stdout/stderr identities are recorded in [red.md](red.md), and raw diagnostics are not retained. |
| `docker image inspect python:3.12` | 0 | The user-provided local Linux/amd64 base is `python@sha256:1a7dbae78e9568b95a8e2934719c4bf984d7ae319b3659ed129f59d8aca6d7a8`; no pull was issued. |
| Focused `CgcCommandQualificationTests` build | 1 | The new whole-Repository command/cleanup test was red because its runner/execution contract did not yet exist (`CS0246`); no provider image or command sequence could be mistaken for a pass. |
| `docker build --pull=false --file docker/Dockerfile .` | 124 | The one permitted exact cached-base build did not finish within its 900,000-millisecond hard cap. The wrapper reported exit `124` after 905.3 seconds including cleanup overhead; no candidate image was produced and raw output is intentionally not retained. |
| `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~CodeGraphQualification --no-restore` | 1 | The qualification test assembly is intentionally still red: `CgcCommandQualificationTests` cannot resolve the absent test-only `CgcQualificationRunner` and `CgcQualificationExecution`. The provider image did not build, so the T007 runner was not implemented and no candidate command sequence could be executed. |
| `Test-Json -Path specs/001-codegraph-extraction/qualification/qualification-result.json -SchemaFile specs/001-codegraph-extraction/qualification/qualification-result.schema.json` | 0 | `True`; the durable `0.5.1` image-build failure conforms to the qualification-result contract. |
| `git diff --check` | 0 | No whitespace errors at the qualification boundary. |

The result records `fail` / `timeout` / `corrective-planning`. The manual
cached-base continuation did not issue another pull. After the cap, the
residual Docker client was terminated. Docker Buildx still displayed this
backend record as running; stopping the shared builder was not safe because an
older unrelated build record was also running. The build timeout is a provider
qualification failure, so no alternate base, registry, provider version, or
infrastructure workaround was attempted.
No requirements lock, Docker provider image, provider runner, or provider command
output was fabricated. Accordingly, `cgc version`, `cgc doctor`, the private
context/create → whole-Repository index → static query → delete sequence,
cleanup, relationship kinds, filtered-target endpoint evidence, and output
grammar remain unqualified.

## Scope and stop decision

Reviewed paths are limited to `docker/`,
`tests/Vulgata.Tests/CodeGraphQualification/`, and
`specs/001-codegraph-extraction/qualification/` (plus task completion markers).
There is no change under `src/dotnet/`, no provider mapping or persistence, no
Git/API/UI/E2E, and no LLM, MCP, Worker Agent, Scan Coordinator, Document,
Uncertainty, or Cross-Repository Resolution work.

**FAIL -> corrective planning.** The digest-pinned CPython 3.12 base is now
available locally, but the lock-install build must complete within a reviewed
bounded command window before a fresh qualification can produce the complete
observed provider contract.

## Corrective capability split verification (2026-07-16)

The T002 exact test passed after fresh compilation. Five further vertical
T003 RED-to-GREEN cycles established the fake/real launcher split, the sealed
lease constructor boundary, the narrow claimed-authorization shape, required
runner injection, and exact synthetic categorization. The surrounding command
`dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter
"Category=CodeGraphQualificationSynthetic"` compiled fresh and passed 62/62
tests at `2026-07-16T19:24:05+08:00` (exit 0).

No current code path can construct `ProviderExecutionLease`: it is sealed with
a private constructor, no claimed authorization can yet be created, and the
real `DockerQualificationProcessLauncher` requires that lease. Ordinary and
synthetic discovery therefore remain provider-inert. The runner has no default
real launcher, while the fake launcher rejects process creation. No Docker,
`cgc`, HTTP, image, container, or provider operation occurred in T002-T003.

## Slim-bookworm continuation

The user replaced the prior normal CPython base with the locally cached,
Linux/amd64 `python:3.12-slim-bookworm` digest
`sha256:d50fb7611f86d04a3b0471b46d7557818d88983fc3136726336b2a4c657aa30b`.
Direct local inspection before the build confirmed that exact digest and
platform; no pull was issued. The current Dockerfile SHA-256 is
`b64e91f8031ff8176aedd89266228684e731741c26665a533710ea58452c4658`, the
requirements lock SHA-256 is
`30b0bdae099a4a71e6c07fb606da7b1759329b16b659c5d580ae3365fd1a273b`, and
the CPython lock SHA-256 is
`aa0d6581054e6e4ff3f91839deca7a854ad37221b8784d060b42d0f847ff1a3b`.

| Command | Exit code | Observed result |
| --- | ---: | --- |
| `docker build --pull=false --file docker/Dockerfile --tag vulgata-codegraph-qualification:0.5.1 .` | 1 | The exact cached base resolved locally, but hash-verified `pip install` failed while downloading a pinned distribution. Raw build output is intentionally not retained. |
| `docker image inspect vulgata-codegraph-qualification:0.5.1` | 0 | A pre-existing local tag was present after the failed rebuild. It was not used for provider commands because this failed rebuild cannot independently bind that tag to the current Dockerfile and lock boundary. |

No second build was attempted. No provider command, alternate image, registry,
package mirror, provider version, or unpinned dependency was used. The durable
decision is `fail` / `image-build-failure` / `corrective-planning`; its result
contains only bounded metadata and no raw output.

## Lock repair and compiler-stage retry

The `falkordblite==0.9.0` source archive downloaded during the Docker build had
SHA-256 `14eb34f617a847927e1c55d54c02b01115ad3db87f16adae4ec54e2f540dac64`.
The prior lock hash was incorrect. PyPI release metadata and an independent
direct archive download both confirmed the Docker value, so the lock was
corrected to that exact hash. A read-only check then confirmed every one of the
88 locked hashes is published for its pinned PyPI release.

The corrected target build passed the `falkordblite` hash gate but failed while
building its source distribution because the slim build stage lacked `gcc`.
The package setup explicitly invokes `gcc` and `make`; consequently,
`build-essential` was added only to the disposable `codegraph` stage, before
the hash-verified install. The final .NET runtime stage is unchanged.

| Command | Exit code | Observed result |
| --- | ---: | --- |
| `docker build --pull=false --file docker/Dockerfile --target codegraph --tag vulgata-codegraph-qualification:0.5.1 .` | 1 | The compiler-stage retry stopped before pip: the configured Debian mirror/proxy returned HTTP 502 for compiler dependencies. |
| Same unchanged command (one bounded retry) | 1 | The same external boundary recurred after 176.16 seconds, with HTTP 502 responses for Debian packages including `libperl5.36`, `perl`, `bzip2`, `libcrypt-dev`, `libgomp1`, and `libubsan1`. |

Neither retry reached `cgc version`, so no provider-execution, non-root,
context/index/query/delete, cleanup, or output-grammar evidence exists. The
durable decision remains **FAIL -> corrective planning** with category
`image-build-failure`; the unresolved dependency is the configured Debian
mirror/proxy, not the requirements lock.

## T004-T005 append-only ledger verification (2026-07-16)

Fresh exact-FQN GREEN runs passed the atomic start claim and single terminal
record tracer tests. The boundary-mismatch theory passed eight cases covering
token SHA-256, expiry, exact command identity, corrective plan commit, accepted
`plan-review-8` path/hash, and explicit workflow approval timestamp/summary.
Create-new state rejects reuse; the plaintext token is absent from ledger
records, and the sealed lease is issued only from the successfully flushed
claim.

The closed schemas are `qualification/attempts/{authorized,started,terminal}.schema.json`.
The accidental run is imported without invented authorization as immutable,
redacted `attempt-0001` records citing
`qualification/evidence/accidental-provider-execution-2026-07-16.md`:

- `started.json` SHA-256 `6dd9b695926b16add679b0518d2a0879f3c2b839a351ae9aacd539ad101f13fb`;
- `terminal.json` SHA-256 `d1c78c33610e7011b4c616b6fc70c3483d3794825ab46fa9a48c5e6f3bc2c4cc`.

`attempt-0002/authorized.json`, `started.json`, and `terminal.json` do not
exist. No Docker, `cgc`, HTTP, image, container, or provider operation ran.

## T006-T007 streaming HTTP JSON verification (2026-07-16)

The schema-1.2 observer now incrementally validates strict UTF-8 and the exact
HTTP JSON envelope/query echo, twelve scalar aliases, all four approved
relationship predicates, the filtered-target candidate, the inclusive shared
65,536-byte budget, byte-65,537 sentinel, logical-channel hashes, and total-byte
conservation. Synthetic rejection cases cover trailing JSON, malformed UTF-8,
record-count mismatch, missing/duplicate/unknown/nested/source-bearing fields,
wrong/null types, relationship-specific ranges, records over 4,096 raw bytes,
and cross-record joining.

The exact tracer command passed 1/1 with fresh compilation. The observer class
suite passed 22/22, and the exact `CodeGraphQualificationSynthetic` category
passed 83/83 at `2026-07-16T20:06:24+08:00`. An `rg` check returned no matches
for the removed raw-response/Python/key-value/CLI-table observer paths, and
`git diff --check` passed after the final bounded edit.

This was entirely R0: zero Docker, `cgc`, HTTP network, image, container, or
provider execution. `attempt-0002` remains unclaimed and unspent.

## T008-T009 fake lifecycle and schema verification (2026-07-16)

The exact T008 fresh-compilation filter passed 1/1 at `20:14:09+08:00`. The
full exact `CodeGraphQualificationSynthetic` category then passed 86/86 at
`20:14:37+08:00`. Fake-only assertions prove the exact eleven-entry lifecycle,
private HOME/Kuzu Context, network-none and loopback/no-port isolation, bounded
health polling, one query with no MCP/tool route, the single 1,800-second clock,
process-tree termination, SIGTERM/five-second grace/conditional SIGKILL cleanup,
cleanup-failure precedence, and atomic failure without partial structural facts.

The closed result schema now accepts current schema 1.2 and historical failure
schema 1.0 only, rejects retired schema 1.1, routes pass to `parser-planning`,
and routes failure to `corrective-planning`. The writer retains only bounded,
redacted structural predicates and their canonical hash summary.

This verification was entirely R0. `attempt-0002` and every attempt-0002 result
or ledger record remain absent; zero Docker, `cgc`, network HTTP, image,
container, or provider actions occurred.

### Interrupted-stage recovery verification

Fresh recovery verification produced the following results:

| Command | Finished (Asia/Shanghai) | Exit | Result |
| --- | --- | ---: | --- |
| Exact T008 FQN plus exact synthetic category | `2026-07-16T21:02:39+08:00` | 0 | 1/1 passed after correcting only the quoted shell-fragment assertion. |
| Exact `QualificationGateTests` class plus exact synthetic category | `2026-07-16T21:03:25+08:00` | 0 | 43/43 writer and schema cases passed. |
| Exact `CodeGraphQualificationSynthetic` category | `2026-07-16T21:03:59+08:00` | 0 | 86/86 passed. |
| `Test-Json` current durable result against the closed schema | `2026-07-16T21:04:26+08:00` | 0 | `True`; historical schema-1.0 failure readability remains valid. |
| Exact `CgcCommandQualificationTests` class plus exact synthetic category | `2026-07-16T21:05:05+08:00` | 0 | 3/3 lifecycle, cleanup-precedence, and atomic-failure cases passed. |
| Exact T008 after preserving the SIGKILL failure status | `2026-07-16T21:06:37+08:00` | 0 | 1/1 passed. |
| Final exact `CodeGraphQualificationSynthetic` category | `2026-07-16T21:07:14+08:00` | 0 | 86/86 passed after the cleanup-status correction. |

The durable `qualification-result.json` Git object hash remained
`d14922c3e5554bf1fb0339037d141f54e43d761a` in both HEAD and the worktree.
The `attempt-0002` directory remained absent. `git diff --check` passed. All
commands compiled fresh where applicable and remained R0: zero Docker, `cgc`,
network HTTP, image, container, or provider actions.
