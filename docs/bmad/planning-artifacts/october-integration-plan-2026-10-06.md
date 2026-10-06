---
title: October first deliverable - integration and presentation readiness
date: 2026-10-06
status: planning-input
scope: four-feature-integration
implementation_authorized: false
---

# Integrate the four first deliverables

## Current integration checkpoint — October 6

The user assigned OCT-Integrator to import Business's initial checkpoint and
prepare the other lanes for Wave 2. This authorizes the bounded integration and
consumer-conformance work below, not the full later feature implementations or
external runtime execution. The original planning-only front matter continues
to describe the broader plan, not this separately authorized checkpoint.

- Imported candidate: `07c3fe4463e3228a6982fd106b58148ca87974bd` plus handoff
  `7a189c34aec625202f74846d4907ded787ccbdca`.
- Integration merge: `3bcd815b09cfbac7418d6644a2a75816e0788ec0` on `oct14/main`.
- Business T001–T005 are complete. T006 remains pending actual Scan, Chat and
  Explore consumer acknowledgements; a merged candidate is not yet accepted K.
- Main current-source replay: `eng/test.ps1 -Filter
  'FullyQualifiedName~Vulgata.Tests.October.Knowledge'`, 50 selected/passed,
  zero failed/skipped, policy passed. Evidence:
  `TestResults/isolation-8e5356d9c6724a80821cccc36eabb4fc/results.trx`.
  This is controlled contract/fixture evidence, not PostgreSQL, native tools,
  providers, Web startup or full-suite qualification.
- Scan is preparing T001–T004, Explore T001–T003 and Chat its K0 conformance.
  Each imports the exact integration commit in its own worktree and preserves
  its accepted planning artifacts. Each pauses before broader implementation.
- Chat's integration-owned W0 Web composition seam is separate from K0 and is
  not yet delivered. It must not block the other consumers' contract acceptance
  or be silently marked complete by passing fixture tests.

Scan's import-only handoff `e62532d624400d547e7f7a113f9d04113189f3cb`
records T001 complete, T002–T004 incomplete and zero consumer tests executed.
Its approval layer rejected test authoring because an earlier direct user
instruction stopped that lane before implementation. The coordinator did not
bypass that rejection. The user must explicitly supersede that stop for the
bounded conformance tasks in the Scan chat; this is not a failed contract test.
Until its acknowledgement is available, T006 and accepted K remain incomplete.
Chat and Explore's bounded preparation dispatches are still in progress.

Only Business owns shared Knowledge types and October schema/migrations.
Integration serializes shared composition and test-inventory merges; consumers
may submit exact inventory additions with their lane-local probes. Missing owner
contracts are resolved with that owner, never through parallel DTO/policy copies.
No provider, native-tool, database or persistent-host execution is granted here.

Reserve October 9-10 for this phase, with earlier merges on October 6-8. One
integration owner is accountable; each feature owner fixes defects in its own
area. This phase combines the [four briefs](october-delivery-plan-2026-09-30.md),
including the useful 18.12 outcomes, without introducing a fifth broad feature.

## Baseline and early merge order

Use `oct14/main` at `57fad124bb0904bf51788e85f4823271291fcfe1` as the inspected
starting point. Preserve and commit the shared planning changes when instructed
before workers branch from the new planning baseline. No merges are executed by
this plan. Do not rerun or reopen the old tool workflows as an integration prerequisite.

1. Merge shared Knowledge interfaces, examples and minimum accepted-file/read
   implementation from Business. Its schema owner serializes October migrations.
2. Merge Explore's minimum reader and Scan's one-real-file route. Prove an actual
   source file produces accepted CL that the ordinary IT reader can open.
3. Merge Business's relationship/BL publication and Chat's event/tool loop. Integrate
   the graph when its bounded view is ready; neither consumer waits for the other.
4. Combine all first-deliverable branches by October 9, then exercise and repair
   the actual generation-to-Web scenario. Keep historical evidence distinct from
   results produced by this combined executable.

Consumers may compile/test against the shared labelled fixture before producer
implementation lands. A merge brings the real service into that same contract;
parallel copies of Knowledge schemas/read rules are not acceptable substitutes.

