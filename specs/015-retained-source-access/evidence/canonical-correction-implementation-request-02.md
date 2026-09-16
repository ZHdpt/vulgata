# Implementation approval: bounded canonical compatibility correction

Independent definition review06 accepted the current spec, plan and tasks with no findings. This is the mandatory production implementation decision, distinct from the approved definition/review scope.

## Exact requested authorization

Execute T044–T049 and resume pending T041–T043 under the active Story13.1 workflow. Observe a public Core regression RED, then add exactly `vulgata/story-12.2/file-code-unit-identity/2.0` to the closed registry in `src/dotnet/Vulgata.Core/DomainServices/CanonicalEnvelope.cs`. The existing producer already emits that domain. Preserve version1.0 golden bytes and deterministic identities, symbol validation/binding, envelope grammar and rejection of all other unknown domains. No production profile, schema, source-retention, tool or Scan-strategy change is authorized.

Run focused Core regressions, the two affected profile fixtures, retained-source regressions and the owned full suite. Continue through convergence, independent quality review, E2E decision and pinned final verification. Any newly discovered out-of-scope production defect returns for classification; this is not a completion or verification waiver.

The alternative is to leave production unchanged and retain the incomplete Story13.1 checkpoint at this gate. Recommend implementation of the bounded correction because it repairs the confirmed producer/validator mismatch without broadening domain matching.

## Accepted scope binding

- Review: `specs/015-retained-source-access/evidence/definition-review-06.json`
- Spec: `specs/015-retained-source-access/spec.md`, CP-FR-001–004 and CP-SC-001–004
- Plan: `specs/015-retained-source-access/plan.md`, closed canonical compatibility correction
- Tasks: `specs/015-retained-source-access/tasks.md`, T044–T049
- Scope SHA256: `796eee971fc5f9b1ff003468602bd7ddab3a2356d2e9fce02a2445e671a327fa`

The prior original Story13.1 implementation approval remains valid for its original scope; it did not authorize this newly added Core correction. The run-bmad-sprint-loop skill requires: "After the complete definition is independently accepted, pause once at implementation-approval before the first production write."
