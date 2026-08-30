# Story 12.5 Prepared Scan initiation implementation approval 01

- Decision ID: `story-12-5-implementation-approval-prepared-scan-initiation-01`
- Kind: mandatory implementation approval
- Story: `12-5-grounded-llm-wiki-chat-and-august-30-release-evidence`
- Accepted definition commit: `cfdbd60b1bfb8521c497d84f07ee7628eb6bf99e`
- Planned-change scope SHA-256: `50958154329acbac8070ecec04f5b6c3527a6cc047129feac024c0e093eb1eab`
- Accepted-definition scope SHA-256: `f6a6e1d2365e72db62a713033b218597d96706810b12069e4417a9bcb0493a6b`
- Specification SHA-256: `469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0`
- Plan SHA-256: `ee13bddfcae94937831a6184f51e7f452bdc0eeec6b91b0b320ac5bed29f35ca`
- Tasks SHA-256: `1f6bad50ce1e8b415b7a0870d999a4ed3073a0e49944b397721a8b0bba47c7ed`
- Status: accepted
- Decided at: `2026-08-30T02:37:18.4173106Z`

## Authorized implementation boundary

The user authorizes T010 through T033 only for the accepted bounded
exactly-two-Repository Prepared Scan initiation correction. This includes its
public-seam tests, Core identity and store contracts, PostgreSQL atomic
create-or-replay implementation, exact-tuple preparation/recovery integration,
existing-test compatibility, server-owned runtime identity, fresh
authorization recheck, endpoint/DI composition, regression, convergence,
independent quality review, and implementation-accepted checkpoint.

Production writes must follow the accepted RED/GREEN order. Shared
`PreparedScanCoordinator`, `PreparedScanPairBuilder`, endpoint, and `Program.cs`
paths have one coordinated integration owner.

## Exclusions

This approval does not authorize T034 through T045, a fresh run-03 database or
external run, credential or source disclosure, CGC or legacy profiling,
`/scan-runs` repair, general/single/N-Repository Scan behavior, EF/model changes,
Story 12.4 changes, accepted Track A/B changes, later-stage behavior changes,
fake evidence, retained-database mutation, or any other prohibited scope.
