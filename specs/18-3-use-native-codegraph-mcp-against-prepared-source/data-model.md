# Transient Data Model: Native CodeGraph Tool Boundary

No persistent entity or migration is introduced. These are conceptual, request-scoped records for the Story 18.3 side of the F0 handoff; exact 18.1 type names are deliberately unresolved until its integrated baseline is verified. [Plan ID-I01/S01](plan.md).

| Record | Fields and validation | Lifetime / relationship |
|---|---|---|
| Prepared Repository handoff (owned by 18.1) | Approved Repository/root binding and current purpose/grant; never a model-selected path. | Consumed per request; revocation or change invalidates CodeGraph handles. Any trusted physical-access mechanism is reconciled with 18.1, not assumed as a lease supplied by it. |
| Native session | Binding/grant epoch, declared installation identity, connection state, discovered qualified tool names and owned process/client handle. | Finite, binding-scoped; disposal on completion/cancellation/failure; no cross-root reuse. |
| Native tool grant | Worker purpose, reviewed native tool name/schema subset, bounds and binding reference. | Validated before discovery exposure and invocation; unknown/unqualified tools denied. |
| Readiness observation | Separate preparation, connection and discovery states; sanitized limitation/failure. | Request/host observation, not semantic-completeness proof or persistent Knowledge. |
| Invocation outcome | Granted tool name, bounded result or typed failure, relative source provenance where safely verified, incompleteness/limit indicator. | Disclosed only after current grant and returned-location checks. Empty result is not proof of source absence. |

## State transitions

`Unprepared -> Preparing -> Prepared -> Connecting -> Discovered -> Invoking -> Completed` is the successful route. Each transition can terminate as `Unavailable`, `Failed`, `TimedOut` or `Cancelled`; discovery can produce `NoGrantedTool`, and a result can be `Limited`. A failed/cancelled state closes its owned session. A stale/revoked binding moves directly to denial and cannot re-enter through a saved handle. Successful preparation and connection are not equivalent to successful native invocation. [Spec FR-004–FR-006](spec.md); [AD-4/6](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md).
