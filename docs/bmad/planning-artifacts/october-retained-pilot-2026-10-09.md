# Retained phone-verification pilot

Status: source verified and pilot selected. User amended runtime authority to reuse
the existing PostgreSQL container/volume with a new database. No real-source
Generate or retained host has started at this checkpoint.

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
  total/per-file requests and continuation windows; 1,000,000,000,000 tokens), not
  a claim of literal infinite limits. With concurrency 1, a 30-minute deadline,
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
