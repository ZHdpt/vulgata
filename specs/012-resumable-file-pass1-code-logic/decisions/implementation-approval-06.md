# Story 12.3 Transcript-Budget Implementation Approval 06

- Decision ID: `story-12-3-implementation-approval-06`
- Status: Accepted
- Decided at: `2026-08-24T15:53:45.6107944Z`
- Scope SHA-256: `2bf0da01fc2fd146e1b21dcaef11bfa99840e2e443845e7ab89fd9713530446c`
- Specification SHA-256: `b03687c3a77f52766280669da6cf9e2f9925a44bec1ae9304991f387477a9493`
- Plan SHA-256: `dbc4faa6e6b4de96639b1b85bb564c5d4c5c281c222db031143323ce13fe2fc0`
- Tasks SHA-256: `cfc36230efeef1834987889741d67d46b055aaad02bb5aed12cf6ec3fbf0a7dc`
- Accepted definition verdict SHA-256: `205a0c07996d4b822a0cd596ced3f9d142a34f77024e9e0f62b96e1d133c420f`

Scope construction: SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
current specification, corrected plan, corrected tasks, and independent
`definition-review-16` ACCEPT verdict.

## Approved implementation

The user explicitly approved the independently accepted definition. T091 may
write the public-seam transcript-budget RED tests in
`Pass1DeepSeekStreamingQualificationTests.cs`; T092 may make the minimal GREEN
in `PreparedPass1JobExecutor.cs`.

The implementation changes only these execution-local budgets:

- assembled response: exactly 16,777,216 bytes;
- each prior assistant contribution: exactly 16,777,216 bytes;
- aggregate prior assistant contributions: exactly 16,777,216 bytes;
- complete outgoing request: exactly 17,416,192 bytes using the accepted
  additive formula.

The raw response remains exactly 16,777,216 bytes. Exact bounds pass; byte plus
one cancels the current response or prevents the later request through the
existing closed failure path.

## Preserved boundary

`MaxOutputTokens=32,768`, maximum nine prior assistant messages, ten service
responses, five-minute inactivity, 25-minute absolute timeout, zero SDK
retries, the two strict functions, complete reasoning replay, candidate/
evidence/schema/declaration bounds, full local validation, privacy,
nonpersistence, atomic immutable Document commit/replay, and source/provider
qualification ordering remain unchanged.

No database, migration, API, UI, scheduling/concurrency, durable transcript,
provider retry, raw-response buffering, system/host/environment/network
configuration, Story 12.4/12.5, or CodeGraphContext change is approved.

## Remaining gates

T091/T092 RED/GREEN and surrounding source-free regressions; fresh T084 3/3
qualification; T085 full T062; convergence; independent quality review;
conditional E2E; security/migration/build/full-suite verification; and the
root-owned checkpoint/state/commit binding.

## Decision

The root may implement T091/T092 and advance automatically through all clean
downstream gates while this accepted scope remains current.
