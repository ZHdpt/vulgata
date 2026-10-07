# October reader integration checkpoint

Scope: integrate Business B0a/B0b through T018, Explorer controlled E0, and Scan
S1. This is not Scan S2, Business B1/B2, live Chat, or real-source qualification.

## Imported boundaries

- Business: `b65213ad5b5f5f26eeffc5d7d9c09db6b608bf55` (includes B0a).
- Explorer: `50be5782` (controlled E0).
- Scan: `50779d73f1ba8c2d9adbccd37b46d7248dc63382` (qualified S1).
- Existing K/CP-K1 remains `12d92d80`. Integration does not alter canonical
  Knowledge types, stored identities, or owner publication/disclosure policy.

## Web handoff

`Program.cs` calls `AddOctoberWikiReader` during final host service composition,
beside the existing frozen `DefaultConnectionSnapshot`. The October context factory
uses that exact connection and `october.__OctoberMigrationsHistory`; registration
does not migrate October, create data, invoke tools/providers, or register producer
capabilities. Ordinary Identity/domain startup migrations remain unchanged.

Configuration lives under `October:Wiki` (environment prefix `October__Wiki__`):

| Key | Meaning |
| --- | --- |
| Enabled | Explicit `true`; otherwise reader stays unavailable. |
| CanonicalDatabaseIdentity, TargetId, BatchId, SelectionDigest, PublicationId | Exact operator-selected persisted publication binding. Never infer latest. |
| LogDirectory | Existing absolute owned directory; no automatic directory creation. |
| Mode | Exact `IT` or `Business`; initial integration profile is server-configured. |
| RepositoryIds:0, RepositoryIds:1, ... | Explicit repository GUID scope; no sample-name or pair requirement. |

An explicit empty scalar `RepositoryIds` means empty scope. Missing, malformed or
duplicate IDs do not expand to all repositories. Empty arrays may disappear in
the configuration provider; use the explicit empty scalar to represent clearing.
Missing/incomplete settings result in unavailable, not a legacy/fixture fallback.
Selection settings are copied once at startup; changes require an intentional
restart with a new profile. This initial fixed profile does not complete the later
Chat mode/scope picker or its W0/W1 integration.

The scoped Explore context uses the circuit authentication provider, one authenticated
identity, the configured Identity user-ID/security-stamp claim types, and Business's
live disclosure policy. It rechecks each observation and invalidates in-flight
presentation on authentication changes. No operator session grants ordinary reads.

Authenticated navigation: **文档** -> `/knowledge/documents`. Exact destinations
are built only by `ExactDocumentLinkBuilder`; preserve batch/publication/logical
document/version/optional section. Business Mode cannot expose CL. CL-only data
therefore produces no Business documents until actual BL publication exists.

## Owned qualification and publication handoff

Run from this checkout using `eng/test.ps1`:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~Vulgata.Tests.October.Integration'
pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~Vulgata.Tests.October.Integration'
```

The PostgreSql case creates an isolated runner-owned database, applies owner
migrations explicitly, and obtains CL through actual acceptance, freeze and
publication APIs. Test-only source/completion/producer authority supplies controlled
semantic inputs; no wiki rows are fabricated as Scan output. The actual published
reader and Explore facade must reassemble a >64-KiB Unicode Document while every
complete serialized ContentPage stays within 64 KiB.

Its TRX output records the generated database name, batch/publication/repository and
Document Version IDs, **never credentials**. The database/container/volume are deleted
after verification. These IDs are evidence, not a still-running environment or a
durable generated dataset. Explorer can reproduce this owned boundary; real E1
generated-CL evidence still requires Scan S2. Do not configure ordinary Web with
these expired test IDs or copy test authority into production.

A retained source-backed publication handoff must later name the approved owned
database, exact binding/handles, schema, user/key/log profile and launch/cleanup
boundary. No shared database or persistent Web host is started by this checkpoint.

## Verification

- Behavioral RED: 8 selected, 4 passed / 4 failed, zero skips against compiling
  registration stub. `TestResults/isolation-dc2b3213faf941d99d504b09ea57a1bf/results.trx`.
- Full current-source SelfContained regression: **2,592/2,592 passed**, zero
  failures/skips, including all **15 integration cases**. Policy covered 351 files.
  `TestResults/isolation-8f969e3448c34497a860190483df8094/results.trx`.
  The combined build had zero errors and existing analyzer/style/package warnings
  (5,427 reported, including AD0001 analyzer exceptions); no warning policy changed.
- Earlier verification attempts: one test compilation error used the wrong entity
  property name (`DocumentVersionId` instead of `VersionId`); a subsequent 14/15
  run lacked mandatory SourceStorage fixture settings in the enabled-host test.
  Both were corrected by the test owner without weakening assertions or changing
  production. Neither setup failure is claimed as behavioral RED.
- Independent static review found one enabled-host verification gap. The added
  actual `VulgataWebComposition` build-only test proves final startup connection
  and publication remain frozen before first resolution after reload. Auth-change-
  during-policy-await is also tested. Review accepted the bounded checkpoint after
  reported full GREEN; the test owner independently checked the TRX counters.
- Owned PostgreSQL: **21/21 passed**, zero failures/skips (20 Business cases plus
  the new integration case). Evidence and verified cleanup logs:
  `TestResults/isolation-bfca9f25a8bb4624825bd3aa8d45eeaa/`.
  This is real persistence/read integration with controlled semantic/authority
  inputs, not a full authenticated PostgreSQL Web host or real Scan run.

Sanitized ephemeral handoff from that PostgreSQL run (database deleted afterward):

- Database: `vulgata_story121_b6c21895738d464e95910e8273e34271` (existing test helper prefix).
- Batch: `00000000-0000-0000-0000-000000000301`.
- Publication: `90e8c09c-f90c-4437-b3d1-647cf1b82969`.
- Repository: `00000000-0000-0000-0000-000000000302`.
- Document Version: `c82471dd-47aa-4563-8c41-771ea96cf0c3`.

Use the recipe above to obtain a new owned test publication. These recorded IDs
cannot reopen the deleted database and are not a presentation dataset.

Separate test author owns integration tests; root owns production composition and
Git; an independent read-only reviewer checks the combined boundary. Do not mark
Explorer T017, Business T020, Scan T024, or Chat C0/live C1 complete from this record.
