# Story 12.3 Transcript-Budget Planned-Change Authorization 04

- Decision ID: `story-12-3-planned-change-authorization-04`
- Status: Accepted
- Decided at: `2026-08-24T15:03:50.3910326Z`
- Scope SHA-256: `99f542e15eaddf6c50c5381225c0c719ebe1f82c6c4f8774f5d565c6c835e63a`

Scope construction: SHA-256 of this exact UTF-8 JSON object with no trailing
newline:

```json
{"assembled_response_bytes":16777216,"assistant_replay_aggregate_bytes":16777216,"assistant_replay_per_message_bytes":16777216,"outgoing_request_bytes":17416192,"raw_response_bytes":16777216}
```

## Decision requested

Authorize an architecture correction to the Story 12.3 execution-local
thinking-mode transcript budgets after the fresh T084 `r9` qualification
proved that DeepSeek cleared the corrected raw SSE limit but exceeded the
unchanged 131,072-byte assembled-response ceiling before finalization.

The user explicitly approved the exact scope above. The approval authorizes
the plan correction and may authorize later task generation only if the
independent plan-only review confirms that the corrected plan matches this
scope exactly. It does not itself authorize production implementation.

## Approved design

- Retain the per-response raw streamed SSE ceiling at exactly 16,777,216
  bytes.
- Raise the per-response assembled reasoning/content/tool-call ceiling from
  131,072 to exactly 16,777,216 bytes.
- Raise the per-assistant-message replay ceiling from 131,072 to exactly
  16,777,216 bytes.
- Add an exact 16,777,216-byte aggregate ceiling across all prior assistant
  contributions in one outgoing request, preventing nine individually legal
  messages from multiplying memory/request exposure.
- Raise the complete outgoing serialized request ceiling from 1,818,624 to
  exactly 17,416,192 bytes: 131,072 prepared initial contribution + 81,920
  declarations + 16,777,216 aggregate assistant replay + 393,216 aggregate
  tool results + 32,768 fixed envelope margin.
- Count assembled and outgoing contributions after final UTF-8 serialization;
  exact bounds pass and byte plus one cancels/no-sends through the existing
  closed failure path.

## Preserved boundary

The 32,768 output-token setting, ten-response/25-minute attempt ceilings,
five-minute resetting inactivity cap, zero SDK retries, two strict functions,
eight navigation attempts, two submissions, 65,536-byte candidate/evidence/
schema limits, 81,920-byte declaration limit, complete reasoning replay,
local `code-logic/file-v2` authority, atomic immutable Document commit/replay,
source-free/provider qualification order, and privacy/nonpersistence rules
remain unchanged.

No database, migration, API, UX, scheduling/concurrency, provider retry,
durable transcript, raw-response buffering, Story 12.4/12.5, or
CodeGraphContext work is authorized.

## Remaining gates

Architecture-plan correction and self-review; independent plan-only review;
regenerated tasks; fresh combined definition review; mandatory implementation
approval binding the accepted artifact hashes; public-seam RED/GREEN;
source-free regression; fresh T084 isolated 3/3 qualification; T085 full T062;
convergence; quality review; conditional E2E; final verification; and the
root-owned checkpoint/state/commit binding.

## Decision

The root may correct and review the plan against the exact scope hash above.
Production remains prohibited until the accepted corrected definition receives
its separate implementation approval.
