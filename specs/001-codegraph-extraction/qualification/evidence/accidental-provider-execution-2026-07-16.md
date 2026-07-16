# Accidental provider execution incident

Date: 2026-07-16.

During Group A TDD, the intended GREEN command incorrectly included
`--no-build`. The class-level test filter therefore ran a stale compiled
`CgcCommandQualificationTests` binary instead of the newly edited synthetic
contract test. That stale test invoked the inherited Docker-based
CodeGraphContext qualification once.

## Bounded evidence

- Started at `2026-07-16T09:07:27.0214186Z`.
- Test duration: approximately 11 seconds.
- Test result: failed.
- Observed provider output bytes: `2012`; overflow: `false`.
- Stdout SHA-256:
  `691c85555c203b959d33cbe500a1d2c0fe41226172ada4de8ab1d3373b688227`.
- Stderr SHA-256:
  `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
- Failure: the inherited CLI-marker structural gate did not find one valid
  ordered marker pair.
- Cleanup: the stale test observed its cleanup-success marker. The disposable
  `--rm` container is absent from `docker ps -a`; no surviving provider process
  or named qualification container was observed.

No raw provider output, source path, symbol, query payload, Context name,
container name, credential, or token is retained here.

## Fail-closed disposition

This execution does not qualify the accepted HTTP JSON candidate: it used the
superseded inherited CLI-marker observer, ran before prerequisite groups were
green, and did not produce schema-1.2 evidence. It nevertheless consumed the
task graph's sole permitted provider execution. No retry is authorized.

Result: **FAIL -> corrective planning** with category
`execution-order-violation`. The unfinished Group A edits remain unaccepted
and uncommitted.
