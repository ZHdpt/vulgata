# Retained CL readiness and blocked BL execution proposal

Business checkout `f081`, branch `oct14/business`, fast-forwarded from
`158e8bb9` to `f06b85bf9660ba4a9cb8c4545413c00428d74efe` without reset or conflict.
This is readiness evidence, not paid BL qualification or permission to execute.

## Verified retained input

An owned local probe authenticated with the protected operator credential through
`OctoberLocalAuthority`, bound the exact publication with `TryBindReaderAsync`,
and enumerated the full canonical reader catalog. The owner published source and
canonical October persistence metadata confirmed exact frozen-corpus membership.
`OctoberResolutionStore.LoadCorpusAsync` and `OctoberFlowPlanningInputLoader.LoadAsync`
then succeeded under live Identity-backed, all-contributor disclosure checks.
The probe used PostgreSQL `default_transaction_read_only=on`, disabled key generation,
and supplied a deny-all commit guard. No host, provider, migration, Begin receipt,
source reader or legacy launcher was started.

| Binding | Verified value |
| --- | --- |
| Target | `phone-15bfe12615eb` |
| Batch | `1a327657-6a55-4e65-ba06-6992f423d40e` |
| Original CL publication | `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9` |
| Corpus | `0ea50f38-2441-4ec0-b31c-313acd0c5f55` |
| Corpus digest | `B7C228C6B2D4F34BF0E43EE1AB3969BA119F5390864DB4B33E0BC909FCF6E29A` |
| Selection digest | `52fde3a3354bb00ce686d6eb4f10feb9279d0f2d9daea9a6b8473f51e48b43bd` |
| Database | `vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d` |
| Membership / coverage | 163 exact CL versions / 77 Accepted files |
| Existing Business continuation receipts | 0 Begin / 0 Complete at observation time |

Local publication-content baseline digest:
`7d905781b3d6aa6a46bc83fa6a6d7e3969226fb0e46c22391130e994c4fa47f7`.
This is SHA-256 of newline-joined `versionId:ContentJson`, ordered by version ID;
it supplements, rather than replaces, canonical corpus/publication identity.
Local evidence: `TestResults/business-readiness/verified.json`, SHA-256
`77F8D0CADD6E93ACF67C16C10BE8CB04FBE50D3C60FE340D40002CA9109D1F5C`.
Protected paths and credentials remain outside tracked configuration.

Reader catalog pages of 100 exceeded the existing output budget. Pages of 10
successfully enumerated all 163 versions. Earlier bounded exact reads also
succeeded; the redundant full per-document probe was deliberately interrupted.
Neither that interrupted probe nor a rejected large page is counted as a pass.
This used the authenticated operator through ordinary reader APIs, not a separately
qualified non-management User account or browser.

## Exact selected roots

All four roots use `SelectedCodeUnit`. The owner loader and planner accepted them
with limits 64 nodes, 128 edges, depth 8 and returned four groups. These are two
business operations seen in two repositories, not four proven end-to-end flows.

| Repository | Path | Selector | Unit ID | Logical Document / exact Version |
| --- | --- | --- | --- | --- |
| `2e44021b-a04d-488c-a8e8-36c487057728` | `src/main/resources/flow/login/handPwdLogin.flw` | `handPwdLoginFlow` | `99fed27d-ed8e-1867-9bb2-5b0159e552ea` | `7b84abba-60e8-34a3-ee47-c8ac512850dd` / `ab6fde7f-d85c-4f30-84ef-575bc792bde2` |
| `2e44021b-a04d-488c-a8e8-36c487057728` | `src/main/resources/flow/register/selfRegist.flw` | `selfRegist-flow` | `7177dfea-c0f5-ff7b-af10-7f1ee34ee1ff` | `5ba9eedb-e024-de47-45bc-4f310183f81c` / `ab9b59d6-cce6-450e-a34b-a7f4c872b348` |
| `9172b209-ed3a-415c-8d8b-c0ef1b2f18f6` | `src/main/resources/flow/login/handPwdLogin.flw` | `handPwdLoginFlow` | `9b6b5e4f-3e4a-7211-639b-fd41fd3387aa` | `62b0e5da-410e-01b6-5f11-bb4aa7f8faed` / `1461509f-fe6b-4cf9-ac1e-4c58cca5235e` |
| `9172b209-ed3a-415c-8d8b-c0ef1b2f18f6` | `src/main/resources/flow/register/selfRegist.flw` | `selfRegistFlow` | `7b983152-5326-a834-eec1-9634c4384886` | `c162bbec-92aa-ea8c-3f32-e119253f7fe0` / `38884f7e-d58b-436f-9a03-cb3fa769e3af` |

Accepted CL describes login success/failure branches, session creation, anti-fraud
handling and nested authentication; registration includes guest-session creation,
password setup, SMS verification and transaction initiation. These make the roots
useful candidates. This observation is from accepted CL, not a new independent
inspection of production source, and does not prove cross-repository edges.

## Blocking compatibility findings

1. **Synthesis cannot admit this input.** Canonical resolution corpus is 1,700,574
   UTF-8 bytes. Owner-loaded flow input with these four roots is **2,419,869 bytes**.
   `OctoberBusinessSynthesisExecutor` rejects inputs above **1,048,576 bytes** before
   dispatch. More time, output tokens, or money cannot fix this pre-dispatch refusal.
