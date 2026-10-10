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

## Confirmed one-run authorization and real result

The user subsequently answered **"Confirm"** in this chat to the explicit request
to send relevant content from all 77 manifest-verified prepared files to
`https://api.deepseek.com`, model `deepseek-flash`, using the existing protected
credential, with only `GetMobileByAccessCodeOp.java` selected for CL generation and
evaluation answers excluded. This resolved the launch approval block above. One
Generate invocation ran; the create-new invocation marker is consumed and no retry
was performed.

Real run target `phone-75362f1af6b5`, batch
`82e9b113-e9ec-4f87-8b28-6cbae66a06f8`, completed in **88.11 seconds** with
exit 0, no timeout, and `NeedsSettlementCheck=false`. The retained report
`generate-04efc49e588b4a2db1ceff2e33e68a43.stdout` records:

- Requested/completed stage: `cl-only`.
- Execution: `Completed`; selected-file coverage: `Complete`.
- One accepted file, one returned CL Document, zero gaps; inventory: 77 files.
- Publication: `Confirmed`, ID `d464ab3c-43a7-4308-9caa-86d0c1361806`.
- Logical Document `3f36e9a6-fc74-aff3-e4c5-7eb74fd6eb55`, exact version
  `8823c2b5-b12e-4870-bdfe-fbc629ef4a96`.
- Matching and Business Logic: `not-requested`; Worker diagnostics: null.
- Three provider requests, 12,439 input tokens, 3,700 output tokens, unknown
  usage false. Standard error is empty.

All 77 prepared source files still match their approved hashes after execution.
The owned database, source binding and required keys are retained. There is no
running Web presentation host. No raw provider response/reasoning was retained.
The in-memory observer reports parse failures only, so a successful run has no
HTTP/SDK/Worker comparison snapshot; its absence is not comparison evidence.

This establishes real source-to-accepted-CL-to-confirmed-publication success for
the single selected file with the replay fix. It does not establish ordinary IT
reader/UI qualification, full-slice completion, BL, Chat or graph behavior, nor
prove the cause of the earlier single-request JSON truncation. The unchanged
full-suite report-parser failure remains outstanding. This one-run paid grant is
consumed; further provider execution requires another user instruction.

## User-authorized full CL attempt

The user next requested **"Try a full CL scan."** One fresh batch selected all
77 exact manifest files, with `cl-only`, the same provider/model, existing finite
limits and zero automatic retries. All 77 hashes matched before and after the
run. The successful one-file publication above was preserved in its original
database. A fresh owned database and fresh prepared tool workspace were used;
the existing PostgreSQL container/volume were reused without alteration.

Target `phone-096e63253cff`, batch `9247a399-fccf-4051-aef3-6d382c1b9635`:
prepare/select both exited 0. Generate report
`generate-bbd2fde7788c44118635ce1e8e632120.stdout` records exit 1 after
**26.47 seconds**, no timeout and `NeedsSettlementCheck=false`:

- Inventory/selected: 77/77; accepted files: 0; Documents: 0.
- Execution `Failed`, coverage `Partial`, publication `NotRequested`.
- First failed file: `mbs_channel/pom.xml`; no later file was attempted.
- Worker `InvalidOutput`, stage `StreamTerminal`, terminal failure
  `UnsupportedFinish`; retry disposition `None`.
- One provider request, 9,748 input tokens, 4,011 output tokens, unknown usage
  false. Standard error is empty.
- No parse failure, tool failure, raw capture or HTTP comparison snapshot.

The current guard classifies every observed finish other than Stop/ToolCalls as
UnsupportedFinish; the report does not preserve its actual value. Therefore this
does not establish Length, ContentFilter or another terminal cause. The observer
emits only at proposal parse failure and supplies no additional evidence here.
This is a new terminal rejection, not proof of a recurrence of malformed JSON or
reasoning replay failure. No assertions or runtime policy were weakened, and no
further provider request was made. The fresh resources are retained for diagnosis.

Next diagnostic requirement: retain the sanitized exact finish reason and admitted
output cap/usage at this failure boundary before attributing the cause or changing
limits. Full CL completion and ordinary-reader qualification remain unachieved;
the prior single-file accepted/publication result remains valid for its own scope.
