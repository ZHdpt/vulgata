---
title: October tool-state admission and integrated qualification
date: 2026-09-29
status: approved-and-adopted
scope: october-stories-18.8-and-18.12-only
source_commit: f7a80cad6ac5b73a2a02ae015e4347590b6200ff
review_mode: batch
approved_on: 2026-09-29
adopted_on: 2026-09-29
implementation_authorized: false
---

# Narrow correction for Stories 18.8 and 18.12

## 1. Approved decision and issue

The user approved two downstream acceptance-criterion additions: Story 18.8 consumes the
accepted distinction between prepared source and application-owned tool state;
Story 18.12 qualifies their coexistence through the integrated source tools.
No new story, source-copying service, schema, UI or lifecycle transition is proposed.

Approval source: the user's explicit **"Proposal approved"** message on September 29,
2026 in Vulgata Master (`019fcbe8-f5db-73b3-914c-b29f75688a22`), following the combined
proposal. This approves definition adoption, not story implementation or runtime use.

The user chose in-place CodeGraph preparation/native MCP in a dedicated managed
prepared workspace, with source files unchanged and ordinary Worker tools remaining
read-only. The trusted adapter may maintain explicitly authorized tool state.
The user reports that Worker 18.3 has begun reconciling this direction and has sent
the coordination instruction to Worker 18.2. Those active-story corrections remain
with their roots; this proposal neither approves their concrete plans nor alters them.

The integration issue is not just job selection. The completed source tools currently
traverse directories including `.codegraph`; directory metadata contributes to
continuation checks. Excluding cache files from scan jobs does not alone stop a
Worker's source search from entering them. Conversely, an observed directory change
may correctly return `ChangedInput`; immutable pagination is not an existing promise.

Evidence and authority, observed on September 29:

- [October proposal](sprint-change-proposal-2026-09-26-october-batch-release.md),
  "Prepared input", "Whole-root tools" and "Context-assisted coverage": prepared
  origin, no copying/hash prerequisite, bounded tools and explicit admission.
- [October definitions](october-release-epics.md), Stories 18.1/18.3/18.8/18.12:
  listing is not admission; CodeGraph and file tools coexist; admission and real
  integration already belong to October scope.
- [Traversal](../../../src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceTraversal.cs)
  lines 65-73 and 121-129, and
  [read context](../../../src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs)
  lines 150-173 and 792-812: directory observations and ordinary recursive traversal.
- [Accepted source-tool contract](../../../specs/016-contained-source-tools/contracts/prepared-source-tools.md),
  "Security and lifecycle invariants", item 4: live source, observed drift and stale
  continuation refusal; no immutable snapshot or unobserved-mutation guarantee.

These are static findings and live-chat status, not new runtime qualification.

## 2. Scope, ownership and artifact impact

At the checked integration commit, 18.1 is done and 18.8/18.12 are backlog; no
registered 18.8/18.12 runner was found in the recent management context. Live worker
status, not the integration tracker's stale backlog labels, shows 18.2 implementing
and 18.3 revising its in-place plan. Recheck ownership before adoption or dispatch.

| Area | Disposition |
| --- | --- |
| 18.1 completed capability | Preserve completion/specification/evidence. Any implementation extension needs a named current owner and its applicable gates; do not silently reopen or rewrite history. |
| 18.2 active shared lease | Reconcile any actual shared-interface impact through its existing owner; this proposal assigns it no new implementation. |
| 18.3 active CodeGraph work | Remains integration/problem owner for write footprint, source/tool-state coexistence and coordinating necessary shared corrections. Concrete repair ownership is still to be settled there. |
| 18.8 backlog | Consume the accepted source/tool-state distinction for admission and coverage accounting; do not implement a competing source reader or cache classifier. |
| 18.12 backlog | Verify the integrated behavior and route defects to the responsible producer; do not hide missing source-tool work inside late qualification. |
| 18.7; Epics 19/20 | Consumers benefit from uncontaminated source-backed knowledge; no definition change or new dependency is needed here. |
| PRD / architecture | FR-4.1/4.9/12.3, NFR-2.3 and AD-3/5/6/23 remain compatible. No generic requirement or immutable retained-source rule is changed. Active tool owners must reconcile the October trusted-write exception through their affected authority/gates. |
| UX / storage / deployment | No visual, schema, migration, host/Docker or secret-handling change. Existing truthful coverage and Chinese UI rules remain. |
| Tracker / old validations | No IDs, story count, dependencies or status change. Preserve September 27 validation as evidence of that definition revision, not automatic acceptance of these additions. |

The September 26 proposal remains the primary October scope authority. This approved
document amends only the two downstream definitions below; it does not grant
filesystem writes, settle an active worker's shared interface, or authorize dispatch.

## 3. Recommended approach

Use direct adjustment, not rollback or release reslicing. Admission/accounting and
integrated tool verification are existing obligations; make this particular risk
explicit before those two stories start. Keep the actual shared source-tool repair
upstream under coordinated ownership.

The planning change is small. The incremental consumer work is expected to be small
once the upstream contract exists; the total repair cost remains unknown until
18.2/18.3 report their impact. No calendar estimate or deadline guarantee is implied.
Rollback would discard useful completed work, and dropping required CodeGraph would
conflict with the selected release; neither is recommended. Add no new approval
layer, broad regression matrix, automatic retry engine or publication-quality gate.

## 4. Exact approved edits

Target: [october-release-epics.md](october-release-epics.md). All existing acceptance
criteria remain unless explicitly quoted as replaced. The two additions and scope
notes below have been applied there; before/after text preserves the approved delta.

