# Story 18.2 Future Validation Guide

This is a plan for later owned verification, not a command to start processes in the present plan-only run. Use `spec.md`, `contracts/navigation-tool.md` and `data-model.md` as expected behavior.

## Preconditions

1. Integrate and verify Story 18.1; reconcile F0 grant, location, authorization and failure semantics. Rerun affected definition/review/authorization gates before tasks or implementation.
2. Establish explicitly owned fixture directories, tool state and process resources; identify the configured local Java/JDT-LS installation and exact restricted settings. Keep source separate from disposable `-data`/configuration. No supplied real Repository is assumed.
3. Confirm auto Maven/Gradle import, build and dependency restoration are disabled. A startup flag or documentation statement alone is insufficient; inspect effective initialization behavior and tool effects.

## Fixture-first qualification

1. Prepare a small owned Java Repository with a declaration and caller; note expected definition and reference locations. Exercise the production navigation interface with its application-selected grant.
2. Record actual definition and reference protocol outcomes and relative locations. Record observed classpath/capability limits and compare source before/after for writes. Initialization alone is not a pass.
3. Exercise foreign/traversing/symlink input and out-of-root returned URI; verify withheld locations and no outside-root disclosure. Exercise overlap, cancellation, timeout, readiness failure, failed navigation and owned process/state cleanup without touching unrelated processes.
4. Confirm 18.1 file tools remain independently available under their own grant when JDT-LS is limited, and do not mark CodeGraph satisfied by that fallback.

## Prepared Repository follow-up

After the user-prepared Repository is actually supplied and access is authorized, repeat representative definition/reference navigation with its real grant and declared profile. Report fixture and prepared-Repository outcomes separately; neither proves universal Java completeness. If direct-root operation writes source or useful navigation is impossible under the restricted profile, stop and return a design finding for architecture review rather than copying or changing the input.

## Evidence limits

Controlled fakes verify tool seams and failure handling; a configured real JDT-LS run verifies this host/profile. Record exact source/profile and result classifications. Preserve the distinction between static inspection, controlled tests and real runtime results under `docs/agents/testing.md`.
