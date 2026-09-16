# Implementation Design Contract

Implementation design uses document-level Markdown front matter plus a conditional section inside `plan.md`. It is not a lifecycle stage, separate artifact, review, or additional dispatch. The validator parses a strict two-field subset directly; no YAML library or plan schema is required.

## Front Matter

When classification is explicit, merge these fields into the single front-matter block at the first line of `plan.md`:

```markdown
---
governed_delivery_classification: bounded
governed_delivery_reason: Introduces an internal adapter and cancellation boundary.
---
```

Unrelated metadata fields are preserved. Governed fields are flat, single-line, unique, and limited to the two names above. Classification is exactly `none`, `bounded`, or `architecture`; reason is non-empty.

Missing governed front matter means implicit `none` for compatibility. Implicit or explicit `none` cannot contain `## Implementation Design`, governed decision IDs, or task references to those IDs. Every active plan review, whether architecture-triggered or repository-triggered, requires explicit classification and reason; completed plans without governed metadata remain historical legacy.

## Classification

The plan author determines exactly one classification:

- `none`: a localized behavior change with no consequential responsibility, interface, state, failure, or coordination choice;
- `bounded`: consequential internal responsibility, interface, adapter, state lifetime, async/cancellation, error, retry/idempotency, or cross-module coordination decisions that remain within accepted architecture;
- `architecture`: a public contract, authority, agent tool, repository-declared high-impact strategy, external dependency, or shared persistence-boundary decision, or any change that modifies accepted architecture.

Use [`decision_policy.py`](../scripts/decision_policy.py) for deterministic representative cases. Classification is based on consequences, not code size. `none` may remain implicit; `bounded` and `architecture` are always explicit.

## Plan Content

For `bounded` or `architecture`, add one `## Implementation Design` section. Use only these conditional third-level headings and identifiers:

| Heading | ID prefix |
|---|---|
| `Responsibilities` | `ID-Rnn` |
| `Interfaces` | `ID-Inn` |
| `State and Lifetime` | `ID-Snn` |
| `Async and Cancellation` | `ID-Ann` |
| `Failure and Retry` | `ID-Fnn` |
| `Reuse and Locality` | `ID-Lnn` |
| `Test Seams` | `ID-Tnn` |
| `Alternatives` | `ID-Xnn` |
| `Consequences` | `ID-Cnn` |

Each decision uses `- **ID-R01**: Non-empty description.` under its matching heading. IDs are unique. Bounded design requires at least one Responsibility and Test Seam. Architecture also requires at least one Alternative and Consequence. Fenced examples, HTML comments, indented code, prose-only task references, and malformed IDs do not satisfy the contract. Task references must appear on SpecKit checklist lines. Add only decisions another implementation could not safely rediscover.

Do not predict private method names or incidental decomposition in the plan. Those decisions remain an implementation responsibility, not an exemption from design: both TDD owners apply accepted constraints and local conventions, then assess their final affected code under `governed-tdd`. Routine helpers remain local; consequential interface, responsibility, lifetime, or other choices outside the accepted design return here. Use an installed `codebase-design` skill when available for module, interface, seam, adapter, depth, leverage, and locality vocabulary.

## Ownership and Routing

The persistent ordinary `definition-author` owns `none|bounded` at Sol/medium. Before recovery takeover, `architecture` uses the serialized `plan-architect-high` Sol/high exception. After exclusive takeover, the retained Astra recovery author owns every classification and all definition phases; fresh classification never restores a superseded author. Independent plan review and applicable authority rules remain.

The same definition author cites every implementation ID (`R/I/S/A/F/L/T`) on task checklist lines in `tasks.md`; alternatives and consequences never become task references. The author self-checks content shape before returning. After the root maps artifact paths and hashes into workflow state, the root runs the authoritative deterministic validator over path/hash binding, front matter, headings, ID shape, complete task coverage, and unknown references. `definition-reviewer` remains the semantic judge: it rejects an implausible implicit `none` or unjustified classification with `earliest_owner: plan` and checks that descriptions preserve the accepted design.

The accepted definition and implementation approval confirm every disclosed test seam. A consequential design discovery outside that accepted boundary returns to `plan` immediately; inadequate intent returns to `specify`, and task-only omission to `tasks`. Routine private decomposition is not an uncovered consequential design. Preserve valid decisions/IDs, code/tests, and task work while reconciling affected downstream content and checks. Retrieve relevant lessons on replan and account for them in normal self-review. Repeat independent review; preserve user approval for unchanged semantic scope with corrected accepted hashes, renewing it only for changed authority/scope or a new architecture trigger. Apply the shared recovery counter across these transitions; see `review-and-correction.md`.
