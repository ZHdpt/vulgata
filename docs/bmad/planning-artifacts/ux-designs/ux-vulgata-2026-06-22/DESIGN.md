---
project: vulgata
type: design
status: final
created: 2026-06-22
updated: 2026-06-22
colors:
  primary: '#445E7A'
  primary-foreground: '#FFFFFF'
  accent: '#B98B6B'
  accent-foreground: '#FFFFFF'
  background: '#F6F4F0'
  foreground: '#1D2026'
  muted: '#EDE9E3'
  muted-foreground: '#7A7A7A'
  border: '#D9D3CA'
  surface: '#FFFFFF'
  primary-dark: '#6A83A2'
  primary-foreground-dark: '#FFFFFF'
  accent-dark: '#D4A88C'
  accent-foreground-dark: '#1D2026'
  background-dark: '#1D2026'
  foreground-dark: '#F6F4F0'
  muted-dark: '#282B33'
  muted-foreground-dark: '#9A9DA5'
  border-dark: '#383B44'
  surface-dark: '#24272E'
typography:
  display:
    fontFamily: 'Noto Serif SC, serif'
    fontSize: 32px
    fontWeight: '400'
    lineHeight: '1.3'
  display-sm:
    fontFamily: 'Noto Serif SC, serif'
    fontSize: 22px
    fontWeight: '400'
    lineHeight: '1.35'
  body:
    fontFamily: 'inherit'
  caption:
    fontFamily: 'inherit'
rounded:
  sm: 4px
  md: 8px
  lg: 12px
spacing:
  scale: 'inherit'
components:
  button-primary:
    background: '{colors.primary}'
    foreground: '{colors.primary-foreground}'
    radius: '{rounded.md}'
  mode-badge:
    background: '{colors.primary}'
    foreground: '{colors.primary-foreground}'
    radius: '{rounded.sm}'
  system-tag:
    background: '{colors.accent}'
    foreground: '{colors.accent-foreground}'
    radius: '{rounded.sm}'
sources:
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/review-rubric.md
---

## Brand & Style

Vulgata is an LLM-powered cross-repo business logic extraction platform built for a large bank's internal use. The product premise is that *business logic is scattered across systems and no single person holds the full picture* — Vulgata reads source code across repositories, extracts the business rules embedded within, and makes them accessible through natural-language chat. The brand expression follows: a serif display moment that signals scholarly depth in an otherwise sober Fluent UI surface, a warm oak accent that means *this is contextual or selected*, and visual restraint everywhere else.

Vulgata inherits Microsoft Fluent UI Blazor defaults wholesale. This DESIGN.md specifies only the brand-layer deltas — primary color, accent color, display typography, dual-mode theme mapping, and a handful of brand-specific component overrides. The 90% of components that ship from Fluent UI (Button variants other than primary, Dialog, DataGrid, NavMenu, TextField, Toast, Tooltip, Breadcrumb, ProgressBar) inherit Fluent's visual specs as-is. Customizing those is *explicitly* against the brand discipline — Fluent's defaults are the contract.

The personality is **Warm Professional**. Vulgata feels like a trusted internal tool with warmth — professional enough for a bank, approachable enough for non-technical users. Not cold like a terminal, not playful like a consumer app. The Library Oak theme registers as scholarly, contemplative, wise — deep warm blue-grays with oak-toned accents. Reads like a reading room, not a terminal.

Vulgata is a single-language product: the UI is Chinese (Simplified). All labels, buttons, placeholders, empty states, and system messages are in Chinese. No i18n surface design exists for V1.

## Colors

The Vulgata palette is two brand colors plus Fluent UI-derived surface tokens. Vulgata runs in two modes — **业务模式** (Business, light theme) and **技术模式** (IT, dark theme) — with each mode locked to its respective theme. There is no mid-session theme toggle; switching modes starts a new chat session.

### Brand Colors

- **Primary (`#445E7A` light / `#6A83A2` dark)** — Warm blue-gray. Used on primary buttons, the active mode badge, selected nav items, link underlines, and the mode-selector banner. Replaces Fluent's default brand color.
- **Accent (`#B98B6B` light / `#D4A88C` dark)** — Oak tan. Used for system/repo selection tags, the active/in-focus system indicator in the management tree, and HITL notification dot. The accent marks *contextual selection* — which system or repo is currently active. Never used for chrome, never used decoratively, never used for status badges.

### Surface Tokens (Light / 业务模式)

