# Contract: Deterministic Framework Detection

## Catalog

`framework-catalog.v1.json` is an embedded, version-controlled, closed-schema
Infrastructure resource. Startup rejects unknown fields, duplicate keys,
ambiguous coordinate mappings, invalid framework names, invalid language
tokens, and unsupported evidence kinds.

Initial framework facts:

```text
aspnet-core
blazor
spring-boot
react
express
django
```

Initial exact catalog mappings:

| Coordinate | Framework | Allowed parser tokens | Evidence |
| --- | --- | --- | --- |
| `nuget-framework:Microsoft.AspNetCore.App` | `aspnet-core` | `c_sharp` | direct |
| `nuget-sdk:Microsoft.NET.Sdk.Web` | `aspnet-core` | `c_sharp` | direct |
| `nuget:Microsoft.AspNetCore.Mvc` | `aspnet-core` | `c_sharp` | direct, paired lock |
| `nuget:Microsoft.AspNetCore.Components.WebAssembly` | `blazor` | `c_sharp` | direct, paired lock |
| `nuget-sdk:Microsoft.NET.Sdk.BlazorWebAssembly` | `blazor` | `c_sharp` | direct |
| `npm:react` | `react` | `javascript`, `typescript`, `tsx` | direct, paired lock |
| `npm:express` | `express` | `javascript`, `typescript` | direct, paired lock |
| `maven:org.springframework.boot:spring-boot` | `spring-boot` | `java`, `kotlin` | direct |
| `maven:org.springframework.boot:spring-boot-starter` | `spring-boot` | `java`, `kotlin` | direct |
| `maven:org.springframework.boot:spring-boot-starter-web` | `spring-boot` | `java`, `kotlin` | direct |
| `python:django` | `django` | `python` | direct |

Matching is exact after ecosystem-defined package-name normalization; no
prefix, substring, wildcard, or regex matching is allowed. Adding a coordinate
is a catalog/code-review change with detector tests, not user input.

## Supported evidence

The detector resolves one exact immutable commit identity, incrementally streams
`git ls-tree -r -z --full-tree <commit> --`, and retains only root or nested
`*.csproj`, `pom.xml`, `package.json`, `package-lock.json`,
`packages.lock.json`, and `requirements.txt` blobs. It never materializes the
complete tree. Untracked, ignored, and dirty working-copy files are never
evidence. Each retained entry preserves its committed blob object ID and mode.

The command argument is the already resolved immutable commit, not a later
mutable `HEAD`. Each NUL-delimited record supplies exact tree mode, object type,
object ID, and Repository-relative path. A retained entry must be a blob and
must be read by that exact object ID. Malformed records, non-blob retained
entries, or mode/type/object identity drift fail the Repository detector result
with zero framework facts.

The inventory stream is bounded to 16 MiB, 100,000 input records, 1,000
retained supported artifacts, 2,048 path characters, and a 30-second deadline
with caller cancellation. Byte, record, deadline, or cancellation exhaustion
returns no partial framework facts.

This contract explicitly supersedes mutable-index
`git ls-files --cached`. It is neither an alternate nor a fallback inventory
path. The index and working-copy state never supply framework evidence.

| Ecosystem | Direct (`High`) | Transitive (`Medium`) |
| --- | --- | --- |
| NuGet | literal unconditional `.csproj` PackageReference/FrameworkReference/SDK coordinates | committed `packages.lock.json` with exactly one validated same-root `.csproj` owner |
| npm | literal `package.json` dependencies/devDependencies/optionalDependencies | committed `package-lock.json` v2/v3 with exactly one validated same-root `package.json` owner |
| Maven | literal project-level `pom.xml` direct dependency; no parent/profile/property/dependencyManagement evaluation | not supported |
| Python | literal requirement; no include/URL/editable/environment marker | not supported |

Unsupported formats or evidence create no fact. No build/restore/install,
MSBuild/Gradle/package script, external service, import, filename, symbol, or
LLM is evaluated.

For XML, a `.csproj` root namespace must be empty or the legacy MSBuild
namespace, and a Maven `project` root namespace must be empty or the standard
Maven POM namespace. Every consumed relevant element must match that accepted
root namespace. A relevant foreign-namespace lookalike makes that artifact
unsupported, produces exactly one bounded closed audit outcome, creates no
fact, and does not suppress independent valid evidence.

## Scope

1. Artifact parent directory is scope root.
2. Code Unit path must be a descendant and its parser language must match the
   catalog entry.
3. Deepest ancestor scope wins per ecosystem.
4. Before reading a lock entry as evidence, enumerate the complete committed
   same-root owner-candidate set: all `*.csproj` files for NuGet, or the exact
   `package.json` for npm.
