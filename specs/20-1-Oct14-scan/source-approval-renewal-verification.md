# Source approval renewal between primary files

October11 whole restart6 reached23accepted files then SourceChanged at611.28s.
All77 source hashes remained unchanged. Owned expiry metadata and current code
identify10minute source approvals, not revoked, while Scan opened runtimes once
for a30minute run. Expiry is continuing authority loss, not proof that hashes
changed. Paid restarts7-10 are held while this concrete lifecycle issue is fixed.

The user's current instruction authorizes correcting concrete application errors
within existing guards. Independent plan assessment and the consulter's final
advice support a consumer-only replacement using existing authority APIs. Web
authority policy, Identity, Business schemas/contracts and approval lifetime stay
unchanged. This is distinct from JSON finalization; text-mode and hardmode remain
unimplemented and conditional.

At a settled primary-file boundary, before acquiring the next frame, inspect all
selected repository runtimes. If validity cannot cover the next file interval plus
bounded preparation allowance (capped by original remaining run/session), verify
the old approval is still Current, confirm native runtime cleanup, verify old
authority again, issue a genuinely new exact-scope approval and verify old authority
again before adoption. Never revive an already expired/revoked approval; deny on
unavailable authority, mismatch, insufficient lifetime or unsettled cleanup.

Replacement lifetime remains bounded by the original monotonic run deadline,
original session and new issuer expiry. No reauthentication or selection change.
All repositories are considered because an idle repository can expire while
another repository is being processed. Only new primary-file work can use the new
approval; no same-file history/identity replacement. Accepted origins retain their
old PreparedVersion; existing freeze contracts preserve each exact origin and do
not require a homogeneous approval identity.

The21-session native CodeGraph ceiling must be shared per repository across
runtime replacements;10calls remains a per-native-session limit. Other run/file,
provider, byte, tool and correction counters are unchanged. No renewal-loop
fallback: one issuance per repository per boundary; failed construction/adoption
must dispose any candidate and stop before paid work.

Owned files: OctoberScanRunSourceFactory.cs, OctoberToolRuntime.cs and the Scan
runner source boundary. Controlled tests cover renewal timing, denial/races,
original deadlines, cleanup and cumulative native capacity. Shared files remain
read-only. Review/tests/real outcomes follow below.

Controlled RED: initial build `isolation-fa57570712d9476a871acc2b62db9d4d`
failed on a test-draft reference to a non-existent Approval property. It is not
behavioral evidence. Corrected tests capture the constructor's supplied approval.
`isolation-8e1d13eed81f4da2af3be5c0d81717e0`:65 selected,60passed,5failed,
zero skips. Failures establish missing near-expiry renewal, expired/revoked and
post-issuance revocation refusal, and missing runner file-window ordering.

Implementation uses an explicit shared21-session counter per repository and
closes old native resources before reissuance. Old-current checks precede cleanup,
precede issuance and follow issuance before replacement construction. Run/session
limiting cases do not repeatedly seek approval; expired bindings still stop.
Typed source_window diagnostics distinguish expiry, authority, binding, cleanup
and insufficient lifetime. Accepted origins are not modified. Source closure and
fault state prohibit further source use after a failed renewal.

Initial controlled GREEN: `isolation-1ce93d29a69044f889eaaabe87c5688c`,
1062 selected/passed, zero failures/skips. This predates review corrections.
Independent review found sequential repository refresh could consume an earlier
binding's margin, and default600second file policy plus30seconds could never fit
the issuer's600second grant. Both are accepted findings.

The corrected boundary caps its fixed renewal target at the shortest initial
grant lifetime minus30seconds and rechecks every binding after all replacements.
It returns the minimum current remaining authority minus30seconds for settlement.
Runner timestamps before renewal and caps the effective file deadline by that
returned window, the configured file maximum and original run deadline. Short
grants therefore shorten execution; they never extend issuer authority. An initial
grant of30seconds or less fails before dispatch. The reviewer accepts this
conservative algorithm; final test/implementation review remains to be recorded.

Final independent production and test review accepted the implemented boundary.
The pre-renewal timestamp deliberately counts issuance latency conservatively;
this can shorten execution but cannot expand authority. Added tests cover the
default600second grant, sequential repository renewal consuming an earlier
margin, and zero usable authority preventing provider creation/calls or acceptance.

Full SelfContained `isolation-ba1ce120a67c45bb9c3e1baa132e345c`:
4832 selected,4830passed,2failed,0skipped. The known strict generated-report parser
failure remains. The other failure is historical controlled
Pass1DeepSeekStreamingQualificationTests.ExecuteAsync_StreamingUpdatesResetInactivityDeadlineAsync
(10ms updates/40ms inactivity threshold, ProviderUnavailable); it does not execute
the changed October source factory/runner. No assertions were weakened. This run
predates the last two added regression cases; targeted final verification follows.
Full-suite GREEN is not claimed. No native/provider evidence follows from it.

