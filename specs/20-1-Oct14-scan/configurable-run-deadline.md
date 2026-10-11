# Configurable run deadline

## Authority and reviewed approach

The user approved making the run deadline configurable, preserving other budgets
and authorization guards, reviewing/testing the change, then starting one fresh
whole scan with a 240-minute limit. Resume remains deferred. This supersedes only
the implementation's fixed run-duration ceilings, not the prior file, provider,
tool, source or publication requirements.

Independent review accepted separating run-owner lifetimes from local operation
limits. Support a finite 1..86400-second configured run deadline with the existing
1800-second default. A neutral Core execution-owner lifetime ceiling is shared by
the policy, source-run owner and CodeGraph prepared owner. The explicit native
preparation overload accepts that lifetime; its default and two-minute preparation
timeout remain unchanged. No new database schema or persistent recovery state.

Generate's authenticated session uses max(one hour, validated run duration),
bounded by the supported day-long ceiling. Other commands retain one hour. Live
subject/security-stamp, purpose, contributor and source authorization checks remain
unchanged; no session is renewed after expiry. Source grants remain ten minutes
and are replaced through existing checks. Source safety margin remains 30 seconds.

The file-attempt, source-window and tool-runtime ceilings remain unchanged. File
requests use the minimum remaining run/file/source window as before. Native
session counts, tool-call limits and all model/token/rate/retry limits remain
unchanged. CL publication receives min(remaining run, existing 30-minute phase
limit), preserving the original hard-stop token and exact freeze guards.

The owned process watcher accepts an explicit run deadline and derives its
watchdog as run deadline plus 60 seconds of process startup/cleanup allowance.
This does not extend the application's dispatch or commit deadline. Existing
direct timeout callers retain their previous default/range. The qualification
launcher reads the new run's policy rather than an independent fixed timeout.

## Ownership and checks

Scan owns policy, orchestration and source/CodeGraph composition. The shared
Core lifetime bound, Infrastructure prepared CodeGraph owner and Web Identity
configuration are narrowly affected shared files; no Business contracts/stores,
migrations or shared Web composition are replaced.

Verification must cover accepted/rejected duration bounds, longer owner lifetime,
unchanged defaults and local budgets, authority expiry/revocation and source
renewal, and original deadline enforcement. Use the official SelfContained runner;
separate controlled results from the subsequent real provider/native scan.
The existing unrelated report-parser failure is not waived or relabelled.

## Verification and runtime evidence

Controlled RED: `isolation-2ef1276029b043409eb3948b0b654727`, 41 selected,
40 passed, one failed: the new four-hour configuration test was rejected by the
old policy ceiling. An earlier sandbox build failed without executing tests;
it is not behavioral RED.

Intermediate focused run `isolation-3d1f73ff6ea94ffab399df2c9a793d2d` selected
1172, passed 1167, failed five. Three were new test setup mistakes (theory marker
and renewal window), one was the obsolete one-hour authority-maximum assertion,
and one was an existing CodeGraph timing assertion. The latter passed the full
run unchanged. No assertions were weakened to hide a production failure.

Full SelfContained `isolation-5337d5c2efa84698a3dcc2d8f193ff3a`: 4900 selected,
4897 passed, three failed, zero skipped. The known unrelated report-parser test
remained failing. Two owned fixture failures were corrected: session expiry is
reported as Expired before approval revalidation, and the synthetic source runtime
must use a short approval window rather than the four-hour owner lifetime.
An intermediate rerun `isolation-2c3738ee3bd4443190eb0ef401defd24` still had the
latter fixture error; no production change was needed for either correction.

Watcher regressions passed using owned child PowerShell processes: original
capture/exit/environment behavior, owned timeout, 14400+60-second watchdog,
conflicting explicit modes and invalid run durations rejected before launch.
During development, assigning the computed watchdog back into the range-validated
TimeoutSeconds parameter failed; the implementation now uses a separate local
watchdog value. The corrected regression run passed with terminating errors enabled.

Final affected GREEN and the authorized real-run launch are recorded below.


Final affected SelfContained `isolation-2876916bce814f18a335c67d0d49fc12`:
143 selected, 143 passed, zero failed/skipped. This includes configuration,
source renewal/session expiry, authority bounds and successful four-hour-policy
runner publication. CodeGraph lifetime checks passed in the full run. Independent
review accepted the final checkpoint after this GREEN. The full suite was not
rerun after test-only corrections; no full-suite GREEN is claimed.

One fresh whole scan is authorized with runDeadlineSeconds=14400. The launcher
asserts unchanged file300s, drain30s, worker1, windows100, requests/file100,
transient retries0, tools32/48, total requests1000000, rate60/minute and total
tokens1000000000000. CL-only, DeepSeek flash, low reasoning, max_tokens393216,
normal retry mode and the same verified77-file source remain selected. New owned
resources will be used; no old acceptance/session/index is adopted.

### Real scan launched

`deadline240-whole-01` started on implementation
`36f19c4e9e3247682b7a1bf16b693a014ec3feec`. Generator binary SHA256
`20E2B6DF1A145D6C809A6C522E4DAF4A86E1713DD8514AAA088CB3A37B6F49B4`
matched the test-output binary. The fresh policy was verified at14400 seconds,
with the unchanged settings above; the watcher reported14460 seconds.
BeginGenerate committed at2026-10-11T06:55:48.3946488Z. This is launch evidence,
not successful generation/publication or long-duration qualification.

- Root: `C:\Users\13728\AppData\Local\Vulgata\OctoberRuns\phone-15bfe12615eb`
- Database: `vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d`
- Batch: `1a327657-6a55-4e65-ba06-6992f423d40e`
- Label/terminal ledger: `TestResults/replay-paid/deadline240-whole-01.json`
  (written when the run ends).
- Live acceptance log: root-relative `logs/generator/log.md`.
- Live correction log: root-relative `logs/generator/worker-json-corrections.jsonl`.

The single authorized run is active. Do not start another run, adopt old accepted
state, rebuild its binaries, or reinterpret this launch as a completed result.