### Edit A — Story 18.8: add after its first admission criterion

**Before:** The first criterion ends with:

> **And** unknown extensions, low confidence or index omission alone cannot remove primary coverage; unsupported exclusions stay admitted.

There was no explicit application-owned tool-state criterion in the prior definition.

**After:** Preserve that criterion and insert:

> **Given** the accepted upstream source/tool-state binding for the selected prepared Repository and batch,
> **When** Scan Control builds source work and coverage accounting,
> **Then** positively identified application-owned tool state creates no source-analysis jobs and contributes nothing to source-coverage counts or denominators; record its location, owning tool and exclusion basis separately through existing policy/diagnostic records,
> **And** a directory name such as `.codegraph` alone cannot establish ownership or justify hiding source. Unproven exclusions retain the existing conservative admission treatment; preparation ownership collisions are returned to the tool owner, not silently overwritten or reclassified.

Append this scope note beneath the new criterion:

> Consume the one accepted upstream distinction; do not introduce another reader filter, directory-copying service, repository-wide hash pass, index or persistence schema. A location-level exclusion record is sufficient; do not recursively enumerate cache contents merely to count excluded files. Missing upstream agreement is an explicit integration dependency, not permission to infer it from names or invent a consumer-local contract. This admission rule does not itself authorize tool-state writes or repair source-tool traversal/continuation.

**Reason:** Covers O-FR03/O-FR33 without turning cache activity into source coverage,
silently discarding unfamiliar source, or moving shared-tool implementation into 18.8.

### Edit B — Story 18.12: add after its first composition criterion

**Before:** The first criterion ends with:

> **And** missing prerequisites fail explicitly rather than invoking legacy pair routing, restoring CGC or substituting old wiki content.

There was no explicit integrated source/tool-state coexistence criterion in the prior definition.

**After:** Preserve that criterion and insert:

> **Given** the qualified upstream prepared-source/tool-state contract and the selected real CodeGraph adapter in an owned small prepared fixture,
> **When** native preparation/MCP use and representative tool-state activity are exercised together with production source listing, ranged reading, search and multi-page continuation,
> **Then** source results remain contained and accurately located; tool-state contents are not returned as ordinary source evidence, dispatched as source jobs or counted as analyzed source, and useful source access works under the declared lifecycle and limits,
> **And** observed drift, cancellation, authority loss and limits retain their existing truthful outcomes rather than becoming empty/completed reads or silent truncation. A permitted `ChangedInput` outcome is not itself a failure, but repeated inability to obtain useful source results cannot qualify the integration as successful.

Append this scope note beneath the new criterion:

> Reuse the existing small-tool and batch qualification work; this is not a new benchmark, additional provider run or exhaustive failure matrix. Include a bounded supported continuation/drift case, not a guarantee that every old cursor survives changes or that unobserved source mutation is detected. Do not suppress genuine source/authority failures to accommodate cache writes, introduce automatic retry/update scanning, or implement a second filter in the host. Route any missing shared capability to its upstream owner. Controlled small-fixture evidence remains distinct from the already-required real registration/login batch evidence.

**Reason:** Makes O-FR34/O-NFR12 integration evidence concrete while preserving
O-FR04/O-FR05 and O-NFR01, existing live-source semantics and the small October scope.
No new explicit dependency is necessary: 18.12 already consumes 18.8, which consumes
18.7 and therefore the real source/tool adapters.

## 5. Adoption and delivery handoff

1. The user reviewed the edits together and approved the proposal. No story is activated.
2. The planning/integration owner applied only the two additions and their scope notes
   to the October definitions, with approval/authority metadata and a link to this
   amendment. Existing IDs/statuses, earlier validation history and the original
   release proposal are preserved.
3. Before implementing affected consumer behavior, 18.3 and the shared-capability
   owner supply the accepted shared contract. Controlled consumer development may
   proceed with labelled adapters under that agreement; it need not wait for finished
   upstream implementation. Before claiming integrated acceptance, adopt and verify
   the qualified upstream checkpoint. Do not invent DTOs or treat this consumer
   amendment as an accepted upstream interface. Unaffected parallel work may continue.
4. Future 18.8 owns admission/accounting; future 18.12 owns integrated qualification.
   Story roots retain independent review, planned-change/implementation approvals,
   evidence and resource permissions. No new approval delegation is created here.

Change classification: minor downstream definition additions, with moderate active
cross-story coordination already owned outside this proposal. No code, tests, workflow
state, commits, worker messages, processes, databases or provider calls were changed
or run to create or adopt it. The handoff is recorded here for the future story roots;
no external notification or upstream checkpoint acceptance is claimed.

### Correct-course checklist disposition

- [x] 1.1-1.3: Trigger, distinction and static evidence recorded.
- [x] 2.1-2.5: Epic remains viable; two existing consumer definitions change, with no resequencing/new epic.
- [x] 3.1-3.4: PRD, architecture, UX and secondary impacts assessed; active upstream correction remains separate.
- [x] 4.1-4.4: Direct adjustment selected; rollback and scope reduction rejected with reasons.
- [x] 5.1-5.5: Issue, exact edits, scope, risks and handoff recorded.
- [x] 6.1-6.2: Proposal checked for scope/ownership, name-only exclusion and false pagination guarantees.
- [x] 6.3: Explicit user adoption approval recorded and the two definition updates applied.
- [N/A] 6.4: No tracker ID/status/structure change required.
- [x] 6.5: Consumer handoff recorded above. Upstream implementation-owner/checkpoint confirmation remains a delivery dependency, not an uncompleted planning action; no external message sent.
