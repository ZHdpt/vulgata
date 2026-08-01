# Contract: Closed Worker Agent Job and Result Unions

## Job

```text
WorkerAgentJobPacket
  common: jobId, systemId, repositoryId, scanRunId, profiledRevision, schemaVersion
  payload: oneof
    Pass1CodeLogicJobPayload
    Pass2BusinessLogicJobPayload
```

The discriminator and runtime variant must match. Pass 1 carries source
identities/hashes and allowed context, never source bytes. Pass 2 carries one
prepared-flow identity, ordered trace evidence, and exact Code Logic Document
Version projections. Unknown fields/values, duplicate IDs, noncanonical order,
absolute paths, and bound violations are rejected before dispatch.

## Result

```text
WorkerAgentResultEnvelope
  valid -> exactly one matching payload:
    Pass1CodeLogicDocumentResultPayload
    Pass2BusinessLogicDocumentResultPayload
  non-valid -> no payload; closed failure code + bounded Chinese message
```

Pass 1 may request only same-Repository `REFERENCES` targets present in its
server-built allowlist. Pass 2 supplies exact `GENERATED_FROM` targets and has
no field for a base Code Logic `REFERENCES` request. Identity echoes must equal
the dispatched packet.

## Bounds and strictness

- exact supported schema version; no forward-compatible catch-all;
- source slice <= 65,536 UTF-8 bytes and <= 2,000 lines;
- complete request <= 131,072 UTF-8 bytes;
- raw response <= 65,536 UTF-8 bytes; JSON depth <= 32;
- every field/collection also satisfies existing KnowledgeCommit bounds;
- strict UTF-8, no trailing JSON, duplicate/unknown property, unknown enum,
  duplicate ID, unstable order, secret, absolute host path, raw-source carrier,
  or raw diagnostic.

Provider schema enforcement is advisory defense-in-depth. Bounded streaming
preflight and semantic post-validation decide validity locally.
