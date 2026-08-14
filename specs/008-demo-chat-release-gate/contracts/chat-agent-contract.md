# Contract: Prepared Chat Evidence and Synthesis

## Core service boundaries

```csharp
public interface IChatEvidenceRetriever
{
    Task<ChatEvidenceResult> RetrieveAsync(
        ChatQuestionRequest request,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default);
}

public interface IChatEvidenceCurrentnessValidator
{
    Task<ChatCurrentnessValidationOutcome> ValidateCurrentAsync(
        ChatCurrentnessValidationRequest request,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default);
}

public interface IChatAgentDispatcher
{
    Task<ChatSynthesisOutcome> GenerateAsync(
        ChatSynthesisRequest request,
        CancellationToken cancellationToken = default);
}
```

All three interfaces and all input/output records are provider-independent and
live in Core. Infrastructure alone implements the relational evidence retrieval
and currentness validation. Agents implements only provider resolution, prompt
loading, dispatch, and closed selection-only result validation; it never queries
EF. Web composes and orders them.

## Evidence tool request

`ChatQuestionRequest` contains:

- non-empty request ID;
- closed `Business|It` mode;
- non-empty System ID;
- exactly two distinct Repository IDs, canonicalized;
- normalized 1-2,000-character question.

The tool performs no automatic scope expansion. It requires:

1. both Repository IDs are present in `RepositoryAccessScope`;
2. live access succeeds for both IDs;
3. both Repositories belong to the requested System;
4. current accepted-profile exact evidence connects current Document Versions
   in the requested direction;
5. every returned Section/Anchor/flow/provenance fact is current, complete, and
   inside the pair.

Closed outcomes:

- `Found(packet)`;
- `NotFoundOrUnauthorized` (shared missing-equivalent outcome);
- `InvalidRequest` (safe local input error);
- `IncompleteOrStale` (safe non-identifying evidence error).

No outcome contains existence details, database IDs, SQL/provider diagnostics,
credentials, absolute paths, source text outside bounded Sections, or partial
packets.

## Canonical Evidence Packet

The tool assigns deterministic request-local handles (`d1`, `s1`, `a1`, `r1`,
and so on) after canonical ordering. The packet has hard maxima: five
Documents, twenty Sections, thirty-two Source Anchors, twelve flow steps, and
the bounded relationship/provenance set needed by the prepared flow. It is
serialized with one exact schema version and fingerprinted with SHA-256.

The server extracts Candidate Claims before provider dispatch. A candidate is
an exact contiguous persisted `ContentMarkdown` paragraph/list-item body with
retained offsets and SHA-256. Business candidates are safe current BL
`Overview`/`ProcessStep` spans; IT candidates are current CL
`Overview`/`ProcessStep`/`TechnicalDetail`/`Evidence` spans. Fenced code,
tables, raw HTML, links, restricted references, missing anchors, and unsafe or
over-bound spans are excluded. The server applies the Business
safe-text/no-code-locator predicate before dispatch and revalidates that same
predicate immediately before render.

Each candidate has an immutable support signature over mode, current Document
Version, stable Section, offsets, exact-text hash, canonical Source Anchor
handles, and required authorized relationship handles. Provider view includes
the normalized question plus candidate handle and exact text for relevance
selection. Server backing retains
all identity/locator/support data. The provider cannot author factual text or
choose citations.

## Post-provider currentness contract

After the selection-only provider result passes `ChatAgentResultValidator`, Web
builds a bounded `ChatCurrentnessValidationRequest` from server-retained state.
It contains the request ID and Evidence Fingerprint; selected System/two-
Repository scope; every selected Logical Document/expected-current Document
Version pair; every selected Stable Section, exact span offsets/length/text
SHA-256; the complete fixed same-version Source Anchor identities, safe
Repository-relative paths, positive ranges, and source hashes; and every
required directed `REFERENCES`/`GENERATED_FROM` Edge, endpoint, exact-evidence
profile, and direction. The provider neither sees nor supplies these fields.

`IChatEvidenceCurrentnessValidator.ValidateCurrentAsync` is implemented only by
Infrastructure. It opens one bounded serializable read and, with the live
`RepositoryAccessScope`, proves all of the following as one whole-answer
predicate:

1. each Logical Document still points to the exact expected current Document
   Version and the version is not superseded;
2. each Stable Section still belongs to that version and still contains the
   exact retained substring at the exact offsets and SHA-256;
3. each fixed Source Anchor still exists, is same-version and in a selected
   Repository, retains safe Repository-relative support, and has
   `LineStart > 0` and `LineEnd >= LineStart`;
4. each required current `REFERENCES`/`GENERATED_FROM` Edge still has the exact
   type, direction, current endpoints, authorized endpoint Repositories, and
   accepted exact-evidence profile/fingerprint where required.

The only success is `Current`; every missing, stale, changed, unsafe,
unauthorized, out-of-scope, extra, or inconsistent condition is
`MissingOrStale`, mapped to the existing bounded Chinese whole-answer failure
with zero claims/citations. The retrieval transaction ends before provider
dispatch. The currentness transaction starts only after provider selection
validation and ends before final Repository authorization; no transaction is
held across provider latency.

## Chat provider resolution

`ChatAgentProviderResolver` applies the existing assignment rule:

