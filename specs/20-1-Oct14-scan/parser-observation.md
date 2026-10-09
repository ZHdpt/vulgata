# Bounded proposal parser observation

2026-10-09. OCT-Integrator (`01a1109d-ec4c-7500-8b73-642a5e22f59b`) approved
one additive parser observation after the settled real run reached
`InvalidOutput` / `ProposalParse`, retry `None`. Allowlisted report fields showed
two requests, 7,883 input and 4,037 output tokens with usage known, and null terminal,
budget and tool causes. No model response, source, arguments or secrets were read.

This work observes rejection; it does not repair, retry, normalize, change the
proposal schema/prompt, or alter acceptance, validation or status classification.
Scan starts clean at `877fb8a4e023b6a3ff7566488d4b0f490e669f57`; the affected
production matches integration `cb5008c3`. No reset or replacement was needed.

## Additive report contract

The existing `workerDiagnostics` gains `parseFailure`, either null or exactly:

```json
{
  "category": "ClosedFields",
  "slot": "Claims"
}
```

Both members are nullable and admit only fixed enum names. Categories are `Syntax`,
`RootShape`, `ClosedFields`, `ValueShape`, `Qualification`, `EvidenceIndex`.
Slots are `Root`, `Units`, `Sections`, `Claims`, `Anchors`, `Observations`,
`Uncertainties`, `Evidence`, `SemanticSelector`, `ParentSelector`, `Title`,
`SectionId`, `Ordinal`, `ClaimGroupId`, `Text`, `Qualification`, `Limitations`,
`ObservationKey`, `OwnerSelector`, `Role`, `Mechanism`, `Operation`,
`LogicalNamespace`, `LiteralsOrExpressions`, `Key`, `Explanation`, `AdvisoryIndex`,
`StartLine`, `EndLine`. Container slots do not contain array positions. Member
names are schema constants, never names copied from an encountered JSON property.

The snapshot contains no encountered keys, values, numeric indices, paths, excerpts,
response body, reasoning, exception message or stack. Preserve the first actual
observed parser guard failure. Unknown detail stays null rather than being inferred
from exception text or a later validation failure. Only a defined failed Worker
status at `ProposalParse` may expose it. Completed, other-stage, cleared and revoked
reports receive no parse cause. Existing report disclosure fences remain authoritative.

## Behavior preservation

Observe existing operations without reordering deferred LINQ or prevalidating JSON
into different exceptions. Rethrow the original exception: numeric conversion and
wrong-kind failures that currently yield `ProviderFailed` must keep that status.
Root syntax/shape, closed fields, values, qualification and advisory references have
explicit observable guards. Host transformations such as content canonicalization
do not justify an invented parse category. The transition to `ProposalValidation`
still precedes the zero-unit and owner-validation guards; those never acquire a
parse cause.

Separate owners retain tests and production. Public enum/record declarations and
nullable properties were added as inert contract scaffolding before typed RED;
they did not capture, forward or project any observation. Execution evidence and
independent review are recorded below when complete.

Typed RED with the inert declarations is
`8f426be5b9394b758f2c2f47be1dd622`: **221 selected, 194 passed, 27 failed,
zero skipped**, build zero errors. The standard SelfContained launcher selected
the complete FileWorker, GenerateRunner and EntryPoint test classes. Failures were
missing parser snapshots, missing forwarding/projection, and the additive JSON
field whitelist; preceding legacy outcome assertions remained intact. The tests
include the existing parser matrix, first-guard precedence, exact safe projection,
unknown enum scrubbing, validation-stage absence and revoked replay suppression.

Final GREEN is `148e69199e9f43a4894c9afd15d1adc6`: **221/221 passed**, zero
failures/skips and build zero errors, through the same three complete affected
classes. Both separate owners and the independent reviewer accepted the unchanged
reviewed implementation with no remaining blockers; `git diff --check` passed.

Capture is per Worker invocation and uses a single first-observation snapshot.
Known JSON read exceptions are rethrown unchanged; explicit existing guards record
their fixed category before their original throw. No diagnostic is attached to
host canonicalization failures merely because parsing was in progress. Runner
mapping and final JSON projection both enforce the failed-status/parse-stage
boundary and sanitize category and slot independently. Existing binding, live
authorization and metadata-only replay clearing continue to govern disclosure.

Changed production is limited to the Worker executor, Scan report snapshot, runner
mapping and Generator JSON projection. Three existing test files and this record
complete the checkpoint. No event-log framework, prompt/proposal schema change,
new resource binding or runtime qualification was introduced. The actual live
parser rejection remains unknown until a separately authorized diagnostic run.

No paid/provider/native/PostgreSQL/source-root/global operation is authorized by
this dispatch. Integrator owns any subsequent diagnostic qualification run.
