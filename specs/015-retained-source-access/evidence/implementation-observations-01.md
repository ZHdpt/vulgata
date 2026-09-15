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

Current dispositions (before T019 acceptance):
- Root normalization and unrepresentable capture/stale durations: observed3RED, minimal policy correction, independently GREEN.
- Active deadline on stalled Git: observed actual endpoint/watchdog RED, monotonic monitor correction, independently GREEN.
- Final authority after identity-lock wait: observed Completed/visible-row RED after revocation, final fresh check correction, independently GREEN. Final-publication cancellation characterization also GREEN.
- Filesystem root/excluded-child overlap: observed no-exception RED, minimal prefix correction, producer7/7boundary GREEN; independent verification dispatched.
- Streaming/no whole-snapshot buffering: measurable public capture allocation regression dispatched; unresolved.
- PostgreSQL helper port portability remains pending before final qualification.
