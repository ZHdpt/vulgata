# Track A Quality Correction 02

Finding: `QR-TA-001` at reviewed boundary
`2655a8da3fb4bbfb166f5ff2057c8020554fd98d`.

## Honest RED

The first passed-receipt fixture run was invalid because the observation
contained three aggregate artifacts while its referenced commands reproduced
only one. The test owner corrected only the fixture by adding matching packet
and streaming command outputs and propagating their IDs through the checks,
observation, and Chat receipt.

The corrected, schema-valid passed-envelope baseline passed. Fourteen mutations
then failed because the semantic validator returned no diagnostic. They covered
dangling Chat receipt command IDs; foreign, altered, or missing aggregate
artifacts; duplicate, missing, or foreign Case Receipt IDs; fixed-corpus/result
coverage mismatch; and reordered, foreign, duplicate, or mismatched Packet
Fingerprints.

## Minimal GREEN

The real `releaseReceipt.chat` validation now:

- resolves receipt command IDs to same-envelope zero-exit commands;
- binds all three aggregate artifacts by exact path, SHA-256, and media type;
- requires unique Case Receipt IDs and exact count/coverage consistency;
- binds invoked Case Receipts to their Stream Attempt case IDs; and
- requires ordered unique Packet Fingerprints to equal invoked Case Receipt
  fingerprints.

Verification: focused passed-receipt vectors 15/15 passed; all Story 12.5
ReleaseGate tests 71/71 passed; `git diff --check` passed. No Track B/C,
streaming transport, Tier 3 runner, filesystem/network lookup, application
production, schema/contract, EF, database, or Story 12.4 change was made.