- **Background (`#F6F4F0`)** — Warm paper. The page-level background. Slightly off-white with a warm undertone — avoids the clinical feel of pure `#FFFFFF`.
- **Foreground (`#1D2026`)** — Deep warm charcoal. Primary body text color.
- **Surface (`#FFFFFF`)** — Pure white. Card backgrounds, chat bubbles (user side), input fields. Sits above the warm paper background for contrast.
- **Muted (`#EDE9E3`)** — Slightly deeper than background. Used for chat bubbles (assistant side), sidebar backgrounds, and section dividers.
- **Muted-Foreground (`#7A7A7A`)** — Medium gray. Captions, timestamps, secondary metadata.
- **Border (`#D9D3CA`)** — Warm gray border. Input outlines, card dividers, tree-view connectors.

### Surface Tokens (Dark / 技术模式)

- **Background (`#1D2026`)** — Deep warm charcoal. The page-level background. Darker than neutral gray, with a subtle warmth that avoids the coldness of pure `#1A1A1A`.
- **Foreground (`#F6F4F0`)** — Warm paper-reversed. Primary body text on dark.
- **Surface (`#24272E`)** — Slightly elevated from background. Card backgrounds, chat bubbles (user side), input fields.
- **Muted (`#282B33`)** — Chat bubbles (assistant side), sidebar backgrounds.
- **Muted-Foreground (`#9A9DA5`)** — Captions, timestamps, secondary metadata.
- **Border (`#383B44`)** — Input outlines, card dividers, tree-view connectors.

### Color Discipline

- Two brand colors only — primary and accent. No third brand color.
- No gradient surfaces. Flat, solid backgrounds only.
- No custom destructive, warning, or success colors — inherit Fluent's semantic color tokens.
- The dual-mode theme mapping is structural: 业务模式 = light tokens, 技术模式 = dark tokens. No exceptions.
- The accent color changes meaning between modes — in light it signals warmth, in dark it signals a soft highlight — but the functional role (contextual selection) is identical.

## Typography

Body, label, caption, and UI text inherit Fluent UI Blazor's default type ramp (Segoe UI on Windows, system fallback). Only the `display` role is brand-overridden, set in **Noto Serif SC** (思源宋体) at 32px (22px small variant). The serif matches the scholarly Library Oak register and reads naturally in Chinese.

The serif moment appears in:

- The empty-chat greeting on first session: *"您好，请问有什么可以帮助您的？"*
- The new-chat prompt after mode switch
- Section headers in the management screen (e.g., *"系统概览"*, *"知识库管理"*)
- The mode-select banner when starting a new session
- Empty states throughout the application

Everything else stays in Fluent's default sans-serif. The serif is a punctuation mark, not a default voice. Never use Noto Serif SC for body text, buttons, labels, form fields, data tables, chat message content, or any interactive element.

Font weight is consistently `400` (regular) for display moments. No bold display text — the serif carries enough visual weight on its own.

## Layout & Spacing

Fluent UI's spacing scale is inherited as-is. No custom spacing tokens — Fluent's 4px-based grid is the law.

The application shell is a single-column layout. The chat surface occupies the full available width with a comfortable maximum content width. The management surface uses a sidebar + content-area split: system tree view on the left, detail panel on the right.

Key layout rules:

- **Chat-first**: The chat surface is the default view after login. No landing page, no dashboard — the user lands in the chat.
- **Mode selector**: Positioned above the chat input area, not in a toolbar or settings panel. Visible and accessible before every message.
- **System/repo selector**: Inline within the chat area — not a separate page, not a modal. Contextual to the conversation.
- **Management link**: A peripheral navigation element at the edge of the chat page. Not the primary action. Labeled *"管理"*.
- **HITL notification**: A bell icon in the app shell corner, visible on both chat and management surfaces. Globally accessible.

## Elevation & Depth

Inherited from Fluent UI — subtle shadows on hover/active states, no elevation as a primary visual hierarchy device. Vulgata adds nothing on top of this. The brand discipline is "Fluent's shadows are correct."

Depth is communicated through surface layering (Background → Surface → Card/Dialog) rather than shadow elevation. The distinction between `{colors.background}` and `{colors.surface}` does the heavy lifting for visual hierarchy.

## Shapes

Fluent UI defaults inherited as-is: `rounded/sm` (4px) for inputs and small controls, `rounded/md` (8px) for cards and buttons, `rounded/lg` (12px) for dialogs and panels. Fluent's native radius already feels right for the warm-professional register — slightly softer than a pure tool, not as soft as a consumer app.

No pill shapes (`rounded/full`). No custom radius overrides. The discipline is "Fluent got it right."

## Components

Vulgata uses the following Fluent UI Blazor components as-is, unchanged: `FluentButton` (all variants except primary), `FluentDialog`, `FluentDataGrid`, `FluentNavMenu`, `FluentTextField`, `FluentTextArea`, `FluentToast`, `FluentTooltip`, `FluentBreadcrumb`, `FluentProgressBar`, `FluentProgressRing`, `FluentBadge`, `FluentIcon`, `FluentDivider`, `FluentTreeView`, `FluentTab`, `FluentCard`, `FluentCombobox`, `FluentSelect`, `FluentSwitch`. The contract: don't customize these.

