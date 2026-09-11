---
project: vulgata
type: experience
status: final
created: 2026-06-22
updated: 2026-09-11
sources:
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md
  - docs/UBIQUITOUS_LANGUAGE.md
---

# EXPERIENCE.md — vulgata

> 行为规范。组件行为、状态模式、交互原语、关键流程。视觉规格见 `DESIGN.md`。
> Component library: Microsoft Fluent UI Blazor。Blazor Interactive Server 渲染模式。

## Scope adoption — 2026-09-11

The [approved reconciliation](../../sprint-change-proposal-2026-09-11-generic-product-reconciliation.md) aligns affected behavior with the [adopted target](../../architecture/ARCHITECTURE-SPINE.md), especially AD-3/13–15/24–29. This is a target UX contract, not a claim that the current application implements or has qualified it. The [archived pre-adoption version](../../../history/pre-generic-adoption-2026-09-11/files/docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md) preserves the earlier behavior. Brand tokens, Chinese-only UI, chat-first navigation, management layout, identity/administration/context workflows and the basic accessibility floor remain unchanged in scope.

Product HITL notification/inbox/question/answer-dismiss, Chat file/image attachments and advanced/live portfolio graph are deferred. This does not defer visible Uncertainties, authorized reader/history/citations, the basic manual-refresh graph, real Scan progress, operational pause/resume/cancel or genuine live Chat. No deferred control or placeholder feature is required.

## Foundation

**形态：** Desktop-browser Web application using Blazor Interactive Server and SignalR. The deployment target is two Docker Compose services: Vulgata with bundled local analysis tools, and PostgreSQL. Browser reconnect and host failure have different Chat outcomes; no offline operation is promised.

**UI 系统：** Microsoft Fluent UI Blazor。组件库承担大部分基础行为；品牌层覆盖仅限 `DESIGN.md` 定义的 tokens。圆角沿用 Fluent 默认值（4px / 8px / 12px），无覆盖。

**视觉参考：** `DESIGN.md` 为视觉识别权威来源。本文档中 `{DESIGN.md.colors.primary}` 语法表示对 `DESIGN.md` 中定义的设计 token 的交叉引用。

**约束：** 无离线状态设计。无 i18n — 仅简体中文。无 WCAG 专项合规要求。V1 单语言、单部署。

---

## Information Architecture

### App Shell（应用壳）

登录后所有界面共享 **顶栏导航**（Navbar）。顶栏包含：

- 左侧：品牌标识 + 主导航菜单项（对话、管理后台）
- Right: user avatar with 「个人资料」 and 「退出登录」. The historical HITL bell/unread badge is deferred.

**Chat 界面** 和 **管理后台** 是两个顶层路由，通过顶栏导航切换。

Management controls retain their existing role/ownership authorization. Document/history/citation and basic graph views are Knowledge-authorized reads, not management operations: permitted readers can reach them from Chat references and reader navigation without management rights. Placement within the existing shell must not introduce a management authorization requirement or a new top-level layout.

### 管理后台 — Holy Grail 布局

管理后台使用 holy grail 布局（顶栏 + 左侧栏 + 主内容区）：

```
┌──────────────────────────────────────────┐
│  顶栏: 品牌 | 系统管理 | 图谱 | 文档 | ...│
├──────────┬───────────────────────────────┤
│ 左侧栏    │  主内容区                       │
│ (导航树)  │  选中系统/仓库的详情             │
│          │  或图谱视图                      │
│ 系统列表  │  或文档视图                      │
│  ├ 系统A  │                                │
│  │ ├仓库1 │                                │
│  │ └仓库2 │                                │
│  └ 系统B  │                                │
│          │                                │
└──────────┴───────────────────────────────┘
```

**顶栏菜单项（管理后台）：**

