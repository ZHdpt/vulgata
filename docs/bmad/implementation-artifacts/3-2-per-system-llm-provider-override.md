---
story_key: 3-2-per-system-llm-provider-override
title: Story 3.2: Per-System LLM Provider Override
Status: ready-for-dev
Epic: 3
Story: 2
created: 2026-06-29
baseline_commit: d0a9ee239272d984b23eccd8c2c031a4fd508e5f
depends_on:
  - 1-5-role-seeding-and-authorization-policies
  - 1-6-administrator-role-assignment
  - 2-1-system-crud-admin
  - 2-3-repository-management
  - 3-1-llm-provider-configuration-admin
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/implementation-artifacts/3-1-llm-provider-configuration-admin.md
  - src/dotnet/Vulgata.Core/Entities/System.cs
  - src/dotnet/Vulgata.Core/Entities/LlmProvider.cs
  - src/dotnet/Vulgata.Core/Entities/AgentType.cs
  - src/dotnet/Vulgata.Core/DomainServices/ISystemRepository.cs
  - src/dotnet/Vulgata.Core/DomainServices/ILlmProviderRepository.cs
  - src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs
  - src/dotnet/Vulgata.Infrastructure/Data/SystemRepository.cs
  - src/dotnet/Vulgata.Infrastructure/Data/Configurations/SystemConfiguration.cs
  - src/dotnet/Vulgata.Infrastructure/Data/Configurations/LlmProviderConfiguration.cs
  - src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor
  - src/dotnet/Vulgata.Web/Components/Pages/Management/LlmProviderManagementPage.razor
  - src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs
  - src/dotnet/Vulgata.Web/Data/ManagementAccessRequirement.cs
  - src/dotnet/Vulgata.Web/Program.cs
  - tests/Vulgata.Tests/LlmProviderConfigTests.cs
  - tests/Vulgata.Tests/RepositoryManagementTests.cs
---

# Story 3.2: Per-System LLM Provider Override

## 用户故事

作为一名 `System Owner` 或 `Administrator`，
我希望在 `管理后台 -> 系统管理` 的系统详情区域，为 `编排代理`、`工作代理`、`对话代理` 分别设置系统级 LLM Provider 覆盖，
从而让同一个系统在不同代理角色上覆盖全局默认 Provider，而不影响其他系统的默认配置。

## 目标边界

- 本故事在 Story 3.1 已完成的全局 LLM Provider 能力之上扩展，不重复实现全局 Provider 的增删改查。
- 本故事新增 `SystemLlmProviderOverride` 领域模型，用于把 `System`、`AgentType` 与某个已存在的 `LlmProvider` 关联起来。
- 本故事的 UI 必须位于现有 `管理后台 -> 系统管理` 页面中的系统详情区域，而不是新增一个偏离 IA 的独立设置页。
- 本故事允许同一系统同时存在多条 override，但每个 `AgentType` 最多一条，因此 `(SystemId, AgentType)` 必须唯一。
- 本故事只实现“系统级覆盖配置”和“读取有效 Provider 决策入口”；不要求在扫描/聊天代理内真正切换到该覆盖值。
- 本故事必须保留 Story 3.1 的全局 LLM Provider 管理页为管理员专属入口；系统所有者只能在自己可见的系统详情中选择现有 Provider，不能修改全局 Provider 配置。
- 本故事中的“Provider supports the agent type”校验，基于现有 3.1 模型解释为：`LlmProvider.DefaultAgentType` 必须等于被覆盖的 `AgentType`。不要在本故事里重新设计多角色支持矩阵。

## 验收标准

### AC-1: 在系统详情中查看当前 override 状态

**假如** 我以 `Administrator` 或已分配的 `System Owner` 身份进入 `管理后台 -> 系统管理`
**当** 我查看某个可见系统的详情区域
**那么** 我应看到一个 `LLM Provider 覆盖` 区块
**并且** 区块应按 `编排代理`、`工作代理`、`对话代理` 展示当前 override 状态
**并且** 当某角色未配置 override 时，应明确显示“使用全局默认提供商”之类的中文提示

### AC-2: 为某个代理角色创建系统级 override

**假如** 某系统尚未为目标 `AgentType` 配置 override
**当** 我在系统详情中选择一个已存在且角色匹配的 `LlmProvider` 并保存
**那么** 系统应持久化一条新的 `SystemLlmProviderOverride`
**并且** 该记录至少包含 `Id`、`SystemId`、`LlmProviderId`、`AgentType`、`CreatedAt`、`UpdatedAt`
**并且** 保存成功后页面应显示中文成功反馈

### AC-3: 更新或移除已有 override

