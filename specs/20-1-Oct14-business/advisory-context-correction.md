# Minimal BL prompt and scoped-context correction

User accepted the bounded correction and instructed: implement, then pause before
an actual scan. Starting checkpoint `782a1109161ed84bb084734806a196cc2a18da45`
was clean on assigned `oct14/business`. No skills or broad replanning are involved.

## Scope and compatibility

The generic synthesis prompt must distinguish declarations from activation, avoid
invented required fields, ordering and success conditions, and preserve uncertainty.
It must not embed repository-specific framework keywords or evaluation answers.
Gap summaries use named categories and explicit counts; generated prose must not
reinterpret category identifiers as counts. No new semantic-review execution stage,
tools, resolution policy, migrations or provider settings are introduced.

System is the existing project-level scope. The owner loader projects only selected
System and Repository entries from the immutable batch snapshot for repositories
in the exact corpus. Each entry must match its selected context reference, repository
and system binding, and an already retained contributor identity/content digest.
It does not append new contributors or change the immutable corpus. Newly authored
or edited context is outside this correction and cannot be silently injected.
Relevant retained System/Repository contributors must also have their matching
selected snapshots; deletion or deselection cannot silently become empty context.

FlowPlanningInput gains optional empty-default AdvisoryContext. The model packet
includes only entries relevant to that flow's member repositories, with named scope,
origin contributor and explicit AdvisoryNotSourceProof marker. Context is evidence
to interpret, not instructions or accepted CL that can be cited through Inputs.
The complete serialized context is bounded to 32 entries/64 KiB and remains within
the existing complete packet/input/output accounting. Overflow fails closed; no
silent truncation or permission widening. Empty-context historical inputs remain
supported. Complete authorization/dependencies and independent store revalidation
remain in force; existing publication content and identities are preserved.

Owned production files: synthesis policy/model, flow input contract, owner input
loader and embedded synthesis prompt. Tests remain in the Business-owned synthesis
and publication suites. Scan source execution, Chat, Explorer and Web composition
are untouched. The shared change is additive owner-projected input, not a new public
context-selection or publication API.

## Retained readiness observation

A read-only metadata inspection of the exact retained batch on 2026-10-12 found
two selected System entries (1080 UTF-8 bytes total), two selected Repository
entries (358 bytes), and two unselected Global slots (empty). This establishes
availability and size, not adequacy of framework explanations or real-model behavior.
No context text, receipt, publication or database schema was changed.

## Verification and stop

Focused tests must exercise actual model request serialization, named gap counts,
scope filtering, payload bounds, exact selected snapshots and contributor hashes,
tampering/authorization denial, and independent publication reload. Independent
review must accept the implemented boundary. Runtime generation and semantic
improvement remain unqualified until a separately authorized real attempt.

The loader's initial RED selected two cases: complete selected context was omitted,
and selected System context missing from the corpus ledger was incorrectly allowed.
Both failed as expected in
`TestResults/isolation-123ceb82f9334316b7b31129b34bd1bd/results.trx`.
The first implementation regression selected 208 cases: 207 passed and one new
test incorrectly compared ImmutableArray backing identity rather than contents.
That assertion was corrected without a production change. Independent review also
identified missing reverse completeness for deleted/deselected context; that
finding received separate RED/GREEN evidence before acceptance.
The completeness RED passed the four existing corruption cases and failed exactly
the deletion and applicability cases: loading incorrectly returned Success.
Evidence: `TestResults/isolation-f9e0b7bd3b4a417794628800cc93803b/results.trx`.
The independent reviewer accepted the corrected source with no remaining blockers:
reverse completeness rejects deletion/deselection while genuine historical empty
inputs remain supported. The review confirmed exact bindings/hashes, authorization,
limits, unchanged historical group identities and publication reload. Review does
not establish real-model semantic improvement.

Final current-source controlled verification passed **210/210**, zero failures or
skips, using the standard launcher:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~OctoberBusinessSynthesisTests|FullyQualifiedName~OctoberFlowPublicationTests|FullyQualifiedName~OctoberResolutionPersistenceTests|FullyQualifiedName~OctoberScanBusinessFinalizationTests'
```

Build completed with zero errors; analyzer warnings remain. Evidence:
`TestResults/isolation-188a9cada9734c4083ed68c80f49644b/results.trx`.
The selected SelfContained suite includes owned SQLite publication/revalidation,
controlled model request capture, and 16 new cases. PostgreSQL, provider, source,
E2E and whole-suite qualification are not claimed. No actual scan, provider call,
retained write, migration or publication replacement was performed.

Stop before Prepare, Generate, provider calls, retained-data writes, new context
selection, or rescanning. Commit only reviewed owned changes; no merge or push.
