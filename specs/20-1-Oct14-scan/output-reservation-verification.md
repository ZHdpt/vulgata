# Fixed completion allowance and bounded POM qualification

## Authority and scope

On October 10, 2026 the user approved the recommendation following consultation
with `Deepseek JSON Truncation Fixes`: separate byte safety from output admission,
preserve a full 8,192-token output allowance, verify locally, then repeat the
isolated POM trial. Work started on `oct14/scan` at clean `c61afa29` in the assigned
worktree. No skills, Integrator messages, provider/model switches, prompt changes,
thinking changes, schema changes or broader S3 work are part of this correction.

The preceding POM trial ended with Length at exactly 4,011 output tokens, all
reported as reasoning. Its effective cap was reduced by the existing byte-based
input reservation; this does not explain historical Stop/malformed-JSON failures.
See [prior evidence](reasoning-replay-verification.md).

## Accepted narrow correction

`OctoberFileWorkerLimits.MaximumOutputTokensPerRequest` explicitly selects 1..8192
tokens, default 8192. The Worker reserves the unchanged input proxy plus that full
allowance using checked long arithmetic; insufficient per-request capacity fails
before scope admission or transport. It never silently reduces the output cap.
Generate binds a 40,960 per-call reservation ceiling, still capped by the existing
cumulative policy. The standalone Worker's total default stays 32,768.

Input byte measurement and cumulative byte guards remain unchanged. The byte-based
token reservation is retained as an explicit proxy, not relabelled as a tokenizer
or proven provider-context upper bound. For the prior POM input proxy, the required
reservation is 28,757 + 8,192 = 36,949. A larger history may still fail admission;
that is a bounded failure, not permission to grow budgets automatically.

Output allowance covers reasoning and answer together. Existing scope settlement
charges actual input plus output, without adding reasoning twice; missing usage
retains its reservation and blocks new admission. Actual usage above reservation
is fully recorded/debited and blocks subsequent admission. No accounting, source
authority, cancellation, lease, byte, tool, deadline, acceptance or publication
guard is relaxed. Current DeepSeek documented capacity accommodates this allowance;
documentation is not a live account fingerprint or proof that 8192 is sufficient.

Separate test ownership and independent review are retained. Final controlled
results and any real qualification are recorded below only after execution.

## Controlled verification

- RED `isolation-6d0177329457498fb4e7b4bb94390fce`: 10 selected, 7 failed,
  3 passed. The Generator history case received 1307 instead of 8192 output
  tokens; insufficient reservations silently shrank rather than rejecting, and
  explicit small/invalid output settings were ignored.
- Focused GREEN `isolation-9b7df1e435d34f988c7b9f0ab43deed4`: **423/423 passed**,
  zero skips, through `eng/test.ps1`. Tests cover exact fit/one-less, explicit
  137-token allowance, growing reasoning/tool history, invalid output and total
  limits, actual wire cap equality, Generator 40960 composition and preservation
  of a smaller cumulative ceiling. Existing byte/authorization/cancellation and
  settlement assertions remain unchanged.
- Separate test-owner final assessment and independent code review found no
  findings. Full-suite verification and the real POM trial remain separate gates;
  the controlled result is not generated Knowledge or runtime qualification.

Full SelfContained `isolation-dd3f6ad3820b4eec87111f10e3906225` completed with
**4,730/4,732 passed**, two failures, zero skips. All new fixed-output cases passed.
The full suite is not GREEN:

- The previously recorded `OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`
  exact report-shape mismatch remains unchanged.
- `OctoberScanOperatorExchangeTests.AtomicResponse_UsesExactOriginalRequestAndExplicitFiniteSelection_ThenCleansAsync(empty: true)`
  failed with `OctoberOperatorCleanupUnconfirmedException`, caused by a Windows
  file-sharing violation while opening the response object for cleanup. This
  unchanged BL selection path is not used by the CL-only trial.

The unchanged focused rerun of both operator-exchange theory cases passed **2/2**,
zero skips, in `isolation-45151b202b354daea5f4c308f067ca86`. No test assertion or
timeout changed. The rerun does not erase the full-suite failure or establish its
cause. Independent final review accepted the scoped correction, retaining both
full-suite limitations; this is not release-gate acceptance or a waiver.

## Bounded real-trial preparation

The user's current instruction supplies the fresh POM-trial authority. Reused
applicable source/provider/resource approvals are recorded in the retained-pilot
plan and prior replay evidence. Only the completion reservation changes: fixed8192
output within the new40960 per-call bound. Thinking, effort, prompt, tools, provider,
model, cumulative policy, deadlines, byte limits and no-retry policy stay unchanged.

A fresh owned database/workspace was prepared for target `phone-b463339e9bcd`,
batch `ce9aa64a-2077-40cf-abb5-dd3284b4e7ee`. All77 approved manifest hashes matched;
only `mbs_channel/pom.xml` is selected, with the rest supporting. Prepare and select
passed without provider calls. The temporary loopback bootstrap host was stopped.
Prior successful/failed databases and the existing container/volume are preserved.
Runtime physical bindings and protected keys remain outside Git. No raw provider
response/reasoning capture is enabled. Generation results follow after invocation.
