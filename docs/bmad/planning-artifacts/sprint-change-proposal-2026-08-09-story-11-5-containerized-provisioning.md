# Sprint Change Proposal: Story 11.5 Containerized Provisioning

**Date:** 2026-08-09
**Trigger:** The second live T064 run reached product-service provisioning but
failed both Repository profiling operations with `ProviderUnavailable` because
the accepted harness executes profiling on the Windows host while the exact
hash-pinned CodeGraphContext 0.5.1 runtime exists only in the qualified Linux
final image.
**Mode:** Incremental
**Approval:** The user explicitly approved Option A on 2026-08-09.
**Scope classification:** Moderate direct adjustment within Story 11.5.
**Execution level:** `full`.

## 1. Issue Summary

Story 11.5 requires one clean, product-service-only, two-Repository release
proof. The current T064 plan builds the source-bound final image after host-side
product provisioning. That ordering assumes the Windows test host can execute
`cgc` during Repository profiling.

The live run disproved that assumption. `CodeGraphContextProvider` launches the
local `cgc` executable, the Windows host has no accepted `cgc` runtime, and the
repository's exact CodeGraphContext 0.5.1 lock targets the Linux runtime built
into `docker/Dockerfile`. Installing an arbitrary Windows or pip runtime would
break the pinned runtime envelope and would not prove the shipped image.

The correction is to build the one qualified final image once, use that same
image for a temporary provisioner container that executes product profiling and
the Scan path, remove the provisioner, and then continue with the existing
provider/app/Playwright proof. No second image, alternate CodeGraph runtime, or
host installation is permitted.

The live run also exposed a separate cleanup defect for read-only or leased Git
pack files. A bounded test-first correction already exists in the two release
harness test files; it remains part of the candidate and must be reviewed with
the topology change.

## 2. Evidence and Impact Analysis

### Concrete evidence

- The second T064 run completed 30 of 31 checks and failed the live final entry
  with Repository profiling outcomes `ProviderUnavailable/ProviderUnavailable`.
- `CodeGraphContextProvider` starts `cgc` as a local process.
- The Windows host has no accepted `cgc` installation; the available Python
  runtime is not the repository's locked Linux CPython 3.12 envelope.
- `docker/codegraph/requirements.lock` pins CodeGraphContext 0.5.1 for the Linux
  image, and `docker/Dockerfile` installs/copies that runtime into the final
  image.
- The current Story 11.5 plan and release contract explicitly place profiling
  on the host before `source-bound-image-build`, so retrying the unchanged
  topology cannot succeed safely.
- External cleanup preserved the running PostgreSQL 17 Alpine server and the
  original development database. The harness-owned nonce database and Docker
  resources were removed.
- The cleanup regression now proves exact nonce-root deletion, bounded retry
  for read-only/leased pack files, and a fixed path-free failure after retry
  exhaustion; the non-live release harness is GREEN 33/33.

### Correct-course checklist results

| Item | Result |
| --- | --- |
| Trigger and evidence | Done — Story 11.5 T064 exposed a host/runtime mismatch during live profiling. |
| Current epic impact | Epic 11 remains viable; only the Story 11.5 release topology and dependent task wording change. |
| Future epic impact | None; general Scan orchestration and deployment evolution remain with their existing epics. |
| PRD impact | None; the clean two-Repository E2E and single final image requirements remain unchanged. |
| Architecture impact | Feature-level release topology changes from host-side provisioning to a temporary container from the same final image. |
| UX impact | None. |
| Data impact | No EF model, migration, snapshot, schema, or backfill change. The harness still owns only a nonce-named database on the supplied PostgreSQL 17 endpoint. |
| Deployment/test impact | Reorder the one image build before provisioning; add one temporary provisioner container and its exact cleanup/evidence. |
| Direct adjustment | Viable — medium effort and medium risk. |
| Rollback | Rejected — it would discard accepted product work and would not supply an accepted host `cgc` runtime. |
| MVP reduction | Rejected — the two-Repository live proof and final image are explicit release acceptance. |
| Sprint status | No epic/story addition, removal, renumbering, or status change is required. |

