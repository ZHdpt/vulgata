---
stepsCompleted: [1, 2, 3, 4, 5, 6]
inputDocuments:
  - specs/008-demo-chat-release-gate/plan.md
  - specs/008-demo-chat-release-gate/contracts/release-verification-contract.md
  - tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs
workflowType: research
lastStep: 6
research_type: technical
research_topic: CodeGraphContext 0.5.1 first-index duration for the Story 11.5 sample repositories
research_goals: Select a defensible timeout for the unchanged approved Scan strategy before retrying T072
user_name: zhdpt
date: 2026-08-10
web_research_enabled: true
source_verification: true
---

# CodeGraphContext 0.5.1 First-Index Duration Research

## Executive Summary

CodeGraphContext does not publish a reproducible average-index-time benchmark for
version 0.5.1. Its official documentation confirms that initial indexing scans a
repository and that `.cgcignore` is the supported performance control for excluding
large dependency, build, generated, or otherwise irrelevant trees. Public reports
show a wide range, from about one hour for a roughly 4 GB codebase to multiple hours
for large workspaces and Java dependency resolution. These reports are anecdotal and
cannot be treated as a service-level guarantee.

The accepted Story 11.5 samples contain 2,556 and 6,771 tracked files, respectively,
and neither accepted commit contains `.cgcignore`. The first live same-image run also
remained CPU-active in the first product profiling operation after 30 minutes, with
no Worker/provider traffic. The existing 30-minute total provisioner timeout is
therefore disproved by direct observation, and the product default of 30 minutes per
Repository is not a safe final-run bound.

For the unchanged approved Scan strategy, use **14,400 seconds (240 minutes) per
Repository** and **510 minutes for the complete provisioner process**. The outer
envelope covers two sequential Repository profiles plus 30 minutes for migrations,
Pass 1, Exact Cross-Repository Resolution, runtime prepared-flow binding, Pass 2,
and atomic result publication. This is a timeout-only correction; it does not change
parsing, filtering, scheduling, grouping, retry, or dispatch behavior.

## Table of Contents

1. Scope and methodology
2. Official behavior and tuning guidance
3. Public timing observations
4. Story 11.5 sample scale
5. Timeout derivation
6. Integration and operational design
7. Risks, limitations, and deferred optimization
8. Recommendation

## 1. Scope and Methodology

The research combines:

- official CodeGraphContext indexing, CLI, troubleshooting, architecture, and
  `.cgcignore` documentation;
- the CodeGraphContext 0.5.1 PyPI release record;
- public user reports, classified as low-confidence observations rather than
  benchmarks;
- exact tracked-file counts at the two accepted sample commits; and
- the observed Story 11.5 attempt in which the first Repository profile was still
  CPU-active at the existing 30-minute outer deadline.

Searches targeted CodeGraphContext indexing performance, large Java repositories,
Kuzu-backed indexing, `.cgcignore`, and version 0.5.1. A similarly named but unrelated
`codegraph` product was excluded.

## 2. Official Behavior and Tuning Guidance

The official indexing guide describes `cgc index` as the initial graph-build step and
documents `.cgcignore` ingest filters. The CLI reference likewise identifies
`.cgcignore` as the supported exclusion mechanism. The troubleshooting guide says
slow or out-of-memory indexing commonly results from scanning large build,
dependency, or compiled directories and recommends appropriate ignore rules.

Sources:

- https://codegraphcontext.github.io/guides/indexing/
- https://codegraphcontext.github.io/CodeGraphContext/reference/cli_indexing/
- https://codegraphcontext.github.io/CodeGraphContext/reference/cgcignore/
- https://codegraphcontext.github.io/reference/troubleshooting/
- https://codegraphcontext.github.io/concepts/architecture/

PyPI identifies the qualified package as CodeGraphContext 0.5.1, released on
2026-06-12. The project page advertises broad language support but provides no
hardware-normalized first-index benchmark or average-duration table.

Source: https://pypi.org/project/codegraphcontext/

**Confidence:** high for product behavior and absence of a published official
benchmark; no official duration estimate is inferred.

## 3. Public Timing Observations

| Observation | Reported scale/configuration | Reported duration | Use in decision |
|---|---:|---:|---|
| Large Java repository dependency resolution | Size not stated | A relationship-building report exceeded 11 hours | Tail-risk evidence only |
| Python workspace, Kuzu, four workers | About 12,000 files | About 5.5 hours | Low-confidence scaling reference |
| Three repositories | About 1,000 files and 5M tokens, wording ambiguous | More than one hour before abandonment | Lower-bound evidence |
| Codebase | About 4 GB | Almost one hour | Confirms tens-of-minutes scale |

