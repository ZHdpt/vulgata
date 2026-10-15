# Bounded BL publication after exact-run fencing

## Authorized and executed boundary

The user accepted the proposed fresh paid attempt with a 20-minute run lifetime.
Assigned checkout was clean on `oct14/business` at
`8f84b17e9106411ea27f78ded7de00bc80173a04`. This was one new bounded invocation,
not a renewal of the expired one-hour retry allowance or a whole-corpus BL scan.

The run used target `phone-15bfe12615eb`, batch
`1a327657-6a55-4e65-ba06-6992f423d40e`, original CL publication
`fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`, and the retained pilot database. The selected
root remained account `authMethods` in `src/main/resources/flow/cust/userLogin.flw`.
All eleven v2 resolution outcomes were reused: four Supported, seven Unresolved.
No resolution calls, source tools, rescanning, Prepare or replacement CL occurred.

Provider remained DeepSeek `https://api.deepseek.com`, model `deepseek-flash`,
configured low reasoning, 393,216 output ceiling. Limits were 1,200 seconds,
26 total requests and 20 million total tokens; existing bounded structural
corrections remained enabled. A fresh marker and RunId were used. Protected
bindings and all prior publications were checked before launch; the previous
run's abandonment was independently looked up as AlreadyCommitted.

## Real execution result

- RunId: `0766b0ac-3fea-45c1-bfd8-ff14062626ac`.
- Owned process: PID 62220, started 2026-10-13 14:42:49 Asia/Shanghai.
- Finalization operation: `c53f3bcd-b965-4b5d-a1c4-fcb7af3d0b83`.
- New publication: `76616690-ea70-4ac0-93d0-bff60b457cad`.
- BL version: `8abd741d-67ad-8c1e-afa7-fab7793ccc0f`.
- Title: 手机银行登录流程静态业务说明.
- Process exit 0 after 174.96 seconds; no watchdog expiry or unknown settlement.
- One synthesis request: 9,882 input + 9,856 output = 19,738 known tokens.
- Model wait 55.57 seconds; report store wait 111.25 seconds.
- Publication Confirmed; execution, matching and Business phase Completed.
- Exact CompleteBusinessFromCl committed at 14:45:43 Asia/Shanghai.

The successful attempt demonstrates that the corrected run lifetime permits this
pipeline to reach synthesis and publication. No production code changed in this
turn; controlled-test and review evidence for its implementation remains at the
preceding checkpoints. This real run is separate evidence from those tests.

## Owner and ordinary-reader verification

The new publication contains the original 163 CL versions and one new BL version.
All seven earlier publications remained unchanged. The eleven stored resolution
witnesses were independently revalidated, including their complete contributor
sets; all four accepted static References are present in the new publication.
These remain same-repository resource references, not runtime-dispatch or
cross-repository completeness claims.

The selected BL group `4df34e48-1fb9-d7d1-feeb-b64101fc34c0` has one member, zero
member relationships, three resolution uncertainties and four frontiers. Its
complete dependency set retains all 163 CL versions. The four publication-level
links are outside this root's group. Therefore this run qualifies link publication,
but does not qualify a connected multi-document authMethods BL group.

A temporary, password-authenticated account with only the User role successfully
opened the canonical Business catalog and read the exact BL in one page (1,155
characters). The Business reader denied CL access. The temporary account was
removed and its absence verified. No Web startup/restart or existing-user change
was needed. Exact run settlement, source/claim references, context override and
prior immutable content checks passed.

## Independent semantic review: presentation hold

Publication and ordinary reading succeeded, but the independent semantic review
found a material wording error in proposal claim `out-pwd-2`, citing CL `am-5`.
The CL says password validation uses `flowEmbedOp` to nest `login/loginPwdCheck`.
The BL says “登录密码验证通过时” (after password validation succeeds), introducing
an unsupported success condition and misleading sequence. Its SourceSupported
qualification therefore overstates the cited evidence. Do not present this
document as a fully verified business explanation until that claim is corrected
through a separately generated, validated version; do not edit accepted JSON.

The other nine claims broadly preserve their cited conditions and effects. Minor
precision concerns remain around the translated recent-login-time field and the
customer-status node label “暂时不判断”; a node label is not separate proof of
runtime behavior. The exported wrapper heading was corrected to describe this
single authentication document rather than a login/registration collection. The
published content and raw ordinary-reader output were not modified.

No further paid run, content rewrite or expansion to 22/163 roots occurred. A
future correction should preserve the difference between an implementation
mechanism (“uses/through”) and a success predicate, then verify the new generated
claim against its exact evidence. This review was not generation input.

## Evidence

Ignored local evidence is retained under `TestResults/business-links-run-7/`
(authorization, preflight, launcher, terminal report and operation log) and
`TestResults/business-links-postrun-7/` (owner verification, ordinary-reader
verification, exact link/group audit, claim-reference pairs, compact semantic
review input and derived readable export). The export is for inspection with the
presentation hold above; it is not a separate publication route.
