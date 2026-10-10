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