Sources:

- https://www.reddit.com/r/mcp/comments/1r9lz8y/codegraphcontext_not_working/
- https://www.reddit.com/r/mcp/comments/1qyncmd/codegraphcontext_an_mcp_server_that_indexes_your/
- https://www.reddit.com/r/GithubCopilot/comments/1rnbmnz/codegraphcontext_an_mcp_server_that_converts_local/

These observations lack controlled hardware, CGC-version, ignore-file, language-mix,
and backend normalization. They establish a plausible range of tens of minutes to
several hours, not an average suitable for prediction.

**Confidence:** low for any numerical extrapolation; moderate that a 30-minute
end-to-end deadline is materially too short.

## 4. Story 11.5 Sample Scale

Local immutable-tree measurements at the accepted commits are:

| Repository | Accepted commit | Tracked files | Dominant tracked extensions | `.cgcignore` |
|---|---|---:|---|---|
| `mbs_account` | `19c60cd3f3976b7697c487c7bafd96142e3a7625` | 2,556 | 925 XML, 854 Java, 545 FLW | absent |
| `mbs_channel` | `47f6b627a7598f862873610413eec1e1e7bf46bf` | 6,771 | 4,342 XML, 2,162 FLW, 153 Java | absent |

The final run profiles these Repositories sequentially. The prior live attempt was
still CPU-active during the first Repository's product profiling after 30 minutes;
provider traffic had not begun, placing the elapsed time before Worker Agent Pass 1.

## 5. Timeout Derivation

The only public observation with both file count and duration is approximately
12,000 files in 5.5 hours. A deliberately simple linear normalization gives about
27.5 minutes per 1,000 files:

- `mbs_account`: approximately 70 minutes;
- `mbs_channel`: approximately 186 minutes;
- sequential total: approximately 256 minutes before downstream product work.

This is not a performance model. It is a conservative sizing check against the
direct observation that `mbs_account` already exceeded 30 minutes. A 240-minute
per-Repository ceiling covers the rough 186-minute larger-repository estimate with
about 29% headroom. A 510-minute provisioner envelope covers two full per-Repository
ceilings plus 30 minutes for the remaining deterministic product path.

The timeout is a failure ceiling, not an expected completion time. Successful stages
continue immediately; they do not wait for the ceiling.

## 6. Integration and Operational Design

The provisioner must set
`CodeGraph:WholeRepositoryProcessTimeoutSeconds=14400` before composing product
services. `RepositoryProfilingCoordinator` then passes the same bounded deadline to
the existing CodeGraph provider for each Repository. The host process executor must
permit 510 minutes only for the closed `provisioner-run` command; unrelated commands
retain their existing 45-minute maximum.

The public non-live provisioner contract should expose the 14,400-second product
deadline, and tests should prove:

- the provisioner uses exactly 14,400 seconds;
- the host uses exactly 510 minutes for `provisioner-run`;
- one tick above 510 minutes is rejected;
- non-provisioner commands remain capped at 45 minutes; and
- the change does not add retries, filters, an eighth Pass 2 payload slot, migration,
  model, Docker topology, provider, or package changes.

## 7. Risks, Limitations, and Deferred Optimization

- Community timings are anecdotal and hardware-dependent.
- File count is not equivalent to parse complexity; Java dependency resolution and
  relationship construction may dominate.
- The large XML/FLW population may add traversal cost even when a format is not
  semantically indexed.
- A 510-minute outer ceiling makes genuine stalls slower to fail, so process activity,
  stage diagnostics, and unconditional cleanup remain essential.
- Adding `.cgcignore` could reduce runtime substantially and is the official tuning
  mechanism. It is deliberately deferred because changing the accepted input set is
  a material Scan filtering change requiring a new reviewed plan and planned-change
  authorization.

## 8. Recommendation

Implement the timeout-only correction using **240 minutes per Repository** and
**510 minutes total for `provisioner-run`**, verify it through non-live TDD, create a
fresh immutable candidate, and require a fresh expedited combined quality ACCEPT
before retrying live T072. If the next run reaches the 240-minute per-Repository
ceiling while remaining active, stop and plan an explicitly authorized `.cgcignore`
or profiling-scope correction instead of increasing the timeout again without new
evidence.

---

**Research completion date:** 2026-08-10  
**Source confidence:** high for official behavior and local measurements; low for
community duration estimates; moderate-high for the selected safety bounds when
combined with the observed failed run.
