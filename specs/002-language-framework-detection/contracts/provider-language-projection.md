# Contract: Provider Language Projection and Completeness

This extends, and during implementation requires a correction to,
`specs/001-codegraph-extraction/contracts/repository-profiling-api.md`.
It does not add a third query, pagination, provider MCP/tool access, retry, or
provider fork.

## Private gateway environment

Start the unpublished loopback gateway with exactly this additional
process-local environment entry:

```text
TOOL_RESULT_LIMITS={"execute_cypher_query":250001}
```

Do not set another tool limit or export the setting globally.

## Projection A extension

Projection A adds one exact returned alias:

```cypher
node.lang AS parser_language
```

The implementation owns one canonical query constant and SHA-256 used by
production and final-image verification. The full row is:

```text
record_kind
provider_kind
path
name
start_line
end_line
parser_language
```

Every field is required exactly once. `parser_language` is a JSON string or
null. File null is valid transport. Any present `parser_language` with the
wrong JSON type or an invalid normalized value rejects the complete Projection
A/provider result atomically. `Filtered/UnknownLanguage` is reserved for no
`parser_language` observation in an otherwise valid complete response. Two
distinct valid normalized values for one path reject the provider result
atomically.

Projection B and `cgc-v1` endpoint identity remain unchanged.

## Envelope completeness

Within `data`, `truncated` and `result_limit` are reserved fields:

- normal complete response: both absent;
- either field present: reject the provider result atomically;
- wrong type, duplicate field, or inconsistent pair: reject atomically.

They are not ignored as additive metadata. `record_count` must still equal
`results.length`; combined Vulgata rows must be at most 250,000.

## Reconciliation

For every eligible candidate path:

- exactly one structural parser identity: retain projected Code Units on that
  path and assign the identity to all;
- none: one `Filtered/UnknownLanguage`, no Code Unit/Edge/unresolved
  relationship;
- conflict: atomic provider failure.

Extensions, generic File presence, names, source symbols, framework catalog,
and LLM output are forbidden language authority.

## Final-image proof

The required gate extends
`FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract`
and must fail when the completion-lane image/environment/evidence is absent.
It proves:

- exact final/verifier image, provider 0.5.1, lock, wheel, and canonical query
  hashes;
- exact `parser_language` alias and structural-node property evaluation;
- all Code Units in one path receive one token;
- a supported empty fixture exposes no structural identity and maps to one
  `Filtered/UnknownLanguage`;
- each projection returns more than 100 rows without truncation metadata;
- injected `truncated`/`result_limit`, query drift, conflict, bound overflow,
  cleanup failure, or forbidden retention fails atomically;
- evidence retains no raw query, row, source, path, filename, symbol, scalar
  provider value, diagnostic, credential, or token.