5. The candidate set must contain exactly one file. That sole file must pass
   Repository containment/reparse/size bounds, parse successfully, use the
   supported direct-manifest shape, and be applicable to that ecosystem and
   scope. For NuGet, only these project-level declarations can prove ownership:
   a literal root `Project` `Sdk` attribute; a literal direct `Project/Sdk`
   child; or a literal `PackageReference`/`FrameworkReference` directly beneath
   a direct project-level `ItemGroup`. Conditions or dynamic values on an
   allowed shape are invalid. Declarations beneath `Target`, `Choose`, `When`,
   import-driven/evaluated content, or any other nested or
   execution-dependent construct never prove ownership.
6. An invalid candidate is not discarded to manufacture a unique owner.
   Zero/multiple candidates, a malformed/unsupported/conditional/unsafe/
   oversize owner, or an inapplicable owner makes the lock ineligible,
   produces one bounded closed audit outcome, and creates no lock-derived
   fact.
7. A validated owner need not itself contain a catalog-matched direct
   framework. Ownership is separate from exact catalog matching, so one valid
   direct manifest may justify scope for a supported transitive lock entry.
8. Same-ecosystem same-winning-root ambiguity produces no fact and a bounded
   audit outcome.
9. Different ecosystems at one root are independent and language-filtered.

A tracked filename, tracked-file count, same-root coincidence, or valid lock
file alone never proves ownership.

## Evidence locator

Only these forms persist:

```text
<relative-path>#json:<closed-json-pointer>
<relative-path>#xml:<closed-coordinate>
<relative-path>#line:<positive-line>:<catalog-coordinate>
```

Maximum 4,096 characters. The package coordinate must exactly match catalog
data. No arbitrary manifest value or raw line persists.

## Deduplication

Group candidates by `(CodeUnitId, FrameworkName)` and sort:

1. `High` before `Medium`;
2. greater scope depth;
3. ordinal evidence locator.

Persist the first. Database uniqueness independently enforces one row per
Code Unit/framework.

## Bounds

```text
8 MiB per artifact
64 MiB combined artifact bytes
1,000 supported artifacts
100,000 parsed package entries
64 nesting depth
250,000 candidate facts before deduplication
64 persisted framework facts per Code Unit
100 retained evidence audit details
2,048 path characters
4,096 locator characters
16 MiB Git inventory output
100,000 Git inventory input records
30-second Git inventory deadline
```

One invalid/unsafe/unsupported artifact is isolated. A lock whose owner proof
fails is isolated in the same way, and independent valid artifacts continue.
A repository-wide cap, catalog invariant failure, or unexpected detector
failure stops before the structural transaction. Diagnostics use only closed
Chinese reason text and a safe Repository-relative path or `(unknown)`.

For a NuGet lock whose sole apparent owner declaration is unsupported,
conditional/dynamic, nested, import-driven, or execution-dependent, the
artifact-local lock/owner relationship creates zero `Medium` facts and exactly
one bounded closed audit outcome. That outcome retains no raw declaration
value, source content, or absolute host path.

## Required owner regressions

Both ecosystems retain valid unique-owner lock cases and add negative fixtures:

- sole malformed direct manifest plus matched lock;
- sole unsupported direct-manifest shape plus matched lock;
- sole unsafe/oversize direct manifest plus matched lock;
- conditional/dynamic NuGet owner plus matched lock;
- NuGet `PackageReference` nested beneath `Target` plus matched lock;
- NuGet `FrameworkReference` nested beneath `Choose/When` plus matched lock;
- import-driven or otherwise execution-dependent NuGet declaration plus
  matched lock;
- multiple same-root owner candidates where any candidate is invalid;
- owner outside the lock root or inapplicable to the Code Unit scope.

Each negative case creates no `Medium` fact, emits only one bounded closed audit
outcome for the lock/owner relationship, and discloses no raw value, source
content, or absolute host path. Independent valid evidence continues.

Positive regression fixtures separately prove all three supported NuGet owner
shapes: root `Project` `Sdk`, direct `Project/Sdk`, and direct
project-level-`ItemGroup` `PackageReference`/`FrameworkReference`.

## Planned-change authorization

This QR9 owner-grammar restriction materially changes Scan XML parsing and
lock-owner filtering. After the complete corrected plan set receives a fresh
independent `plan-review` `ACCEPT`, the root must obtain explicit user
authorization before task generation. The request identifies this trigger,
the exact grammar/effects and intended TDD implementation, this contract and
plan, and PRD FR-4.1. The prior QR8 inventory/namespace approval does not
pre-authorize QR9 and waives no later gate.
