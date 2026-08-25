# Story 12.3 Mandatory Tool Selection Planned-Change Authorization 06

- Decision ID: `story-12-3-planned-change-authorization-06`
- Status: Accepted
- Decided at: `2026-08-24T23:35:40.0294274Z`
- Scope SHA-256: `3b703a6b4dde2d85dab9ca4a4461d2c634d18adff714edb85e01774dbb262bd5`
- Plan SHA-256: `a2344e815cdb6440ebab8dc3345f4ed410634e0a862e19637fd8a090ea4eeb6b`
- Accepted plan-review SHA-256: `e452bcb20166e6be9c235cb4c7b9113d2bea1f3764077fad8b66aef68cc673fc`

Scope construction: SHA-256 of this exact UTF-8 JSON object with no trailing
newline:

```json
{"source_free_requires_call_and_done":true,"story_12_3_worker_thinking":"disabled","story_12_3_worker_tool_choice":"required","stream":true,"system_changes":false,"zero_call_preflight":"reject"}
```

## Authority

The user explicitly approved continued diagnosis and correction of further
LLM/DeepSeek problems after checking current best practices, authorized the
resulting bounded fix without another approval gate, and prohibited system
changes. Independent plan review 16 accepted the exact AD-22 correction with
zero findings.

## Approved planned change

- Change only the Story 12.3 strict Worker and its qualification-only
  source-free handshake from thinking-enabled default/auto selection to
  streamed `thinking:{"type":"disabled"}` plus `tool_choice:"required"`.
- Preserve the exact two projected strict functions. Required selection grants
  no application authority: complete-turn preflight still admits only a legal
  sole submission or bounded all-navigation batch.
- Reject a zero-call completed turn before FICC receives an update, using only
  a payload-free closed diagnostic while preserving the external
  `MissingSubmission` result mapping.
- Require the source-free gate to observe a function call and `[DONE]` without
  invoking a function, sending a result, retaining response content, or
  mutating durable state.
- Preserve streaming, byte/time/turn/retry bounds, durable reservation, local
  result validation, atomic persistence, privacy, endpoint/model, and source
  scope.

## Preserved boundary

No host/system/network/firewall/service/permission/tool/credential change,
provider loop, SDK retry, dependency, function, schema weakening, migration,
source expansion, ordinary `vulgata` database mutation, Story 12.4/12.5 work,
or sibling client behavior is authorized.

## Decision

Task generation and implementation may proceed through the accepted
independent review/TDD/qualification gates without another user pause.
