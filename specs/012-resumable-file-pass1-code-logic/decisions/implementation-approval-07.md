# Story 12.3 DeepSeek Budget and Wire Compatibility Implementation Approval 07

- Decision ID: `story-12-3-implementation-approval-07`
- Status: Accepted
- Decided at: `2026-08-24T16:57:37.5858986Z`
- Scope SHA-256: `596a6bafc6886ff811307c3a050d1e3fa25309330cf4c76cb9c7dd601bd441d1`
- Specification SHA-256: `b03687c3a77f52766280669da6cf9e2f9925a44bec1ae9304991f387477a9493`
- Plan SHA-256: `edd9c805173ded3b55abeecc6ba081f11cb9583818a13b94241a12acd527a399`
- Tasks SHA-256: `9800936c9a1ab66baa2c0a8e7566932e637d03d6440821043c1bb29c6e20df90`
- Accepted definition verdict SHA-256: `91fc18eb28dabb471a98c79f780c5b679a4aa61c37d8645222b9b64bc389a222`

Scope construction: SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
current specification, corrected plan, corrected tasks, and independent
`definition-review-18` ACCEPT verdict.

## Authority

The user explicitly authorized the root to diagnose later Story 12.3 LLM or
DeepSeek problems, compare the affected plan and implementation with current
official best practices, correct repository plan/tests/code, and omit a
further in-scope user approval pause. That standing authority is recorded in
`deepseek-continuation-authorization-05.md`. Definition review 18 independently
accepted this corrected boundary with no findings and closed DR17-001 and
DR17-002. The root therefore binds the user's standing authorization to these
exact accepted hashes rather than reusing superseded implementation approval
06.

## Approved implementation

T091 may replace the three named definition-invalidated interim assembled
boundary tests and write the corrected test-only RED in
`tests/Vulgata.Tests/PreparedPass1/Pass1DeepSeekStreamingQualificationTests.cs`.
Exact inclusive/plus-one byte arithmetic belongs at the deterministic checked
UTF-8 accounting seam; the public executor seam proves only reachable
transport behavior, final bodies within the cap, and constructible overflow
as sanitized no-send.

After honest RED evidence, T092 may change only
`src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1JobExecutor.cs` to:

- set assembled, per-assistant, and aggregate assistant replay caps to exactly
  16,777,216 bytes while retaining the 16,777,216-byte raw cap;
- enforce the complete outgoing body cap of exactly 17,416,192 bytes;
- capture the provisional SDK body within that hard cap and parse it with
  duplicate-aware validation;
- retain exact `max_tokens: 32768` or rename one exact
  `max_completion_tokens: 32768`, and independently retain exact enabled
  thinking or insert one absent `thinking: {"type":"enabled"}` object;
- reject missing, duplicate, simultaneous, wrong-type, malformed, disabled,
  extra, conflicting, or oversized shapes before transport;
- deterministically reserialize, remeasure all contributions and the final
  body, validate the complete final DeepSeek wire, then send; and
- preserve complete reasoning/content/tool-call/tool-result continuity,
  cancellation, content-dark diagnostics, privacy, sibling-client isolation,
  five-minute inactivity, 25-minute absolute timeout, and zero SDK retries.

## Preserved boundary

No database, migration, schema, prompt, dispatcher, persistence, API, UI,
provider-health, other Chat/Worker, system, host, environment, network,
firewall, service, permission, installed-tool, global configuration,
credential, endpoint/model, Story 12.4/12.5, CodeGraphContext, or source-scope
change is approved. The ordinary `vulgata` database remains prohibited.

Source-free tests and capability gates must pass before fresh T084 isolated
source qualification. T084 must succeed 3/3 on a fresh disposable database
before T085 full T062 qualification. All local validation, identity/binding,
atomic immutable Document commit/replay, privacy, and failure-stop rules remain
mandatory.

## Decision

The root may execute T091/T092 and advance automatically through clean
source-free, disposable-database, provider, convergence, review, E2E, and
verification gates while this exact accepted scope remains current. A required
system or host change remains a hard stop and must be reported without attempt.