2. **Resolution silently has no eligible inquiries.** The canonical planner returns
   **0 inquiries and 355 NotEvaluated dispositions**. Accepted observation roles are
   free text (including `external-service-call`, `远程服务调用`, field/configuration
   roles); `OctoberResolutionPolicy` recognizes exact `caller` and `provider` only.
   File acceptance permits those free-text roles. No existing data is rewritten or
   relabelled here. Zero inquiries does not mean cross-repository resolution passed.
3. **Attempt accounting needs care before proposing spend.** Business currently uses
   the same `maximumAttemptTokens` value for the total lease reservation and output
   token cap. Input tokens are additional; an actual overrun marks usage uncertain
   in the scope. A cumulative admission limit is not a guaranteed monetary ceiling.

Recommended narrow correction scope, **not implemented or accepted by this note**:
Business should produce bounded, exact-version evidence projections for each
inquiry/flow while retaining complete owner authorization/dependency information;
separate input reservation from output allowance; and define source-evidenced
eligibility for retained free-text observations. No blanket role aliasing, inferred
accepted edges, weaker witnesses, new store, migration, corpus mutation or CL rescan.
The eligibility change affects shared Knowledge resolution semantics and requires
the management owner's decision before implementation. Request-shape and semantic
changes need focused regressions and independent review, without restarting broad
planning. The held competing continuation extension remains held.

## Execution proposal — blocked, not authorized

Use the implemented continuation only: `outputStage=cl-and-bl` and
`existingClPublicationId=fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`, exact roots above.
Use fresh protected policy/settings files, not the old consumed launch marker.
The runner itself generates a fresh `Guid.NewGuid()` RunId; it has no operator
RunId override. Record that actual ID with its Begin receipt and invocation/PID.
Do not invent a preconfigured RunId or reconstruct a process-local capability.

Selected protected provider: profile `selected`, model `deepseek-flash`, endpoint
`https://api.deepseek.com`, Chat Completions. Current Business calls are nonstreaming
JSON-object requests with no tools, SDK retries disabled, and 32,768 output tokens.
DeepSeek cap translation applies. Worker low-reasoning, staged correction, reasoning
replay and 393,216 output setting do **not** apply to this Business call path.
Business permits up to two transport attempts per item, sharing 120 seconds for
resolution or 180 seconds for synthesis; it has no Worker-style JSON regeneration.

Proposed outer limits after the blockers are corrected and remeasured: one invocation,
one worker, 14,400 seconds overall, 30-second graceful drain, 60 requests/minute,
at most 136 physical requests (64 inquiries plus four synthesis groups, each with
at most two attempts), and 4,500,000 aggregate tokens. Keep 32,768 output tokens and
64/128/8 graph limits. These are proposed caps, not an estimate or a spend grant;
the corrected input reservation must fit them or the proposal must return to the
user. No automatic retry of the overall invocation.

Business owns the exact generator PID/process tree, invocation-specific protected
logs, policy hashes and settlement evidence. No raw response/secret capture, shared
database migration, unrelated host or native source process is needed. Before send,
reauthorize all retained contributors for the selected provider. Only known settled
calls and writes allow the original owner to append CompleteBusinessFromCl. Lost
process, unknown usage/commit, unmatched Begin or cleanup uncertainty stops work;
no receipt clearing, takeover or implicit retry.

Missing runtime authority remains one consolidated grant: the single bounded paid
Business invocation and disclosure to that provider; append-only Business writes
to this exact database; owned process/log lifetime; and an authenticated ordinary
User reader identity (with explicit provisioning authority if none exists). Do not
request or consume that grant while the compatibility blockers remain.

## Qualification and evidence boundary

- Fresh focused SelfContained verification: **106/106**, zero failures/skips;
  `OctoberScanBusinessTransportTests`, `OctoberScanBusinessFinalizationTests`,
  `OctoberScanBusinessSettingsTests`; launcher run
  `7a1519deff5f4f1781bcf4734e7d6b94`. Controlled HTTP/fixtures, not paid generation.
- Known strict `OctoberGeneratedClReport` parser mismatch remains. It also requires
  a clean single-file CL-only report and cannot qualify this 77-file BL continuation.
  Preserve its assertions. Use exact owner receipts, BL publication and ordinary
  reader verification for real qualification; do not label a report parse as storage proof.
- After an authorized successful run, compare original CL membership/content digest
  and reopen its exact publication; verify a distinct BL publication with valid exact
  document/relationship dependencies and full contributor authorization.
- Ordinary User in Business mode must search/page/read full BL, see no CL metadata,
  and fail closed on unauthorized dependencies. Current operator readiness is not that test.
- Review login and registration separately: meaningful conditions, branches, errors
  and effects; source-evidenced directed cross-repository links; missing links and
  unevaluated observations explicitly disclosed; advisory context never promoted to fact.
- Record provider request/input/output usage, unknowns, terminal outcome, settlement
  and retained resources. No new provider/source execution, BL publication, browser
  qualification, schema change or production-code correction occurred in this checkpoint.
