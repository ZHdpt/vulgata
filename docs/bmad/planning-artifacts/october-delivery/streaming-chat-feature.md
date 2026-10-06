# OCT-CHAT — First useful streaming conversations

## First-deliverable boundary

An ordinary authorized user asks questions and narrative follow-ups over one explicitly
selected published batch, using its configured Chat provider. Business reads BL only;
IT reads permitted CL and BL. Keep mode and scope fixed per conversation; if a selector
is provided, changing either starts a new empty conversation. Preserve Chinese
Fluent/Library Oak and Business/light, IT/dark themes.

[Stories 19.1–19.3](../october-release-epics.md#story-191-use-permitted-wiki-tools-and-report-answer-documents)
provide traceability, not a requirement to complete every original acceptance criterion
or dependency before this first deliverable. Reconnect, advanced history/summary
management, source inspection and new retrieval/RAG UI are outside this slice.
This does not mark the original stories or wider PRD complete.

## Inputs and parallel work

Baseline `57fad124` includes the narrowed tools and batch-setup integrations. Full
Story 18.5 is abandoned as a delivery plan; minimum Knowledge foundation is now
**OCT-BUSINESS B0**. Consume the
[minimum shared contract](../october-delivery-plan-2026-09-30.md#minimum-shared-contract):
selected batch/publication, stable turn reads, exact handles, bounded wiki operations,
qualifications and live user/purpose/mode/all-contributor authorization. Business
exclusion covers metadata, tools, history and references, not only prose.

Start against agreed contracts and labelled controlled fixtures in parallel with
producers; integrate the first real published CL/BL as it appears. Fixtures establish
mechanics, not generated-document quality. Chat owns wiki adapters, conversation
execution, typed events and feature-local UI. OCT-BUSINESS owns shared read policy,
persistence and migrations, including Chat's supplied conversation requirements;
do not create a duplicate Knowledge store or authorization policy.

## Reuse and required replacement

Reuse provider configuration, encrypted credentials, bounded SSE/UTF-8 handling and
sanitized observations selectively. The current tool loop is not already implemented:

- [Demo scope](../../../../src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs)
  fixes backend repositories by name, while the
  [scope resolver](../../../../src/dotnet/Vulgata.Infrastructure/Data/PreparedChatScopeResolver.cs)
  requires a pair. Replace both with the selected-batch contract.
- [Dispatcher](../../../../src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs)
  buffers deltas until strict candidate JSON can be parsed;
  [assembler](../../../../src/dotnet/Vulgata.Agents/ChatExecution/ChatProviderStreamAssembler.cs)
  accepts content/stop, not native tool rounds. Implement bounded native tool-call
  assembly/execution or an equivalent using installed MAF capabilities; preserve
  transport checks without retaining candidate-only answer gates.
- [ChatPage](../../../../src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs)
  reveals accepted text after `AskAsync` finishes. Replace typewriter replay with
  immediate provider deltas, including text between tool rounds.

## Minimum working increment

Provide bounded wiki search/browse, content/section and existing-related-document
tools. Treat retrieved content as evidence, never instructions. Run actual multi-round
tool conversations and follow-ups, preserving useful completed history. Report only
exact handles read this turn and still authorized; derive footer titles and reader
links server-side, rejecting invented or foreign-turn handles. Empty reports and
honest knowledge gaps are valid.

Use basic running/done/failed UI, concise sanitized tool activity and an exact-document
footer linking to OCT-EXPLORE's single reader. Deltas appear before provider completion,
footer readiness or save. Permit one active answer per conversation; persist ordered
completed history and one idempotent content/reference/status outcome per turn.
Recover uncertain save acknowledgement by checking that outcome, not generating
again. Completion requires valid untruncated generation and no unfinished tools.
Retain useful authorized partial text as interrupted, not completed history; never
automatically replay generation after visible output. Expose no raw reasoning,
credentials or tool payloads, and add no provider failover.

## Verification and integration

Check a real tool round plus a useful follow-up, Business CL/metadata denial, exact
footer navigation, interruption and idempotent save. Use a small source-checked
question set including an unknown; record visible-before-finish timing and distinguish
retrieval gaps from answer/document quality. Controlled failures remain labelled.

The [explicit integration phase](../october-integration-plan-2026-10-06.md) connects
the same published batch, provider, reader and graph. The integration owner adds the
new opt-in October read module to ordinary Web; integrated 18.4 does not already
register these queries or load the selected batch's Chat provider. Coordinate shared
`Program.cs`/navigation changes with that owner; OCT-EXPLORE owns the shared reader
route. This brief authorizes no workflow execution today.
