---
story_key: 2-4-standalone-repository-creation
title: Story 2.4: Standalone Repository Creation
Status: in-progress
Epic: 2
Story: 4
created: 2026-06-29
baseline_commit: ecb045680cf6d4fad49dd708831e9a3c81e19a1c
depends_on:
  - 2-1-system-crud-admin
  - 2-2-grant-system-ownership
  - 2-3-repository-management
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/implementation-artifacts/2-3-repository-management.md
  - src/dotnet/Vulgata.Core/Entities/System.cs
  - src/dotnet/Vulgata.Core/Entities/Repository.cs
  - src/dotnet/Vulgata.Infrastructure/Data/RepositoryRepository.cs
  - src/dotnet/Vulgata.Infrastructure/Data/Configurations/RepositoryConfiguration.cs
  - src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs
  - src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor
  - src/dotnet/Vulgata.Web/Program.cs
  - tests/Vulgata.Tests/RepositoryManagementTests.cs
---

# Story 2.4: Standalone Repository Creation

## 用户故事

作为一名 **SystemOwner**，
我希望创建不归属于任何系统的独立仓库，
从而让团队能够先登记组织自有的共享库，并在后续故事中把它们作为跨系统依赖源使用。

## 目标边界

- 本故事只覆盖“独立仓库”的展示与创建，不覆盖按需扫描、锁机制、常见库白名单或未知库推断；那些属于后续 Epic 6。
- 本故事继续复用现有 `Repository` 实体；独立仓库的表达方式是 `SystemId = null`，而不是引入第二套仓库实体或伪造“共享系统”。
- 本故事不重做 Story 2.3 已完成的“系统内仓库新增/删除”流程，只把“仓库必须挂在某个系统下”的实现假设放宽为“可以挂在系统下，也可以独立存在”。
- V1 不引入“独立仓库所有者”新模型。为避免额外授权表，本故事将独立仓库视为所有已具备 `ManagementAccess` 的用户都可见的共享管理资源；普通 `User` 仍不可见。

## 验收标准

### AC-1: 管理后台展示独立仓库区域

**Given** 我以 `SystemOwner` 或 `Administrator` 身份进入 `管理后台 -> 系统管理`  
**When** 页面加载完成  
**Then** 我应看到一个独立于系统列表之外的 `独立仓库` 区域  
**And** 该区域使用 Fluent UI 风格展示仓库列表  
**And** 至少显示 `仓库名称`、`扫描状态`、`最近扫描时间`、`文档数量` 与 `查看` 列  
**And** 提供 `+ 新建独立仓库` 操作

### AC-2: 创建独立仓库

**Given** 我打开 `+ 新建独立仓库` 对话框  
**When** 我输入必填的仓库名称、必填的 Git 远程地址，以及可选的描述和纯文本上下文并提交  
**Then** 系统应先执行与 Story 2.3 相同的 Git URL 可达性校验  
**And** 校验成功后创建仓库记录  
**And** 新记录的 `SystemId` 应为 `null`  
**And** 新仓库应立即出现在 `独立仓库` 区域，而不是出现在任何系统详情下

### AC-3: 不可达 Git URL 阻止创建

**Given** 我输入了一个无法访问的 Git URL  
**When** 独立仓库创建流程执行可达性校验  
**Then** 我应看到中文错误提示 `Git URL 不可达：{details}`  
**And** 独立仓库记录不得被创建

### AC-4: 独立仓库名称在独立作用域内唯一

**Given** 已存在一个 `SystemId = null` 的独立仓库  
**When** 我再次创建同名的独立仓库  
**Then** 请求应被拒绝  
**And** UI 或 API 应返回中文提示，说明独立仓库名称已存在  
**And** 系统内仓库的同名规则仍保持原有“按系统作用域唯一”的行为，不因本故事回退

### AC-5: SystemOwner 与 Administrator 都能查看独立仓库

**Given** 我已经具备 `ManagementAccess`  
**When** 我进入 `管理后台 -> 系统管理` 或调用独立仓库查询接口  
**Then** 我应能看到所有独立仓库  
**And** 不需要额外的系统授权映射  
**And** 该可见性决策必须在故事实现中显式表达，而不是依赖偶然的空外键行为

### AC-6: 普通用户不可见

**Given** 我是普通 `User`  
**When** 我查看应用导航或尝试调用独立仓库接口  
**Then** `管理后台` 导航入口仍不可见  
**And** 服务器端请求必须被拒绝

### AC-7: 不回归 Story 2.3 的系统内仓库管理

