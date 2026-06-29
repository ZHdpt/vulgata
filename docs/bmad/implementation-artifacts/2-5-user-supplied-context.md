---
story_key: 2-5-user-supplied-context
title: Story 2.5: User-Supplied Context
Status: ready-for-dev
Epic: 2
Story: 5
created: 2026-06-29
baseline_commit: 18a6c5ea7f5d68e2fbf598bc479a47df6c461aa7
depends_on:
  - 2-1-system-crud-admin
  - 2-2-grant-system-ownership
  - 2-3-repository-management
  - 2-4-standalone-repository-creation
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/implementation-artifacts/2-3-repository-management.md
  - docs/bmad/implementation-artifacts/2-4-standalone-repository-creation.md
  - src/dotnet/Vulgata.Core/Entities/System.cs
  - src/dotnet/Vulgata.Core/Entities/Repository.cs
  - src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs
  - src/dotnet/Vulgata.Web/Components/Layout/ManagementLayout.razor
  - src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor
  - src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor
  - src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs
  - src/dotnet/Vulgata.Web/Program.cs
  - tests/Vulgata.Tests/RepositoryManagementTests.cs
---

# Story 2.5: User-Supplied Context

## 用户故事

作为一名 **Administrator** 或 **SystemOwner**，
我希望在全局、系统和仓库三个层级维护纯文本上下文，
从而让后续扫描代理和问答代理在不修改源代码的前提下获得业务补充信息。

## 目标边界

- 本故事覆盖三类上下文的录入、存储、显示、读取组合顺序，以及扫描进行中时的“延后生效”规则。
- 本故事只允许纯文本上下文；不支持 Markdown、富文本、附件、结构化 JSON/YAML 或引用外部文件。
- 本故事需要把“上下文”从已有的系统/仓库字段扩展为一个清晰的管理能力，但不实现真正的扫描代理消费逻辑；代理消费入口只需建立可复用的读取/组合契约，供后续 Epic 5、Epic 8、Epic 9 调用。
- 本故事不实现完整的通知中心、实时扫描状态或 HITL 回复界面；若为满足 FR-14.4 需要记录“待应用修改”，应使用最小可持久化方案，而不是提前做完整工作流引擎。
- 本故事必须保持中文 UI，并延续 Story 2.3 / 2.4 已建立的管理后台与仓库管理行为，不得回退已有权限边界。

## 验收标准

### AC-1: 管理员维护全局上下文

**假如** 我以 `Administrator` 身份进入 `管理后台 -> 设置`  
**当** 我打开全局上下文设置区域  
**那么** 我应看到标签为 `全局上下文（适用于所有系统）` 的纯文本输入区域  
**并且** 我可以编辑并保存  
**并且** 保存后的内容会被持久化，而不是只停留在页面状态中

### AC-2: SystemOwner 维护系统级上下文

**假如** 我以 `SystemOwner` 身份查看自己被授权的系统  
**当** 我打开该系统的设置区域  
**那么** 我应看到标签为 `系统上下文` 的纯文本输入区域  
**并且** 我可以编辑并保存  
**并且** 未获授权的系统不应允许我写入其上下文

### AC-3: SystemOwner 维护仓库级上下文

**假如** 我以 `SystemOwner` 身份查看自己被授权系统下的仓库，或查看独立仓库  
**当** 我打开仓库设置区域  
**那么** 我应看到标签为 `仓库上下文` 的纯文本输入区域  
**并且** 我可以编辑并保存  
**并且** 独立仓库的上下文编辑权限应与 Story 2.4 已定义的 `ManagementAccess` 可见性保持一致

### AC-4: 多层上下文按固定顺序组合

**假如** 全局、系统、仓库三个层级都提供了上下文  
**当** 后续代理或应用服务请求某仓库的有效上下文  
**那么** 系统应按 `全局 -> 系统 -> 仓库` 的顺序组合文本  
**并且** 返回结果必须保留层级顺序，避免仓库级文本覆盖或打乱全局文本  
**并且** 不属于任何系统的独立仓库只组合 `全局 -> 仓库`

### AC-5: 扫描进行中时变更排队

