# Approved-scope input reconciliation — 2026-09-11

## Verdict

**One bounded preservation correction is needed before finalization.** The new PRD preserves all 104 original normative IDs and the approved generic product direction. No settled design choice needs reopening. Restore the explicit global/provider and per-System/role-assignment scopes in FR-3.1 and FR-3.3; no other material semantic omission was found in this input check.

This is the pre-rubric input-reconciliation result, not a full PRD quality review, implementation acceptance, or proof of runtime capability.

## Pinned inputs and evidence limits

- Current canonical PRD: `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md`, SHA256 `305656DBC7D8DFE8F276CFC336F60730ED4122E39D36E390D3DBA8ED1009D1FA`, 249 lines.
- Approved proposal: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-11-generic-product-reconciliation.md`. Its approval record identifies the approved pre-image and the documentation/backlog-only authority.
- Complete disposition input: `C:/Users/13728/Documents/VulgataSepAudtion/formal-reconciliation-2026-09-11/prd-reconciliation.md`, including all 104 individual mapping rows, temporal authority, original-backlog safeguards, and the non-Scan/UI omission guard.
- Exact original requirements: `docs/bmad/history/pre-generic-adoption-2026-09-11/files/docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md`; all original FR/NFR clauses were compared, with relevant journeys, success measures, assumptions and exclusions checked.

Only this report was written. No PRD, historical evidence, Git state, source, tests, build, runtime, provider, database or delivery-state operation was changed or run. No implementation assertions were independently evaluated. Line references below refer to the pinned files, not later edits.

## Finding AS-01 — Preserve the two provider-configuration scopes explicitly

**Severity: medium; required localized correction.**

- Original FR-3.1, archived PRD line 229, places provider configuration at the **global level**. Original FR-3.3, line 231, permits a specific provider for each task role **within a System**.
- The complete approved-scope input explicitly preserves global provider configuration (line 80) and per-System role assignment (line 82). The approved proposal also preserves provider configuration/overrides (line 138).
- Current FR-3.1, line 69, lists administrator configuration and default task assignments but omits its global scope. Current FR-3.3, line 71, allows System Owners to assign providers to permitted roles but omits the per-System dimension.
- Consequently, the revised wording no longer clearly requires different Systems to retain independent role/provider selections over the global inventory/defaults. Merely allowing different providers for different roles is not equivalent: a single platform-wide role mapping could satisfy that narrower wording while losing the original per-System override capability. “Permitted” constrains authorization, not configuration cardinality.

**Minimal restoration:** say that Administrators configure the global provider inventory/default task assignments in FR-3.1, and that System Owners assign providers to the overview/Worker/Chat roles **per authorized System** in FR-3.3. Preserve different providers by role and System. Do not add new provider failover, adapter qualification, or Repository-level override behavior.

Exact localized wording, retaining the remaining sentences in both current clauses:

- FR-3.1 opening: “Administrators shall configure the global provider inventory, including provider names, endpoints, protected keys, supported API types and default task assignments.”
- FR-3.3 opening: “System Owners shall assign providers to permitted overview, Worker Agent and Chat Agent roles per authorized System. Roles and Systems may use different providers; deterministic Scan Control needs no model assignment.”

This is restoration of approved retained scope, not a new product choice or a reason to reopen the generic Scan design.

## Identity preservation check

The check extracted normative clause headings using `^- \*\*((?:NFR|FR)-\d+\.\d+):\*\*` from both snapshots, then compared the sets and duplicate counts.

| Check | Result |
| --- | --- |
| Original normative clauses | 104 total / 104 unique: 89 FR + 15 NFR |
| New normative clauses | 104 total / 104 unique: 89 FR + 15 NFR |
| Missing, added or duplicate normative IDs | None |
| ID-only proof used as semantic acceptance | No; the original clauses and disposition input were compared for preserved obligations and authorized changes |

## Semantic preservation evidence

| Requirement/input concern | New PRD evidence and result |
| --- | --- |
| Identity, roles and basic administration | FR-1.1–1.4 retain registration, login/logout, profile/password changes, administrator-assigned roles and authorized management/consumption. FR-2.1–2.5 retain System management, ordinary/standalone Repositories, context, removal, and best-effort reachability with nonblocking authentication failure. Provider field/API vocabulary, multiple providers and explicit tests survive FR-3; only AS-01's scope qualifiers need restoration. |
| Database and advisory-context administration | FR-10.1 preserves per-Repository protected connection configuration independently of deferred Worker schema/sample access. FR-14.1–14.4 preserve Global/System/Repository context, including standalone Repositories, without expanding management authority or mutating active Scan inputs. |
| Chinese UI and established experience | Section 2, line 48, explicitly retains Simplified Chinese-only V1 UI, localized errors, Library Oak, Business light/IT dark, chat-first login, a separate management surface and keyboard/basic accessibility. Knowledge reader access is not converted into management permission. |
| Literal activity-log output | FR-4.8, line 83, retains append-only `log.md`, Scans/queries/system events and the exact timestamped heading format. Sanitization constrains content; structured diagnostics cannot silently replace the output. Its Current label is explicitly an obligation, not a completion claim. |
| Prompt Workbench | FR-5.9, line 99, retains a distinct development-time prompt-iteration feature as original unmet backlog. Catalog coupling is removed as approved; native tools/externalized prompts receive no completion credit. Section 0 states that Epics 13–17 cannot establish full-V1 completion. |
| Dedicated source-unavailable library inference | FR-8.5, line 129, preserves the separate inference feature outside the core redesign. Only mandatory owner notification follows approved HITL deferral. General hypotheses do not complete the feature or become source proof. |
| General configurable MCP | FR-13.1/13.2/13.5, lines 167–171, retain original unmet general consumption/management, including Repository/System/global registration. Native CodeGraph and internal wiki tools do not complete it; original low priority is retained without inventing a deferral. FR-13.3 preserves purpose-specific approvals. Outward wiki MCP alone follows explicit deferral. |
| Authorized empty-selection auto-scope | FR-11.4 and FR-11.8, lines 150/154, retain the initial all-accessible-Systems default, adjustment/clearing and automatic relevance selection when empty. Relevance operates inside server-approved discovery and never grants authorization. |
| Session/mode behavior and useful consumer journey | FR-11.1–11.7 preserve mode-specific themes/answers, confirmed mode switch, saved/new session behavior, exact useful references and multi-document retrieval. The approved changes add stable turn reads, durable dialogue, genuine provisional streaming and reconnect without weakening Business BL-only or live authorization. Attachment deferral does not remove typed questions/dialogue. |
| Immutable history and mixed deferred clauses | FR-4.10/9.4 retain immutable Documents, Version Chains and exact historical provenance. FR-6.4 retains nonblocking Dangling Links and historical wontfix decisions while deferring new human actions. FR-7.3 retains human context's lower authority. FR-12.6 retains the reader/history/basic manual graph despite deferring advanced/live visualization. |
| Security and containment | NFR-2.1–2.4 retain modern password hashing, encrypted secrets, self-hosted source containment, necessary-only external-provider context and read-only/non-production database restrictions. Composite-contributor, source/tool, history, count and live-disclosure protections strengthen these obligations. Deferred tools do not discard safeguards. |
| Explicit deferrals without scope collapse | FR-6.7/9.1–9.3/9.5 defer incremental/source-update work, not repeated explicit static Scans or late-knowledge reconciliation. FR-8.2/8.3 defer caller-triggered partial library work, not FR-8.1 standalone scanning. FR-7/12.5 defer product HITL, not visible Uncertainties, operational recovery or development approval gates. FR-10.2/10.3 and FR-5.13 defer new external-information tools, not existing configuration. |
| Generic replacement versus inherited mechanisms | FR-4/6 explicitly replace preallocation, mandatory supersteps, fixed-pair/global barriers and deadlock-yielding with source-bound file work, semantic Code Units, bounded resolution and per-flow publication. Source-change-only regeneration is revised while immutable history remains. FR-4.11/4.12 are historical, not falsely completed by new inquiry/qualification. |
| Protocol/runtime/quality boundary | gRPC-specific guaranteed detection and shared-database communication remain excluded. Temporary no-import/build/restore LSP is not a permanent topology or air gap. Environment-qualified performance replaces expired universal timing without introducing a five-Document cap. Two-service Docker, retained source/keys, coherent restore and recovery are obligations, not claimed current readiness. |
| Success outcomes and exclusions | SM-1–SM-4 retain generic Cross-Repository understanding, readable Business Logic, correct roles/links and untrained Business usage. SM-5 is explicitly deferred. Scope exclusions retain SSO, new fine-grained RBAC, production database access, webhooks, formal WCAG commitment, compliance reporting, independent Audition Agent and dedicated answer-confidence scoring. |

## History and current-authority reconciliation

**Pass.** Section 0 distinguishes Current, Deferred, Original unmet backlog and History; specifically says Current does not mean implemented; and states that core Epics 13–17 do not exhaust V1. It identifies one canonical PRD, the adopted architecture and the exact archived original without granting the archive competing authority.

Section 7, especially lines 245–249, preserves these distinct facts:

- Completed Epics 1–4 and Epic 11 Stories 11.1–11.4 remain completed history; Story 11.5 remains incomplete.
- Accepted Epic 12 Stories 12.1–12.4 remain accepted; Story 12.5 remains terminated incomplete, not a completed release.
- August 17/18/29 changes had scoped effects; the August 31 integration did not itself authorize the new streaming design.
- September 1's compilation-only exception applied to decommission cleanup, not future implementation/testing authority.
- Competition deadlines, samples, quotas, mock topology, host exception and evidence ceremonies are historical, not current product acceptance or blanket fallback instructions.

The approved source/backlog distinctions are explicit, and this PRD does not claim that design adoption qualifies implementation. After AS-01's narrow restoration, this input check has no remaining material scope-preservation gap to hand to the rubric review.