## Runtime composition to implement

The integration owner owns cross-module DI and Web registration changes; Scan
owns the generator run command and feature-local implementation stays with its owner.

- **Generator:** extend the existing `Prepare`/`Select` executable with a run path
  that holds its authenticated operator session, resolves approved roots/providers,
  analyzes admitted files and calls fixed-corpus Business finalization. Existing
  setup commands dispose their sessions and do not return ready-to-run tool contexts.
- **Web:** add an explicit wiki-read composition using the frozen `DefaultConnection`
  and persisted batch/publication identity. Ordinary Web startup and administration
  remain available without a generated wiki. Missing/mismatched data yields an
  unavailable reader/Chat/graph result, not an older dataset. Web must not require
  generator credentials, restore its former authority routes or migrate October.
- **Authority:** Business supplies live ordinary-reader authorization through real
  Identity/System/Repository checks. Do not make every reader authenticate as the
  operator stored by `IOctoberBatchSelector`.
- **Providers:** connect actual Worker/Chat clients to the explicit selected profiles.
  `OctoberProviderBinder` is configuration validation, not an LLM client. Declare
  each host's configuration source; do not assume generator secrets were persisted
  for Web, silently mix sources or require an unrelated provider administration change.
- **Tools:** Scan supplies one qualified composition of approved root binding,
  bounded CodeGraph/JDT lifetimes, ordinary file reads and tool-state exclusion.
  The separate native fixture passes do not establish this combined behavior.

Use owned feature test databases/ports while developing. The integration owner
controls the final dataset and schema application window. Keep the selected prepared
sources stable and provider credentials out of source control and evidence.

## What must pass together

| Check | Required observation and owner |
| --- | --- |
| Real input to Knowledge | Scan/Business: one command reaches real analysis, atomic acceptance, bounded resolution, useful BL and explicit publication. No dummy ScanRun, fabricated Git ID or raw-row import. |
| Tool duration and coexistence | Scan: multiple file jobs can use the approved source and tool session policy; expiry, cancellation, existing `.codegraph` and index/read interaction behave explicitly. Source/tool-state remains separated. |
| Storage and selection | Business: complete file acceptance and BL dependencies are coherent; replay does not duplicate or regenerate; failed drafts are invisible; a wrong database/batch/publication cannot fall back. |
| Actual Chat | Chat: at least one real tool round, a follow-up requiring wiki content, visible text before provider completion and a saved result with exact read-document references. Include an honest unknown and interrupted/error outcome. |
| Shared reading and graph | Explore: Chat references and graph nodes open the same exact Documents from the selected publication. Edges retain meaning/direction; limited coverage is visible. |
| Access boundary | Business with consumers: ordinary permitted user succeeds; Business sees BL only; denied/revoked contributors cannot leak content, titles, counts, footer entries or graph metadata. |
| Useful content | All owners with source expectations: inspect key business rules, branches/effects, the actual caller-provider relationship and unresolved facts. Structural validity alone does not establish usefulness. |

Begin with one small real cross-Repository case; then generate the selected
registration/login source slice. Record actual coverage, tool/model/store waits,
duration and known limitations. Use the existing test launcher/suite separation;
one integrated build and SelfContained run after the final combined corrections
plus the affected PostgreSQL/native/provider/UI checks are sufficient for their
claimed boundaries. Additional repeats need a changed behavior or unresolved defect.

## Freeze and handoff

By October 10 evening, select the passing code checkpoint and published dataset.
Record a short start/stop/configuration/runbook, schema version, supported source
scope and known limits. Preserve the data and required keys without exposing secrets.
Capture a recording from this build; screenshots, live demonstrations and slides
must agree about implemented behavior. Keep an offline playback copy for presentation.

Reserve October 11-13 for the ten-minute narrative, source-to-wiki explanation,
Business questions, graph/reader demonstration and rehearsal. If the connected
scenario is incomplete, report the exact remaining gap and adjust what is shown;
do not fill it with invented generation evidence. No new platform work enters this phase.