**假如** 某个仓库当前存在进行中的扫描  
**当** 管理用户修改该仓库生效范围内的上下文（仓库级，或其所属系统级，或全局级）  
**那么** 该修改不得立即覆盖当前扫描可见的上下文  
**并且** 系统应把修改记录为待应用状态  
**并且** UI 应显示中文提示 `上下文修改将在当前扫描完成后生效`

### AC-6: 无扫描时立即生效

**假如** 当前没有相关扫描在运行  
**当** 我更新任一层级的上下文并保存  
**那么** 该修改应立即成为后续扫描与问答请求读取到的最新上下文

### AC-7: 普通用户只读且无编辑入口

**假如** 我是普通 `User`  
**当** 我查看系统或仓库详情中的上下文展示  
**那么** 我只能以只读方式查看现有上下文  
**并且** 不应看到保存按钮或编辑控件  
**并且** 任何直接写接口请求都必须被服务器拒绝

### AC-8: 中文界面与纯文本约束

**假如** 我在上下文管理界面执行查看或保存  
**当** 页面渲染或返回校验结果  
**那么** 所有标题、标签、按钮、提示、错误消息都必须是简体中文  
**并且** 输入内容按纯文本处理，不渲染 Markdown，不接受附件

## Tasks / Subtasks

- [ ] Task 1: 建立可持久化的上下文配置模型与读取契约 (AC-1, AC-4, AC-6)
  - [ ] 1.1 为全局上下文新增明确的持久化模型，不要把平台级配置塞进 `System` 或 `Repository` 现有实体
  - [ ] 1.2 梳理并复用 `System.Context` 与 `Repository.Context` 现有字段，避免重复创建平行列
  - [ ] 1.3 新增统一的上下文解析/组合服务，显式输出 `全局 -> 系统 -> 仓库` 顺序结果，供后续扫描与问答复用

- [ ] Task 2: 定义“扫描中延后生效”的最小领域契约 (AC-5, AC-6)
  - [ ] 2.1 为上下文修改增加最小可持久化的待应用表示，能够区分“立即生效”与“排队待生效”
  - [ ] 2.2 在当前尚无完整扫描引擎落地的前提下，定义可测试的扫描状态抽象或接口边界，而不是硬编码占位布尔值到页面
  - [ ] 2.3 明确全局/系统变更影响哪些仓库范围，确保“相关扫描运行中则排队”的规则可被服务层统一判断

- [ ] Task 3: 扩展应用服务与 API (AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7)
  - [ ] 3.1 为全局上下文增加受 `AdministratorOnly` 保护的读取与保存 API
  - [ ] 3.2 为系统上下文增加基于现有系统授权链路的读取与保存 API
  - [ ] 3.3 为仓库上下文增加基于现有仓库可见性规则的读取与保存 API，包括独立仓库场景
  - [ ] 3.4 对外返回 DTO 时显式区分 `当前生效上下文` 与 `待生效上下文`（如存在），避免 UI 无法提示排队状态

- [ ] Task 4: 增量演进管理后台 UI (AC-1, AC-2, AC-3, AC-5, AC-7, AC-8)
  - [ ] 4.1 将当前占位型 `设置` 页面演进为真正的全局上下文管理入口
  - [ ] 4.2 在系统详情或系统设置区域中加入系统上下文编辑/只读展示
  - [ ] 4.3 在仓库详情或仓库设置区域中加入仓库上下文编辑/只读展示，并覆盖独立仓库
  - [ ] 4.4 对排队保存场景显示 `上下文修改将在当前扫描完成后生效`，对立即生效场景显示中文成功反馈

- [ ] Task 5: 为后续代理消费建立稳定调用面 (AC-4, AC-5, AC-6)
  - [ ] 5.1 提供单一入口获取“某仓库当前对代理可见的有效上下文”
  - [ ] 5.2 为独立仓库明确返回 `全局 + 仓库` 组合结果
  - [ ] 5.3 在实现中避免让后续扫描/聊天直接拼接数据库字段，统一走上下文组合服务

