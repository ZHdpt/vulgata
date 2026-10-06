# Feature Specification: OCT-CHAT — First useful streaming conversations

**Feature Branch**: `oct14/chat`
**Created**: 2026-10-06 (Asia/Shanghai)
**Status**: Validated for planning; implementation not authorized
**Input**: New October first-deliverable feature using direct SpecKit: useful questions/follow-ups over an explicitly selected published batch, immediate answer text, real wiki-tool rounds, saved outcomes and exact references in the existing UI. Stop after independent plan review.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask a useful grounded question (Priority: P1)

An ordinary authorized user asks about a scenario across permitted Documents and sees an answer grow while generation is still running, followed by exact supporting Documents.

**Why this priority**: The first usable Chat outcome must explain business logic, not replay a fixed candidate sentence.
**Independent Test**: Ask a question requiring two Documents against the shared labelled fixture; observe actual search/read rounds, text before generation finishes and exact footer navigation. Real generated Knowledge separately qualifies usefulness.

**Acceptance Scenarios**:
1. **Given** an ordinary permitted user and explicit publication, **when** a multi-document question is asked, **then** Chat searches/browses, reads exact content/sections and follows existing related Documents as useful, returning a supported narrative and material Uncertainties.
2. **Given** answer text before or between tool rounds, **when** generation is unfinished, **then** the user sees provisional text without waiting for references or saving and sees concise real tool activity.
3. **Given** references reported for exact content read this turn, **when** the footer appears, **then** entries are deduplicated, currently authorized and open the shared exact reader; invented/foreign-turn references are excluded.
4. **Given** insufficient Knowledge, **when** a question cannot be established, **then** Chat states the gap and permits an empty footer without invented evidence.
5. **Given** a large Document or individual section, **when** bounded continuation is needed, **then** successive reads preserve the exact selection, complete text characters and qualifiers; stopped reads disclose incompleteness without crediting the unread remainder.
6. **Given** source-supported lock acquisition and an advisory explanation of business motive, **when** Chat streams an explanation, **then** the material source/advisory distinction is visible beside the affected prose from its first presentation, including explicitly attributed mixed claims.

### User Story 2 - Continue a safe conversation (Priority: P1)

A user asks narrative follow-ups using useful completed dialogue within the conversation's fixed mode and scope.

**Why this priority**: A one-shot answer does not demonstrate conversational value or context isolation.
**Independent Test**: Complete one turn, ask a follow-up requiring another wiki read, attempt overlapping submission, then change mode/scope after settlement.

**Acceptance Scenarios**:
1. **Given** completed permitted dialogue, **when** a follow-up arrives, **then** bounded useful history informs the answer and new evidence is read in this turn's stable selection; earlier references do not count as new reads.
2. **Given** an active or unresolved answer, **when** another submission/retry arrives, **then** no overlapping generation starts.
3. **Given** an offered mode/scope change, **when** the current work is settled and the user confirms, **then** a new empty conversation starts without importing old transcript, summary or model context.
4. **Given** saved completed turns, **when** permitted dialogue is loaded again, **then** order and original references remain intact without replaying generation or animation.
5. **Given** completed dialogue containing advisory or mixed claims, **when** a follow-up reuses it under a history budget, **then** material attribution stays with the claim or the affected group/turn is omitted; the motive is never silently upgraded to source proof.

### User Story 3 - Protect restricted information (Priority: P1)

Business Mode reads Business Logic only; IT Mode can also read permitted Code Logic. Ordinary reader access does not require management authority.

**Why this priority**: Authorization is a prerequisite to every disclosure, including partial text and metadata.
**Independent Test**: Exercise the common fixture with BL, CL, an Uncertainty and a disallowed contributor across tools, history, streaming and footer.

**Acceptance Scenarios**:
1. **Given** Business Mode, **when** tools, history or references are disclosed, **then** CL content, titles, counts, paths and technical metadata are absent server-side.
2. **Given** revocation of any source/advisory contributor, **when** output or history is requested, **then** affected material is withheld and active affected generation stops; already delivered text cannot be recalled.
3. **Given** missing/unpublished/wrong-database/mismatched selected data, **when** Chat opens, **then** it reports unavailable without substituting a prior batch, legacy wiki or demo data.

