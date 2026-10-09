# Output-byte accounting assessment

2026-10-09. The original assessment below left production and limits unchanged;
the subsequently authorized correction and its evidence follow at the end. Base Scan
`b5aff0ef0737dae0a8d9296d947976097e1f31c3` matches the affected production in
integration `449e00bf874c9e864b5a848369d891e33bade4cc`.

## Established live boundary

The latest Integrator report identifies `BudgetExceeded` / `StreamTerminal` /
`OutputBytes`, retry `None`, with `toolFailure: null`. Three provider requests were
observed; aggregate token usage remains unknown. This identifies the exceeded
bound, not which payload contributed to it. Neither chunk-envelope inflation nor
native JDT fallback is established for that real run. Paid repeats remain held;
earlier partial CL and retained resources remain untouched.

## Current accounting

`OctoberFileWorkerExecutor` accumulates one output counter across the file's
provider leaves, tools and source windows. The default maximum is 262,144 bytes;
equality is permitted and the first charge beyond it fails. The counter includes:

| Content | Current charge |
| --- | --- |
| `TextContent.Text` | Strict UTF-8 bytes of the SDK string |
| Executable function call | UTF-8 bytes of default-serialized arguments, once for the SDK's assembled call |
| Tool result, including argument refusal | UTF-8 bytes of its serialized result |
| Reasoning and other non-call/non-usage content | UTF-8 bytes of each default-serialized `AIContent` object |
| Usage | No output-content charge; observed usage is settled separately |
| Informational function call | Currently omitted; this is a separate accounting gap, not an endorsed exclusion |

The extra argument serialization used to freeze the call is not another charge.
Tool results and earlier leaves intentionally consume the same file-wide budget;
the cap is not just the final proposal size. Reasoning is charged even though the
adapter exposes only settled final text and executable calls to the conversation.

Default JSON serialization can expand CJK and supplementary Unicode into escape
sequences. It also wraps each reasoning delta in object/property metadata. These
are distinct costs. `TextReasoningContent` is a different type from `TextContent`:
it can contain opaque `ProtectedData` even with empty text, plus annotations and
additional properties. Removing its entire serialized charge would discard real
payload accounting. `RawRepresentation` is already ignored by serialization; the
existing output counter is not a raw SSE-wire or exact managed-memory meter.

Other bounds remain independent: 8 provider rounds, 16 executable tool calls,
8,192 updates per provider leaf, default 4 source windows of at most 32,768 UTF-8
bytes each, request timeout and original file/run lifetime. Cumulative input is
separately bounded (default 1 MiB), with conservative request/token reservations.
Stopping before a usage packet arrives must leave that usage unknown, without
inventing zero usage or granting a retry. Earlier observed usage is still retained.

## Controlled measurements

Run `13a89dc1e84140128ebc6ec5befc2ef4` passed **164/164**, zero failures/skips,
through the standard SelfContained launcher selecting the full Worker,
ExecutionScope and GenerateRunner classes. All nine new characterization cases
passed. Build completed with zero errors. Existing assertions and production
remained unchanged; this is current-behavior evidence, not RED for a production fix.

The actual installed SDK mapped identical logical reasoning into either one or
7,500 `TextReasoningContent` deltas. Splits were Unicode-scalar aligned. Final text
was identical (413 UTF-8 bytes); total observed update counts were 4 versus 7,503,
both below the existing per-leaf 8,192 limit.

| Reasoning content | Raw reasoning UTF-8 | Escaped reasoning payload | One-delta total charge | 7,500-delta total charge |
| --- | ---: | ---: | ---: | ---: |
| ASCII | 7,500 | 7,500 | 8,012 | 750,413 |
| Chinese | 22,500 | 45,000 | 45,512 | 787,913 |
| Emoji | 30,000 | 90,000 | 90,512 | 832,913 |

Each total includes the same 413-byte final text. The measured fixed reasoning
envelope is **99 bytes per delta**: 99 versus 742,500 bytes of envelope alone.
The real executor with controlled transport and the unchanged **262,144-byte**
default completed each one-delta case and stopped each fragmented case with
`OutputBytes` at `StreamTerminal`. Fragmented failures occurred before terminal
usage: usage stayed unknown, retry remained `None`, and the attempt settled and
disposed once. This proves a chunk-dependent accounting defect independently of
the unobserved contents of the latest real run.

Controls establish which charges remain legitimate:

