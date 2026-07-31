# Internal Contract: Current Knowledge Retrieval Projection

This contract defines the finite Core read model returned by Infrastructure for
Story 11.1. It is not a public API, UI model, Chat Agent tool, Worker Agent
tool, or authorization policy.

## Request boundary

```text
GetCurrentAsync(
  logicalDocumentId: non-empty Guid,
  scope: RepositoryAccessScope[1..256],
  requestedStableSectionIds: distinct Guid[0..100])
  -> CurrentKnowledge?
```

The source Repository must be in the current scope. Missing and unauthorized
source Logical Documents both return `null`; no existence or rejection detail
distinguishes them. Scope is supplied and evaluated on every call. No tracked
entity or cached authorization decision crosses Infrastructure.

## Fully authorized result

`CurrentKnowledge` contains one current Document Version, its safe source
anchors, authorized relationships, bounded Flow Groups/Traces/Steps, one
`CurrentIndexEntry`, and only explicitly requested sections in canonical order.
Every value is a materialized Core type. It contains no EF entity, `DbSet`,
`IQueryable`, absolute host path, source text, secret, or provider diagnostic.

An authorized relationship contains its exact Edge type and source/target
Document Version identities plus target Repository identity. A fully authorized
flow step contains its exact Document Version identity and bounded
description. A fully authorized section contains only authorized Document
Version references and accepted source-anchor IDs.

## Restricted target marker

When the source Repository remains authorized but a target Repository is not
in current scope, the relationship is represented only as:

```text
RestrictedRelationship
  isRestrictedRelationship: true
```

The marker has no Edge ID/type, target Repository, Logical Document, Document
Version, title, path, anchor, content, term, or behavioral field. Restricted
flow steps set `isRestrictedRelationship = true` and return no Document Version
identity or description. Restricted section Document references return no
target IDs and set `hasRestrictedDocumentReference = true`.

## Fail-closed textual projection

Canonical and retrieval text can contain target-derived behavior without
carrying a target identity. Selective string scrubbing is therefore not an
accepted security boundary.

If any loaded Edge, flow step, or section Document reference targets a
Repository outside current scope, the complete materialized result applies
these deterministic rules:

```text
CurrentKnowledge.contentJson = ""
CurrentKnowledge.contentMarkdown = ""

CurrentIndexEntry
  summary = ""
  keySymbols = []
  businessTerms = []
  technicalTerms = []
  searchText = ""

each returned CurrentDocumentSection
  heading = ""
  contentMarkdown = ""
  searchText = ""
```

A section with a restricted Document reference additionally returns empty
`sourceAnchorIds` and `authorizedDocumentVersionIds`, with only
`hasRestrictedDocumentReference = true`. Authorized source-anchor read models
remain bounded and contain locators/hashes rather than source text.

These are read-time projection rules. They never mutate the immutable Document
Version, its historical evidence, `IndexEntry`, or `DocumentSection` rows.
Restoring target authorization on a later call permits the fully authorized
projection again.

## Bounds

- at most 512 source anchors;
- at most 1,024 authorized-or-restricted relationships;
- at most 32 Flow Groups, 64 Flow Traces per group, and 256 steps per trace;
- exactly one bounded current index entry;
- at most 100 explicitly requested sections.

Over-limit request input is rejected before query materialization. Stored
values beyond accepted output invariants fail closed; they are never silently
expanded into an unbounded result.
