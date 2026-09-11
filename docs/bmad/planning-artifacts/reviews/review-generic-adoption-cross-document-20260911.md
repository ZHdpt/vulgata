# Generic adoption cross-document integration review

Date: 2026-09-11

Verdict: The reviewed planning boundary is coherent, with one narrow contributor-guidance correction before finalization. This is not acceptance of a completed generic product or completion of the adoption goal; replacement epic/story definition and sprint tracking remain coordinator-owned unfinished steps.

## Scope and pinned inputs

Read the canonical PRD, glossary, architecture gateway and spine, both current UX specifications, AGENTS.md and both READMEs. Compared relevant preserved obligations with the approved September 11 proposal and pre-adoption archive. Checked selection metadata without running feature setup or prerequisites. No source, tests, migrations, packages, Docker configuration, services, provider calls or databases were inspected or changed; no Git/build/test/runtime commands were run. Only this report was written.

Reviewed PRD SHA-256: `466CF34CBAAAF7442392999C99CBBEBFD27E547DF1F84DCF604B2A2C7AD23D11`.

Other reviewed SHA-256 values:

| Input | SHA-256 |
| --- | --- |
| `docs/UBIQUITOUS_LANGUAGE.md` | `680494A9E766270A18AE02D5C92670771A72912A87F632946350431D4696550D` |
| Architecture gateway | `2F40212D9FF4F90593A6FC9D478E1237E6630064E466F2369CA62CF5B1FC98DC` |
| Architecture spine | `13504EB83C7822B996E278113DFA31D5A70D07C2407035ADDBA3645BCDEB5A8B` |
| `DESIGN.md` | `87B022E601BC4603AB75DC0211C1E10F1F0F4B90CEB3250ECCA8166FFA3C2CAC` |
| `EXPERIENCE.md` | `A550D650B01F6EF732FC4F11004D6EADDCCF511FE34916F1B1CE5ECE3AF1B87F` |
| `AGENTS.md` | `4573C74682C40D7B3B52103184EA9389641F7F032E09CA8F3DC2FB8C56D734BB` |
| `README.md` | `DBAC58B0B39E4297B7686B8D08DF69D89F85AD65C05E0CC5B3EF9173D27D0485` |
| `README.zh-CN.md` | `F1739EC07A7C2279092E767E92787F08B00B4DD865FBD2E47D6EE4ACF2CE132B` |

The BMad review used adversarial and independently delegated edge-case passes. The customization runner could not open its shared uv cache; shipped defaults were read directly. The requested bounded, material-findings-only scope excludes editorial redesign and padding.

## Finding CD-1: Docker launch guidance disagrees

Both lenses independently identified the same issue; their findings below represent one correction, not two distinct defects.

### Adversarial

- **Location:** `AGENTS.md:53–54`, compared with `README.md:79–91` and `README.zh-CN.md:79–91`.
- **Trigger condition:** A contributor follows AGENTS.md's command labelled “Full Docker stack (Blazor app + PostgreSQL 17)”.
- **Guard snippet:** Use `docker compose --profile post-deadline-app up --build` and label it the existing application baseline, not the qualified generic target.
- **Potential consequence:** The current documentation gives conflicting launch instructions. According to both READMEs, the unprofiled command starts only PostgreSQL, so the promised application is unavailable.

### Edge-case

- **Location:** `AGENTS.md:53–54`; `README.md:79–91`; `README.zh-CN.md:79–91`.
- **Trigger condition:** Following AGENTS.md to launch the documented full Docker stack.
- **Guard snippet:** Use `docker compose --profile post-deadline-app up --build`, labelled as the existing baseline.
- **Potential consequence:** Only PostgreSQL starts; the documented application and administration UI remain unavailable.

This is a static cross-document inconsistency. Compose configuration and actual startup were deliberately not tested. The correction aligns existing contributor instructions without introducing a new deployment policy.

## Verified alignment

