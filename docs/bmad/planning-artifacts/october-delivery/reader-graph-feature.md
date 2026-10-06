# OCT-EXPLORE — Exact reader first, then a small real document graph

## First-deliverable boundary

An ordinary authorized user opens exact published Knowledge Documents without
management permissions. Deliver the reader before graph work: IT reads accepted CL
immediately, and Business reads BL when available. Before BL exists, show truthful
Business empty/denied states, never a CL substitute. Then show a bounded graph of
the same selected published data and open its Documents through that reader.

[Story 18.6](../october-release-epics.md#story-186-read-exact-knowledge-documents-without-management-permissions)
and [Stories 20.1–20.2](../october-release-epics.md#story-201-query-an-authorized-graph-of-the-published-wiki)
remain source traceability. Their complete original acceptance criteria and dependency
chains are not mandatory for this first deliverable; do not report this narrower
result as completion of those full stories. A small explicit scope is sufficient.
Defer advanced filters/search, interactive expansion, large-graph handling and full
version-history browsing. No source viewer or live-refresh engine is included.

## Inputs and parallel work

Baseline `57fad124` includes the narrowed tool and batch-setup integrations. Full
Story 18.5 is abandoned as a delivery plan; **OCT-BUSINESS B0** supplies minimum
Knowledge foundation. Agree the
[minimum shared contract](../october-delivery-plan-2026-09-30.md#minimum-shared-contract):
selected batch/publication, exact version/section handles, safe titles, accepted
relationships, qualifications and live user/purpose/mode/all-contributor authorization.

Begin reader components and graph projection against labelled controlled fixtures in
parallel with producers. Consume the first real CL and BL publications as available,
without waiting for a broad batch or finished Chat. Fixtures prove wiring, authorization
and layout, not genuine generation. Final integration uses the same actual published
data as Chat, not a richer demonstration substitute.

OCT-BUSINESS owns shared read policy and persistence/migrations. OCT-EXPLORE owns
bounded graph projection/query DTOs, the single reader route and feature-local UI;
consume policy rather than forking authorization or creating another store. Chat
links to that reader. The integration owner owns shared `Program.cs`, navigation
and opt-in module composition; coordinate changes rather than editing shared files
independently.

## Reader increment

Replace the management-only
[Documents placeholder](../../../../src/dotnet/Vulgata.Web/Components/Pages/Management/DocumentsPage.razor)
with ordinary Knowledge-authorized navigation. Render the exact selected version's
structured content, title/type and material gaps or qualifications; open an exact
section when supplied. Unknown, unpublished, revoked and unavailable targets must not
fall back to another batch, today's version or a current checkout. Preserve Chinese
Fluent/Library Oak, Business/light and IT/dark, keyboard access, readable loading/error
states and simple retry feedback.

Existing version/section/provenance storage and authorization patterns are reusable,
but [GetCurrentAsync](../../../../src/dotnet/Vulgata.Core/DomainServices/IKnowledgeStoreRepository.cs)
is not an exact-version published-read contract. B0 supplies that boundary; the reader
must not query producer internals to compensate.

## Graph increment

Replace the management-only
[Graph placeholder](../../../../src/dotnet/Vulgata.Web/Components/Pages/Management/GraphPage.razor)
with permitted CL/BL nodes and actual accepted `REFERENCES`/`GENERATED_FROM` edges,
bound to selected publication and exact reader destinations. Apply authorization before
returning nodes, edges, labels, counts or tooltips. Business remains BL-only; omitted
CL must not leak through topology or metadata.

Provide simple pan/zoom/fit, node selection, open-Document action, directional legends,
visible uncertainty/gaps and manual refresh. Cap displayed results with a clear
limit/incomplete indication; never present a truncated neighborhood as complete.
Refresh selects coherent published data without mixing old nodes and new relationships.
Sparse or empty graphs are valid. Shared CL helpers do not imply BL-to-BL business
dependencies or runtime order. CodeGraph's source-symbol/caller index is not this
Knowledge graph; substitute neither it nor fabricated demo edges. No graph library
is installed in the Web package baseline; this slice does not mandate a new library,
graph database or framework. Layout never edits domain edges.

## Verification and integration

Verify ordinary-user CL reading, Business BL reading/CL-metadata denial, exact Chat
footer and graph navigation, revocation/unpublished failures, bounded results and
truthful uncertainty/refresh. Inspect a small real published neighborhood against
accepted relationship evidence; separate fixture layout checks from that proof.

The [explicit integration phase](../october-integration-plan-2026-10-06.md) adds a new
opt-in October read module to ordinary Web and binds both consumers to one publication.
Integrated 18.4 does not already register these read queries or selected-batch provider
loading. This brief is first-deliverable planning, not workflow execution today.
