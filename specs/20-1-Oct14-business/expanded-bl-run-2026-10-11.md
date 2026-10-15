# Expanded login/registration BL qualification

## Authority and exact boundary

The management user instructed: “Follow your recommendation. You can set a larger
budget limit in case scan blocks for it. 2 hour for whole scan and trillion token
consumption. Ignore them it if there is no such budget setting”. This authorizes
the proposed expansion over meaningful retained login/registration roots, with
canonical publication and ordinary-reader qualification. It does not authorize
cross-repository resolution redesign, source rescanning, migrations or arbitrary
selection of every CL unit as a BL root.

Assigned `f081` / `oct14/business` was clean at
`3f6f51e2dfcdbedc2eadc2b0cbcd9d63997bee7d`. Uses the reviewed implementation and
catalog fix; no production changes in this qualification.

- Target `phone-15bfe12615eb`, batch `1a327657-6a55-4e65-ba06-6992f423d40e`.
- Existing CL publication `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`.
- Corpus `0ea50f38-2441-4ec0-b31c-313acd0c5f55`, 163 CL versions / 77 accepted files.
- Retained DB `vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d`.
- Selected protected provider `deepseek-flash`, configured low reasoning.
- Run deadline **7200 seconds**, watchdog **7260 seconds** for owned cleanup only;
  total token ceiling **1000000000000**, maximum **88 physical provider requests**,
  per-root output ceiling 393216 and existing 900-second synthesis deadline.
- One invocation, 22 roots, existing staged structural corrections only. No
  outer transport retries, indeterminate retries or unmatched-receipt clearing.

Both requested budget settings exist and pass the production policy validator.
The trillion-token value is an upper ceiling, not an intended spend: 22 roots and
at most four attempts per root further bound work. Existing per-message, owner-record,
store, authorization and reader limits remain unchanged.

## Root selection

Select coherent processes/segments with accepted conditions, transitions or outcomes.
Do not merge disconnected CL units, use advisory context to establish edges, or turn
structural XML descriptions, mappings and helper nodes into independent business
processes. These are distinct local explanations, not a joined end-to-end graph.

Account repository `9172b209-ed3a-415c-8d8b-c0ef1b2f18f6`:

| Relative flow path under `src/main/resources/flow/` | Exact selected selectors | Business scope |
|---|---|---|
| `cust/userLogin.flw` | `versionGate`, `loginTypeGate`, `authMethods`, `channelGate`, `ecifFlow`, `deviceProtect`, `finalize` | Version gate, login routing, authentication methods, channel/customer checks, device protection and login completion |
| `login/guestLogin.flw` | `pwdLogin`, `noPwd`, `deviceBind`, `authUpdate`, `status` | Guest password/passwordless branches, binding, stronger authentication and status outcomes |
| `login/mbLoginRiskQry.flw` | `requestSwitch`, `strategy` | Risk-request switch and verification strategy decisions |
| `login/checkHandPwd.flw` | `checkHandPwdFlow` | Gesture authentication checks |
| `login/checkLoginIfaa.flw` | `checkLoginIfaaFlow` | IFAA authentication checks |
| `login/loginPwdCheck.flw` | `loginPwdCheckFlow` | Login password checks |
| `register/selfRegist.flw` | `selfRegistFlow` | Registration branches and writes |
| `register/userAuth.flw` | `userAuthFlow` | Registration identity verification |
| `register/phoneNumbAuth.flw` | `phoneNumbAuthFlow` | Mobile-number validation sequence |
| `register/userAcctQuery.flw` | `userAcctQueryFlow` | Customer account/card query sequence |

Channel repository `2e44021b-a04d-488c-a8e8-36c487057728`:
`login/handPwdLogin.flw` / `handPwdLoginFlow`: gesture-login orchestration with
success/failure routing and session/anti-fraud declarations. This is the single
selected channel-side entry; no relationship to an account-side target is asserted.

