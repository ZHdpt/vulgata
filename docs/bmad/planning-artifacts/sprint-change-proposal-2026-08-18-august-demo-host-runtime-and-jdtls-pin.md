# Sprint Change Proposal: August Demo Host Runtime and JDT-LS Pin

**Date:** 2026-08-18
**Trigger:** Docker application builds and container networking repeatedly
consumed deadline time without improving the bounded scanning claim, while the
August release still needs a stable local CodeGraph/JDT-LS runtime.
**Mode:** Batch
**Scope classification:** Moderate MVP release-topology correction
**Artifact status:** Approved
**Approval:** The user approved the recommended host-runtime topology and
StreamJsonRpc-based Java LSP adapter on 2026-08-18, selected sequential Epic 12
story execution, and selected
`jdt-language-server-1.61.0-202608141332` for local installation.
**Design authority:**
`docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md`

## 1. Issue Summary

The August 30 recovery requires a real, bounded two-Repository Scan, not proof
that every product process can be packaged into one Linux application image.
Prior Docker builds and Docker Desktop networking failures consumed significant
time and made the release run less repeatable. The .NET application and its
analysis dependencies can run directly on the documented Windows demonstration
host while PostgreSQL 17 remains isolated and persistent in Docker.

The change does not remove Docker deployment from the intended V1 product. It
temporarily removes the application container and container-restart proof from
the August 30 blocking release gate. Application-process restart against the
same PostgreSQL database proves the durable Scan and wiki/Chat requirements
without coupling the demonstration to Docker application networking.

The same correction fixes the Java analysis identity before Story 12.1 planning
advances: Eclipse JDT LS is pinned to
`jdt-language-server-1.61.0-202608141332`, runs on Java 21, and is accessed over
stdio through a small Vulgata-owned adapter using StreamJsonRpc `2.25.29`.
The previously ambiguous CodeGraph name is resolved to npm package
`@colbymchenry/codegraph@1.5.0`, repository
`https://github.com/colbymchenry/codegraph`, command `codegraph`, with the
Windows x64 platform bundle `@colbymchenry/codegraph-win32-x64@1.5.0`.

## 2. Change Navigation Checklist

### Trigger and evidence

- [x] Story 12.1 introduces the production CodeGraph/JDT-LS runtime boundary.
- [x] Docker application build/network instability is a schedule and release
  repeatability risk; PostgreSQL-only Docker operation is already a documented
  local-development topology.
- [x] The bounded product claim depends on real source analysis, durable state,
  generated Documents, Cross-Repository Resolution, and grounded Chat—not the
  location of the Vulgata process.
- [x] Eclipse JDT LS needs one exact release identity, Java 21, an isolated
  workspace per Repository revision, and a bounded lifecycle client.

### Epic impact

- [x] Epic 12 remains the active August 30 delivery overlay and its five stories
  remain in their existing order.
- [x] Story 12.1 owns the host-side analysis runtime, typed ports, process
  lifecycle, version/checksum qualification, and durable restart foundation.
- [x] Story 12.2 continues to own real JDT-LS readiness during PreScan.
- [x] Story 12.5 changes its Tier 3 proof from application-container restart to
  application-process restart against the unchanged PostgreSQL database.
- [N/A] No story, migration, domain entity, UI flow, or retrieval requirement is
  added or removed.

### Artifact conflicts

- [x] PRD NFR-4.1 remains the long-term V1 deployment requirement but is no
  longer a blocking August 30 acceptance condition.
- [x] The August 30 architecture currently requires a production Docker image,
  Docker restart, and one Docker two-Repository run; those statements require a
  dated host-runtime replacement.
- [x] Epic 12 currently maps NFR-4.1 into the dated release and Story 12.5 names
  the final real-LLM Docker path; those clauses require replacement.
- [x] The active Story 12.1 specification must adopt the exact JDT-LS version,
  StreamJsonRpc client decision, and host-runtime boundary before plan review.
- [N/A] UI/UX, persistence shape, authorization, and source-evidence contracts
  are unchanged.

### Path evaluation

- [x] **Direct adjustment selected:** host-run Vulgata, CodeGraph, Java 21, and
  JDT LS; run only PostgreSQL 17 in Docker for the August demo.
- [N/A] Full Docker application proof is rejected as a blocking gate because it
  adds operational risk without strengthening the core demo claim.
- [N/A] Removing Docker deployment support entirely is rejected because NFR-4.1
  remains valid post-deadline product work.
- [N/A] Rolling back Epic 11 or current Story 12.1 work provides no benefit.

## 3. Recommended Approach

Use this August demonstration topology:

```text
Windows demonstration host
|- Vulgata.Web: dotnet run or a framework-dependent published executable
|- CodeGraph CLI: @colbymchenry/codegraph@1.5.0 child process
|- Java 21
|- Eclipse JDT LS: jdt-language-server-1.61.0-202608141332
|  `- one stdio child process and isolated workspace per active Repository revision
`- Docker Desktop
   `- PostgreSQL 17 only
