# Decision: bounded production correction blocking full-suite verification

## Confirmed defect

The existing PreparedScanIdentityBuilders.CreateSemanticFileCodeUnitIdentity method emits domain `vulgata/story-12.2/file-code-unit-identity/2.0` (PreparedScanContracts.cs:534). CanonicalEnvelope's closed domain registry does not contain it, so RequireDomain throws. Corrected controlled fixtures now reach this path and fail before publication. Both production files are unchanged from the pre-Story13.1 baseline bff540980800eb67254170a0c82e38678b63576d. Raw failure and provenance are in baseline-production-blocker-01.json.

## Proposed bounded approach

Prepare a new, current correction under the active governed workflow. Add only the already-emitted version2 domain to the closed registry if independent review confirms that contract. Preserve version1 bytes, deterministic identity behavior, and rejection of every other unknown domain. Add public-seam regression tests for semantic identity creation/validation, symbol binding, version1 golden compatibility, and unknown-domain rejection. Verify affected profile/identity tests, source regressions and the full suite.

Do not edit historical feature plans, activate Story12.5, alter source retention, change a profile, weaken publication/authorization assertions, or claim generic Scan completion. Any additional production defect is separately classified rather than silently folded in.

## Authority and approval boundary

Proposals02/03 authorize test maintenance and explicitly return genuine production defects for a separate decision. This proposal is a scope request for the bounded production correction, not permission to write production code immediately. If authorized, prepare/review the necessary current definition amendment, apply any planned-change approval trigger, regenerate affected tasks and independently review the delivery definition. Preserve the mandatory implementation approval after the accepted definition, followed by honest RED/GREEN, convergence, quality review, E2E and final verification.

Alternative: retain the verified Story13.1 work and test repairs, leave production untouched, and keep Story13.1 incomplete at its required full-suite gate. No waiver is proposed.
