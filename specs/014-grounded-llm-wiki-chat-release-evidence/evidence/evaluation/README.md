# Story 12.5 evaluation preparation

This directory documents preparation for the fixed Story 12.5 Chat evaluation. It does not contain Tier 3 execution evidence or a release claim.

## Bound inputs

- Corpus: `tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json`
- Corpus schema: `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/story125-evaluation-case.schema.json`
- Release envelope schema: `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/story125-release-evidence.schema.json`
- Evaluation and evidence rules: `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/release-evidence-contract.md`
- Test seam: `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs`

The corpus contains 16 fixed cases (`q01`-`q16`) in Business and IT modes. Its `sourceAuthorities` bind the Story 12.5 authority, accepted evaluation specification, exact-resolution profile, neutral prepared-scanning fixture manifest, and the Java/XML fixture sources. The cases cover Cross-Repository flow and resolution, `GENERATED_FROM` provenance, Flow Groups, business rules and decisions, failure behavior, one initial question plus two follow-ups, insufficient evidence, revoked or missing authorization, citation coverage, and honest Uncertainty.

Questions and `usefulAnswerCriteria` define an evidence-bounded rubric; they do not embed desired provider prose. Repository names are release aliases, not proof that an observed candidate contains or authorizes those Repositories.

## Planned fields are not observations

Every current corpus `observation.status` is `planned`. Null durations, packet fingerprints, answer fingerprints, usefulness, citation coverage, and disclosure fields mean that the case has not been executed. The accompanying limitation is preparation context only. These fields must not be interpreted as a pass, failure, provider response, timing measurement, authorization result, or Tier 3 evidence.

An actual attempt must use a new immutable candidate-bound run envelope and preserve its exact commands, exit codes, durations, fingerprints, provider/tool identities, and hashed artifact provenance. Corrections supersede earlier envelopes; they never edit or replace prior observations. Only a schema-valid `passed` envelope may contain a release receipt.

Tier 3 corpus execution, real-LLM streaming observations, screenshots, restart evidence, and release acceptance remain gated. Do not populate this preparation document with synthetic results, fake Backup B state, or copied Story 11.5 evidence.

## Preparation verification

Run the focused corpus/schema contract tests without a network LLM:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~Story125EvaluationContractTests"
```

The semantic scoring seam requires exact nonnegative round-half-up usefulness (10/12 is 83.33), 100% accepted material technical-claim citation coverage, zero unauthorized evidence or existence disclosure, zero accepted answers at or above 30 seconds, and the closed `providerDisposition` vocabulary from the release schema.