## 3. Recommended Approach

Use Option A, a single-image containerized provisioner:

1. Preserve the clean-candidate and source-manifest checks, then build the
   source-bound final image exactly once before product provisioning.
2. Start the existing isolated provider container and preserve its loopback-only
   host publications.
3. Start one temporary provisioner container from the same final image and the
   same immutable image ID that will later run the app. Do not build or pull a
   second application image.
4. Override only the provisioner container entry point to run a test-owned,
   prebuilt .NET provisioner executable. Mount that executable read-only rather
   than modifying the production image or Dockerfile.
5. Run the provisioner in the provider network namespace so the stored Worker
   and Chat endpoints remain identical to the final app topology.
6. Mount only harness-owned configuration, Data Protection key ring, output,
   and the two accepted source checkouts. Source mounts are read-only and use
   stable Linux paths. The nonce database remains on the supplied external
   PostgreSQL 17 Alpine server via `host.docker.internal`.
7. Inside the qualified Linux container, compose the real product services and
   execute migrations, Identity/System/Repository/provider setup, Repository
   profiling, deterministic ordinary Pass 1, exact Cross-Repository Resolution,
   runtime prepared-flow binding, and the single exact seven-slot Pass 2
   dispatch/commit.
8. Emit only the existing sanitized provisioning/result contract to a
   harness-owned output path. Reject secrets, source contents, absolute host
   paths, or raw provider diagnostics.
9. Stop and remove the provisioner container before starting the final app
   container from the exact same image ID. Continue the existing label, curl
   health, Identity, Playwright citation, authorization-negative, and revocation
   proof.
10. Unconditionally remove the provisioner alongside the app, provider,
    network, image, browser state, nonce database, key ring, environment file,
    and nonce run directory. Retain primary and cleanup failures separately and
    verify final absence.

The correction does not change the production Scan algorithm, exact profile,
ordinary Pass 1 identity, runtime Pass 2 binding, seven-slot payload, provider
contract, or deployment artifact. It changes where the already accepted
product-service path runs during the release proof.

## 4. Detailed Artifact Changes

### Plan, research, quickstart, and release contract

Replace the current host-side provisioning sequence:

> validate host/provider connectivity -> create/migrate nonce database ->
> host-side product provisioning and profiling/Scan -> build one source-bound
> final image -> start app -> browser proof

with:

> validate host/provider connectivity -> create nonce database -> build one
> source-bound final image -> start provider -> run migrations and the complete
> product-service profiling/Scan path in a temporary provisioner container from
> that same image -> remove provisioner -> start final app from the identical
> image ID -> browser proof

The amended artifacts must define:

- one build and one immutable application image identity;
- the provisioner as a temporary container instance, not a second image or
  deployment component;
- exact network, database, source-mount, key-ring, configuration, and sanitized
  output boundaries;
- fail-before-provisioning checks for image identity, accepted refs/commits,
  source mount reachability, PostgreSQL 17, and provider namespace;
- product-service-only execution with no fixture SQL, direct `DbSet`,
  reflection, fabricated identifiers, or host `cgc` fallback;
- unconditional provisioner cleanup and final absence evidence;
- Windows Docker Desktop Linux-container scope, with no native-Linux support
  claim introduced by this correction.

### Tasks

After fresh plan acceptance and the required planned-change authorization,
regenerate the unfinished task boundary so it includes:

- regression RED for the old host-side profiling order and host `cgc`
  dependency;
- RED/GREEN contract tests for one image ID, one temporary provisioner, stable
  read-only source mounts, provider namespace sharing, nonce database access,
  sanitized output, and unconditional cleanup;
- implementation of the test-owned provisioner entry and harness orchestration;
- surrounding non-live harness, build, static, no-migration, and cleanup checks;
- fresh Full analysis, acceptance review, quality review, candidate checkpoint,
  live T064, and final T065 verification.

Completed production behavior tasks remain complete. T064 and T065 remain open
until a newly reviewed candidate passes the live run and independent cleanup
verification.

