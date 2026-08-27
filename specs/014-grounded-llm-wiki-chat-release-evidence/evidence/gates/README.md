# Story 12.5 Entry Gates

| Track | Entry condition | Current result |
|---|---|---|
| A | Accepted definition plus explicit Track A implementation approval | Open for T001-T019 under `story-12-5-implementation-approval-track-a-01` |
| B | Full implementation approval plus accepted Story 12.4 final checkpoint and integrated final commit | Closed |
| C | Accepted Track B plus candidate-bound release preflight | Closed |

Track A scope SHA-256:
`81ea1431d710e6ab9d0647bebcc49525dd924f1265862b1666917ff2a08c5309`.
The durable approval is recorded in
`specs/014-grounded-llm-wiki-chat-release-evidence/decisions/implementation-approval-track-a-01.md`.

Every later gate decision must record its decision ID, exact scope hash, UTC
decision time, evidence path, and still-applicable downstream gates. A gate must
fail closed when any bound artifact hash changes.
