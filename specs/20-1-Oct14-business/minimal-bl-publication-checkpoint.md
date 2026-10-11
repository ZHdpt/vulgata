# One local-flow BL: implementation and execution boundary

2026-10-11. User approved the narrowed management handoff and separately authorized
Business-only nonstreaming changes in `OctoberWorkerClientFactory.cs`. Assigned
`f081` / `oct14/business` was clean at `fde78bdc`; no reset, merge or push.

## Implemented boundary

- Project complete flow-member sections/claims into an exact packet; include accepted
  relationships, frontiers and gap counts. No whole-corpus prompt, retrieval tools,
  role classifier, resolution-policy change, source execution or alternate publication.
- New proposals can cite only packet members. The existing publication store rebuilds
  the same packet during independent validation. Full dependencies, contributors,
  coverage and gap records remain owner-held and published through the canonical route.
  Historical result lookup continues checking stored request/outcome digests without
  reinterpreting historical proposals under the new member-only rule.
- Finite UTF-8 bounds: owner input 16 MiB; complete gap ledger 8 MiB; validation result
  16 MiB; model packet/messages and proposal 1 MiB each. Existing stored-read 32 MiB
  bound remains unchanged. Oversize evidence is rejected, never silently truncated.
- Synthesis reserves conservative escaped-message UTF-8 bytes plus bounded protocol
  overhead **and** output allowance. This is a reservation estimate, not tokenizer usage.
  Actual usage/settlement remains on each original leaf lease.
- Maximum output ceiling 393216; total synthesis deadline 900 seconds; initial response
  plus at most three structural replacements, no outer transport retry. Strategies:
  configured, configured, thinking disabled, compacted history with thinking disabled.
  Original packet/instructions remain; rejected proposals are not evidence.
- Required output fields cannot silently default omitted ordinal/qualification values.
  Duplicate fields and unknown fields remain invalid. Explicit Mixed/undefined or
  changed qualifications, invalid evidence references, Length, unknown usage, authority
  failure and transport failure do not obtain structural correction retries.
- Static correction instructions are inside the existing hash-bound synthesis prompt;
  follow-up feedback contains only fixed failure classification and remaining count.
  DeepSeek compatibility/reasoning replay is reused on the Business port. CL streaming
  code is unchanged. No schema or migration change.

## Exact single root and refreshed read-only preflight

Target `phone-15bfe12615eb`, batch `1a327657-6a55-4e65-ba06-6992f423d40e`,
CL publication `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`, corpus
`0ea50f38-2441-4ec0-b31c-313acd0c5f55`, database
`vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d`.

Selected repository `9172b209-ed3a-415c-8d8b-c0ef1b2f18f6`, relative path
`src/main/resources/flow/register/selfRegist.flw`, selector `selfRegistFlow`,
unit `7b983152-5326-a834-eec1-9634c4384886`, logical document
`c162bbec-92aa-ea8c-3f32-e119253f7fe0`, exact CL version
`38884f7e-d58b-436f-9a03-cb3fa769e3af`. Kind `SelectedCodeUnit`.

Authenticated owner API reads and canonical owner metadata, using PostgreSQL
read-only connections and a deny-write guard, confirmed:

- 163 CL versions / 77 accepted files, exact frozen-corpus membership.
- Zero Business Begin/Complete receipts at observation time.
- Unchanged policy yields zero inquiries, 355 NotEvaluated dispositions. Do not
  reinterpret zero inquiries as resolution completeness; refuse a changed prerequisite.
- One planned group, 29 supplied claims, packet **25717 bytes**.
- Complete owner input **2419063 bytes**, full gap ledger **547626 bytes**.
- Final prompt input reservation **47102**, output ceiling **393216**, initial
  attempt reservation **440318**. Correction history is measured again per attempt.
- Original CL content digest remains
  `7d905781b3d6aa6a46bc83fa6a6d7e3969226fb0e46c22391130e994c4fa47f7`.

Local preflight evidence `TestResults/business-readiness/minimal-verified.json`,
SHA-256 `6B67F1175721B7B80B0428C87283BE0D0E66268363734E89838E52EEA04D3004`.
This is not BL generation or ordinary non-management reader qualification.

## Review and tests

Independent focused source review accepted after fixing two findings: explicit
qualification failures being retryable, and correction instructions missing from
the prompt identity. Required-field checks were also reviewed. No larger redesign
was introduced.

Behavioral RED: outside-packet acceptance `40961eb0b0be4ce5a195b290c1b0f6ed`;
large owner input `74a7c4a2400d421ab4d1c5f8e2e45166`; four qualification/presence
regressions `9036c0cee0024349a29a553d13aba6b6`. Earlier invalid test-fixture
fingerprints, sandbox build failures and interrupted frozen-clock runs are not RED.
Initial controlled 74/74 passed at `f017c0a077f54b4aace299f26db5b447` before review
fixes. Broader tests exposed old fixture-only 100-token and transport-retry/deadline
assumptions; only synthesis fixtures were adjusted, preserving existing budget-test
defaults and resolution branches.

Final controlled qualification: **373/373 passed, zero failures/skips**, including
18 new correction/wire cases, finalizer, canonical publication and immutable replay,
existing-CL continuation, execution scope and transport. Evidence:
`TestResults/isolation-bcaef8c6e60143ecb0cedc3c299181aa/results.trx`.
Run through `eng/test.ps1` in the SelfContained inventory. This does not establish
real-provider success, retained-database BL publication or ordinary-reader qualification.
The unrelated historical qualification-report parser mismatch remains separate.

## Single execution proposal — runtime authority pending

Use the existing `cl-and-bl` continuation with the exact existing publication and
single root above. Selected profile `selected`, model `deepseek-flash`, exact
DeepSeek endpoint, configured low reasoning. No CL generation or new resolution
inquiries may be introduced to fit this milestone.

One invocation, freshly generated RunId and original capability; worker cap 1;
**maximum 4 physical provider calls**, **6000000 cumulative tokens**, output ceiling
393216 per call, synthesis deadline 900 seconds, overall deadline **1200 seconds**,
graceful drain 30 seconds and owned-process watchdog 1260 seconds. Input reservations
cannot exceed 1 MiB per call, so four maximum reservations total 5767168 tokens.
No second whole invocation is included. Unknown outcomes require reporting, not retry.

Fresh ignored local policy/settings proposals have hashes:
`DF08091D6A887BF9A2BF46A198A45641C5E7D6B02318DE61E1D359B738080B2F` and
`2B9DA4220E6470422914109833F7F3017752777D23AF5A99FCB23B5A383EB6B6`.
Credentials/runtime paths remain outside tracked configuration. Original consumed
launch markers stay untouched; a new invocation requires its own explicit grant.

Required consolidated authority: disclose this accepted root packet to the selected
provider within these limits; run one owned headless generator process and protected
logs; append canonical Business admission/finalization/publication/completion records
only in this exact retained database; create and remove one dedicated User-only
verification account through Identity APIs if no existing ordinary-reader credential
is supplied. No management role, migration, Prepare, source scan, host startup,
unrelated-process operation or manual accepted-Knowledge seed is part of the proposal.

After a successful settled invocation, verify one separate BL publication and its
full dependencies through owner APIs; authenticate the ordinary non-management User
and read all BL pages; compare original CL membership/content digest and reopen its
exact publication. Review useful conditions, branches, effects and limitations
against the accepted root, without supplying evaluation answers to generation.
Report **“Local-flow BL published; cross-repository behavior remains unconfirmed.”**
Browser/Chat/graph qualification remains separate. Stop after this first success.
