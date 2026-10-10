# Bounded Worker stream aggregation

## Authority and diagnosis

On October 10, 2026 the user instructed "Proceed" after the fixed-output POM trial
hit `BudgetExceeded / StreamUpdates`. Work resumes in the assigned worktree on
`oct14/scan`, clean at `101bdec4`. The preceding failure and unknown usage are
retained in [output reservation evidence](output-reservation-verification.md).
No skills or external coordination messages are used.

The Worker retained every `ChatResponseUpdate` in a list solely to read the final
update's response ID. Useful text, reasoning, calls, finish and usage were already
aggregated separately. The list's 8192-update limit therefore bounded transport
fragmentation, even for nonempty payload comfortably below the output-byte cap.
The prior real failure proves this local guard fired; it does not establish the
provider's eventual finish reason, usage or proposal validity.

## Reviewed bounded correction

Remove the full update list and retain only the last response ID, including null
on a final ID-less update. Coalesce plain reasoning text while preserving the
ordered protected segments, including empty protected values. Preserve the presence
of an empty plain-reasoning run as one marker: omission could change wire field
presence. Independently bound retained reasoning segments to 8192, including
zero-payload segments within a single update, before adding each segment.
Every incoming text/protected/metadata payload is charged before retention using
the existing byte accounting. No raw SDK object is copied into replay history.

Keep a limit of 8192 cumulative non-progress updates per leaf. Progress requires
an increase in charged output bytes. Numeric usage, empty content, roles, finish
and changing response IDs do not reset or evade that limit. Positive-byte updates
are bounded by the existing cumulative byte allowance, so total updates are
bounded by that allowance plus 8192; alternating empty updates and small payloads
does not renew a flood budget. Check cancellation before and after each iterator
move, including synchronously completed moves, and during content processing.

Preserve all token reservations/caps, byte limits, deadlines, leases, source and
authorization checks, terminal ordering, complete-tool-call validation, numeric
usage settlement and atomic proposal acceptance. Do not yield partial drafts,
increase the output allowance, change thinking, repair JSON or skip the POM.

Independent design review accepted the approach with the cumulative rather than
consecutive non-progress limit and explicit preservation/bounding of empty reasoning
markers. Separate test and production ownership are retained.
Controlled results and real qualification are recorded separately after execution.

## Controlled verification

- Initial RED `isolation-9ad9c5dd366648328912334ac7040595`: 8 selected, 7 failed,
  1 passed. Actual-SDK text/reasoning fragmented into 8300 updates hit the original
  stream guard instead of reaching Stop or Length/usage. A synchronous stream
  canceled during its first completed move kept iterating to BudgetExceeded
  instead of returning Cancelled. One replay assertion in this selection was
  subsequently found to target the wrong boundary, as described below.
- Revised empty-preservation/segment-bound RED
  `isolation-5b43571158154df3a8c5e0ac6cd470df`: 3 selected, 2 failed, 1 passed.
  The old implementation admitted 8193 empty protected segments in one update;
  exactly 8192 already passed. Revised tests preserve empty field presence rather
  than asserting its omission.
- Separate test-owner final assessment and independent code review found no
  findings. Controlled GREEN and complete SelfContained outcomes follow below.

First affected run `isolation-f4fbcd1d16b844b69a428d8db84ff297` passed **432/433**,
zero skips. The sole failure expected nine reasoning segments after downstream
MAF aggregation. The baseline RED and corrected production both actually yielded
the same three downstream segments. Installed SDK documentation describes this
coalescing. That assertion was not evidence of a regression or of the Worker's
pre-MAF representation. The test owner is correcting the check to assert exact
Worker output directly and separately compare downstream text/protected-data
pairs against legacy aggregation. Production is not changed to override MAF.
Direct Worker preservation is not a claim that MAF retains identical segmentation.

Corrected Worker-boundary RED `isolation-25ba2f61512a4457b436840281836ad3` failed
**2/2** against the pre-change Worker at `101bdec4`: adjacent `d`/`e` fragments
remained separate instead of coalescing to `de`. This checks actual owned behavior,
including null/non-null final response IDs. The exact current production file was
saved before this bounded baseline reproduction and restored byte-for-byte in
`finally`; no Git reset or unrelated change was used. The corrected test separately
checks downstream text/protected-data pairs against the installed SDK's legacy
aggregate behavior. Test-owner and independent final review accepted both checks.

