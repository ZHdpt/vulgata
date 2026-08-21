# Story 12.2 Convergence — Iteration 7

- Pinned correction checkpoint: `d6eeb6bf99cf75d03ec3a8b687f1cac90c6d11f3`
- Branch: `demo0831/story-12-2`
- Full base: `29e889badc00db671fcabf80f50918e15381052c`
- Correction diff (`ea7e8946fef8bc8daf7db0292eda38c673ebcf8d..d6eeb6bf99cf75d03ec3a8b687f1cac90c6d11f3`, `git diff --binary` captured as UTF-8): `98e92c45cddeab2f99d2c3475c39ccf6a10b0828573887b47e4c41a0d4baaf05`
- Accepted definition boundary: `e997c182a466694bc5eab405bb68fc7396edee53ce7559f443bcbb2e7beaefb6`
- Attempt: `7`
- Outcome: `tasks_appended`
- Tasks hash before: `7da2328fa6886a31953ee73c48bff64aab21e48892ae98bd3a126f28fd31587f`
- Tasks hash after: `6f1d2ebf2c9221a118e80edb81ed54580642440a96ca10562cee7bef9fe93b3b`
- Appended task: `T060`
- Diff check: `git diff --check -- specs/011-java-xml-prescan-manifest/tasks.md` passed; only the repository's existing LF/CRLF notice was emitted.

## Authority, prerequisites, and coverage

- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned the required feature directory and `research.md`, `data-model.md`, `contracts/`, `quickstart.md`, and `tasks.md`.
- `.specify/extensions.yml` has no executable `hooks.before_converge` or `hooks.after_converge` entry; no hook ran.
- `.specify/memory/constitution.md` is ratified. Principles I–V were checked. No Constitution MUST violation was found.
- The accepted `spec.md`, `plan.md`, `tasks.md`, research, data model, quickstart, canonical-envelope, prepared-scan-contracts, and safe-analysis contracts were inspected. The accepted definition hashes remain:
  - `spec.md`: `c15d03b2b10e4eaa452a59e3b96acafdbb04160de556ca93c28380117a085ad3`
  - `plan.md`: `2a9373661e935918ab6b55a4d83903e4ba92ba7e9fb80534a616d6583cb4b318`
  - `tasks.md` after T060: `6f1d2ebf2c9221a118e80edb81ed54580642440a96ca10562cee7bef9fe93b3b`
  - `research.md`: `617304112b3b744a4f393ffccddd7859a37faca59db056680d0eda300954ac3c`
  - `data-model.md`: `1a648ab2a7c8802d2d9ca5c279476371685890240cc9adfb0143cbe707a3522c`
  - `quickstart.md`: `4cff15d5f8dba6f2f4d454e68dca94f95759357581126e81acf5e86b8b01dc22`
  - `contracts/canonical-envelope.md`: `d334327a4039ad9e6d9f8859cac0de8cfb8d44dd1b679e721bbaaa0089faa7c9`
  - `contracts/prepared-scan-contracts.md`: `60fc8a4b2064fb87b9f89f03be0b29501d0762a89da0069bebc4d26d53d6eab2`
- Coverage assessed: all 43 Functional Requirements, 9 buildable Success Criteria, 16 acceptance scenarios, 9 Architecture Decisions, 5 Constitution principles, the complete Story 12.3 exclusion boundary, all existing tasks T001–T059, convergence findings F1–F7, QR-001, QR2-001, and raw quality reviews 01–02. Convergence reports 01–06 and the correction diff were inspected.

## Prior finding closure

- F1/T053 exact JDT-LS Java ranges and whole-file invalid-range behavior remain closed.
- F2/T054 bounded post-readiness related-file expansion and deterministic direct-score fallback remain closed.
- F3/T055 complete candidate/filter/skip/XML/split/expansion/classification audit projection remains closed.
- F4/T056 real revision-bound Java 21/JDT-LS stdio readiness remains closed.
- F5/T057 per-file bounded artifact rejection and safe-file continuation remain closed.
- F6/T058 real revision-bound CodeGraph initialization, rebuild, status, and completeness proof remain closed.
- F7/T059 empty/BOM-only/whitespace-only Java exclusion before ordinal-first readiness selection remains closed.
- QR-001 typed CodeGraph/Java proof preimages, complete manifest scalar reconstruction, and stable-duration replay handling remain closed.
- QR2-001 is substantially closed: `PreparedCommunicationDetector.OrderCanonical` is used by detector retention, durable reload, row comparison, and persisted manifest tag-26 reconstruction; the PostgreSQL restart regression proves normative order differs from fingerprint order and exact retry returns `Replay` without mutation.

## Finding F8

| ID | Gap type | Severity | Source | Evidence | Remaining work |
| --- | --- | --- | --- | --- | --- |
| F8 | partial | HIGH | FR-023, FR-034, FR-040; US4/AC3; canonical-envelope §§4.5/4.8/6; prepared-scan store replay contract; plan AD-9 | `PreparedCommunicationDetector.Detect` orders created classifications normatively, and `PreparedScanRepository.InspectAsync` orders durable rows before `ClassificationsMatch` and `StoredManifestFingerprintMatches`. However, `PreparedScanIdentityBuilders.CreateManifest` emits tag 26 directly from `package.Classifications`, and `PreparedScanRepository.ValidatePackage` does not require that supplied sequence to be canonical. A caller can reverse two valid classifications, recompute the manifest and pair fingerprints through those same direct-list builders, and pass the no-existing-manifest validation path; this permits a noncanonical approved first commit. The contract requires normative retained-classification order and any reordered value to conflict. Existing QR2-001 coverage proves restart replay but does not prove rejection of a deliberately noncanonical first-commit package. | Complete T060: expose one shared normative comparator to manifest construction and package validation, reject noncanonical supplied rows before initial persistence, and add PostgreSQL exact-replay/conflict coverage for missing, extra, changed, and noncanonical rows while retaining the two-classification normative-vs-fingerprint restart case. |

`T060` was appended under a new `## Phase 13: Convergence` section in `tasks.md`. It is implementation-owned, has no new architecture decision, and does not alter Story 12.3 scope or the approved August host-runtime architecture.

## Finding classification

| Gap type | Count | Severity result |
| --- | ---: | --- |
| missing | 0 | none |
| partial | 1 | F8 HIGH |
| contradicts | 0 | none |
| unrequested | 0 | none |

No intent or authority defect was found. The remaining gap is a traceable implementation/test correction, not a specification, plan, Constitution, ADR, terminology, or architecture contradiction.

## Handoff

**Tasks appended — one HIGH partial (`T060`).** Run the authorized TDD implementation for T060, then rerun convergence against the resulting pinned boundary. The host-runtime pins, PostgreSQL-only Docker topology, no-CodeGraphContext rule, and all Story 12.3 exclusions remain unchanged. Independent quality review and later verification gates remain pending after convergence becomes clean.
