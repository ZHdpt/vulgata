# Same-run operator root selection v1

Integrator relayed the user's October 9 approval for sequential explicit multi-file processing, bounded operator selection within the original run, and BL qualification. This contract adds no concurrency, durable recovery, automatic root discovery, provider allowance or schema. Root owns shared fixture/host integration; Scan owns this command-local interaction; Business owns canonical Knowledge; Explorer owns ordinary-user BL qualification assertions.

## Configuration and compatibility

Default `October__BusinessRootSelectionMode=exact-settings` preserves the existing `October__BusinessSettingsFile` roots/limits/maximumAttemptTokens behavior. Omission of the mode keeps that default.

For explicit operator selection, set:

- `October__BusinessRootSelectionMode=operator`.
- `October__BusinessRootSelectionDirectory` to an absent absolute child path beneath an existing app-owned parent, outside all source, native-state and log roots. Host creates it exclusively; preexisting/reparse/replaced boundaries fail closed.
- `October__BusinessRootSelectionTimeoutSeconds` to an explicit integer 1–300. This is further limited by the original run/session remaining lifetime, never a renewed allowance.
- The existing Business settings document must contain `roots: []` plus its existing finite limits and maximumAttemptTokens. In operator mode this is configuration exclusivity; only an explicit empty operator response selects no roots. Nonempty configured roots conflict with operator mode and are rejected.

CL-only ignores all Business selection options. No stdin changes are needed. Stdout remains the single terminal report. Candidate metadata is not written to logs or the final report.

## File exchange

The host issues one `root-selection.request.json` atomically using exclusive creation. A caller writes a bounded temporary response in the same directory and atomically renames it, without overwrite, to `root-selection.response.json`. Do not write the final response file incrementally.

The UTF-8 request is at most 1 MiB, with at most 1,024 candidates; oversized catalogs fail rather than truncate. CamelCase fields are:

```text
version: 1
requestId: fresh GUID challenge
runId: original Generate run GUID
corpusId: exact owner frozen-corpus GUID
expiresAtUtc: bounded original-run/session deadline
candidates: [
  { unitId, document: { batchId, logicalDocumentId, documentVersionId },
    repositoryId, relativePath, semanticSelector }
]
```

Candidates come exclusively from the authorized owner-loaded planning input after freeze and B1. They contain no source text, credentials or canonical database identity. Their IDs and versions are actual accepted identities, not caller-generated identifiers. The complete candidate contributor set is reauthorized before display. Ambiguous/duplicate candidate identities fail closed.

The response is at most 64 KiB:

```text
{ version: 1, requestId, runId, selectedUnitIds: [GUID, ...] }
```

All fields are required; unknown/duplicate fields, trailing content, wrong version/challenge/run, duplicate or foreign/stale IDs, more than 256 selections, and malformed input fail closed. `selectedUnitIds: []` explicitly selects none. An absent response, null/default selection, timeout or cancellation never means an empty selection. No all-units or first-unit fallback exists.

Host maps each selected ID to the exact held candidate's UnitId, DocumentVersion and SemanticSelector using `SelectedCodeUnit`. The response cannot replace those values. The original authority, all contributors and monotonic run/session deadline are checked after waiting/parsing and before finalization admission. B1 is not reordered and all earlier costs remain charged to the original scope.

## Lifetime and cleanup

Interaction is asynchronous and cancellation-aware. The actual I/O/interaction task remains tracked until settlement, including when a bounded caller stops waiting. No database transaction or provider permit is held during the wait. Directory/ancestor identity remains pinned through response handling and owned cleanup; path replacement or reparse traversal is not accepted. Candidate request/response metadata is removed on completion, cancellation, timeout and authority loss. Cleanup touches only this interaction's fixed owned files and directory; unexpected foreign entries are not recursively deleted.

The command-local C# data shapes are `OctoberOperatorRootCandidate`, `OctoberOperatorRootSelectionRequest` and `OctoberOperatorRootSelectionResponse` in the Generator assembly. They are transport objects, not competing Core/Knowledge contracts. Fixture authors may consume the JSON fields above and explicitly choose offered identities; fixture choices do not establish production automatic root selection.
