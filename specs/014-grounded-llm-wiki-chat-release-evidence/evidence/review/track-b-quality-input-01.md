# Story 12.5 Track B quality-review input 01

## Purpose and reviewer boundary

This is a verdict-neutral input for T039. It does not issue or imply `ACCEPT`,
`REVISE`, or `BLOCKED`; only the fresh independent quality reviewer may write the
quality-review verdict. Review the clean converged candidate for correctness,
security, edge cases, tests, architecture seams, regression risk, and repository
standards. Do not start conditional E2E, Track C, Tier 2/3 qualification, or
release-evidence execution from this record.

- Branch: `demo0831/story-12-5`
- Candidate commit: `a0eda652f01cb7b410a270783128d1eaefbd0ec1`
- Candidate tree: `f58256f79fac4f919448281a7b155bb31668d6f1`
- Candidate checkpoint: `bmad-12-5-converge-20260829T101849Z-02`
- Accepted corrected-definition base: `5adc504a912528b8b7ef93a145517ed78eb99b58`
- Story 12.4 accepted dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Observed worktree/index state: clean; canonical empty-status SHA-256
  `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`
- Candidate ancestry check: Story 12.4 commit `4798b97d...` is ancestral; its
  completion trailer is
  `Workflow-Checkpoint: bmad-12-4-complete-20260827T132524Z-d8e2d1`.

The implementation delta to review is `5adc504a..a0eda652`, not only the final
checkpoint commit. It contains 27 paths, 2,216 insertions and 210 deletions.
The converged chain is:

1. `ba45c590` verified the corrected implementation boundary.
2. `ec15c8a2`, `e5d50047`, `f4ff035c`, and `29cf72d4` reaccepted Track A,
   B1, B2, and B3.
3. `65ffe698` added the B4 publication owner and B-INT composition/tests.
4. `9db9b344` bound Track B integration evidence.
5. `4e617288` corrected the canonical exact-decision witness exposed by
   convergence finding F1.
6. `a0eda652` changed only the convergence report, task marker, and compact
   workflow state to bind the clean second convergence pass.

## Accepted definition and authority identities

| Input | SHA-256 |
|---|---|
| `spec.md` | `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4` |
| `plan.md` | `fad42dcd3a3f393809e95500ea6d6c7607ce4a16787d8b16d054b415ce533318` |
| `tasks.md` | `f44af9292e219b744a0706b7c287990ea8bdcb245c2d48146fb0924d1e053e17` |
| planned-change authorization 04 | `e28f64ef6dd4c217057e56e55b9ea22ca705a15ff61b4388cf2397da7aeaf976` |
| implementation approval 03 | `5a23d942a8c831243307fefe22a1de994b784c57e9ac5a11370ace3dfc9d4f06` |
| definition review 08 | `15e7896e78186d268e765e6777d0bcef24d9a7d0598b4824753e4297b3638af2` |

The specification's literal `Status: Draft` header is known stale metadata and
is explicitly dispositioned by the accepted plan; it is not evidence that the
definition gate is unaccepted. The reviewer should nevertheless report any
actual spec/plan/task contradiction rather than relying on this disposition.

## Pinned implementation evidence

The ordered evidence-set digest below is SHA-256 over UTF-8 lines
`path=lowercase-file-sha256`, in the displayed order, including the final LF:
`3eaf3da8a4e9601149d8022716b638f9fa70e3294143c2fcb9d1313cc92eba0e`.

| Evidence | SHA-256 |
|---|---|
| `evidence/checkpoints/track-b-b1-reacceptance-01.md` | `475ad6061a677705e9825e6b9379f08cc8c6c0aec75a99173829b4324e60f139` |
| `evidence/checkpoints/track-b-b2-reacceptance-01.md` | `4056a0f6a4f55bf7630e05df24510357613d47dd4b0478dddab4432d84754070` |
| `evidence/checkpoints/track-b-b3-reacceptance-01.md` | `9284a2c28fd5738ecb836fdfc3bccf9684e351fed11315fda53928bc0c62aa2c` |
| `evidence/checkpoints/track-b-b4-implementation-01.md` | `bdba3e0ab2f1e1539274dd4a4b749aa01a435aead81d801c76debfe44cf52c33` |
| `evidence/integration/track-b-int-01.md` | `d618d7f1c0f26fd4236d03792ba4dea37a18cf3b5837553d2e3e81904d478b70` |
| `evidence/convergence/track-b-corrected-01.md` | `942a7b23ea499ddde16c658b5279e806a265992fae6eaf8c440ba80e5ee62006` |

Additional dependency evidence:
`evidence/integration/story-12.4-dependency-integration-correction-01.md`
has SHA-256
`6233809baa9ea702ed05ce562c4e9de22e14e749fe366d7917228298d14f0c8d`.

Key current production identities for source inspection:

