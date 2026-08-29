# Story 12.5 Track C-MVP PostgreSQL/provider preflight authorization 01

- Decision ID: `story-12-5-track-c-mvp-postgresql-provider-authorization-01`
- Decided at: `2026-08-29T23:16:52Z`
- Scope SHA-256: `6e88b42958a615399a9b4167a1c9fc27f7229ea7cedcc7f4febedafd8c775400`
- Standing authorization: `story-12-5-track-c-mvp-overnight-standing-authorization-02`
- Status: **accepted**

## User authority

The user explicitly approved PostgreSQL use for Track C-MVP and supplied the
current non-secret provider identity:

- endpoint: `https://opencode.ai/zen/go/v1/chat/completions`
- model: `deepseek-v4-flash`
- credential source: an existing Vulgata test environment variable, resolved
  without printing its value as `VULGATA_TEST_KEY`

The earlier standing authorization remains the exact `mbs_account` /
`mbs_channel` real-source disclosure authority. The qualification process may
bind `VULGATA_T062_APPROVED_PAIR_REAL=1` and
`VULGATA_T062_SOURCE_BINDING_READY=1` process-locally for this run only.

## Authorized action

Use the existing healthy `vulgata-postgres` PostgreSQL 17 container's verified
database-creation role as the approved administrator mechanism. Create only the
fresh Story 12.5 guard-accepted database recorded by run 02. Use the supplied
provider identity and existing credential only through the configured Vulgata
provider boundary; never print or persist the credential or send source outside
that provider.

This decision does not authorize another database, destructive cleanup, an EF
migration, `VulgataDbContext`/snapshot changes, Story 12.4 mutation, Scan changes,
fake evidence, or disclosure beyond the exact two approved Repositories.
