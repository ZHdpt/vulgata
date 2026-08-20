# API Contract: Start Paired Prepared Scan

Prepared Scan Manifest and File Code Unit follow their authoritative glossary entries. API implementation/qualification is downstream of canonical audit GREEN and, for XML-observing scope, XML split GREEN; neither may run in parallel with those producers.

## Endpoint

`POST /api/systems/{systemId}/prepared-scans`

Requires authentication and `ManagementAccess`. Repository lookup is filtered through the caller's SystemOwner scope.

## Request

```json
{
  "repositories": [
    { "repositoryId": "guid", "gitRef": "refs/tags/release-a", "profiledRevision": "40-lowercase-hex" },
    { "repositoryId": "guid", "gitRef": "refs/tags/release-b", "profiledRevision": "40-lowercase-hex" }
  ]
}
```

Exactly two distinct entries. Unknown properties, unsafe refs, nonexact commits, duplicate identities, and invalid GUIDs are validation errors. No path/runtime/catalog/timeout override is accepted.

## Success

`201 Created` for a new pair or `200 OK` for exact replay. Response contains coordination ID, PairSelectionFingerprint, PairPackageFingerprint, two Repository/Scan Run/Profiled Revision/ManifestFingerprint bindings, each Repository's `codeGraphProofIdentityHash`, `javaReadinessIdentityHash`, and `preparedScanContextIdentityHash`, combined selected count, and each entry's scope-named `manifestOrdinal` and `pairSelectionOrdinal`. Repository response order uses lowercase UUID `D` text ordinal comparison. Legacy ContextSnapshotId, capture time, proof durations, and telemetry are not identity projections and are not returned. The response contains no source, host path, tool output, or dispatch/job state.

Any approved audit projection is read from persisted canonical preimages. A `Selected` audit exposes its File Code Unit identity fingerprint and named pair-selection ordinal; `Eligible` exposes the fingerprint with null pair ordinal; other outcomes expose both as null. The API never infers association from path or trusts a stored DetailFingerprint without reconstructed preimage validation.

Selected File Code Unit projections join through the prepared-only Manifest Entry principal, not nullable CodeUnit manifest/ordinal columns. The entry supplies manifest membership, scores, selection reason, and both ordinals; historical CodeUnits without an entry cannot appear in a Story 12.2 approved response.

## Errors

- `400` ProblemDetails: malformed/unsupported request.
- `401` unauthenticated; `403` authenticated policy failure.
- `404` System/Repository absent or outside ownership scope, without distinction.
- `409` changed existing package, stale version, moved ref, or canonical conflict.
- `422` safe bounded preparation rejection such as analysis not ready, insufficient 50-unit scope, or no deterministic Java readiness file.
- `503` configured analysis runtime unavailable/integrity mismatch.

Cancellation and infrastructure failures never expose an approved partial package. No response starts a Worker.