| Path | SHA-256 |
|---|---|
| `ChatLexicalEvidenceProjector.cs` | `321dba84a1c7ba067e904477e843d521fae85b4e2ebb6724065ae050ad511225` |
| `ChatRelationshipEvidenceProjector.cs` | `6cb18ec2cf25ab0427415f6e73c0c354573474a756bd1b0efe3bde8d2d892c98` |
| `ChatEvidenceRetriever.cs` | `637b726a84022a5a69121fb4dd90cbe39e460e9f619dfe937a4079c2a66e04b6` |
| `ChatEvidenceCurrentnessValidator.cs` | `16f55b8a6293d1131ec5758d8c60c638b2a36aa2b2657f3a193aede41e5f60c2` |
| `ChatEvidencePacketBuilder.cs` | `8c8f06376bce2f5d5934e7c78aa4d073a0e73606c250f530d4dc803ec7b22d6f` |
| `ChatAgentDispatcher.cs` | `91b22ef2746104a518d0e78cdebabcdaa2eaf12ac8e62929ae12132a120c137b` |
| `OpenAiCompatibleChatProviderStreamingTransport.cs` | `33b6ed7991286dc694f314a5d61a6da19aeb7c6a826e9b5378ca08508ab03586` |
| `PreparedChatCoordinator.cs` | `4288a7fffbc0749b493591a307ba03435c4d70fb503f818e2c45f860b9ac364e` |
| `PreparedChatPublicationOwner.cs` | `20904e4313dba45a636b36fc813ce10014e01a9c7a5072e390e6bcf615e2a7d5` |
| `Program.cs` | `e9be44da3a4e5f4ed23278900867a584cfc87be755600f4f7bba2e6345ce1f1d` |

## Evidence claims to challenge

### Correctness, authorization, and currentness

- B1 reports one constructor-composed lexical projection, one relationship
  projection, and one packet build. Lexical primary identities are immutable;
  relationship expansion is optional support and cannot replace them.
- B2 reports current `DocumentEdge`, `GENERATED_FROM`, accepted v2 exact
  decisions, complete decision-set count/fingerprint, and only complete
  `Published` Flow Groups. Selected-required support fails closed on drift.
- The convergence correction changes the relationship witness to
  `ChatExactDecisionSet.SelectedDecision.Id` while retaining all independently
  valid decision-owned Published Flow Groups. The reversed Flow Group-ID vector
  now passes and closes F1.
- B3 reports per-turn re-fetch and revalidation of authorization, current
  Documents, sections, Source Anchors, relationships, exact decisions, Flow
  Groups, advisory Context authority, and Conversation State hints. Prior prose
  is context only and cannot become evidence.
- The final currentness/authorization barrier precedes citation materialization
  and publication. Missing, stale, revoked, inconsistent, or semantically
  unsupported results withhold the complete answer and do not activate a turn.

### Strict streaming transport and whole-answer validation

- Primary synthesis and the single syntax-only repair use the dedicated raw
  streaming transport under one propagated absolute deadline. There is no
  accepted SDK aggregate/non-streaming fallback.
- Evidence covers strict incremental UTF-8, SSE/JSON framing, one `[DONE]` plus
  physical EOF, terminal uniqueness, no post-terminal data, decoded-data
  inactivity, cancellation, overall timeout, received Provider Chunk and
  assembled-response exact/+1 caps, separate raw/SSE/received/decoded/final
  byte domains, zero-output rejection, and whole-buffer discard.
- Only bounded local syntax failure may repair. Semantic, handle, fingerprint,
  citation, currentness, and authorization failures reject the whole answer
  without repair. Provider frames remain server-only and client-invisible.

### Preallocated atomic publication

- `PreparedChatPublicationOwner` is intended to be the single Web-scoped owner.
  The immutable candidate preallocates the Grounded Answer, proposed next
  Conversation State, neutral completion summary, reveal directive, and bound
  packet/advisory/deadline authority before the critical section.
- All fallible and asynchronous work is intended to complete before commit. The
  synchronous critical section observes cancellation and one monotonic sample;
  cancellation or `sample >= deadline` publishes nothing. Acceptance performs
  one terminal transition to `AcceptedPublicationCommit`.
- No clock read, callback, telemetry emission, allocation-dependent success
  action, or live Conversation State mutation is intended after commit. The
  coordinator derives follow-up Conversation State only from the committed
  composite. Page/turn reveal receives only the commit's reveal directive and
  operates on an already validated complete answer.

### Telemetry separation

- Track B runtime observations and completion summaries are content-dark and
  release-neutral. Candidate commit, corpus case, release run, process key,
  signature, export, and release-pass fields are intended to be absent.
- Production composition resolves the typed raw transport, scoped neutral
  completion recorder, and the same scoped publication owner used by the
  coordinator. It composes no `Story125StreamAttestationSink` or alternate live
  Conversation State/publication owner.
- Track C alone may add sealed admission, candidate/process/signature identity,
  export, Tier 2/3 execution, real-LLM receipts, restart proof, or screenshots.

## Regression and compatibility evidence

