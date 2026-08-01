# Contract: Worker Agent Job Packet

**Date**: 2026-07-31 | **Plan**: [plan.md](../plan.md) | **Data model**: [../data-model.md](../data-model.md)

## Purpose

The bounded, schema-validated input to one Worker Agent job, dispatched by
the Sequential Scan Runner through `IWorkerAgentDispatcher`. Story 11.2
defines the contract; Story 11.3 supplies the LLM-backed implementation.

## Closed allowlist

`WorkerAgentJobPacket` is an immutable Core type constructed through a static
factory that validates every invariant. The exact fields are:

- `JobId` (`Guid`, non-empty, runner-assigned)
- `Kind` (`WorkerAgentJobKind`, closed; `Pass1CodeLogicDocument` for Story 11.2)
- `ScanExecutionId` (`Guid`, non-empty)
- `RepositoryId` (`Guid`, non-empty)
- `SystemId` (`Guid`, non-empty)
- `ScanRunId` (`Guid`, non-empty)
- `CodeUnitId` (`Guid`, non-empty)
- `ProfiledRevision` (`string`, bounded lowercase Git object ID)
- `BoundedSourceSlice` (closed allowlist, see below)

`BoundedSourceSlice` fields:

- `FilePath` (`string`, repository-relative, ≤2048 UTF-8 bytes)
- `LineStart` (`int`, ≥1)
- `LineEnd` (`int`, ≥`LineStart`)
- `SymbolName` (`string`, ≤512 UTF-8 bytes)
- `UnitType` (`CodeUnitType`, closed)
- `Language` (`string?`, normalized or null)
- `SourceHash` (`string`, lowercase SHA-256)
- `FileIdentityHash` (`string`, lowercase SHA-256)

## What the packet MUST NOT carry

- no source text (only identity and hashes);
- no unauthorized Repository identity;
- no secret or credential;
- no absolute host path;
- no raw-source carrier outside accepted anchors;
- no unbounded diagnostic;
- no arbitrary payload, extension object, or `byte[]`/`JsonElement`.

## Sensitive-content guard

Every string is Form-C normalized, rejects forbidden controls, and is checked
against the closed credential, host-path, raw-source-carrier, and
raw-diagnostic rules (the Story 11.1 sensitive-content guard) before
construction. A rejected value throws a closed `ArgumentException`; the
rejected value and any provider diagnostic do not cross the contract.

## Deterministic ordering

The runner constructs packets in deterministic order over the prepared Code
Units: by `FilePath` (ordinal), then `LineStart`, then stable `CodeUnitId`.
This order is observable through the `IWorkerAgentDispatcher` fake/test
recording and is part of the contract.

## Testable boundary

This is the testable boundary for FR-004 and the sensitive-content guard.
The plan does not claim semantic detection of every possible secret; it
claims the closed lexical guard rejects the accepted prohibited fixtures
across packet strings.
