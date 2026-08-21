# Story 12.2 Convergence — Iteration 4

- Pinned implementation checkpoint: `00189c590c4233ba787e177224d29cccef47ead5`
- Checkpoint ID: `bmad-12-2-implement-20260821T001329Z-509221`
- Accepted definition boundary: `e997c182a466694bc5eab405bb68fc7396edee53ce7559f443bcbb2e7beaefb6`
- Outcome: `tasks_appended`
- Coverage assessed: 43 Functional Requirements, 9 buildable Success Criteria, 16 acceptance scenarios, 9 Architecture Decisions, 5 Constitution principles, all Story 12.3 exclusions, and all 57 existing tasks.
- Finding count: 2 HIGH partial; 0 missing, contradictory, removed, or unrequested.
- Tasks hash before: `509221c0f7b204e462ab3e719c4159bf47ae85b710d9abfa35903e1230193f2d`
- Tasks hash after: `ba6aec6cd21b1f2f0bf78045d6661fe17eab25c598da9c8a839e912a981ecbdd`
- Diff check: `git diff --check -- specs/011-java-xml-prescan-manifest/tasks.md` passed; only the existing LF/CRLF notice was emitted.

## Prerequisites and hook evidence

- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `specs/011-java-xml-prescan-manifest` and the required `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, and `tasks.md` artifacts.
- `HEAD` was the pinned checkpoint above on `demo0831/story-12-2`. The root-owned `workflow-state.json` was the pre-existing unrelated working-tree change and was not edited.
- `.specify/extensions.yml` contains no `hooks.before_converge` or `hooks.after_converge` entry; no hook ran.
- `.specify/memory/constitution.md` is ratified. All five principles were checked; no Constitution MUST violation was found.
- The refreshed barrier record in `tests/Vulgata.Tests/PreparedScanning/PreparedScanVerificationChecklist.md` reports the accepted prerequisite evidence: PreparedScanning 102 passed/1 intentional skip, PostgreSQL migration 2/2, PostgreSQL atomicity/replay 21/21, real host analysis 19/19, paired release/convergence 7/7, T057 raw/release 10/10, boundary/XML 7/7, production-literal zero-match, zero-error Web build, and the same six unrelated full-suite baseline failures.

## Prior finding closure

- F1 / T053: closed. `PreparedScanPairBuilder` invokes the exact Java range source/provider for oversized Java and emits one closed invalid-range skip; release qualification covers the production Java split path and exact hashes/ranges.
- F2 / T054: closed. `PreparedRelatedFileExpansion` runs after the hard proof gates with the bounded seed, relationship, hop, file, and timeout limits; direct-score fallback emits the deterministic Warning.
- F3 / T055: closed. Candidate/filter/skip, unsafe/bounded XML, split, expansion, and classification outcomes are carried into canonical inventory audits and complete totals before manifest construction; the release/convergence qualification checks these projections.
- F4 / T056: closed for Java. Production DI resolves the real Java 21/JDT-LS `1.61.0-202608141332` StreamJsonRpc `2.25.29` factory through the acquired revision lease. The Java-containing paired qualification produced Ready proof and split units, while XML-only remains `NotApplicable`.
- F5 / T057: closed for per-file continuation. `PreparedSnapshotSource` catches typed raw-artifact rejections and continues safe files; hard-bound outcomes are bounded Warnings, invalid/binary/unsupported artifacts retain closed reasons, no rejected artifact becomes a File Code Unit, and the focused plus production-bound qualification passed.

## Findings

| ID | Gap type | Severity | Source | Evidence | Remaining work |
| --- | --- | --- | --- | --- | --- |
| F6 | partial | HIGH | FR-010-FR-011, FR-031, FR-042; US2/AC1-4; SC-003; AD-2/AD-3 | `src/dotnet/Vulgata.Web/Program.cs` registers the parameterless `CodeGraphAnalysisAdapter`, whose `_options`, `_snapshots`, and `_process` remain null, and no `ICodeGraphRepositorySnapshotResolver` registration exists. `src/dotnet/Vulgata.Web/Data/PreparedScanPairBuilder.cs:125` calls only `BuildCompletenessProofAsync`; it never invokes fresh `IndexAsync`/`RebuildAsync`/`GetStatusAsync`. `BuildCompletenessProofAsync` rejects an unbound adapter and requires `_preparedStatus`, which is populated only by `GetStatusAsync`. The real adapter qualification invokes those operations directly, while `tests/Vulgata.Tests/AnalysisRuntime/PreparedScanRealQualificationTests.cs:258-325` replaces the paired port with a stub. | T058 wires the acquired exact snapshot to a real CodeGraph adapter, runs fresh index/rebuild/status before proof, and proves the actual Java/XML paired path and hard-failure gate with the pinned host runtime. |
| F7 | partial | HIGH | FR-012-FR-015; US2/AC2-3; empty-file edge case; AD-3 | `src/dotnet/Vulgata.Web/Data/PreparedScanPairBuilder.cs:134-136` passes every eligible Java path to readiness after ordinal sorting. `PreparedScanCandidateEnumerator` marks zero-byte Java eligible, and `PreparedScanRawArtifactReader` returns a zero-byte/zero-line artifact. `PreparedJavaReadinessService.IsReadinessCandidate` excludes only `module-info.java` and `package-info.java`; it does not exclude empty files. A lexically first empty Java can therefore be selected and fail document-symbol readiness instead of selecting the next valid file. Existing readiness tests cover XML-only and descriptor properties but no empty-first candidate. | T059 filters empty eligible Java from the readiness candidate set and adds focused plus production-bound qualification proving deterministic selection of the next valid file while preserving XML-only `NotApplicable`. |

No intent-authority defect or new architecture decision was found. Both findings are implementation gaps inside the accepted revision-bound CodeGraph/JDT-LS architecture and existing implementation authorization. This is the fourth consecutive non-converged pass in the converge phase; the root workflow must pause at its required user-approval gate before another implementation correction cycle.

## Append-only task disposition

- Added `T058` and `T059` under new `Phase 12: Convergence` in `tasks.md`.
- Tasks are dependency ordered: real CodeGraph acquisition/readiness composition (T058) precedes the paired Java readiness selection qualification (T059); both remain within US2 and do not absorb Story 12.3 Worker or Document behavior.
- No spec, plan, contract, decision, state, checklist, production source, test source, branch, index, commit, or Git history was changed by this convergence pass.