**Given** 系统内仓库管理已经在 Story 2.3 完成  
**When** 本故事引入独立仓库能力  
**Then** 现有 `/api/systems/{systemId}/repositories` 行为必须保持不变  
**And** 已授权 `SystemOwner` 对所属系统仓库的创建/查看能力不得被破坏  
**And** 系统详情中的仓库列表不得混入独立仓库

## Tasks / Subtasks

- [ ] Task 1: 放宽 `Repository` 聚合约束以支持独立仓库 (AC-2, AC-7)
  - [ ] 1.1 将 `Repository.SystemId` 与 `Repository.System` 调整为可空，并保留现有系统内仓库创建路径
  - [ ] 1.2 为独立仓库增加明确的受控工厂入口，例如 `CreateStandalone(...)`
  - [ ] 1.3 保持 `System.AddRepository(...)` 继续通过聚合根创建系统内仓库，避免回归 Story 2.3

- [ ] Task 2: 扩展仓储与唯一性策略 (AC-2, AC-4, AC-5, AC-7)
  - [ ] 2.1 为仓储增加独立仓库的列表、详情与重名检查方法
  - [ ] 2.2 在 EF 配置中同时保证系统内仓库按系统唯一、独立仓库按空作用域唯一
  - [ ] 2.3 为域数据库补充迁移并同步测试 SQLite 建表/索引逻辑

- [ ] Task 3: 通过协调器与 API 统一独立仓库管理能力 (AC-2, AC-3, AC-4, AC-5, AC-6, AC-7)
  - [ ] 3.1 复用现有 `CreateRepositoryRequest`、DTO 与 Git 可达性校验服务
  - [ ] 3.2 在协调器中显式表达“所有具备 `ManagementAccess` 的用户都能看到独立仓库”
  - [ ] 3.3 新增 `GET /api/repositories/standalone` 与 `POST /api/repositories/standalone`，并保持系统内仓库 API 行为不变

- [ ] Task 4: 增量演进管理后台 UI (AC-1, AC-2, AC-5, AC-6, AC-7)
  - [ ] 4.1 在系统列表之外新增 `独立仓库` 区域与 `+ 新建独立仓库` 操作
  - [ ] 4.2 统一通过协调器处理仓库创建与刷新，避免页面复制 Git 校验/重名逻辑
  - [ ] 4.3 保持所有文案、列标题、错误提示为简体中文，并确保独立仓库不会混入系统详情列表

- [ ] Task 5: 增加 Story 2.4 集成测试覆盖 (AC-2, AC-3, AC-4, AC-5, AC-6, AC-7)
  - [ ] 5.1 覆盖 `SystemOwner` 成功创建独立仓库且数据库 `SystemId` 为 `null`
  - [ ] 5.2 覆盖独立仓库对所有具备 `ManagementAccess` 的用户可见
  - [ ] 5.3 覆盖独立仓库重名被拒绝且返回中文提示
  - [ ] 5.4 覆盖独立仓库 Git URL 不可达仍返回 `Git URL 不可达：...`
  - [ ] 5.5 覆盖普通 `User` 无法访问独立仓库接口
  - [ ] 5.6 覆盖 Story 2.3 的系统内仓库用例不回归

- [ ] Task 6: 验证与收尾
  - [ ] 6.1 运行 Story 2.4 相关集成测试并确认红绿循环完成
  - [ ] 6.2 运行完整构建与测试，确认无回归
  - [ ] 6.3 更新故事文件的 Dev Agent Record、File List、Change Log 与最终状态

## 功能需求提炼

- FR-2.4: System Owners shall create standalone Repositories (not belonging to any System).
- FR-2.5: Git URL reachability validation still applies when the repository is added.
- 独立仓库沿用现有 `Repository` 字段集合：`Name`、`GitUrl`、`Description`、`Context`、扫描占位字段。
- 管理后台必须保持中文 UI，并沿用 Story 2.3 已建立的仓库列表和即时反馈模式。

## 现有实现与必须复用的代码面

### 当前领域模型的真实约束

- `src/dotnet/Vulgata.Core/Entities/Repository.cs`
  - 当前构造函数和 `Create(...)` 工厂都要求非空 `Guid systemId`。
  - 现有实现会在 `systemId == Guid.Empty` 时抛出 `系统标识不能为空。`，这正是 Story 2.4 需要放宽的根约束。
- `src/dotnet/Vulgata.Core/Entities/System.cs`
  - 当前只有 `System.AddRepository(...)` 这一条仓库创建路径。
  - 这对系统内仓库是正确的，但独立仓库不能通过伪造系统来复用它。

