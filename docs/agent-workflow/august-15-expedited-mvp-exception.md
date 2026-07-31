# August 15 Expedited MVP Exception

## Authority and lifetime

The user approved this temporary execution level on 2026-07-30 for the fixed
2026-08-15 competition deadline.

`expedited-mvp` is permitted only for:

- `4-3-conservative-pre-scan-gate-and-communication-type-detection`; and
- Stories `11-1` through `11-5` in the August 15 Demonstration Slice.

It expires when Epic 11 completes or at `2026-08-15T23:59:59+08:00`,
whichever occurs first. It cannot be applied to post-deadline Epics 5–10 and
does not redefine normal Full or Standard eligibility.

## Exact flow

```text
context
-> specify
-> plan
-> conditional plan-review + planned-change authorization
-> tasks
-> implement
-> combined quality-review
-> conditional-e2e
-> verify
-> complete
```

Create one concise SpecKit feature per story. `spec-review`,
`speckit-analyze`, and the separate `acceptance-review` are omitted. The fresh
`quality-reviewer` owns combined acceptance and quality judgment over every
included acceptance criterion, security boundary, migration, regression risk,
test, and known limitation.

The omitted gates remain unused at iteration zero with no fabricated verdict
history. The combined quality gate persists one unchanged raw verdict as its
required output, not as an independent evidence ledger; unavailable historical
raw verdicts are never reconstructed.

Plan review remains mandatory when the plan changes an agent tool, Scan
strategy, PRD requirement, authorization/security boundary, migration
invariant, or Cross-Repository matching behavior. One accepted plan does not
waive the repository's planned-change authorization.

## Checkpoints

Use three planned checkpoints per story:

1. **Accepted delivery definition:** prepared/activated story, concise
   SpecKit artifacts, accepted plan review when required, authorization, and
   tasks.
2. **Reviewed candidate:** implementation, RED/GREEN evidence, included tests,
   and the pinned candidate boundary at `review`.
3. **Completed story:** accepted combined review, E2E decision/result,
   verification, and `done`.

A correction may add a fourth recovery checkpoint only when a reviewer or E2E
runner finds a real product defect. Do not create evidence-freeze, ledger,
manifest, or audit-only checkpoints.

## Quality floor

- Production behavior still follows observed RED -> GREEN -> REFACTOR.
- RED is transient. Never commit or complete with a deliberately failing test.
- Deferred edge cases are removed from deadline acceptance criteria and
  recorded as known limitations; they are not represented by failing tests.
- Included targeted tests, migrations, authorization/security checks, Web
  build, and story verification must pass.
- Run one clean-database two-Repository E2E and one final Docker image build in
  Story 11.5. Earlier stories use current-boundary integration coverage unless
  they change container/runtime behavior. Story 11.1 Docker is final-slice-only,
  while its persistence and migration E2E predicates remain required.
- Never weaken secret handling, source isolation, authorization, migration
  safety, deterministic evidence, or exact Cross-Repository matching.

## Evidence limit

Durable evidence consists of the command, exit code, observed commit/boundary,
timestamp, and concise result. Preserve raw output only for failures,
provider/runtime contracts, security checks, migrations, final E2E, and final
Docker verification.

Do not create independent evidence ledgers, repeated immutable freezes, or
one-time namespaces unless an external provider contract genuinely requires
them. Evidence production must not become a separate product subsystem.

## Review correction

A combined-review `REVISE` returns raw findings to a fresh TDD implementer.
Behavior changes require regression RED, then GREEN. Re-pin the candidate and
rerun the combined review. The third `REVISE`, any `BLOCKED`, or a finding that
requires scope beyond the deadline slice stops for user direction. `BLOCKED`
persists its raw verdict but does not increment the revision count.

## Red flags

- Calling risky deadline work Standard.
- Applying this exception outside Story 4.3 or Epic 11.
- Leaving failing tests because an edge case was deferred.
- Omitting plan review or user authorization for a triggered planned change.
- Skipping authorization, migration, security, or final end-to-end checks.
- Claiming deferred Epic 5–10 requirements are complete.
- Continuing this exception after its expiry.

## Skill pressure-test evidence

On 2026-07-30, the unmodified two-level skill stopped on a deadline case
because risky work was ineligible for Standard and Full retained every gate.
After this exception was added, a fresh read-only agent correctly:

- paused Story 11.3 after accepted plan review for its triggered Scan-strategy
  authorization before tasks;
- selected the combined review, three checkpoints, limited evidence, and
  Story 11.5 final-slice E2E responsibility;
- rejected `expedited-mvp` for Story 5.6; and
- rejected committing a failing test for a deferred edge case.

The executable workflow contract also accepts `expedited-mvp` only as a closed
execution-level value with the unused acceptance gate invalidated at iteration
zero.
