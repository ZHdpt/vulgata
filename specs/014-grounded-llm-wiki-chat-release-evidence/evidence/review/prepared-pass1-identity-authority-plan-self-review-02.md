# Prepared Pass 1 identity-authority plan self-review 02

Disposition: **PASS — ready for fresh independent plan-only review**

Reviewed at: `2026-08-30T06:12:26.6554047Z`

Source commit: `d84788f4d4cb7536dac119c62c60cd77352adeb1`

Specification SHA-256 (unchanged): `469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0`

Planned-change decision: `story-12-5-planned-change-prepared-pass1-identity-authority-02`

Approved decision SHA-256: `7a9e3fc5fc4b9485cd7538e61f2b2153a81bcc8388a89935750d5a29dbf931b2`

## Correction reviewed

The corrected plan removes `IScanRecoveryDispatchIdentitySource` from Prepared Scan initiation. It adds one dependency-free Core value/port, `PreparedPass1ContractIdentity` and `IPreparedPass1ContractIdentitySource`, and one sole production implementation in Agents, `EmbeddedPreparedPass1ContractIdentitySource`. That implementation binds the exact embedded `pass1-code-logic-worker.v2.md` dispatch text to `pass1-code-logic-worker.v2/sha256/{lowercase-64-hex}` and exact Document schema identity `code-logic/file-v2`.

`WorkerAgentDispatchIdentitySource` remains the separate unchanged durable-recovery authority for the v1 prompt and `vulgata.knowledge-document/1.0`. No adapter, fallback or coercion connects the two authorities.

## Source feasibility evidence

- `src/dotnet/Vulgata.Agents/WorkerExecution/WorkerAgentDispatchIdentitySource.cs` proves the legacy source hashes the v1 prompt and returns `KnowledgeDocumentLimits.SchemaIdentity`.
- `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1WorkerDispatcher.cs` proves current Prepared Pass 1 dispatch loads logical resource `Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md` and rejects a Document schema other than `code-logic/file-v2`.
- `src/dotnet/Vulgata.Agents/Vulgata.Agents.csproj` proves the v2 prompt is an embedded resource and Agents references Core.
- `src/dotnet/Vulgata.Web/Vulgata.Web.csproj` proves Web already references Core and Agents; the new SDK-globbed source files require no project-file edit.
- `src/dotnet/Vulgata.Web/Data/PreparedScanInitiationCoordinator.cs` proves the mismatch occurs before the fresh authorization recheck/store and identifies the exact dependency replacement seam.

## Inline review lenses

| Lens | Result | Review evidence |
| --- | --- | --- |
| Feasibility | PASS | Core can own the two-string contract; Agents owns and embeds the exact v2 resource; Web can consume/register the sole implementation. No new dependency direction or unavailable technology is required. |
| Accepted intent | PASS | FR-066 already requires the current prompt and Document schema identities. The specification remains unchanged and its SHA-256 still matches the accepted value. |
| Layering | PASS | Core declares only a dependency-free value/port; Agents owns prompt-resource interpretation; Web orchestrates; Infrastructure remains the only EF/Npgsql owner. |
| Identity invariants | PASS | Prompt version, exact SHA-256 grammar and exact `code-logic/file-v2` are closed. Null, v1, malformed hash and wrong schema fail before authorization recheck or persistence. Replay continues to bind prompt/schema in the canonical execution identity. |
| Authorization/currentness | PASS | Initial, fresh pre-store and pre-publication authorization checks are unchanged. Identity failure creates no store call and exposes no protected tuple detail. |
| Failure behavior | PASS | Missing, duplicate, invalid UTF-8, empty or schema-inconsistent resource returns Not Ready; the endpoint maps it to bounded 503. No fallback to the legacy authority, Web constant or caller field exists. |
| Test seams | PASS | Direct authority tests independently hash the embedded v2 prompt and pin schema; composition proves exactly one Agents mapping and no legacy coordinator dependency; hostile source values prove 503/zero recheck/store; recovery regression pins unchanged v1 output; public/PostgreSQL initiation tests remain the vertical and atomicity seams. |
| Agent-tool/Scan effects | PASS | The port exposes identity only. It owns no prompt content output, provider, dispatch, retry, scheduling, parsing, persistence, Agent tool or endpoint request behavior. Existing dispatcher/provider/tool behavior is explicitly forbidden from change. |
| Migration/compatibility | PASS | No EF migration, `VulgataDbContext`, configuration, model snapshot or project-file edit. Legacy recovery identity/tests, Story 12.4 and accepted Track A/B behavior remain unchanged. |
| Transaction/concurrency | PASS | Identity resolution remains before the existing short atomic transaction. Advisory locking, replay dimensions, phase recovery and no-long-running-work bounds are unchanged. |
| Constitution I-V | PASS | Dependency direction, Ubiquitous Language, API/validation behavior, integration-first TDD and security/data stewardship remain aligned. |
| ADR/architecture | PASS | Story 12.1 AD-10/no-CGC remains intact. The architecture trigger is explicit and covered by the accepted planned-change decision; independent plan review and a new implementation approval remain required. |
| Scope exclusions | PASS | No general/single/N-Repository Scan lifecycle, historical profiling repair, Story 12.4 mutation, later-stage execution, fake evidence, retained database contact or run-03 work is planned. |

## Alternatives and consequences check

The plan explicitly rejects changing the legacy recovery source, hardcoding identity in Web, accepting the knowledge schema, exposing caller-selected identity, and injecting dispatcher/provider execution. The selected consequence is one new Core contract file, one new Agents implementation file, one DI mapping and the narrow coordinator dependency replacement plus behavioral/composition tests. It does not add a new Agent tool or change Worker behavior.

## Complete corrected plan-set hashes

| Artifact | SHA-256 |
| --- | --- |
| `plan.md` | `6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8` |
| `research.md` | `a7f60efb0095fbd484404cca5c612fcf4957a75cfe49d6a74d2f0e21b00aed7f` |
| `data-model.md` | `8842229e03143a72c45b3a0d32888875451b0a4d271adc90b224caa3c384f94d` |
| `quickstart.md` | `f1985e42023ea8641457fb854f17627cddbb887a831672187273362e2e9e03ec` |
| `contracts/prepared-scan-initiation.md` | `2234243212da60fddf94287f4b4946d57d2a600be3161c4d2673b4ae624b7c6d` |

## Conclusion

`PLAN_READY_FOR_INDEPENDENT_PLAN_REVIEW_ATTEMPT_03`.

The plan is internally consistent, source-feasible and bounded to the accepted identity-authority correction. No task generation or production implementation is performed by this review. After independent plan acceptance, the orchestrator may regenerate the affected tasks and run independent definition review under the accepted planned-change decision; a newly scope-bound implementation approval remains mandatory before the first production write.
