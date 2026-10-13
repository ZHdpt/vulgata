# Knowledge read latency and graph timeout — repair handoff

Date: 2026-10-11 (Asia/Shanghai). Purpose: a fresh chat will diagnose and fix the
problem directly in `G:\source\repos\vulgata`. This document records evidence and
recommended boundaries; it does not itself authorize runtime operations or expand
the October first-deliverable scope.

## Current checkout and ownership

Observed at handoff creation: branch `oct14/main`, HEAD `4457b38d`
(`Fix post-login rendering during October Chat initialization`). Chat, Explorer
and Scan integration is already present. Verify the checkout again; do not reset
newer work. The pre-existing untracked
`docs/bmad/planning-artifacts/october-minimal-bl-first-publication-2026-10-11.md`
belongs to other work; preserve it. No source edits or runtime operations were
performed while authoring this handoff.

The user manages coordination. OCT-BUSINESS is working independently in its lane.
Do not alter its running process, operator permissions, publication inputs or
schema. Avoid concurrent edits to shared persistence/read contracts; coordinate
any such overlap with the user. Do not restart broad planning or load skills
unless explicitly requested.

## Evidence: what was actually observed

Source: latest completed report from **Qualify Explorer and IT Chat**, chat ID
`01a12a59-649c-79c0-80d9-6ca299e7d05c`, read on October 11. These are that chat's
reported measurements, not a second reproduction by the handoff author.

The prepared ordinary User reached Document and graph pages on loopback port
15075 against retained data. PostgreSQL stayed running. No provider request was
made, and that measurement pass changed neither application code nor retained data.

| Browser surface     |                             First captured load | Three subsequent loads                     |
|---------------------|------------------------------------------------:|--------------------------------------------|
| Document list       |                                        3,961 ms | 3,880 / 3,853 / 3,785 ms                   |
| Same exact Document |                                       18,696 ms | 12,272 / 10,430 / 9,641 ms                 |
| Graph               | About 13.6 seconds until budget/timeout message | No successful graph qualification reported |

Times measure browser navigation to visibly loaded content, not database time.
The attempted Web restart failed because port 15075 was occupied; the browser
continued using the existing listener. **Cold startup was not isolated.** Do not
label the first sample a cold-start result or claim the listener ran the newest
binary without verifying its ownership/build.

The graph showed `图谱读取已超时或达到读取限制，请重试。` after
`正在加载图谱…`. Its server operation deadline is ten seconds. Browser navigation
time and server deadline are different intervals, so 13.6 seconds is not proof
that the deadline itself malfunctioned.

The report reproduced these operation-log timestamps (timezone not established
in that report; preserve them as raw log values rather than relabeling as local):

| Operation                    | Timestamp    |
|------------------------------|--------------|
| OpenSelection                | 13:37:35.031 |
| Search                       | 13:37:37.578 |
| First GetRelated completion  | 13:37:41.139 |
| Second GetRelated completion | 13:37:44.720 |

The reporter inferred expiry during or before completion of a third serial
GetRelated. **That is not proven:** no canceled-operation timing was recorded.
Timestamp differences are not reliable isolated method durations.

## Diagnosis status and code entry points

Confirmed: persistent slow list/Document reads and a graph budget failure.
Not confirmed: exact costly stage, database bottleneck, precise cancellation point,
cold-start cost, or how much time belongs to frontend rendering.

Current source corroborates repeated work worth measuring:

- `src/dotnet/Vulgata.Web/Explore/ExploreGraphPresentationService.cs`:
  ten-second overall deadline, seed search followed by serial awaited GetRelated
  reads, current-context checks and final aggregate dependency reauthorization.
- `src/dotnet/Vulgata.Infrastructure/Data/October/Knowledge/OctoberKnowledgeReader.cs`:
  selection/read-context loading calls the published source; visibility/disclosure
  processing includes sequential Document and contributor checks.
- `src/dotnet/Vulgata.Infrastructure/Data/October/Knowledge/OctoberKnowledgePublishedSource.cs`:
  inspect publication materialization, queries, validation and hashing.
- `src/dotnet/Vulgata.Core/DomainServices/OctoberKnowledgeDisclosurePolicy.cs`:
  live permission/disclosure policy; preserve its semantics.
