# Story 12.5 Ownership and Identity Manifest

## Track ownership

| Boundary | Owner | Restrictions |
|---|---|---|
| Track A production Scan status/UI | Track A TDD slices | No Chat retrieval or provider-streaming changes |
| Track A evaluation fixtures/harness | Track A TDD slices | Preparation only; no Tier 3 observation claims |
| Track B Chat/retrieval/streaming | Unassigned while gated | Requires Story 12.4 final acceptance and integration |
| Track C release execution/evidence | Unassigned while gated | Requires accepted Track B |
| Workflow state, decisions, evidence, Git | Active root | Workers never stage or commit |

## Shared integration files

`src/dotnet/Vulgata.Web/Program.cs`,
`src/dotnet/Vulgata.Agents/DependencyInjection.cs`, project files, and central
endpoint registration have one root-coordinated owner at a time. Track A may
edit only `Program.cs`, and only for the accepted T019 Scan status registrations
after T011 and T013 are green. Agents DI remains outside Track A.

## Forbidden ownership

No Story 12.5 worker owns `VulgataDbContext`, EF migrations,
`VulgataDbContextModelSnapshot`, Story 12.4 worktree files, Story 12.4 temporary
directories, its qualification database, or the main development database.

## Evidence identity template

Each executable evidence record must bind the full candidate commit, command and
arguments, tool/runtime versions, provider identity where applicable, database
identity, input artifact hashes, output artifact hashes, UTC observation time,
exit/result status, warnings, and redactions. Provider-stream receipts must remain
content-dark. Templates are not observations and cannot satisfy release evidence.
