# T020-T022 Prepared Scan tuple compatibility evidence

T020 recorded the expected compile RED after removal of the tuple-less builder
shape: four `CS1503` diagnostics in the accepted Prepared Scan API, release and
real-qualification fixtures.

T021 changed only those fixture/composition callers to construct and pass the
immutable `PreparedScanInitiationTuple`. The test project then built with zero
errors. The tuple-only seam guard remained RED until T027 removed the obsolete
tuple-less `PreparedScanCoordinator.PrepareAsync` method; no fallback overload
was introduced.

Focused results observed across the four compatibility suites and the new
coordinator suite:

- release qualification: 8 passed;
- Prepared Scan boundary: 5 passed;
- initiation coordinator after T025: 14 passed;
- API/real qualification assertions unrelated to host tooling passed;
- four host-only cases remain excluded from a GREEN claim because the approved
  JDT-LS archive/readiness prerequisite is absent in this execution
  environment. They failed with `jdt_distribution_archive_missing` or the
  existing real JDT-LS readiness failure, not tuple compatibility behavior.

No accepted acquisition, CodeGraph, JDT-LS, XML, selection, timeout,
determinism, failure or qualification assertion was removed. T022 remains a
pending final verification marker until the host-only prerequisites are
available or T028 records the accepted conditional environment disposition.
