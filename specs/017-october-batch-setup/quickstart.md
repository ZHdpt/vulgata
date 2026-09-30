# Validation Guide: Independent October Batch Setup

This describes the corrected acceptance checks, not completed runtime qualification. Required reviews/approvals precede changed implementation. Actual accounts/databases/processes need explicit owned-resource authority; no old Web/TLS permission is assumed.

## Prerequisites

1. Two authorized runner-owned PostgreSQL targets A/B with valid real test Identity/System/Repository/context records created through existing supported Identity/domain APIs. Default layout puts identity/vulgata/october schemas in each chosen database; at first prepare no october schema or batch exists. Also exercise the optional explicitly configured separate Identity layout. No shared database, exported users/hashes or ambient credentials.
2. A permitted real operator, denied operator, System member and standalone Repository, differing advisory context, exact deployment target descriptors/AuthorityId and immutable per-Repository root-version mappings to owned prepared source.
3. Explicit operator credential source: no-echo console or explicitly named purpose-protected credential file with compatible keys/application identity. Required factors must be supplied and checked. Keep secrets out of command arguments/output.
4. Complete explicit Worker/Chat provider profiles with synthetic protected credentials. No paid endpoint call is needed.
5. Corrected generator and Web using the same direct authority/policy/store/selector. No Web authority URL, activation grant, mutual-TLS certificate or authorization-only bootstrap.

## Controlled and runtime checks

1. Prove headless authentication through the actual Identity-backed module: password/factor success, wrong/missing factor, unsupported account mechanism, lockout/deleted account, unavailable store and cancellation. Request SubjectId/roles and DB credentials alone never authenticate. Change security stamp/role/System assignment and observe fresh denial on the next protected check. Confirm no password/factor/session is persisted or logged.
2. With no Web listener/process running, execute actual generator prepare on A through current database authority. Verify exact connected target and current operator/organization before any October migration, then one atomic first-batch commit. Authority reads may use A's existing identity/vulgata schemas. Denied authority leaves a fresh target without October migration/batch. Repeat B, identical retry and changed-content conflict; no fake pair/FlowGroup/ScanRun or copied legacy Knowledge.
3. Execute generator select independently against A/A and B/B. Missing/mismatched selection fails nonzero with sanitized outcome and no latest/other-database fallback. Select performs no migration. All capabilities require exact committed identity.
4. Start actual selected-data Web with DefaultConnection=A plus A's target/batch/authority; inspect domain, October and Identity/factory connections. Restart with B and corresponding configuration; every domain/October read follows B. Configuration reload alone cannot switch. Optional separate Identity changes only Identity, never management or October. A/B mismatch denies before serving; selected-data performs no implicit authority seeding or October migration.
5. Through actual WebApplicationFactory<Program>.ConfigureAppConfiguration, prove one final immutable selection and consistent context/factory registrations. Ordinary mode keeps baseline behavior and no October operational registrations/migrations/routes. Invalid/obsolete authorization-only/grant/Web-URL/certificate config fails in explicit October mode. All four former October HTTP authority routes are absent; generator requires none. Preserve ordinary cookie/API behavior.
6. Exercise recording writer and real October persistence separately: exact immutable context/origin/membership/applicability/differences/advisory classification, denial of missing/default/forged payload, canonical v2 digest, first-commit UTC time, replay time and atomic rollback. Read-only same-authority binding after text edits returns original historical values/time; new prepare conflicts. Different AuthorityId with colliding IDs/root/values denies before capability/text. No context/log output on denial.
7. Through actual direct source-approval adapter, create a ten-minute-bounded exact approval and issue via existing PreparedSourceBinding/PreparedSourceContextIssuer. Revoke it through the production trusted revoke operation while Web remains stopped. Also remove role/membership, rotate stamp/root binding or make authority DB unavailable between reads and immediately before disclosure. Assert exact Current/Revoked/Unavailable mapping, no failed context, no buffered payload/continuation/reactivation and settled cancellation/disposal. Reuse 18.1 containment/tool-schema tests rather than clone them.
8. Check provider distinct/shared profiles, missing/conflicting fields, secret failure, declared precedence and immutable process selection. No source/operator/provider secret diagnostic, automatic failover, field mixing or paid health call.
9. Verify complete PostgreSQL October migrations/history/model on both targets and exclusion from ordinary model discovery. Default ordinary Web migration/seeding remains unchanged; selected-data validates pre-existing authority schemas instead of silently modifying them. Inspect scoped resource cleanup.

## Command shapes and evidence

After implementation and resource authorization, use the repository launcher:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~October'
pwsh -NoProfile -File eng/test.ps1
pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~October'
```

Actual process/loopback cases use the classified E2E suite and -WithPostgreSql when needed, with explicit owned resources first. The child generator command is `dotnet run --project src/dotnet/Vulgata.OctoberGenerator/Vulgata.OctoberGenerator.csproj` with the explicit startup configuration contract; never append passwords/connection secrets. Web uses its existing executable and selected-data configuration. Classify every new test/helper before running, follow current testing.md, and serialize builds using shared outputs.

Report static definition checks, SelfContained, owned PostgreSQL and actual-process results separately. Prior 18.1 verification establishes its contained source boundary; old grant/mTLS GREEN establishes only historical transport. Neither proves independent batch completion. No Docker, paid provider, JDT-LS, CodeGraph or Knowledge generation is part of this guide.