### 当前持久化与查询仍是“系统中心”

- `src/dotnet/Vulgata.Infrastructure/Data/RepositoryRepository.cs`
  - 当前公开的方法几乎全部围绕 `systemId`：`ListBySystemAsync`、`GetBySystemAndIdAsync`、`NameExistsAsync(Guid systemId, ...)`。
  - 尚不存在任何 `SystemId is null` 的查询或唯一性检查能力。
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/RepositoryConfiguration.cs`
  - 当前唯一索引为 `(SystemId, NormalizedName)`。
  - 当前外键配置 `HasForeignKey(r => r.SystemId)` 仍隐含“仓库必须属于系统”的数据模型。

### 当前协调器、API、页面都假设仓库挂在系统下

- `src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs`
  - `ListVisibleAsync`、`CreateAsync`、`DeleteAsync` 都以 `systemId` 为入口。
  - Git 校验逻辑已经在这里抽象完成，Story 2.4 应复用这条校验路径，而不是在 Razor 页面里复制 `git ls-remote`。
- `src/dotnet/Vulgata.Web/Program.cs`
  - 当前仓库 API 仅存在于 `/api/systems/{systemId}/repositories` 下。
  - 尚无任何独立仓库路由。
- `src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor`
  - 当前 UI 只在遍历系统时渲染仓库表格与 `+ 新建仓库`。
  - 页面里还保留了一套绕过 API 的内嵌创建逻辑，因此 Story 2.4 应优先把共享能力收敛到统一的数据/服务边界，避免继续复制分支。

### 当前测试基线

- `tests/Vulgata.Tests/RepositoryManagementTests.cs`
  - 已验证系统内仓库创建、不可达 Git URL、认证错误脱敏、授权边界、管理员全局可见性。
  - Story 2.4 应延续这套 `WebApplicationFactory` 集成测试模式，并新增独立仓库专用断言，而不是退回到仅测领域类。

## 技术实现计划

### 1. 放宽 Repository 聚合约束

更新以下文件：

- `src/dotnet/Vulgata.Core/Entities/Repository.cs`
- `src/dotnet/Vulgata.Core/Entities/System.cs`

实现要求：

- 将 `Repository.SystemId` 从必填关系调整为可空关系，以允许 `SystemId = null` 表示独立仓库。
- 保留现有系统内仓库创建路径 `System.AddRepository(...)`，避免回归 Story 2.3。
- 为独立仓库引入明确的受控创建入口，例如 `Repository.CreateStandalone(...)` 或等效工厂，而不是传入 `Guid.Empty` 作为特殊值。
- `Repository.System` 导航属性与相关 EF 配置必须同步调整为可选关系。

### 2. 扩展仓储与唯一性策略

更新以下文件：

- `src/dotnet/Vulgata.Core/DomainServices/IRepositoryRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/RepositoryRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/RepositoryConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`（新增迁移）

实现要求：

- 为仓储补充独立仓库查询能力，例如：
  - `ListStandaloneAsync()`
  - `GetStandaloneByIdAsync()`
  - `StandaloneNameExistsAsync()`
- 继续保留 Story 2.3 的系统内方法，不要把两种场景混成难以理解的布尔分支 API。
- 唯一性约束必须同时支持：
  - 系统内仓库：同一系统下名称唯一
  - 独立仓库：`SystemId = null` 作用域内名称唯一
- 由于 PostgreSQL 与测试环境 SQLite 在 `NULL` 唯一索引语义上存在差异，实现时需要选择对两端都稳定的约束策略，并配套测试验证。

### 3. 提炼独立仓库管理协调器/API

更新以下文件：

- `src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs`
- `src/dotnet/Vulgata.Web/Program.cs`
- `src/dotnet/Vulgata.Shared/Repositories/CreateRepositoryRequest.cs`
- `src/dotnet/Vulgata.Shared/Repositories/RepositorySummaryDto.cs`
- `src/dotnet/Vulgata.Shared/Repositories/RepositoryDetailDto.cs`
- `src/dotnet/Vulgata.Shared/Validators/Repositories/CreateRepositoryRequestValidator.cs`

实现要求：

- 复用现有 `CreateRepositoryRequest`、DTO 与 Git 校验服务，避免创建第二套“独立仓库请求模型”。
- 为独立仓库增加明确路由，建议至少覆盖：
  - `GET /api/repositories/standalone`
  - `POST /api/repositories/standalone`
  - 如实现需要辅助详情刷新，可增加 `GET /api/repositories/standalone/{repositoryId}`
- 接口需要求 `ManagementAccess`；普通 `User` 不可访问。
- 需要在协调器中显式表达“独立仓库对所有具备管理权限的用户可见”的规则，而不是依赖 `GetVisibleByIdAsync` 这类系统授权方法。
- Git 不可达和认证失败继续返回 `ProblemDetails`，消息格式与 Story 2.3 保持一致。

### 4. 管理后台 UI 增量演进

更新以下文件：

- `src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor`
- `src/dotnet/Vulgata.Web/Components/Layout/ManagementLayout.razor`（如需要为独立仓库区域补充导航或布局钩子）

实现要求：

- 在现有系统列表之外新增 `独立仓库` 区域，并保持中文标签。
- 区域内列表列名与 Story 2.3 已建立的系统内仓库表格一致，减少认知分裂。
- 新建对话框文案使用简体中文，并明确说明“该仓库不属于任何系统”。
- 新建成功后，独立仓库只在独立区域刷新，不应混入任一系统仓库表。
- 如果页面继续保留内嵌创建逻辑，必须避免复制第二套 Git 校验和名称校验规则；优先通过 API/协调器统一处理。

### 5. 测试覆盖

新增或更新以下文件：

- `tests/Vulgata.Tests/RepositoryManagementTests.cs`

测试要求：

- 覆盖 `SystemOwner` 成功创建独立仓库。
- 覆盖创建后数据库记录的 `SystemId` 为 `null`。
- 覆盖独立仓库出现在 `独立仓库` 区域，且不会出现在任何系统仓库表中。
- 覆盖独立仓库重复名称被拒绝，并返回中文错误。
- 覆盖不可达 Git URL 仍返回 `Git URL 不可达：...`。
- 覆盖普通 `User` 无法访问独立仓库接口。
- 覆盖 Story 2.3 的系统内仓库创建用例在改动后仍通过，防止回归。

## 校验规则

- `Name` 必填，且不得为纯空白。
- `GitUrl` 必填，且不得为纯空白。
- `Description` 可空。
- `Context` 可空，按纯文本处理。
- 创建独立仓库前必须执行 Git URL 可达性校验。
- 独立仓库以 `SystemId = null` 表示，不允许通过 `Guid.Empty`、虚拟系统或特殊名称约定模拟。
- 独立仓库名称只需在独立作用域中唯一；系统内仓库仍按系统作用域唯一。
- 所有按钮、提示、错误消息、表头均为简体中文。

## 测试场景

1. `SystemOwner` 在管理后台看到 `独立仓库` 区域和 `+ 新建独立仓库` 按钮。
2. 输入合法名称与可达 Git URL 后，独立仓库创建成功并出现在独立区域。
3. 创建成功后的仓库记录 `SystemId` 为 `null`。
4. 输入不可达 Git URL 时，创建失败并显示 `Git URL 不可达：{details}`。
5. 输入重复独立仓库名称时，系统返回中文唯一性错误。
6. 普通 `User` 访问独立仓库 API 时被拒绝，且仍看不到 `管理后台`。
7. Story 2.3 的系统内仓库接口和页面列表在本故事落地后仍保持正常。

## 开发说明

### 这是一个“去掉系统必选”而不是“再做一套仓库管理”故事

- 目前从领域到 API 的所有路径都把仓库视为系统子资源。Story 2.4 的核心工作是识别并放宽这个假设，而不是复制一套新的仓库模型。
- 用户已明确要求“same repository entity, just `SystemId = null`”。实现必须忠实于这一约束。

### 优先复用 Story 2.3 已完成能力

- 复用现有 `Repository` 字段、Git 校验服务、中文错误格式、Fluent UI 列表样式与测试工厂。
- 不要在 `DashboardPage.razor` 中新增第二套不经 API 的特殊校验逻辑；Story 2.3 已暴露页面内嵌写入逻辑容易漂移，这一故事应尽量收敛分支。

### 关键风险

- `SystemId` 改为可空后，EF 关系、唯一索引和旧查询条件都会受影响；这不是纯 UI 改动。
- SQLite 与 PostgreSQL 对 `NULL` 唯一性处理不同，必须用测试锁定预期行为。
- 若直接把“独立仓库”塞进系统循环或系统授权逻辑，后续 Epic 6 的按需扫描入口会变得更难扩展。

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- Pending

### Completion Notes List

- Pending

### File List

- Pending

### Change Log

- 2026-06-29: Story moved to in-progress and execution checklist initialized for implementation.