| 菜单项 | 左侧栏内容 | 主内容区 |
|---|---|---|
| **系统管理** | 系统树视图（系统 → 仓库） | 选中系统/仓库的详情 + 扫描状态 |
| **图谱** | Selected permitted scope | Basic manual-refresh Logical Flow/Document neighborhood (Z.Blazor.Diagrams) |
| **文档** | CL source tree or BL Logical Flow view, according to mode | Selected Document Version and history |
| **扫描历史** | 扫描记录列表 | 选中扫描的详细结果 |
| **设置** | 设置导航 | LLM 配置 / 用户管理（Administrator） |

### 各界面详情

| 界面 | 入口 | 用途 |
|---|---|---|
| 登录/注册 | 首次访问 | 邮箱+密码注册；登录后直接进入 Chat 界面 |
| Chat（对话） | 顶栏"对话" | 主要交互界面。模式选择、系统/仓库选择、对话区 |
| 管理后台 → 系统管理 | 顶栏"管理后台" → "系统管理" | 系统/仓库 CRUD，扫描进度监控 |
| 图谱 | 「图谱」 and authorized reader navigation | Manual-refresh scoped graph; Knowledge authorization |
| 文档 | 「文档」 or exact Chat citation | Mode-safe Document browsing, reading and history; Knowledge authorization |
| 管理后台 → 扫描历史 | 顶栏"管理后台" → "扫描历史" | 历史扫描记录 |
| 管理后台 → 设置 | 顶栏"管理后台" → "设置" | LLM Provider 配置、用户管理（Administrator） |
| 通知中心（暂缓） | No current entry required | Historical HITL inbox; not part of current scope |

**Chat 界面结构（从上到下）：**

1. Navbar: brand, 「对话」 / authorized 「管理后台」 navigation, avatar; no current HITL bell.
2. 模式选择器（业务模式 / 技术模式 双标签切换）
3. 内联系统/仓库选择器
4. 对话区（消息气泡流）
5. 输入框（底部固定）

**管理后台结构（从上到下，左到右）：**

1. Navbar: brand, authorized 「系统管理」 / 「图谱」 / 「文档」 / 「扫描历史」 / 「设置」, avatar; reader access is distinct from management rights.
2. 左侧栏（内容随菜单项变化：系统树 / 文档树 / 扫描列表 / 设置菜单）
3. 主内容区（选中项的详细信息）

**系统管理页（默认管理后台着陆）：**
- 左侧栏：系统树视图 + "新建系统" 按钮
- 主内容区：
  - 选中系统时：系统名称、描述、仓库数量、扫描中仓库数、仓库列表（每行含扫描状态 + 仪表盘入口）、"新建仓库" 按钮、编辑/删除系统操作
  - 选中仓库时：仓库详情（名称、描述、git URL、上次扫描、文档数量）、扫描状态区（进度条 + 阶段指示器 + 仪表盘入口）、编辑/删除仓库操作

**通知中心：** Deferred with product HITL. Missing facts remain visible in authorized Knowledge/Scan views without requiring an owner-answer workflow.

**文档浏览：** CL browsing follows Repository → file → Code Unit; a file can own several semantic-unit Documents. BL browsing follows Logical Flows within System/Repository views; Cross-Repository appearances share one Logical Document. Read exact versions/sections and history through authorized navigation, including Chat citations. Business exposes BL only, with no CL/source metadata in fetched content or previews.

**知识图谱：** The dedicated Z.Blazor.Diagrams view shows a selected Logical Flow or Document neighborhood with manual 「刷新」, pan/zoom and permitted scope selection. Bind nodes/relationships to the selected version/evidence; explain actual Edge meanings and explicit Uncertainties. Sparse authorized graphs are valid. Live animation and advanced portfolio layouts are deferred; interaction never edits domain Edges.

---

## Voice and Tone

### 业务模式

用户为非技术角色（PM、BA）。文案要求：平实、叙述性、无术语。

