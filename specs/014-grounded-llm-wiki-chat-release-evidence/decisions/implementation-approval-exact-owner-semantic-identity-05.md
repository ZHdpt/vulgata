# Story 12.5 Exact Owner Semantic Identity implementation approval 05

- Decision ID: `story-12-5-implementation-approval-exact-owner-semantic-identity-05`
- Gate: mandatory implementation approval rebind
- Decision: **accepted**
- Decided at: `2026-08-31T06:53:22.5220795Z`
- Accepted-definition scope SHA-256: `fe212d79108b1a30e5bfc557dcce58314fabc6316f1ef6556d435d708f6ac70a`
- Source checkpoint: `619682bb858614fa1cf7ea67ade692550cf4ee83`
- User authorization: explicit approval of the corrected Exact Owner Semantic Identity definition and T047-T054 implementation boundary

## Accepted definition

- Specification — `6da3e251b217ab8dd8190f84c4cf864b994be1e87090558a29e125f5648ec8b7`
- Plan — `fd7feea32f1e7a992e0b6e1fdfe0aabdd380968c842bb67bd3ee1794e4373d1d`
- Tasks — `5c595dfcf1ce37e6960626e07297077bda4f84991d7048644a69074ec2dd7e46`
- Independent plan review — `d724ba40f82d07f9e4883d29ddd88f2ab1390401a5733862310483a1fe2706a7`
- Independent definition review — `59d4619312162f78295d567a0d9e07a3dbad73e804f25785b4e16324a46e0454`
- Planned-change authorization — `b7e831e10648de1ac4560857e338468bf44ec78f7efac69bf9a5a2a7bb0cdb5d`

The accepted-definition scope hash is SHA-256 over the sorted
repository-relative path and file-hash preimage for those six artifacts.

## Authorized implementation boundary

This decision authorizes T047-T054 only and preserves the already observed
T043-T046 honest RED evidence. T047 is the first and sole coordinated
production GREEN. It includes the corrected, plan-required
`PreparedScanPairBuilder` ownership only for one per-build JDT observation
cache and matched semantic-owner validation/binding before existing package
construction.

Production changes remain limited to the exact Core, Infrastructure, Agents,
and Web internal paths enumerated by T047. `PreparedPass1JobRunner` may change
only inside `CreateDispatchRequest`. Internal Prepared Pass 1 changes may only
carry, reconstruct, and revalidate server-owned `SymbolName`; no
provider-visible contract or job policy may change.

## Excluded downstream work and preserved controls

T055-T066 remain unauthorized. Their separate external-authority and
prerequisite gate remains mandatory before a fresh release database,
application host, remote LLM, restart, screenshot, or release run.

This approval does not waive TDD, convergence, independent quality review,
conditional E2E, final verification, authorization, currentness, Source
Anchors, citation validation, streaming, accepted publication, or secret
protection. It does not permit an EF migration, `VulgataDbContext` or snapshot
change, Story 12.4 mutation, profile resource/parser change,
generic/nonmatching Scan drift, CGC/legacy restoration, public/provider
`SymbolName`, prompt/tool or result changes, Pass 1
scheduling/retry/job-state/failure-policy changes, Track A/B changes, external
service use, database creation outside the already completed disposable RED
fixture, or release evidence work.
