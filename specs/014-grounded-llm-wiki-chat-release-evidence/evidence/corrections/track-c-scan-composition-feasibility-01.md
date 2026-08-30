# Story 12.5 Track C Scan composition feasibility 01

- Evidence ID: `12-5/plan/scan-composition-feasibility-01`
- Kind: plan-author self-review and failure evidence
- Stage: `plan`
- Source commit: `c710bc315b477eb1040b026ea6f10aa1c2613b60`
- Observed at: `2026-08-30T00:10:24.4661357Z`
- Outcome: `BLOCKED`
- Earliest owner: `specify/correct-course`

## Requested bounded design

Compose the existing real `RepositoryProfilingCoordinator` in production
without changing Scan parsing, filtering, selection, scheduling, grouping,
retry, dispatch, persistence, EF, Story 12.4 contracts, or accepted Track A/B
behavior.

## Source-grounded feasibility result

1. `src/dotnet/Vulgata.Web/Program.cs:474` binds
   `IRepositoryProfilingCoordinator` to
   `RuntimeUnavailableRepositoryProfilingCoordinator`.
2. `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs:54-68`
   shows that the existing real coordinator also requires
   `ICodeGraphProvider`.
3. Production composition has no `ICodeGraphProvider` registration. The only
   implementation is the legacy `CodeGraphContextProvider`.
4. `specs/010-durable-scan-foundation/plan.md:149-151` (AD-10) explicitly
   excludes `CodeGraphContextProvider` from production/default release
   composition. The same plan records that the closed CodeGraph analysis ports
   replace the production CGC boundary.
5. `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md:59-63,113`
   makes Epic 4's CGC path historical and removes CodeGraphContext from the
   production/release composition.
6. `specs/014-grounded-llm-wiki-chat-release-evidence/spec.md:226-228`
   (FR-049 through FR-051) forbids new or materially changed Scan behavior and
   limits Track C-MVP support to observing the existing product path.

## Alternatives assessed

- Swapping only the coordinator registration cannot resolve its missing
  `ICodeGraphProvider` dependency.
- Restoring `CodeGraphContextProvider` contradicts accepted Story 12.1 and
  August 30 runtime authority and targets the retired CGC command surface.
- Adapting the current closed CodeGraph 1.5 analysis ports into the legacy
  whole-graph `ICodeGraphProvider` result materially changes the Scan/provider
  design and is outside the approved composition-only scope.
- Reusing Story 12.4 data is explicitly forbidden as Story 12.5 release
  evidence.

## Self-review

- Feasibility: failed within the authorized boundary.
- Constitution/layering: no migration or layering violation is necessary, but
  the runtime contracts are incompatible.
- Security and data isolation: preserved by rejecting legacy-provider and
  Story 12.4 database substitution.
- Failure behavior: retain the honest HTTP 503, zero-Scan-Run evidence, and the
  guarded isolated Story 12.5 database.
- Testability: a production-composition RED is available, but there is no
  lawful minimal GREEN under the current specification.

No specification, plan, tasks, source, test, database, index, or Git history
was changed during this feasibility assessment. The accepted plan hash remains
`614bf140543847a396ca04eedb1f007a7e937e260619818838506f053c1cc0d7`.

## Required next decision

Choose either an explicitly bounded correct-course/specification strategy for
the missing production profiling prerequisite or a separate corrective story.
That decision must disclose the current-versus-legacy CodeGraph contract gap.
It cannot be inferred from the earlier composition-only approval.