| ✅ 应写 | ❌ 不应写 |
|---|---|
| "风险评估涉及三个系统：手机银行接收用户请求，风控引擎执行规则校验，核心系统完成资金操作。" | "风控引擎通过 SOFA RPC 调用核心系统的 evaluateRisk 接口。" |
| "需要我帮你梳理哪个业务流程？" | "请输入查询参数。" |
| "找到了 3 份相关文档。" | "检索返回 3 条记录。" |
| "以下是文档支持的贷款审批步骤，未确认的部分已注明。" | "Business Logic Document #42 摘要如下。" |

### 技术模式

用户为开发者。文案要求：精确、代码感知、直接。

| ✅ 应写 | ❌ 不应写 |
|---|---|
| "riskEvaluate() 通过 Dubbo 调用 riskeng-service 的 evaluate() 方法 → `RiskController.java:156`" | "系统之间有一个调用关系。" |
| "跨仓库链路已解析：3 条确认，1 条待扫描。" | "链接情况还行。" |
| "该函数依赖 `UserService.validate()`（同仓库）和 `RiskClient.evaluate()`（跨仓库 → riskeng-service）" | "这个函数调用了其他一些东西。" |

### Uncertainty and operational status

Neutral and actionable, without requiring a product notification/inbox workflow.

| ✅ | ❌ |
|---|---|
| "有 2 处信息尚未确认，相关说明已标注。" | "🎉 新问题来啦！" |
| "目标仓库暂无可用知识；当前说明保留这一限制。" | "错误：无法解析 — 请立即处理！" |

### 错误状态

不归咎。陈述事实 + 提供下一步。

| ✅ | ❌ |
|---|---|
| "连接失败，正在重试。" | "API 调用出错！" |
| "找不到相关文档。试试换个问法，或检查系统选择范围。" | "无结果。" |
| "扫描已暂停（模型服务暂不可用）。服务恢复后，请点击‘恢复扫描’。" | "扫描失败。" |

---

## Component Patterns

行为规范。视觉规格见 `DESIGN.md.Components`（或 Fluent UI 默认值）。

### ChatArea（对话区）

| 属性 | 规则 |
|---|---|
| 消息气泡 | User right, assistant left. Relay actual answer deltas as provisional output, not replay of a buffered complete answer. Label hypotheses on first appearance; show no hidden reasoning or secrets. |
| 引用 | Useful claim-linked authorized references, not fabricated citations for greetings/no-results. Inline previews can open the exact cited Document Version/section in the reader. Business: BL title/section and permitted business content only. IT: permitted CL/BL and recorded-revision source where relevant/available. |
| 读取一致性 | One Knowledge Read Context pins discovery, related membership, pagination, content and citations for the turn. Later publications do not retarget the active answer; later turns may use fresher knowledge. Authorization remains live. |
| 会话历史 | Durable ordered dialogue and original references survive reconnect. Initially one active answer per Conversation. Only permitted completed history enters inference; interrupted text retains its status and hypothesis meaning. |
| 完成与重连 | Mark completed only after valid provider completion and durable content/reference/status save. Browser disconnect joins the same host-owned turn on reconnect; host failure instead requires explicit retry. See Chat state table. |
| 模式徽章 | 当前会话模式显示在对话区顶部：业务模式为浅色背景的 "业务模式" 标签，技术模式为深色背景的 "技术模式" 标签。 |
| 系统标签 | 当前选中的系统/仓库以小型 Fluent Tag 显示在输入框上方。可点击 × 移除。 |
| 空状态 | 新会话时居中显示 Noto Serif SC 欢迎语。"你好，想了解什么业务流程？"（业务模式）或 "你好，需要追踪哪段逻辑？"（技术模式）。 |
| 输入框 | `Enter` 发送，`Shift+Enter` 换行。placeholder 随模式变化。 |
| 附件 | File/image Chat uploads are deferred; retain typed questions and advisory text context. |

### ModeSelector（模式选择器）

