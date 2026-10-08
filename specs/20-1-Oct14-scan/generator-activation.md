# Generator activation and shared qualification

## Current authority and priority

The October 8 user dispatch authorizes support for the first real CL-only Generate-to-ordinary-reader run and narrow command composition of the already-delivered internal CL-and-BL path. It does not authorize broader S3 scheduling, root discovery, recovery or stop-policy changes. No skills are loaded. The assigned `oct14/scan` worktree was clean at `95864e555ff3e747464eb8051d5c0eb853a7b314`; approved Integrator main `f6be1b410bd5de08394d70e5e83017c6512fec51` was merged without reset as `fcfcc14771231681602903b2841d1cb50da9989b`.

CL-only Program/CLI wiring already exists. Its qualification does not depend on BL reader binding, Business B1/B2, Chat or graph completion. Explorer owns the missing combined E2E test; Integrator owns its existing shared fixture, resources and run. Scan supplies the command/adapter contract and corrects concrete Scan defects. Do not create a second fixture/dataset or repeat generation after an unknown outcome.

## CL-only executable handoff

Use `OctoberScanGenerateQualificationFixtureAdapter.ForFixture(existing GeneratorPrepareFixture, approvedProfile, approvedPolicy, approvedRelativeFile, OctoberGenerateInputFiles)` and then `ExecuteOnceAsync`. The adapter and fixture helper are in `tests/Vulgata.Tests/OctoberScan/OctoberScanGenerateQualificationFixtureAdapter.cs` and `OctoberScanGenerateQualificationAdapter.cs`. Existing adapter tests are controlled validation, not the missing real E2E scenario.

The adapter checks the E2E suite guard and the fixture-owned PostgreSQL target immediately before launching the existing child command:

```text
dotnet run --project src/dotnet/Vulgata.OctoberGenerator/Vulgata.OctoberGenerator.csproj --no-build --no-restore
```

Build the exact approved checkpoint before this invocation. The shared E2E launcher must provide its existing suite/owned-resource boundary (`eng/test.ps1 -Suite E2E -WithPostgreSql` plus the actual Explorer test filter when available). Neither this command nor a suite flag supplies source/provider/native/resource consent. The fixture, not an ad hoc shell or second dataset, owns values and process cleanup.

| Child environment | Required binding |
| --- | --- |
| `October__Mode` | `generate` |
| `October__TargetId`, `October__BatchId` | Exact prepared fixture target and batch |
| `October__Authority__Id` | Configured management authority, not an Identity security stamp |
| `October__Targets__0__TargetId`, `October__Targets__0__CanonicalDatabaseIdentity` | Exact target catalog entry matching the held database identity |
| `ConnectionStrings__DefaultConnection` | Fixture-owned data target; never ambient/shared substitution |
| `October__Operator__CredentialSource`, `October__Operator__CredentialFile` | `protected-file` and the fixture's protected operator credential |
| `DataProtection__KeyRingPath`, `DataProtection__ApplicationName` | The fixture's approved key/application binding used to protect credentials |
| `October__RuntimeFile`, `October__ScanPolicyFile`, `October__Providers__ConfigurationFile` | Three validated regular, non-reparse files inside `fixture.OwnedRoot` |

Generate does not accept `October__SetupRequestJson`, a separate `October__OutputStage`, or competing provider/binding overrides. Optional separate Identity connection configuration follows the existing fixture's approved policy. Do not copy credentials into handoff files, logs or source control.

The runtime document supplies one exact `preparedRoots` tuple for the selected target/batch/Repository/root binding and `OwnedSourceRoot`, absolute `codeGraphCommandPath`, `javaNavigation`, `javaHostPolicy` and `logDirectory`. Dedicated state and log directories must stay outside source. The native profile retains initialization at most 20 seconds, requests at most 6 seconds, shutdown at most 15 seconds and responses at most 2 MiB. Configuration parsing does not itself open source or launch tools.

The approved policy must specify `outputStage: cl-only`, `workerCap: 1`, exactly one selected rule matching the approved relative file, finite deadlines and a positive explicit `maximumTotalTokens`. Policy defaults are not spend authorization. The adapter compares all approved scalar/rule values exactly. Runtime limits remain the original run's limits; no downstream phase receives a renewed allowance.

