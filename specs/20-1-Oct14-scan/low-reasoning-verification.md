# Explicit low-effort POM experiment

## Authority and decision

On October 10, 2026 the user explicitly requested trying `low` reasoning effort
because the deadline is near. The preceding POM trial exhausted all 8192 output
tokens on reasoning and ended with Length. Work starts in the assigned worktree
on clean `oct14/scan` at `63edab33`. No skills or external coordination messages
are used. Separate test ownership and independent review remain required.

Current [DeepSeek Chat Completions documentation](https://api-docs.deepseek.com/api/create-chat-completion/)
supports `reasoning_effort: low`, with high as the default. It now documents output
limits above 8192 for the current models; the older 8K maximum is not the current
model limit. Nevertheless this experiment deliberately retains the existing 8192
completion cap and 40960 reservation ceiling. Only reasoning effort changes.

## Bounded configuration

Optional `October:DeepSeekWorkerReasoningEffort` accepts absent or exact `low`.
Invalid configuration fails before runtime startup. An explicit setting requires
the existing qualified DeepSeek HTTPS root/default-port endpoint and fails before
credential decryption for other endpoints. The setting is passed through Generate
composition to the selected-client factory; no shared provider schema is changed.

Only Worker streaming receives a cloned `ChatOptions`/reasoning-options object
with low effort. Caller options/history are not mutated. Unset behavior and Business
nonstreaming remain unchanged. Standard SDK serialization must be proven at the
wire boundary. Thinking is not disabled; token/byte/deadline/source/authorization,
lease, replay, settlement, acceptance and publication guards remain intact.

The retained runtime launcher supplies `October__DeepSeekWorkerReasoningEffort=low`
only in the Generate child environment. No persistent environment, PATH or global
provider setting changes. The real experiment selects only `mbs_channel/pom.xml`
with the same approved supporting source and protected provider credential. A
failed POM does not permit a full-slice retry or automatic paid retry. Lower effort
may change reasoning quality; syntax/acceptance alone is not semantic qualification.

Controlled and real results follow after execution.

## Controlled evidence and preparation

RED `isolation-b851ea07a3384243b829bf471a0a70f5`: 19 selected, 16 failed, 3 passed,
zero skips. Baseline requests omitted the low setting or retained caller High;
invalid values/endpoints lacked the new refusal. Unset/default cases passed.
Test-owner and independent review accepted the narrow final shape. SDK reasoning
options are copied through their public fields into a fresh object; an initial
attempt to call its non-public clone method stopped at compile time and is not
behavioral evidence. Numerical guards and Business calls are unchanged.

Prepared fresh target `phone-54fd3b9dd498`, batch
`f0333d19-6dd0-4cc0-aff3-d1cbc6d4ff0b`, under the recorded retained-pilot resource
boundaries. Bootstrap, prepare and select passed. All 77 source hashes matched;
only the channel POM is selected. The bootstrap host stopped, existing databases
and container/volume are preserved, and protected runtime resources remain outside
Git. The child Generate launcher explicitly supplies the low-effort setting; no
provider request occurs during these preparation steps.

Complete SelfContained verification `isolation-f63d473a5a454e50aae41a40539b6b83`
passed **4,761/4,762**, zero skips. All **19 new low-effort cases** and all **453
affected class cases** passed in that run. The sole failure remains the previously
recorded exact report-shape mismatch in
`OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`.
The full suite is not GREEN; no assertion was changed or waiver claimed. Existing
timing/cleanup cases passed this time, without establishing their earlier causes.
The actual SDK tests prove low on both tool-conversation requests, unchanged 8192
cap and reasoning replay, immutable caller options/history, unset omission,
Business isolation and invalid configuration/endpoint rejection before decryption.
These are controlled checks, not live provider or generated-knowledge evidence.

## Real POM result with low requested

One Generate invocation ran on reviewed commit
`b319289999f6e16621d54391369cd425f4691996`, using the explicit low child setting.
The executed Generator assembly matches the tested copy by SHA-256. Report
`generate-094dd867165c43a38d6e4fa0a419e344.stdout` records exit 1 after **44.05
seconds**, no timeout, `NeedsSettlementCheck=false`:

| Observation | Value |
| --- | --- |
| Execution / coverage / publication | Failed / Partial / NotRequested |
| Accepted files / Documents | 0 / 0 |
| Worker failure | InvalidOutput / StreamTerminal / UnsupportedFinish |
| Finish reason | Length |
| Requested output cap | 8192 |
| Provider requests | 1 |
| Input / output / reasoning tokens | 9748 / 8192 / 8192 |
| Unknown usage | false |
| Retry disposition | None |

No byte/stream-budget, tool or proposal-parse failure was reported. The low setting
did not make this instance complete within the unchanged allowance: all reported
output tokens were still reasoning. This result does not prove the provider ignored
low or establish equal internal behavior to high; no raw response/reasoning was
retained, and the terminal rejection has no HTTP comparison snapshot. Exact low
serialization is established by the controlled SDK tests, while the real launcher's
low configuration and tested binary binding were separately verified.

Standard error is empty, and all 77 source hashes remain unchanged. Prior successful
data and this fresh owned run are retained. The bootstrap host is stopped. No
whole-slice retry or additional paid attempt ran because the isolated POM did not
succeed. A thinking-disabled comparison would be a different, separately bounded
experiment and would require checking resulting explanation/evidence quality;
it was not performed or automatically substituted here.