| 属性 | 规则 |
|---|---|
| 外观 | 双标签切换器，位于对话区上方、系统选择器上方。当前选中标签高亮为 `{DESIGN.md.colors.primary}`。 |
| 切换行为 | Confirm with 「切换模式将开始新对话，已保存的历史仍可查看。确定切换？」. Open a fresh inference context/new-chat surface and update theme; retain only authorized older dialogue. An active turn remains bound to its originating context, not the new mode. Never import old IT prompts, summaries, caches or late deltas into Business. |
| 主题联动 | 业务模式 → 浅色主题；技术模式 → 深色主题。切换 200-400ms 过渡动画。 |

### InlineSelector（内联系统/仓库选择器）

| 属性 | 规则 |
|---|---|
| 外观 | 位于模式选择器下方、对话区上方。Fluent Dropdown 多选组件，以内联方式呈现。 |
| 选项 | Show only authorized Systems/Repositories, including standalone Repositories. Retain whole-System and individual Repository selection; organizational grouping is not communication evidence. |
| 默认 | Retain the last-used selection filtered by live authorization. First use selects all permitted Systems and eligible standalone Repositories; this does not grant management authority. |
| 空选择 | Retain automatic relevant-scope selection within current authorization only. Show 「已自动选择以下范围：{范围名称}」 and 「调整范围」; never broaden access or initiate a Scan. |
| 范围变更 | Create fresh inference context; recheck completed history/summaries against the new scope and live access. Keep old turns/references bound to their originating contexts. |

### SystemTree（系统树视图）

| 属性 | 规则 |
|---|---|
| 外观 | Fluent TreeView。系统为父节点，仓库为子节点。展开/折叠箭头。选中节点高亮为 `{DESIGN.md.colors.accent}`。顶部有 "+ 新建系统" 按钮。 |
| 可见性 | Existing Administrator/SystemOwner management policy and assigned-System ownership apply. Ordinary User readers gain no management rights; Knowledge views remain independently reachable. |
| 交互 | 点击系统节点 → 主内容区显示系统详情（系统信息、仓库列表、扫描中仓库数）。点击仓库节点 → 主内容区显示仓库详情。右键系统节点可编辑/删除。 |
| 空状态 | "尚未添加任何系统。创建你的第一个系统开始扫描。" + 主按钮"创建系统"。 |
| 扫描指示 | Real operational state with text/icon, not color alone; distinguish queued, running, paused and terminal outcomes. Separate coverage and Knowledge Currentness from the status dot. |

### RepoDetail（仓库详情 — 主内容区）

| 属性 | 规则 |
|---|---|
| 内容 | 顶部：仓库名称 + 描述 + 编辑/删除操作按钮。信息卡片：git URL、上次扫描时间、扫描状态、文档数量。 |
| 扫描入口 | 「开始扫描」 starts one durable source-to-knowledge workflow, with no separate Pass 1/resolution/Pass 2 starts. Running/paused work exposes real progress and authorized operational controls. 「查看文档」 reads permitted selected Knowledge, including gaps. |
| 编辑 | System Owner 可通过编辑按钮修改仓库元数据。 |

### SystemDetail（系统详情 — 主内容区）

| 属性 | 规则 |
|---|---|
| 内容 | 顶部：系统名称 + 描述 + 编辑/删除操作按钮。统计卡片：仓库总数、扫描中仓库数、已完成仓库数、文档总数。 |
| 仓库列表 | FluentDataGrid 表格：仓库名称、扫描状态、上次扫描时间、文档数。每行有"查看"操作。表头有 "+ 新建仓库" 按钮。 |
| 批量扫描 | System/selection controls start independent Repository Scan Runs; optional Scan Campaign progress is aggregate only. No mandatory pair, global wait or fixed sample scope. |

### GraphView（知识图谱）

