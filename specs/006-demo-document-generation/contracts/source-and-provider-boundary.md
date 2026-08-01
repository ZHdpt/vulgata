# Contract: Source Hydration and Provider Execution Boundary

## Pass 1 hydration

Given a validated packet and caller `RepositoryAccessScope`, the hydrator:

1. proves System/Repository/ScanRun/Code Unit/Profiled Revision authorization;
2. acquires the existing trusted committed Repository snapshot lease and checks
   exact revision equality;
3. validates the normalized Repository-relative path and inclusive line range;
4. reads only that file/range from the committed snapshot;
5. verifies File Identity Hash and exact source-slice SHA-256, then rechecks the
   lease revision;
6. enforces byte/line/request bounds before returning transient context.

Any mismatch returns a closed non-valid result before provider execution. No
working-copy path, arbitrary filesystem read, raw packet source, or logging of
hydrated content is permitted.

## Pass 2 context

The resolver loads only authorized current Code Logic Document Version
sections named by the prepared manifest and trace evidence. Repository,
ScanRun, Profiled Revision, currentness, exact-set, and canonical-order checks
precede provider execution. Pass 2 performs no source discovery, graph traversal,
or clustering.

## Provider

- Existing System Worker Agent override, otherwise existing global Worker
  Agent assignment; no unconfigured fallback.
- Centrally pinned Microsoft Agent Framework OpenAI-compatible adapter.
- Credential decrypted only by the scoped execution factory, never placed in
  packet/prompt/result/log/exception/durable evidence.
- Fixed embedded `pass1-code-logic-worker.v1.md` or
  `pass2-business-flow-worker.v1.md`; runtime prompt replacement is forbidden.
- Exactly the configured endpoint is contacted. Self-hosted assignment has no
  external fallback, telemetry export, retry, or failover.
- Cancellation is honored through hydration, HTTP execution, parse, and
  validation and never fabricates a valid result.

Loopback protocol tests capture requests and prove minimal source/context,
schema selection, prompt resource use, endpoint selection, and absence of
credentials/unrelated source/absolute paths.
