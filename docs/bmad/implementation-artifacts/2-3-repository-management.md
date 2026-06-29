---
story_key: 2-3-repository-management
title: Story 2.3: Repository Management
Status: ready-for-dev
Epic: 2
Story: 3
created: 2026-06-29
baseline_commit: 057f6c8eaeef10d3a787c97fa5bc1f2b761553c6
depends_on:
  - 2-1-system-crud-admin
  - 2-2-grant-system-ownership
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/implementation-artifacts/2-1-system-crud-admin.md
  - docs/bmad/implementation-artifacts/2-2-grant-system-ownership.md
  - src/dotnet/Vulgata.Core/Entities/System.cs
  - src/dotnet/Vulgata.Core/Entities/Repository.cs
  - src/dotnet/Vulgata.Core/DomainServices/ISystemRepository.cs
  - src/dotnet/Vulgata.Infrastructure/Data/Configurations/RepositoryConfiguration.cs
  - src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor
---

# Story 2.3: Repository Management

## 用户故事

作为一名**SystemOwner**，
我希望在自己获授权的系统下新增和移除仓库，并在保存前验证 Git 远程地址可达，
从而让 Vulgata 后续能够基于这些仓库执行扫描与文档生成。

## 目标边界

- 本故事只覆盖“挂在某个系统下的仓库”管理。
- 本故事包含仓库新增、仓库删除、Git URL 可达性校验，以及仓库级可选补充上下文的录入。
- 本故事不包含独立仓库创建，那属于 Story 2.4。
- 本故事不包含扫描启动、扫描状态写入、文档浏览或上下文治理工作流，那些在后续故事中补齐。
- 本故事必须延续 Story 2.1 的系统管理入口与 Story 2.2 的授权边界，不得破坏现有系统 CRUD 和系统所有者分配逻辑。

## 验收标准

### AC-1: 系统详情展示仓库列表与新增入口

**假如** 我以 `SystemOwner` 身份进入 `管理后台 -> 系统管理` 并选中一个我被授权的系统  
**当** 系统详情区域加载完成  
**那么** 主内容区应显示该系统下的仓库列表  
**并且** 列表使用 Fluent UI 风格的数据展示模式  
**并且** 列表至少显示 `仓库名称`、`扫描状态`、`最近扫描时间`、`文档数量` 与 `查看` 操作列  
**并且** 页面中应提供 `+ 新建仓库` 操作

### AC-2: 新增仓库并通过 Git 可达性校验

**假如** 我点击 `+ 新建仓库`  
**当** 我输入必填的仓库名称、必填的 Git 远程地址，以及可选的描述和纯文本上下文并提交  
**那么** 系统应先通过 `git ls-remote` 校验该 Git URL 是否可达  
**并且** 校验成功后才创建仓库记录  
**并且** 新仓库应立即出现在系统详情列表和左侧系统树中

### AC-3: 不可达 Git URL 阻止创建

**假如** 我输入了一个无法访问的 Git URL  
**当** 可达性校验执行时  
**那么** 我应看到中文错误提示 `Git URL 不可达：{details}`  
**并且** 仓库记录不得被创建

### AC-4: 认证要求错误不得泄露敏感信息

**假如** 我输入的 Git URL 需要认证  
**当** 可达性校验失败  
**那么** 我应看到明确说明需要认证的中文提示  
**并且** 错误消息中不得回显密码、令牌、内嵌凭据或完整敏感命令行参数

### AC-5: 删除系统内仓库

**假如** 某系统下已经存在仓库  
**当** 我对该仓库执行 `删除` 并确认  
**那么** 该仓库应从系统中移除  
**并且** UI 应立即刷新列表与树节点  
**并且** 仓库删除流程不得把未来的文档与扫描归档策略硬编码为级联物理删除

### AC-6: SystemOwner 仅能管理自己获授权系统下的仓库

**假如** 我是只拥有 System A 授权的 `SystemOwner`  
**当** 我查看系统树或直接调用仓库管理接口  
**那么** 我只能看到并操作 System A 下的仓库  
**并且** 我不能新增、查看或删除未授权系统下的仓库

### AC-7: 管理员仍具备全局可见性

**假如** 我是 `Administrator`  
**当** 我进入 `管理后台 -> 系统管理`  
**那么** 我仍可查看所有系统及其仓库  
**并且** 仓库管理实现不得回退或削弱既有管理员全局访问能力

### AC-8: 中文界面与即时反馈

**假如** 我在仓库管理界面执行新增或删除操作  
**当** 系统完成处理  
**那么** 所有标签、占位符、按钮、确认框、验证提示与错误提示都必须是简体中文  
**并且** 成功或失败结果应立即反映到当前管理界面状态

## 功能需求提炼