Final affected GREEN `isolation-4e47228a10494deda89437984d9c7636`:
70 selected/passed,0failed/skipped, including both review regressions and the
zero-window dispatch guard. Generator and test-copy assembly SHA256 both
`EF06DD88EE7E7ACB30E564676399DA4101EE247C6DEC4AF7753888473A768F52`.

The unchanged historical streaming-inactivity test passed1/1 when rerun alone
through the same runner: `isolation-e59773a2067e47ba8277e47313b896eb`.
This does not erase its full-suite failure or prove complete regression GREEN.

Committed implementation: `b4a8e5fe926d8f56decfe724782baeae54d67d2e`.
Real isolated QueryAllCreditCardOp.java trial `renewal-isolated-01` passed in33.28s,
Completed/Complete/Confirmed,1accepted file/1Document, no timeout or uncertain
process settlement. All77 prepared hashes match. One request,4208input/4083output
tokens, known usage. Root phone-bec479723a0c, database
vulgata_oct_pilot_cadb0a89eeb143cc83f6d9378598f3d6, batch57f6f564-fa3e-43a6-b07c-79702c26c0a8,
report generate-a49bfd6c14e84fa99c83c0d7cb9fedb6.stdout. Retained publication
80e97dd9-9d52-470b-b313-fbedf26931a6, logical Document
c5386ea8-d4e0-fd3b-27e1-fcc203b4f1bb, exact version1dfc7bc8-af56-48b2-a87d-391b25a2b30a.
This short run does not exercise renewal or ordinary-reader access. Whole restart7
follows within the existing bounded series; JSON behavior remains unchanged.

## Real renewal exposed retained-descriptor ownership defect

Restart9 reached13accepted files and then source_frame_unavailable at285.51s.
Both new approval rows were issued at17:56:00UTC (expiry18:06:00UTC), immediately
before failure; old rows were still current. All77source hashes matched, and the
next userAuth.xml file was1449bytes/valid UTF-8. Runtime replacement had disposed
the descriptor, while its intentional disposal retains .codegraph. The next
PrepareAsync rejects that preexisting state by design. This is a defect in this
Scan lifecycle correction, not a reason to bypass the factory's ownership guard.
The earlier controlled tests used inert runtimes and missed the descriptor handoff.

Plan.md109-114 requires one preparation and retained same-process descriptor per
Repository. Independent assessment accepts a per-run/per-repository CodeGraph
owner holding only that exact descriptor and its original run/session lifetime.
Replacement runtimes borrow it, keep their own new source approval/verifier and
close native sessions/JDT before replacement. Final source cleanup closes all
runtimes then owners, even on cleanup failure. Failed creation is terminal and
cannot retry against partially created state. No deletion, path-based reconstruction,
foreign adoption, shared schema or authority-policy change. Original session count,
source/root/state/currentness and deadline checks remain mandatory. Restart10 is
held for controlled RED/GREEN and review of this correction.

Ownership regression RED `isolation-d086a8cddf2f449197a2fd50a941b5d3`:
34selected,32passed,2failed,0skipped. Runtime preparation received its1minute
authority-bound lifetime rather than the5minute owner lifetime; factory replacement
had no shared owner. These are behavioral failures, not build errors.
The correction keeps original preparation under the existing current-approval,
30second lease/preparation cancellation checks and the runtime cancellation token;
only descriptor retention uses original run/session remaining time. Every new
source context still verifies the replacement approval and exact root/state.
Added a real contained-read controlled test against test-owned files: two approval
identities produce their own immutable frame origins using the same descriptor,
one preparation and no native/provider call. Runtime disposal settles borrowers;
owner disposal releases the descriptor once. Final full-suite verification follows.

Independent implementation review accepted the owner correction. Full SelfContained
`isolation-e1f2ed78bdb748dbbb92d6d5c7a0418f`:4839selected,4838passed,1failed,
0skipped. All13 selected owner/renewal-named cases passed, including real contained
frames with different genuine test approval identities. The sole failure is the
existing generated-report parser case. No assertion changes or full-suite GREEN
claim. No native/provider qualification is inferred from these controlled tests.

Committed owner correction `2cb6dc2740f380eda2ea56d22cad4801652cdd94`.
Generator/test-copy SHA256 both
`A0E5D6F4C8682E192312872A4C7154B5D768773E97ACA86F7F1E2D96E1E83048`.
Real isolated channel userAuth.xml trial `owner-isolated-01` passed30.85s:
Completed/Complete/Confirmed,1accepted/1Document, settled/no timeout, all77hashes
match.2requests,9364input/3082output, known usage. Root phone-6c6b62ac2b31,
database vulgata_oct_pilot_adc4c107155e48deb0545a6b9b35804d,
batch540cfa67-a418-463b-a66d-93b520ebadbc,
report generate-0ace3fed8a6a462aa280b5fde377d692.stdout. Retained publication
9c0cc388-ed2a-46a0-aac2-09bdad7e3fb3, logical Document b74e86ef-9d03-59e4-5eee-bb7487bd6962,
exact version fed63014-2b91-49a8-9aa0-b1abfcc831cd.
The short trial does not qualify renewal, JSON correction usage or reader access.
Whole restart10 follows; no text-mode or hardmode change yet.