Use the fixture's `WriteGenerateWorkerProviderAsync` to create the protected Worker-only document. The current fixture convention uses `sourceIdentity: owned-generate-worker`, `precedence: only-source`, one exact selected profile (`apiType: chat-completions`, endpoint, model, credential reference) and one matching protected credential. No Chat profile is needed. The adapter does not decrypt the document.

## Lifecycle and result interpretation

Authenticate and retain the original operator session, select the existing prepared historical batch and exact context, and obtain a known committed Begin before opening source/inventory/native tools. A saved batch or receipt is not restart authority. Source approval binds batch, Repository, subject, Worker-source-read purpose and root. Actual revision integrity comes from retained identity, byte digest, full-input/two-pass and current-source checks; the root-session prepared version is not a guessed Git revision.

The adapter is consumed before validation/dispatch. Failure, cancellation or unknown settlement cannot be bypassed by constructing a replacement adapter. The fixture observes the original run deadline plus a 120-second cleanup margin; that margin grants no new provider or write budget. Timeout cleanup is restricted to its owned child. Preserve committed history and report unresolved settlement honestly.

The Generate execution path emits camelCase JSON with string enums. Earlier startup/configuration/credential failures can exit nonzero with stderr and no report JSON; missing JSON is failed or unknown evidence, never permission to regenerate. For CL-only, `matching` and `businessLogic` remain `not-requested`. The public projection intentionally omits canonical database identity; hydrate/check it only against the held fixture binding. Do not treat deserialization of the public projection as independent authority. Preserve returned exact publication and Document handles; never invent IDs or fall back to a current publication.

| Exit | Meaning |
| --- | --- |
| 0 | Completed requested stage, confirmed exact publication, nonempty handles and complete selected scope |
| 2 | The same confirmed route with partial selected coverage |
| 3 | Interrupted execution |
| 4 | Indeterminate execution/publication; no regeneration or rollback assumption |
| 5 | Completed explicit NoContent result |
| 1 | Other failure, including mandatory logging failure |

A committed publication with a logging failure retains confirmed handles/completed stage but reports failed execution and exit 1. Null request/token counts mean unknown, never zero. An accepted row, exit code or controlled model response alone is not the real milestone.

Explorer's combined test must open the returned retained Document through the actual ordinary IT reader/Identity path, verify exact binding and source-backed content, Business-mode exclusion and revocation. It must use the one generated publication from the shared fixture. The headless operator's management authority is not ordinary-reader authentication evidence.

## Narrow BL activation dependency

Scan owns `Vulgata.OctoberGenerator/Program.cs`, strict command settings and the adapter from verified reader binding into the existing internal runner. Integrator owns verified Identity binding and shared Web composition. The current internal reader callback must be replaced before activation: constructing `KnowledgeSubject.AuthenticationBinding` from `ManagementAuthorityId` is invalid for the production Identity adapter.

Integrator must supply a binding for the original authenticated operator session and exact publication that returns a genuinely verified `Subject`, exact Reader-purpose `Selection`, `Reader` and an `IAsyncDisposable` lifetime, retaining current Identity/security-stamp/permission checks. Keep that lifetime until actual reads settle. Missing or denied binding fails BL closed and must not affect CL-only. No provided string or fixture-only reader may stand in for this service.

Explicit roots must map uniquely to actual owner-projected units and use their returned UnitId/DocumentVersion. This activation accepts only `SelectedCodeUnit`, the kind currently supported by the owner planner; reject other kinds without conversion. No automatic all-units selection, heuristic root discovery, source rescanning, new persistence policy or provider configuration is authorized. The original session/client/profile, budget, acceptance guards and permanent finalization slot remain authoritative.

## Runtime qualification boundary

Controlled implementation, owned PostgreSQL checks and real source/provider/native/ordinary-reader qualification are separate evidence. Before a real run, Integrator must bind the recorded approval to exact prepared source/file and supporting scope; selected provider/model/credential reference and cumulative spend ceiling; owned database/schema/identities/key/log resources; and process/host lifecycle, ports, retention and cleanup. Unprovided approvals block only the affected runtime operation. This document records no new resource grant and no successful real generation.

Integrator relayed the user's concrete decision for this wave: A, the 62 source-free owned PostgreSQL checks, is approved exclusively for Business; Scan starts no competing PostgreSQL launcher. B, the real Generate/provider run, is explicitly paused while the user arranges LLM configuration. C, the retained presentation dataset, remains pending until B passes. Controlled activation work continues. The shared fixture remains disposable and its cleanup must not be disabled.
