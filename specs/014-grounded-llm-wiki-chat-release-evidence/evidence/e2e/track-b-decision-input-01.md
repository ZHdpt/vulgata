# Story 12.5 Track B conditional E2E decision input 01

- Quality checkpoint: `b48f13b2`
- Reviewed product candidate: `f8c89541cfbef3ba89fea79c852ae9fcfe7bc081`
- Quality verdict: `ACCEPT` (`track-b-quality-review-05.json`)
- Decision owner: orchestrator
- Execution owner: `e2e-runner`
- Initial conclusion: `required: true`, `result: pending`

This decision applies only to Track B/B-INT. It must not substitute fake Backup B state, deterministic rescue output, or Track C real-provider/restart/screenshot/release evidence.

## Predicates

| Predicate | Gate | Triggered | Affected behavior | Current evidence and decision |
| --- | --- | --- | --- | --- |
| `user-visible-workflow` | required | yes | Authenticated Chinese Business/IT question, pending state, accepted-only immediate/progressive reveal, cancellation, and explicit `新对话` generation reset | bUnit coverage is extensive, but it is not a real authenticated rendered browser boundary; no coverage override. |
| `authentication-authorization` | required | yes | Authorized System/two-Repository scope, currentness, final authorization revalidation, hidden/missing equivalence | Coordinator and WebApplicationFactory tests cover parts, but no single current-boundary E2E proves the rendered flow plus authorization; no coverage override. |
| `api-persistence-multi-module` | required | yes | Web page → coordinator → advisory/retrieval/relationship/packet → strict raw streaming dispatcher → validation → sole publication owner → rendered accepted composite | B-INT interaction/composition tests prove typed seams and counts, but not the complete rendered boundary; no coverage override. |
| `ef-migration-data-lifecycle` | required | no | Track B adds no EF migration, DbContext, model snapshot, or observable persisted lifecycle | Guarded by the reviewed manifest. |
| `existing-integration-coverage` | may-skip | yes | Existing bUnit, coordinator integration, composition, streaming, packet, retrieval, and currentness suites | Partial for the three triggered Required predicates, so precedence override is invalid. |
| `documentation-or-refactor` | may-skip | no | The reviewed product delta changes observable conversation reset/publication behavior | Not applicable. |

## Source boundary

- Base: `a0eda652f01cb7b410a270783128d1eaefbd0ec1`
- Reviewed candidate: `f8c89541cfbef3ba89fea79c852ae9fcfe7bc081`
- Reviewed diff SHA-256: `0c448ea66438c23276cdf6ca624c63a6eb4dc9779c191d92eba6895ba658f466`
- Accepted quality review SHA-256: `caa3e59daf004d586db460a9f34854433a052e070feb988e8c32b9462081c9e5`
- T074 checkpoint SHA-256: `5b67e4c3cda0a17c7fc82844323334d2600708973c7587fea9baf182bcc675fc`
- T075 convergence SHA-256: `f794b0fd3d04005db0e90e37230515da38f7bb0c37988d975bd0dd261fbb35dd`

The product-owned paths are the Track B boundary listed in quality review 05. No tracked product or test change is permitted during E2E without invalidating the accepted review and returning through the sprint loop.

## Required economical execution

Use the smallest existing real rendered/integration path that proves every triggered predicate at the current boundary. It must cover:

1. authenticated Business and IT operation with the production typed composition;
2. no raw provider fragment or unvalidated claim/citation/handle/relationship/Source Anchor visible before accepted publication;
3. accepted Chinese answer and citations render only from the committed composite;
4. cancellation exposes no partial provider output;
5. explicit `新对话` after an accepted turn, including an in-flight/reveal race, leaves the next identical-scope retrieval with empty Conversation State;
6. authorization/currentness and the two-Repository scope are not bypassed;
7. no fake Backup B state and no Track C attestation/release evidence is used.

The runner may use existing test infrastructure and add or correct E2E/API/integration tests only within its allowed boundary. Any tracked test edit changes the candidate identity and invalidates quality review before E2E may advance. Product failure returns raw evidence to TDD; test failure stays with the runner; ambiguous infrastructure or product attribution stops.

## Pending result

- `required`: true
- `coverage_override`: null
- `owner`: `e2e-runner`
- `result`: pending
- `evidence_ids`: pending runner evidence