| 属性 | 规则 |
|---|---|
| 入口 | 「图谱」 or authorized reader navigation; Knowledge authorization, not management authority. |
| 渲染 | Z.Blazor.Diagrams, selected Logical Flow/Document neighborhood, 「刷新」, pan/zoom and authorized System/Repository filters. Layout is presentation, not relationship editing. |
| 节点与边 | Selected version/evidence only. Preserve permitted CL blue / BL green styling and labelled REFERENCES/GENERATED_FROM meanings. An explicit Uncertainty may use a dashed frontier/diamond; no invented target or failed draft Document. Shared code does not establish business dependency. |
| 权限 | Server filters every node, edge, label, count, tooltip and destination by user/mode/scope, including all contributors to composite prose. Business receives BL-only data, never a fetched IT graph merely hidden in the UI. |
| 更新 | Manual refresh may select newer Knowledge and reports selection/currentness. Historical views remain bound to their versions. Live node animation and advanced portfolio visualization are deferred. |
| 空状态 | 「当前范围暂无可用图谱。可调整范围或稍后刷新。」 Sparse permitted graphs and explicit partial/stale states are valid. |

### DocumentTree（文档浏览）

| 属性 | 规则 |
|---|---|
| 入口 | 「文档」 or exact cited-section navigation from Chat; accessible to authorized Knowledge readers without management rights. |
| 左侧栏 | CL: Repository/file/Code Unit source hierarchy. BL: Logical Flows under System/Repository views; Cross-Repository appearances share a Logical Document. Business shows BL only; IT may show both type badges. |
| 主内容区 | Render selected authorized Document Version/section and explicit 「版本历史」. Preserve exact historical dependencies/relationships rather than following today's graph. IT may view Repository + full Profiled Revision + relative source locations; Source Snapshot is transparent access, not a public version axis. |
| 来源与可见性 | Business never receives CL, source code, file/range/Git metadata in body, header, preview or citation target. IT source opens only the authorized recorded revision when available; show 「该版本源代码暂不可用，文档及来源记录仍保留。」 if absent, never substitute current checkout. |
| 当前性 | Label pending/stale/failed refresh and coverage gaps; initial canonical-source retention does not imply every historical source is always available. Authorization remains live for all composite contributors and history. |
| 搜索 | 左侧栏顶部搜索框，按文档标题/关键词过滤。 |
| 空状态 | 「当前范围暂无可用文档。可调整范围或稍后再试。」 Management actions appear only for users authorized to perform them. |

### ScanDashboard（扫描仪表盘）

| 属性 | 规则 |
|---|---|
| 入口 | Repository details expose 「仪表盘」 for active, paused and recorded terminal work; keep the existing inline/detail arrangement. |
| 内容 | Real SignalR progress: operational state, admitted/accepted/pending/failed primary-source counts, explicit gaps, active Worker/error counts and separate Knowledge publication/refresh state. No fictional percent or elapsed-time simulation. |
| 阶段指示器 | Show observed work such as 「源代码准备」 / 「代码分析」 / 「跨仓库解析」 / 「业务说明生成」 / 「知识发布」 without implying all work is a single sequential progress bar. Independent runs and flow refresh can overlap. File windows are progress, not Code Unit counts. |
| 恢复与取消 | Bounded increasing temporary-provider retries may end in 「已暂停」; configuration errors require correction. 「恢复扫描」 is explicit and checks prerequisites, source and authorization. Restart/provider recovery never auto-resumes paused work or switches provider. Cancellation fences owned unfinished work without deleting accepted history or cancelling independent consumers. |

### NotificationCenter（通知中心）

Deferred product HITL: no current global bell/inbox/question/answer-dismiss or human 「不再处理」 action. The archived June specification preserves the former 400px panel, cards and badges as history. Visible Uncertainties remain; human advisory text retains lower authority than source evidence. Missing information does not force owner intervention or invalidate otherwise accepted analysis. Operational status, authorization and development approval gates are unaffected.

### EmptyState（空状态）