- FR-2.2: System Owners 向 System 添加 Repository。
- FR-2.3: System Owners 从 System 移除 Repository。
- FR-2.5: Repository 添加时必须验证 Git URL 可达性。
- Repository 领域对象至少包含 `Name`、`Description`、`Git remote URL` 与可选 `Context`。
- 管理后台仓库管理必须沿用 Epic 2 的系统树与系统详情布局，而不是新增一套平行页面。

## 现有实现与必须复用的代码面

### 已存在的领域对象

- `src/dotnet/Vulgata.Core/Entities/Repository.cs`
  - 当前已存在 `Id`、`SystemId`、`Name`、`GitUrl`、`CreatedAt`、`UpdatedAt`、`System`。
  - 目前尚未承载 `Description`、`Context`、规范化名称或任何受控创建/更新方法。
- `src/dotnet/Vulgata.Core/Entities/System.cs`
  - 已通过 `Repositories` 集合维护 `System -> Repository` 关系。
  - Story 2.3 应继续使用这一聚合关系，不要引入旁路关联表或重复归属字段。

### 已存在的持久化配置

- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
  - 已包含 `DbSet<Repository>`，说明仓库已进入域模型与数据库上下文。
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/RepositoryConfiguration.cs`
  - 当前仅配置 `Name`、`GitUrl` 与 `SystemId` 外键关系。
  - Story 2.3 需要扩展此配置以支持新增字段与约束，但不得改用数据注解。

### 已存在的权限与管理后台基础

- `src/dotnet/Vulgata.Core/DomainServices/ISystemRepository.cs`
  - 当前只覆盖系统列表、系统可见性、删除依赖检查与所有者分配逻辑。
  - 尚未提供专门的仓库查询/写入抽象。
- `src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor`
  - 当前仍以系统列表表格为主，已包含系统 CRUD 与 `管理所有者` 入口。
  - Story 2.3 的实现需要把页面推进到“系统详情 + 仓库列表”的形态，同时保留 Story 2.1/2.2 已完成能力。
- `src/dotnet/Vulgata.Web/Data/SystemOwnershipCoordinator.cs`
  - 已建立集中式授权协调模式。
  - Story 2.3 若需要引入仓库管理协调器，应遵循同样的“集中协调 + 仓储边界 + 中文错误映射”思路。

## 技术实现计划

### 1. 领域模型扩展

更新以下文件：

- `src/dotnet/Vulgata.Core/Entities/Repository.cs`
- `src/dotnet/Vulgata.Core/Entities/System.cs`

实现要求：

- 为 `Repository` 增加 `Description`、`Context` 与必要的规范化字段或受控更新逻辑。
- 不要在 Razor 页面或 API 端点中直接拼装不完整的 `Repository` 实体。
- 保持 `System` 聚合根对仓库归属关系的主导地位，避免仓库脱离系统的新增路径混入本故事。
- 若需要删除仓库，优先通过聚合或仓储方法表达“从系统移除仓库”语义，而不是页面层直接操作 DbContext。

### 2. 仓储与 Git 校验抽象

新增或更新以下文件：

- `src/dotnet/Vulgata.Core/DomainServices/IRepositoryRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Repositories/RepositoryRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/RepositoryConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
- `src/dotnet/Vulgata.Infrastructure/Git/GitRemoteValidationService.cs`
- `src/dotnet/Vulgata.Infrastructure/Git/IGitRemoteValidationService.cs`

实现要求：

- 引入专用仓库仓储抽象，不要把仓库 CRUD 继续堆叠到 `ISystemRepository` 上。
- `IGitRemoteValidationService` 负责执行 `git ls-remote` 或等效探测，并把原始命令失败映射为安全、可本地化的结果类型。
- 认证失败与网络失败必须可区分，但任何日志和用户错误都不得泄露 URL 中的嵌入凭据。
- 如果当前项目还没有统一的 shell 执行封装，本故事可以先实现最小可测的 Git 探测服务，但应保持接口可替换。

### 3. Shared 合约与验证器

新增或更新以下文件：

- `src/dotnet/Vulgata.Shared/Repositories/RepositorySummaryDto.cs`
- `src/dotnet/Vulgata.Shared/Repositories/RepositoryDetailDto.cs`
- `src/dotnet/Vulgata.Shared/Repositories/CreateRepositoryRequest.cs`
- `src/dotnet/Vulgata.Shared/Repositories/DeleteRepositoryRequest.cs`
- `src/dotnet/Vulgata.Shared/Validators/Repositories/CreateRepositoryRequestValidator.cs`

实现要求：

- 所有输入校验继续使用 FluentValidation。
- 名称与 Git URL 为必填项；描述和上下文为可选纯文本。
- 验证器负责基础输入规则，Git 可达性属于应用/基础设施协作校验，不应硬塞进纯静态字段规则。
- 所有面向 UI 的提示消息使用简体中文。

### 4. Web/API 与页面重构

