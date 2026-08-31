# Sprint Change Proposal: Story 12.5 Exact Owner Semantic Identity

**Date:** 2026-08-31
**Story:** 12.5 — Grounded LLM-Wiki Chat & August 30 Release Evidence
**Mode:** expedited
**Classification:** moderate, release-blocking Scan-strategy correction
**Status:** approved as `story-12-5-correct-course-exact-owner-semantic-identity-04`

## 1. Trigger and proven diagnosis

The accepted T037–T042 exact-profile seed correction works through Prepared Scan
and required Pass 1. The no-preseed PostgreSQL vertical reaches
`ReadyForResolution`; the consumer and provider extractors each return exactly
one current candidate. T043 then returns the accepted fail-closed `Refused`
outcome because the unsupported-observation owner cannot be found.

This is a semantic owner-identity mismatch, not a CodeGraph 1.5, JDT-LS,
selection, provider, authorization, revision, Pass 1, or Story 12.4 defect:

- `CodeUnit.CreatePreparedFileCodeUnit` stores the Repository-relative path in
  `CodeUnit.SymbolName` for every File Code Unit;
- small Java files take the whole-file path and do not call the existing
  JDT-LS top-level-symbol source;
- the unchanged Story 12.4 owner query requires both the exact file path and
  the fully qualified Java type symbol from the reviewed profile; and
- the retained run therefore has the correct path and Source Anchor but zero
  matching semantic owners.

The raw diagnosis is preserved in
`specs/014-grounded-llm-wiki-chat-release-evidence/evidence/diagnostics/exact-resolution-owner-binding-gap-01.md`.
The current checkpoint remains recovery evidence, not Track C completion.

## 2. Impact analysis and checklist

| Checklist area | Result |
| --- | --- |
| Trigger/context | Done — T043 exposed a plan-feasibility omission after the accepted matched-profile selection delta. |
| Epic impact | Story 12.5 remains achievable. No new Story, Epic, dependency, or sequencing change is required. Stories 12.2–12.4 remain accepted. |
| PRD impact | No requirement intent changes. The August overlay already requires the reviewed unsupported-observation source, exact JDT-LS evidence, current Source Anchors, and the exact Cross-Repository outcome. After approval, add only this proposal to the overlay authority list. |
| Story/spec impact | No acceptance-intent change. FR-081, FR-084, FR-085, SC-026, and SC-029 already require a current source-anchored owner and forbid production sample literals. `spec.md` remains unchanged unless plan review proves a contradiction. |
| Architecture impact | Action required in section 4.1: define the semantic symbol authority for the one matched reviewed Java owner even when it is a bounded whole file. |
| Plan/tasks impact | Return to `plan`; add one architecture decision, invalidate unchecked T043 onward, then regenerate only the affected tasks after accepted plan review and planned-change authorization. |
| UX impact | None. Chinese Scan/Chat UI behavior and all Track A/B behavior remain unchanged. |
| Data/schema impact | None. Existing `CodeUnit.SymbolName`, File Code Unit fingerprint, Manifest, Document, and Source Anchor columns are sufficient. No EF migration or model edit is allowed. |
| Security impact | The semantic symbol remains server-owned, profile-derived only after exact pair/ref/revision activation, validated against current revision-bound JDT-LS evidence, authorization-preserving, and fail-closed. |
| Option 1 — direct plan correction | **Recommended.** Bind the reviewed semantic owner to the existing File Code Unit through a current JDT-LS validation seam. Medium effort, medium risk. |
| Option 2 — change Story 12.4 lookup | Rejected. The lookup correctly requires exact semantic identity and is an accepted immutable dependency. |
| Option 3 — path-only or database repair | Rejected. Either weakens semantic ownership or fabricates release evidence. |
| Option 4 — remove exact outcome from MVP | Rejected. The exact Edge and honest Uncertainty are mandatory retained release value. |

## 3. Recommended bounded correction

Deepen only the already matched reviewed-profile path. Extend the existing
server-owned `IReviewedExactProfileSeedAuthority` result so an
unsupported-observation required source may carry its profile-owned fully
qualified Java owner symbol and selector line. No caller, request, provider,
LLM, environment variable, or production literal may supply either value.

For that exact required Java owner, `PreparedScanPairBuilder` shall call the
existing revision-bound JDT-LS document-symbol boundary even when the file fits
the whole-file bounds. It shall require exactly one current top-level type that:

