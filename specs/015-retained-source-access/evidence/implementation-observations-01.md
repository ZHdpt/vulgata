# Implementation observations for later verification

These are root inspection notes, not an independent review verdict or permission to bypass TDD.

- Source policy normalization currently trims all directory separators, so a filesystem-root value can cease to be fully qualified. Add a focused configuration boundary test before fixing if confirmed (RetainedSourceCapturePolicy.NormalizeRequiredRoot).
- Source policy converts arbitrary positive Int64 deadline/stale values to TimeSpan without mapping representability overflow to source_configuration_invalid. Test the invalid extreme-value path before correction if confirmed.
- PostgreSQL test helper hardcodes port5511. Current owned runtime uses that port, but future isolated runtime ports should come from explicit test configuration while keeping loopback/unique-name safeguards. This is test-fixture portability, not production capability.
- Add an explicit cancellation-at-prepublication-check test if the current cancellation decorator only cancels after initial authorization; do not treat early cancellation as proof of the final publication boundary.

All remain scoped to the accepted Story13.1 contracts and should be resolved through the relevant test/implementation or verification work before final acceptance.
- Initial capture chain retains `byte[] Content` for every committed entry and writes blobs with WriteAllBytesAsync. This conflicts with plan's bounded streaming/no whole-snapshot buffering requirement. Route a measurable public resource-bound test or appropriate characterization to test author before correction; do not claim streaming qualification from small fixtures.
- Initial capture service checks live authority/deadline before acquiring the identity lock, then may wait and seal before non-cancellable DB insertion. Exercise revocation/cancellation/deadline at the actual final publication boundary and replay, not only before lock acquisition. Plan requires final fresh checks before linearization.
- Capture deadline currently checks elapsed time around Git calls but does not visibly cancel a stalled Git operation at the configured deadline. Verify bounded completion with a controlled owned Git transport/process fixture, and route any behavioral failure through TDD.
- After root-preserving normalization, Contains still appends a separator unconditionally. Test configured filesystem root versus a tool subdirectory: a doubled separator may defeat root-overlap rejection. Include this policy containment boundary in subsequent regression work.

Current dispositions after capture checkpoint dede5daa:
- Root normalization and unrepresentable capture/stale durations: observed3RED, minimal policy correction, independently GREEN.
- Active deadline on stalled Git: observed actual endpoint/watchdog RED, monotonic monitor correction, independently GREEN.
- Final authority after identity-lock wait: observed Completed/visible-row RED after revocation, final fresh check correction, independently GREEN. Final-publication cancellation characterization also GREEN.
- Filesystem root/excluded-child overlap: observed no-exception RED, minimal prefix correction, producer and independent7/7boundary GREEN.
- Streaming/no whole-snapshot buffering: observed344,123,784 allocated bytes for64MiB payload RED; streaming correction independently measured78,413,312 bytes below128MiB ceiling. Resource1, boundary7, capture27, PostgreSQL4 and surrounding16 all GREEN; T019 checkpoint bound.
- PostgreSQL helper port portability remains pending before final qualification.

Read-context first-slice observations (not an independent review verdict):
- The accepted ordinary-operation protocol requires row/manifest revalidation per call. First implementation reads cached manifest metadata after authorization; test post-open tampering before claiming T025 complete.
- Metadata/list post-authorization cancellation must discard payload even when cancellation is signaled immediately after the verifier returns. Test the public operation boundary before correction.
- Disposal/read concurrency and active read deadlines remain scheduled for the later read/concurrency groups; initial open/list GREEN is not those qualifications.
- For T031 replay qualification, compare all stable public receipt fields, including CompletedAt: PostgreSQL timestamp precision can differ from an in-memory DateTimeOffset immediately after insert. Confirm actual behavior before any correction.
- T026/T031 concurrent tests should admit multiple simultaneous operations, not only reject all but one by budget. Verify real repository/context composition tolerates concurrent access and returns the promised logical outcomes; do not assume a scoped EF context is safe for concurrent calls.
- For T031 grant-lifecycle concurrency, exercise a timer callback during scheduling through the public TimeProvider seam. The constructor assigns _deadlineTimer from CreateTimer while the callback may reschedule through that field; verify initialization ordering before any correction.

- T031/final containment inspection: DurableRetainedSourceStore.ResolveContained still trims all root separators even though policy normalization now preserves filesystem roots. Verify a filesystem-root storage value with excluded tool roots on a different volume through a supported public boundary before any production correction; this is an unproven observation, not a failure claim.

Filesystem-root mapping disposition: read-only public Windows reopen characterization root-mapping-characterization-01.json passes; current Path.Combine restores the separator after the drive designator. The hypothesized cwd alias is not selected. No volume-root write or production correction; do not carry this suspicion as a confirmed defect.

- Full-suite harness preflight: RetainedSourceCaptureResourceTests uses process-wide GC.GetTotalAllocatedBytes and currently has no nonparallel collection. No assembly/runner parallel-disabling setting was found. Isolate this allocation-measuring class with an explicit xUnit nonparallel collection before grouped/full-suite execution so unrelated concurrent allocations cannot invalidate its measurement; this is test scheduling/isolation, not a production defect or skipped test.

Reader lifecycle/harness dispositions: actual timer NullReferenceException RED corrected and verified in persistence-lifecycle-complete-01.json; disposing one context preserves its sibling. Allocation/cwd tests now use a nonparallel collection, full source138passes. PostgreSQL helper port portability remains pending before final qualification.

Fixture portability disposition: T035/T036 now require explicit numeric-loopback administrator port and preserve owned database naming; hardcoded5511 removed. Current qualification still uses authorized5511. Fresh process/tool-root cleanup and corruption evidence are in fresh-process-complete-01.json.