**假如** 某系统已为某个 `AgentType` 配置 override
**当** 我把该角色切换到另一条角色匹配的 `LlmProvider`
**那么** 系统应更新同一角色的 override 记录而不是创建重复记录
**并且** `UpdatedAt` 应更新

**并且**

**当** 我删除该角色的 override
**那么** 系统应移除对应记录
**并且** 该角色应恢复为“使用全局默认提供商”状态

### AC-4: 一个系统可配置多个 override，但每个角色只能有一条

**假如** 我已经为某系统配置了 `编排代理` 的 override
**当** 我继续为同一系统配置 `工作代理` 或 `对话代理` 的 override
**那么** 系统应允许这些记录并存
**并且** 同一系统下每个 `AgentType` 只能存在一条记录
**并且** 数据库必须通过唯一约束防止 `(SystemId, AgentType)` 重复

### AC-5: 严格校验 Provider 存在性与角色匹配

**假如** 我提交 override 请求
**当** 指向的 `LlmProvider` 不存在、目标系统不可见，或该 Provider 的 `DefaultAgentType` 与请求中的 `AgentType` 不一致
**那么** 系统必须拒绝保存
**并且** 返回简体中文的 `ProblemDetails` / `ValidationProblem`
**并且** 不得写入无效 override

### AC-6: 权限边界

**假如** 我是 `Administrator`
**当** 我访问任意系统的 override 区块和接口
**那么** 我可以查看并维护所有系统的 override

**并且**

**假如** 我是某系统的 `System Owner`
**当** 我访问自己被分配的系统
**那么** 我可以查看并维护该系统的 override
**并且** 不得访问其他不可见系统的 override

**并且**

**假如** 我不是 `Administrator` 且也不是该系统所有者
**当** 我尝试访问页面或调用相关接口
**那么** 系统必须拒绝访问

### AC-7: 提供稳定的“有效 Provider”读取入口

**假如** 后续扫描或聊天逻辑需要知道某系统某角色的有效 Provider
**当** 应用服务按 `SystemId + AgentType` 查询
**那么** 应优先返回系统级 override
**并且** 如果不存在 override，则回退到 Story 3.1 中该角色的全局默认 Provider
**并且** 该读取能力应以独立服务/协调器形式暴露，避免未来代理代码直接拼接查询逻辑

## Tasks / Subtasks

- [ ] Task 1: 建立系统级 override 领域模型 (AC-2, AC-3, AC-4, AC-5)
  - [ ] 1.1 在 `src/dotnet/Vulgata.Core/Entities/` 新增 `SystemLlmProviderOverride.cs`
  - [ ] 1.2 为实体实现 `Id`、`SystemId`、`LlmProviderId`、`AgentType`、`CreatedAt`、`UpdatedAt`
  - [ ] 1.3 在 `System.cs` 中新增 overrides 集合导航属性
  - [ ] 1.4 在 `LlmProvider.cs` 中新增反向导航属性，便于 EF Core 映射
  - [ ] 1.5 将“同角色更新而非重复新增”的行为边界固定到服务层或聚合辅助方法中

- [ ] Task 2: 建立持久化映射、仓储与迁移 (AC-2, AC-4, AC-5)
  - [ ] 2.1 在 `VulgataDbContext` 注册 `DbSet<SystemLlmProviderOverride>`
  - [ ] 2.2 在 `src/dotnet/Vulgata.Infrastructure/Data/Configurations/` 新增 `SystemLlmProviderOverrideConfiguration.cs`
  - [ ] 2.3 配置外键到 `Systems` 与 `LlmProviders`，删除行为使用 `Restrict`
  - [ ] 2.4 配置唯一索引 `(SystemId, AgentType)`
  - [ ] 2.5 在 `src/dotnet/Vulgata.Core/DomainServices/` 新增 `ISystemLlmProviderOverrideRepository`
  - [ ] 2.6 在 `src/dotnet/Vulgata.Infrastructure/Data/` 新增 `SystemLlmProviderOverrideRepository.cs`
  - [ ] 2.7 新增 EF Core migration 与 snapshot 更新

- [ ] Task 3: 建立 override 应用协调层与有效 Provider 读取服务 (AC-3, AC-5, AC-7)
  - [ ] 3.1 在 `src/dotnet/Vulgata.Web/Data/` 新增 `ISystemLlmProviderOverrideCoordinator` 与实现
  - [ ] 3.2 协调器必须复用 `ISystemRepository.GetVisibleByIdAsync(...)` 的可见性判断
  - [ ] 3.3 协调器必须校验 `LlmProvider` 是否存在，且 `DefaultAgentType == requested AgentType`
  - [ ] 3.4 提供 `ListAsync(systemId, userId, isAdministrator)` 供 UI 展示当前 override
  - [ ] 3.5 提供 `UpsertAsync(...)` 与 `DeleteAsync(...)` 供页面和 API 调用
  - [ ] 3.6 提供 `GetEffectiveProviderAsync(systemId, agentType, ...)` 或等效服务，先查 override，再回退到全局默认 Provider

