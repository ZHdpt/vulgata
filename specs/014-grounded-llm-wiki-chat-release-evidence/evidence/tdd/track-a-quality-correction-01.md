# Track A Quality Correction 01

Finding: `QR-TA-001` at reviewed boundary
`216f2392f1d00e7df94e27449970487978760f7c`.

## Honest RED

The initial adversarial fixture used a lone artifact assertion, which could not
honestly distinguish an altered hash or foreign relative path. The test owner
corrected the fixture to the accepted same-envelope seam: command
`stdoutArtifact`, observation artifact, and check artifact began with identical
path, SHA-256, and media type. A pre-mutation baseline assertion stayed green.

Focused result after the fixture correction: 1 baseline passed and all seven
binding mutations failed because semantic validation returned no diagnostics.
The mutations covered dangling command reference, unresolved observation,
foreign artifact binding, missing observation artifact, nonzero command exit,
altered artifact hash, and mismatched corpus/result/receipt/fingerprint sets.

## Minimal GREEN

`Story125ReleaseHarness` now:

- indexes unique command and observation IDs;
- resolves referenced IDs and requires referenced commands to exit zero;
- binds artifacts by exact path, SHA-256, and media type to same-envelope
  command/observation evidence;
- requires nonempty observation evidence; and
- enforces corpus/result/receipt set equality and bounded unique packet
  fingerprints.

Verification: focused vectors 8/8 passed; all Story 12.5 ReleaseGate tests
56/56 passed; `git diff --check` passed. No runner, filesystem/network evidence
lookup, application production, Track B/C, EF, database, Story 12.4, schema, or
contract change was made.
