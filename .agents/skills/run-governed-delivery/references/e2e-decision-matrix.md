# E2E Decision Matrix

The orchestrator writes every applicable predicate, its evidence, the decision, and the owner to a durable E2E decision artifact. Compact `workflow-state.json` stores only `e2e.result` and `e2e.decision_path`. A true Required row initially makes the gate required. One narrow precedence rule may override that result: a cited existing integration test must prove the full changed behavior across every triggered observable boundary and pass at the current reviewed boundary. With that evidence, the decision artifact records a coverage override and state records `result: skipped-covered`. Partial coverage never overrides a Required row.

| Change type | E2E gate | Owner |
|---|---|---|
| User-visible workflow | Required | `e2e-runner` |
| Authentication/authorization | Required | `e2e-runner` |
| API/real-time/persistence/multi-module integration | Required | `e2e-runner` |
| Schema migration or observable data lifecycle | Required | `e2e-runner` |
| Existing integration test proves full changed behavior | May skip with cited evidence | Orchestrator |
| Documentation-only or behavior-preserving refactor | May skip with rationale | Orchestrator |

## Decision Artifact

The artifact referenced by `e2e.decision_path` contains:

- `required`: boolean conclusion;
- `predicates`: one object per matrix row with `predicate_id`, `change_type`, `gate` (`required` or `may-skip`), `triggered`, repository-relative `affected_paths`, `affected_behavior`, and `evidence` IDs;
- `coverage_override`: null or an object with nonempty `test_ids`, `covered_predicate_ids`, the exact current `source_boundary`, nonempty current-boundary `passing_evidence_ids`, and a `completeness_rationale`;
- `rationale`: why execution or skip is safe;
- `covering_tests`: exact tests and latest passing evidence when an existing integration test justifies a skip;
- `owner`: `e2e-runner` for required execution, otherwise orchestrator;
- `source_boundary`: the same nonrecursive boundary object used by reviews: `base_commit`, nullable `candidate_commit`, `diff_sha256`, `artifact_hashes`, and `owned_paths`;
- `result`: `pending`, `passed`, `failed-product`, `failed-test`, `blocked`, `skipped-no-risk`, or `skipped-covered`;
- `evidence_ids`: command/test evidence.

“Tests exist” is insufficient. A coverage override must identify the tests, cover every triggered Required predicate ID, show that they exercise the full changed behavior across each relevant observable boundary, match the decision's current `source_boundary`, and cite nonempty passing evidence from that boundary. Otherwise `required` stays true. `skipped-covered` is valid only with this complete override.

Persistence and migration/data-lifecycle predicates remain Required unless a complete current-boundary integration test satisfies this override. Story-specific ownership belongs in the accepted tasks and E2E decision rather than an execution-level exception.

## Runner Boundary

`e2e-runner` uses `gpt-5.6-luna` with high reasoning effort. It may add or update E2E, API, or integration test code and run the repository's existing test framework. It may not change production code, alter requirements, accept a failed product behavior, update lifecycle state, stage files, or commit.

When the failure is in product behavior, the root routes raw evidence through regression TDD, reusing the appropriate task group's available/current test author and `tdd-implementer`. An unavailable owner is replaced from the accepted scope, current raw artifacts, prior findings, and RED/GREEN evidence. The affected review axes and E2E gate rerun. When the test itself is defective, only `e2e-runner` corrects it and reruns the gate. Ambiguous failures block rather than invite speculative production edits.

Any added or modified tracked E2E/API/integration test changes the candidate diff identity. It invalidates and reruns the unified quality review against the complete new boundary before the E2E result may advance. Test-only edits receive no review bypass.

Substantive E2E failures use the same unresolved recovery episode and earliest-owner guard as convergence/review. A disproven plan or intent returns upstream before more code repair. Refresh affected lookback evidence before reviewing the corrected candidate; preserve its count through all transitions. Only the final accepted/verified boundary closes a post-implementation episode. Infrastructure failure uses the separately persisted three-root-invocation allowance where retries can help; ambiguous failure remains blocked independently of remaining budget.