- [ ] Task 4: 建立 DTO 与校验模型 (AC-1, AC-2, AC-5)
  - [ ] 4.1 在 `src/dotnet/Vulgata.Shared/` 新增 system override 摘要/详情 DTO
  - [ ] 4.2 新增用于创建/更新 override 的请求模型
  - [ ] 4.3 在 `src/dotnet/Vulgata.Web/Validators/` 新增中文校验器
  - [ ] 4.4 校验 `AgentType` 必须是 `Orchestrator`、`Worker`、`Chat` 之一
  - [ ] 4.5 校验 `SystemId`、`LlmProviderId` 为有效 `Guid`，并把“角色不匹配”放在协调器或业务校验中处理

- [ ] Task 5: 暴露系统级 Minimal API 端点 (AC-1, AC-2, AC-3, AC-5, AC-6)
  - [ ] 5.1 在 `Program.cs` 注册新仓储、协调器、验证器
  - [ ] 5.2 新增 `GET /api/systems/{systemId:guid}/llm-provider-overrides`
  - [ ] 5.3 新增 `POST /api/systems/{systemId:guid}/llm-provider-overrides`
  - [ ] 5.4 新增 `PUT /api/systems/{systemId:guid}/llm-provider-overrides/{overrideId:guid}`
  - [ ] 5.5 新增 `DELETE /api/systems/{systemId:guid}/llm-provider-overrides/{overrideId:guid}`
  - [ ] 5.6 新增只读 provider 目录接口，供系统所有者选择候选 Provider，例如 `GET /api/systems/{systemId:guid}/llm-provider-overrides/providers`
  - [ ] 5.7 所有系统级端点必须使用 `ManagementAccess` + `GetVisibleByIdAsync(...)` 组合，而不是放宽全局 Provider 管理接口的管理员权限

- [ ] Task 6: 在系统详情区域实现 override UI (AC-1, AC-2, AC-3, AC-4, AC-6)
  - [ ] 6.1 更新 `DashboardPage.razor`，在每个系统详情区域增加 `LLM Provider 覆盖` 区块
  - [ ] 6.2 推荐提取独立组件，例如 `SystemLlmProviderOverridesPanel.razor`，避免继续膨胀 `DashboardPage.razor`
  - [ ] 6.3 UI 至少展示角色名称、当前 override Provider、全局回退状态、编辑/删除入口
  - [ ] 6.4 角色标签必须为简体中文：`编排代理`、`工作代理`、`对话代理`
  - [ ] 6.5 当没有 override 时，明确展示“使用全局默认提供商”
  - [ ] 6.6 仅对当前用户可见系统渲染可交互入口

- [ ] Task 7: 延续并收敛 Story 3.1 的全局 Provider 能力 (AC-5, AC-7)
  - [ ] 7.1 保持 `LlmProviderManagementPage.razor` 仍为管理员专属，不把其改造成系统所有者入口
  - [ ] 7.2 复用已有 `LlmProviderSummaryDto` 或扩展出适合系统详情使用的只读 DTO
  - [ ] 7.3 明确 Story 3.2 的“角色支持”来自现有 `DefaultAgentType`，不要扩展 `LlmProvider` 为“支持多个默认角色”
  - [ ] 7.4 为未来代理消费保留统一选择面，而不是让代理直接查询数据库表

- [ ] Task 8: 增加 Story 3.2 测试覆盖 (AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7)
  - [ ] 8.1 覆盖管理员可为任意系统创建、更新、删除 override
  - [ ] 8.2 覆盖系统所有者只能管理自己被分配系统的 override
  - [ ] 8.3 覆盖未分配系统所有者或普通用户访问被拒绝
  - [ ] 8.4 覆盖 `(SystemId, AgentType)` 唯一性，防止重复记录
  - [ ] 8.5 覆盖不存在的 `LlmProviderId` 被拒绝
  - [ ] 8.6 覆盖 `LlmProvider.DefaultAgentType` 与 override `AgentType` 不匹配时返回中文错误
  - [ ] 8.7 覆盖 `GetEffectiveProviderAsync(...)` 优先取 override，否则回退到全局默认 Provider
  - [ ] 8.8 覆盖管理页面 HTML 中出现 `LLM Provider 覆盖` 与各代理角色中文标签

