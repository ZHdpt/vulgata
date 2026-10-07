# Controlled Chat checkpoint — 2026-10-07

## Authority and boundary

OCT-Integrator dispatch from task `01a1109d-ec4c-7500-8b73-642a5e22f59b` relayed the user's instruction: "Execute this round of integration, pause before next development action. Show me exactly next suggestion steps before quit." The dispatch authorizes a local commit of the reviewed controlled Chat source/tests/specification evidence in this assigned worktree, then stop. It supersedes the earlier uncommitted status only; it authorizes no new implementation, live activation or merge here.

Source branch: `oct14/chat`. Parent checkpoint: `803142072cabd3606b2121ed6fc142c80aa4c881`. Planning baseline `fad7f11` is an ancestor. The commit containing this handoff is the import checkpoint; root reports its exact SHA after creation. Integration plans to merge and verify against `c0cf972f`; that target was not merged or qualified in this lane.

## Included changes and task status

- Existing parent: controlled T001–T006 completion.
- This checkpoint: controlled T008–T011 contracts, budgets, canonical wiki facade and consumed-read ledger; approved internal T013 typed-client MAF agent/adapter; fixed-resource prompt loader/text; controlled turn events, view model/component and narrow ChatPage bindings; six new classified SelfContained test files plus the existing controlled fixture; lane-local status/review/verification and owner dependency handoff.
- T007 remains open for W0. T012/T013 remain partial despite the accepted controlled adapter: raw HTTP/SSE, selected ChatCompletions/profile, credential/retry/admission/permit integration and runtime qualification remain outstanding.
- T014/T015 retain controlled prompt-loading evidence; actual embedding and semantic qualification remain outstanding. T016–T019 require Business-owned dialogue and complete disclosure-dependency contracts. T020/T021 retain controlled UI evidence without actual coordinator/provider/browser integration or complete rich rendering. T022–T038 remain unexecuted.

No Business shared contracts, persistence/migrations, Program.cs, package/project files, provider registration or root agent instructions are included. Inventory changes are only the six reviewed Chat SelfContained entries and retain existing classifications. No unrelated edits were found during checkpoint inspection.

## Verification and retained limitations

Independent review accepted the controlled source after regression-backed fixes for bounded pending iteration, preterminal input limits and disposal lifetime. Both test and production owners accepted final shape. No unresolved failing case or actionable review finding remains in this controlled selection; historical RED evidence is retained in quickstart.md.

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat|FullyQualifiedName~Vulgata.Tests.October.Knowledge|FullyQualifiedName~Vulgata.Tests.ChatPageTests|FullyQualifiedName~Vulgata.Tests.ChatConversationStateTests'
```

Latest result: **291 selected/executed/passed, zero failed/skipped**, including 40 provider cases; discovery 3,572 and inventory 330. Persisted local evidence: `TestResults/isolation-ccd63f8f85234e7dba0d29a19fef51ef/results.trx` and its `build.log`. Build: zero errors, 5,956 warnings including AD0001 analyzer exceptions. Logs are local generated artifacts, not included in Git; quickstart.md preserves the results. Source/tests have not changed since that GREEN; this checkpoint only adds handoff documentation. No rerun is claimed for this commit operation.

This is an affected SelfContained selection, not full-suite or live qualification. PostgreSql, LocalTools, External, E2E and HistoricalBoundary remain excluded. No live provider, Web host or database was started. The adapter bounds consumer waits but cannot forcibly stop opaque borrowed I/O; W1 must retain real request permits through eventual underlying cleanup. Consumed-read reports are not full-input disclosure authorization.

## Integration-only actions and next-development dependencies

For this round, integration should import the checkpoint, preserve its partial statuses, reconcile only the reviewed ChatPage and suite-inventory additions with its current tree, and run the affected filter above plus checks appropriate to any actual merge changes. Stop before new development or live activation; record the merge SHA and integration results separately from this lane's evidence.

The next proposed development work, after that pause, is owner delivery of W0 real reader/host composition; Business D0 dialogue/fencing/save/history and complete contributor reauthorization semantics; and W1 selected provider/profile/protected credentials/Web request permits. Integration also owns future prompt resource registration with the exact logical names recorded in `contracts/imported-k0-and-w0.md`. These are requirements, not edits authorized in this round. Chat must not invent replacement contracts, reuse generator admission as Chat admission, or activate the typed-client path before its dependencies are satisfied. No new per-checkpoint human gate is added; the current pause is the user's explicit instruction.
