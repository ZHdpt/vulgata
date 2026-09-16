# Story 13.1 implementation traceability

Status: implementation verification in progress; this document does not accept convergence, quality review, E2E, or story completion.

## Requirements and controlled evidence

| Obligations | Implementation | Behavioral test families |
| --- | --- | --- |
| FR-001–004; SC-001/002 | SourceSnapshot identity, live SourceRepositoryAuthorizationVerifier, RetainedSourceCaptureService and GitCommittedObjectReader | RetainedSourceCaptureTests, RetainedSourceApplicationContractTests, RetainedSourceAuthorizationTests |
| FR-005/017/018/020; SC-007/008 | Atomic RetainedSourceRepository.PublishAsync, DurableRetainedSourceStore seal/identity lock, owned operation and sealed-object cleanup | RetainedSourcePersistenceTests, RetainedSourcePostgreSqlTests, RetainedSourceReadLifecycleTests |
| FR-006–011/013; SC-003–005 | RetainedSourceReadContext cumulative reservations/deadline, manifest/blob verification and strict relative reads | RetainedSourceReadContextTests, RetainedSourceRangeReadTests, RetainedSourceAuthorizationTests |
| FR-012/014/016 | Infrastructure-internal TrustedSourceWorkspaceProvider, sanitized outcomes and owned disposal | TrustedSourceWorkspaceTests, RetainedSourceLoggingTests, RetainedSourceRepositoryDeletionTests |
| FR-015/016; SC-006 | Durable store plus fresh production composition, without prior process, lease, remote or tool roots | RetainedSourceSeparateProcessTests and its explicitly opted-in child test modes |
| FR-019; SC-008 | Distinct ref/acquisition/integrity/storage/authorization outcomes, no alternate source | RetainedSourceCaptureTests, RetainedSourceReadContextTests, RetainedSourceLoggingTests |
| FR-021/022; SC-009/010 | Additive source composition, no semantic activation | RetainedSourceCapabilityBoundaryTests; domain SQL guard installed after fixture setup; exact outcome-to-test mappings |
| CP-FR-001–004; CP-SC-001–004 | Exact existing semantic identity2.0 domain registered in CanonicalEnvelope; existing producer/grammar/1.0 remain unchanged | PreparedCanonicalEnvelopeTests, PreparedCanonicalEnvelopeGoldenTests, PreparedFileCodeUnitAndSelectionTests; affected reviewed-profile fixtures |

## Plan and research decisions

Research R1/R2 and plan decisions 1/2 map to raw Git object capture, canonical manifest/blob objects, and immutable source identity. R3/plan3 maps to the unique PostgreSQL completion row as publication visibility, same-volume sealing, and shared identity locking. R4/plan4 maps to current authenticated subject plus live Repository/System authority. R5/R6/plan5 map to process-local cumulative grants and exact byte/strict-text capabilities. R7/plan6 maps to the internal trusted workspace seam. R8/plan8 maps to validated server-owned capture bounds and caller-lowered read requests. R9 maps to additive registrations and preservation of historical prepared-pair contracts.

The separately approved compatibility amendment reconciles an existing Core producer and its closed domain registry; it is not a new source or semantic-analysis design. T044–T049 bind its public RED/GREEN, compatibility checks, affected fixtures, source regressions and full-suite evidence. It does not change the retained-source data model or either source contract.

The data model is represented by SourceSnapshot and SourceSnapshotConfiguration, a provider-aware migration, immutable PostgreSQL enforcement, and restrictive Repository deletion. The existing Repository API maps the retained-source conflict to Chinese 409; it adds no source-management UI or new public snapshot endpoint.

The application contract and manifest schema remain acceptance authorities. Contract/reflection, canonical manifest, raw path, exact byte, unsupported entry, authorization, resource-limit, publication and corruption tests exercise their declared boundaries. Quickstart distinguishes static checks, controlled Git/filesystem tests, PostgreSQL constraints, and actual separate-process reopening.

## Downstream gate predicates

The E2E matrix triggers authorization, API/persistence/multi-module integration, and EF migration/data lifecycle. The Repository deletion conflict is an existing user-visible behavior change. Any coverage override must cite exact passing tests at the independently reviewed boundary; the existence of these test families alone does not authorize a skip. The root records that decision after quality review.

## Qualification limits

Only retained-source capture, retention, scoped reads, trusted materialization, and reopen behavior are qualified here. PostgreSQL runs inside an isolated owned container; that infrastructure does not qualify the application Docker stack. These results do not establish generic Scan, accepted Knowledge, Chat streaming, native MCP/LSP, real-provider behavior, backup, restore, or universal capacity. Historical opt-in external qualification remains disabled. Parent-run child-only skips are distinct from child processes asserted by their parent tests.

The historical exact-resolution producer/store stable-key mismatch remains outside this correction and is recorded in `canonical-correction-affected-fixture-diagnosis-01.json`. The owner-symbol-mismatch characterization now establishes that negative premise explicitly in an owned fixture and retains its Refused/zero-write assertions; passing it does not qualify successful exact resolution.
