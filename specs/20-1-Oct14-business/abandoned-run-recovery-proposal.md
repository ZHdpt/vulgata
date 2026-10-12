# Bounded recovery of the failed 22-root Business run

Status: independently reviewed and explicitly approved for implementation and
one owned disposable PostgreSQL qualification resource. Runtime execution evidence
will be recorded separately; approval is not a claim of successful recovery.

## Verified boundary (2026-10-12)

Assigned `oct14/business` is at `3158388344817306828aefb335ed7ccf30b5070c`.
The publication scaling correction and its accepted 111-test review remain the
implementation baseline. User now requests safe recovery and one fresh 22-root run.

The exact failed RunId is `4a38a325-98bb-428c-beb3-32d0110ef0a4`, batch
`1a327657-6a55-4e65-ba06-6992f423d40e`, target `phone-15bfe12615eb`, original CL
publication `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`. The retained database is
`vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d`.

Read-only owner inspection reconfirmed unchanged original CL content and both prior
BL projections, three publications, two BL versions, and an unmatched Begin for
the failed run. No new finalization belongs to that run. Its original launcher
observed process exit; current PID absence corroborates that evidence but cannot
alone establish settlement. Its recorded 23 provider calls all have known usage.

Current database observation found four connections, including one active during
the second observation, and no prepared transactions. Host socket inspection found
four PostgreSQL connections owned by an independently running Vulgata.Web process,
PID 32716, started 10:27:07 Asia/Shanghai. Docker translation prevents asserting a
one-to-one port mapping from this observation alone. No process was stopped and no
session was terminated. Recovery must exclude old or competing continuation writers and settle old-run-owned
work. Independently proven unrelated read-only Web activity may remain. Connection
count alone is not a recovery conflict; a maintenance window is the fallback if
writer exclusion cannot be established. Do not treat this observation as a standing fence.

## Exact proposed contract change

Normal `CompleteAsync` remains original-process-only. Do not reconstruct its
admission or fabricate normal completion. Add a distinct append-only
`AbandonBusinessFromCl` operation in the existing KnowledgeOperations journal,
bound to the exact database/target/batch/publication, old RunId, Begin digest,
operator recovery authorization and evidence digest. It records administrative
abandonment, not successful generation or a claim that no prior effects exist.
Existing integer operation storage has no enum constraint; no table migration is
anticipated, subject to implementation verification.

The recovery owner must verify permanent original process/descendant exclusion,
provider settlement and database settlement. Logs, caller booleans, PID absence,
absence of publication rows and deadline expiry are individually insufficient.
If that combined proof cannot be established, stop without mutation or generation.

Recovery takes the existing exclusive batch transaction lock and reconciles exact
committed outcomes before appending its receipt. It preserves the original Begin,
all semantic receipts/publications and all consumed invocation markers. Exact
replay is idempotent; changed identity/evidence conflicts; indeterminate recovery
permits lookup only.

Every continuation semantic submission, including resolution and finalization,
must check the run-bound durable abandonment fence while the existing batch lock
is held. Resolution's random operation IDs are not a substitute for RunId binding.
New Begin may recognize validated abandonment as terminal but may never reissue
the old RunId. Old binaries do not observe the new fence, making original-process
exclusion mandatory. No automatic restart, schema replacement or general recovery
framework is proposed.

## Owned change and verification boundary

- Core Business continuation contracts/policy and OperationKind.
- Infrastructure continuation store and shared committer lock selection.
- Transferred Business-only continuation semantic commit guard in Generator.
- Business-owned focused tests and existing feature evidence.

No Scan source pipeline, CLI selection, Chat, Explorer, Web composition, migrations
or resolution policy change. The recovery lifecycle and shared-contract semantics
change; older binaries must not execute continuation work against this batch during
maintenance or bypass the abandoned-run terminal marker afterward.

Required tests: wrong identity/authorization/evidence rejected; active or unproven
owner rejected; exact recovery replay; conflicting replay; recovery/write ordering;
late old-run resolution/finalization fenced; old RunId never reissued; new RunId
admitted only after validated terminal recovery; unchanged CL/BL; no partial receipt
on failure. Use the repository runner and an independent focused review. PostgreSQL
locking qualification needs an explicitly owned disposable test resource; the
retained target is not a fixture database.

## Conditional one-run execution

After reviewed recovery and exact durable lookup, use a fresh RunId and fresh
invocation marker, the same stored 22 roots, original CL publication, cl-and-bl,
DeepSeek profile/model, 393216 output cap, bounded structural corrections, 7200s
overall deadline, 88-request maximum and the already authorized 1,000,000,000,000-token ceiling. Recheck all protected
bindings, calculated reservations and live authorization first. Do not Prepare,
rescan, replace the CL batch, retry an indeterminate operation or feed qualification
answers into generation.

Verify the new publication's exact lineage, distinct useful flows, explicit gaps,
source-supported claims and ordinary authenticated Business reading. Recheck the
original CL digest and both earlier BL projections. Record actual usage/settlement;
stop after this one execution, including on failure.

## Management decision needed

The safe method requires the explicit abandonment/fencing contract above, rather
than ordinary cleanup through existing APIs. Confirm this bounded lifecycle scope,
and an owned disposable PostgreSQL test grant. Continue read-only writer attribution;
request a maintenance window only if exclusion cannot otherwise be established. The new
provider run itself is already authorized; do not ask for that authorization again.

Decision received on 2026-10-12: **“Approve recovery change and disposable test
resources.”** The question explicitly covered the append-only abandonment/fence,
one newly owned PostgreSQL 17 test instance cleaned afterward, exact retained-run
recovery and the already authorized 22-root rerun. No unrelated process termination,
schema migration, additional paid retry, merge or push is included.

Independent read-only review accepted this bounded proposal with the exact-budget
record and writer-exclusion clarifications above. Implementation and locking
qualification remain outstanding; no retained mutation or provider call occurred.

## Implemented checkpoint

The approved boundary is implemented: optional canonical abandonment metadata,
distinct operation kind, exact maintenance recovery/lookup, validated terminal
admission, and repeated run-bound semantic fencing under the existing transaction
lock. Normal completion remains original-owner-only and refuses abandonment.
No table migration or automatic host command was added. The trusted maintenance
authority must prove settlement; the API does not infer it from row absence.

Focused verification passed with zero failures/skips:

- 195/195 affected SelfContained cases, `isolation-00c26b09fdda42d09bcb0548b203686e`.
- 2/2 final changed regression cases, `isolation-a37b711fcaf34e66b3120e11747c6977`.
- 3/3 PostgreSQL locking/concurrent replay cases, `isolation-34e52d82140748c09fac679fcda23926`.
  Runner exit zero; owned test container and volume absence independently verified.

Independent review accepted production and the exact-run maintenance helper after
fixing malformed replay and cross-batch metadata checks. The helper binds hash-pinned
original launcher/process/usage evidence, live operator authority, original process
exclusion and absence of old database backends/prepared transactions. Current unrelated
Web readers are not terminated. This checkpoint does not yet claim retained recovery
or new generation; those require actual exact result evidence.