| 场景 | 文案 | 排版 |
|---|---|---|
| 新会话（业务模式） | "你好，想了解什么业务流程？" | Noto Serif SC, `{DESIGN.md.typography.display}` |
| 新会话（技术模式） | "你好，需要追踪哪段逻辑？" | Noto Serif SC, `{DESIGN.md.typography.display}` |
| 无系统（系统管理） | "尚未添加任何系统。创建你的第一个系统开始扫描。" | 正文 + 主按钮"创建系统" |
| 空仓库列表（系统详情） | "该系统下暂无仓库。添加第一个仓库。" | 正文 + 主按钮"新建仓库" |
| 无图谱数据 | 「当前范围暂无可用图谱。可调整范围或稍后刷新。」 | Body + permitted 「调整范围」 / 「刷新」 |
| 无文档 | 「当前范围暂无可用文档。可调整范围或稍后再试。」 | Body + permitted 「调整范围」; no compulsory management navigation |

---

## State Patterns

### LoadState 枚举

全局统一状态枚举：

| 状态 | 界面表现 |
|---|---|
| `Loading` | Fluent Shimmer（骨架屏），4-6 行，匹配预期布局。 |
| `Refreshing` | 内容保持可见，顶部细进度条（indeterminate），不阻塞交互。 |
| `Empty` | 引导文案 + 单一主操作按钮（见 EmptyState 表）。 |
| `NoResults` | "未找到匹配结果。试试调整关键词或系统选择范围。" |
| `Error` | 不归咎文案 + 重试按钮。业务模式不展示技术细节，技术模式可展示简化错误信息。 |

### 模式切换状态

| 状态 | 处理 |
|---|---|
| 切换确认 | Fluent Dialog: 「切换模式将开始新对话，已保存的历史仍可查看。确定切换？」. Do not promise an unsaved answer was saved. |
| 切换中 | Preserve the 200–400ms theme transition/new-chat welcome. Create fresh inference context; late output/save outcomes remain with the originating turn. Older dialogue is visible/reusable only when authorized for its contributors and mode. |

### 扫描状态（RepoDetail）

| 状态 | 表现 |
|---|---|
| 未扫描 | 灰点 + "未扫描" 文字。显示"开始扫描"按钮。 |
| 排队中 | 「排队中」 and observed queue state; do not pretend Worker work has begun. |
| 扫描中 | 「扫描中」 plus real counts/current operations and dashboard. A percentage needs a defined denominator and cannot equate work, coverage and publication. |
| 已暂停 | 「已暂停」, sanitized cause and 「恢复扫描」 after correction/prerequisite checks. Provider availability or host restart does not resume automatically. |
| 正在取消 / 已取消 | Show pending cleanup then the confirmed cancellation outcome. Accepted history remains readable under current authorization. |
| 已结束 | Show actual successful or gap-bearing terminal outcome, timestamp and coverage. Terminal operation does not imply all source accepted or every BL refresh published. |
| 失败 | 「失败」, sanitized reason and available explicit recovery/retry action. No silent source substitution or automatic fallback route. |

These are Chinese display meanings, not a new lifecycle enum/schema. Keep primary-source coverage (admitted/accepted/pending/failed) and Knowledge currentness (published/pending refresh/stale/failed/retired as applicable) visible separately. Counters include only permitted scope and do not leak restricted contributors.

### Chat 加载状态

| 状态 | 表现 |
|---|---|
| 等待回复 | 打字指示器（三点跳动），200ms 间隔。 |
| 检索中 | "正在查找相关文档…" 临时提示。 |
| 流式响应 | Actual provisional answer deltas, labelled 「正在生成」; hypotheses are labelled on first text. No buffered typewriter replay or hidden reasoning. |
| 正在保存 | 「正在保存」 after valid provider completion; not yet normal completed history. Truncation/unfinished tools are not completion. |
| 已完成 | Display completed only after one durable content/reference/status save. Saved completion survives lost acknowledgement; telemetry failure cannot undo it. |
| 连接中断 / 重连 | 「连接已断开，重新连接后将查看同一条回答。」 Host-owned work may finish within original limits. Authorized reattachment reconciles accumulated text and new updates without duplication, loss, new generation or renewed budgets. Disconnect is not cancellation. |
| 已中断 | Explicit cancellation, limit exhaustion or terminal/host failure shows 「回答已中断」 plus reason and useful authorized partial text. Keep it separate from completed factual history; explicit 「重试」 is required after host failure. Never auto-restart generation after visible output. |
| 保存未确认 / 保存失败 | Show 「正在确认保存结果」 while checking durable state; if save failed, show 「回答未完成保存」 and retain useful permitted text/status. Recover/retry the same completion save where possible, never reinvoke the model to fix a save acknowledgement. |
| 权限变化 | Stop unauthorized disclosure and exclude affected generated history/summary content; a stable read selection never freezes access rights. |
| 无结果 | "未找到相关文档。试试调整系统选择范围或换个问法。" |

