# Story 18.2 Validation Guide

Use `spec.md`, `contracts/navigation-tool.md` and `data-model.md` as expected behavior. Execute the scenarios under the accepted task ownership, resource authorization and verification gates.

## Preconditions

1. Use the integrated, verified Story 18.1 `PreparedSourceApproval` and current-authority verifier; preserve its issuer/context/functions unchanged. Re-accept the corrected 18.2 plan and obtain the applicable planned-change authorization before tasks. Authorize and verify the 18.2-owned shared trusted-workspace checkpoint separately before 18.3 consumption.
2. Establish explicitly owned fixture directories, writable tool state and process resources. October application config files explicitly select the Java executable and JDT-LS installation/distribution; missing values fail closed rather than using the current Web user-profile fallback. Existing Java-plus-launcher-jar code is a candidate mechanism, not a qualification result for the historical `bin/jdtls.bat`. Only real JDT-LS tests load `JDT_LS_BIN` from their local `.env`; do not discover arbitrary `.env` files or infer qualification from an installed directory.
3. Confirm automatic Maven/Gradle import, build and dependency restoration are disabled. Establish an actual least-privilege subprocess profile with selected prepared source read-only, separate writable `-data`/configuration and unrelated host files inaccessible except declared Java/JDT runtime inputs. A path lease, startup flag or documentation statement is insufficient.

## Fixture-first qualification

1. Prepare a small owned Java Repository with a declaration and caller; note expected definition and reference locations. Issue the trusted workspace lease with exact 18.1 approval plus separate `JdtNavigation` purpose, then exercise the production Worker navigation interface.
2. Record actual definition and reference protocol outcomes and relative locations. Record observed classpath/capability limits and compare source before/after for writes. Initialization alone is not a pass.
3. Exercise missing verifier/tool authorizer, wrong tool purpose, foreign/traversing/symlink input and out-of-root returned URI; verify withheld locations and no outside-root disclosure. Revoke/change approval or make either verifier unavailable at issuance, during use and immediately before disclosure with multiple buffered requests. Verify every response releases zero payload and the closed lease cannot resume without new issuance. Exercise overlap, cancellation, timeout, readiness failure, failed navigation and owned cleanup without touching unrelated processes.
4. Confirm 18.1 file tools remain independently available under their own grant when JDT-LS is limited, and do not mark CodeGraph satisfied by that fallback.

## Prepared Repository follow-up

After the prepared registration/login Repository is actually supplied and access is authorized, repeat representative definition/reference navigation with its real grant and declared profile. Report fixture and prepared-Repository outcomes separately; neither proves universal Java completeness. If direct-root operation writes source, escapes the process profile or useful navigation is impossible under the restricted profile, stop and return a design finding for architecture review rather than copying or changing the input.

## Evidence limits

Controlled fakes verify tool seams and failure handling; a configured real JDT-LS run verifies this host/profile. Record exact source/profile and result classifications. The separate Story 18.3 shared-workspace checkpoint must prove its JDT-independent callable contract before authorized consumption; it does not qualify CodeGraph itself. Preserve static, controlled-test and real runtime evidence distinctions under `docs/agents/testing.md`. For Windows native traversal failures, apply `docs/project-lessons.md`: compare launcher permission modes and native status before changing containment code.
