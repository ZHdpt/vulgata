# Recovered 22-root Business qualification — 2026-10-12

## Authority and code

User requested safe recovery of the unsettled run and a fresh run for the same
22 roots, preserving existing publications. After independent review, the user
explicitly approved the abandonment/fencing contract and one disposable PostgreSQL
test resource. Implementation checkpoint:
`07ae057342cdd718c438b0a3f48d2fecf0472a1c`, assigned `oct14/business`.
The prior scaling fix `3158388344817306828aefb335ed7ccf30b5070c` is included.

Controlled qualification: affected SelfContained195/195, final changed cases2/2,
PostgreSQL locking/replay3/3; all zero failures/skips. Owned test container and volume
were removed and their absence checked. Independent production/helper review accepted.
No migration was introduced or applied to the retained database. No unrelated
process was stopped; the independently running Web host was left untouched.

Consumer prerequisite: another existing-CL generation host must include the recovery
checkpoint before operating on this recovered batch. An older Begin implementation
does not recognize abandonment as terminal and remains blocked; do not patch its
receipts to bypass that block. Ordinary publication reading retains its existing
contracts. No merge or push is performed by this task.

## Exact retained boundary

- Target: `phone-15bfe12615eb`.
- Batch: `1a327657-6a55-4e65-ba06-6992f423d40e`.
- CL publication: `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`.
- Corpus: `0ea50f38-2441-4ec0-b31c-313acd0c5f55`.
- Database: `vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d`.
- Original CL digest:
  `7d905781b3d6aa6a46bc83fa6a6d7e3969226fb0e46c22391130e994c4fa47f7`.

Preflight reconfirmed 163 CL versions, 77 coverage entries, the exact unchanged
22 selected roots/groups and 196 packet claims. Compact model packet sizes were
4184–25717 bytes. Original CL and both earlier BL projections remained unchanged.
No rescanning, replacement batch, manual accepted Knowledge or evaluation-answer
input was used. Zero accepted cross-repository relationships remain an explicit
limitation; local flow segments are not a complete joined login/registration graph.

## Confirmed recovery

Failed RunId `4a38a325-98bb-428c-beb3-32d0110ef0a4` received a distinct
`AbandonBusinessFromCl` receipt. Recovery returned Committed; subsequent read-only
exact lookup returned AlreadyCommitted with identical abandonment metadata. Its
original Begin remains. No normal Complete receipt was manufactured.

Recovery evidence digest:
`11477206B9A4346FCB4FB1A63117710CBC959F734EBD895E77EFD3DA7529523F`.
Proof combined hash-pinned original process/launcher/terminal evidence, observed
process exit, known usage for all 23 provider calls, the source-free/no-tool-child
execution path, live exact operator authority, and no surviving pre-exit database
backends or prepared transactions. The exclusive batch lock ordered settlement
against earlier semantic writes. New binaries enforce the durable terminal fence
before submission; the original exited binary cannot be reconstructed from its receipt.

Local private evidence is under `TestResults/business-recovery/`; credentials and
physical runtime paths remain outside tracked configuration. Existing consumed
markers were retained. Recovery itself made no provider calls.

## One fresh generation attempt

RunId: `e0d78673-2bc3-43eb-b0bf-0efac3a17749`.
Started: 2026-10-12 10:51:35 Asia/Shanghai. Owned process PID39064.
Canonical Begin committed at 10:51:39. The fresh local invocation marker is under
`TestResults/business-expanded-retry/`; no old marker was cleared.

Configuration: cl-and-bl with exact original CL publication; selected
`deepseek-flash` at the approved DeepSeek endpoint, Business low-reasoning settings,
393216 output ceiling, at most4 structural attempts per root,88 total requests,
7200-second overall deadline and1,000,000,000,000-token user ceiling. No outer
transport retry. Policy SHA-256:
`614C49F5404D59D9EBDF5FA9FA256A24416B84B91C24625E7E152F15722D2E84`;
root settings SHA-256:
`409B10226CB425783EBC462468E352B7DD37EFA5D9720EAF948D4F8FA6A3C8F5`.

## Confirmed execution and ordinary-reader result

**Generation/publication/settlement succeeded, exit 0.** Publication:
`e572a7d6-1730-4788-961d-3793a1728744`; finalization operation:
`bc74f187-85ca-43ee-845a-6e23776cc488`. Finalization committed at 11:10:48;
normal CompleteBusinessFromCl committed at 11:13:03 Asia/Shanghai. Owned process
exited normally in 1287.48 seconds, no watchdog expiry or unsettled cleanup.