Not independently selected: metadata-only whole-file roots (`diagram`, `file` and
structural `flow` units); duplicate thin forwarding wrappers; standalone query,
notification and persistence helpers; isolated mapping/node definitions. Those
remain exact corpus dependencies and may require later work if their detailed
behavior is needed. Selecting a segment does not establish the full containing
file's behavior. Other unselected flow segments remain outside this run.

## Read-only preflight

Live owner authentication and canonical root selection succeeded. All four prior
Business Begin/Complete receipts were present, with no unmatched prior run.
The production settings selector resolved exactly the intended 22 roots; planner
produced 22 groups, each with its supplied root member. There remain zero resolution
inquiries and 355 NotEvaluated dispositions. No cross-repository links are claimed.

- 196 accepted claims across the 22 packets.
- Model packets: 4184–25717 bytes; total 242004 bytes.
- Complete owner input: 2424624 bytes; complete gaps preserved.
- Existing canonical reader measurement: original CL publication 3563292 serialized
  bytes; representative BL document 1102552 bytes. Estimated 22-root publication
  approximately 28 MB, below the existing 32 MiB materialization bound; actual
  resulting publication must still pass the real reader check.
- Policy SHA-256 `614C49F5404D59D9EBDF5FA9FA256A24416B84B91C24625E7E152F15722D2E84`.
- Root settings SHA-256 `409B10226CB425783EBC462468E352B7DD37EFA5D9720EAF948D4F8FA6A3C8F5`.

Ignored local artifacts: `TestResults/business-expanded-readiness/`,
`TestResults/business-expanded-run/`, `TestResults/business-expanded-postrun/`.
Runtime paths/credentials are not tracked. Original launch markers are preserved;
this run has a fresh exclusive marker.

## Execution evidence

Started 2026-10-11 21:09:15 Asia/Shanghai, RunId
`4a38a325-98bb-428c-beb3-32d0110ef0a4`. Process ended after 831.66 seconds
(about 21:23:07), without watchdog timeout. Command **exit 4**, execution and
publication **Indeterminate**, diagnostic boundary ExactLookup. Finalizer and
exact lookup both reported CommitIndeterminate. Gaps:
`business_publication_unavailable`, `business_attempt_unsettled`.

Actual usage: **23 provider calls**, **135291 input + 199611 output = 334902 tokens**,
all usage known. Model wait 12:46.306; total command time 13:49.365. The two-hour,
trillion-token and 88-request limits were not exhausted. Neither another generation
nor a receipt-clearing operation was attempted.

## Retained state and diagnosis

Read-only owner inspection at 21:24:59 confirmed:

- This run has a committed BeginBusinessFromCl receipt and no CompleteBusinessFromCl.
- No new Business finalization, publication or BL version exists. Only the two
  previously qualified BL versions and three prior publications remain.
- Original CL digest and both prior BL document projections remain unchanged.
- No ordinary-reader test account was created for this failed run, because there
  was no new publication to qualify.

The run's terminal report remains indeterminate; observing absent rows afterward
does not grant permission to manufacture completion authority or bypass the receipt.
Admission explicitly refuses any batch with an unmatched BeginBusinessFromCl.
Normal completion requires the original in-process admission capability plus its
settlement authority. The process has ended; no cross-process recovery is performed.

Source inspection found independent hard-coded ten-second publication bounds in
`OctoberBusinessFinalizer` and `OctoberKnowledgePublicationCommitter`. They are not
the configurable run deadline. `OctoberFlowPublicationStore.PrepareAsync` calls
synthesis validation separately for every completed root; validation reconstructs
the full multi-root plan each time.

A **controlled pure-validation timing probe** used the actual retained input but
minimal one-claim proposals solely in memory; it made no provider calls, publication
calls or writes, and supplied nothing to generation. All 22 validations succeeded,
but together took **18286 ms**, already exceeding the publication deadline before
database work. A one-root comparison took **258 ms**. This reproduces a concrete
multi-root scaling blocker consistent with the real failure; the real swallowed
exception is not available, so this is not a claim of a captured exception trace.
The probe is not accepted BL and does not prove all real responses were valid.