- [ ] Task 6: 增加 Story 2.5 测试覆盖 (AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8)
  - [ ] 6.1 覆盖管理员保存全局上下文并重新读取成功
  - [ ] 6.2 覆盖被授权 `SystemOwner` 保存系统/仓库上下文成功，未授权用户被拒绝
  - [ ] 6.3 覆盖独立仓库上下文可由具备 `ManagementAccess` 的用户维护
  - [ ] 6.4 覆盖普通 `User` 只能读取不可编辑，且写接口被拒绝
  - [ ] 6.5 覆盖上下文组合顺序为 `全局 -> 系统 -> 仓库`，独立仓库为 `全局 -> 仓库`
  - [ ] 6.6 覆盖“扫描中保存进入待应用状态”和“无扫描时立即生效”两条路径

- [ ] Task 7: 验证与收尾
  - [ ] 7.1 运行 Story 2.5 相关集成测试并确认红绿循环完成
  - [ ] 7.2 运行完整构建与测试，确认对 Story 2.3 / 2.4 不回归
  - [ ] 7.3 更新故事文件的 Dev Agent Record、File List、Change Log 与最终状态

## 功能需求提炼

- FR-14.1: Administrators shall supply context at the global level.
- FR-14.2: System Owners shall supply context at the System level.
- FR-14.3: System Owners shall supply context at the Repository level.
- FR-14.4: Context changes during an active Scan shall be queued unless the change is part of HITL answering.
- UI 必须保持简体中文。
- 上下文在本阶段只能是纯文本。

## 现有实现与必须复用的代码面

### 当前已有的上下文字段并不等于“上下文管理能力”

- `src/dotnet/Vulgata.Core/Entities/System.cs`
  - 已有 `Context` 字段与 `UpdateDetails(...)` 归一化逻辑。
  - 这意味着系统级上下文已有持久化落点，但目前只在系统 CRUD 表单里作为“补充上下文”随系统信息一起维护，尚未形成独立的读取/显示/权限故事。
- `src/dotnet/Vulgata.Core/Entities/Repository.cs`
  - 已有 `Context` 字段，系统内仓库和独立仓库都可承载仓库级上下文。
  - Story 2.5 应复用该字段，而不是再创建第二份仓库上下文字段。

### 当前缺失全局上下文持久化模型

- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
  - 当前只公开 `Systems`、`Repositories`、`SystemOwnerAssignments` 三个集合。
  - 不存在任何平台级配置或全局上下文实体，这正是 Story 2.5 需要补齐的根缺口。
- 当前代码库中也没有现成的 `ApplicationSetting`、`GlobalContext` 或等价配置聚合可直接复用。

### 当前设置页仍是占位页

- `src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor`
  - 目前只有静态说明和“用户管理”入口，没有真正的上下文编辑 UI。
  - Story 2.5 应优先把这里演进为全局上下文入口，而不是新增一条偏离现有导航的信息架构路径。
- `src/dotnet/Vulgata.Web/Components/Layout/ManagementLayout.razor`
  - 顶部标签已包含 `设置` 路由，可作为全局上下文承载位置。
  - 左侧“系统树将于 Epic 2 完成”仍是占位文案，说明系统/仓库设置入口也可能需要在当前管理页结构中渐进落地。

### 当前仓库与系统管理已建立权限和列表基线

- `src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs`
  - 已明确系统内仓库与独立仓库两种可见性规则。
  - Story 2.5 的仓库上下文编辑应复用这些授权边界，而不是重新实现一套仓库权限判断。
- `src/dotnet/Vulgata.Web/Program.cs`
  - 已有 `ManagementAccess` 与 `AdministratorOnly` 授权策略，以及系统/仓库最小 API 组织方式。
  - 新增上下文 API 时必须保持 `/api/` 前缀、ProblemDetails/ValidationProblem 风格和现有授权模式。
- `src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor`
  - 当前已经同时展示系统与独立仓库列表，并保留系统级 `补充上下文`、仓库级 `补充上下文` 表单字段。
  - 但它还没有“设置/详情视图”来显示当前已保存上下文，也没有只读/编辑模式区分，更没有“扫描中待应用”提示。

### 当前没有扫描状态抽象可直接判定 FR-14.4

