# Story 12.5 run-03 task-group approval 01

- Decision ID: `story-12-5-run-03-task-group-approval-01`
- Kind: bounded implementation/external-execution approval
- Story: `12-5-grounded-llm-wiki-chat-and-august-30-release-evidence`
- Scope SHA-256: `28311e4f690b7b3e529184ffbcc147df42dde3a7088e88aa90a6292767f61b6e`
- Accepted-definition scope: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`
- Production candidate: `9413558a42b39f8f2a9ce41a717a0b37c9f72a98`
- T033 checkpoint: `be8f51907b98335ec8ffc73a823aff27651cbe32`
- Current tasks SHA-256: `87b751ce78d29f744acb37227db4abeaf8d1b893914217b5d5bac40282980507`
- Status: accepted
- Decided at: `2026-08-30T13:59:34.1508253Z`

## User authorization

The user said, "Approve. Proceed to next task group" immediately after the
orchestrator reported that T025-T033 were accepted and that T034-T045/run-03
required new authorization. This decision interprets that instruction
conservatively as approval for the next dependency group only: T034-T035,
fresh run-03 preflight and creation/bootstrap of one new isolated database.

## Scope-hash preimage

SHA-256 is over this exact UTF-8 string without a trailing newline:

`story=12-5-grounded-llm-wiki-chat-and-august-30-release-evidence|decision=story-12-5-run-03-task-group-approval-01|accepted-scope=f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1|candidate=9413558a42b39f8f2a9ce41a717a0b37c9f72a98|checkpoint=be8f51907b98335ec8ffc73a823aff27651cbe32|tasks=87b751ce78d29f744acb37227db4abeaf8d1b893914217b5d5bac40282980507|tasks=T034-T035`

## Authorized actions

- truthfully record run-03 readiness or a sanitized blocker;
- pin the exact real Repository revisions and non-secret tool/provider identity;
- generate and guard one fresh lowercase `vulgata_story125_*` database name;
- prove absence and create only that database through the approved local
  PostgreSQL administrator mechanism;
- start the accepted application candidate only as needed to apply existing
  migrations and bootstrap one Administrator, one System, one provider
  assignment and exactly two ordinary Repository records through production
  behavior;
- record sanitized commands, exits, ownership and bounded identifiers.

## Exclusions and next gate

This does not authorize T036-T045, the real Prepared Scan/Pass 1/Pass 2 run,
source-bearing provider calls, twelve-question evaluation, restart,
screenshots, E2E, final verification or Story completion. It does not permit
database deletion, retained-database mutation, secrets disclosure, fake Backup
B evidence, an EF migration, DbContext/model-snapshot change, Story 12.4
mutation, CGC/legacy profiling restoration, general Scan changes, branch or
history operations, or any production correction outside the accepted plan.

T035 completion must leave a clean checkpoint and return for explicit
authorization before T036.
