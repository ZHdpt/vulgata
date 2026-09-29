# Story 18.2 Worker Navigation Interface

This is the navigation-specific F0 consumer contract for plan review, not an independent Story 18.1 root/list/read/search contract or a claim that code already implements it. Exact transport/type names follow the integrated 18.1 contract.

## Invocation

- Application grants one selected prepared Java Repository and chooses the fixed restricted JDT-LS profile outside model arguments.
- Worker may request `definition` or `references` for a path relative to that grant, a source position and bounded result controls. It cannot submit a physical root, adapter, executable, workspace, Git revision, import/build mode, command or write operation.
- The tool validates live grant and input containment before JDT-LS use. At most one owned session exists for the selected Repository; unsafe shared operations are serialized.

## Result

- Return contained Repository-relative locations and ranges actually observed, the request kind, an explicit response limit/truncation indicator, and any capability/classpath limitation.
- Represent no result as `no result observed`. It is not a claim that a definition, caller or relationship is absent.
- Withhold external/unmappable URI locations; report the withheld/limited condition without leaking physical paths or source text.
- Missing configuration/readiness, changed/unavailable grant, protocol error, timeout and cancellation are distinct failure outcomes. Diagnostics are bounded and sanitized.
- If navigation is limited, separately granted 18.1 list/read/search remains callable. It does not satisfy native CodeGraph MCP.

## Illustrative valid and invalid requests

Valid: within the application's granted Repository, `definition` on a relative Java file and one-based position; result points to another contained relative Java file.

Invalid: Worker asks for `C:\\other\\Secret.java`, `../other/Secret.java`, a symlink escape, another Repository or a different JDT-LS executable. Return a denial/limitation and no outside-root source location. An LSP response URI outside the grant is withheld even when the original request was valid.

## Integration hold

Story 18.1 owns the prepared grant and shared location semantics. Reconcile this contract with its integrated, verified implementation before tasks or delivery; a semantic mismatch returns to definition and affected review/approval gates.
