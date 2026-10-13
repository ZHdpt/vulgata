# BL self-review and CL-referenced source retrieval

## Requested boundary

The user requests runtime BL self-review instead of a separate critic context,
an explicit prohibition on guessing/inventing facts, then a retrieval tool available
from the first synthesis step. Independent Codex implementation review remains.
This amendment does not activate a broader planning workflow or authorize a paid run.

The first correction replays the original synthesis packet and exact latest draft
as user/assistant context for self-review. The application still checks complete
verdict coverage, exact draft binding, literal evidence references and publication
eligibility. One correction and full self-review of the corrected draft remain.
This is a change of review context, not a claim that self-review is more reliable.
The prompt forbids invented field meanings from abbreviations and requires plain
uncertainty instead of exposing implementation identifiers in Business prose.

## Concrete retrieval proposal

Expose one read-only `read_referenced_source` function in the first BL synthesis
request, subsequent investigation and self-review. Its arguments are an opaque
source-reference handle and a bounded page position. Each handle binds the full
original repository/root/PreparedVersion/file digest/range tuple. Host-generated handles map to
the union of `EvidenceReference.Source` anchors actually cited by the current flow's
member CL claims. Merely belonging to the full 163-document dependency ledger does
not permit a read. No model-supplied path, repository, revision, glob or URL is accepted.

Return only the allowed source span, exact original source identity/digest, line
bounds and an explicit complete/limited/unavailable status. Pagination stays inside
the cited ranges; file headers, neighboring code and referenced callees outside
those ranges are not implicitly allowed. No directory/search/shell/network tool.
Source text and comments are untrusted evidence, never instructions.

Use the contained `PreparedSourceBinding`/`IPreparedSourceReadContext` adapter with
a separately issued Business-purpose grant and live authorization. Verify the full
file bytes against the original CL `PreparedOrigin.SourceDigest` before returning
any slice; enforce the existing bounded two-pass/identity checks against mixed reads.
Fresh grant identity is not a replacement for the original evidence's PreparedVersion.
Never read the current checkout as a fallback for missing or changed exact bytes.
No rescan, new CL acceptance, CodeGraph/JDT startup, or publication mutation is needed.

Start with caps per flow: 12 source calls, 32 KiB returned per call, 192 KiB returned
in total, 4 MiB per verified file and 16 MiB total examined bytes (both verification
passes and every repeated read count), all within the
existing synthesis deadline. Reject exhaustion explicitly. Allow at most six tool-bearing model rounds per flow across generation, correction
and self-review, and at most 16 physical model requests including existing final-JSON
corrections/reviews; lower run request/token ceilings still take precedence. Account
for every request, tool schema, function arguments/result and retained conversation.
Neither the source-call/model-round counters nor structural correction allowances
reset on a retry, stage change or new source grant. Successful pages confirm only
the returned portion of the permitted cited span, never whole-file completeness.
Structural correction allowances must not be consumed or reset by source retrieval.
Before final JSON, disable tools and retain the existing structural correction path.
Reasoning/tool replay must follow the selected provider protocol; no automatic retry
after unknown usage or indeterminate settlement.

Reauthorize the complete contributor ledger and exact run/purpose before and after
every awaited read and before model disclosure. Capture the bounded tool result and
its binding in protected diagnostics; never print raw source into ordinary logs.
All retrieved anchors must already be represented in owner-held CL contributors.
BL still cites exact accepted CL claims and preserves their qualification and gaps.
If raw source contradicts CL or supports a genuinely new assertion absent from CL,
report the discrepancy/unknown; this tool is not an acceptance or CL-repair bypass.

## Owned edits and compatibility checkpoint

- Agents: `OctoberBusinessSynthesisExecutor.cs`, `OctoberBusinessSynthesisRequests.cs`,
  `OctoberBusinessSemanticReview.cs`, the Business prompt and a narrow injected
  source-read capability/tool adapter. No direct filesystem access in Agents.
- Generator Business seam: `OctoberGenerateRunner.Business.cs`, existing-CL
  composition, `Program.cs`, Business settings and a contained source adapter.
  Business-only tool-capable transport in `OctoberWorkerClientFactory.cs`; preserve
  the CL streaming path and existing resolution policy.
- Shared authorization scope requiring explicit approval: append a Business read
  purpose in `PreparedSourceContracts.cs`; add an explicit issuance port in
  `OctoberLocalAuthorityContracts.cs` and its implementation in
  `Vulgata.Web/Data/OctoberLocalAuthority.cs`; update only source-read validation in
  `OctoberPreparedSourceAuthorityVerifier.cs` and `PreparedSourceContextIssuer.cs`.
  Existing Worker-purpose behavior remains unchanged; tool-workspace issuance does
  not acquire Business purpose. This is not shared Web composition or UI work.
