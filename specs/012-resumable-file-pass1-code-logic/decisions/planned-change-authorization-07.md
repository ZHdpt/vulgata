# Story 12.3 AD-23 Navigation Prefix Closure Planned-Change Authorization 07

- Decision ID: `story-12-3-planned-change-authorization-07`
- Status: Accepted
- Decided at: `2026-08-25T00:44:04.9203593Z`
- Scope SHA-256: `3b1d439e1abb693b9d5c42e8381b0fbdfe9b29e5089acde9d9ffe8d5eb632ae6`
- Plan SHA-256: `27c356f7e798fec727d412635da07781e13998ec268a749b15fa13aac7ec6e1a`
- Accepted plan-review SHA-256: `d08fbc6f811fade6a0d29ca0115b36eb9f7a1dda95816ce58345dd7159953dca`

Scope construction: SHA-256 of this exact UTF-8 JSON object with no trailing
newline:

```json
{"ad_23_navigation_prefix_closure":true,"initial_zero_provider_conversation":true,"sdk_iteration_zero_based":true,"system_changes":false,"turn_ten_terminal_submission":true}
```

## Authority

The user explicitly approved diagnosis, current LLM and DeepSeek best-practice
review, bounded plan/spec/test/implementation correction, and continuation
without further approval pauses. System changes remain prohibited. Independent
plan review 19 accepted the exact AD-23 correction with zero open findings.

## Approved planned change

- Preserve the exact streamed DeepSeek request with disabled thinking,
  required tool choice, and the same two strict projected declarations.
- Admit legal all-navigation responses containing one through eight calls even
  when the response exceeds remaining durable navigation capacity.
- Execute only the provider-ordered prefix fitting remaining capacity and
  return the constant `navigation-budget-exhausted` result for every excess
  original SDK-managed call ID without invoking or reserving it.
- Reject above-eight and all other illegal completed turns before callbacks.
- Remove the initial-zero fallback so direct submission, or closed navigation
  results followed by submission, remains possible.
- Derive provider turn as zero-based SDK iteration plus one; allow sole
  first-or-second submission on provider turn ten and prohibit request eleven.

## Preserved boundary

No manual provider loop, SDK fork, dependency, prompt or declaration identity
change, schema or migration, source expansion, ordinary `vulgata` database
mutation, Story 12.4/12.5 behavior, or host/system/network/firewall/service/
permission/tool/credential change is authorized.

## Decision

Implementation may proceed through the accepted TDD and qualification gates
without another user approval pause.
