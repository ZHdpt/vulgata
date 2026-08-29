# Story 12.5 Track B corrected quality-review input 02

## Reviewer boundary

This is a verdict-neutral T065 input. It does not issue or imply `ACCEPT`, `REVISE`, or `BLOCKED`; only the fresh independent T066 reviewer may write quality-review 04. Review correctness, security, edge cases, architecture seams, tests, regression risk, and repository standards. Do not start E2E, final Track B verification, Track C, Tier 2/3 qualification, or release-evidence execution from this record.

- Branch: `demo0831/story-12-5`
- Candidate commit: `ce9777bff62c10edb678a2eeea2e99bcc129f392`
- Candidate tree: `00ea0d4c6182db0b19406d817e90b971ffc170cc`
- Convergence checkpoint: `bmad-12-5-converge-20260829T120545Z-04`
- Correction base: `a0eda652f01cb7b410a270783128d1eaefbd0ec1`
- Story 12.4 dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995` is ancestral.
- Worktree and index: clean before this input write.

The complete correction delta is `a0eda652..ce9777bf`: 18 paths, 1,454 insertions, and 96 deletions. Review the complete delta, not only the final convergence commit.

## Accepted authority identities

| Input | SHA-256 |
|---|---|
| `spec.md` | `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4` |
| `plan.md` | `fad42dcd3a3f393809e95500ea6d6c7607ce4a16787d8b16d054b415ce533318` |
| `tasks.md` through T067 | `1c0fdf903c56b3686e6604086b71906f4c956ec0a384d06d75e174ee883294d1` |
| planned-change authorization 04 | `e28f64ef6dd4c217057e56e55b9ea22ca705a15ff61b4388cf2397da7aeaf976` |
| implementation approval 03 | `5a23d942a8c831243307fefe22a1de994b784c57e9ac5a11370ace3dfc9d4f06` |
| definition review 08 | `15e7896e78186d268e765e6777d0bcef24d9a7d0598b4824753e4297b3638af2` |

## Correction evidence

| Evidence | SHA-256 |
|---|---|
| quality review 03 (`REVISE`, QR-TB-005..008) | `9e7ce69afe1a7c7b8408fe50a5ff037a29139bb155bcf4ae1e0cb7540eb30f92` |
| T055-T063 checkpoint | `6c90ce9641dff4bdd3e2e9da8ec9c3572f3e3b498d7cc81afeb7361b2d9efdc8` |
| convergence pass 1 (`NOT_CONVERGED`, T067 appended) | `ba11e4e300cd19c6adbe65797f3a93a4a77ad9bc92961b88ead9a27dc2bfa5d5` |
| convergence pass 2 (`CONVERGED`) | `c05a30ed832ec5f5419265f7a58569449995e73cd584aa042b94222060617649` |

Correction commits:

1. `4efef026` recorded quality review 03 and the accepted Sol/high correction plan/tasks.
2. `363c7179` implemented and verified QR-TB-005 through QR-TB-008 (T055-T063).
3. `e505f267` aligned the stale provenance characterization identified by convergence pass 1 (T067).
4. `ce9777bf` bound the clean convergence-pass-2 report and state.

## Claims to challenge

### QR-TB-005 — accepted publication cannot be retroactively cancelled

After the page observes `AcceptedPublicationCommit`, cancellation or reveal-delay interruption may only complete the already accepted content immediately. It must not clear the Grounded Answer, claims, Document Citations, or committed follow-up Conversation State, and must not convert the turn to `Cancelled`.

### QR-TB-006 — non-throwing owner linearization

The publication owner samples cancellation/deadline and one monotonic timestamp, uses primitive non-throwing deadline arithmetic, and performs one terminal exchange. The owner-current committed composite atomically contains answer, proposed Conversation State, completion summary, and reveal directive. The coordinator has no post-commit shadow activation.

### QR-TB-007 — prebound reveal authority

The immutable candidate binds Unicode text-element schedule cost at 25 ms per delayed element and a fixed 1,000 ms safety margin before the owner sample. Progressive reveal is accepted only when schedule plus margin strictly fits; the page consumes the committed directive and uses live supervision only to finish already accepted content on lag.

### QR-TB-008 — typed scoped composition

`PreparedChatCoordinator` requires exactly one non-null `IChatStreamCompletionSummaryRecorder` and one non-null `IPreparedChatPublicationOwner`. There is no `object?`, nullable/default parameter, null recorder, or direct owner fallback. Production composition supplies the exact same scoped instances; completion data is exposed through the accepted composite.

### Preserved Track B boundary

The correction must not weaken deterministic current-v2 lexical/relationship retrieval, `REFERENCES`, `GENERATED_FROM`, exact decisions, complete Published Flow Groups, authorization/currentness/cardinality, packet 2.2, bounded transient Conversation State, whole-answer validation, strict raw streaming, accepted-only client reveal, Chinese Business/IT behavior, or the under-30-second target. It must add no EF migration, Story 12.4 contract change, vector/autonomous retrieval, fake Backup B evidence, provider-fragment leak, or Track C release sink.

## Verification evidence

- Historical REDs: T055 2/2 failed; T057 64 passed/1 failed; T059 53 passed/5 failed; T061 79 passed/2 failed.
- T063 affected suites: 127/127 passed after excluding exactly two default Data Protection key-path characterizations denied by the managed sandbox.
- T067 provenance suite: 5/5 passed.
- Fresh bounded Track B/B-INT matrix: 428/428 passed, 0 skipped, with the same two environment-only default-key-path tests excluded; controlled key-ring vectors passed.
- Web build: 0 errors, with 19 existing NU1507/NU1900/MSB3277 warnings.
- `git diff --check`: clean.
- No `Program.cs`, EF model/migration/snapshot, Story 12.4 contract, provider transport, retrieval contract, or Track C production path differs in the correction delta unless explicitly listed by `git diff a0eda652..ce9777bf`.

Current corrected file identities:

| Path | SHA-256 |
|---|---|
| `ChatPage.razor.cs` | `abcfbf8e49f9559c0346bef7e67f91ca09a0817f2562a91bf1005b5a76816b95` |
| `PreparedChatCoordinator.cs` | `e913e6878a6aa741e9e4ecc965ee9f4b0367e5601e41b75dcdc583b31e0158e6` |
| `PreparedChatPublicationOwner.cs` | `1da2f31e87f6aa496a2fffc0b7e49753b17f681f1dc792375b79588895aa6974` |
| `ChatStreamObservationProvenanceTests.cs` | `9e73a2ab43b1005486260bc34a5ec8839fc07ca28531795ffa5e805a86fb1f16` |

## Independent-review questions

1. Can any cancellation, deadline, scheduler-lag, disposal, or repeated-commit race publish or later erase a partially/fully accepted answer or Conversation State?
2. Does the owner perform any fallible operation after its authoritative sample, and can its current composite become visible before the accepted terminal state or remain visible after reset?
3. Is reveal cost calculated from the same Unicode text-element semantics the page renders, with equality/within-margin cases always immediate and strictly fitting schedules bounded?
4. Can optional/default/null/object construction or an alternate scoped instance regain completion/publication authority?
5. Do the new tests exercise public behavior and composition strongly enough to fail if owner, recorder, reveal directive, cancellation handling, or Conversation State activation is bypassed?
6. Does the complete correction preserve every Track B retrieval, validation, streaming, authorization, currentness, no-migration, Story 12.4, and Track C separation invariant?

The reviewer must bind its raw verdict to candidate `ce9777bff62c10edb678a2eeea2e99bcc129f392`, the repository-observed authority hashes, the complete `a0eda652..ce9777bf` delta, and this evidence input. Any hash typo in this input must be reported as an evidence defect, not silently corrected or misclassified as a production defect.