- All 17 archived pre-images match their manifest hashes. The external spine hashes to `CC7E5AC92810734549607F38399720A027E3E237320ACEC30926FBF24D6FE093`; all 32 complete normative AD blocks match the repository copy after line-ending normalization.
- Stable Code Unit/Logical Document/Logical Flow identities, immutable versions and exact historical dependencies align. PRD FR-4.2/4.3/4.10 now explicitly retain unavailable-generic behavior, legacy lineage and evidence-based withdrawal; omissions, failures and access loss do not become deletion proof.
- PRD FR-11/12.6, glossary Chat terms, AD-24–29 and both UX documents consistently enforce Business BL-only reads across tools, history, graph data and citations. IT exact-source access stays exceptional and authorized; reader placement confers neither management rights nor source permission.
- Scan's explicit prerequisite-checked resume differs from same-turn browser reattachment and host-failure Chat retry. Durable completion, originating context, live authorization and no duplicate generation are consistently preserved.
- Library Oak/Chinese-only UI, chat-first navigation, management/identity/provider/context obligations and the accessibility floor remain. The architecture gateway explicitly preserves non-root execution and protected data/source/key handling without claiming current Docker readiness.
- Approved deferrals remain separate from original unmet Prompt Workbench, unknown-library inference and general MCP backlog. Native CodeGraph/internal wiki tools do not receive completion credit for those capabilities. No hidden new tool, protocol, visual or security policy was found.
- All local Markdown link targets in the nine reviewed current documents resolve. `.specify/feature.json` is absent; `SPECIFY_FEATURE_DIRECTORY` and `SPECIFY_FEATURE` are empty in this review process. AGENTS.md's managed block requires deliberate new feature selection and does not resume terminated Story 12.5.

No additional material missing behavior was found by the edge-case path/deletion-contract pass. Pending replacement epics/tracker work is explicitly excluded from this correction finding and remains unfinished, not silently accepted.

## Focused closure — 2026-09-11

Verdict: CD-1 is closed. The additional wiki JSON-content preservation is consistent with the accepted boundary; no new material issue or scope expansion was found in this focused check. This is not a second full review.

- `AGENTS.md:53–54` now labels the command as the current app/PostgreSQL baseline with the generic bundled-tool runtime still pending, and uses `docker compose --profile post-deadline-app up --build`, matching both READMEs. The Agents project comment at line 27 now describes existing execution/contracts/prompts instead of calling the whole project planned.
- PRD FR-4.7 (`prd.md:82`) explicitly retains structured JSON as canonical Code Logic/Business Logic content, with derived human-readable forms and retrieval projections rather than parallel editable authorities. The archived FR-4.7 (`pre-adoption PRD:244`) already derives retrieval indexes from `content_jsonb`; the restoration does not replace immutable versioning or per-flow acceptance/publication.
- The gateway (`architecture.md:73`) preserves the existing `ContentJson`/PostgreSQL `jsonb` boundary and explicitly excludes Chat prose/conversation records from this wiki-content rule. Narrow static source checks confirm `DocumentVersion.cs:53` exposes `ContentJson` and `DocumentVersionConfiguration.cs:45` maps it to `jsonb`; the adjacent `ContentMarkdown` field is not evidence of a second editable authority or current runtime qualification.
- PRD FR-11.7 (`prd.md:153`) and unchanged AD-27 (`ARCHITECTURE-SPINE.md:211`) still prohibit a mandatory complete-candidate JSON/semantic-verification gate for Chat streaming. The wiki-storage clarification does not alter that contract or prescribe a new Worker generation protocol.

Closure input SHA-256 values:

| Input | SHA-256 |
| --- | --- |
| `AGENTS.md` | `26E8BE91360610E1128C3B98303388F614C7F7A8077BBA1A51708C927216D650` |
| PRD | `AC4C5C5BAAA677AF2D63901CC33AB2A652CD8F94C4E781256E9AC0C94DD2DB25` |
| Architecture gateway | `2BBACCA8DEBB46F043AA05320C43B99783E98B5530152F66DC0CA3D19ACCCDFC` |
| Architecture spine, unchanged | `13504EB83C7822B996E278113DFA31D5A70D07C2407035ADDBA3645BCDEB5A8B` |

Only this report was appended. No runtime, Git, build, test, provider or database operation was performed. Replacement epic/story definition, tracking and coordinator finalization remain outside this closure and unfinished.
