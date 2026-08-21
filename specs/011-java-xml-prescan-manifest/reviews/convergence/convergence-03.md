# Story 12.2 Convergence — Iteration 3

- Pinned implementation checkpoint: `a3b1636c391ecdbe8b97773bbc0837e4d4a51056`
- Checkpoint ID: `bmad-12-2-implement-20260820T180826Z-a68724`
- Accepted definition boundary: `e997c182a466694bc5eab405bb68fc7396edee53ce7559f443bcbb2e7beaefb6`
- Outcome: `tasks_appended`
- Coverage assessed: 43 Functional Requirements, 9 buildable Success Criteria, 16 acceptance scenarios, 9 Architecture Decisions, 5 Constitution principles, and Story 12.3 exclusions.
- Tasks hash before: `A68724FA5DD361E75E862919884B6667FBE79262D841AF020C30C20C7C0F7287`
- Tasks hash after: `4F733252221548E7A8D3B57951530B97C5FB82872434011ACF19D8EBAF405D78`
- Diff check: `git diff --check -- specs/011-java-xml-prescan-manifest/tasks.md` passed; only the repository's existing LF/CRLF notice was emitted.

## Prerequisites and hook evidence

- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned feature directory `specs/011-java-xml-prescan-manifest` and the required `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, and `tasks.md` artifacts.
- The pinned source is `HEAD` at `a3b1636c391ecdbe8b97773bbc0837e4d4a51056`; the root-owned `workflow-state.json` remains the only unrelated working-tree change.
- `.specify/extensions.yml` has no `hooks.before_converge` or `hooks.after_converge` entry; no hook ran.
- Constitution is ratified, not a template. Its five principles were checked; no MUST violation was found.

## Prior finding closure

- F1 / T053: closed. The production paired builder calls the revision-bound JDT source for oversized Java, maps exact UTF-16 top-level spans to raw-byte ranges, and emits one closed invalid-range skip rather than partial units. Release qualification covers the production Java path.
- F2 / T054: closed. Expansion runs only after hard readiness with the bounded 20-seed, Calls/Imports/Inherits, two-hop, 200-file, five-second contract and deterministic Warning fallback.
- F3 / T055: closed for inventory outcomes that reach the prepared snapshot. Filtered entries, safe-reader XML outcomes, split outcomes, expansion fallback, and classification totals are projected through canonical audits and are covered by the corrected release/convergence qualification.
- F4 / T056: closed. Production DI now resolves the real Java 21/JDT-LS `1.61.0-202608141332` StreamJsonRpc 2.25.29 stdio factory with the acquired revision lease; the actual Java-containing paired builder produced Ready proof and split units, while XML-only remains `NotApplicable`.

## Findings

| ID | Gap type | Severity | Source | Evidence | Remaining work |
| --- | --- | --- | --- | --- | --- |
| F5 | partial | HIGH | FR-006, FR-016–FR-018, FR-025–FR-028; US3/AC3 and US4/AC2; `safe-analysis-contracts.md` Candidate artifact and Hard limits; AD-1/AD-4/AD-5 | `PreparedScanRawArtifactReader.ReadAsync` checks `before.Length > 4,194,304` and throws, and also throws for binary/invalid Java or unsupported XML encoding. `PreparedSnapshotSource.ReadAsync` has no per-file recovery; its outer catch returns an empty file list with `prepared-snapshot-read-failed`. Thus the remaining file is not represented by a closed audit, and safe files cannot continue to a paired manifest. The contract requires oversized bytes to be audited/skipped before parsing and every exceeded bound to emit one deterministic per-file outcome; the spec requires invalid inputs to be explicitly audited and oversized files to be wholly skipped without partial units. | Change the exact snapshot source/read seam to retain one closed per-file `Skipped`/bounded `Warning` record for oversized, binary, invalid-encoding, or otherwise unreadable eligible artifacts, preserve complete candidate/retained totals, continue safe files, and qualify that no skipped file becomes a File Code Unit. |

No missing, contradictory, removed, or unrequested finding was identified. No intent-authority defect or new architecture decision was found; F5 is an implementation correction within the accepted exact-source/audit architecture and existing implementation approval.

## Coverage and handoff

The complete intent inventory was rechecked against the accepted spec, plan, all contracts and decisions, research/data model/quickstart, Constitution, prior convergence reports, implementation evidence, verification checklist, production source, and test source at the pinned boundary. T057 was appended as the next sequential task under a new append-only Phase 11 in `tasks.md`. It is the only actionable task from this pass; run its TDD RED/GREEN correction, then rerun the final barrier and convergence.
