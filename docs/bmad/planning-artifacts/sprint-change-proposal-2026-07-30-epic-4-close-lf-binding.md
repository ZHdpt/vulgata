# Sprint Change Proposal: Epic 4 Close LF Binding

Date: 2026-07-30  
Epic: 4  
Affected story: 4.2, Language and Framework Detection  
Decision: approved narrow remediation

## Trigger

Epic 4 close verification ran from a clean detached worktree at
`4a6527d20e86fb13d6812939fae22b58dd886841`. Docker image construction and
the Web build passed. The full test suite reported 613 passed and two failed
provider-contract checks.

One failure was the expected absence of the separately authorized, one-time
final-image provider-contract input. The actionable failure was
`AcceptedCorrectionManifest_IsCanonicalAndArtifactBound`: the accepted
`plan-review-10.json` bytes had SHA-256
`db83a12633aacf3461625fe85410ca2c1e513053b2ed70f65a63999019b60690`
instead of the required canonical LF SHA-256
`dc0638af64c19545adc96a3e8462abc4b9302ac77bc13c3b267726d565ce32be`.

The detached checkout was Git-clean. Repository-local `core.autocrlf=true`
converted this JSON file to CRLF because `.gitattributes` pins the surrounding
accepted-plan artifacts but omits this exact bound review artifact.

## Approved correction

Pin both exact JSON inputs whose SHA-256 values are enforced by the executable
accepted-correction manifest:

```gitattributes
/specs/002-language-framework-detection/reviews/plan-review-10.json text eol=lf
/specs/002-language-framework-detection/evidence/quality9-scan-strategy-remediation-approval.json text eol=lf
```

The second path was exposed by the first fresh-checkout GREEN attempt: after
the plan-review hash passed, the same test advanced to the approval artifact
and observed the identical CRLF checkout conversion. This is the same root
cause and correction boundary, not a product-scope expansion.

Then prove the focused contract from a fresh clean checkout with
`core.autocrlf=true`, run the surrounding non-one-time provider-contract
tests, obtain an independent combined correction review, and create a new
Story 4.2 completion checkpoint before restarting Epic 4 close.

The already consumed final-image provider namespace
`a4abf083843659cedf4d9e8bc9c4e98282eef3616dedfa5cc1536237c610ff89`
must not be rerun or overwritten. Epic-close verification must treat its
accepted immutable claim/evidence as the historical provider execution and
must not misclassify the environment-guarded test as an ordinary repeatable
test.

## Scope and effects

- No production code changes.
- No Scan strategy, parsing, filtering, scheduling, grouping, retry, or
  dispatch behavior changes.
- No Worker Agent or agent-tool changes.
- No PRD, ADR, domain-model, API, persistence, migration, or UI changes.
- No test assertion weakening.
- The correction makes a fresh Windows checkout preserve the canonical bytes
  that the existing accepted-plan binding already requires.

The planned-change authorization triggers in `AGENTS.md` do not apply.

## Workflow intent

Story 4.2 temporarily returns from `done` to `review` for this bounded
correction. Epic 4 remains `in-progress`. After focused GREEN evidence,
independent review, and a new bound Story 4.2 checkpoint, Epic 4 closure
restarts from eligibility at the new `HEAD`; evidence from the invalidated
close boundary is retained only as failure history.
