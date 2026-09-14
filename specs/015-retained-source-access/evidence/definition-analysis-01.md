# Specification Analysis Report

| ID | Category | Severity | Location | Summary | Recommendation |
|---|---|---|---|---|---|
| C1 | Constitution alignment | CRITICAL | tasks.md:23-31,39-42,78-90,135-149 | Several observable implementations precede their behavioral RED; trusted-workspace behavior lacks an explicit prior test. | Reorder or split tasks into public-seam RED -> minimal GREEN -> surrounding verification. |
| I1 | Architecture seam | HIGH | tasks.md:28,89 | T005 places trusted-workspace access in Core-facing interfaces despite the accepted Infrastructure-only boundary. | Keep the provider and physical lease entirely in Infrastructure. |

## Coverage summary

| Requirement key | Has task? | Task IDs |
|---|---|---|
| FR-001 | Yes | T009, T013-T014 |
| FR-002 | Yes | T009, T013-T014 |
| FR-003 | Yes | T004, T009, T011, T014 |
| FR-004 | Yes | T003-T004, T007, T009, T012, T014, T024, T028 |
| FR-005 | Yes | T004, T010, T012, T014, T025, T028-T029 |
| FR-006 | Yes | T005, T017-T018, T020-T022 |
| FR-007 | Yes | T005, T017, T021 |
| FR-008 | Yes | T007, T009, T011, T018, T021 |
| FR-009 | Yes | T007, T012, T017, T021 |
| FR-010 | Yes | T013, T017, T019, T022 |
| FR-011 | Yes | T017, T019, T021 |
| FR-012 | Yes | T003, T005, T018-T019, T021, T030, T036 |
| FR-013 | Yes | T018, T020 |
| FR-014 | Yes | T010, T012, T018, T020, T025, T029-T030 |
| FR-015 | Yes | T026, T028, T031 |
| FR-016 | Yes | T026, T030-T031 |
| FR-017 | Yes | T008, T024-T025, T027-T029 |
| FR-018 | Yes | T008, T024-T025, T027-T028 |
| FR-019 | Yes | T004, T010, T013-T014, T022, T025, T036 |
| FR-020 | Yes | T010, T014, T022, T025, T029 |
| FR-021 | Yes | T002, T026, T031, T033 |
| FR-022 | Yes | T002, T034-T035, T038 |
| SC-001 | Yes | T009, T011-T012, T014, T016 |
| SC-002 | Yes | T009, T013-T014, T016 |
| SC-003 | Yes | T009, T011-T012, T016-T018, T021, T023 |
| SC-004 | Yes | T017-T023, T036 |
| SC-005 | Yes | T010, T018, T020, T023 |
| SC-006 | Yes | T026, T031-T032 |
| SC-007 | Yes | T025, T028-T032 |
| SC-008 | Yes | T010, T025, T029, T032 |
| SC-009 | Yes | T001-T002, T026, T031, T033, T035 |
| SC-010 | Yes | T002, T034-T035, T037-T038 |

Constitution alignment has one blocking issue: Principle IV's behavioral RED-before-GREEN MUST is not represented by the current dependency order. No specification or accepted-plan authority conflict was found.

Unmapped tasks: none. All tasks trace to an FR, SC, plan obligation, migration/qualification requirement, or final governed verification.

Metrics: 32 buildable requirements; 38 tasks; 32 requirements with at least one task (100% coverage); 1 ambiguity/inconsistency finding; 0 duplication findings; 1 Critical finding; 1 High finding; 0 unresolved placeholders.

Prerequisite command: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks`

Result: FEATURE_DIR resolved to G:/source/repos/vulgata/specs/015-retained-source-access; AVAILABLE_DOCS: research.md, data-model.md, contracts/, quickstart.md, tasks.md.

Hook disposition: .specify/extensions.yml parsed successfully. It defines only after_specify and after_plan; no before_analyze or after_analyze hook is registered, so no hook ran.

Boundary verification: all 20 manifest hashes matched current bytes, and canonical JSON recomputation produced 2644ebf01e273507b052a59c5f1e90a6062ab0dc76b4ed359cc8822de38c5bb7. No build, test, runtime, database, provider, lifecycle, file-write, staging, commit, or Git-history action was performed by the reviewer.
