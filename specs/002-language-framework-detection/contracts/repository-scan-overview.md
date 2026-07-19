# Contract: Authorized Repository Scan Overview

The existing scan-run POST/GET routes and
`RepositoryDetailDialog` are extended. No parallel endpoint or authorization
policy is created.

## Response extension

`ScanRunSummaryDto` adds:

```text
languages:
  - language: normalized provider token
    distinctFileCount: positive integer

frameworks:
  - frameworkName: normalized catalog fact
    codeUnitCount: positive integer
    strongestConfidence: High | Medium
    totalFactCount: positive integer
    evidenceSamples:
      codeUnitId
      confidence
      detectionSource

unknownLanguage:
  distinctFileCount: non-negative integer
  paths: sorted unique Repository-relative paths

frameworkEvidenceAudit:
  sampledOutcomeCount
  totalOutcomeCount
  paths/reasons: bounded safe samples
```

Collections are deterministically sorted: language/framework/path by ordinal
identifier/path; evidence by framework, Code Unit ID, confidence rank, then
source. Counts are complete. Framework evidence samples and framework-evidence
audit samples share a maximum of 100 details and report whether sampling
occurred. Unknown-language paths are not sampled: the response includes all
sorted unique paths recorded for the ScanRun, with at most 10,000 paths and
16 MiB of combined UTF-8 path data under the accepted Repository scale. A
larger candidate set is rejected before profiling rather than silently
omitting audit paths.

The response contains no source content, absolute host path, raw manifest
value, raw provider row/query, or provider diagnostic.

## Authorization

- Existing route groups continue to require `ManagementAccess`.
- System Repository operations retain `GetVisibleByIdAsync` checks before
  profiling/read and the completion-time visibility recheck.
- Standalone Repository visibility behavior remains unchanged.
- Missing, hidden, or access-revoked Repository/ScanRun returns the established
  not-found response and exposes no count, language, framework, path, or
  timing detail.

## Chinese UI

New labels and messages are Simplified Chinese, including:

```text
语言分布
框架
置信度
检测依据
未知语言文件
框架证据警告
```

Provider language tokens, normalized framework names, and exact evidence
locators are technical facts and are rendered without translating or changing
their identity.
