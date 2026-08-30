# Story 12.5 Prepared Pass 1 identity-authority implementation approval 02

- Decision ID: `story-12-5-implementation-approval-prepared-pass1-identity-authority-02`
- Kind: mandatory implementation approval
- Story: `12-5-grounded-llm-wiki-chat-and-august-30-release-evidence`
- Accepted-definition scope SHA-256: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`
- Specification SHA-256: `469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0`
- Plan SHA-256: `6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8`
- Tasks SHA-256: `469956462085136a1d1a6acf1b59ab895da403e0de409631c3f10b3b8f90015b`
- Planned-change decision: `story-12-5-planned-change-prepared-pass1-identity-authority-02`
- Approved planned-change decision SHA-256: `7a9e3fc5fc4b9485cd7538e61f2b2153a81bcc8388a89935750d5a29dbf931b2`
- Plan-review verdict SHA-256: `6138486b372f515d4488445ab436600cd13d496845b85b71e5ca05ac6ae4f680`
- Definition-review verdict SHA-256: `88076e5f3a3b6f54e7ddd767ba292c196727d410288de6cb14133debe1e514e7`
- Status: pending explicit user approval

## Scope-hash preimage

The scope hash is SHA-256 over this exact UTF-8 string without a trailing
newline:

`story=12-5-grounded-llm-wiki-chat-and-august-30-release-evidence|decision=story-12-5-implementation-approval-prepared-pass1-identity-authority-02|spec=469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0|plan=6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8|tasks=469956462085136a1d1a6acf1b59ab895da403e0de409631c3f10b3b8f90015b|planned-change=story-12-5-planned-change-prepared-pass1-identity-authority-02@7a9e3fc5fc4b9485cd7538e61f2b2153a81bcc8388a89935750d5a29dbf931b2|plan-review=6138486b372f515d4488445ab436600cd13d496845b85b71e5ca05ac6ae4f680|definition-review=88076e5f3a3b6f54e7ddd767ba292c196727d410288de6cb14133debe1e514e7|tasks=T025-T033`

## Decision requested

Approve implementation of T025 through T033 only for the accepted bounded
Prepared Pass 1 identity-authority correction. Production and behavioral-test
writes must begin with T025's honest RED and follow the accepted task order.

The implementation will:

- add the dependency-free Core `PreparedPass1ContractIdentity` value and
  `IPreparedPass1ContractIdentitySource` port;
- add the sole Agents `EmbeddedPreparedPass1ContractIdentitySource`, binding
  exact embedded v2 prompt bytes to a lowercase SHA-256 identity and
  `code-logic/file-v2`;
- replace only the initiation coordinator's legacy recovery-identity dependency;
- remove direct EF Core/`VulgataDbContext` access from the Web authorization
  rechecker through existing materialized Core repository seams backed by
  Infrastructure, while retaining Web `UserManager` role orchestration;
- preserve and characterize `WorkerAgentDispatchIdentitySource` unchanged;
- complete focused verification, regression, convergence, independent quality
  review and the implementation-accepted checkpoint before any run-03 work.

## Options and recommendation

1. **Approve** — recommended. It permits the minimal TDD correction that closes
   the proven identity-authority and layering defects.
2. **Do not approve** — leaves the repository at the accepted-definition
   checkpoint with the public Prepared Scan entrance still failing closed.

## Exclusions and remaining gates

This decision does not authorize T034-T045, a run-03 database or external run,
an EF migration, `VulgataDbContext`/model-snapshot change, Story 12.4 or Track
A/B mutation, CGC or historical coordinator registration, `/scan-runs` repair,
general/single/N-Repository Scan behavior, a new Agent tool, provider/dispatch/
retry/parsing/persistence behavior, later-stage execution during initiation,
credential/source disclosure, fake evidence, retained-database mutation, or Git
history operations.

Approval does not waive honest RED/GREEN, convergence, independent quality
review, conditional E2E, pinned verification, database isolation, security or
the separate release-run prerequisites.