- [ ] Task 9: 验证与收尾
  - [ ] 9.1 运行 Story 3.2 相关集成测试
  - [ ] 9.2 运行完整 `dotnet build`
  - [ ] 9.3 运行完整 `dotnet test`
  - [ ] 9.4 更新故事文件的 Dev Agent Record、File List、Change Log 与最终状态

## 功能需求提炼

- FR-3.3: `System Owners may override the global default LLM Provider for each agent role within their System.`
- 每个系统可对 `编排代理`、`工作代理`、`对话代理` 分别设置 override。
- override 实体必须是 `System` 与 `LlmProvider` 之间的 join entity，而不是把 `AgentType -> ProviderId` 直接塞进 `System` 表。
- UI 必须位于 `系统管理` 的系统详情区域，不应绕回全局设置页。
- 只有 `Administrator` 与对该系统有可见性的 `System Owner` 可以管理 override。
- 需要稳定的“有效 Provider 决策读取”能力，为后续扫描/聊天故事复用。

## 领域模型详情

### 新增实体：SystemLlmProviderOverride

建议新增文件：

- `src/dotnet/Vulgata.Core/Entities/SystemLlmProviderOverride.cs`

实体字段必须至少包括：

- `Id: Guid`
- `SystemId: Guid`
- `LlmProviderId: Guid`
- `AgentType: AgentType`
- `CreatedAt: DateTimeOffset`
- `UpdatedAt: DateTimeOffset`

导航属性：

- `System: System`
- `LlmProvider: LlmProvider`

### 现有实体调整

- `System.cs`
  - 增加 `IReadOnlyCollection<SystemLlmProviderOverride>` 暴露
  - 保持现有 `Repositories`、`OwnerAssignments` 行为不回退

- `LlmProvider.cs`
  - 增加与 override 的反向导航属性
  - 不改变 Story 3.1 中 `DefaultAgentType` 的语义

### 唯一约束

- 数据库必须强制 `(SystemId, AgentType)` 唯一
- 这表示“一个系统一个角色只有一个覆盖项”，但同一 Provider 可以被多个系统和多个角色引用，只要它们满足角色匹配规则

## 业务规则与校验

- `SystemId` 必须指向当前用户可见的系统；不可见系统按未授权或未找到处理。
- `LlmProviderId` 必须存在。
- `AgentType` 必须是现有 `AgentType` 枚举值之一。
- Story 3.2 中“Provider 支持该角色”的判断依据为：`LlmProvider.DefaultAgentType == AgentType`。
- 新建同一 `(SystemId, AgentType)` 时应执行 upsert 语义或明确返回重复错误；无论哪种实现，最终表中都只能有一条记录。
- 删除 override 后，该角色必须恢复到全局默认 Provider 决策路径。
- 所有页面文案、按钮、校验消息、错误消息保持简体中文。

## 技术实现计划

### 1. 持久化与仓储

新增或更新以下文件：

- `src/dotnet/Vulgata.Core/Entities/SystemLlmProviderOverride.cs`
- `src/dotnet/Vulgata.Core/Entities/System.cs`
- `src/dotnet/Vulgata.Core/Entities/LlmProvider.cs`
- `src/dotnet/Vulgata.Core/DomainServices/ISystemLlmProviderOverrideRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/SystemLlmProviderOverrideRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/SystemLlmProviderOverrideConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/` 下新增迁移

实现要求：

- 沿用 `SystemConfiguration.cs`、`RepositoryConfiguration.cs`、`LlmProviderConfiguration.cs` 的 `IEntityTypeConfiguration<T>` 风格。
- 外键删除策略用 `Restrict`，避免误删 `System` 或 `LlmProvider` 时产生隐式级联。
- 仓储查询优先返回 UI 和协调器真正需要的数据，不让 Razor 组件直接堆数据库查询。

### 2. 系统级协调器与选择逻辑

新增或更新以下文件：

- `src/dotnet/Vulgata.Web/Data/SystemLlmProviderOverrideCoordinator.cs`
- 如有必要，可新增只读选择服务，例如 `SystemLlmProviderSelectionService.cs`

实现要求：

- 复用 `ISystemRepository.GetVisibleByIdAsync(...)` 的系统可见性规则。
- 复用 Story 3.1 的 `ILlmProviderRepository` 查询全局 Provider 列表与角色默认 Provider。
- 不要把“有效 Provider 决策”散落在 `Program.cs` 或 Razor 组件里。
- 返回结果模式保持与现有 `RepositoryManagementCoordinator`、`LlmProviderManagementCoordinator` 一致。

### 3. API 设计

