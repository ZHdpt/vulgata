# Correction-only text response-format experiment

The user authorized a narrow evaluated fix and another bounded whole-run series
after the first series failed. Initial whole plus10restarts produced no complete
publication. Most failures were normally-ended incomplete final JSON; separate
source approval/descriptor lifecycle defects were corrected and separately tested.
Restart10 hit the existing cumulative input-byte limit. That limit stays unchanged.

Consulter final advice and independent plan assessment accept this experiment:
`UseTextForJsonCorrection` defaults false in Scan policy and Worker limits. When
true, only the existing single eligible correction changes its cloned request
ResponseFormat to Text. Initial, tool-continuation and next-file requests remain
JSON-object mode. Same endpoint, low reasoning,393216 cap, prompt, replay/history,
tool declarations and tool-choice-none; host rejects any correction tool call.
No new attempt, carrier, beta endpoint, extraction, delimiter repair or schema.
The local duplicate-aware parser, evidence/semantic checks, authorization, budgets,
deadlines, settlement, atomic acceptance and exact publication remain unchanged.

This is a hypothesis about generation mode, not evidence of the provider's internal
cause. Success without a correction does not qualify correction recovery. Neither
text mode nor a completed Stop can make malformed JSON acceptable. A budget refusal
is not an unexpected syntax error and does not receive an allowance reset.

Owned production: OctoberScanPolicy.cs, OctoberGenerateRunner.cs,
Vulgata.Agents/WorkerExecution/OctoberFileWorkerExecutor.cs. Existing controlled
wire/runner/parser test classes own verification; shared contracts/store/Identity
remain unchanged. Hardmode stays off and unimplemented during the comparison.
After controlled tests and independent implementation review, retry the latest
failed account POM alone, then up to10 fresh whole runs, stopping at the first
complete successful publication. New authority/settlement defects stop affected
runtime work for diagnosis rather than blindly consuming the remaining attempts.

Controlled RED `isolation-cd67f29a2d0b4c1a9117a704c6738b26`:31selected,
29passed,2failed,0skipped. Both failures are actual controlled SDK wire checks:
opt-in Worker and runner correction expected text but still emitted json_object.
Production correction now propagates the flag and changes only cloned correction
options. Independent implementation/test review accepted without findings.
Full SelfContained verification follows; no real text-mode run has yet occurred.

Full SelfContained `isolation-8ea4d1d9786942608f7a1c16de460362`:
4844selected,4843passed,1failed,0skipped. All31correction cases passed. The sole
failure is the existing exact generated-report parser case; no assertion was
weakened and full-suite GREEN is not claimed. Actual controlled SDK wire shows
initial/tool leaves JSON-object, correction text only when opted in, unchanged
tool declarations/none/history/cap, and strict malformed-output refusal.
Generator/test-copy Agents SHA256 both
`10187D5D3CB63937B1B799D4E5360863A1D450D9344E858467F0AA5D441D60BE`;
Generator SHA256 `D1456D490A3D00B97F01478603DED47866CAE67B82BFEF6039EA120E819020C0`.