---

## Interaction Primitives

### 键盘

Chat is keyboard-first: `Enter` sends, `Shift+Enter` inserts a newline, `Esc` closes the active dialog/panel. Management remains mouse-oriented with the existing keyboard-accessibility floor; no current notification-panel action is implied.

### 鼠标

- Click a citation for a mode-safe inline preview and exact version/section reader navigation.
- 悬停仓库行 → 显示操作按钮
- 所有操作即时反馈，无拖拽（V1）

### 模式切换

- Click mode label → confirmation → fresh inference context/new-chat surface, matching theme and welcome; preserve authorized historical dialogue and originating turn bindings.
- 无撤销 — 确认即执行

### 扫描入口

- 扫描未运行 → 按钮"开始扫描"
- Running/queued work → real operational progress + 「仪表盘」 + permitted pause/cancel controls.
- Paused work → sanitized reason + explicit 「恢复扫描」; terminal work → truthful coverage/currentness and retained dashboard/history access.

### Chat 引用

- Attach useful authorized references to supported claims; a no-results response need not fabricate one.
- Preview and open the exact cited Document Version/section. Business shows BL-only title/content; never source paths, Git IDs, Code Units or line ranges. IT may follow authorized source at its recorded Profiled Revision when available.
- Keep previous references fixed to their original versions. Show partial/stale/unavailable-source states, and recheck live contributor access before every disclosure.

### 禁止项

- 无无限滚动（分页或"加载更多"）
- 无拖拽重排
- 无模态堆叠（弹窗不超过一层）
- Do not replace an existing turn with fresher Knowledge automatically. Genuine streaming and same-turn reattachment updates remain required.

---

## Accessibility Floor

Behavioral floor; preserve Fluent defaults and Library Oak tokens. Verify contrast and keyboard/screen-reader behavior during implementation; this adoption supplies no fresh accessibility qualification or certification claim.

- Fluent UI Blazor 内置键盘导航 — `Tab` 顺序跟随阅读顺序
- `Esc` 统一关闭顶层弹窗/面板
- 屏幕阅读器：界面切换时播报 — "对话界面" / "管理系统界面"
- Operational state and streaming completion/interruption use accessible text, not color alone; announce meaningful changes without reading every token. Deferred notification badges, if later authorized, retain accessible count labels.

---

## Key Flows

The named Systems, Repositories and code below are illustrative scenarios, not fixed product scope, required sample data or runtime proof. Explanations describe available static knowledge with branches and gaps, not a guaranteed executed sequence.

### 林 — 产品经理想了解风险评估流程

1. 林打开 Vulgata，用邮箱和密码登录，直接进入对话界面。
2. 她在模式选择器中确认当前是「业务模式」（默认）。
3. 她在内联选择器中勾选「移动银行系统」的相关仓库。
4. 她打字："风险评估的完整流程是什么？" 按 Enter 发送。
5. 系统先显示"正在查找相关文档…"，随后流式输出回答：一步一步的业务叙述，纯中文、无代码、引用业务逻辑文档。
6. 她追问了一个细节，系统继续用业务语言回答。