- B1: 216/216 focused tests.
- B1+B2: 265/265 aggregate; 124/124 focused relationship/currentness/packet.
- B1-B3: 286/286 aggregate; 133/133 focused B3; 37/37 bounded state/result;
  3/3 new-conversation reset.
- B4: 13 honest public-seam REDs before production; 118/118 final focused;
  73/73 raw-streaming; 14/14 telemetry/composition; 355/355 B1-B4 aggregate.
- B-INT: 409/409 focused matrix after excluding exactly two host-path tests;
  8/8 authenticated Business/IT initial/follow-up interaction tests;
  103/103 Track A Scan UI/corpus/evidence tests; Web build 0 errors.
- Convergence attempt 2: T054 1/1; 403/403 bounded matrix without the default
  Data Protection class plus 7/7 controlled-key-ring/authenticated integration
  vectors, for 410/410 effective Story 12.5 assertions; Web build 0 errors;
  `git diff --check` passed.

No path under `VulgataDbContext`, migrations, model snapshot, or
`specs/013-exact-cross-repository-flow-pass2` differs between the corrected
definition base and this candidate. The Story 12.4 completion commit remains
ancestral. The candidate does not change a Story 12.4 production contract and
does not add an EF migration, persisted Conversation State, vector search,
autonomous retrieval loop, or Track C sink.

## Known non-product/environment observations

These observations must remain visible but must not be converted into positive
product evidence or silently suppressed:

- Two default ASP.NET Data Protection characterization tests fail only because
  the restricted sandbox cannot create keys under
  `C:\Users\13728\AppData\Local\ASP.NET\DataProtection-Keys`. Their controlled
  repository-key-ring counterparts and authenticated integration vectors pass.
- A broader Track A quickstart selection reported 12 login-startup failures at
  the same default Data Protection boundary. The directly relevant 103 tests
  passed, and Track A's committed isolated checkpoint records 116/116.
- A diagnostic broader repository selection reported 2,451 passed, 275 failed,
  and 4 skipped. It included unavailable PostgreSQL/provider/real-tool host
  prerequisites and unrelated baseline failures such as Pass 1 schema identity
  drift. It is not Track B acceptance evidence and was not exhaustively triaged.
- Builds retain existing NU1507 package-source-mapping, NU1900 unavailable
  vulnerability-feed, and MSB3277 EF assembly-version warnings. Selected builds
  and tests complete; the reviewer should still report any warning that proves
  candidate-specific risk.

## Questions for the independent reviewer

1. Does source inspection prove there is exactly one production retrieval path,
   one lexical-primary owner, one optional relationship projector, and one
   packet builder, with no legacy exact-profile or
   `ExactCrossRepositoryEdgeEvidence` authority regaining control?
2. Does the T054 correction select the first canonical current-v2 decision from
   the complete ordered decision set independently of Flow Group enumeration,
   while retaining and revalidating every decision-owned Published Flow Group?
3. Can any authorization/currentness race, including applied advisory Context
   A-to-B-to-A, relationship/decision-set mutation, or final authorization loss,
   expose an answer, citation, Source Anchor, relationship, or Conversation
   State update?
4. Is syntax repair reachable only for the closed local syntax category, and
   can any semantic/evidence/citation/handle failure or non-streaming provider
   path obtain a second attempt or an accepted answer?
5. Are received-wire, logical Provider Chunk, assembled received response,
   decoded delta, and final answer counters enforced in their own domains with
   strict UTF-8, terminal/EOF, inactivity, cancellation, and exact/+1 behavior?
6. Is every publication candidate field genuinely immutable and preallocated,
   with no fallible operation after the authoritative monotonic sample and no
   success owner other than the one atomic `AcceptedPublicationCommit`?
7. In cancellation/deadline races, is cancellation/expiry guaranteed to publish
   neither answer nor Conversation State nor completion success, while time
   advancing after a completed commit cannot retroactively revoke it?
8. Can `ChatPage` or `ChatTurn` reveal any provider fragment or uncommitted
   result, including through progressive rendering, cancellation, component
   disposal, or a late callback?
9. Are runtime observations/completion summaries content-dark and operationally
   useful without importing any Track C candidate/run/case/process/signature or
   release-pass authority into Track B?
10. Do scoped DI lifetimes ensure the coordinator, publication owner, raw
    transport, and completion recorder share the intended instances without an
    alternate live Conversation State or success path?
11. Are the focused/mutation tests sufficiently independent of implementation
    details to fail if any B1-B4/B-INT port is bypassed, reordered, duplicated,
    or replaced, and do the environment exclusions conceal no Story 12.5
    assertion?
12. Does the complete candidate preserve the accepted Story 12.4 contract and
    no-migration boundary, with no unreviewed shared-file or persistence change?

The reviewer should return a raw quality verdict bound to candidate
`a0eda652f01cb7b410a270783128d1eaefbd0ec1`, the accepted artifact hashes above,
the complete `5adc504a..a0eda652` implementation delta, and this evidence set.