新增或更新以下文件：

- `src/dotnet/Vulgata.Web/Program.cs`
- `src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor`
- `src/dotnet/Vulgata.Web/Components/Pages/Management/CreateRepositoryDialog.razor`
- `src/dotnet/Vulgata.Web/Components/Pages/Management/DeleteRepositoryDialog.razor`
- `src/dotnet/Vulgata.Web.ViewModels/Management/DashboardViewModel.cs` 或等效管理页 ViewModel

实现要求：

- 最小 API 路由使用复数名词，建议至少覆盖：
  - `GET /api/systems/{systemId}/repositories`
  - `POST /api/systems/{systemId}/repositories`
  - `DELETE /api/systems/{systemId}/repositories/{repositoryId}`
- 继续使用 `ProblemDetails` 返回错误，不要引入自定义 envelope。
- `SystemOwner` 只能操作自己可见系统下的仓库；`Administrator` 保持全局可见与可操作能力。
- 页面应从“系统列表”演进为“选中系统后显示仓库列表与仓库操作”，但不能丢失现有系统编辑、删除与所有者管理入口。
- `扫描状态`、`最近扫描时间`、`文档数量` 列在扫描功能尚未落地时，可以使用稳定的占位值或派生显示，但列契约与中文文案必须先建立好。

### 5. 测试覆盖

新增或更新以下文件：

- `tests/Vulgata.Tests/RepositoryManagementTests.cs`
- `tests/Vulgata.Tests/SystemCrudTests.cs`（如需补充管理后台布局回归）
- `tests/Vulgata.Tests/GrantSystemOwnershipTests.cs`（如需补充 SystemOwner 授权后仓库可见性联动）

测试要求：

- 覆盖被授权 `SystemOwner` 成功新增仓库。
- 覆盖不可达 Git URL 被拒绝且显示中文错误。
- 覆盖认证要求错误不会泄露密码、令牌或带凭据的完整 URL。
- 覆盖 `SystemOwner` 无法操作未授权系统下的仓库。
- 覆盖管理员仍可查看并操作任意系统下的仓库。
- 覆盖删除仓库后列表与树节点刷新。
- 覆盖页面仍保留 Story 2.1 与 Story 2.2 已有操作入口。

## 校验规则

- `Repository.Name` 必填，且不得为纯空白。
- `Repository.GitUrl` 必填，且不得为纯空白。
- `Description` 可空。
- `Context` 可空，按纯文本处理。
- Git URL 在持久化前必须完成可达性探测。
- 认证失败信息不得暴露敏感数据。
- 只有对目标系统拥有访问权的 `SystemOwner` 或 `Administrator` 能执行仓库写操作。
- 所有标签、验证消息、按钮文案和错误提示均为中文。

## 测试场景

1. 已获授权的 `SystemOwner` 选中某系统后，可以看到仓库列表和 `+ 新建仓库` 按钮。
2. 输入合法名称与可达 Git URL 后，仓库成功创建并立即出现在当前系统详情与树中。
3. 输入不可达 Git URL 时，创建失败并显示 `Git URL 不可达：{details}` 中文错误。
4. 输入需要认证的 Git URL 时，系统提示需要认证，但不泄露敏感凭据。
5. 删除某仓库后，当前系统详情列表和树节点立即刷新。
6. `SystemOwner` 无法通过 UI 或直接 API 管理未授权系统下的仓库。
7. `Administrator` 仍可查看所有系统并执行仓库管理。
8. 现有系统 CRUD 与系统所有者管理入口在仓库管理完成后仍然可用。

## 开发说明

### 来自 Story 2.1 / 2.2 的直接延续

- Story 2.1 已建立中文 Fluent UI 管理后台基础、系统删除约束和系统可见性过滤；Story 2.3 必须在此基础上增量推进，而不是重写管理页。
- Story 2.2 已建立基于 `SystemOwnerAssignment` 的系统授权链路；Story 2.3 必须复用这条链路做仓库级访问控制，避免再造一套系统归属判断。
- 现有 `DashboardPage.razor` 还没有完全达到 architecture.md 期望的“系统树 + 系统详情/仓库详情”形态，因此本故事允许进行受控重构，但必须保留既有行为。

### 实现注意事项

- 当前代码库尚不存在现成的 Git 远程探测服务，本故事应把这部分能力抽象出来，而不是把 `git ls-remote` 直接写死在组件代码里。
- 当前 `Repository` 实体尚未具备 Story 2.3 所需完整字段；字段扩展和配置更新要同步考虑数据库迁移与测试数据构造。
- 扫描相关领域模型尚未进入本故事范围，不要为了满足仓库列表列展示而提前引入完整扫描子系统。
- 删除仓库时请保持未来可扩展性，避免把“删除仓库”实现成对后续文档/扫描数据的不可逆级联删除假设。