1. exact System override for `AgentType.Chat`;
2. otherwise first existing global provider whose default type is
   `AgentType.Chat`;
3. require Chat Completions support, valid HTTP(S) endpoint, non-empty model
   name, and encrypted key;
4. decrypt only at dispatch through `ISecretEncryptionService` and never retain
   plaintext after the call.

No Worker provider fallback, retry, or dynamic provider failover is allowed.

## Prompt contract

Two versioned embedded resources are required:

- `Vulgata.Agents.Prompts.chat-business.v1.md`;
- `Vulgata.Agents.Prompts.chat-it.v1.md`.

Both receive the normalized question and mode-eligible candidates, require an
explicit supported/unsupported relevance decision, preserve uncertainty, and
permit selection/order of supplied handles only. Nonsense, unrelated, and
unsupported questions return `unsupported` with zero selections. Prompt
absence, duplicate resource, malformed resource, or wrong mode mapping fails
before provider use.

## Closed provider JSON

```json
{
  "schemaVersion": "prepared-chat-answer-v1",
  "requestId": "request-local-id",
  "mode": "business",
  "evidenceFingerprint": "64-lowercase-hex",
  "answerability": "supported",
  "selections": [
    {
      "candidateHandle": "k1",
      "order": 1
    }
  ]
}
```

An unsupported question returns the same echoes with
`"answerability": "unsupported"` and an empty `selections` array.

Validation rejects unknown/missing/duplicate properties; wrong version,
request, mode, or fingerprint; non-contiguous order; zero or more than twelve
supported selections; any unsupported result with a selection; duplicate,
foreign, unrelated, contradictory-decoy, stale, or
out-of-allowlist handles; changed retained span hash/support signature;
restricted packet evidence; or a Business candidate that no longer passes the
retained mode/safe-text predicate. Post-provider database currentness is owned
by the separate Infrastructure validator, not this Agents validator.

The provider request contains the normalized question plus candidate handles
and exact text. The result schema permits only the
schema/request/mode/fingerprint echoes, answerability, and unique ordered
Candidate Claim handles shown above. It contains no
provider-authored claim text, citation handle or selection, locator, narrative,
diagnostic, or extension field. The ordered selection list is the complete
provider result; the server materializes every Material Claim from retained
exact text and fixed support. The validator never repairs, drops, or partially
accepts selections.

Adversarial coverage is mandatory: otherwise schema-valid handles from a
foreign packet containing unrelated text and from a contradictory decoy
candidate, a candidate paired with a swapped/tampered server support record,
and a Section mutated after extraction must all reject the whole answer and
render zero claims/citations. This is deterministic support by persisted-span
identity, not model-rated semantic similarity.

## Web composition contract

`PreparedChatCoordinator.AskAsync` executes in this exact order:

1. validate explicit mode/System/two-Repository/question bounds;
2. verify current access to both Repositories and common System;
3. call the evidence tool once with a live verifier-bearing
   `RepositoryAccessScope`;
4. extract the closed mode-appropriate Candidate Claim allowlist, applying the
   Business safe-text/no-code-locator predicate before dispatch;
5. call the Chat dispatcher once with the normalized question and candidate
   handles/exact text;
6. validate the entire selection-only result against the retained packet,
   recompute retained hashes, and revalidate Business safe-text/no-code-locator
   eligibility without querying EF;
7. for an unsupported result, reverify Repository access and return the bounded
   Chinese no-information response with no claims/citations;
8. for a supported result, call `ValidateCurrentAsync` once so Infrastructure performs the bounded
   serializable current Document Version/Stable Section/exact span/Source
   Anchor/relationship support proof;
9. reverify access to the same two Repositories immediately after currentness
   validation;
10. map each candidate's fixed support to server-created citations;
11. atomically return the complete Grounded Answer.

Any failure from steps 2-10 returns one bounded Chinese state and no answer.
Cancellation clears pending state and publishes no provider partial. The page
never retains a prior answer while a new request is in progress or failed.

Barrier-controlled integration tests pause the deterministic dispatcher after
step 3 has completed and step 5 has begun. During the pause they independently
(a) supersede a selected Document Version and move its Logical Document current
pointer, (b) make a fixed Source Anchor unavailable/unsafe, and (c) remove or
alter required directed `REFERENCES`/`GENERATED_FROM` support using test-only
database fault injection. Releasing the provider must allow step 6 to pass and
force step 7 to return `MissingOrStale`; steps 8-10 must not publish any claim
or citation. The barriers assert each mutation occurred strictly between
retrieval and currentness validation.

## Render contract

- Business: claim text contains no inline locator; each claim shows one or more
  Chinese Document citation controls; expanding a control reveals authorized
  Repository-relative path and inclusive line range.
- IT: each claim shows Document citations; authorized Repository-relative path
  and inclusive line range may be visible inline.
- Both: no absolute host path, URI, credential, raw source, raw provider error,
  internal database ID, unauthorized label, or restricted relationship detail.
- The UI has no upload control, automatic-scope option, graph/dashboard, saved
  history, or live notification behavior in this slice.

## Planned-change status

This new evidence interface is an agent retrieval tool and therefore requires
independent plan acceptance followed by explicit user planned-change approval
before tasks are generated. Approval does not waive TDD, review, security,
migration/no-pending, E2E, regression, or Docker gates.
