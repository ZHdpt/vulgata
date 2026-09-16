---
name: governed-tdd
description: Apply governed behavioral RED/GREEN discipline and final-shape assessment when run-governed-delivery assigns implementation or correction work.
---

# Governed Test-Driven Development

Use behavioral RED, minimal GREEN, and final-shape assessment for a coherent task group. Passing tests are necessary, not sufficient for completion. Read the applicable guidance before and during the loop.

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

- **Red before green.** Write the failing test first, then only enough code to pass it. Minimal GREEN limits speculative behavior and unnecessary scope, not code quality. Preserve the original raw RED observation unchanged. Already-correct behavior needing only coverage records no expected RED instead of fabricating one.
- **One slice at a time.** One seam, one test, one minimal implementation per cycle.
- **Assess final shape before completion.** Both authors inspect their final affected units in context, not just changed lines or passing commands. Production design should have cohesive responsibilities, meaningful names, understandable control flow, and justified abstractions, consistent with the accepted plan and applicable repository/language/framework conventions. Test design should have clear scenarios, independent expectations, understandable setup, useful failures, appropriate fixtures, and reliable isolation. Correct concrete local defects; do not mandate patterns, private-method layouts, or universal size limits.
- **Refactor conditionally while GREEN.** Assessment is required; refactoring is not. No-refactor-needed is valid, and necessary local edits have no arbitrary cleanup count. Stay inside owned paths and preserve behavior, public contracts, schema, migrations, dependencies, architecture, and authority. Test refactoring also preserves intended assertions and coverage. Refactor evidence accounts for every protected dimension; rerun focused and surrounding tests after changes. Record minimal-GREEN and final-GREEN evidence separately, including refactor evidence when applicable. Routine private decomposition stays local; consequential choices outside the accepted design return through root to plan.

## Ownership and continuity

Root retains one `tdd-test-author` (Luna/high) and one `tdd-implementer` (Sol/medium) for the coherent task group, using stage-scoped follow-ups and reusing available/current owners for later corrections. Test and production writes stay disjoint. An unavailable replacement receives raw artifacts, accepted scope, prior findings, and actual RED/GREEN evidence; runtime handles never enter durable state.

The implementer may challenge a defective or overconstrained test through root with evidence, but never edit or weaken it. The test owner may correct an independently evidenced test defect as dispatched, preserving the historical RED and recording the corrected expectation and applicable fresh checks; it must not weaken tests to fit faulty production behavior. Root alone controls lifecycle/workflow state, authoritative evidence, approvals, checkpoints, staging, and commits. An E2E product failure returns through root to this same regression-TDD correction path, not directly to a fresh production author.