Final affected GREEN `isolation-7c54692553574c7ba77f065ce9d4faf2`: **434/434 passed**,
zero skips, through `eng/test.ps1`. This includes eleven new bounded-stream cases
and the existing byte, token, tool, source, authorization, settlement, terminal,
reasoning-replay and Generate regression coverage. No assertions, byte/token caps,
timeouts or authorization guards were weakened to obtain GREEN.

Full SelfContained `isolation-d2477221d45d4327a612f6ba9cc1df6b`: **4,741/4,743 passed**,
two failures, zero skips. All bounded-stream cases passed. The full suite is not
GREEN and this record does not waive its failures:

- The previously recorded exact report-shape mismatch in
  `OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`.
- The previously observed controlled 40 ms inactivity timing case,
  `Pass1DeepSeekStreamingQualificationTests.ExecuteAsync_StreamingUpdatesResetInactivityDeadlineAsync`,
  returned ProviderUnavailable. It uses synthetic in-memory SSE, not DeepSeek.
  That legacy executor was not edited. Prior isolated passing evidence does not
  erase this failure or establish its cause. No timeout/assertion was changed.

The prior BL operator-exchange cleanup case passed in this full run. None of the
full-suite outcomes is PostgreSQL/native/provider/ordinary-reader qualification.

## Bounded real-trial preparation

The current user instruction authorizes the next isolated POM trial after the
correction. Applicable source/provider/resource approvals remain those recorded
in the retained-pilot and replay evidence. A fresh owned target
`phone-04a64b5ff694`, batch `9ad649bf-7989-4c5f-98e6-4be4173d3ac6`, was bootstrapped
without modifying prior databases or the existing container/volume. The temporary
loopback Web host stopped after bootstrap. All 77 source hashes matched; only
`mbs_channel/pom.xml` is selected and the remaining files are supporting source.
Prepare/select passed without provider calls. Fixed 8192 output, 40960 reservation,
provider/model, thinking, prompt, tools, finite deadlines, byte limits, cumulative
policy and zero automatic retries remain unchanged. No raw capture is enabled.
Physical paths, keys and credential bindings remain in protected local files.

## Real POM result

One Generate invocation ran on reviewed commit
`c17b5caed7d2c4bcf2e0269ba4ad20b6ba8a5f89`. Report
`generate-686ff7fc856143119d64af2c555f83d7.stdout` records exit 1 after **38.91
seconds**, no timeout and `NeedsSettlementCheck=false`:

| Observation | Value |
| --- | --- |
| Stage / execution / coverage | cl-only / Failed / Partial |
| Accepted files / Documents | 0 / 0 |
| Publication | NotRequested |
| Worker failure | InvalidOutput / StreamTerminal / UnsupportedFinish |
| Finish reason | Length |
| Requested output cap | 8192 |
| Provider requests | 1 |
| Input / output tokens | 9748 / 8192 |
| Reasoning tokens | 8192 |
| Unknown usage | false |
| Retry disposition | None |

No stream-count or byte-budget failure was reported. The corrected stream path
reached the provider's terminal finish and usage instead of stopping at the prior
local update-count guard. This is direct evidence that the provider exhausted the
unchanged 8192 completion allowance, all reported as reasoning; it does not prove
that a larger allowance would finish successfully. No proposal-parse failure or
HTTP comparison snapshot exists for this terminal rejection, and no raw response
or reasoning was retained. Historical Stop/malformed-JSON failures remain separate.

Standard error is empty. All 77 prepared source hashes still match. The fresh
owned database/workspace and prior successful publication remain retained. The
bootstrap Web host is stopped. This is real source/provider failure evidence,
not successful generation or ordinary-reader qualification.

The POM success condition was not met: **no full-slice retry and no further paid
request ran**. The next separately bounded sizing experiment would be an explicit
16384 completion allowance with sufficient pre-dispatch reservation, preserving
thinking/prompt and all other guards. This run does not authorize or implement
that increase, automatic escalation, JSON repair or acceptance of incomplete work.