Next work: eliminate repeated whole-plan validation while preserving independent
store checks, qualify bounded multi-root persistence and timeout classification,
and agree a narrow canonical recovery for the stranded original admission before
any further run. Increasing token allowance again does not address this blocker.
No schema, shared contract, timeout bypass or recovery framework was introduced.
Do not rerun this batch or clear/forge its receipt under this execution grant.

Evidence:

- `TestResults/business-expanded-run/stdout.log`, SHA-256
  `9EDB2C29C402C690081F68E7C290E2DB8A022CB85F3C4A45176653EB51F01759`.
- `TestResults/business-expanded-postrun/indeterminate-inspection.json`, SHA-256
  `E0A5B935118F9672DC4EAA1E5088979FC99352B3507896E30C7F4F9AC07DA7A2`.
- `TestResults/business-expanded-readiness/validation-profile.json`, SHA-256
  `60439BD8EB982B1936BF3A3D2B4299144464B13EA9758D2D7F2C74A8191387D5`.

**Expanded qualification did not publish.** Earlier one-flow success remains valid;
multi-root readiness is blocked at publication scaling and admission recovery.

## User-authorized publication scaling correction

User instruction **“DO the fix”** authorizes the focused correction. Starting
checkpoint `caad8078` was clean. No provider run, retained write, receipt recovery,
schema change or timeout increase was performed.

`OctoberBusinessSynthesisPolicy.ValidateAll` now builds one owner plan for a bounded
batch of existing `FlowSynthesisSubmission` values. It validates complete input once,
matches exact root document/unit/kind/selector/group for each entry, rejects duplicate
batches, and preserves result order. Each proposal still undergoes the same packet,
citation, qualification, complete dependency and gap checks. Single-proposal validation
also stops recomputing the plan a second time merely to rebuild its packet.

`OctoberFlowPublicationStore` reloads owner input and uses this batch validation
before atomically publishing. It does not trust the generator's prior validation or
a caller-supplied plan. Existing complete-dependency comparisons, publication/replay
identities, authorization, cancellation, deadlines and schema remain intact.

Verification:

- **111/111 SelfContained tests passed**, zero failures/skips, through `eng/test.ps1`.
  `TestResults/isolation-32c7f6fcb3304325afc9f899a5faee1e/results.trx`.
- New equivalence tests compare batch and single results including full lineage/gaps;
  exact root/group/qualification checks and batch bounds are exercised.
- Controlled SQLite store tests publish 22 BL documents with all 484 dependencies
  in the 22-document fixture, verify exact replay and unchanged CL, and prove that an
  invalid final proposal leaves no partial publication, BL, flow or operation records.
- Read-only controlled profiling on the same actual retained 22-root input: batch
  validation **1222.8622 ms**, compared with the original **18286.1661 ms**. All 22
  minimal in-memory proposals validated. They were not sent to a model or persisted.
- Focused independent review accepted, no outstanding findings. Synchronous validation
  still relies on the committer's cancellation check before writes/submission; no
  claim is made that arbitrary-size batches fit the fixed deadline.
- Profiling evidence: `TestResults/business-expanded-readiness/validation-profile-after.json`,
  SHA-256 `48C28022866B49E9626F1FD4B242F4ADA0434E6E7D577FA83904EB8028EE9D4F`.

This qualifies the CPU correction and controlled atomic behavior, **not a new full
PostgreSQL publication**. No PostgreSQL write qualification or generation retry was
run. The stranded Begin receipt remains unchanged. Existing admission explicitly
forbids recreating its process-local authority from persisted receipts, and completion
does not accept a report/boolean as proof. Recovery therefore needs an explicit
canonical owner decision before a new protocol or retained-data action; it is not
silently included as a timeout workaround. No migration or deletion is proposed.
