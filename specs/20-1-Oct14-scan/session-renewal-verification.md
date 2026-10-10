# CodeGraph preparation renewal

The user authorized fixing the host failure, retrying LoginAntiSendOp.java first,
then the whole slice only after success. Prior run returned run_unavailable on
the fifth file after about153 seconds, four provider requests and four committed
acceptances; the underlying exception was not retained in that report.

Inspection found a matching deterministic defect: native CodeGraph sessions expire
after two minutes. OctoberToolRuntime.ExploreAsync already closes and replaces an
expired session once, but PrepareAsync returned Expired directly. Every new file
creates Worker tools and performs discovery through PrepareAsync. Expired readiness
there throws before Worker dispatch and is caught as run_unavailable. This explains
the observed timing and zero fifth request, while the absent original exception
means the historical cause is inferred from this path rather than directly logged.

The scoped fix gives preparation the same one-replacement behavior for Expired,
only while the original runtime remains available. CloseSessionAsync must confirm
disposal before opening another session. Existing run lifetime, native two-minute
session lifetime, replacement cap21, source authorization, fail-closed cleanup,
serialization gate and cancellation remain. No provider retry or schema change.

Previous partial/successful data is retained for diagnosis; no cleanup is necessary
for this fix. Controlled verification and real isolated/whole outcomes follow.

RED `isolation-b1e09e30a288498b92068de2592c978a`: four selected, four failed,
zero skips. Ready replacement returned Expired; old session was not disposed;
repeated expiry retained one session instead of reaching the finite replacement
cap. The cleanup-failure test also exposed its own teardown propagation; final
test explicitly asserts runtime/descriptor cleanup failure and always disposes
its owned source fixture. No production cleanup assertion was weakened.
Independent GPT-6 Luna review accepted the one-shot renewal without findings.

Prepared isolated target `phone-bd2ba2a54c6f`, batch
`65c49c1f-1cc3-49a1-87a7-b4583b6c4a3f`, database
`vulgata_oct_pilot_4c0f6527bff94aab9dfef78f651b391e`: only channel
`src/main/java/cn/com/yitong/actions/domain/login/LoginAntiSendOp.java` selected,
same approved supporting source, low reasoning/cap393216, CL-only. Bootstrap,
prepare and select passed; no paid request during preparation.

Full SelfContained `isolation-229a2fea7d334e6e967e7bc26384c3ce` passed4791/4792,
zero skips. All100 ToolRuntime/CodeGraphLifetime/GenerateRunner cases passed,
including all four new cases. Sole failure remains the known
OctoberGeneratedClReportTests exact-report projection mismatch. No full-suite
GREEN claim. Generator executable/test copies agree at SHA-256
`A5696989EBB4174C4E5FFC5AFFEF34978F5A08CA20E0070577E6794B391573F1`.