```

Application and tool installation paths remain machine configuration. No local
absolute path becomes domain state, a prompt value, or a release identity.
Release identity consists of the declared version, archive checksum, Java major
version, executable/archive path after normalization, and the Repository
revision/workspace binding.

The Java LSP implementation is a narrow Vulgata-owned adapter over
StreamJsonRpc `2.25.29`, not a general LSP framework. It owns process startup,
LSP framing, initialization, required server notifications, cancellation,
timeouts, sanitized stderr, graceful shutdown, and forced cleanup. The public
port exposes only document symbols, definition, references, and implementations;
raw JSON-RPC/LSP messages and arbitrary process access do not cross the port.

The qualified CodeGraph Windows x64 installation records these observed
SHA-256 values, which the Story 12.1 runtime manifest must verify:

- `npm-shim.js`:
  `80419260F06862D7A422D13EF69E3B24C7A21DBEF4309BCE41DB02AD642B1877`;
- platform `node.exe`:
  `B3094D0B49F9AD602262A9921551737BB97637C05DD357A06AE98188D7290AA3`;
- platform `lib/dist/bin/codegraph.js`:
  `03E4C791CC0DD91ED264278461BF9A56C0278AA0670D5942FC4732311C66DE03`.

## 4. Detailed Artifact Changes

### PRD

**Existing NFR-4.1:** Vulgata shall deploy as a single Docker container.

**August 30 replacement:** Run Vulgata and its analysis tools on the Windows
demonstration host and run PostgreSQL 17 in Docker. Treat the application Docker
image build as optional, non-blocking evidence. Retain NFR-4.1 as post-deadline
V1 work.

### Epic 12

- Story 12.1 qualifies the host-side CodeGraph/JDT-LS runtime and exact JDT-LS
  identity; tool locations are configurable.
- Story 12.2 proves JDT-LS initialization, `initialized`, usable import state,
  and `textDocument/documentSymbol` for a neutral eligible Java file within 60
  seconds.
- Story 12.5 performs a clean-database real-LLM host run and restarts the
  Vulgata process against the same PostgreSQL database before verifying persisted
  wiki and Chat access.
- A final Docker application image build may be recorded if convenient, but its
  failure cannot fail the August release candidate. No application container or
  Docker restart is required.

### Architecture

- Use stdio for JDT LS; do not allocate TCP ports or expose a network service.
- Pin `jdt-language-server-1.61.0-202608141332` and record the exact installed
  archive checksum before readiness can pass.
- Pin `@colbymchenry/codegraph@1.5.0` and its Windows x64 platform package;
  verify the three approved installed-file hashes above.
- Pin StreamJsonRpc `2.25.29` centrally.
- Keep one isolated writable JDT-LS workspace per Repository Profiled Revision.
- Replace Docker restart evidence with application-process restart evidence.
- Preserve clean PostgreSQL migration and cleanup evidence.

## 5. Verification and Success Criteria

### Story 12.1 qualification

1. Start the pinned JDT LS with Java 21 over stdio.
2. Send `initialize`, receive capabilities, and send `initialized`.
3. Open a neutral Java file and successfully request document symbols.
4. Exercise definition, references, and implementations through the typed port.
5. Cancel one bounded request and verify the adapter remains usable or fails
   closed according to the classified error.
6. Send `shutdown` and `exit`; force cleanup if the process exceeds its bound.
7. Reject a version mismatch, checksum mismatch, non-Java-21 runtime, reused
   workspace, malformed response, or readiness timeout.

### Final August release

- Vulgata and analysis tools run on the documented host.
- PostgreSQL 17 is the only required Docker service.
- The database begins clean and migration lifecycle evidence passes.
- The real two-Repository CodeGraph/JDT-LS/LLM Scan and Chat acceptance metrics
  remain unchanged.
- Restarting the Vulgata process preserves Scan recovery and completed wiki/Chat
  access through the same PostgreSQL database.
- The evidence manifest records Git revisions, counts, durations, schema/prompt
  identities, CodeGraph/JDT-LS/StreamJsonRpc/Java identities, archive checksums,
  and evidence fingerprints.

## 6. Implementation Handoff

**Classification:** Moderate; Product Owner/Architect artifact correction plus
Developer execution inside the existing Story 12 sequence.

1. The canonical PRD, architecture, and Epic 12 statements adopt this proposal.
2. The active Story 12.1 spec/plan owner incorporates the runtime topology and
   exact tool/client decisions without rewriting its persistence scope.
3. The Story 12.1 plan still receives independent review.
4. Because the plan adds a Java LSP tool and materially changes Scan startup,
   the sprint-loop orchestrator still pauses after accepted plan review for the
   repository-required planned-change authorization before task generation.
5. Story execution remains sequential: 12.1 -> 12.2 -> 12.3 -> 12.4 -> 12.5.

## 7. Approval Decision

The user approved the host-runtime topology and the StreamJsonRpc-based JDT-LS
adapter on 2026-08-18. The user also selected sequential Epic 12 execution and
the exact JDT-LS release
`jdt-language-server-1.61.0-202608141332`. This approval authorizes planning
artifact correction. It does not waive Story 12.1 plan review, planned-change
authorization, TDD, quality review, migration verification, real-tool E2E, or
the final real-LLM evidence gate.
