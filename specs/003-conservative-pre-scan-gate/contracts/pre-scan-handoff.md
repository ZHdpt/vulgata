# Contract: Approved Pre-Scan Handoff

## Schema

Schema version: `1.0`

```text
PreScanHandoff
  resultId: Guid
  repositoryId: Guid
  scanRunId: Guid
  profiledRevision: lowercase Git object ID, max 64
  state: PreScanApproved
  schemaVersion: "1.0"
  approvedAt: DateTimeOffset
  approvalMessage: bounded Simplified Chinese string
  confirmedKinds: ordered distinct [Rpc | Http]
  totalConfirmedEvidenceCount: integer >= 0
  totalHintCount: integer >= 0
  totalWarningCount: integer >= 0
  evidenceTruncated: boolean
  confirmedEvidence: ordered PreScanClassification[]
  hints: ordered PreScanClassification[]
  warnings: ordered PreScanClassification[]

PreScanClassification
  kind: Rpc | Http | null (null only for a general warning)
  level: Confirmed | Hint | Warning
  confidence: High | Conservative | None
  rule: closed EvidenceRule
  provenance: closed EvidenceProvenance
  evidenceLocator: Repository-relative string, max 4096
  occurrenceCount: integer >= 1
```

`confirmedKinds` is derived from persisted closed flags, never from retained
classification samples, so it remains complete after truncation.
`confirmedEvidence` contains the bounded retained Confirmed samples, including
the deterministic reserved direct sample for every confirmed kind. Hints and
warnings remain separate and can never appear in `confirmedKinds`.

## Retrieval

The Core `IScanRunRepository` operation materializes by
`(repositoryId, scanRunId)` and returns the Core-owned immutable
`ApprovedPreScanHandoff` read model with bounded samples. Infrastructure never
returns an EF entity. Web's authorized `PreScanHandoffService` provides
system-scoped and standalone-Repository methods and maps the Core model to
Shared's `ApprovedPreScanHandoffDto`.

System-scoped behavior:

1. verify current System visibility using `IUserContext`;
2. verify Repository belongs to that System;
3. materialize the approved result;
4. re-check current System/Repository visibility;
5. return DTO or the same not-found outcome.

Standalone behavior reuses the current standalone Repository visibility rule
before materialization. Any API composition requires `ManagementAccess`.

Missing, hidden, revoked, or wrong-System outcomes disclose none of result
existence, Repository identity, evidence, warning, or timing.

## Redaction

The DTO permits only bounded closed values and Repository-relative locators in
confirmed evidence, hints, and warnings.
It forbids source content, absolute/UNC/host paths, working-copy root, secret,
raw exception/diagnostic, provider payload, and arbitrary type discriminator.

## Consumer boundary

Story 11.2 may consume this Pre-Scan Handoff only after authorization, schema
version validation, and acquisition/verification of the exact Profiled
Revision. If the available checkout differs, consumption fails closed even
when the working copy advanced after approval. The handoff authorizes no
dispatch itself and carries no
Worker Agent job, provider/consumer role, endpoint, REFERENCES, or
Cross-Repository Resolution instruction.

## Observability

When warnings exist:

`approvalMessage = 预扫描已批准并保留审慎警告——继续进入顺序演示运行器`

The same text is durable on the ScanRun. Structured logging includes only
RepositoryId, ScanRunId, state, schema version, and classification counts.