建议路由：

- `GET /api/systems/{systemId:guid}/llm-provider-overrides`
- `POST /api/systems/{systemId:guid}/llm-provider-overrides`
- `PUT /api/systems/{systemId:guid}/llm-provider-overrides/{overrideId:guid}`
- `DELETE /api/systems/{systemId:guid}/llm-provider-overrides/{overrideId:guid}`
- `GET /api/systems/{systemId:guid}/llm-provider-overrides/providers`

实现要求：

- 全部路由保持 `/api/` 前缀与 plural noun 风格。
- 不要通过放宽 `/api/llm-providers` 的权限来让系统所有者读取候选 Provider；系统级候选查询应走新的可见系统路由。
- `Program.cs` 中的授权与错误返回应沿用现有 `ManagementAccess`、`AdministratorOnly`、`ProblemDetails`、`ValidationProblem` 模式。

### 4. UI 设计落点

更新以下文件：

- `src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor`

建议新增：

- `src/dotnet/Vulgata.Web/Components/Pages/Management/SystemLlmProviderOverridesPanel.razor`

实现要求：

- override 区块应紧贴系统详情上下文，而不是隐藏到 `设置` 页面。
- 尽量抽成子组件，避免 `DashboardPage.razor` 继续承担系统 CRUD、仓库 CRUD、所有者管理之外的第四类复杂交互。
- 视觉风格沿用当前 Fluent UI Blazor 与简体中文文案。

## 测试场景

- `Administrator_CanCreateUpdateAndDeleteSystemLlmProviderOverride`
- `SystemOwner_CanManageOverrides_ForAssignedSystemOnly`
- `SystemOwner_CannotManageOverrides_ForUnassignedSystem`
- `CreateOverride_WithUnknownProvider_ReturnsChineseProblem`
- `CreateOverride_WithMismatchedAgentType_ReturnsChineseValidationError`
- `CreateOverride_ForSameSystemAndAgentType_EnforcesUniqueness`
- `GetEffectiveProvider_WhenOverrideExists_ReturnsOverride`
- `GetEffectiveProvider_WhenOverrideMissing_FallsBackToGlobalDefault`
- `DashboardPage_ShowsOverrideSection_ForVisibleSystems`

建议测试文件：

- `tests/Vulgata.Tests/SystemLlmProviderOverrideTests.cs`

测试风格应延续：

- `tests/Vulgata.Tests/LlmProviderConfigTests.cs`
- `tests/Vulgata.Tests/RepositoryManagementTests.cs`

特别注意：

- 现有测试大量使用集成测试 + HTML 文案断言，应继续保持。
- 如果测试基座对 SQLite 手工建表有依赖，需要同步扩展 `SystemLlmProviderOverrides` 表与唯一索引。
- 断言必须覆盖中文文案、权限边界、唯一性和角色匹配规则。

## Dev Notes

- Story 3.1 已经把全局 Provider 配置独立成完整能力；Story 3.2 不要绕开那套实现另起一套“系统内 Provider 配置”。
- `DashboardPage.razor` 当前已经承担系统与仓库管理，继续内联太多 override 逻辑会让页面失控；优先拆出子组件或协调器。
- 系统所有者当前有 `ManagementAccess`，但不是管理员；因此需要新的系统级候选 Provider 只读接口，不能直接复用管理员专属全局管理端点。
- 不要把“Provider supports agent type”理解成 `SupportedApiTypes`。该字段描述 API 形态，不是代理角色匹配。当前故事按 `DefaultAgentType` 匹配角色。
- 不要修改 `LlmProviderManagementPage.razor` 的授权边界，把全局配置暴露给系统所有者。
- 不要让 Razor 页面直接拼 EF 查询完成 override CRUD；沿用 coordinator + repository 模式。
- 如果需要为未来代理执行打基础，应把“有效 Provider 选择”封装成稳定服务，而不是只为 UI 做一层一次性查询。

## 需遵循的架构与代码模式

- 复用 `ISystemRepository.GetVisibleByIdAsync(...)` 与 `SystemRepository` 的系统可见性模式。
- 复用 `RepositoryManagementCoordinator` 的 mutation result 组织方式与页面集成方式。
- 复用 Story 3.1 中 `LlmProvider` 的现有聚合、仓储、DTO 与最小 API 风格。
- 复用 `ManagementAccessRequirement` 作为“管理员或系统所有者”入口，避免重新发明授权模型。
- 复用 `DashboardPage.razor` 当前的系统详情信息架构，不新增偏离 UX 规划的 route。
- 复用现有中文 `ProblemDetails` / `ValidationProblem` 风格与测试断言方式。