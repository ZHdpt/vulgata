# E2E Decision Matrix

The orchestrator records every applicable predicate, its evidence, the decision, and the owner in `workflow-state.json`. A true Required row initially makes the gate required. One narrow precedence rule may override that result: a cited existing integration test must prove the full changed behavior across every triggered observable boundary and pass at the current reviewed boundary. With that evidence, the orchestrator records a coverage override and sets `required: false`, `result: skipped-covered`. Partial coverage never overrides a Required row.

| Change type | E2E gate | Owner |
|---|---|---|
| User-visible workflow | Required | `e2e-runner` |
| Authentication/authorization | Required | `e2e-runner` |
| API/SignalR/persistence/multi-module integration | Required | `e2e-runner` |
| EF migration or observable data lifecycle | Required | `e2e-runner` |
| Existing integration test proves full changed behavior | May skip with cited evidence | Orchestrator |
| Documentation-only or behavior-preserving refactor | May skip with rationale | Orchestrator |

## Decision Record

The record contains:

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

Under `expedited-mvp`, persistence and migration/data-lifecycle predicates remain Required unless a complete current-boundary integration test satisfies this override. Story 11.1 therefore retains persistence and migration E2E responsibility. The clean-database two-Repository demonstration and final Docker release verification belong to Story 11.5; Story 11.1 runs Docker only if it changes container/runtime behavior.

## Runner Boundary

`e2e-runner` uses `gpt-5.6-terra` with low reasoning effort. It may add or update E2E, API, or integration test code and run the repository's existing test framework. It may not change production code, alter requirements, accept a failed product behavior, update BMad lifecycle state, stage files, or commit.

When the failure is in product behavior, the root sends raw evidence to a fresh `tdd-implementer`; the affected review axes and E2E gate rerun. When the test itself is defective, only `e2e-runner` corrects it and reruns the gate. Ambiguous failures block rather than invite speculative production edits.

Any added or modified tracked E2E/API/integration test changes the candidate diff identity. Full invalidates and reruns both acceptance and quality verdicts; Standard invalidates and reruns its combined quality verdict against the complete new boundary before the E2E result may advance. Test-only edits receive no bypass.
