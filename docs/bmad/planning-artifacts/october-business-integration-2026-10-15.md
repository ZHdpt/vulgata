# October Business integration — October 15

User request: integrate OCT-BUSINESS into the current branch after the local
reader repair was committed. Integration does not authorize runtime operations.

## Checkpoints and retained work

- Destination: `oct14/main`, `df5c15db` (large-publication reader repair), following
  `48868cde` (saved Chat history and viewport pagination).
- Incoming: `oct14/business`, `45e3e8177a14cb631d13a49ef3f0ab08cf9aad7b`.
- Merge preserves both histories. No reset, automatic stash, unrelated file
  cleanup, provider request, retained database access or running-host change.

Business brings continued BL generation, context overrides, bounded recovery,
retained resolution, referenced-source retrieval, synthesis/review corrections,
publication deadline handling and their tests/evidence. The included purpose-only
`BusinessSourceReadApprovalPurpose` migration is source integration only: it was
not applied by this task.

## Conflict resolution

- Project state preserves the newer reader and saved-Chat repairs. Business's
  earlier failed whole-run note is explicitly historical, preceding the later
  108-BL publication already recorded by main and reported in the Business chat.
- Disclosure policy had equivalent exact-advisory matching on both branches;
  retain the multiple accepted revision semantics and live authority checks.
- Selection authority retains main's exact-publication advisory ledger and adds
  Business's retained-finalization/context-override validation. An invalid retained
  override fails closed rather than bypassing validation via the ledger.
- Main's lightweight publication index, per-Document content loading, reader
  reuse, saved transcript fix, Chat UI and Explorer changes remain intact.
- Removed an incoming test file's trailing blank lines; no assertion was weakened.

## Verification scope

Use the current-source launcher with SelfContained and the combined filter:
`October|Vulgata.Tests.Explore|ChatPageTests|PreparedSourceAuthority` (each expressed
as a FullyQualifiedName substring). Evidence directory:
`TestResults/isolation-24b6dea1346c4941818cc35e62ae2cd7`.

Build completed with zero errors; existing analyzer warnings remain. The combined
run selected 3,134 tests: **3,125 passed, nine failed, zero skipped**, 6,315
discovered. Seven failures were the diagnostic projection test's old exact field
list; one was the old malformed-JSON admission classification. Assertions now
explicitly include the two approved original-publication diagnostic fields and
require InvalidJson with no false authority failure; no assertion was removed.
The ninth failure is the previously recorded strict one-file CL report parser
rejecting additional console fields. It is unchanged and unresolved by this merge.
Focused post-correction results are recorded below. No PostgreSQL, native-tool, External or E2E suite
is authorized by this integration run. No full-suite or real-content claim follows
from affected tests.

Post-correction focused verification passed **254/254**, zero failures/skips,
build zero errors, 6,315 discovered. It covers entry-point diagnostics, resolution
handoff, flow publication (including tampered-retained-request refusal), Knowledge
authorization, Web reader composition and read behavior. Evidence:
`TestResults/isolation-00a011d58f70463782301f30584817e0/results.trx`.
The combined 3,134-test run was not repeated after these test-only corrections;
its original result remains recorded, and full-suite GREEN is not claimed.

## Runtime follow-up

The lane reports publication `1396428a-7661-4761-b421-869e1fbc01eb` with 108 BL
Documents in the existing retained database. Main records 163 CL alongside them.
Neither content nor database state was independently queried by this integration.

Before using the merged build, requalify ordinary Business/IT reads, graph and Chat
against the intended exact publication under existing runtime ownership. In
particular, the merged selection-authority path now also validates retained
finalization payloads within its existing ten-second/32-MiB stored-read bounds;
the pre-merge real reader timings do not qualify this combined path. Do not raise
limits or bypass provenance checks merely to obtain a pass. No host was restarted
and no running process was adopted by this integration.
