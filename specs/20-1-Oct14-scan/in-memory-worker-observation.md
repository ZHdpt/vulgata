# Bounded Worker response observation

2026-10-10. OCT-Integrator relayed the user's explicit approval of one bounded
in-memory comparison of the failing Worker's HTTP response and SDK text, with no
retention of raw responses. Integrator owns that single runtime operation and its
resources. Scan owns only the minimal observation seam, controlled verification
and independent review. No unrelated repair, further paid run, native tool,
PostgreSQL operation or historical raw-capture access is authorized in this lane.

The starting lane checkpoint is `6734662dad3bd3b1e0bf714707bb4d80aecab141` on
`oct14/scan`; the affected factory/executor production files match Integrator's
approved `d57d42e5`. Existing work is preserved without reset or replacement.

## Observation boundary

The observer is default off, bound to the exact original execution/session/run
and configured target, batch, subject and management authority. It cannot be
combined with persistent raw capture. Only the actual final failed proposal leaf
may produce a finite diagnostic snapshot. Earlier tool/window leaves are reset;
success, cancellation, replacement and disposal discard retained raw references.
This is reference release, not a guarantee of zeroing managed memory.

The three observations are independent:

- HTTP response events decoded by a passive incremental stream tee.
- SDK `TextContent` collected in the factory before yielding it to the Worker.
- The exact final text submitted by the Worker to its unchanged proposal parser.

The tee performs no read-ahead or drain and preserves the existing read results,
exceptions, cancellation, terminal behavior and disposal. It does not repair text,
change acceptance, retry, alter budgets or replace streaming with whole-response
buffering. Diagnostic parsing faults and limits disable the observation rather
than fail the original response.

Bounded observation limits are 4 MiB observed wire bytes, 512 KiB per SSE event,
262,144 UTF-8 bytes per final HTTP/SDK text accumulation and 64 KiB read scratch.
The original run and lease bound lifetime. Unsupported framing/choices, overflow
or insufficient observation makes comparisons indeterminate. No unread tail is
reported absent merely because SDK enumeration completed; EOF and DONE are
separate observations. Post-terminal content describes only actually read bytes.

HTTP text equality concerns content events actually observed by the tee, not the
entire unread response. If an already-read buffer contains content after DONE,
that content participates in the HTTP aggregate and is flagged as post-terminal.
The SDK normally ignores such content; a resulting mismatch establishes observed
out-of-protocol delivery, not SDK loss of validly ordered content. If the tail was
not read, its contents and absence remain unknown.

Only fixed enum, boolean and numeric facts may be retained: actual cap key/value,
HTTP and SDK finish reasons, DONE/EOF, observed post-terminal content, lengths and
equality, conservative JSON syntax/depth/EOF/missing-delimiter classifications,
and known usage counts including reasoning tokens. No request, header, credential,
response, reasoning text, source-derived property name, raw hash or exception text
belongs in the snapshot, test failure output or logs. Fresh source/contributor
authority and existing disclosure/replay fences still apply.

## Interpretation limits

Equal incomplete HTTP, SDK and parser text locates the observed defect upstream
of SDK/Worker text assembly; it does not distinguish the remote provider from an
intermediary or prove why generation ended. Complete HTTP content with different
SDK text isolates a client mapping boundary only when framing and observation are
complete enough to support that comparison. SDK/parser inequality isolates the
Worker boundary. Overflow, unsupported framing or unread data cannot justify any
of those conclusions. The diagnostic can establish only what its observed
boundaries support; it cannot promise a deeper root cause.

Independent design review accepted these constraints. Implementation verification,
exact opt-in wiring and runtime evidence remain to be recorded below; design
acceptance is not runtime qualification.

## Planned root opt-in

The approved configuration shape is process-local environment configuration:

```text
October__WorkerHttpObservation__Enabled=true
October__WorkerHttpObservation__TargetId=<exact selected target>
October__WorkerHttpObservation__BatchId=<exact selected batch UUID>
October__WorkerHttpObservation__SubjectId=<original session subject>
October__WorkerHttpObservation__ManagementAuthorityId=<original authority>
```

There is no raw-response destination. `October:ParseFailureCapture` must remain
absent or disabled without leftover capture bindings. These values enable only
observation within the already authorized original run; they authorize no extra
run, resource, source or provider. The finite result is projected under
`workerDiagnostics.httpObservation`. An absent observation is not evidence that
HTTP and SDK text matched. `Complete` describes diagnostic sufficiency, not
accepted output or a successful Generate operation.

## Controlled evidence

The initial inert-scaffold run `d75aa29cd6554747b3048de0a97f8afb` selected eight
Worker cases: one passed, seven failed, zero skipped, current build zero errors.
All seven failures occurred at the unimplemented exact-run binding (`TryBind`
returned false); default-off passed. This is behavioral RED for missing activation.
The downstream stream/text/classification assertions were not reached in this run
and still require passing evidence. No production observation behavior was present.

The first complete implementation run `c67f433dae9441c0bb6f8db2a6c0056e`
passed **377/377**, zero failures/skips. This reached the downstream comparison,
framing, limit, configuration, binding and report assertions. Independent review
nevertheless found two diagnostic truthfulness defects requiring correction:
zero-length requested reads must not imply EOF, and later EOF after observation
overflow must not imply absent DONE/post-terminal events in the unparsed tail.
This GREEN does not close those review findings or authorize runtime dispatch.

Focused review-counterexample RED `8d22410ab2464314be1aac2a27fc452b` selected 18
cases: 13 passed, five failed, zero skipped, build succeeded. Four read-overload
cases reproduced false EOF from a zero-length request. The actual SDK overflow
case reached real EOF after observation stopped and reproduced a false negative
DONE flag instead of unknown. The other 13 observation cases continued to pass.

The correction observes a zero-byte result as EOF only for a nonempty requested
read. Negative event-derived facts become null after failed/partial event
observation, while positively observed events and actual EOF remain available.
Independent final review closed both findings and accepted the implementation
pending the final current-source regression result. No transport, acceptance,
budget or retry behavior was changed by these corrections.

Final GREEN `f3f220bab7f541f99d978536f5a3c447`: **382/382 passed**, zero failures
or skips, current build zero errors, standard SelfContained launcher over the
complete Worker, GenerateRunner, EntryPoint and ExecutionScope classes. The TRX
confirms all five review counterexamples passed. Separate test/production owners
and independent review accepted the boundary; `git diff --check` passed. Existing
status, usage and settlement assertions remain intact. No provider/native/
PostgreSQL qualification was performed in this lane, and no real failure cause is
inferred from controlled observations.

The opt-in above is implemented. Omission or a sole `Enabled=false` disables it;
partial/unknown/mismatched configuration is rejected without content-bearing
diagnostics. The existing DeepSeek adapter supplies the observed cap key/value;
other endpoints have unknown cap fields because no additional request-body
inspection is performed. Neither the observer nor its report persists raw data.
