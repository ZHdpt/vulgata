# Track A Quality Correction 03

Finding: `QR-TA-001` at reviewed boundary
`1b922be95e66a5bde9293c5441024707c1fb66cc`.

## Honest RED

The test owner added a test-only reflection probe for
`ValidateSemanticEnvelope(string, IReadOnlyDictionary<string, byte[]>)`, plus
schema-valid passed-envelope artifact content with real SHA-256 descriptors.
The baseline and coordinated foreign Case Receipt/Stream Attempt mutation,
altered content/hash, and missing-content cases all reached the probe and failed
because the two-argument validator overload did not exist: 4 failed, 0 passed.

## Minimal GREEN

The test-only semantic validator now preserves the one-argument overload and
adds an in-memory artifact-content overload that:

- verifies each named aggregate descriptor's SHA-256 before parsing bytes;
- rejects missing, altered, malformed, or non-object artifacts;
- parses explicit fixed-corpus, result, packet, and streaming Case IDs;
- requires nonempty unique and exact Case ID agreement with Case Receipts; and
- preserves ordered Packet Fingerprint binding.

Verification: focused pass-3 tests 4/4 passed; all Story 12.5 ReleaseGate tests
75/75 passed; `git diff --check` passed. No filesystem/network access, Tier 3
runner, Track B/C, streaming transport, application production, schema/contract,
EF, database, or Story 12.4 change was made.