1. belongs to the exact required Repository-relative path;
2. has an exact UTF-16 range containing the reviewed selector line; and
3. has a JDT-reported type name equal, ordinally, to the terminal type segment
   of the profile-owned fully qualified symbol.

The profile supplies the fully qualified semantic identity; JDT-LS proves that
the type currently exists at the bound Profiled Revision and owns the reviewed
line. Neither authority can succeed alone. Missing, duplicate, ambiguous,
out-of-range, stale, malformed, or mismatched evidence maps to the existing
matched-profile fail-closed preparation outcome before either Manifest
publishes.

The scheduled unit remains a `CodeUnitType.File` with the existing whole-file
or safe split byte/line range. For an oversized Java file, only the exact safe
segment containing the reviewed selector line receives the semantic owner
symbol; the other required segments retain path-valued identity. Every segment
continues to receive the accepted score 1,100 high-value classification.

The selected owner draft, File Code Unit entity, Prepared Scan replay, handoff,
Worker packet, resulting Source Anchor, and exact-resolution lookup shall all
carry the same semantic symbol. The semantic symbol must be included in a new
canonical File Code Unit identity version for only symbol-bound units. Existing
path-valued units continue to use the existing v1 identity bytes, preserving
neutral/nonmatching fingerprints. Replay reconstructs the applicable identity
from the persisted `SymbolName` and rejects any path/symbol/range/fingerprint
mismatch; it never silently attaches to a different unit.

## 4. Exact old-to-new authority edits after approval

### PRD August overlay

**Old:** the authority list ends with the exact-profile seed-selection proposal.

**New:** add this approved proposal to that list. The retained requirements do
not change: current JDT-LS evidence, Source Anchors, exact revision binding, and
the reviewed unsupported-observation outcome already require the correction.

### Epic 12 / Story 12.5 and Story 12.5 specification

**Old:** the matched reviewed profile retains every required File Code Unit and
must reach the exact outcome without direct repair.

**New:** no acceptance-criteria or specification edit. Interpret “required
unsupported-observation File Code Unit” through the existing canonical
`CodeUnit.SymbolName`: its profile-owned fully qualified symbol must be
validated against current JDT-LS top-level evidence and bound through the
current Source Anchor. This is a plan clarification, not new product scope.

### August architecture section 4.1

**Old:** bounded files become whole-file Code Units; only oversized Java files
invoke JDT-LS top-level ranges for safe splitting. Prepared File Code Units use
their Repository-relative path as `SymbolName`.

**New:** preserve that generic rule. Add one matched-profile exception: a
required Java unsupported-observation owner invokes current JDT-LS top-level
symbol evidence even when bounded, validates the profile-owned fully qualified
symbol and reviewed line, and binds that symbol to the owning whole-file or
safe-segment File Code Unit. Failure rejects matched preparation before atomic
publication. This exception does not schedule a class/method Code Unit or
change the existing bounds.

### Story 12.5 plan and tasks

Add `AD-PSI-7 — Matched reviewed semantic owner identity` with the authority,
ordering, canonical identity, replay, failure, and ownership rules above.
Regenerate unchecked work from T043 so honest RED precedes the coordinated
production GREEN, followed by the same PostgreSQL vertical, regression,
convergence, quality review, conditional E2E, and fresh real run. Preserve
completed T001–T042 and all their accepted evidence.

## 5. Ordering and transaction boundary

1. Complete the accepted initiation transaction and exact reviewed-profile
   activation exactly as today.
2. Acquire and verify the exact revision/inventory and complete the existing
   reviewed-path validation.
3. Build ordinary bounded candidates. For the matched semantic-owner source,
   obtain current JDT-LS document-symbol evidence and validate path, range,
   simple type name, and selector-line ownership.
4. Bind the validated fully qualified owner symbol to only the owning candidate
   and its canonical identity before CodeGraph expansion and deterministic
   selection.
5. Prove every required segment remains selected, then atomically publish the
   existing pair of Manifests and File Code Units.
6. Let unchanged Pass 1 create current Documents and Source Anchors from those
   units; only after terminal accepted Pass 1 may unchanged exact resolution
   consume the semantic owner.

No database transaction may remain open during Git, CodeGraph, JDT-LS, XML,
Worker, provider, Pass 1, exact resolution, Pass 2, Chat, or LLM work.

