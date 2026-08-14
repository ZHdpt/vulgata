# Transient Chat History and Progressive Reveal Design

## Purpose

Improve the demo Chat experience by retaining prior turns within the current
page lifetime, making **新对话** the explicit reset action, and revealing a
validated answer progressively with a brief fade-in effect.

This design does not add conversational context to Chat requests. Every question
remains an independent prepared Chat request over the currently selected System
and exactly two Repositories.

## Scope

### Included

1. Retain all submitted questions and their assistant results in the rendered
   timeline while the Chat page component remains alive.
2. Preserve that timeline when Chat Mode, System, or Repository selections
   change.
3. Clear the complete timeline only when the user clicks **新对话**.
4. Reveal successful answer text one Unicode text element at a time after the
   complete Grounded Answer has passed existing validation.
5. Apply a transient fade-in effect to newly revealed text.
6. Preserve cancellation, authorization, currentness, citation, and fail-closed
   behavior.

### Excluded

- Database, browser-storage, or cross-navigation history persistence.
- Sending previous turns to `PreparedChatCoordinator`, the Chat dispatcher, or
  the LLM provider.
- Multi-turn conversational reasoning or follow-up-question resolution.
- Provider-token or provider-prose streaming.
- Changes to evidence retrieval, Candidate Claim selection, currentness
  validation, authorization, or Grounded Answer composition.

## Existing Constraint

The provider returns only an ordered Candidate Claim selection. The server
materializes the answer from immutable evidence after validating the complete
selection, current Document Versions, exact spans, Source Anchors, relationships,
and Repository authorization. The approved release contract requires atomic
publication and forbids exposing partial provider output.

Therefore, progressive output begins only after
`PreparedChatCoordinator.AskAsync` returns a complete validated Grounded Answer.
This is a presentation-layer progressive reveal, not network streaming.

## UI State Model

Replace the page's single published question, answer, and message fields with an
ordered in-memory collection of `ChatTurn` presentation records. A turn owns:

- a locally generated identity;
- the normalized submitted question;
- the Chat Mode used for that request;
- a state: `Pending`, `Revealing`, `Complete`, `Unsupported`, `Error`, or
  `Cancelled`;
- the validated Grounded Answer when successful;
- per-claim visible text used during progressive reveal;
- a bounded Chinese status or fallback message when unsuccessful.

`ChatTurn` is page presentation state. It is not a Core entity, persistence
model, provider contract, or conversation-context object.

## Interaction Behavior

### Sending a question

1. Snapshot the current question, Chat Mode, System, and Repository selection.
2. Append a new pending turn and clear the composer.
3. Call the existing coordinator with only the current question and scope.
4. Keep all older turns visible while the request runs.
5. On success, attach the validated Grounded Answer to the pending turn and
   start progressive reveal.
6. On unsupported, error, or cancellation, update only the pending turn with the
   corresponding bounded state and message.

Only one request or reveal runs at a time. Existing composer disabling and
cancellation behavior remain in force.

### Selection and mode changes

Changing Chat Mode, System, Repository one, or Repository two updates only the
composer's next-request state. It does not remove or rewrite prior turns. Each
successful historical turn renders citations according to the Chat Mode stored
in its Grounded Answer, not the newly selected mode.

Selection changes may clear transient validation/status notices and may clear
the draft question where existing behavior requires it, but must not clear the
timeline.

### New conversation

Clicking **新对话**:

1. increments the conversation version;
2. cancels and disposes the active request or reveal cancellation source;
3. clears all `ChatTurn` records;
4. clears the composer and transient messages;
5. preserves the selected Chat Mode, System, and Repositories;
6. restores the ready `LoadState`;
7. briefly displays **新对话已开始**;
8. returns focus to the composer.

The conversation-version guard prevents a late request or animation callback
from repopulating the cleared timeline.

## Progressive Reveal

Successful answers reveal each Material Claim sequentially, one Unicode text
element at a time. Using text elements rather than UTF-16 code units prevents
splitting surrogate pairs, emoji, or combining sequences.

- The first text element appears as soon as the validated Grounded Answer is
  available to the component.
- Subsequent elements appear at a fixed interval of approximately 20-30 ms.
- Each newly visible segment receives a 120-180 ms opacity transition.
- A claim's Document Citations appear only after that claim is fully visible.
- Unsupported, error, and cancellation messages appear immediately.
- If the user prefers reduced motion, the complete answer and citations appear
  immediately without timers or animation.
- Cancellation, disposal, or **新对话** stops the reveal without publishing
  additional characters.

The final rendered claim text must be byte-for-byte equivalent at the .NET
string level to the validated Material Claim text. Progressive reveal never
modifies, summarizes, or regenerates evidence.

## Component Boundaries

Primary changes remain in:

- `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`
- `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`
- `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.css`
- `tests/Vulgata.Tests/ChatPageTests.cs`

A small page-local presentation type may be added beside `ChatPage` if keeping
it in the code-behind would make the component difficult to understand. No
Core, Infrastructure, database, migration, coordinator, evidence, or provider
contract change is intended.

Reduced-motion detection may use a small static CSS rule for animation and a
minimal browser interop query only if the reveal timer itself must be skipped.
The implementation should prefer the smallest approach that gives deterministic
component tests.

## Accessibility

- The timeline remains an `aria-live="polite"` region.
- Do not announce every individual character to assistive technology. The
  completed claim must be exposed as one coherent accessible text value, or
  live announcements must be suppressed during reveal and emitted once on
  completion.
- The loading state remains distinguishable from the reveal state.
- Reduced-motion users receive the complete answer immediately.
- Focus remains in or returns to the composer after **新对话**.

## Failure Handling

- A failed new turn never removes older turns.
- Unsupported responses append the existing bounded no-information assistant
  response.
- Provider, evidence-currentness, authorization, and validation failures append
  their existing bounded Chinese response without leaking diagnostics.
- A cancelled request updates its pending turn to `Cancelled`; **新对话** removes
  it along with the rest of the timeline.
- Component disposal cancels both request and reveal activity.

## Verification

Focused component tests must prove:

1. two successful questions render two retained user/assistant pairs;
2. switching Chat Mode preserves prior turns;
3. changing System or Repository selection preserves prior turns;
4. coordinator requests contain only the current question and never history;
5. unsupported and error results append without erasing older turns;
6. **新对话** clears all turns but preserves mode and scope selections;
7. late request completion cannot restore a cleared turn;
8. reveal starts only after a successful complete coordinator result;
9. the first text element appears immediately and the final text is exact;
10. Unicode grapheme clusters are not split;
11. citations remain hidden until their claim completes;
12. cancellation and **新对话** stop reveal activity;
13. reduced-motion behavior renders the answer immediately;
14. existing authorization, citation-mode, unsupported-answer, validation, and
    cancellation component tests remain green.

Browser verification should cover one two-question timeline, a Business/IT Mode
switch without history loss, the fade-in reveal, and the **新对话** reset.

## Acceptance Boundary

The feature is accepted when the page provides a convincing multi-turn visual
timeline and progressive answer presentation while every coordinator/provider
request remains single-question and the existing complete-answer safety contract
remains unchanged.
