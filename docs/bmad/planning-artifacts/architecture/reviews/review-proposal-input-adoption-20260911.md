# Proposal-input reconciliation review — 2026-09-11

Verdict: **One bounded preservation correction required.** The architecture-owned adoption otherwise honors the approved proposal. This is an independent input-reconciliation check before the architecture adoption reviewer floor, not that floor's acceptance or coordinator finalization.

## Scope and evidence

Read the complete approved [planning correction](../../sprint-change-proposal-2026-09-11-generic-product-reconciliation.md), [architecture gateway](../../architecture.md), and [AD-1–AD-32 spine](../ARCHITECTURE-SPINE.md). Compared the external source AD blocks, the five current historical companions against their archived bodies, and relevant platform/security sections of the archived gateway. The supplied current AGENTS instructions establish the existing Identity-placement exception; production source was not independently inspected.

Reviewed gateway SHA-256: `1C6A28B8595C3F26CFAFC61935B6262B6DB71082FCF797FBB530D0112C11D770`.

Reviewed spine SHA-256: `13504EB83C7822B996E278113DFA31D5A70D07C2407035ADDBA3645BCDEB5A8B`.

The review method was a focused adversarial input-preservation pass under `bmad-review`, bounded by the assigned proposal checks rather than a new design or general editorial review. Its customization resolver could not initialize the configured uv cache; the documented shipped-default fallback was used. Only source-supported findings are reported.

## Finding

### Preserve the app-container non-root security baseline

- **Lens:** adversarial, input preservation.
- **Location:** current gateway, "Platform and project boundaries" / "Security and user experience"; [archived architecture](../../../history/pre-generic-adoption-2026-09-11/files/docs/bmad/planning-artifacts/architecture.md), "Docker Strategy", line 271.
- **Trigger condition:** The archived Docker strategy explicitly requires the app to run as a non-root user (`USER app`). The new gateway replaces that document as current platform/security authority but does not carry this requirement, and the preserved spine does not supply it. The approved proposal retains platform/security conventions and does not explicitly retire non-root execution. The rule is therefore available only in material the new gateway correctly forbids treating as current fallback authority.
- **Guard snippet:** Restore the baseline in the gateway deployment paragraph: "Run the application and bundled tool processes as a non-root container user with only the required writable data/workspace mounts." This is preservation of the archived security rule, not a new AD or Docker implementation request; keep all accepted AD blocks unchanged. The coordinator has agreed to apply this narrow repair before the reviewer floor.
- **Potential consequence:** Future Docker/tool implementation could treat root execution as unconstrained, silently weakening a previously explicit security baseline during an adoption approved to preserve it.

## Checks without findings

| Approved architecture-owned obligation | Observed evidence |
| --- | --- |
| One gateway and one stable decision spine | Gateway assigns authority by subject and points generic workflow rules to one spine; it explicitly rejects competing fallback authority or reconstructed redundant ADRs. |
| AD-1–AD-32 substance preserved | External source hash freshly matched `CC7E5AC92810734549607F38399720A027E3E237320ACEC30926FBF24D6FE093`. Both sources contain 32 AD blocks. All complete AD blocks match after line-ending normalization; zero changed blocks. |
| Historical companions preserved and scoped | Each of the five archived bodies remains an exact contiguous text substring of its current companion. Four files contain only a prepended historical notice plus the original body; the decision log contains the original body plus one appended dated disposition. |
| Recoverable architecture pre-images | All six architecture-related snapshot hashes match the [pre-adoption manifest](../../../history/pre-generic-adoption-2026-09-11/manifest.json). The former gateway is preserved, not rewritten as if it had always described the generic target. |
| Platform and current Identity exception | Gateway retains six-project .NET 10/Blazor/PostgreSQL 17 boundaries, Core/Infrastructure separation, two contexts/schemas/migration histories, and explicitly states that existing Identity context/migrations remain in Web/Data without a relocation requirement. |
| Preserved security and product foundations | Gateway retains scoped roles, live server-enforced access/mode checks, encrypted secrets, read-only/non-production database restrictions, self-hosted data containment, validation, prompt separation, sanitized diagnostics and the literal append-only `log.md` obligation. The non-root omission above is the sole identified preservation gap. |
| Target is not runtime success | Both documents remain adoption drafts pending review/finalization; generic implementation, Docker/restore readiness, provider qualification and prior green evidence are explicitly distinguished. |
| No service or upgrade expansion | Two Compose services are explicit; bundled tools are not a new service. Package seeds are not upgrades. No host prerequisite, remote tool bridge, local LLM service or automatic failover is inferred. No-import/no-restore is not misrepresented as a network air gap. |
| Historical/backlog meaning preserved | Gateway preserves completed portions and incomplete Stories 11.5/12.5, supersedes old Epics 5–10 without marking them done, and retains Prompt Workbench, dedicated unknown-library inference and general MCP configuration/consumption as original-capability backlog. |
| User deferrals preserved | Gateway and spine retain automatic/incremental source-update scanning, external operational-information integration, outward wiki MCP, attachments, product HITL, Worker-triggered partial library scans, multiple/dependency-aware LSP analysis and advanced/live graph deferrals. Ordinary standalone scans, visible Uncertainties, internal wiki/native CodeGraph tools, basic manual-refresh graph, real Scan progress, genuine Chat streaming, operational controls and development approval gates survive. |

## Limits and handoff

PRD, glossary, UX, epic/sprint and selector completion belong to concurrent adoption lanes and were not assessed as missing architecture inputs. This check does not accept their final cross-artifact consistency or full requirement/story coverage. It does not certify every old prose convention as an independently retained normative clause.

No Git, production-code, build, test, host/runtime, Docker, database or provider operation was performed. Only this report was written. Restore the one explicit security baseline in the gateway, then proceed with the independent adoption reviewer floor; later coordinator finalization and governed feature activation remain separate.