- 现有 Epic 2 代码还未落地真正的扫描引擎、扫描状态存储或通知机制。
- 因此 Story 2.5 的关键不是伪造完整扫描系统，而是建立一个最小、可演进、可测试的“相关扫描是否运行中”判断边界，供后续 Epic 5 替换真实实现。

## 技术实现计划

### 1. 建立全局上下文持久化模型

新增或更新以下文件：

- `src/dotnet/Vulgata.Core/Entities/` 下新增平台级上下文实体或配置聚合
- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/` 下新增对应 EF 配置
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`（新增迁移）

实现要求：

- 全局上下文必须有明确持久化落点，不能只存在于 `appsettings`、静态单例或 Razor 组件私有状态。
- 该模型只承载纯文本与必要元数据，不提前承载 LLM 提供商、扫描参数等无关设置。
- 保持 EF Core `IEntityTypeConfiguration<T>` 模式，不使用数据注解替代配置。

### 2. 提炼统一的上下文解析与排队服务

新增或更新以下文件：

- `src/dotnet/Vulgata.Core/DomainServices/` 下新增上下文读取/保存抽象
- `src/dotnet/Vulgata.Web/Data/` 或等效应用协调层新增上下文协调器
- 如有必要，在 `src/dotnet/Vulgata.Shared/` 新增上下文 DTO 与请求模型

实现要求：

- 单一入口负责：读取某层级上下文、判断是否需要排队、返回当前生效值与待生效值、组合代理上下文。
- 组合顺序必须是固定的 `全局 -> 系统 -> 仓库`。
- 独立仓库不得尝试读取不存在的系统层。
- 不要把组合逻辑散落在页面、API 端点或后续代理调用方中。

### 3. 为 FR-14.4 建立最小可演进的扫描状态边界

新增或更新以下文件：

- `src/dotnet/Vulgata.Core/DomainServices/` 或 `src/dotnet/Vulgata.Web/Data/` 下新增扫描状态查询接口
- 测试替身与假实现所在文件

实现要求：

- 通过接口判断“某仓库或某作用域是否存在进行中的扫描”，而不是直接读取还不存在的数据表。
- 全局上下文更新需要能够判断其影响范围内是否有任一扫描进行中。
- 系统上下文更新需要能够判断该系统下任一相关仓库是否在扫描中。
- 仓库上下文更新只需判断目标仓库。
- 若当前迭代无法接入真实扫描状态，至少要设计出后续 Epic 5 可以替换的抽象，并用测试驱动其行为。

### 4. API 与权限设计

新增或更新以下文件：

- `src/dotnet/Vulgata.Web/Program.cs`
- `src/dotnet/Vulgata.Shared/` 下新增上下文请求/响应模型与验证器

建议路由：

- `GET /api/settings/global-context`
- `PUT /api/settings/global-context`
- `GET /api/systems/{systemId}/context`
- `PUT /api/systems/{systemId}/context`
- `GET /api/repositories/{repositoryId}/context`
- `PUT /api/repositories/{repositoryId}/context`

实现要求：

- 全局路由要求 `AdministratorOnly`。
- 系统路由复用现有系统可见性/所有权校验。
- 仓库路由复用现有仓库管理可见性，包括独立仓库。
- 普通 `User` 的读取若通过 UI 暴露，只能返回只读展示数据；写入接口必须拒绝。
- 错误返回继续使用 `ProblemDetails` / `ValidationProblem`，所有文案为中文。

### 5. 管理后台 UI 演进

新增或更新以下文件：

- `src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor`
- `src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor`
- 如需要拆分组件，可新增 `GlobalContextSettings.razor`、`SystemContextEditor.razor`、`RepositoryContextEditor.razor` 等局部组件

实现要求：

- 全局上下文入口放在现有 `设置` 页。
- 系统与仓库上下文的编辑/展示位置应与当前系统管理和仓库管理流保持邻近，不要新增平行页面迷宫。
- 普通 `User` 的场景应展示只读文本，不显示编辑按钮。
- 文本框、按钮、提示消息、排队说明统一为简体中文。
- 输入按纯文本显示和回显，不渲染 Markdown。

### 6. 测试覆盖

新增或更新以下文件：