### User Story 4 - Recognize completed and interrupted answers (Priority: P2)

A user distinguishes saved answers from useful partial text and retries explicitly only after originating work settles.

**Why this priority**: Visible text is not completion; duplicate paid generation and lost outcomes defeat useful conversation.
**Independent Test**: Induce truncation, unfinished tools, cancellation, transport failure and uncertain save acknowledgement; inspect one outcome without repeated generation.

**Acceptance Scenarios**:
1. **Given** valid untruncated generation and settled tools, **when** one content/reference/status outcome is saved, **then** completed status is shown and the turn becomes eligible completed history.
2. **Given** useful authorized partial text, **when** interrupted, **then** it is retained with a reason and excluded from normal completed-answer inference history.
3. **Given** uncertain save acknowledgement, **when** recovered, **then** the same outcome is looked up or identically resubmitted without asking the model again.
4. **Given** browser/host interruption, **when** the user returns, **then** saved, unresolved or interrupted state is shown honestly; no live reattachment is promised and explicit retry cannot overlap unresolved work.

### Edge Cases

- Empty permitted results, BL not yet published, sparse relationships and knowledge gaps are valid; bounded results disclose limits without denied counts.
- Publication changes cannot mix running-turn discovery, pagination and exact content; the first-deliverable conversation stays on its explicitly selected immutable publication.
- Invalid arguments, duplicate tool identities, malformed streaming data, truncation and budget exhaustion cannot produce completed status.
- Retrieved malicious instructions and fabricated handles cannot alter application authority.
- Revoking a contributor not displayed in the footer still invalidates affected generated text/history.
- Late output remains bound to its originating turn; duplicate saves cannot append duplicate dialogue.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Bind conversations to authenticated owner, explicit batch/publication, fixed authorized Repository scope and mode. Do not enforce names/exactly-two eligibility or use latest-batch discovery/operator authority for ordinary readers.
- **FR-002**: Consume shared Knowledge policy for stable turn reads and live subject/purpose/mode/all-contributor checks over content and metadata. Business Mode is BL-only.
- **FR-003**: Support bounded search/browse, exact content/section and existing-related-Document reads with provenance, coverage, limits and Uncertainties.
- **FR-003a**: Permit successive bounded reads of large Documents and large individual sections within the same exact selection. Preserve complete text characters, material qualifications and live authorization; distinguish the returned portion from unread content and explicitly disclose budget-limited incompleteness.
- **FR-003b**: Preserve source-supported, advisory, hypothesis, uncertain and mixed material claim-group distinctions in prompts, the first presentation of affected streamed prose, saved answers and reused history. Mixed claims retain explicit attribution; supplied motives never become source-proven facts through paraphrase or follow-up.
- **FR-004**: Run actual bounded multi-round conversations; retrieval is evidence, never instructions. Chat cannot initiate Scans, repair Knowledge or use source tools.
- **FR-005**: Display genuine provisional answer text before provider completion, footer readiness and save, including between tool rounds; no complete-candidate buffering or typewriter replay.
- **FR-006**: Show concise sanitized running/done/failed activity in the existing assistant turn, without reasoning, credentials or raw tool payloads.
- **FR-007**: Accept reference reports only for exact authorized content/sections read this turn. Derive safe titles/destinations server-side, deduplicate in first accepted report order and allow empty reports. Reference identity is not semantic proof.
- **FR-008**: Preserve durable ordered dialogue, one active answer per conversation and bounded useful completed history with current contributor authorization. Interrupted text is not completed history.
- **FR-009**: Save one idempotent terminal content/reference/status outcome per turn. Completion requires valid untruncated generation and no unfinished tools; ambiguous acknowledgement is resolved from saved state without regeneration.
- **FR-010**: Retain useful authorized partial text with honest interrupted/failed status. Never automatically regenerate visible output or silently fail over providers; explicit retry waits for settlement.
- **FR-011**: Keep conversation mode/scope fixed. Offered changes require confirmation and start empty conversations after settlement. Preserve Chinese Fluent/Library Oak and Business/light, IT/dark UI.
- **FR-012**: Fail closed on unavailable/mismatched publication, provider binding or authorization, without prohibited metadata or legacy/demo fallback.
- **FR-013**: Qualify real multi-document tool use, follow-up, exact references, unknown and interruption with source-checked questions; separate controlled mechanics, real streaming and content usefulness evidence.

