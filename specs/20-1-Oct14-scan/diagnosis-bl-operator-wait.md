# BL operator-wait diagnosis — October 9

## Evidence boundary

Integrator reported one consumed, approved real BL attempt on `cafedace`, E2E run `3bde4a7267ca4d7d9ecb5c38d50f6a7b`. The complete E2E took 00:03:24.8683785; separate child/phase durations were not retained. Actual child JSON reported exit 3, `run_interrupted`, hidden/empty inventory, accepted files, coverage and Documents, and known usage of one request, 2,095 input and 2,889 output tokens. The test failed at the `InteractionFailure is null` assertion before checking exit code. Request/response presence and the specific interaction failure were not emitted. Owned database, temporary files, container and volume were cleaned. No retry is authorized by that grant.

Scan inspected its clean `oct14/scan` checkpoint `d45c612e`; Generator production sources match `cafedace`. This diagnostic changes tests only. No provider, native tool, PostgreSQL, real source, host or budget renewal was used.

## Bounds and matching mechanism

The fixture configures run 900 seconds, file 480 seconds, and operator selection 120 seconds. Main supplies no caller cancellation token; its operator session lifetime is one hour. Source approval is at most ten minutes. The fixture waits run deadline plus 120 seconds for the child. Ordinary elapsed expiry of those larger bounds does not explain a 204.868-second complete E2E. Per-authority calls have a separate 30-second bound; owner reads generally translate their bounded failures to unavailable results.

`OctoberGenerateRunner.Business.cs:64` limits the interaction to the minimum of the explicit operator wait, remaining run and remaining session. Both it and `OctoberOperatorRootSelection` use a local cancellation deadline. A no-response timeout escapes as `OperationCanceledException`; `OctoberGenerateRunner.cs:145` maps it to `run_interrupted` with disclosure disabled. Metrics still retain observed Worker usage. Thus empty terminal metadata is not independent evidence that no file was accepted or corpus frozen.

The real fixture catches selector/exchange rejection into a failure result, but waits for the generator terminal result. If rejection occurs before writing a response, the child can spend the remaining operator wait without a response and then follow this exact reporting path. The earlier CL-only B route has no operator interaction. The real run's duration is compatible with this mechanism, but cannot prove the missing phase timings or the initiating rejection.

## Controlled reproduction

Two additive cases in `OctoberScanBusinessFinalizationTests` use the existing SQLite/controlled HTTP harness and the actual file broker. Both retain a 900-second run, 480-second file bound and live original caller/session. Only the diagnostic operator bound is one second; this is not a runtime allowance change.

- `operator-broker-no-response`: observe and validate the actual request, deliberately reject selection without publishing a response, then await the broker's own deadline. Assert the current exit-3/`run_interrupted`/hidden-metadata shape, one known controlled Worker request, no Business dispatch, live original caller/session, and owned exchange cleanup. Independently query the owned SQLite store: **one accepted file, one frozen corpus, zero publications**. The report assertions describe observed behavior, not a desired future timeout contract.
- `operator-broker-valid`: select the exact offered unit and atomically publish the valid response; assert normal publication through the same composition.

Run `8a9ce4e107784656bedc3b488934c663` used `eng/test.ps1 -Suite SelfContained -Filter 'DisplayName~operator-broker-no-response|DisplayName~operator-broker-valid' -TestTimeoutSeconds 300 -CommandTimeoutSeconds 900`: **2 selected, 2 passed, zero failed/skipped**, current-source build and policy/guard checks passed. The previously recorded short process-only PATH was used; no persistent setting changed. Independent review accepted the paired diagnostic.

This confirms a matching controlled mechanism, not the real fixture's initiating cause. Integrator owns bounded, sanitized request-presence/phase/rejection/timing evidence before cleanup and any later explicitly approved real run. No production correction or deadline increase is justified solely by this reproduction.
