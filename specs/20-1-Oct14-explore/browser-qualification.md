# Explorer readiness and retained-publication browser checks

Updated 2026-10-11. Management/coordination belongs to the user; the former integration chat is advice-only. No chats were dispatched for this resumption. This document prepares verification; it does not authorize a host, database, source access, Generate or provider operation.

## Source baseline and readiness

Assigned checkout: `C:\Users\13728\.codex\worktrees\bd91\vulgata`, branch `oct14/explore`. Clean starting checkpoint `1ad31920498fd8aaba925de7bf77dc362de91d06`; local integration `d623c509de19824a444babe9d55085e2220128c0` merged without reset at `a2da17fa96f00bf9c3770f5a702a87b462f7cce9`. Planning baseline `fad7f11` remains an ancestor. Integration source is not evidence that Scan's latest dataset exists in this checkout or is published/qualified.

| Surface | Current implementation and limits |
| --- | --- |
| Reader | Ordinary `[Authorize]` routes `/knowledge/documents` and `/knowledge/documents/read`; no management role required. Exact batch/publication/logical/version and optional string section; invalid or unavailable targets never fall back to latest. One owner content chunk per explicit action, not a whole-document client cache. Section deep links focus their heading. Chinese Business/light and IT/dark states. |
| Graph | Ordinary `/knowledge/graph`, one seed page plus bounded one-hop accepted related pages; at most 40 displayed Documents, 80 edges, eight protocol calls, ten-second deadline. Owner page envelopes/capsules and final aggregate reauthorization required. No Chat dialogue-store dependency. |
| Interaction | Deterministic grid, pan/zoom/fit, manual refresh, list and canvas node selection, selected-node qualifications and exact-version links. Selection/layout cannot change domain relationships or acquire more data. Full labels remain in the list/detail even when canvas labels are shortened. |
| Filtering | Trusted mode/repository/publication selection is applied server-side. No interactive type/repository/text filters or alternate layout algorithm are currently exposed. Advanced filters and interactive expansion are explicitly deferred by the accepted feature spec. |
| References | `ExactDocumentLinkBuilder` is the shared pure exact-route encoder. Chat owns reference production/display authorization. Reader accepts exact section handles without guessing from prose. Evidence-only version references are not turned into invented publication/section destinations. |

The local gap correction adds a selected-node detail panel and canvas mouse/keyboard selection. It also marks the graph incomplete when an accepted relationship is omitted because an endpoint is outside the displayed set. No relationships, source data, contracts, policy, migrations, shared Program/navigation or production fixture fallbacks are added.

## Required owner handoff and activation inputs

Before browser work, the user must provide/authorize one shared retained target and its host lifetime. Do not reuse disposed qualification resources or regenerate content for Explorer screenshots.

- Confirmed publication with exact `CanonicalDatabaseIdentity`, `TargetId`, `BatchId`, `SelectionDigest`, `PublicationId`; actual exact CL/BL Document/version handles and any accepted section IDs. A Generate success message alone is insufficient.
- Actual accepted relationship versions, From/To handles, `REFERENCES` / `GENERATED_FROM` kinds and meanings; permitted Uncertainties and coverage. Empty relationships are valid. Two Documents or a BL dependency do not imply a displayable edge.
- Approved frozen `ConnectionStrings:DefaultConnection` for that target; existing Identity account with ordinary User role and live contributor access. Credentials stay out of reports/screenshots. Startup migration/seed effects need separate runtime authority.
- `October:Wiki:Enabled=true`; all five publication/binding fields above; exact `Mode=IT` or `Business`; explicit `RepositoryIds` section; absolute existing authorized `LogDirectory`. Missing/invalid selection fails closed. Host mode is trusted configuration, never a query-string elevation.
- Existing `AddOctoberWikiReader` / `AddExploreReader` composition; `IExploreContextProvider`, `IOctoberKnowledgeReader`, live Identity/disclosure policy and token protector. Graph additionally needs `IOctoberKnowledgeDisclosedReader`, `IOctoberKnowledgeDependencyAuthority`, disclosure protector and the existing output profile. Business owns those contracts and persistence.

No shared composition change was found necessary in this source baseline. If the retained host lacks these existing registrations or its exact selection, report that precise host/configuration gap to the user; do not edit shared composition unassigned.

## Compact browser and screenshot sequence (all pending)

Use one ordinary authenticated user and the exact supplied publication. Record URL, mode, publication/version IDs and expected outcome in a private verification note, not credentials. Screenshots use genuine returned titles/content only; sparse or unavailable data must stay visibly honest.

| Check / screenshot | With CL only | Additional prerequisite |
| --- | --- | --- |
| IT Document list, exact CL open, Chinese dark theme, full title/content/qualifications | Yes | Confirmed CL and IT access |
| Exact section URL, heading focus, next chunk then terminal state; back/forward preserves version | Yes, if actual section/chunks exist | Owner-supplied actual section IDs; no fabricated long content |
| Wrong batch/publication/version/section unavailable; query `mode=IT` cannot elevate Business | Yes | Safe nonexistent handles; trusted Business host/session |
| Business list honest empty and CL exact-route denial | Yes | CL-only publication and Business mode |
| Graph overview, full labels/list, click and keyboard selection/detail, exact node link; pan/zoom/fit/manual refresh | Yes, potentially isolated nodes | Graph-purpose authorization; actual accepted edges only |
| Selected-node screenshot plus supporting Document screenshot at the exact version | CL node can open itself | Supporting link requires an actual accepted relationship with both visible endpoints |
| Business/light BL read and graph detail | No | Confirmed BL plus Business disclosure; never show CL as BL |
| Direction/type/meaning and Uncertainty screenshot for a registration/login flow spanning repositories | No claim from CL success alone | Actual accepted cross-repository edges/endpoints and owner-permitted Uncertainties; retain partial coverage |
| Click a real Chat reference to exact Document/section | Reader destination can be checked independently | Actual authorized Chat reference; Chat turn execution/browser flow is separately owned/authorized |
| Revocation clears prior content/graph, empty/partial/unavailable recovery | Controlled evidence available now | Live mutation/revocation and host changes require separate approval; do not alter retained accounts/data under this plan |

For graph overview/detail, verify edge count/IDs match the disclosed graph, arrow direction is From → To, both relationship kinds keep their meaning, and the displayed uncertainty/coverage limitations remain visible. Zero edges must not be filled from prose, matching titles or apparent similarity. Viewport changes must not change semantic counts. Capture a genuine partial/empty state if supplied; do not create production data to obtain one.

Browser rendering, Fluent custom-element keyboard behavior, responsive layout, focus/scroll and real reference navigation remain pending an authorized host. Controlled component tests are not screenshots or a live-browser pass. No new runtime operation is implied by previous disposable CL/BL qualification.
