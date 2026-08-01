# Contract: Worker Agent Result Envelope

**Date**: 2026-07-31 | **Plan**: [plan.md](../plan.md) | **Data model**: [../data-model.md](../data-model.md)

## Purpose

The schema-validated output of one Worker Agent job, returned to the
Sequential Scan Runner by `IWorkerAgentDispatcher.DispatchAsync`. Story 11.2
defines the contract; Story 11.3 supplies the LLM-backed implementation.

## Closed allowlist

`WorkerAgentResultEnvelope` is an immutable Core type constructed through a
static factory that validates every invariant. The exact fields are:

- `JobId` (`Guid`, must match the dispatched packet)
- `ScanExecutionId` (`Guid`, must match the dispatched packet)
- `Kind` (`WorkerAgentJobKind`, must match the dispatched packet)
- `Outcome` (`WorkerAgentResultOutcome`, closed: `Valid`, `SchemaInvalid`,
  `Error`, `Unbounded`)
- `BoundedReason` (`string?`, Chinese, ≤512 UTF-8 bytes, non-sensitive;
  required on every non-`Valid` outcome, optional on `Valid`)
- `DispatchedAt` (`DateTimeOffset`, from packet)
- `ReceivedAt` (`DateTimeOffset`, `>= DispatchedAt`)
- `Payload` (`WorkerAgentResultPayload?`, closed allowlist, present only on
  `Valid`; reserved for Story 11.3)

## Outcome semantics

- `Valid`: the job returned a schema-valid result; the runner records
  `RecordValid` and continues.
- `SchemaInvalid`: the result failed schema validation; the runner records
  `RecordFailure` with `TerminalReasonKind.SchemaInvalid` and stops.
- `Error`: the job returned an unhandled error; the runner records
  `RecordFailure` with `TerminalReasonKind.Error` and stops.
- `Unbounded`: the result exceeded a finite bound; the runner records
  `RecordFailure` with `TerminalReasonKind.Unbounded` and stops.

## What the envelope MUST NOT carry

- no source text;
- no secret or credential;
- no absolute host path;
- no raw-source carrier outside accepted anchors;
- no unbounded diagnostic;
- no arbitrary payload outside the closed `WorkerAgentResultPayload`
  allowlist.

## Sensitive-content guard

Every string is subject to the Story 11.1 sensitive-content guard. A
rejected value throws a closed `ArgumentException`; the rejected value and
any provider diagnostic do not cross the contract.

## Story 11.2 consumption

Story 11.2 validates envelope shape and outcome only. It does not consume any
`Payload` field; that is Story 11.3 work. The runner never commits a Document
Version from an invalid result.
