# QR-PSI-002 public activation RED/GREEN

- Story: 12.5
- Accepted scope: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`
- Finding: `QR-PSI-002`
- Owner boundary: T025-T029 Prepared Pass 1 identity/composition only

## Honest RED

`PostPreparedScan_HostilePass1ResourceActivation_Returns503WithoutSideEffectsAsync`
composed the real `EmbeddedPreparedPass1ContractIdentitySource` through the
public authenticated endpoint with a missing embedded resource. The current
constructor validated eagerly during scoped DI activation, so the request
returned HTTP 500 instead of the accepted sanitized HTTP 503. Fresh
authorization, initiation-store, runtime/preparation and durable-row counters
all remained zero.

Focused result: `1 failed`; actual `500`, required `503`.

## Minimal GREEN

The Agents-owned source now captures its resource providers in the constructor
and performs the unchanged strict validation in `GetCurrent`. This lets the
existing coordinator catch/map missing, duplicate, empty, invalid UTF-8 and
schema-inconsistent resources to `Unavailable`, which the existing endpoint
publishes as HTTP 503 with code `prepared-scan-initiation-unavailable`.

No Web, Core, Infrastructure, prompt, dispatch, provider, tool, migration,
DbContext, Story 12.4, CGC, legacy Scan or later-stage production behavior
changed.

Results:

- source/coordinator/public API boundary: `41/41` passed;
- broader initiation/atomic/store/runtime boundary: `116 passed`, `1` known
  environment-dependent concurrency skip, `0` failed;
- Track A Scan/release contracts: `121/121` passed;
- prepared-chat product boundary excluding the pre-existing prohibited-legacy
  profiling fixture: `455/455` passed;
- Web build: `0` errors, `9` existing warnings;
- `git diff --check`: clean (line-ending information only).

The broad `FullyQualifiedName~Chat` diagnostic selected one pre-existing
`DemoChatReleaseGatePostgreSqlTests` assertion that requires the prohibited
legacy `IRepositoryProfilingCoordinator`; it failed independently of this diff
and was not treated as a candidate product regression.