- Actual-SDK final text split into one versus scalar-sized fragments charged the
  same **404 / 428 / 440 bytes** for ASCII / Chinese / emoji, respectively, and
  completed. Default JSON string representations would have been 686 / 746 / 818
  bytes, but those wrappers are not charged for `TextContent`.
- A two-leaf actual-SDK tool flow charged **31 argument bytes + 43 serialized tool
  result bytes + 413 final-text bytes = 487**. An explicitly labelled 487-byte
  comparison cap completed; 486 failed. The first leaf's known usage survived,
  while the interrupted second leaf's usage stayed unknown. These small comparison
  caps characterize cumulative charging and are not changes to the default.
- Empty-text reasoning charged 99 bytes alone, 131,170 with opaque data, 131,185
  with additional properties, and **262,256 with both**. The combined payload
  correctly exceeded the default cap; removing reasoning accounting wholesale
  would discard real resource protection. `RawRepresentation` did not alter the
  current charge, consistent with its existing serialization exclusion.
- A synthetic informational call containing **262,145 argument characters** was
  ignored and the proposal completed with zero executed tools. This is an existing
  arbitrary-payload accounting gap, not permission to execute such a call or a
  demonstrated property of the latest provider response.

Only counts, fixed labels and outcomes are retained from these synthetic probes.
No real reasoning, arguments, tool output or source content was captured.

## Evidence-backed correction proposal

The actual-SDK comparisons establish chunk-envelope inflation. Keep the same
finite file-wide cap and charge recognized textual payload as strict UTF-8:
final text and reasoning text each contribute their real SDK string bytes. This
means text in the content field, not decoded semantic fields of a final JSON
proposal. Continue charging executable call arguments and complete tool results
as structured payload; do not reset the counter between leaves or windows.

Opaque reasoning `ProtectedData`, and annotations/additional properties on both
recognized text types, must be charged when present. Unknown content retains a bounded full-serialization
fallback, or fails closed if it cannot be safely measured. Repeated actual metadata
is still a resource cost; only empty/fixed per-delta wrappers and text escaping are
candidates for removal from the logical text charge. Preserve the update, input,
token, tool-call, lifetime and settlement bounds. This is a logical payload bound,
not a new claim of a raw transport or peak-memory limit.

Before implementation, decide explicitly how to bound or reject informational
function-call payloads and unexpected payload-bearing usage metadata. Their current
omission must not silently become permission for unbounded arbitrary content.
No informational call may gain executable authority.
The recommended choice is to charge informational calls by their full serialized
representation while retaining their non-executable treatment, and to charge
extensible usage metadata while leaving the fixed numeric usage counters in their
existing accounting role. Reject unsupported representations that cannot be safely
measured. Integrator must explicitly accept this gap-closing boundary along with
the reasoning change; it is not implicit permission to ignore arbitrary payloads.

Recommended acceptance checks for an approved correction: identical recognized
text must charge identically across valid scalar-aligned fragmentation; exact-cap
and cap-plus-one behavior must remain explicit; opaque/metadata and unknown-content
payloads must still exhaust a finite bound; tool arguments/results remain cumulative;
unknown usage, terminal validation, call settlement and the update cap remain intact.
Do not replace these controls with a larger limit or trust provider token counts as
the only resource bound.

Candidate owned changes are the executor's accounting helpers and existing Worker
tests. No provider token-limit, shared contract, Knowledge schema, tool capability
or runtime profile change is proposed. This document is not implementation
authority: Integrator must decide the bounded correction after reviewing the
measurements. No live probe is authorized here.

## Review and handoff

The test owner assessed all nine passing cases. Independent review accepted this
test-and-assessment checkpoint with no blocking findings; the read-only accounting
review concurred. The 99-byte envelope measurement is specific to the installed
SDK and controlled fixtures. Production, prompts, caps and provider token limits
remain unchanged. Only the existing Worker test file and this assessment changed;
no new qualification dataset, native/provider/PostgreSQL operation or runtime
resource binding was introduced. The next decision is the explicit bounded
accounting correction above, followed by its own RED/GREEN and review. Paid repeats
and any claim about the latest live byte contributors remain held.

## Authorized bounded correction

