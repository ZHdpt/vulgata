# OCT-CHAT validation guide (planned, not executed)

Run only after implementation and scoped resource authorization. This document is a verification plan, not proof that commands/tests already exist or pass. Source artifacts and common fixture identities must be pinned when K0 arrives.

## Preconditions

1. Work in this assigned worktree on the reviewed implementation commit. Keep output/test resources separate from other lanes.
2. Integrate the Business-owned compiled K0 contracts/common fixture. Before any real provider or production user-facing C1 dispatch, also integrate Business's durable dialogue begin/fence/terminal-save/lookup/orphan implementation and schema, and pass focused PostgreSQL admission/save checks. Earlier C1 work is test-only controlled mechanics. Obtain Explore's exact reader route for C2 and integration-owner Web/profile/shared-admission composition. Do not create fallback contracts or write October migrations from Chat.
3. Use existing accounts with an ordinary permitted user, Business/IT cases and denied/revoked contributor. Setup operator credentials are not ordinary reader access.
4. Confirm explicit database/batch/publication and schema identity. The integration owner controls the schema application window; ordinary Web does not execute October migrations. Starting Web still applies existing Identity/domain migrations/seeding, so it requires an explicitly owned target.
5. For real execution, declare source consent, selected ChatCompletions endpoint/model and host configuration source, protected credentials, request/rate admission, time/byte limits and owned test database/ports. Do not print secrets or use production/shared databases. No global PATH or settings changes.

## Controlled verification

After classifying new tests/helpers in the existing suite inventory, use the repository launcher. Proposed tests should use namespace `Vulgata.Tests.October.Chat` so the following focused filter is concrete; before execution confirm discovery is nonzero.

```powershell
pwsh -NoProfile -File eng/test.ps1 -PolicyOnly
pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat'
```

Expected observations:

- Selected fixture is visibly controlled; wrong database/batch/publication is unavailable with no legacy substitution. Ordinary authorized users need no operator/management role.
- Tool transport sends native fragmented call arguments, invokes a real registered controlled function, continues the model round and yields answer text while provider completion remains deliberately blocked. Include text before a later tool finishes. UI observation must not be inferred from transport receipt alone.
- Malformed UTF-8/framing, incomplete tool JSON, conflicting IDs, invalid function, truncation and limits terminate honestly. Cancellation releases owned work and provider permits.
- Business never receives CL/title/count/path/history/footer metadata. Revoke a contributor used only in non-footer input; subsequent text/history is withheld. Authorization outage fails closed.
- Malicious retrieved instructions cannot expand scope, tools or provider settings. Raw reasoning, arguments/results, credentials and unsafe rendered links/HTML remain absent from UI/diagnostics.
- Current-turn exact reads qualify references; discovery-only, earlier-turn, invented and foreign-section handles fail. Empty report is valid. Footer readiness does not delay text.
- F3: use Business-owned fixture Documents with (a) multiple sections totaling >64 KiB and (b) one section >64 KiB. Invoke ReadWiki with the returned opaque continuation until complete within budgets. Assert fixed publication/version/section binding, complete UTF-8 across multibyte boundaries, preserved claim qualifiers and exact returned coverage. Repeat with revoked access, expired/tampered tokens and mismatched target; expect owner-safe failure without fallback. Exhaust aggregate/time/tool budgets after a prefix: report incomplete, do not credit unread suffix/sections or duplicate coverage on replay. Failed/cancelled pages add no ledger coverage.
- F4: reuse Business's lock-acquisition versus supplied business-motive fixture, including a Mixed group. Observe source/advisory attribution beside the first affected streamed prose and again in a saved-history follow-up; motive/duration must not become source proof. Exercise bounded history and chunk boundaries near the qualifier, preserving it or omitting the affected group/turn honestly. Business presentation must keep safe attribution without technical CL identifiers. No sentence-by-sentence citations, whole-answer verifier, buffering or publication gate may be added to pass this check.
- Provider admission: verify agreed policy with separate process-local Web/generator permits; Chat uses Web's pool and generator Business finalization shares Scan's pool. Do not claim a global cap or create a cross-process coordinator.
- Duplicate submit, same-key conflict, one-active-turn contention and late events never start another generation or attach text to a different conversation.
- Useful completed history supports a follow-up. Interrupted output and old IT context are excluded from new Business inference. Context budget omits whole old turns with an honest indication, not a fabricated summary.

## Persistence qualification

Use only runner-created PostgreSQL resources and guarded tests:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat'
```

Require atomic text/reference/dependency/status save, ordered history, cross-request active fence, identical replay and conflicting-payload rejection. Drop a commit acknowledgement after the database commits: outcome lookup confirms the single saved result with no provider call. An unavailable lookup leaves save-indeterminate and retry blocked. Confirm host-orphan fencing cannot steal live work; restart never dispatches old turns. Useful terminal partial text is retained; unsaved process-crash tokens are not promised.

## Real provider and browser qualification

External/E2E commands run only after explicit execution/source/provider authorization and actual classified test names exist. The filter must discover enabled tests; all-skipped/zero-selected is non-success.

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite External -WithPostgreSql -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat'
pwsh -NoProfile -File eng/test.ps1 -Suite E2E -WithPostgreSql -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat'
```

The runner supplies owned databases, not the frozen shared delivery dataset. Seed only through supported owner interfaces or explicitly authorized local seed inputs. Manual UI observation against the final shared dataset belongs to the integration owner's separately authorized run window.

For the real selected source-backed publication, record:

| Scenario | Required observable result |
| --- | --- |
| Cross-Repository registration/login explanation | Actual native tool round and at least two actual Document reads; narrative matches independently inspected source-backed Documents, including branches/effects and relationship meaning. |
| Narrative follow-up | Uses useful completed history plus a further wiki read; references belong to this turn's exact ledger. |
| Unknown | States a genuine source/Knowledge gap without inventing a business relationship or a footer target. |
| Live delivery | Browser first-visible text timestamp precedes final provider completion; include interleaved activity. Record total duration/limits, not an invented universal latency target. |
| Exact navigation | Footer opens Explore's exact version/section in the same publication; compare with reader/graph selection. No latest-version redirect. |
| Interruption / error | Useful partial text has interrupted/failed status, no automatic generation replay, no completed-history pollution; controlled injected failures stay labelled. |
| Access | Ordinary permitted success; Business BL-only; current revoked/denied contributor cannot leak tool/history/footer metadata. |

Separate retrieval completeness, generated-document quality, answer usefulness and reference faithfulness. Exact IDs or valid schema alone are not semantic evidence. Preserve sanitized timings/correlation IDs, result status, declared source coverage and limitations; never store credentials, hidden reasoning or raw tool payloads in evidence.

## Integration handoff and completion

Provide the integration owner the feature commit, K0/extension pins, registration entry point, required configuration source, test results and remaining limits. The integration owner performs the final combined Web build and complete SelfContained run after final fixes, plus affected runtime checks. Do not repeat broad suites absent changed behavior or an unresolved concern. No original 19.x/full PRD closure is implied.

This planning request stops before all commands above except document/Git validation. No model calls, migrations, application startup or runtime tests were executed to author it.