Brand-layer-overridden components:

- **Primary Button** — The main call-to-action button. `{colors.primary}` fill, `{colors.primary-foreground}` text, `{rounded.md}` corner. Applied to: *"发送"* (send chat), *"开始扫描"* (start scan), *"保存"* (save in management). Other button variants (secondary, outline, ghost, subtle) inherit Fluent defaults.

- **Mode Badge** — The current-mode indicator above the chat input. Displays either *"业务模式"* or *"技术模式"*. `{colors.primary}` fill, `{colors.primary-foreground}` text, `{rounded.sm}` corner. Compact, non-interactive (mode switching is a separate control). Acts as a session-state label, not a toggle.

- **System/Repo Tag** — The inline selector chips in the chat area showing which system and repository are active. `{colors.accent}` fill, `{colors.accent-foreground}` text, `{rounded.sm}` corner. Removable, selectable. Appears in the chat context bar and in the management tree as active-node highlight.

- **Chat Bubble (Assistant)** — `{colors.muted}` background in light mode, `{colors.muted-dark}` in dark mode. Left-aligned. Contains markdown-rendered LLM responses with citation links.

- **Chat Bubble (User)** — `{colors.surface}` background in light mode, `{colors.surface-dark}` in dark mode. Right-aligned. Plain text.

- **Mode-Select Banner** — The interstitial shown when starting a new session (after mode switch). `display` typography, `{colors.primary}` as text color, centered. Text: *"开始新的对话"* with the selected mode name beneath.

- **HITL Notification Icon** — Fluent's bell icon with a `{colors.accent}` dot when there are pending HITL questions. Positioned in the app shell corner (top-right). Globally visible across both chat and management surfaces.

- **Management Tree View** — Fluent's `FluentTreeView` with the active system/repo node highlighted in `{colors.accent}`. Systems as parent nodes, repositories as child nodes. Scans in progress show a `FluentProgressRing` adjacent to the repo name. Plus a "+ 新建系统" button at the top.

- **Knowledge Graph** — Rendered via Z.Blazor.Diagrams (the sole non-Fluent exception). Nodes for documents, edges for references. Intra-repo edges = solid light-gray; cross-repo = dashed colored; unresolved = dashed to red diamond placeholder; disputed = yellow dashed with warning icon. Background matches `{colors.background}` / `{colors.background-dark}`. Toolbar uses `{colors.primary}` for active controls.

- **Document Viewer** — Markdown-rendered content on `{colors.surface}` background. Code blocks with `{colors.muted}` background. Document metadata header uses `{colors.primary}` for the title. Type badges: blue for Code Logic Docs, green for Business Logic Docs.

- **Admin Pages (LLM Config, User Management)** — Standard Fluent UI forms and grids, no brand overrides beyond primary buttons and surface tokens. Inherit Fluent defaults.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Inherit Fluent UI defaults for everything not in the brand layer | Override Fluent's color tokens beyond `primary` and `accent` |
| Use `{colors.accent}` only for contextual selection (active system/repo, HITL dot) | Use accent for status badges, chrome decoration, or hover affordances |
| `display` typography sparingly — empty states, greetings, section headers | Set body text or chat content in `display` to "make it pretty" |
| Use Chinese for all UI strings — labels, buttons, placeholders, empty states | Mix Chinese and English in labels or expose English-only UI strings |
| Lock theme to mode — 业务模式 always light, 技术模式 always dark | Allow mid-session theme toggling or decouple mode from theme |
| Keep the chat surface as the default landing view | Build a dashboard or landing page as the primary entry point |
| Surface the knowledge graph through a dedicated Graph page with Z.Blazor.Diagrams | Hide the graph entirely or show it only in chat |
| Use Fluent's native rounded corners (4/8/12) | Introduce custom radius values or pill-shaped elements |
| Single-column layout with comfortable max-width | Wide multi-column layouts — Vulgata is a reading-and-chat product |
| Place the mode selector above the chat input | Hide mode selection in a settings panel or toolbar menu |
| Use the serif only for Chinese display text in Noto Serif SC | Use the serif for Latin text (falls back poorly) or for body copy |

---

## Mockups

- [Chat 界面 (业务模式 + 技术模式)](mockups/chat.html) — 双模式对话界面，林和王的关键流程
- [管理后台 (Holy Grail 布局)](mockups/management.html) — 系统管理 + 仓库详情 + 扫描仪表盘，张的关键流程
- [信息架构图](wireframes/ia-2026-06-22.excalidraw) — 全应用 IA 总览（Excalidraw）