After assessment commit `d3240f85fc3a24cfa530268661dbf381414b2721`, OCT-Integrator
(`01a1109d-ec4c-7500-8b73-642a5e22f59b`) explicitly approved the correction above,
including informational-call and payload-bearing usage-metadata gap closure.
Integration records the decision at `1b1306398df749830285481ad4b4290cb42be9a4`;
its executor and Worker tests match this lane's committed baseline. No reset or
replacement of the completed implementation was needed.
The original assessment and measurements remain historical evidence. The approved
write boundary is the executor accounting helpers, existing Worker tests and this
record, with separate test/production owners, RED/GREEN and independent review.
Measurement must itself be bounded by the remaining allowance; it must not create
an arbitrarily large serialized representation merely to measure its size.
No provider configuration, shared contract, schema, tool capability or execution
limit change is authorized. Real provider/native/PostgreSQL/source-root operations
remain with Integrator; the controlled defect is not proof of the last live run's
byte contributors. Verification and review for the correction are recorded below
when complete.

The first correction build (`5d784608949743059c02180f84d73e69`) caught a test-only
attempt to subclass sealed SDK `TextContent`; that impossible case was removed,
with unknown-content coverage preserved. This build failure was not behavioral RED.
Behavioral RED on unchanged production is `841f889d2c9c4a23a34d42cc318e190d`:
**180 selected, 165 passed, 15 failed, zero skipped**, build zero errors. Failures
cover reasoning fragmentation/exact cap, ignored informational/text/usage metadata,
malformed scalar handling, cyclic/deep metadata and missing bounded traversal.

Initial correction GREEN is `8ef779d7726543e5adc82869b62d7069`: **184/184 passed**,
zero failures/skips and build zero errors. After RED, the traversal probe gained a
valid final proposal/terminal (the omitted metadata previously led to a missing
terminal), and four exact metadata/informational-charge controls were added.
Those added controls are GREEN coverage, not claimed as separate RED evidence.

Recognized text and opaque reasoning strings now use strict UTF-8. Present
annotations/additional properties are charged as their serialized collections;
usage `AdditionalCounts` is charged too. Fixed usage counters are observed before
metadata charging, so actually observed usage survives a later metadata failure.
Informational calls charge their full polymorphic JSON without executable authority.
Executable arguments and tool results retain their original serialized byte costs.

Structured measurement uses a private bounded buffer and charges actual written
bytes, not reservation sizes. Scratch reservations are limited to 18 times the
remaining allowance plus 4 KiB (sixfold escaping and conservative threefold
transcoding reservation). Strings are checked before escaping; retained
`JsonElement` content is checked against six times the remaining allowance plus
4 KiB before writing/unescaping, without a new DOM or full string copy. Unsupported
content fails closed. These are finite measurement bounds, not a raw-wire,
exact peak-memory or scan-time guarantee: reading an already-retained oversized
scalar still takes work. Serializer metadata resolution and trusted host getters
are not a general sandbox for arbitrary CLR execution.

Independent review identified that arbitrary custom CLR metadata converters could
bypass the string precheck by writing directly. A finite, source-free converter
probe established RED at `3fb9e30da66d4149aae0673422c4b936` (**one selected, one
failed**, unexpectedly completed). This is a trusted-host extensibility case, not
evidence that provider JSON can instantiate CLR converter types. A measurement-only
type/property converter guard rejects unsupported converters before their `Write`
method is invoked; built-in serialization and the two bounded converters remain
available. Metadata resolution/getters are still not sandboxed. A property-level
control supplements the initial type-level RED.

Final expanded controlled run `f60d5b3cbab2443b888176ebc1ae846d` selected all
October Scan SelfContained tests: **727 selected, 726 passed, one failed, zero
skipped**, build zero errors. All **187/187** affected Worker/ExecutionScope/
GenerateRunner checks passed, including both custom-converter rejection cases and
the built-in property-converter exact-cap compatibility control.

The broader run is **not GREEN**: unchanged
`OctoberScanBusinessTransportTests.ActualExecutor_DelayBeforeFactoryFinalGuard_CannotSpendAnOldRateReservationAsync`
failed at line 204 (expected two HTTP calls, observed one). Its path invokes
`OctoberResolutionExecutor`, not the modified file Worker. Integrator received the
failure; its assertions and timing were not changed. The same case passed alone
at `7d606bccf57b4a03b616ee5c82e26011` (**1/1**, zero failures/skips). That isolated
pass neither explains nor erases the expanded-run failure. Independent review accepts the owned accounting boundary on
187/187 affected GREEN; it does not waive the broader failure.

The correction remains confined to the executor, existing Worker tests and this
record. No shared bindings, migrations, provider settings or runtime permissions
changed. Integrator owns integration and any subsequent real qualification; the
last live run's byte contributors and native JDT fallback remain unproven by this
controlled evidence. Earlier retained partial CL and resources remain untouched.
