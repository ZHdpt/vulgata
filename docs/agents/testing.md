# Testing and verification

Read this before test authoring, runtime qualification or interpreting a failure.
The current story workflow owns exact TDD/review/verification gates; these notes
preserve stack-specific pitfalls and evidence boundaries.

## Harness

- Tests live in `tests/Vulgata.Tests`, mirroring the .NET source structure. Use
  `{Method}_{Scenario}_{ExpectedResult}` names and the existing xunit patterns.
- Web integration uses `WebApplicationFactory<Program>` and the host's
  `public partial class Program`, with SQLite substitutions for both DbContexts
  where applicable. Those substitutions are not PostgreSQL equivalence proof.
- Post Blazor SSR forms with **all rendered hidden inputs**, not only antiforgery.
  Select by `action`/`formname`; a logout form may precede the target form.
- Assert AccessDenied redirects by path prefix where query details vary. Verify API
  401/403 behavior separately from browser redirect behavior.
- Reflection/component harnesses must supply every injected dependency, even if the
  constructor is parameterless or an earlier test needed fewer injections.
- SQLite cannot translate every PostgreSQL query, notably some `DateTimeOffset`
  ordering. Keep selection bounded and use a justified provider-compatible/client
  projection in the harness; do not weaken production semantics to make SQLite pass.
- Keep status-code-page re-execution off `/api/`; middleware can otherwise change a
  correct API response into an HTML rewrite or another status.
- Verify Chinese Identity validation messages through the actual Identity/form
  path; do not assume `ChineseIdentityErrorDescriber` translates every framework
  validation attribute automatically.

## Evidence and safety

- Behavioral changes need honest RED then minimal GREEN and relevant regression
  evidence. Documentation/metadata changes use proportionate checks, not fabricated
  RED or unnecessary claims of application qualification.
- Under governed-delivery 0.7, retain the separate test and production owners per
  coherent task group. Both assess the final affected code before group completion;
  necessary refactoring stays GREEN and within their disjoint write boundaries.
  Minimal GREEN is a scope constraint, not permission to leave concrete design
  defects. Production cannot weaken tests; disputed test behavior returns through
  root to the test owner with evidence. See the installed
  [TDD skill](../../.agents/skills/governed-tdd/SKILL.md).
- A no-build result can use a stale binary. Confirm the tested assembly/build
  boundary. Concurrent `dotnet` processes can lock shared outputs; serialize owned
  test work or use proven isolated outputs rather than killing unrelated processes.
- Establish explicitly owned disposable databases, containers, processes and fixture
  directories. Startup applies migrations/seeding. Never infer authority to drop a
  shared database, stop all `dotnet` processes or copy real users' hashes/secrets from
  a legacy helper recipe. Use supported fixture or application creation paths.
- Separate host/configuration/network readiness from product behavior. Report exact
  failing URLs and sanitized error classifications when applicable. Use bounded
  source-free readiness checks under task authority; do not repeatedly retry an
  unchanged expensive provider/Scan operation.
- Record the actual source/revision/configuration and whether evidence is static,
  controlled/mock, local integration or real tool/provider/UI behavior. Native MCP
  requires native MCP proof; provider SSE ingestion alone is not browser streaming.
- Old passing tests and fixed-pair fixtures are reusable evidence for their accepted
  boundary, not proof of generic one/several/standalone behavior. Test-only data must
  remain test composition, not production fallback.
- Failed/incomplete drafts are not accepted Knowledge. Valid JSON or exact citation
  identity alone does not establish explanation usefulness/faithfulness. Preserve
  current feature-specific real qualification requirements and honest gaps.
- `git diff --check` may print normal LF/CRLF conversion warnings. Distinguish those
  warnings from an actual whitespace failure; do not declare a line-ending blocker
  without a failing relevant check.

Use [development guidance](development.md) for commands and the current
[workflow state machine](../../.agents/skills/run-governed-delivery/references/state-machine.md) for required ownership,
correction and verification gates. Recheck a pitfall against source before removing
it; lack of a recent failure is not evidence its cause disappeared.
