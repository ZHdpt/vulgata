# Story 12.5 exact-profile seed implementation preflight 01

## Outcome

`PASS` — T036 permits the first current behavioral RED. No production or test
file was changed during this preflight.

## Repository identity

- Branch: `demo0831/story-12-5-trackC`
- Observed HEAD: `705657c9c4fbea26724930f206fd880277038cc0`
- Corrected specification checkpoint `7fc5bd1b8b787d5ff205378abb9e6a464bf00eb5`: ancestral
- Story 12.4 dependency `4798b97d9f23aef587829175df7ab7ebc3daf995`: ancestral
- Index: empty
- Porcelain state: only user-owned `.env.example` modified
- `.env.example`: read-only external prerequisite input; excluded from every
  workflow write, stage, diff, and commit manifest

## Accepted definition binding

- Specification: `6da3e251b217ab8dd8190f84c4cf864b994be1e87090558a29e125f5648ec8b7`
- Plan: `35766983a07db621efbdef3fa45934b2f66d86514d15deb490b5eca83ca75c43`
- Tasks: `6ba9c8ed9336d3526b1d842a527e42381121be6adf1dddd936c4a529aebee041`
- Independent plan review: `31c848cfdc4e5522b57efb4c9673b13f16569d49d2f38891e810cf4cca2fb49a`, `ACCEPT`
- Independent definition review: `5d94d387ce348de33289395d67b9f5708fc1488ddc637715417f0b0d78e4ac9b`, `ACCEPT`
- Planned-change decision: `f04c16379e64a2ac6ef886288ef184140b571edcdfcd781d4abcae07a9b90274`, accepted
- Implementation decision: `b918b849d175bb82e81a754fa35f28ca6aabfe1674e4b04692d645aa0a0b8378`, accepted for T036-T060

## Immutable historical boundary

Run-03 database `vulgata_story125_b527027ad0dc4e359070f26498817c3c`
and its report remain diagnostic evidence only. They may be inspected read-only
but may not be repaired, migrated, restarted, deleted, or reused for release
success. A fresh run-04 database is required only after T048 accepts the
technical correction.

## Exclusive ownership for the current TDD group

- T037 test owner: reviewed authority and pure planner tests only.
- T038 test owner: builder, endpoint, composition, literal, and boundary tests
  only.
- T039 test owner: new no-preseed PostgreSQL vertical test and its test fixture
  additions only.
- T040 production owner: the new Core seed contract, the closed planner evidence
  value/table, the Infrastructure adapter, `PreparedScanPairBuilder`, and one
  coordinated `Program.cs` registration only.
- Active root: workflow state, evidence, integration, staging, and commits.

No concurrent owner may edit `VulgataDbContext`, configurations, migrations,
model snapshot, Story 12.4, `ExactResolutionProfile`/catalog/resource/parser,
`PreparedPass1Store`, Pass 1 policy, generic nonmatching selection, caller DTOs,
CGC/legacy profiling, or the retained run-03 database.

## Next action

Execute T037-T039 test-first and observe honest failure at the missing public
authority/planner, builder/composition, and no-preseed PostgreSQL seams before
the coordinated T040 production write.
