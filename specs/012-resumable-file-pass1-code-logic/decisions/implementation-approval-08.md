# Story 12.3 DeepSeek V4 Wire Compatibility Implementation Approval 08

- Decision ID: `story-12-3-implementation-approval-08`
- Status: Accepted
- Decided at: `2026-08-24T17:51:25.9206958Z`
- Scope SHA-256: `d332473a5145d52bf5a3f7628c02318900bc5ae027f2057d8d24b68c058704c4`
- Specification SHA-256: `b03687c3a77f52766280669da6cf9e2f9925a44bec1ae9304991f387477a9493`
- Plan SHA-256: `76c6078ffaaa9dc71125c62890cce145291fbafb5951c61f280fa01f89942d06`
- Tasks SHA-256: `5b4a10213ec876b5d698f8a2f012b77806d03069ec263ddc0e93bbea45207564`
- Accepted definition verdict SHA-256: `0c34635d5afd25f41cf55065217d84412ffee8aefeac72780daae91c7f8f6874`

Scope construction: SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
current specification, corrected plan, corrected tasks, and independent
`definition-review-19` ACCEPT verdict.

## Authority

The user explicitly authorized the root to diagnose later Story 12.3 LLM or
DeepSeek failures, compare the complete affected flow with current official
best practices, correct repository plans, tests, and implementation, and omit
another in-scope approval pause. That standing authority is recorded in
`deepseek-continuation-authorization-05.md`. Plan review 11 and definition
review 19 independently accepted this corrected boundary with no findings.
The root therefore binds the standing authorization to these exact hashes
rather than reusing superseded implementation approval 07.

## Approved implementation

T093 may add only the accepted fake-wire and contract tests in
`Pass1DeepSeekStreamingQualificationTests.cs` and
`Pass1DeepSeekContractQualificationTests.cs`. The RED must expose the current
declaration mismatch, forbidden compatibility fields, and incomplete
assistant tool-call content handling before any production or handshake edit.

After honest RED evidence, T094 may change only
`PreparedPass1JobExecutor.cs` and, if needed, add
`DeepSeekStrictToolDeclarations.cs` to:

- construct one deterministic accepted declaration blob from the two actual
  public `AIFunction.AsOpenAIChatTool()` declarations and use it on every
  production turn;
- remove only one exact SDK-emitted `tool_choice:"auto"`,
  `parallel_tool_calls:false`, and
  `response_format:{"type":"json_object"}`, rejecting duplicate or alternate
  shapes before transport;
- preserve complete reasoning and tool-call/result continuity and represent
  only absent/null tool-call assistant content as `""`; and
- preserve the accepted token, thinking, streaming, size, timeout, turn,
  retry, local-validation, privacy, and sibling-client boundaries.

T095 may change only `DeepSeekSourceFreeHandshakeHarness.cs` to consume the
same declaration authority, enable thinking, omit the three compatibility
fields, invoke no returned function, send no tool result, and expose only
sanitized hashes, sizes, and classification. T096 may rerun deterministic and
live source-free qualification. Only after T096 succeeds may T084 use a fresh
disposable database for three approved source-bearing jobs; T085/full T062
remains blocked until T084 succeeds.

## Preserved boundary

No ordinary `vulgata` database mutation, source-scope expansion, schema or
migration change, prompt or dispatcher change, provider-health or other
Chat/Worker change, endpoint/model rewrite, credential disclosure, weakened
validation/privacy/atomicity, Story 12.4/12.5 work, or system/host/environment/
network/firewall/service/permission/installed-tool/global-configuration change
is approved. A required system change is a hard stop and must be reported.

## Decision

The root may execute T093-T096 and continue through fresh disposable-database
T084/T085 plus convergence, review, E2E, and verification gates while this
exact accepted scope remains current. This omits only another user approval
pause; it does not waive honest TDD or any downstream gate.
