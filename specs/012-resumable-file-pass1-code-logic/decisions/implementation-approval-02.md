# Story 12.3 Corrected Implementation Approval 02

- Decision ID: `story-12-3-implementation-approval-02`
- Status: Accepted
- Decided at: `2026-08-22T08:23:08Z`
- Scope SHA-256: `bef1ba1d1de192ee31ba68c6ccf1b57b7bccb57c9a94172e05a542306585b42a`
- Specification SHA-256: `7dba4ddf1d808dd6327468920d8ae2e0bf8130881a95c10166858c407e2f98a1`
- Plan SHA-256: `9535a4b31c16cb927a11ef4e6c71d6b3398536e53dedda38c4dd70ece3b0226f`
- Tasks SHA-256: `d76b48872a94b9b4313e4899bbc1d452fba1c37a9a225a9a454a7d52e490e680`
- Accepted definition verdict SHA-256: `f685d131682829bb002c28627cfe9a04471108f93d2074c8eec52a013ac638a3`

Scope construction: SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
current Story 12.3 specification, complete corrected plan set, tasks, and
independent `definition-review-09` ACCEPT verdict.

## Decision requested

Authorize behavioral TDD implementation of exactly correction tasks T050-T064
in the independently accepted Story 12.3 definition. Production intent is to:

- make the single activated pair coordinator capacity-filling while preserving
  single-flight ownership, durable claim identity, current lease revalidation,
  limits one through four, drain, terminal exhaustion, and deterministic order;
- compose the closed Worker executor, dispatcher, committer, provider-failure,
  and reconciliation route without exposing another Worker tool or scheduling
  another file;
- drive exactly the five active provider probes at 10/30/60/120/300 seconds;
- lease-bind all eight authorized `navigate_code` operations, including
  `Implementations` through bounded JDT-LS `GetImplementationsAsync`;
- compare the full persisted canonical Document graph on replay and apply only
  the bounded conditional reconciliation conflict retry; and
- prove the production-composed authenticated PostgreSQL and Windows-host
  CodeGraph 1.5.0, Java 21, and JDT-LS 1.61.0 boundaries. No migration is
  expected.

The authorization excludes Story 12.4 and Story 12.5, Cross-Repository
Resolution, endpoint grouping, `retrieve_code_logic`, Pass 2, Business Logic
Documents, Chat, release UI/evaluation/run, CodeGraphContext, unrestricted
Agent tools, and changes to accepted Story 12.1/12.2 behavior beyond the
accepted additive compatibility boundary.

It authorizes implementation only. It does not waive TDD RED/GREEN evidence,
convergence, fresh quality review, E2E, security, migration-no-delta,
PostgreSQL/host qualification, pinned final verification, checkpoint, or
root-owned commit gates.

## Decision

The user explicitly approved production implementation of this exact scope on
2026-08-22. No excluded scope or downstream gate was waived.