## 6. Proposed production ownership and test seams

### Production files

- `Vulgata.Core`: extend the reviewed required-source value, JDT symbol evidence
  value, Prepared File Code Unit draft/input, versioned canonical identity, and
  `CodeUnit.CreatePreparedFileCodeUnit` semantic-symbol input. No entity shape
  or database configuration change.
- `Vulgata.Infrastructure`: project JDT document-symbol names with their exact
  ranges; derive the owner binding from the unchanged profile catalog; persist,
  replay-validate, and rehydrate the symbol through the existing Prepared Scan
  repository/handoff source.
- `Vulgata.Web`: validate and assign the matched owner symbol in
  `PreparedScanPairBuilder` before CodeGraph expansion/selection/publication.
  No new endpoint or public input; no `Program.cs` edit is expected unless the
  accepted plan proves composition must change.

### Required behavioral RED/GREEN

1. A small matched Java owner must invoke the revision-bound JDT-LS symbol seam,
   persist the exact fully qualified owner symbol, and preserve its whole-file
   scheduling range.
2. An oversized matched owner must bind the symbol only to the exact safe segment
   containing the reviewed selector line.
3. Missing, duplicate, ambiguous, wrong-path, wrong-name, non-containing,
   malformed, stale, or unavailable JDT evidence must publish zero Manifests.
4. A neutral or nonmatching pair must make zero new JDT calls and retain byte-
   identical candidate, unit, package, audit, and Manifest fingerprints.
5. Identical authorized replay must return the same symbol-bound unit and
   canonical identity; a symbol/range/fingerprint mismatch must conflict.
6. The public disposable-PostgreSQL no-preseed vertical must reach the unchanged
   accepted exact decision/current `REFERENCES` Edge and honest Uncertainty with
   no row repair.
7. Existing CodeGraph 1.5/JDT-LS, Prepared Scan, Pass 1, Story 12.4, Track A/B,
   authorization, currentness, streaming, and security regressions must pass.

## 7. Explicit preserved and prohibited boundaries

- Preserve accepted Track A/B and T001–T042 source, behavior, tests, and evidence.
- Preserve the exact-profile resource, parser, activation tuple, score 1,100,
  20-per-Repository and 150-pair bounds.
- Preserve Story 12.4 contracts, resolver/store behavior, exact decision, Edge,
  GENERATED_FROM, and Flow Group authority.
- No EF migration, `VulgataDbContext`, configuration, or model-snapshot edit.
- No CGC, historical `RepositoryProfilingCoordinator`, `/scan-runs`, legacy v1
  result, generic Scan redesign, single/N-Repository path, or new public input.
- No source, Document, Source Anchor, Edge, decision, or Uncertainty pre-seed or
  repair; no fake Backup B release evidence.
- No Worker tool, prompt/schema, provider, Pass 1 policy, Pass 2, Chat, streaming,
  publication, cancellation, conversation, UI, or LLM behavior change.
- No run-04 database, remote LLM call, restart, screenshot, or release-success
  claim before the corrected technical gates accept.

## 8. Estimate, risk, and gate route

Definition/plan correction and independent review: 1–2 active hours. TDD and
focused verification: 4–8 active hours. Convergence, quality review, conditional
E2E, and checkpoint: 2–4 active hours. The fresh real Scan/Pass 1/Pass 2/Chat/
restart/screenshots run remains 6–12 elapsed hours depending on provider and
analysis latency.

Primary risks are canonical-identity compatibility and ambiguous JDT symbol
evidence. Both fail closed and are covered before a fresh release database is
created. The route after approval is:

correct architecture/plan and self-review → independent plan review → explicit
Scan planned-change authorization → task regeneration → independent definition
review → newly bound implementation approval → honest TDD → T043 and downstream
technical gates → fresh real run.

Approval of this proposal authorizes only the authority/plan correction above.
It does not authorize task generation, production implementation, external
services, database creation, or any prohibited scope.

## 9. Approval record

The user explicitly approved the proposal preimage with SHA-256
`cbfd779d145b3a2c1a67a28334626b1f2753aed04272d404333fb1d2a51545f6`
and authorized the bounded architecture and plan correction plus independent
plan review. The required post-review Scan planned-change authorization remains
pending; task generation and production implementation remain unauthorized.