### Key Entities *(include if feature involves data)*

- **Conversation**: Owner, fixed mode/scope, selected publication, ordered dialogue; separate from wiki Knowledge.
- **Chat Turn**: Request identity, originating conversation, Knowledge Read Context, bounded execution, provisional text and one terminal outcome.
- **Knowledge Read Context**: Knowledge-owned version-bound selection for discovery, membership, pagination, exact reads and citations, with live authorization.
- **Document Version / section reference**: Immutable identity actually read this turn with safe presentation and exact contributor provenance.
- **Turn outcome**: Durable completed/interrupted/failed text/reference/status result; uncertain saving is not success.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A declared real question set includes a cross-Repository multi-document explanation, a follow-up needing a further wiki read and an unknown. Each has a recorded source-checked usefulness/gap assessment.
- **SC-002**: Controlled and real observations both show at least one visible answer segment before generation completes, with timestamps. A controlled interleaved scenario also displays text before a later tool round finishes.
- **SC-003**: Every displayed footer opens the same exact authorized version/section; zero invented or foreign-turn handles pass negative checks.
- **SC-004**: Declared Business exclusion, revoked-contributor and missing-selection checks disclose zero prohibited content/metadata.
- **SC-005**: Duplicate-submission and uncertain-save checks yield one generation per turn identity and one terminal outcome; truncation/unfinished tools never appear completed.
- **SC-006**: A follow-up uses permitted completed dialogue; interrupted text and an old IT session contribute no inference context to a fresh Business conversation.
- **SC-007**: Large multi-section Document and large single-section checks recover all content through bounded continuation when budgets permit; limited reads never claim unread coverage. Changed/expired continuation or revoked access yields safe failure without substitution.
- **SC-008**: Business's lock-action/supplied-motive fixture preserves material attribution in streamed answers and a saved-history follow-up, with no unsupported promotion of motive/duration to source proof and no whole-answer delay.

## Assumptions

- Authoritative inputs are the [October delivery plan](../../docs/bmad/planning-artifacts/october-delivery-plan-2026-09-30.md), [integration plan](../../docs/bmad/planning-artifacts/october-integration-plan-2026-10-06.md), [Chat brief](../../docs/bmad/planning-artifacts/october-delivery/streaming-chat-feature.md) and compatible [October scope proposal](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md), verified at `fad7f11c9d6bc9ed3da1b371b24beb9f5c09db5d`.
- OCT-BUSINESS B0 owns shared contracts/fixture, live Knowledge policy, persistence and October migrations, including Chat's supplied dialogue requirements. These are dependencies, not delivered APIs at this baseline.
- OCT-EXPLORE supplies the exact reader. The integration owner supplies shared Web composition and selected Chat-provider wiring. Fixtures establish mechanics only.
- Fixed publication and omitted same-turn reconnect follow the October cut. Broader fresh-selection, scope discovery, summaries/history UI, exceptional IT source reads and reattachment remain unmet wider-product scope, not deleted requirements.
- No retrieval/RAG UI, source viewer, graph ownership, provider failover, Scan-strategy change, external tools, generalized recovery platform or visual redesign is included.
- Planning does not authorize task generation, implementation, runtime/provider execution or database changes.

## Traceability

Selected portions of Stories 19.1–19.3 only. Preserve [PRD](../../docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md) FR-11.1–11.4/11.6–11.8, FR-13.3, NFR-1.2/2.2–2.3/3.3 and [spine](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md) AD-24–29 within the explicit October dispositions. Original stories/full PRD are not completed by this feature.
