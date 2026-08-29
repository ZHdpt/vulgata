# Story 12.5 Track B quality-review input 03

- Candidate: `f8c89541cfbef3ba89fea79c852ae9fcfe7bc081`
- Correction base: `1513747666ba8c5f8f05b1c99e340251f9ab2781`
- Branch: `demo0831/story-12-5`
- Scope: T068-T075 correction and convergence for QR-TB-009; prior Track B/B-INT behavior remains in review scope.
- Intended consumer: fresh independent quality reviewer for T077.

This is a verdict-neutral input. It does not claim quality acceptance, E2E acceptance, final Track B verification, Track C readiness, or Story completion.

## Accepted authority bindings

| Artifact | SHA-256 |
| --- | --- |
| `spec.md` | `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4` |
| `plan.md` | `fad42dcd3a3f393809e95500ea6d6c7607ce4a16787d8b16d054b415ce533318` |
| `tasks.md` after T075 | `1484c1d736e7b3f30fb0861cbdd15cfbc0ce4b2395759cf2fd4d3157dbe4d877` |
| T074 correction checkpoint | `5b67e4c3cda0a17c7fc82844323334d2600708973c7587fea9baf182bcc675fc` |
| T075 convergence report | `f794b0fd3d04005db0e90e37230515da38f7bb0c37988d975bd0dd261fbb35dd` |

The specification and plan hashes are unchanged from the accepted authority set. T075 returned `CONVERGED` without appending tasks.

## Raw finding under review

Quality review 04 raised `QR-TB-009` (HIGH, correctness/conversation reset/publication authority): the visible `新对话` action cleared page state but did not reset the scoped owner-current committed Conversation State. A next request in the same mode and System/two-Repository scope could therefore inherit hidden prior validated turns, handles, packet fingerprint, and Logical Document hints. A pre-reset in-flight candidate could also become current after reset because it was not bound to a conversation generation.

Requested correction: add public accepted-turn/reset/next-request and reset-race REDs; route explicit reset through one publication-owner-owned generation boundary; bind every candidate to the captured generation; ensure no pre-reset candidate can activate or reactivate current state; preserve re-fetch/revalidation, accepted-only reveal, and mandatory streaming.

Source: `reviews/quality-review/track-b-quality-review-04.json`, finding `QR-TB-009`.

## Complete correction-base-to-candidate delta

`git diff --name-status 1513747666ba8c5f8f05b1c99e340251f9ab2781..f8c89541cfbef3ba89fea79c852ae9fcfe7bc081`

```text
A evidence/checkpoints/track-b-quality-correction-04.md
A evidence/convergence/track-b-quality-correction-03.md
M tasks.md
M workflow-state.json
M src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs
M src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs
M src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs
M tests/Vulgata.Tests/ChatPageTests.cs
M tests/Vulgata.Tests/ChatPipelineCompositionTests.cs
M tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs
M tests/Vulgata.Tests/PreparedChatIntegrationTests.cs
M tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs
```

The delta is 12 paths, 599 insertions, and 71 deletions. The production boundary is exactly three files. No `Program.cs`, project file, EF context/configuration/migration/model snapshot, Infrastructure retrieval/currentness, Agents streaming transport, Story 12.4 contract/artifact, sprint status, or Track C file changed.

## Implementation and evidence to inspect

- The scoped `PreparedChatPublicationOwner` owns one serialized `(generation, current accepted composite)` authority. `CaptureSnapshot`, `Reset`, and `Commit` are required typed operations; there is no default-interface, `ConditionalWeakTable`, `object`, null, or direct-construction fallback.
- `Reset` advances generation and clears current state under the same owner gate used by commit.
- `PreparedChatPublicationCandidate` is immutable and bound to the captured owner authority and generation. Commit rejects a foreign-owner or stale-generation candidate before accepted publication. First, repeated, delayed, and concurrent stale commits remain closed.
- `PreparedChatCoordinator` reads current Conversation State only from the owner snapshot, binds the request/candidate to that snapshot, and has no local `PreparedChatPublicationAuthority` or live `ChatConversationState` owner.
- `ChatPage.StartNewConversationAsync` invokes the coordinator reset before page-version advance, cancellation, delayed clear, notice, and focus. Page version remains presentation-race protection only.
- T074 records honest REDs, 18/18 focused GREEN, 140/140 affected suites with the two environment exclusions removed, and 436/436 bounded Track B/B-INT/mandatory-streaming verification.
- T075 independently traced 60 FR, 17 SC, AD-1 through AD-12, Constitution I-V, and T001-T074; it reran 397 passing tests, observed only the same two sandbox failures, built Web with zero errors, validated the workflow, and appended no tasks.

## Required adversarial challenges

The reviewer should independently challenge, at minimum:

1. Accepted-turn then explicit `新对话` then identical-mode/identical-scope request: no prior turn, answer, citation handle, packet fingerprint, or Logical Document hint reaches retrieval.
2. Reset racing a pre-reset candidate before terminal commit: first, repeated, delayed, and concurrent commit calls cannot publish, become current, or reactivate state.
3. Reset during accepted Unicode progressive reveal: late page continuations reveal no pre-reset answer/citations and cannot restore pre-reset owner-current Conversation State.
4. Owner identity and generation binding: foreign-owner, unbound, stale-generation, default-interface, compatibility-table, coordinator-local, or alternate live-state authority paths are absent.
5. Commit/reset ordering and exception behavior: the sole owner gate retains one terminal commit, bounded deadline/cancellation behavior, immutable accepted composite, and no post-linearization mutation.
6. Preservation: same-scope follow-up still re-fetches/revalidates; scope changes reset correctly; currentness, authorization, evidence-packet v2.2, relationship authority, citation validation, strict raw provider streaming, caps/deadlines, and whole-answer semantic rejection remain intact.
7. Publication boundary: no provider fragment, unvalidated claim/citation/handle/relationship/Source Anchor, or completion summary crosses the client boundary before the accepted composite; immediate/progressive Unicode reveal still consumes only the committed directive.
8. Composition: exactly one scoped typed publication owner and completion recorder are injected; no optional/null/object fallback, second success owner, Track C attestation sink, or non-streaming dispatcher boundary exists.

## Environment exclusions

Two production-composition characterization tests attempt the default ASP.NET Data Protection key path under `C:\Users\13728\AppData\Local\ASP.NET\DataProtection-Keys`, which this sandbox denies. They fail with `UnauthorizedAccessException` before exercising Story 12.5 behavior:

- `ProductionComposition_KeyRingPathWithoutApplicationName_PreservesDefaultsAsync`
- `ProductionComposition_ApplicationNameWithoutKeyRingPath_PreservesDefaultsAsync`

Controlled key-ring vectors pass. The reviewer should classify these independently and must not convert the exclusion into product acceptance.

## Gate boundary

Only the independent reviewer may issue the T077 verdict and write `reviews/quality-review/track-b-quality-review-05.json`. A `REVISE` verdict returns to append-only test-first correction tasks; `BLOCKED` stops; `ACCEPT` permits the sprint loop to decide conditional E2E and final Track B verification. This input itself opens none of those gates.
