# Story 12.5 Exact Owner Semantic Identity Scan planned-change authorization 04

- Decision ID: `story-12-5-planned-change-authorization-exact-owner-semantic-identity-04`
- Gate: AGENTS.md Scan planned-change authorization
- Decision: **accepted**
- Decided at: `2026-08-31T04:29:26.7318414Z`
- Scope SHA-256: `a38b4c26f657eaa3c7baaced52a1fc1c92784ee1b576821ebad5292e1132b678`
- Accepted plan checkpoint: `845817e898a0c94789f99b2bd9cd7db94ede3733`
- User authorization: explicit approval of both task generation and implementation in the task immediately following the complete planned-change decision packet
- Independent plan review: **ACCEPT**, `plan-review-exact-owner-03.json`

## Trigger and approved effect

The accepted plan materially changes the bounded Prepared Scan strategy for the
single reviewed exact-resolution profile. A matched profile may use an
authoritative JDT-LS `DocumentSymbol` owner to give an oversized owner segment a
semantic File Code Unit identity. Only supported Class, Enum, Interface, and
Struct symbols with valid contained `selectionRange` coordinates qualify. Flat
`SymbolInformation.containerName` evidence cannot supply semantic authority.

The semantic-owner unit uses canonical identity schema `2.0`; ordinary
path-valued units retain schema `1.0`. The server-only `SymbolName` is propagated
and revalidated through Prepared Scan and Prepared Pass 1 integrity boundaries,
including the Web dispatch-request bridge, but it never enters prompts, tools,
provider JSON, or result contracts. Generic and nonmatching profile behavior is
unchanged.

## Implementation intent

Regenerate only the affected Story 12.5 tasks, run a fresh independent combined
definition review, and—only if that review accepts without changing this bound
scope—record the user's implementation approval against the accepted
specification, plan, and regenerated tasks hashes. Implementation then follows
the sprint loop's public-seam RED, minimal GREEN, surrounding regression,
convergence, independent quality review, conditional E2E, and pinned final
verification gates.

The correction may touch only the accepted Prepared Scan/JDT-LS/File Code Unit
and Prepared Pass 1 identity seams and their tests. It does not authorize an EF
migration, `VulgataDbContext` or snapshot changes, Story 12.4 contract changes,
CGC or legacy Scan-route restoration, Track A/B changes, prompt/tool/provider
contract changes, external service use, database creation, or release-run work.

## Bound artifacts

- Correct-course proposal scope — `cbfd779d145b3a2c1a67a28334626b1f2753aed04272d404333fb1d2a51545f6`
- Accepted specification — `6da3e251b217ab8dd8190f84c4cf864b994be1e87090558a29e125f5648ec8b7`
- Accepted plan — `fd7feea32f1e7a992e0b6e1fdfe0aabdd380968c842bb67bd3ee1794e4373d1d`
- Independent plan-review verdict — `d724ba40f82d07f9e4883d29ddd88f2ab1390401a5733862310483a1fe2706a7`
- Accepted plan commit — `845817e898a0c94789f99b2bd9cd7db94ede3733`

This approval advances the accepted plan to task generation. The user's same
instruction also preauthorizes implementation only if the fresh independent
definition review accepts the unchanged bound definition; it does not waive or
replace that review or any downstream technical gate.