- No new canonical Knowledge content/schema or migration is proposed. Verify that
  persisted approval-purpose validation accepts the additive enum before coding;
  if a migration is necessary, stop and present its exact scope first.

Checkpoint 1: self-review/no-guess correction with focused controlled tests and
independent code review. Checkpoint 2: source allowlist/authority adapter tests for
out-of-range/foreign handles, path attacks, changed bytes, revocation, cancellation
and limits. Checkpoint 3: controlled real-provider-protocol tool loop, complete
usage accounting, structured finalization and store regression tests. Review the
combined implementation before any separately authorized real-data/provider trial.

Feasibility is confirmed at the API/design level, not runtime-qualified. Exact
retained source availability and disclosure authority must be established before
a real run. Missing framework/SQL evidence outside the CL anchors remains a gap.

The repository AGENTS planned-change rule is triggered by a new agent tool and
materially changed dispatch behavior (PRD FR-4.5, FR-4.9 and live authorization
FR-1.4 remain preserved). Obtain independent plan review and explicit approval for
this concrete shared-file/tool scope before implementing retrieval. Do not add
per-checkpoint human gates after that approval. No paid calls or retained database
writes are authorized by this proposal.


## Existing authorization comparison (verified in this Business checkout)

| Existing mechanism | What it authorizes/provides | Difference from the proposed tool |
| --- | --- | --- |
| `BusinessDisclosure` / `IOctoberResolutionDisclosureAuthority` | Live run/corpus and complete contributor disclosure checks | No raw-source context, file read or CL-span enforcement; keep and reuse these checks. |
| `KnowledgePurpose.Synthesis` | Domain purpose enumeration | Not a source-read grant; the ordinary disclosure policy only accepts Reader/Chat/Graph selections. |
| `PreparedSourcePurpose.WorkerSourceRead`, `PreparedSourceBinding`, `IPreparedSourceReadContext` | October live approved contained source reads with paths held by trusted host | Existing lower-level adapter is reusable; current issuer/verifier explicitly accept Worker purpose only, and its generic reader is broader than the requested CL-anchor tool. |
| `SourceAuthorizationPurpose.Read`, `IRetainedSourceApplication` / `ISourceReadContext` | Existing generic repository + ProfiledRevision retained-source reads | Not Business-specific and not currently bound to this October existing-CL synthesis route. October PreparedVersion is not a substitute for its ProfiledRevision. Requires proven owner mapping before reuse as a backend. |
| Proposed `read_referenced_source` | Model-visible opaque handles for already cited exact member-CL spans | Combines an applicable byte-read grant, CL allowlist, immutable-byte verification, complete disclosure checks and tool/model budgets; not a competing store or authorization bypass. |

No implemented Business-specific source-read purpose was found in the assigned
checkout. The distinct Business purpose above is proposed, not existing. Reuse the
existing contained byte-read implementation and contributor checks; add only the
missing purpose-bound issuance and restrictive model-facing wrapper. Do not infer
source-read permission from corpus disclosure or from the generic Synthesis enum.
The user's response to the approval question requested this comparison, so it is
**not approval** to implement retrieval/shared authorization changes.

## First correction verification

The self-review/no-guess correction is implemented. Generation remains a two-message
request; self-review/correction use system, original evidence packet (user), exact
latest canonical draft (assistant), then the phase/digest/findings envelope (user).
The packet/draft are not duplicated inside the envelope. Full message accounting,
strict verdict/citation checks, one correction, protected capture and independent
store validation remain. This is reconstructed synthesis context, not preservation
of hidden reasoning or an independent critic session.

Behavioral RED: three selected cases fail with the previous detached two-message
context (`TestResults/isolation-68b29d15670e4f858f20ccaa45909dc3/results.trx`).
An earlier test lambda-name compilation collision was fixed and is not RED evidence.
Focused GREEN: 339/339, zero failures/skips, including synthesis/capture/finalizer/
planner/publication/controlled transport tests
(`TestResults/isolation-c95e437cbae042c4986c5ead23c1fb08/results.trx`).
Independent final implementation review accepted. Retrieval plan review also accepted
its clarified design, but implementation approval is pending the comparison above.
No provider call, raw retained-source read, database write or publication change was
performed for this correction; live semantic reliability remains unqualified.
