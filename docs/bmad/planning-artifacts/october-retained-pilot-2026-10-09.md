# Retained phone-verification pilot

Status: diagnostic retry proves Worker `ContentAfterFinish`; Scan owner is tracing
the SDK event sequence before any compatibility fix. No real-source content
qualification is claimed.
An initial Generate
launch was rejected by execution auto-review before starting because supporting
reads could disclose additional slice files. The user subsequently explicitly
confirmed relevant content from ALL 77 prepared files may be sent to the approved
DeepSeek provider, while only nine selected files generate Documents. Evaluation
answers remain excluded. This resolves that specific scope hold; reverify source
and runtime bindings before launching. No provider request occurred in the rejected
invocation.

## Scope

The supplied two-repository slice contains 77 manifest-verified files / 411,180
bytes, with no extra files. Use only its two prepared roots for source access.
The handoff is advisory context; separate evaluation answers must not enter Worker
or Business inputs. Do not assume deployed RPC wiring or external gateway behavior.

The first selected generation scope is nine files / 10,516 bytes:

| Repository | Relative path |
| --- | --- |
| mbs_account | src/main/java/cn/com/yitong/actions/acct/GetMobileByAccessCodeOp.java |
| mbs_account | src/main/java/cn/com/yitong/actions/acct/GetMobileToReplaceResOp.java |
| mbs_account | src/main/java/cn/com/yitong/actions/register/RegisterCheckPhoneNumOp.java |
| mbs_account | src/main/resources/disgram/in/register/phoneNumbAuth.xml |
| mbs_account | src/main/resources/flow/acct/getMobileByAccessCode.flw |
| mbs_account | src/main/resources/flow/register/phoneNumbAuth.flw |
| mbs_channel | src/main/resources/disgram/in/register/phoneNumbAuth.xml |
| mbs_channel | src/main/resources/disgram/out/register/phoneNumbAuth.xml |
| mbs_channel | src/main/resources/flow/register/phoneNumbAuth.flw |

Other slice files may supply read-only support; mark them as supporting/unprocessed,
not generated or covered. Full-slice completion is not claimed. Choose actual
offered channel/account entrypoint roots from accepted CL, not invented IDs.

## Proposed bounded runtime

- Reuse the existing running PostgreSQL 17 container and volume; do not create,
  restart or delete a container/volume. Only create a new uniquely named owned pilot
  database. No mutation of existing databases. Read-only verification found PostgreSQL
  17.10 and the configured role has database-creation authority.
- Existing Web Identity/domain migrations and Generator October migrations only;
  no schema changes. Dedicated bootstrap Administrator and ordinary User accounts.
- Temporary Web on an unused loopback port for production registration/setup/login
  and exact reader verification. Stop host after qualification; successful data and
  required keys remain retained. No externally accessible listener.
- Existing approved provider/model and protected credential reference. The user
  removed the proposed cumulative request/token budgets. The implementation still
  requires positive finite fields: use nonbinding configuration values (1,000,000
  total requests; 1,000,000,000,000 tokens), not a claim of literal infinite limits.
  Worker per-file rounds and continuation windows must remain at the supported
  technical maximum of 100: setting them to 1,000,000 passes policy validation but
  fails Worker admission. With concurrency 1, a 30-minute deadline,
  existing rate control and per-call context/output bounds, these must not be the
  practical stopping condition. No change to semantic validation or automatic
  transient retry policy. Business existing two-attempt policy remains unchanged.
  Run deadline 1,800 seconds, file deadline 300 seconds, operator selection 120
  seconds, drain 30. Stop on provider credit exhaustion rather than buying credit.
- Pinned CodeGraph/offline JDT profile with owned tool state only. Do not install
  tools, change persistent PATH or modify business source. Recheck source hashes.
- Runtime configuration, credentials, keys, source paths, process IDs and physical
  bindings stay in protected local files outside Git; tracked material uses logical
  names only. Never print secrets. Retain sanitized timing/usage/status evidence.
- Use `eng/watch-october-batch.ps1` with explicit 1,800-second timeout. Console
  progress is elapsed/deadline, not an invented work-completion percentage. At the
  wall-clock limit terminate only the launched Generator tree and check settlement.
  A hard kill does not prove rollback; preserve ambiguous state and remaining-time
  assessment before any retry or cleanup. The user's ten-minute constraint was
  monitor implementation effort, not a ten-minute batch deadline.

## Production bootstrap prerequisites

Repository registration invokes git ls-remote. The original local repositories
were verified readable without network or writes; use those only as registration
locators if confirmed, never as source-tool roots. The prepared roots remain exact
scan inputs. Do not replace Git validation with a fixture stub or fabricated URL.

The Generator reads child-process environment, not CLI configuration arguments.
An untracked local run configuration can supply those variables. Use prepare,
select and generate with their existing command contracts and production authority.
There is no existing non-test protected-credential export utility; a small local
bootstrap adapter using the existing Data Protection/secret service is needed.
Do not reuse GeneratorPrepareFixture or disable its cleanup. Use System/Repository
advisory context through production APIs; no need to select global context for this
pilot. Preserve exact reader identity and publication binding.

