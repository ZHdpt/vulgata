# Story 12.3 DeepSeek Continuation Authorization 05

- Decision ID: `story-12-3-deepseek-continuation-authorization-05`
- Status: Accepted
- Decided at: `2026-08-24T15:53:45.6107944Z`
- Scope SHA-256: `19daebedce0c0ad89b44dc612fa086ecf27074a822be73eb7c197763d9eadf68`

Scope construction: SHA-256 of this exact UTF-8 JSON object with no trailing
newline:

```json
{"allowed":["diagnose","review-current-official-llm-deepseek-best-practices","correct-story-12.3-plan","correct-repository-tests-and-implementation","rerun-disposable-database-qualification"],"approval_gate":"omit-further-user-approval-for-in-scope-corrections","boundary":"story-12.3-llm-deepseek","prohibited":["host-system-changes","environment-configuration-changes","network-configuration-changes","credential-disclosure","ordinary-vulgata-database-mutation","source-scope-expansion","weakening-local-validation-privacy-atomicity"]}
```

## Authorized continuation

If a later Story 12.3 LLM or DeepSeek qualification problem occurs, the root
must first diagnose the exact failure, then compare the complete affected LLM
request/response/tool/reasoning flow and the current plan/implementation with
current official DeepSeek and applicable SDK best practices. The root may then
correct repository plan artifacts, tasks, tests, and implementation and rerun
the repository review/TDD/qualification gates without another user approval
pause.

This standing authority does not waive independent plan/definition/quality
reviews, honest public-seam RED/GREEN, source-free gates, disposable-database
isolation, privacy/security checks, or final verification. It omits only a
repeated user approval pause for further corrections inside the exact boundary.

## Prohibited actions

System or host changes remain prohibited. This includes environment, machine,
network, firewall, service, permission, installed-tool, global configuration,
and credential changes. If such a change becomes necessary, stop and report it
instead of attempting it.

The authority also excludes mutation of the ordinary `vulgata` database,
source transfer outside the two already approved repositories, credential or
payload disclosure, Story 12.4/12.5, and any weakening of complete local
validation, identity/binding, privacy, atomicity, or immutable Document rules.

## Decision

The root may continue automatically through in-repository Story 12.3
LLM/DeepSeek diagnosis and correction cycles within this scope. A required
system change remains a hard stop.