- `src/dotnet/Vulgata.Web.ViewModels/Explore/KnowledgeGraphViewModel.cs`:
  maps ReadBudgetExceeded to the observed message; it is not the expensive loader.

Repeated full publication materialization and repeated authorization/query work
are credible hypotheses, not established causal attribution. Graph has no LLM
dependency. Sparse/absent cross-repository edges are valid and must not be fabricated.

The separate Chat message `对话服务暂不可用。` is not yet explained by this latency
report. An earlier host deliberately disabled Chat dispatch. Verify current Chat
configuration separately; do not claim this repair alone activates Chat. Slow wiki
reads may also affect Chat tools, which is why a shared read-path fix is preferable
to a graph-only timeout increase.

## Recommended bounded repair approach

1. Identify the actual port-15075 listener, owning session, binary/configuration
   and dataset binding. Never kill an unknown listener to obtain a cold sample.
2. Add minimal, sanitized, request-correlated timing/query-count diagnostics if
   existing observations are insufficient. Measure context/Identity authorization,
   publication materialization, validation/hashing, visibility/dependencies, graph
   subreads and final reauthorization. Separate server latency from visible render.
3. Reproduce the same list, exact Document and graph; capture warm repeats. Measure
   cold startup only with permission to restart the identified owned host.
4. Remove demonstrated repeated expensive work. Consider operation-scoped reuse of
   immutable exact-publication material, bounded batch queries and reduced repeated
   loading before introducing parallel reads. These are candidate techniques, not
   approval for a particular cache or shared-contract change.
5. Preserve live authorization at required disclosure boundaries, exact version
   selection, all-contributor dependencies, cancellation and final graph checks.
   Immutable data reuse must not become cached access permission. Do not share an
   EF context concurrently, use unbounded caches/tasks or silently return stale data.
6. Test the changed behavior and repeat real browser measurements on the same
   publication. Report query/load-count changes and measured before/after results.

Do not merely raise the graph timeout, remove validation, suppress failures or
reduce authorization to make the test pass. A necessary limit change requires a
separate explanation based on measurements. Do not rewrite the whole read system.

## Retained data and runtime boundaries

- Target: `phone-15bfe12615eb`.
- Batch: `1a327657-6a55-4e65-ba06-6992f423d40e`.
- Original CL publication: `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`.
- Selection digest: `52fde3a3354bb00ce686d6eb4f10feb9279d0f2d9daea9a6b8473f51e48b43bd`.
- Database: `vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d`.
- Scan reports 77 files / 163 CL Documents. No independent count query was made
  for this handoff. Pin the exact publication, not latest or newly generated BL.

Read-only real-data performance qualification should not require an LLM call or
new dataset. Reuse applicable explicit approvals; obtain any missing host/database
authority before execution. Startup can apply migrations/seed roles: coordinate
that boundary, particularly while Business runs. No shared database deletion,
Prepare, rescan, CL replacement, permission mutation, competing migrations or extra
PostgreSQL container. Protect successful retained data. Keep runtime configuration,
credentials and environment-sensitive paths outside tracked configuration.

Do not change persistent PATH/global settings or stop unrelated processes. Record
only sanitized timings/IDs/counts, not secrets, Document bodies or raw responses.

## Completion evidence expected

- Reproducible bottleneck diagnosis, including which hypotheses were disproved.
- Small reviewed fix with affected automated tests; preserve revocation/denial,
  contributor completeness, immutable selection and cancellation behavior.
- List and exact Document warm-load measurements showing material improvement;
  agree a specific user-facing target after the stage baseline, rather than invent
  an existing performance requirement.
- Graph successfully loads the real bounded selection within its existing server
  budget, with correct partial/empty semantics, labels and exact Document links.
- Browser checks use an ordinary User and the same retained publication. No
  fabricated edges, provider answers or content.
- Exact commit/build, controlled versus runtime evidence, remaining limitations,
  and retained host/resource state. Do not claim IT Chat or Business-mode
  qualification solely from improved Explorer reads.

Earlier integration full SelfContained was 4,915/4,917: a process-PATH CodeGraph
case passed its unchanged focused replay; the strict OctoberGeneratedClReport
field-set mismatch remained. Those are historical results, not a current full-suite
baseline. Read `docs/agents/testing.md` and use `eng/test.ps1`; distinguish unrelated
known failures from newly introduced failures without weakening assertions.
