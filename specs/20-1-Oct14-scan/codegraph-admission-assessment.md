# Complete CodeGraph result admission assessment

2026-10-09. Original source-free assessment authorized by OCT-Integrator
(`01a1109d-ec4c-7500-8b73-642a5e22f59b`). Production fixes and further paid/native
runs are not authorized by this dispatch. The clean starting Scan checkpoint is
`27df8f7f4f52e995a9efd40d3419908236cbb542`; the affected ledger, wrapper and native
CodeGraph files match integration checkpoint `9df504a6`.

## Established live boundary

Only allowlisted scalar fields were read from the supplied report:
`ProviderFailed` / `ToolInvocation`, retry `None`, tool operation
`CodeGraphExplore`, boundary `ResultAdmission`, native envelope status `Complete`.
Two requests reported 7,556 input and 1,528 output tokens with usage known.
No source, model text, arguments or secrets were printed or inspected.

This proves that the Complete envelope reached the ledger boundary and was
withheld. It does not prove that its payload was valid, nor identify the particular
ledger rejection. It is not evidence of required CodeGraph unavailability or a
JDT outage. Paid retries remain held; earlier retained partial CL is untouched.

## Admission contract

The Worker wrapper, CodeGraph facade and ledger agree on required nonblank `query`
(at most 512 UTF-8 bytes), optional integer `maxFiles` (1 through 12), default 4,
and no extra request fields. An explicit null is not omission. The ledger uses
root scope `.` and checks the exact Repository identity and defined result enums.
The runtime already checks result Repository identity before returning it.

Complete alone grants no disclosure. The ledger validates every location's scoped
relative path and limits distinct locations across both locations and excerpts to
`maxFiles`. Every nonempty excerpt acquires or reuses an exact same-root,
same-prepared-version source frame. The frame's digest, bytes, identity and currentness
are checked; excerpt text and optional line bounds are checked against that frame.
Unverified free-form graph text is withheld and marked `UncontainedSource`.

Supporting frame acquisition reads the whole file through the existing
contained source service (default maximum 1 MiB), even when the native adapter
disclosed only an excerpt. The native adapter also verifies/rereads each whole file
with a 1 MiB limit before returning; there is no default per-file size mismatch.
Later acquisition/currentness can still fail independently of that earlier status.
The Business-owned limits remain 128 content origins including primary, 128 metadata
observations, and 64 KiB each for the safe request and canonical metadata projection.
Exact contributor union authorization and source currentness are checked before
committing the staged inputs. No competing provenance or authorization policy is
proposed.

The wrapper reports `ResultAdmission` for the whole ledger call. That includes
request/envelope validation, support acquisition, content/range checks, canonical
projection limits, cancellation, currentness and final contributor authorization.
The ledger currently collapses exceptions into denial; the live report cannot
distinguish those causes.

## Candidate mismatch and verification

Native contained parsing validates excerpt text against source with CRLF converted
to LF. The ledger currently compares the excerpt against the raw pinned frame text.
Thus an LF multiline excerpt verified by that adapter can fail the ledger's raw
substring check on a CRLF frame. Single-line excerpts do not establish this mismatch.

This candidate is not attribution of the live failure. No real source file or
native payload was read, and no result is admitted merely because it says Complete.

Other native behaviors make legitimate consumer rejection necessary: `maxFiles`
is validated and sent upstream, but the parser does not subsequently cap distinct
returned files; empty-line stripping can produce an empty excerpt that passes the
producer's empty-substring comparison. The consumer's distinct-file and nonempty
excerpt checks must remain. Native response text is bounded to 256 KiB. It emits
null optional line bounds and structural text, and can repeat locations for multiple
excerpts in the same file; duplicate locations are not extra distinct files.

## Minimal proposed correction and next observation

If approved, align only the excerpt comparison with the producer's deterministic
CRLF-to-LF representation. Preserve the raw retained frame, its exact digest and
identity, currentness, bounds and complete contributor authorization. Do not trim
arbitrary whitespace, relax paths, ignore mismatched text, change schemas or admit
all Complete results. A correction would need its own desired-behavior RED/GREEN
and review. This assessment does not authorize it.

The smallest useful new observation is a finite ledger rejection category, such
as request/envelope, location bound, supporting acquisition/binding/currentness,
excerpt text, line range, projection bound or final authorization. At the excerpt
comparison, `rawMatch` and `crlfNormalizedMatch` booleans distinguish the candidate
without revealing text. Such instrumentation also requires Integrator's decision;
it has not been added here. Keep exception messages, paths, hashes, source/model
text and arguments out of diagnostics.

