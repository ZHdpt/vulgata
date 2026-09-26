---
name: governed-tdd
description: Apply governed behavioral RED/GREEN discipline and final-shape assessment when run-governed-delivery assigns implementation or correction work.
---

# Governed Test-Driven Development

Use behavioral RED, minimal GREEN, and final-shape assessment for changed production behavior in a coherent task group. Apply the routing below before entering the loop; passing tests alone do not establish completion.

## Choose the verification route

Route by the accepted behavior and risk being changed, not file extension or the existence of a task. State the route and reason briefly in the existing task/dispatch evidence; add no separate gate or ledger.

- Changed production behavior: behavioral RED/GREEN at an accepted seam. This includes behavior implemented in scripts, configuration, or integration glue.
- Already-correct behavior needing coverage: test-only work with passing tests and a coverage reason; never fabricate RED.
- Behavior-preserving refactor: use existing regression coverage, adding characterization tests where the seam is unprotected; verify before and after, without inventing a behavior change.
- Nonbehavioral work: bounded normal execution plus direct verification appropriate to the risk. Documentation, dependency pins, supplied binaries, generated files, packaging, and environment setup do not automatically need new tests. Check affected build, integration, startup, or installation behavior where relevant; these categories are not exemptions when behavior actually changes.

Before adding a test, identify the accepted obligation, observable outcome, and meaningful regression it would catch. Reuse adequate existing coverage. Never invent a requirement, test, production change, or failing assertion solely to supply RED/GREEN evidence. If the required behavior cannot be exercised, report the verification limitation through root rather than disguising it with a proxy test.

Third-party artifact hashes are not product behavior by themselves. Only add integrity-verification behavior when required by accepted scope or repository authority; test our verifier's acceptance/rejection behavior using controlled fixtures. An authorized installation/release integrity check may be direct operational verification, not a permanent product regression test. Do not pin arbitrary vendor bytes, generated output, or source text merely to detect any change. This does not prohibit required integrity checks or workflow evidence hashes that bind reviewed artifacts.

When exploring the codebase, read `CONTEXT.md` (if it exists) so test names and interface vocabulary match the project's domain language, and respect ADRs in the area you're touching.

## What a good test is

Tests verify behavior through public interfaces, not implementation details. Code can change entirely; tests shouldn't. A good test reads like a specification — "user can checkout with valid cart" tells you exactly what capability exists — and survives refactors because it doesn't care about internal structure.

See [tests.md](tests.md) for examples and [mocking.md](mocking.md) for mocking guidelines.

## Seams — where tests go

A **seam** is the public boundary you test at: the interface where you observe behavior without reaching inside. Tests live at seams, never against internals.

**Test only at pre-agreed seams.** The accepted definition and implementation approval confirm every seam disclosed in the plan. Before writing a test, name the accepted seam and cite it. Return a missing or materially different required seam through root to plan rather than inventing an internal interface during RED authoring; root applies the existing authority gates.

Ask: "Which accepted public interface makes this behavior observable?"

When the shape of that interface is itself in question, return to the plan-contained [implementation design contract](../run-governed-delivery/references/implementation-design.md). Use an installed `codebase-design` skill when available for module, interface, depth, seam, adapter, leverage, and locality vocabulary.

## Anti-patterns

- **Implementation-coupled** — mocks internal collaborators, tests private methods, or verifies through a side channel (querying the database instead of using the interface). The tell: the test breaks when you refactor but behavior hasn't changed.
- **Tautological** — the assertion recomputes the expected value the way the code does (`expect(add(a, b)).toBe(a + b)`, a snapshot derived by hand the same way, a constant asserted equal to itself), so it passes by construction and can never disagree with the code. Expected values must come from an independent source of truth — a known-good literal, a worked example, the spec.
- **Horizontal slicing** — writing all tests first, then all implementation. Bulk tests verify _imagined_ behavior: you test the _shape_ of things rather than user-facing behavior, the tests go insensitive to real changes, and you commit to test structure before understanding the implementation. Work in **vertical slices** instead — one test → one implementation → repeat, each test a **tracer bullet** that responds to what the last cycle taught you.

## Rules of the loop

The RED/minimal-GREEN loop below applies to changed behavior. Other routes use their evidence requirements above; dispatch only needed owners, without a test-author turn solely for ceremony.

- **Red before green.** Write the failing test first, then only enough code to pass it. Minimal GREEN limits speculative behavior and unnecessary scope, not code quality. Preserve the original raw RED observation unchanged. Already-correct behavior needing only coverage records no expected RED instead of fabricating one.
- **One slice at a time.** One seam, one test, one minimal implementation per cycle.
- **Assess final shape before completion.** Both authors inspect their final affected units in context, not just changed lines or passing commands. Production design should have cohesive responsibilities, meaningful names, understandable control flow, and justified abstractions, consistent with the accepted plan and applicable repository/language/framework conventions. Test design should have clear scenarios, independent expectations, understandable setup, useful failures, appropriate fixtures, and reliable isolation. Correct concrete local defects; do not mandate patterns, private-method layouts, or universal size limits.
- **Refactor conditionally while GREEN.** Assessment is required; refactoring is not. No-refactor-needed is valid, and necessary local edits have no arbitrary cleanup count. Stay inside owned paths and preserve behavior, public contracts, schema, migrations, dependencies, architecture, and authority. Test refactoring also preserves intended assertions and coverage. Refactor evidence accounts for every protected dimension; rerun focused and surrounding tests after changes. Record minimal-GREEN and final-GREEN evidence separately, including refactor evidence when applicable. Routine private decomposition stays local; consequential choices outside the accepted design return through root to plan.

## Ownership and continuity

For a behavioral TDD task group, root retains one `tdd-test-author` (Luna/high) and one `tdd-implementer` (Sol/medium), using stage-scoped follow-ups and reusing available/current owners for later corrections. Test and production writes stay disjoint. An unavailable replacement receives raw artifacts, accepted scope, prior findings, and applicable verification evidence; runtime handles never enter durable state.

The implementer may challenge a defective or overconstrained test through root with evidence, but never edit or weaken it. The test owner may correct an independently evidenced test defect as dispatched, preserving the historical RED and recording the corrected expectation and applicable fresh checks; it must not weaken tests to fit faulty production behavior. Root alone controls lifecycle/workflow state, authoritative evidence, approvals, checkpoints, staging, and commits. An E2E product failure returns through root to this same regression-TDD correction path, not directly to a fresh production author.