### BMad/PRD/architecture/UX

- Story 11.5 acceptance text and the PRD outcome do not change.
- No new epic or story is added; sprint ordering and status remain unchanged.
- No global product architecture or UI/UX edit is required.
- The Story 11.5 plan set is the authority for this release-only container
  topology.

## 5. Scope and Guardrails

### Included

- Story 11.5 plan-set and unfinished-task correction.
- Test-owned provisioner executable/entry and release-harness orchestration.
- Existing cleanup correction in the two dirty harness test files.
- One source-bound final image built once and used by both provisioner and app.
- Focused/non-live regressions, Full analysis, fresh reviews, live T064, and
  final T065 cleanup verification.

### Excluded

- Installing `cgc`, Python packages, or an alternate CodeGraph runtime on the
  Windows host.
- A second application image, modified production Dockerfile solely for tests,
  or an unlabelled/unreviewed helper image.
- A durable provisioner service or production deployment topology change.
- Product Scan scheduling, grouping, retry, failover, matching, provider, or
  seven-slot payload changes.
- PRD, global architecture, UX, EF model, migration, snapshot, schema, or
  development-database changes.
- Fixture SQL, direct `DbSet` provisioning, reflection, fabricated identities,
  source/secret leakage, or weakening any authorization/currentness/citation
  gate.

## 6. Handoff and Gate Sequence

| Step | Owner | Required outcome |
| --- | --- | --- |
| Correct-course approval | User | Option A approved on 2026-08-09. |
| Plan correction | Plan architect | Amend the Story 11.5 plan set without changing product scope. |
| Independent plan review | Fresh plan judge | `ACCEPT` the exact single-image provisioner topology. |
| Planned-change authorization | User | Separately authorize progression to tasks because the release proof materially relocates Scan execution. |
| Task regeneration and analysis | Task author + artifact analyst | Dependency-ordered unfinished work and Full analysis with no Critical/High finding. |
| TDD implementation | Fresh bounded implementer | Observed RED, minimal GREEN, surrounding verification, no live resource acquisition. |
| Fresh Full reviews | Acceptance + quality reviewers | Both `ACCEPT` the identical corrected boundary. |
| Candidate checkpoint | Root orchestrator | Clean immutable commit/tree/manifest/evidence boundary. |
| Live T064/T065 | E2E runner + root | One live run, complete sanitized evidence, and independently verified nonce-only cleanup. |

The root remains the sole Git author. Other agents may not stage or commit.

## 7. Success Criteria

- The plan judge accepts a byte-specific, testable single-image provisioner
  topology with no unresolved host/runtime assumption.
- The exact final image is built once and its immutable ID is used for both the
  temporary provisioner and final app containers.
- Repository profiling and all Scan steps execute inside the qualified Linux
  image and use its hash-pinned CodeGraphContext 0.5.1 runtime.
- Provisioning still uses real product services, the supplied PostgreSQL 17
  Alpine server, and a cryptographically nonce-named database without touching
  the administrator, development, or other databases.
- No second image, host `cgc`, fixture SQL, direct `DbSet`, reflection, or
  fabricated identity is used.
- The provisioner output and release evidence remain sanitized, and the exact
  source refs/commits, image labels, seven-slot dispatch, citations,
  authorization-negative case, and revocation remain proven.
- Primary and cleanup failures remain separately visible. The provisioner and
  every other harness-owned resource are absent after success or failure.
- The existing read-only/leased Git pack cleanup tests and all applicable
  non-live/build/static/no-migration checks pass.
- Fresh Full analysis and both independent Full reviews accept the corrected
  boundary before live T064 is retried.

## 8. Approval and Remaining Authorization

The user approved Option A on 2026-08-09. That approval finalizes this
correct-course direction and authorizes plan correction and independent plan
review. It does not waive the repository's separate planned-change gate after
plan acceptance, nor TDD, Full analysis, independent reviews, live E2E,
security, migration, cleanup, verification, or Git checkpoint requirements.
