# Story 12.3 DeepSeek Strict-Result Implementation Approval 04

- Decision ID: `story-12-3-implementation-approval-04`
- Status: Accepted
- Decided at: `2026-08-24T05:55:00Z`
- Scope SHA-256: `c0e79f162eb97e3d77c2141a3598e8ae4b1363a16812b306efef3222695542a3`
- Specification SHA-256: `b03687c3a77f52766280669da6cf9e2f9925a44bec1ae9304991f387477a9493`
- Plan SHA-256: `457c461cbbcb3761bb27e43074e5e663e1d0ad854de658a4e7d8cd6b82960d0c`
- Tasks SHA-256: `1fb24af08b52ccb923dc50ca10d8c205eba0f5de452782a17e71d9239279e3c1`
- Accepted definition verdict SHA-256: `01f065e4518cb9de8f7d1444c10c951a97c7645221a644edb9df4d140350d628`

Scope construction: SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
current Story 12.3 specification, accepted plan, corrected tasks, and
independent `definition-review-13` ACCEPT verdict.

## Decision requested

Authorize production and test implementation of the dependency-ordered
T070-T086 DeepSeek strict-result correction. The implementation uses public-
seam RED/GREEN slices for deterministic schema projection, execution-local
`submit_code_logic` capture, externalized prompt/DI composition, streaming and
wire behavior, and the separate source-free capability handshake helper.

After focused local GREEN proof, qualification remains ordered: fake outgoing
wire, source-free DeepSeek beta handshake, three isolated source-bearing jobs,
full authenticated T062, and final verification. The user's earlier bounded
authorization to send source/context from the two approved repositories
applies only after the fake-wire and source-free gates succeed.

## Effects and implementation intent

- Expose exactly two closed provider-strict functions: `navigate_code` as the
  sole evidence-access function and `submit_code_logic` as execution-local,
  non-evidence finalization.
- Derive the provider schema from the one embedded authoritative
  `code-logic/file-v2` resource while preserving complete local validation as
  the sole success and commit authority.
- Preserve nested candidate bytes, typed rejection categories, at most two
  submissions in ten Vulgata service turns, complete reasoning/tool history,
  five-minute resetting inactivity, a nonresetting 25-minute cap, and zero SDK
  retries.
- Correct the externalized Worker prompt and production composition; do not
  hardcode a replacement prompt or add a third function.
- Stop and report immediately on environment, host, configuration, network,
  authentication, schema-capability, or provider-availability failure. Do not
  remediate the host, rewrite endpoint/model, weaken the schema, or use an
  alternate route.

## Preserved boundary and remaining gates

No database entity, table, column, migration, durable submission state,
provider transcript, user-facing workflow, Story 12.4/12.5 feature,
CodeGraphContext path, or unrestricted source disclosure is authorized.
Existing unrelated work remains preserved.

This approval does not waive honest RED/GREEN evidence, focused and adjacent
regressions, fake-wire/privacy proof, the source-free gate, isolated 3/3
qualification, T062, convergence, independent quality review, conditional
E2E, security/migration checks, pinned final verification, lifecycle,
checkpoint, staging, or commit gates.

## Decision

The user explicitly approved this exact implementation scope on 2026-08-24.
The root may implement and qualify T070-T086 while the bound scope hash remains
unchanged. No excluded scope or downstream gate was waived.
