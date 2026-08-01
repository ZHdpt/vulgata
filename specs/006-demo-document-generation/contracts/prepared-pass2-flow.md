# Contract: One Prepared Pass 2 Flow

One fixed-name, embedded, version-controlled JSON manifest declares one flow,
expected demo System/Repository, an ordered non-empty set of prepared Pass 1
job/Code Unit identities, and finite ordered trace evidence. Its schema is
closed. It carries no raw source, credential, arbitrary query, traversal rule,
or mutable prompt.

At the durable `Pass1Completed` boundary the runner, under caller scope:

1. requires exactly one configured manifest;
2. resolves each declared member to exactly one current Code Logic Document
   Version committed by the same ScanRun/Profiled Revision;
3. rejects missing, extra, stale, duplicate, reordered, ambiguous, or
   unauthorized members;
4. constructs and dispatches exactly one `Pass2BusinessLogicDocument` packet;
5. commits one Business Logic aggregate and reaches `Completed` only after the
   commit returns `Committed`.

The Business Logic result must state a non-technical purpose, ordered main
steps, and outcome, with exact Source Anchors/flow evidence. Its
`GENERATED_FROM` target set equals the synthesis version set. Any dispatch,
validation, authorization, commit, conflict, rejection, or cancellation failure
leaves the Scan non-completed. General entry-point discovery, path-state
traversal, flow clustering, multiple Pass 2 jobs, and narrative-derived base
`REFERENCES` Edges are out of scope.