If the actual native envelope is needed, Integrator can separately authorize one
bounded source-local probe using the existing approved prepared binding, native
runtime and exact contained source reader, with an explicitly chosen query and
`maxFiles` (no provider/LLM call). Feed the actual typed result into the same ledger,
without acceptance or publication. Retain only native status, admission category,
bounded counts, source acquisition status and match/currentness booleans. Stop after
one result and settle the owned runtime. Do not broaden roots, invent a second
generation fixture or use another paid round to obtain these observations. Exact
source/native lifecycle permissions belong to Integrator, not this lane dispatch.

## Controlled verification and review

Run `af12e495f9ac4b929f3377fc82ec55d7` passed **193/193**, zero failures/skips,
build zero errors. The standard SelfContained launcher selected the full
`OctoberScanWorkerInputLedgerTests`, `OctoberScanCodeGraphLifetimeTests`,
`OctoberScanWorkerToolsTests` and `CodeGraphFunctionsTests` classes. All 17 new
cases passed. These characterize unchanged production; they are not RED for a fix.

The actual-parser composition pair reuses the existing private native harness,
injecting its controlled transport and contained reader. It performs discovery and
the actual adapter parse, then passes that result to the ledger with an explicit
synthetic same-Repository frame. It does not launch a native process or accept or
publish Knowledge.

| Retained frame | Actual adapter status/excerpt | Raw substring | CRLF-normalized substring | Ledger result |
| --- | --- | --- | --- | --- |
| LF | Complete / LF | true | true | Allowed |
| CRLF | Complete / LF | false | true | Denied, no completion snapshot |

Both cases retain the original frame text and hash, check null structural text and
optional line bounds, and require one controlled transport call plus repeated
contained verification. The LF case proves the expected source and metadata
contributors. Fifteen additional ledger cases include exact LF/CRLF and single-line
controls, omitted defaults, explicit-null rejection, bad identities/bindings,
stale/unmatched/empty content, duplicate locations and excess distinct files.
Existing revocation, input-limit, schema and runtime checks remain intact.

The test owner and independent reviewer accepted the result with no blocking
findings. The only changed files are the two existing test files and this record.
No new suite entry or generation dataset was needed. PostgreSQL, real native tools,
providers and source-root qualification were excluded. Production correction and
any real observation remain pending Integrator's explicit decision.

## Authorized comparison correction

After test-only checkpoint `febbb239dda97bbec0927d21696b6ea4a3f9c314`,
OCT-Integrator explicitly approved only the excerpt-comparison correction.
Integration imported the assessment as `19ba0953`; its affected files match this
lane's baseline. No reset or replacement of completed work was needed.

The approved comparison applies one deterministic ordinal CRLF-to-LF replacement
to both operands. It does not alter the retained frame, returned excerpt, digest,
identity or contributors. Bare CR, whitespace, Unicode, case and paths receive no
additional normalization. Every surrounding admission guard remains applicable.
The separate test/production owners establish desired-behavior RED/GREEN and
independent review. No new diagnostics, native probe or paid qualification is part
of this dispatch; the live failure's cause remains unproven.

Desired-behavior RED is `1a61256c5d994db89e77ef0a4f5c5587`: **202 selected,
198 passed, four failed, zero skipped**. The actual-parser CRLF case, CRLF source
with LF excerpt, reverse LF source with CRLF excerpt, and mixed-newline positive
all failed against the unchanged raw comparison. Rejection controls passed.
Historical characterization expectations were explicitly replaced by this approved
behavior; raw-text/hash and snapshot-verification assertions remain.

The production delta is only the existing `Contains` expression: each temporary
comparison operand replaces `\r\n` with `\n` using ordinal semantics, followed by
ordinal substring comparison. The original graph result and source frame are not
rewritten. Verification and final review follow below.

Final GREEN is `9698e2a3ea334bed9d9b677076eacebc`: **202/202 passed**, zero
failures/skips and build zero errors, using the same four complete affected
SelfContained classes. Both actual-parser source forms now admit while preserving
their original hashes and verified snapshots. Reverse/mixed newline positives and
whitespace, bare-CR, case, Unicode, no-trim and invalid-line-bound negatives pass.
The test owner, production owner and independent reviewer accepted the unchanged
reviewed code with no remaining blockers; `git diff --check` passed.

Only this record, the two existing test files and the one ledger comparison changed.
No new diagnostics, shared bindings/schema, provider/tool permissions, lifetime or
other limits changed. This is controlled correctness of the approved comparison,
not attribution of the last live rejection or real native/provider qualification.
Integrator owns integration and the separate qualification decision.