Actual usage: **22 provider calls**, **129538 input +222051 output =351589 tokens**,
all usage known. The22 roots required no additional correction requests. Command
elapsed21:24.891, model wait18:25.544, store/read wait2:17.203. No further provider
execution occurred. The command's Complete coverage/matching labels describe this
selected operation; they do not establish cross-repository completeness.

Owner and authenticated ordinary-reader verification passed:

- 22 new BL versions,197 generated claim groups,185 exact publication members
 (original163 CL plus22 BL).
- Original CL digest and both earlier BL publication projections unchanged.
- Each group retains exactly the 163 original CL document dependencies, one exact
 root member,240 stored file uncertainties and zero invented relationships.
 The19 groups with one frontier and 3 with none retain their corresponding scope.
- Exact selected root identities, stored section-scoped input references,
 canonical source evidence and claim qualifications were checked. Finalization
 and the new run's Begin/Complete receipts match.
- Real password-authenticated **User-only** account opened Business selection,
 enumerated22 BL documents and read all 22 successfully, one page each,17985
 characters in total. Business-mode CL reading was denied. The owned temporary
 Identity account was deleted and its absence verified.

The first local postrun helper stopped before account creation because it incorrectly
treated CL claim IDs as globally unique. The corrected helper uses stored proposal
section-and-claim references and exact transformed evidence sets. Verification was
rerun on the same publication; no generation or published content changed. This
helper failure is separate from the successful generation command. A supplementary
read-only pass verified complete exact dependency sets and uncertainty counts.

This is real owner/ordinary-reader API evidence, not browser/Explorer/Chat qualification.

## Independent content review: useful output, semantic corrections remain

An independent review compared all 22 generated documents against their accepted CL
evidence; physical source was not reread. This is **not a clean semantic pass for all
197 generated claims**. Structural reference validation does not prove textual entailment.

| BL version / generated claim | Concrete finding |
| --- | --- |
| `634edeeb-2169-b8ea-5ff5-ded57b0bcc7f` / `sg_login_type_required` | Generic `*required` becomes a claim that login type is mandatory. The accepted CL does not identify that required field. |
| `09bc0d2e-aebc-7a8c-2917-1f5cda178bad` / `cg_failure_err_update` | Claims remaining attempts use the pre-update error count; the accepted expressions do not establish evaluation order. |
| `9617f49c-2513-0546-5997-a04e04de285e` / `bp-5` | Adds a success condition before invoking the nested password check, whereas the CL says checking is implemented through that nested flow. |
| `10340209-ac66-8fa4-1185-91d7a1e73ecf` / `c-entry-core-card`, `c-credit-card-query`, `c-registration-filter` | Declared `*track` configuration is promoted into enabled/executed debugging; supplied CL does not establish activation. |

Additional caution: some wording generalizes `*throw=false` into broad suppression
of failures, which the supplied CL does not establish. The guest-password document's
`cg_lock_result` limitation lists gap-kind ordinals 0–4 as if they were counts;
actual stored file uncertainties are 240 per group, not 1. The complete owner ledger
is preserved, but that generated explanation is inaccurate.

Stronger useful material includes self-registration SMS branches, transaction steps
and post-registration actions; gesture/IFAA error thresholds; device binding;
channel freezes; and version-update branches. The uncertain input remains uncertain.
No advisory claims were present in the paired inputs, so this run does not qualify
advisory handling. No verified cross-repository completeness was established.
Some titles/flow areas overlap and some prose retains implementation vocabulary or
uses vague status labels; ordinary readability is useful but uneven.

Preserve these immutable results. Do not silently edit accepted content, manufacture
success, or retry generation. A separately scoped correction would need to address
these semantic overstatements before presenting every document as verified business
rules. This task stops after the authorized successful run and recorded verification.

## Evidence

- `TestResults/business-expanded-retry/stdout.log`, SHA-256
  `8A259714C7A27EE3FED88C1F7D629DB6CDEFCB79175EBEB061CE7F97DF72E45A`.
- `TestResults/business-retry-postrun/verification.json`, SHA-256
  `2D737218DB2A1A668C640C8DD3DDDC2D55DA578931018579E309EED3014AD767`.
- `TestResults/business-retry-postrun/dependency-verification.json`, SHA-256
  `541C06B4A862F02211A22D3814BC4B9EE5494C8E5EA167033A99751F5A562A9F`.
- `TestResults/business-retry-postrun/business-logic-documents.md` is the derived
  ordinary-reader export, SHA-256
  `9D810448A419273734CA852DAE17CABF7DC9520C21095C223920166176E8EA41`.
  It preserves the generated wording, including the limitations above; it is not an
  editorially corrected version.