## Retention and evidence

Success: retain database, source bindings and required keys until explicit deletion;
every later run gets a fresh database. Failed incomplete owned runs may be removed
after settled outcome and sanitized evidence; indeterminate state is preserved.
Measure setup/tool/Worker/Business/publication/reader times separately where available.
Claim cross-repository resolution only if actual source-backed accepted results
demonstrate it. Compare generated content with evaluation answers after generation.

## Bootstrap checkpoint

Existing container `vulgata-postgres` was reused unchanged. Created only database
`vulgata_oct_pilot_mv0r9q4b_e19qg1`. The dedicated temporary loopback Web host applied
existing migrations; production registration and System/Repository APIs succeeded.
Keys and encrypted provider/operator files are in the protected local run directory
`phone-mv0r8ity`, outside Git. Generator prepare/select passed without a provider call.
The temporary Web process was stopped after the blocked launch; keep the database,
keys and configuration for continuation. Do not delete the shared container/volume.

The generic credential packager uses stdin-only secrets and production Data
Protection purposes; its controlled synthetic roundtrip/overwrite refusal passed
review. The watchdog's completion/output/environment/timeout controls passed; timeout
remains an indeterminate-settlement signal, never proof of database rollback.

## Runtime recovery observations

- Missing owned Generator log directory originally caused exit 1 before admission;
  create it explicitly during bootstrap.
- Original batch admitted Generate but rejected invalid per-file limits before any
  provider request. One operation, zero accepted files/publications were verified.
- October setup has one selection slot per database. A different batch in an already
  selected database conflicts and is surfaced as `Denied`; do not reset that record.
- Replacement `phone-527d7e0ffe4f` prepared successfully, then ended in 56.26 seconds
  with zero requests, empty inventory and `source-authority-or-identity-lost` gaps.
  The earlier attempt's two `.codegraph` directories were still present. The adapter
  rejects pre-existing state but disposal releases handles without deleting state.
  Their creation times match the original run; all 77 source hashes still matched.
  Archived only those generated directories under the failed run's protected local
  directory, not in the source trees. This needs a repeat-run operational fix; no
  source-access check was weakened.
- Current fresh run `phone-9350f644c594`, batch
  `94363a52-69d2-465f-be62-82a2a6a5b9bf`, uses database
  `vulgata_oct_pilot_97cbb54eb7e34cf493f020c06b8dba95` in the same existing container.
  Production bootstrap and prepare passed. Generate ended after 18.70 seconds
  (reported pipeline 16.46 seconds), exit 1, no timeout or unconfirmed cleanup.
  Inventory contained all 77 files; first Worker failed `InvalidOutput`,
  `StreamTerminal`. One request used 2,438 input / 1,087 output tokens, usage known.
  Read-only settlement: one operation, zero accepted-file manifests, zero publications.
  No successful retained dataset exists; failed databases remain isolated for diagnosis.
  Raw provider content was not captured, so this broad failure stage does not prove
  which stream condition failed. Scan owns bounded diagnosis/safe diagnostics and
  controlled tests; root owns any later real retry. No unchanged retry is appropriate.

## Diagnostic checkpoint and retry

Integrated reviewed Scan delta `5aa759a9` as `0bb15df6`: one finite nullable
`workerDiagnostics.terminalFailure`, no acceptance/retry/validation changes or raw
payload logging. Main affected SelfContained verification passed **173/173**, no
skips, isolation `d16a79f3687b4792a9a716d1b5d47d1c`. This is not full-suite GREEN.
Initial restricted build had no compiler diagnostics; subsequent host build exposed
`pwsh` resolution failure under the inherited over-8,200-character PATH. A short
process-local PATH resolved it; no persistent configuration changed.

Fresh diagnostic run `phone-43e354514b75`, batch
`ea0798f0-1ad6-476e-8751-9a2e2c241e2c`, database
`vulgata_oct_pilot_e57f71e490ef4c41bc577570dee86168` in the same existing container:

- Wall **21.02 seconds**, pipeline 18.74 seconds; exit 1, no timeout or cleanup doubt.
- Inventory **77**, accepted files **0**, Documents **0**, publications **0**.
- **1 request**, 2,438 input / 1,613 output tokens; usage known.
- `InvalidOutput / StreamTerminal / ContentAfterFinish`, retry disposition `None`.
- Read-only settlement: one operation, zero accepted manifests/publications.
- Previous attempt's owned indexes archived; all 77 source hashes matched before retry.

This identifies the exact rejected branch, not the nature of the trailing content.
An SDK-assembled tool-call event is a hypothesis to inspect, not an established
cause. Scan owns SDK-sequence diagnosis/controlled correction; preserve rejection
of genuinely invalid trailing content. No further unchanged paid retry.
