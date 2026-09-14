# Task-author correction handoff checks

Kind: author-output validation (not an independent gate verdict)
Story: 13.1
Input: tasks.md SHA256 5cb6ad8ed6077f1b50d6597787898232effbffc3e8dba7fe84f8c208812c875a4

The task author reported PASS, but the returned file is not ready to pin for independent re-review:

1. Checklist IDs occur T001-T008, T039-T044, T009... instead of sequential execution order required by speckit-tasks. Renumber all tasks/references coherently before return.
2. T039 says T040 verifies policy but T040 implements manifest. T040 says T041 verifies manifest but T041 implements EF. Correct all RED/GREEN/verification references.
3. T006/T007/T008 all write separate RED suites, then T039/T040/T041 implement them, then verification follows. This still batches test and production horizontally. Express ordered coherent RED -> minimal GREEN -> verification groups, one behavior microcycle at a time.
4. T044 promises PostgreSQL/delete GREEN before migration T027, repository T028 and public Chinese 409 implementation exist. T016 promises completed atomic capture before publication implementation T028/T029. Move required production persistence/publication/deletion into the appropriate earlier tested vertical slice; later durability tests should extend a working published capture.
5. Parallel examples T004/T005 both edit RetainedSourceContracts.cs; T020/T021 both edit RetainedSourceReadContext.cs. Remove these parallel claims and conservatively serialize shared production files.
6. Add an explicit production task for the already-approved Chinese 409 mapping in RepositoryApiEndpoints.cs and RepositoryManagementCoordinator.cs, with its RED first; mentioning those paths in a test is not implementation coverage.
7. T026 still says existing executable entry point without proving one exists. Use a concrete separate OS-process approach within the current single test project, such as an explicitly selected filtered test-host invocation, and name exact test helper files/runner arguments without inventing another project.
8. Public-seam behavioral assertions must fail at runtime before behavior implementation. Compile failures are intermediate diagnostics only. Do not make codec-private-method tests substitute for capture/open behavior or fabricate RED for later test-only coverage.

Keep spec/plan/approval unchanged. This is continued correction of the first definition revision, not a new independent REVISE attempt. Rewrite the task list coherently rather than appending patch IDs to its stale phase order.
