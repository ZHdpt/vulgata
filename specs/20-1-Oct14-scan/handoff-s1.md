# S1 integration handoff — 2026-10-07

## Checkpoint and authority

This commit delivers only reviewed OCT-SCAN S1 **T005–T012** from `oct14/scan`,
based on accepted common CP-K1 `12d92d80339ea622cd26c593ebdb74da59401f91`.
Planning baseline `fad7f11` and imported checkpoint
`3bcd815b09cfbac7418d6644a2a75816e0788ec0` remain ancestors.

Commit authority was verified against the actual user message in OCT-Integrator
chat `01a1109d-ec4c-7500-8b73-642a5e22f59b`: “Execute the item 1
\"Integration checkpoint\".” Item 1 explicitly begins by having Scan commit its
reviewed S1 changes. The dispatch authorizes this commit and handoff, not S2 work.
The exact resulting SHA is reported by this chat; identify this handoff by its
containing Git commit rather than a self-referential hash inside the file.

## Delivered behavior and integration ownership

- `Vulgata.OctoberGenerator/OctoberToolRuntime.cs` and `PreparedSourceBinding.cs`
  compose one bounded Repository tool owner, fresh dedicated JDT leases, scoped
  source reads and the returned CodeGraph descriptor. Incompatible phases serialize.
- Infrastructure's prepared source issuer/context/traversal positively exclude
  exact owned tool state, retain file identity across pages/ranges, fence changed
  authority and bound expiry restart without hiding ordinary same-name source.
- Infrastructure's three CodeGraph seams retain the same descriptor/index across
  bounded replacement sessions and verify excerpts through contained source reads.
- `PreparedJdtProcessLauncher.cs` separates stderr cleanup from exit confirmation,
  terminates validated retained descendants and waits for actual Windows process
  signals before identity-checked state cleanup. Uncertainty stays fenced.
- Six new OctoberScan test/helper files have additive suite entries: three
  SelfContained and three LocalTools. Two existing CodeGraph tests have narrow
  API-shape adaptations while retaining their behavioral/exit assertions.

Integration owns merging and the shared suite-inventory reconciliation; preserve
all other lanes' entries. Knowledge contracts, persistence/migrations, shared Web
composition, root agent instructions, feature selection and packages are unchanged.
No other lane was merged into this worktree.

## Verification carried by this checkpoint

Independent review and both separate owners accepted the final boundary.
[verification-s1.md](verification-s1.md) records the behavioral RED cases,
corrections, exact filters and resource limits. On this commit-preparation turn,
the source/test files and retained TRX results were checked; no code changed and
no tests, native tools, application hosts, providers or databases were rerun.

| Evidence | Result |
| --- | --- |
| `isolation-2e9b1dde97d54d28a9f699af079a901b` | Final affected SelfContained: 296/296 passed, zero skips |
| `isolation-b49df6d245ea4e528fd9d7893c31176e` | 139 LocalTools cases: 138 passed; combined native S1 and all existing selected regressions passed. One new lifecycle fixture teardown failed |
| `isolation-3c909a7540bf499ebe0bd6d7b2b51d4d` | Corrected fixture-only rerun: all six lifecycle cases passed, zero skips; production unchanged |

The combined fixture proves real JDT definition/reference, multiple source pages,
two native CodeGraph sessions, revoked disclosure and confirmed owned cleanup.
The pinned profile uses CodeGraph 1.5.0, JDT-LS core 1.58.0.202605150919 and physical
Java 21.0.12. Source/JDT leases stay at 30 seconds and shutdown at 15 seconds.
No 120-second extension or global PATH change was introduced. This is not generic
process containment, a complete Scan, provider analysis or Knowledge publication.
Earlier failed diagnostic directories remain preserved; the successful fixture
was removed. Exact-ID checks found no remaining matching Java/Python processes.

## Stop and remaining dependencies

**Stop before T013.** Integration may import this commit and perform its separately
authorized combined checks. S2 T013–T024 still needs implementation authority,
Business B0's real services against CP-K1, owned PostgreSQL and provider/spend
authority for live qualification, and Explore/integration reader composition.
B1/B2 is an S3 dependency. No S2 implementation, push or message to active Chat is
part of this checkpoint.
