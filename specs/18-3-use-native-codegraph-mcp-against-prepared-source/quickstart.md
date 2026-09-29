# Later Validation Guide: Story 18.3

This is a future validation guide for the accepted plan. No runtime or test qualification occurred during the plan-only run. Exact test filters, launch command and installed CodeGraph version must be taken from the later implemented host, not guessed here. [Spec success criteria](spec.md); [testing guidance](../../docs/agents/testing.md).

## Prerequisites

1. Integrate and verify Story 18.1's approved prepared-root/tool binding. Reconcile [plan ID-I01/T03](plan.md) and rerun affected definition gates before implementation.
2. Use an explicitly owned small fixture with known source location plus a separate foreign root; run owned processes directly on the selected October host. Confirm the actual declared CodeGraph installation and native MCP client/launch mode. Docker application/tool packaging is outside this qualification; no model/provider call is needed. [October proposal item 11](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md).
3. Apply a reviewed purpose-scoped native read-tool grant and bounded output/deadline policy. Do not use a simulated response as native evidence.

## Controlled verification

After implementation, use the existing `tests/Vulgata.Tests` harness to cover grant filtering, fixed-root and nested-path override denial, stale binding, concurrent roots, result limits, distinct failures and cancellation cleanup. Record honest RED/minimal GREEN/final GREEN under the governed TDD route. A relevant command shape is `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter <implemented Story 18.3 test name>`; replace the placeholder with the actual test name rather than treating it as a runnable command today.

## Native host smoke

Record the actual CodeGraph version, host configuration identity without secrets, native discovery result, one useful native invocation and source-relative location on the approved fixture. Attempt a foreign root/path override and observe denial. Exercise preparation, connection and invocation failures plus timeout/cancellation with only owned processes; verify independent 18.1 file-tool access remains possible. Distinguish readiness from semantic completeness and native proof from CLI/JSON, fixture, mock or provider behavior. Stop and report a blocker if the selected host cannot make a genuine native connection. [O-AR09](../../docs/bmad/planning-artifacts/october-release-epics.md); [contract](contracts/native-codegraph-boundary.md).
