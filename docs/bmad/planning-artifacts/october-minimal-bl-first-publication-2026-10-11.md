# Minimal BL plan: first useful publication

Date: 2026-10-11. Owner: OCT-BUSINESS (`01a10fb4-7397-7c53-8df9-f5e28858fdbf`).
This is a narrow implementation handoff, not a replacement architecture or runtime grant.

## Goal and deliberate cut

Generate one useful Chinese Business Logic Document from one sufficiently rich,
explicitly selected accepted CL root. Publish it through the existing Business
validation/store and verify ordinary Business-reader access. Preserve all original
CL and its publication. Cross-repository completeness is deferred for this first
milestone, not declared achieved or removed from the eventual product.

No new retrieval tools, role classifier, resolution-policy change, source access,
Scan rerun, schema/migration, UI work or alternate import/publication route.
Keep the successful CL implementation unchanged. Do not start the larger
tool-assisted Business redesign as a prerequisite.

## Verified starting point

- Main observed clean at `f06b85bf9660ba4a9cb8c4545413c00428d74efe`.
- Business last observed at `fde78bdc5ed49c7a9109ab07a357c5023084006c`;
  verify current state and preserve newer work rather than resetting to this hash.
- Retained evidence: 77 accepted files / 163 CL versions; batch
  `1a327657-6a55-4e65-ba06-6992f423d40e`, CL publication
  `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`, corpus
  `0ea50f38-2441-4ec0-b31c-313acd0c5f55`.
- Business preflight measured 2,419,869 bytes of complete flow input against a
  1 MiB synthesis bound; 355 observations produce zero inquiries and 355
  NotEvaluated dispositions under the unchanged resolution policy.
- Zero inquiries permits root-only flow planning and BL publication today. It
  does not prove successful resolution. Recheck this assumption before execution;
  do not suppress new inquiries or manufacture resolution outcomes to fit the plan.

Read the Business checkout's `specs/20-1-Oct14-business/retained-cl-readiness-2026-10-11.md`,
the existing-CL route and Scan lessons linked below. Keep credentials and local
runtime configuration outside tracked files.

## Implementation sequence

1. **Choose one root, without new source analysis.** Inspect accepted login and
   registration root CL under existing read authority. Choose one with meaningful
   conditions, branches and outcomes. If it is too shallow, choose another accepted
   root; do not invent facts or expand this milestone into retrieval. Initially
   generate one group only. Revalidate exact publication/corpus bindings and that
   no unmatched Business admission blocks a new attempt.

2. **Project a small model packet; retain the complete owner input.** Include the
   root/objective and complete relevant accepted member claims with exact Document
   Version, SectionId, ClaimGroupId, text, qualification and limitations. Include
   applicable accepted relationships/frontiers and a truthful bounded gap summary.
   For the currently observed zero-edge case, membership is the root document.
   Do not serialize the complete corpus or conservative dependency ledger into
   the prompt. Update the prompt's input paths. Reject references not supplied in
   the packet. Model input selection must not change the frozen corpus identity,
   flow membership or coverage claim.

3. **Correct the separate limits.** Keep finite, measured bounds for owner input,
   full gap ledger and validation result; these differ from model-message,
   proposal and output bounds. Check all layers, not just the executor's first
   size test. Preserve the full lineage, authorization contributors, coverage,
   gaps and independent store revalidation. Do not simply remove limits or raise
   every limit globally. Use a smaller root/packet rather than silently truncating
   evidence to fit.

4. **Reuse proven CL reliability behavior in synthesis only.** Preserve endpoint-
   specific request compatibility; use the approved 393,216 maximum output setting
   as a ceiling, not a target. Reserve estimated input plus output separately and
   settle every physical attempt against finite cumulative run limits. Port only
   the final-JSON correction behavior: at most three regenerations after initial;
   first retains configured reasoning, second disables thinking, third also prunes
   rejected proposals/obsolete feedback. Preserve original instructions, evidence
   and required reasoning replay; use request-local overrides. No tools, fresh
   investigation, silent JSON repair or stacked outer retry allowance. Correct
   syntax/schema failures only; do not retry away evidence/authorization failures,
   Length, exhausted budgets or indeterminate outcomes. Use Business-specific
   feedback. Recalculate the deadline/request/token envelope; do not inherit the
   old 180-second/32,768 reservation blindly. No CL executor refactor is required.

5. **Verify, then execute once within explicit runtime authority.** Focused tests
   must cover a large complete owner input with a small request, exact supplied
   references/qualifications, preserved full gaps/dependencies, request accounting,
   staged corrections and original-CL immutability. Obtain one focused independent
   review. Keep unrelated known suite failures visible rather than fixing them.
   Report exact root, bindings, request/token/time caps and retained-write scope.
   Use prior runtime authorization only if it demonstrably covers these exact
   operations; otherwise ask one consolidated approval before provider calls,
   retained DB writes or host startup. Never clear unmatched receipts or take over
   uncertain attempts. No repeated whole-slice trials.

## Done and reporting

- Real provider output explains useful conditions, steps, branches/outcomes and
  limits; inspect content quality as well as structural validity.
- Normal validation/store accepts a separate immutable BL publication. The exact
  original CL content, membership and publication remain unchanged.
- An authorized ordinary non-management Business reader retrieves the new BL;
  operator-only API success is not this qualification.
- Record provider usage, receipt settlement, exact publication and reader result.
  Runtime `Published` alone does not establish resolution completeness. Say:
  **"Local-flow BL published; cross-repository behavior remains unconfirmed."**
- Chat/graph/browser presentation qualification remains separate. After success,
  report before broadening root count, tools or resolution behavior.

Target one focused half-day session, not a guarantee. At four hours, report whether
focused verification and the first authorized real attempt have been reached.
Separate an approval wait from implementation delay. If blocked, identify the
smallest concrete blocker; do not automatically expand into the earlier redesign.

## Ownership and source pointers

OCT-BUSINESS owns this correction in its assigned checkout. Start with
`OctoberBusinessSynthesisExecutor.cs`, `OctoberBusinessSynthesisPolicy.cs`,
`Prompts/OctoberBusiness/Synthesis.md` and focused tests. Keep persistence semantics
unchanged. If Generator wiring must change, identify exact files first and obtain
the existing owner's coordination; do not alter shared Scan contracts silently.
No automatic integration, push, new management role or global environment change.

- [Integrated existing-CL boundary](october-scan-business-integration-2026-10-11.md)
- [Existing-CL continuation](../../../specs/20-1-Oct14-scan/business-from-existing-cl.md)
- [Successful Scan handoff and provider lessons](../../../specs/20-1-Oct14-scan/handoff-business-generation.md)
- [Current synthesis executor](../../../src/dotnet/Vulgata.Agents/OctoberBusiness/OctoberBusinessSynthesisExecutor.cs)
- [Current validation policy](../../../src/dotnet/Vulgata.Core/DomainServices/OctoberBusinessSynthesisPolicy.cs)
- [Planner and root membership](../../../src/dotnet/Vulgata.Core/DomainServices/OctoberFlowPlanner.cs)
- [Canonical BL publication](../../../src/dotnet/Vulgata.Infrastructure/Data/October/Knowledge/OctoberFlowPublicationStore.cs)
