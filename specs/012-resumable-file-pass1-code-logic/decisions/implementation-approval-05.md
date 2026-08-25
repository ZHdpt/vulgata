# Story 12.3 Raw SSE Transport Implementation Approval 05

- Decision ID: `story-12-3-implementation-approval-05`
- Status: Accepted
- Recorded at: `2026-08-24T14:15:06Z`
- Scope SHA-256: `d37e7aba192bd86e148dbef1bf550948298c4c65e74a22b0e9bb8aea191bd55e`
- Specification SHA-256: `b03687c3a77f52766280669da6cf9e2f9925a44bec1ae9304991f387477a9493`
- Plan SHA-256: `7d9490dc424ffbab384a794e62fba55d0bb4c89dec592a1f067852e61549eafa`
- Tasks SHA-256: `ddaf83fe29a4cef77c58d80d609d05e886e445676995b398100bea8b3343bf16`
- Accepted definition verdict SHA-256: `30386eae2e3efc9feb1e8580e5f0aba9a1ca6d6d88fcca78f9a5b1cd8177a0a9`

Scope construction: SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
current Story 12.3 specification, corrected plan, corrected tasks, and
independent `definition-review-14` ACCEPT verdict.

## Approved correction

The user explicitly approved the bounded design in this task before the
production correction. The accepted definition changes only the per-response
raw streamed SSE transport ceiling from 262,144 bytes to exactly 16,777,216
bytes. The value is the local Vulgata allowance of 32,768 configured maximum
output tokens multiplied by 512 serialized SSE bytes per token; it is not a
provider guarantee.

T087 proves the exact 16,777,216-byte public executor boundary is legal and
that byte 16,777,217 cancels transport without a later send. T088 changes only
`PreparedPass1WireBudget.MaximumRawResponseBytes` in
`PreparedPass1JobExecutor.cs`, then reruns focused and surrounding source-free
tests before any fresh source-bearing qualification.

## Preserved boundary

The 131,072-byte assembled response authority, 32,768 output-token setting,
65,536-byte candidate/evidence/schema bounds, 81,920-byte declaration bound,
1,818,624-byte outgoing request bound, five-minute resetting inactivity,
25-minute absolute cap, zero SDK retries, ten service turns, two submissions,
eight navigation attempts, full local `code-logic/file-v2` validation, one
durable Worker retry, atomic immutable Document commit/replay, privacy, and
nonpersistence rules remain unchanged.

The larger raw allowance increases bounded network and parsing exposure only.
Raw SSE is counted incrementally and discarded; it does not authorize complete
raw-response buffering, logging, persistence, a larger assembled transcript,
or a larger accepted Document.

## Remaining gates

The approval does not waive T087/T088 RED/GREEN evidence, exact-bound and
privacy regressions, the fresh T084 3/3 isolated qualification, T085 full
production-composed qualification, convergence, independent quality review,
conditional E2E, migration/security checks, final verification, lifecycle,
checkpoint, staging, or commit gates. The source-bearing provider scope remains
limited to the two repositories already authorized by the user.

## Decision

The root may implement and qualify T087/T088 and resume T084 while the scope
hash above remains unchanged. No excluded scope or downstream gate is waived.