**高潮节拍：** If permitted, Lin starts 「新建对话」 in 「技术模式」 with a fresh inference context. The dark-theme answer may show exact authorized CL/source references and explicitly describe missing or conflicting evidence. Her saved Business dialogue remains separate; switching modes does not grant new source access.

### 陈 — 新入职业务分析师探索系统

1. 陈入职第一周，打开 Vulgata 登录进入对话界面（业务模式，浅色）。
2. 她在内联选择器中浏览可选系统，勾选了「核心银行系统」。
3. 她打字："贷款审批是怎么走的？"
4. 对话引导她理解相关系统和文档 — 每一步都是中文业务叙述，无代码。
5. 她点击回答中的文档引用，看到业务逻辑文档的摘要。

**高潮节拍：** 陈理解了整个审批流程后，尝试问了一个她自己的问题："如果客户信用评级是 C 级呢？" — 系统依然用业务语言回答，她意识到 Vulgata 可以成为她日常工作的助手。

### 王 — 开发追踪跨仓库调用链路

1. 王切换模式到「技术模式」— 主题立刻变为深色。
2. 他选中「移动银行系统」相关仓库，输入："风险评分计算涉及哪些跨仓库调用？"
3. The answer explains the source-supported path, for example `RiskEngine.evaluate() → LoanService.processApproval() → LedgerService.record()`, with permitted exact references and observed communication mechanisms. It labels branches, cycles and missing bindings; a static path is not proof of runtime execution.
4. Wang opens an exact cited section and, where authorized and retained, its recorded-revision source lines. Unavailable source is explicit rather than replaced with the current checkout.

**高潮节拍：** Wang asks 「这段说明依据哪个版本？还有哪些部分未确认？」. The answer identifies its permitted Document Versions and Profiled Revisions, links exact sections/source where available and explains uncertainties. Automatic source-change detection, impact analysis and claims about an unseen latest commit remain deferred.

### 张 — System owner configures a Scan and inspects Knowledge

1. 张登录后点击顶栏「管理后台」→「系统管理」。
2. 左侧栏显示他负责的系统树视图。他点击「核心银行系统」，主内容区显示系统详情：3 个仓库，1 个扫描中，2 个已完成。
3. 他点击主内容区的「+ 新建仓库」按钮，在弹出的表单中填写 git URL 和描述，保存。
4. 新仓库出现在左侧树和仓库列表中。他点击该仓库，主内容区切换到仓库详情。
5. 他点击「开始扫描」按钮。按钮变为进度条 + 阶段指示器，旁边出现脉动的「仪表盘」链接。
6. 他点击「仪表盘」，看到实时扫描进度 — 文件已处理/总数、活跃 Worker 数。
7. Zhang sees explicit coverage gaps/Uncertainties alongside accepted results, with no mandatory HITL inbox. If a provider outage pauses the run after bounded retries, he corrects the prerequisite and deliberately selects 「恢复扫描」.
8. After a real checkpoint/publication, Zhang opens 「图谱」 and selects 「刷新」 for the permitted version-bound neighborhood. In 「文档」, he browses CL by source/Code Unit and BL by Logical Flow, using history and exact references; terminal Scan status alone does not assert every BL refresh is current.

**高潮节拍：** Zhang returns to Business Chat and asks 「核心银行系统的贷款审批流程是什么？」. Published permitted BL supports a useful provisional explanation with cited sections and explicit gaps. The turn becomes completed only after durable save; browser reconnect rejoins the same answer without generating again.

---

## Mockups

These June files remain unchanged visual/layout references. Their historical HITL, advanced graph, source-preview and recovery behaviors do not override this adopted target.

- [Chat 界面 (业务模式 + 技术模式)](mockups/chat.html) — 双模式对话界面
- [管理后台 (Holy Grail 布局)](mockups/management.html) — 系统管理 + 仓库详情 + 扫描进度
- [信息架构图](wireframes/ia-2026-06-22.excalidraw) — IA 总览（Excalidraw）

