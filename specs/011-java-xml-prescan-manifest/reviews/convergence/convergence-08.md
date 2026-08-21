# Story 12.2 Convergence — Iteration 8

- Pinned implementation checkpoint: `2a8723b0ce7aaae72361ae5fa4bb9bd714802c3e`
- Branch: `demo0831/story-12-2`
- Full base: `29e889badc00db671fcabf80f50918e15381052c`
- Correction diff (`d6eeb6bf99cf75d03ec3a8b687f1cac90c6d11f3..2a8723b0ce7aaae72361ae5fa4bb9bd714802c3e`, UTF-8 `git diff --binary`): `91a95d866388eaf0368de009ca7bbaff466cdea321fa5a9e03c33c0b636c8c10`
- Accepted definition boundary: `e997c182a466694bc5eab405bb68fc7396edee53ce7559f443bcbb2e7beaefb6`
- Attempt: `8`
- Outcome: `converged`
- Tasks hash before: `aab3144ff425d56fa2e4509afde51e6b0c120e5c953d1cb218e87c56c6cecb12`
- Tasks hash after: `aab3144ff425d56fa2e4509afde51e6b0c120e5c953d1cb218e87c56c6cecb12`
- Appended task IDs: none; `tasks.md` was left byte-for-byte unchanged.
- `git diff --check` passed for the pinned correction and this report. The only
  working-tree change outside this report is the root-owned
  `workflow-state.json` binding edit; it was not modified here.

## Prerequisites, hooks, and authority

- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
  returned `specs/011-java-xml-prescan-manifest` with `research.md`,
  `data-model.md`, `contracts/`, `quickstart.md`, and `tasks.md`.
- `.specify/extensions.yml` has no executable `hooks.before_converge` or
  `hooks.after_converge` hook; no hook ran.
- The ratified Constitution was checked across Principles I–V. No MUST
  violation was found. The repository instructions, ubiquitous language,
  context/ADR boundary, accepted decisions, all four contracts, research,
  data model, quickstart, implementation evidence, verification checklist,
  convergence reports 01–07, and quality reviews 01–02 were inspected.
- The approved August host-runtime architecture remains intact: Vulgata,
  CodeGraph `@colbymchenry/codegraph@1.5.0`, Java 21, and JDT LS
  `jdt-language-server-1.61.0-202608141332` over stdio run on the Windows
  host; PostgreSQL 17 is the only required Docker service; CodeGraphContext
  remains outside the production boundary.
- Story 12.3 Worker execution, `navigate_code`, Scan Job Execution/dispatch,
  Document generation, final Edges/Uncertainties, Cross-Repository Resolution,
  Pass 2, Chat/retrieval, and Backup B remain excluded.

## Pinned artifact hashes

| Artifact | SHA-256 |
| --- | --- |
| `spec.md` | `c15d03b2b10e4eaa452a59e3b96acafdbb04160de556ca93c28380117a085ad3` |
| `plan.md` | `2a9373661e935918ab6b55a4d83903e4ba92ba7e9fb80534a616d6583cb4b318` |
| `tasks.md` | `aab3144ff425d56fa2e4509afde51e6b0c120e5c953d1cb218e87c56c6cecb12` |
| `research.md` | `617304112b3b744a4f393ffccddd7859a37faca59db056680d0eda300954ac3c` |
| `data-model.md` | `1a648ab2a7c8802d2d9ca5c279476371685890240cc9adfb0143cbe707a3522c` |
| `quickstart.md` | `4cff15d5f8dba6f2f4d454e68dca94f95759357581126e81acf5e86b8b01dc22` |
| `contracts/canonical-envelope.md` | `d334327a4039ad9e6d9f8859cac0de8cfb8d44dd1b679e721bbaaa0089faa7c9` |
| `contracts/prepared-scan-contracts.md` | `60fc8a4b2064fb87b9f89f03be0b29501d0762a89da0069bebc4d26d53d6eab2` |
| `contracts/safe-analysis-contracts.md` | `d3d2e406d958777bab3bd6f69b9efd14edb2e5dd61ba290d520e559b892797a8` |
| `contracts/preparation-api.md` | `3871f94535833e65fa4566a5124d7a7c91e79d43ffa39baf3f142e96a6ffee8f` |

## Coverage and prior-finding closure

The intent inventory covered all 43 Functional Requirements, 9 buildable
Success Criteria, 16 acceptance scenarios, 9 Architecture Decisions, 5
Constitution principles, all existing tasks T001–T060, and the complete Story
12.3 exclusion boundary.

- F1/T053: exact JDT-LS Java ranges and whole-file invalid-range behavior are
  closed.
- F2/T054: bounded post-readiness Calls/Imports/Inherits expansion and direct
  score fallback are closed.
- F3/T055: complete candidate/filter/skip/XML/split/expansion/classification
  audit projection and totals are closed.
- F4/T056: production revision-bound Java 21/JDT-LS stdio readiness is closed;
  XML-only remains explicit `NotApplicable`.
- F5/T057: per-file bounded rejection and safe-file continuation are closed;
  rejected artifacts cannot become File Code Units.
- F6/T058: production CodeGraph composition resolves the acquired exact
  revision, performs fresh index/rebuild/status checks, and proves complete
  readiness before Java or approval; hard failures remain rejecting.
- F7/T059: empty, BOM-only, and whitespace-only Java artifacts are excluded
  before ordinal-first readiness selection.
- QR-001: typed CodeGraph/Java proof preimages and complete manifest scalar
  reconstruction are closed, with duration-only replay variance preserved.
- QR2-001/F8/T060: `PreparedScanIdentityBuilders.OrderCommunicationClassifications`
  is the single normative comparator. Detector retention, manifest tag 26,
  initial package validation, durable reload, row equality, and stored manifest
  reconstruction all consume it. `ValidatePackage` rejects a supplied
  noncanonical sequence before any durable mutation. The PostgreSQL
  qualification proves a two-classification restart replay where normative
  order differs from fingerprint order, and proves a deliberately
  noncanonical first commit returns `Conflict` with zero prepared rows.
  Count, identity, field, and fingerprint comparisons continue to reject
  missing, extra, or changed classifications.

The direct current-source `PreparedScanAtomicReplayTests` run passed 5/5. The
checked-in host qualification records the unchanged T060 regression passing
1/1, the complete PostgreSQL atomic suite 30/30, PostgreSQL tamper cases 7/7,
SQLite replay 5/5, classification contracts 4/4, and migration lifecycle 2/2.
Build output reports zero errors; existing analyzer/package/reference warnings
remain separately documented.

## Findings

| Gap type | Count | Severity result |
| --- | ---: | --- |
| missing | 0 | none |
| partial | 0 | F1–F8 and QR-001/QR2-001 closed |
| contradicts | 0 | none |
| unrequested | 0 | none |

No intent, authority, canonical terminology, Constitution, ADR, architecture,
or scope defect must return to definition. No new convergence task is
justified.

## Handoff

**Converged — the pinned Story 12.2 implementation satisfies the accepted
specification, plan, tasks, contracts, Constitution, prior convergence findings,
and quality-review correction boundary.**

No implementation or task-generation pass is required. The root workflow may
advance to the independent quality-review acceptance, conditional E2E decision,
pinned final verification, and root-owned lifecycle/checkpoint updates.
