# JSON format rollback and stream review

## Scope

The user requested restoration of json_object where JSON is expected and a review
of possible trailing-stream loss. The user clarified that only response format
should roll back. A pre-existing broad uncommitted Worker rollback was preserved
in an ignored local patch, then replaced by the committed Worker to retain hard
correction limits, tools-disabled guards and safe syntax feedback. No paid scan,
provider change, budget increase, shared schema change or other lane edit.

Correction requests now use `ChatResponseFormat.Json`, which the actual SDK wire
test maps to `response_format.type=json_object`. The old
`UseTextForJsonCorrection` flag remains accepted for configuration compatibility
but cannot select text. Initial/tool requests already used JSON. Non-JSON Chat
and other consumers are unchanged.

## Prompt inspection

`OctoberFileWorker.md` is supplied as the system message and explicitly requires
one complete JSON object, all closing delimiters, the exact schema, no Markdown
fences and no additional prose. `OctoberFileWorkerCorrection.md` is supplied as
a user correction message and explicitly requires the entire replacement JSON
object, not a continuation fragment. The actual-wire tests assert both roles and
these requirements. No prompt amendment was needed.

## Independent stream review

No confirmed loss of valid pre-DONE content was found in the reviewed Scan path.
The Worker awaits enumeration to completion, appends every text content item, and
processes content in the same update before storing its finish reason. SDK-delivered
later content after a finish becomes an explicit terminal failure, not silent truncation.
Cancellation/timeout and incomplete settlement are failure paths. The factory
iterates every SDK update and marks SDK completion only after enumeration ends.
The observer forwards original bytes without rewriting them and retains split
UTF-8/SSE data until complete event boundaries.

Two evidence gaps remain:

1. HTTP observation is passive and calls status Complete when SDK enumeration
   completes and either DONE or EOF was observed. DONE without EOF is not proof
   the HTTP body was exhausted. The existing unread-tail test demonstrates that
   later bytes can remain unread, with ContentAfterDone unknown. Such bytes occur
   after the expected Chat Completions terminator; the test does not demonstrate
   loss of a valid pre-terminator chunk.
2. The HTTP/SDK/parser comparison is exported only on proposal parsing failure.
   Successful proposals and ordinary prose do not provide equivalent retained
   comparisons, so failure-only observations cannot establish absence or frequency
   of silent losses across all successful responses.

The exact installed package metadata points to OpenAI2.10.0 commit
edc1b51f9138375a16fc078b9ce175de76529136 and Microsoft.Extensions.AI.OpenAI10.6.0
commit c8437bc222c86c535ef6d763468d655737b1261d. Upstream
[AsyncSseUpdateCollection](https://github.com/openai/openai-dotnet/blob/edc1b51f9138375a16fc078b9ce175de76529136/src/Utility/AsyncSseUpdateCollection.cs)
returns false at `[DONE]` and disposes the response when enumeration is disposed;
there is no JSON-versus-text branch. The pinned
[Microsoft adapter](https://github.com/dotnet/extensions/blob/c8437bc222c86c535ef6d763468d655737b1261d/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs)
enumerates streaming updates and converts their content. This supports protocol
sentinel termination, not a diagnosed SDK defect.

A missing suffix is easier to detect with strict JSON parsing than with prose.
That is a detection asymmetry, not evidence that ordinary text is routinely lost.
Future diagnostic work could compare bounded text-free facts for successful and
failed terminals and distinguish protocol completion from body EOF, preserving
original deadlines. No such stream-behavior change is included in this rollback.

## Verification

RED `isolation-cdfe856c6d1a4d56a44e8bb88e3e8d15`:4selected,2passed,2failed,0skipped.
Both legacy-flag=true cases expected json_object but observed text; false cases
passed. Prompt assertions passed. Historical paid failure evidence remains unchanged.

The first affected selection `isolation-0c4dd110a6994d18a951fc9ad61d119d`
selected543, passed541 and failed2 new tail-test cases. That fixture emitted an
undeclared initial tool call before reaching the intended tail. It was replaced
with the existing SSE helper and a direct actual-SDK adapter test, avoiding new
custom stream plumbing. No production guard was weakened to make the fixture pass.

Final affected SelfContained selection
`isolation-6774592990324288985ee61a8cdb4579`:543selected,543passed,0failed,0skipped.
The paired SDK test sends json_object and text in separate controlled requests;
both preserve exact Unicode text and the final `]}` supplied in a separate SSE
content event with Stop, with raw non-ASCII bytes and one-byte HTTP reads.
This is adapter-level controlled evidence, not a real provider or ordinary-Chat
qualification. Existing Worker aggregation, correction, hard-mode and terminal
guard tests also passed. Independent review accepted the scoped commit with no
remaining findings. The earlier full-suite4864/4865 result and its known unrelated
report-parser failure are not replaced by this affected selection.
