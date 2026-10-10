# Reasoning replay correction and one paid trial

The user authorized "fixing reasoning replay next, then try a paid scan" on
October 10, 2026. Assigned worktree `f8dc/vulgata`, branch `oct14/scan`, was clean
at `0234e570eb9efd20a86c300baae146788772302b` before this correction. No skills or
coordination messages to other chats were used. Separate test ownership and
independent review were retained.

## Correction

The Worker now retains already-accounted reasoning text/protected data in its
provider history, separate from `LastText`, proposal parsing and failure capture.
Raw SDK objects and arbitrary metadata are not copied into history. Existing
input measurement accounts replay on subsequent requests; limits, admission,
source currentness, cancellation, tool counts, settlement and retries are unchanged.

Preserving Worker history alone was insufficient. The installed OpenAI chat
adapter reads `reasoning_content` but omits it on outgoing history conversion.
The selected-client factory therefore clones affected assistant messages and uses
standard SDK message conversion plus its extension-field patch mechanism to add
the exact concatenated reasoning. Caller history is never mutated. This applies
only to Worker streaming at the existing exact DeepSeek HTTPS root/default-port
endpoint predicate. Generic endpoints and Business nonstreaming are unchanged.
The numeric `max_tokens` adapter, thinking mode and JSON acceptance remain unchanged.

References: [DeepSeek thinking mode](https://api-docs.deepseek.com/guides/thinking_mode/)
and [Microsoft adapter issue 7525](https://github.com/dotnet/extensions/issues/7525).
Neither establishes the cause of the earlier one-request malformed JSON.

## Controlled evidence

- Original behavioral RED: `isolation-023be6ade0ac42a4bb88b80566b739a7`, two
  cases selected, both failed on missing outgoing `reasoning_content`.
- Worker-only fix: `isolation-2ffa64698d0849978ab5a4af782cb262`, 470/474 passed.
  Four replay cases still failed, requiring the outgoing compatibility correction.
- Tests require exact whole/fragmented Unicode replay, all prior tool rounds,
  plain assistant history across source windows, unchanged tool association and
  settlement, exclusion from proposal/capture/results, endpoint boundaries and
  caller immutability. The direct factory test bypasses MAF/Worker aggregation.
  Reviewer-requested ASCII exclusion markers avoid false passes from JSON escaping.
- Final full SelfContained: `isolation-f2fa92bec79f44b295415a02102a84a6`,
  **4,701/4,702 passed**, zero skips. All **481 affected class cases** and all
  **11 replay/direct-factory endpoint cases** passed. Build had zero errors.
  The only failure remains the unchanged
  `OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`
  report-shape mismatch recorded in `search-validation-replay-verification.md`.
  The full suite is not GREEN and no release-readiness waiver is claimed.
  Independent final review accepted the scoped fix with no outstanding findings,
  explicitly retaining that full-suite limitation.

## Paid-trial boundary

This is one fresh CL-only Generate invocation, without automatic retry or raw
capture, selecting the previously failing `GetMobileByAccessCodeOp.java`. The
remaining approved slice is supporting source only. All 77 source hashes matched
the approved manifest before materialization; fresh copies avoid changing prior
CodeGraph state or business source. Existing provider/model/protected credential,
8,192 per-call maximum, finite input/output guards and 30-minute run/5-minute file
deadlines remain. The previous diagnostic's 100 per-file requests/windows and zero
transient retries are retained. The original source/provider/resource approvals
are recorded in `docs/bmad/planning-artifacts/october-retained-pilot-2026-10-09.md`;
the current user instruction supplies the new one-run paid authorization.

Bootstrap used this worktree's binaries, a fresh owned database in the existing
PostgreSQL container, protected local keys/configuration, and a temporary loopback
Web host which was stopped afterward. No existing database, container, volume or
other worktree was modified. Prepare/select both exited 0 without timeout or
provider calls. Exact local resource identities and outputs remain in the protected
run directory, not tracked configuration. A create-new invocation marker prevents
an accidental repeat. The existing bounded in-memory HTTP observer is enabled;
raw responses and reasoning are not retained.

## Launch blocked before execution

The reviewed correction is committed as
`5e02dbb6d75007c4889ba0c3d2762933d9d320ce`. The paid Generate command was rejected
by automatic approval review before process creation. Its reason was missing
exact source-payload/destination authorization in the current transcript.

Read-only history verification found direct original user approvals in chat
`01a1109d-ec4c-7500-8b73-642a5e22f59b`: message
`01a11fd2-01e9-71a2-8edc-dc0793389b73` named the prepared login slice, DeepSeek
endpoint/model/protected credential and available-balance spending; message
`01a12028-a49f-71e2-8f93-5e01b6e04186` confirmed the immediately preceding
request for relevant supporting content from all 77 files, excluding evaluation
answers. The same command was resubmitted with those references. Review again
rejected it because cross-chat tool output was not accepted as trusted user
authorization in this transcript. No indirect execution or further retry followed.

No Generate process, provider request, accepted file or Document resulted from
this attempted trial. The invocation marker is absent and no Generate log exists.
Prepared database/keys/configuration remain retained for the user's decision;
the bootstrap host is stopped. Exact source-scope/destination confirmation in
this chat is the remaining launch requirement. Real replay behavior and JSON
truncation remain unqualified despite passing controlled replay tests.
