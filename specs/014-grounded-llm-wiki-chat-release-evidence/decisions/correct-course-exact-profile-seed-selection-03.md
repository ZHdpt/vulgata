# Story 12.5 exact-profile seed-selection correct-course decision 03

- Decision ID: `story-12-5-correct-course-exact-profile-seed-selection-03`
- Kind: approved bounded Sprint Change Proposal
- Story: `12-5-grounded-llm-wiki-chat-and-august-30-release-evidence`
- Source commit: `0ba7f10c00c26e10896006cbf1788f954ff737ee`
- Scope SHA-256: `1e461a95a26bc6505d0d9976d2d634dee308f1943ec5bf981995167ef526ffc6`
- Status: accepted
- Decided at: `2026-08-30T21:39:51.5612393Z`
- User authorization: `story-12-5-run-03-all-remaining-tasks-approval-02`

## Decision

The run-03 exact-resolution refusal is classified as a release-blocking Scan
selection-definition gap, not a CodeGraph 1.5, JDT-LS, provider, or Story 12.4
defect. The recommended in-scope option is auto-accepted under the user's
explicit authorization to execute all remaining Story 12.5 work without
further routine approval pauses.

One server-owned read-only adapter may derive required source paths from the
existing embedded reviewed `ExactResolutionProfile` only after the complete
two-Repository pair, full Git refs, and Profiled Revisions match exactly. The
matched required sources become mandatory high-value seeds within the existing
20-per-Repository and 150-pair bounds. A nonmatching pair receives no profile
effect; a fully matched but incomplete, unsafe, ambiguous, filtered, or
out-of-bound seed set fails before Manifest publication.

Run-03 remains immutable diagnostic evidence. A fresh guarded lowercase
`vulgata_story125_*` database and complete real release run remain required
after the correction passes all definition and technical gates.

## Bound authority set

- Sprint Change Proposal — `c5b31c6591f4b1400e18133e0b9b58926bedc1ec1a3df960f3243038ab7e9233`
- PRD August overlay — `e14783b0e8b80f7226f6f4875ddf02135d06523e83e3c82d0d9202e3424b571b`
- Epic 12 / Story 12.5 — `b5b90c4d8d82d0a69c7dd74df93312674902f170725bbe0ac71bfc4672db1706`
- August 30 architecture — `6fa9bed2bd90e0abad5333b05381638bcf471088480b00ec94229c96f173878c`
- Story 12.5 specification — `6da3e251b217ab8dd8190f84c4cf864b994be1e87090558a29e125f5648ec8b7`
- Run-03 diagnostic report — `a3e089010967a201c75e5f77029789b4bbb97a6788984089fa652ef284812fca`

The scope hash is SHA-256 over the sorted repository-relative path and file-hash
preimage for this six-file authority and evidence set.

## Preserved boundary

This decision authorizes continuation through plan, independent plan review,
task regeneration, independent definition review, implementation, convergence,
quality review, conditional E2E, fresh real release execution, and final
verification. It does not waive any technical gate and does not permit an EF
migration, `VulgataDbContext` or model-snapshot change, Story 12.4 contract
mutation, CGC or historical profiling restoration, caller-supplied profile or
path input, generic nonmatching-pair selection changes, fake evidence,
credential disclosure, destructive database action, or reuse or repair of
run-03 as passed release evidence.
