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
