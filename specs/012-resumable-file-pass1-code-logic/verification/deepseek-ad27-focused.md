# DeepSeek AD-27 Focused Verification

Observed at `2026-08-25T12:15:28.5544922+08:00` against source checkpoint
`29459174348511bb813c4aa9672ccffee61d82a6`.

## Result

- Exact T145/T146 ordinary-two/terminal-submit-only cases: 7/7 passed.
- Complete `PreparedPass1JobExecutorStreamingTests`: 107/107 passed.
- Nonlive DeepSeek streaming, contract, and isolated-source classes: 151 passed,
  0 failed, 3 expected live skips.
- Build errors: 0. Existing NU1507 and MSB3277 warnings remain unchanged.
- No migration or model-snapshot delta was present.
- `git diff --check` passed with line-ending advisories only.

## Proven boundary

The local FICC still owns the same two strict functions. Provider requests one
through nine use the byte-identical ordered two-function projected array.
Provider request ten preserves the exact named submit choice and uses the exact
one-element strict submit declaration derived from the already-projected submit
object. Complete-turn preflight, transcript continuity, terminal diagnostics,
ten turns, no request eleven, streaming/deadline/body limits, zero SDK retries,
and sibling-client isolation remain green.

No live provider, source, database, network, host, or system operation occurred.
The next allowed operation is T148's source-free, durable-state-free live gate.

Before that live gate, the qualification-only serializer was corrected to label
the artifact as T148 and include the helper-owned terminal declaration hash and
byte count. The focused 7/7 and nonlive 151-pass/3-expected-skip barriers were
rerun successfully at `2026-08-25T12:17:11.1067527+08:00`.