- `tests/Vulgata.Tests/` 下新增 `UserSuppliedContextTests.cs`，或在现有管理测试中增量扩展
- 如需要复用已有夹具，可参考 `RepositoryManagementTests.cs`

测试要求：

- 覆盖管理员全局上下文保存/读取。
- 覆盖被授权 `SystemOwner` 的系统级与仓库级上下文写入。
- 覆盖未授权 `SystemOwner` 与普通 `User` 的拒绝路径。
- 覆盖独立仓库上下文写入与读取。
- 覆盖上下文组合顺序。
- 覆盖“扫描中排队”与“无扫描立即生效”。
- 覆盖中文提示 `上下文修改将在当前扫描完成后生效`。

## 校验规则

- 全局、系统、仓库上下文都允许为空；空白值保存时应归一化为 `null` 或空状态，而不是保留无意义空格。
- 上下文仅按纯文本处理。
- 不接受 Markdown、HTML、附件、富文本、结构化配置。
- 组合上下文时必须保持固定顺序，不做去重、重排或智能摘要。
- 若存在待应用上下文，必须可被读取和显示，以便 UI 提示当前状态。
- 所有标签、按钮、错误消息、提示文案均为简体中文。

## 测试场景

1. `Administrator` 在 `管理后台 -> 设置` 中保存全局上下文后，重新打开页面仍可看到已持久化内容。
2. 被授权 `SystemOwner` 可以保存所属系统的 `系统上下文`，未授权系统返回拒绝或不可见。
3. 具备 `ManagementAccess` 的用户可以保存独立仓库的 `仓库上下文`。
4. 普通 `User` 只能看到只读上下文展示，不看到保存按钮，直接写 API 被拒绝。
5. 读取系统内仓库有效上下文时，结果顺序为 `全局 -> 系统 -> 仓库`。
6. 读取独立仓库有效上下文时，结果顺序为 `全局 -> 仓库`。
7. 当相关扫描处于进行中时，保存后返回排队状态并显示 `上下文修改将在当前扫描完成后生效`。
8. 当无扫描运行时，保存后立即生效，后续读取返回最新文本。

## 开发说明

### 这是一个“把已有上下文字段产品化”的故事

- `System.Context` 与 `Repository.Context` 已经存在，因此系统级和仓库级的重点不是建模本身，而是把这些字段纳入明确的权限、展示、读取和组合流程。
- 真正的新建模重点在于全局上下文，以及“待应用上下文/排队”这一最小工作流。

### 不要把 FR-14.4 简化成前端提示词

- 用户要求的是“存储并显示”，且 Epic 明确要求扫描进行中时变更排队。
- 因此不能只在 UI 上弹出一条消息然后立刻覆盖数据库字段；需要可测试的状态表达。

### 与 Story 2.3 / 2.4 的关系

- Story 2.3 已把系统内仓库上下文作为创建输入建立起来；Story 2.5 要补齐的是后续维护与展示，而不是重新定义仓库表单。
- Story 2.4 已明确独立仓库对所有具备 `ManagementAccess` 的用户可见；仓库上下文能力必须沿用这一规则。

### 关键风险

- 若把全局上下文硬塞进 `SettingsPage.razor` 的本地状态或 `appsettings`，将无法满足“存储并显示”。
- 若把组合逻辑散落在多个调用方，后续扫描和聊天会出现上下文顺序不一致。
- 若没有最小扫描状态抽象，FR-14.4 最后会退化成不可验证的注释要求。

## Dev Agent Record

### Status

ready-for-dev

### Notes for Implementation

- 基线提交：`18a6c5ea7f5d68e2fbf598bc479a47df6c461aa7` (`Finalize code review for story 2.4`)
- 最近实现序列：`Implement story 2.4 standalone repository creation` → `Finalize code review for story 2.4`，说明 Epic 2 当前代码已接受“独立仓库 + 管理权限共享可见性”模式，Story 2.5 应直接复用。
- 建议优先新增专门的上下文测试文件，避免把 Story 2.5 的行为混进 `RepositoryManagementTests` 导致定位困难。

### File List

- Pending

### Change Log

- 2026-06-29: Story created and moved to `ready-for-dev` with implementation guardrails for global/system/repository context management.