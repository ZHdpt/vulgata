---
story_key: 3-3-database-connection-configuration
title: Story 3.3: Database Connection Configuration
Status: ready-for-dev
Epic: 3
Story: 3
created: 2026-06-29
baseline_commit: e2df714a599870542a2172a4a67b5d7bd3ae98c7
depends_on:
  - 1-5-role-seeding-and-authorization-policies
  - 1-6-administrator-role-assignment
  - 2-3-repository-management
  - 2-4-standalone-repository-creation
  - 3-1-llm-provider-configuration-admin
references:
  - docs/bmad/epics.md
  - docs/bmad/implementation-artifacts/3-1-llm-provider-configuration-admin.md
  - docs/bmad/implementation-artifacts/3-2-per-system-llm-provider-override.md
  - src/dotnet/Vulgata.Core/Entities/Repository.cs
  - src/dotnet/Vulgata.Core/Entities/LlmProvider.cs
  - src/dotnet/Vulgata.Web/Data/ApiKeyEncryptionService.cs
  - src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs
  - src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs
  - src/dotnet/Vulgata.Infrastructure/Data/Configurations/RepositoryConfiguration.cs
  - src/dotnet/Vulgata.Shared/Repositories/RepositoryDetailDto.cs
  - src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor
  - src/dotnet/Vulgata.Web/Program.cs
  - tests/Vulgata.Tests/RepositoryManagementTests.cs
  - tests/Vulgata.Tests/LlmProviderConfigTests.cs
---

# Story 3.3: Database Connection Configuration

## 用户故事

作为一名 `System Owner` 或具备仓库可见性的管理用户，
我希望在 `管理后台 -> 系统管理` 的仓库详情中维护数据库连接配置并执行连接测试，
从而让每个 Repository 都能以加密方式保存自己的数据库访问信息，并为后续扫描阶段的数据库 schema / sample data 能力提供可靠连接入口。

## 目标边界

- 本故事为 `Repository` 建立零或一条 `DatabaseConnection` 配置能力；不支持同一仓库保存多套数据库连接。
- 本故事覆盖连接字符串、数据库类型、用户名、密码的录入、更新、删除、读取摘要，以及连接测试入口。
- 本故事要求连接字符串、用户名、密码全部加密后落库，复用 Story 3.1 的 ASP.NET Core Data Protection 模式，但使用数据库连接专属 purpose string。
- 本故事的 UI 必须落在现有 `管理后台 -> 系统管理` 的仓库详情流中，不新增偏离 IA 的全局设置页。
- 本故事必须兼容系统内仓库和独立仓库；权限边界沿用现有仓库可见性规则。
- 本故事只建立“配置与测试”能力，不在本故事内实现扫描代理真实读取 schema/sample data；但接口与模型必须为 FR-4.13 / FR-4.14 预留可复用基础。
- 连接测试只允许执行只读、最小化探活操作，不能产生写入副作用，并且必须符合 NFR-2.4 的非生产/只读约束。

## 验收标准

### AC-1: 在仓库详情中查看数据库连接状态

**假如** 我以具备该仓库访问权限的管理用户身份进入 `管理后台 -> 系统管理`
**当** 我打开某个仓库的详情区域
**那么** 我应看到 `数据库连接` 区块
**并且** 区块至少展示 `数据库类型`、`连接字符串是否已配置`、`用户名是否已配置`、`最后更新时间`
**并且** 页面上应提供 `新增/编辑`、`删除`、`测试连接` 操作入口
**并且** 不得回显连接字符串、用户名、密码明文

### AC-2: 为仓库创建数据库连接配置

**假如** 目标仓库尚未配置数据库连接
**当** 我填写 `连接字符串`、`数据库类型`、可选 `用户名`、可选 `密码`
**并且** 点击 `保存`
**那么** 系统应为该仓库持久化一条新的 `DatabaseConnection` 记录
**并且** 其 `RepositoryId` 必须唯一指向该仓库
**并且** 连接字符串必须以密文形式存储
**并且** 若填写了用户名或密码，它们也必须以密文形式存储

### AC-3: 更新已有数据库连接且保留未替换密文

**假如** 某仓库已存在数据库连接配置
**当** 我修改数据库类型、连接字符串、用户名或密码并保存
**那么** 系统应更新同一条 `DatabaseConnection` 记录，而不是创建第二条
**并且** `UpdatedAt` 必须更新
**并且** 若编辑时用户名或密码留空，系统不得意外清空原有密文
**并且** UI 只能显示“已配置/未配置”或掩码摘要，而不是明文

### AC-4: 一个仓库最多一条数据库连接

**假如** 我对同一仓库重复提交数据库连接配置
**当** 系统处理保存请求
**那么** 必须保证该仓库最终只有一条 `DatabaseConnection`
**并且** 数据库层必须通过 `RepositoryId` 唯一约束防止重复记录
**并且** 删除配置后，仓库应恢复为“未配置数据库连接”状态

### AC-5: 支持数据库类型枚举与必填校验

**假如** 我在编辑数据库连接
**当** 我选择 `PostgreSQL`、`SqlServer`、`MySql`、`Sqlite`、`Oracle` 或 `Other` 之一
**那么** 系统应将其持久化为 `DatabaseType` 枚举值
**并且** `连接字符串` 与 `数据库类型` 必须为必填项
**并且** 所有校验错误必须以简体中文返回

### AC-6: 提供只读连接测试

**假如** 某仓库已保存数据库连接配置
**当** 我点击 `测试连接`
**那么** 系统应使用解密后的连接信息执行一次最小化、只读的连通性检查
**并且** 成功时返回中文成功消息
**并且** 失败时返回中文错误消息与可操作提示
**并且** 测试结果、日志和错误响应都不得泄露连接字符串、用户名或密码明文
**并且** 测试实现不得执行写操作

### AC-7: 权限边界遵循仓库可见性

**假如** 我是 `Administrator`
**当** 我访问任意可管理仓库的数据库连接区域或接口
**那么** 我可以查看并维护其数据库连接

**并且**

**假如** 我是某系统的 `System Owner`
**当** 我访问自己可见系统下的仓库
**那么** 我可以查看并维护这些仓库的数据库连接

**并且**

**假如** 我对某仓库无可见性
**当** 我尝试访问页面或调用相关接口
**那么** 系统必须拒绝访问或按现有可见性约定返回未找到/无权限结果

### AC-8: 为后续数据库工具消费提供稳定读取入口

**假如** 后续扫描阶段需要读取仓库数据库连接配置
**当** 应用服务按 `RepositoryId` 请求数据库连接
**那么** 系统应提供独立的协调器/服务返回解密前的持久化摘要和供测试/工具使用的解密结果
**并且** 调用方不需要直接拼接 `DbContext` 查询或自行处理解密

## Tasks / Subtasks

- [ ] Task 1: 建立数据库连接领域模型与仓库导航 (AC-2, AC-3, AC-4, AC-5)
  - [ ] 1.1 在 `src/dotnet/Vulgata.Core/Entities/` 新增 `DatabaseConnection.cs`
  - [ ] 1.2 新增 `DatabaseType` 枚举，包含 `PostgreSQL`、`SqlServer`、`MySql`、`Sqlite`、`Oracle`、`Other`
  - [ ] 1.3 在 `Repository.cs` 中增加可空的 `DatabaseConnection` 导航属性
  - [ ] 1.4 在实体中实现 `EncryptedConnectionString`、`EncryptedUsername`、`EncryptedPassword`、`CreatedAt`、`UpdatedAt` 的归一化与更新时间行为

- [ ] Task 2: 建立 EF Core 映射、仓储与迁移 (AC-2, AC-4)
  - [ ] 2.1 在 `VulgataDbContext` 注册 `DbSet<DatabaseConnection>`
  - [ ] 2.2 在 `src/dotnet/Vulgata.Infrastructure/Data/Configurations/` 新增 `DatabaseConnectionConfiguration.cs`
  - [ ] 2.3 配置 `RepositoryId` 为唯一外键，形成 `Repository` 到 `DatabaseConnection` 的一对零或一关系
  - [ ] 2.4 外键删除策略使用 `Restrict`
  - [ ] 2.5 在 `src/dotnet/Vulgata.Core/DomainServices/` 新增 `IDatabaseConnectionRepository`
  - [ ] 2.6 在 `src/dotnet/Vulgata.Infrastructure/Data/` 新增 `DatabaseConnectionRepository.cs`
  - [ ] 2.7 新增 EF Core migration 与 snapshot 更新

- [ ] Task 3: 建立数据库密文保护与连接测试服务 (AC-2, AC-3, AC-6, AC-8)
  - [ ] 3.1 在 `src/dotnet/Vulgata.Web/Data/` 或更合适的应用层目录新增 `IDatabaseConnectionEncryptionService`
  - [ ] 3.2 使用 ASP.NET Core Data Protection 实现数据库连接专用加解密服务，模式对齐 `ApiKeyEncryptionService`
  - [ ] 3.3 新增 `IDatabaseConnectionTestService` 与实现，根据 `DatabaseType` 执行最小化只读探活
  - [ ] 3.4 对 `PostgreSQL`、`SqlServer`、`MySql`、`Sqlite`、`Oracle`、`Other` 定义清晰的测试策略或明确的受限返回
  - [ ] 3.5 所有测试失败路径必须返回中文错误，并避免泄露敏感信息

- [ ] Task 4: 建立仓库级协调器与 DTO/校验 (AC-1, AC-3, AC-5, AC-6, AC-8)
  - [ ] 4.1 在 `src/dotnet/Vulgata.Shared/Repositories/` 新增数据库连接请求/响应 DTO
  - [ ] 4.2 扩展 `RepositoryDetailDto` 或新增仓库详情专用 DTO，包含数据库连接摘要信息
  - [ ] 4.3 在 `src/dotnet/Vulgata.Shared/Validators/Repositories/` 或现有验证目录新增中文校验器
  - [ ] 4.4 在 `src/dotnet/Vulgata.Web/Data/` 新增 `RepositoryDatabaseConnectionCoordinator.cs`
  - [ ] 4.5 协调器负责仓库可见性校验、upsert、删除、摘要读取、连接测试与密文字段保留策略

- [ ] Task 5: 暴露仓库级 Minimal API 端点 (AC-1, AC-3, AC-6, AC-7, AC-8)
  - [ ] 5.1 在 `Program.cs` 注册数据库连接仓储、加密服务、连接测试服务、协调器与验证器
  - [ ] 5.2 新增仓库级读取接口，例如 `GET /api/repositories/{repositoryId:guid}/database-connection`
  - [ ] 5.3 新增仓库级创建/更新接口，例如 `PUT /api/repositories/{repositoryId:guid}/database-connection`
  - [ ] 5.4 新增仓库级删除接口，例如 `DELETE /api/repositories/{repositoryId:guid}/database-connection`
  - [ ] 5.5 新增仓库级连接测试接口，例如 `POST /api/repositories/{repositoryId:guid}/database-connection/test`
  - [ ] 5.6 端点必须遵循现有 `ManagementAccess`、ProblemDetails、ValidationProblem 与中文错误风格

- [ ] Task 6: 在仓库详情 UI 中实现数据库连接管理 (AC-1, AC-2, AC-3, AC-6, AC-7)
  - [ ] 6.1 更新 `DashboardPage.razor`，启用真正的仓库详情查看流，而不是继续保留禁用的 `查看` 按钮
  - [ ] 6.2 推荐提取独立组件，例如 `RepositoryDatabaseConnectionPanel.razor` 或 `RepositoryDetailDialog.razor`
  - [ ] 6.3 在仓库详情中增加 `数据库连接` 区块，展示摘要状态与操作入口
  - [ ] 6.4 表单字段与按钮文案保持简体中文：`数据库类型`、`连接字符串`、`用户名`、`密码`、`保存`、`删除`、`测试连接`
  - [ ] 6.5 绝不在 UI 中回显敏感值明文，仅显示“已配置/未配置”或掩码摘要

- [ ] Task 7: 为后续数据库工具消费提供稳定接口 (AC-6, AC-8)
  - [ ] 7.1 提供按 `RepositoryId` 获取数据库连接摘要的方法
  - [ ] 7.2 提供仅供应用服务使用的解密读取方法，供连接测试与未来 schema/sample data 工具复用
  - [ ] 7.3 明确后续故事应复用该服务，不允许直接在工具或 Razor 组件里自行解密

- [ ] Task 8: 增加 Story 3.3 测试覆盖 (AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8)
  - [ ] 8.1 覆盖可见仓库的数据库连接创建成功且密文落库
  - [ ] 8.2 覆盖编辑时留空用户名/密码不会清空原密文
  - [ ] 8.3 覆盖同一仓库只能存在一条数据库连接
  - [ ] 8.4 覆盖无权限用户无法访问仓库数据库连接页面或接口
  - [ ] 8.5 覆盖连接测试成功与失败路径，且错误消息为中文
  - [ ] 8.6 覆盖仓库详情页面出现 `数据库连接` 区块与状态文案
  - [ ] 8.7 覆盖 SQLite 测试基座建表与唯一索引更新

- [ ] Task 9: 验证与收尾
  - [ ] 9.1 运行 Story 3.3 相关集成测试
  - [ ] 9.2 运行完整 `dotnet build`
  - [ ] 9.3 运行完整 `dotnet test`
  - [ ] 9.4 更新故事文件的 Dev Agent Record、File List、Change Log 与最终状态

## 功能需求提炼

- FR-3.5: `System Owners shall configure database connections per Repository — specifying connection string, database type, and access credentials.`
- 每个 `Repository` 只能有零或一条 `DatabaseConnection`。
- 连接字符串、用户名、密码都必须加密存储，且所有测试和读取过程不得泄露明文。
- 管理入口必须位于 `管理后台 -> 系统管理` 的仓库详情，而不是全局设置页。
- 必须提供连接测试能力，且测试只允许执行最小化只读探活。
- 数据库连接配置将作为未来 FR-4.13 / FR-4.14 数据库工具的基础能力，因此需要稳定的读取与解密接口。

## 领域模型详情

### 新增实体：DatabaseConnection

建议新增文件：

- `src/dotnet/Vulgata.Core/Entities/DatabaseConnection.cs`

实体字段至少包括：

- `Id: Guid`
- `RepositoryId: Guid`
- `EncryptedConnectionString: string`
- `DatabaseType: DatabaseType`
- `EncryptedUsername: string?`
- `EncryptedPassword: string?`
- `CreatedAt: DateTimeOffset`
- `UpdatedAt: DateTimeOffset`

导航属性：

- `Repository: Repository`

### 现有实体调整

- `Repository.cs`
  - 增加 `DatabaseConnection?` 导航属性
  - 保持现有名称、Git URL、上下文字段与工厂方法行为不回退

### 枚举设计

建议新增 `DatabaseType`：

- `PostgreSQL = 0`
- `SqlServer = 1`
- `MySql = 2`
- `Sqlite = 3`
- `Oracle = 4`
- `Other = 5`

### 业务规则

- `RepositoryId` 全局唯一，保证一个仓库最多一条数据库连接。
- `EncryptedConnectionString` 为必填密文字段。
- `EncryptedUsername`、`EncryptedPassword` 为可选密文字段，但若用户提交空值且记录已存在，应支持“保留原值”语义。
- `DatabaseType` 必须是已定义枚举值之一。

## 业务规则与校验

- `连接字符串` 必填，去首尾空格后不能为空。
- `数据库类型` 必填，必须在既定枚举中。
- `用户名`、`密码` 可选，但需要定义“清空”和“保留”的显式语义，避免编辑时误删密文。
- 所有页面文案、按钮、校验消息、错误消息保持简体中文。
- 连接测试前必须保证配置已通过基础校验；若缺少必要凭据，应直接返回中文校验错误。
- 连接测试实现必须默认只读，不允许写入、迁移或执行危险命令。
- 日志中只能出现仓库标识、数据库类型、成功/失败状态等安全信息，不能打印明文 secrets。

## 技术实现计划

### 1. 持久化与仓储

新增或更新以下文件：

- `src/dotnet/Vulgata.Core/Entities/DatabaseConnection.cs`
- `src/dotnet/Vulgata.Core/Entities/Repository.cs`
- `src/dotnet/Vulgata.Core/DomainServices/IDatabaseConnectionRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/DatabaseConnectionRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/DatabaseConnectionConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/` 下新增迁移

实现要求：

- 复用 `RepositoryConfiguration.cs` 与 `LlmProviderConfiguration.cs` 的 `IEntityTypeConfiguration<T>` 风格。
- 一对零或一关系必须在数据库层落实唯一约束，而不是只靠应用层约定。
- 仓储应提供按 `RepositoryId` 读取与 upsert 友好的查询接口。

### 2. 加密与连接测试

新增或更新以下文件：

- `src/dotnet/Vulgata.Web/Data/IDatabaseConnectionEncryptionService.cs`
- `src/dotnet/Vulgata.Web/Data/DatabaseConnectionEncryptionService.cs`
- `src/dotnet/Vulgata.Web/Data/IDatabaseConnectionTestService.cs`
- `src/dotnet/Vulgata.Web/Data/DatabaseConnectionTestService.cs`

实现要求：

- Data Protection 用法对齐 `ApiKeyEncryptionService`，但应使用数据库连接专属 protector purpose，避免与 LLM API key 共用命名空间。
- 连接测试优先采用 provider 原生 client 的最小读操作，例如 `OpenAsync` + 简单只读探活。
- 对暂不具备 runtime provider 依赖的数据库类型，需要明确决定是引入正式驱动还是返回受控的“不支持测试此类型”中文结果，不能留下模糊行为。

### 3. 仓库级协调器、DTO 与 API

新增或更新以下文件：

- `src/dotnet/Vulgata.Shared/Repositories/` 下新增数据库连接请求/响应模型
- `src/dotnet/Vulgata.Shared/Validators/Repositories/` 下新增数据库连接校验器
- `src/dotnet/Vulgata.Web/Data/RepositoryDatabaseConnectionCoordinator.cs`
- `src/dotnet/Vulgata.Web/Program.cs`

建议路由：

- `GET /api/repositories/{repositoryId:guid}/database-connection`
- `PUT /api/repositories/{repositoryId:guid}/database-connection`
- `DELETE /api/repositories/{repositoryId:guid}/database-connection`
- `POST /api/repositories/{repositoryId:guid}/database-connection/test`

实现要求：

- 协调器必须复用现有仓库/系统可见性规则，而不是在 Razor 组件里自己判权限。
- 结果组织风格对齐 `RepositoryManagementCoordinator` / `LlmProviderManagementCoordinator`。
- API 保持 `/api/` 前缀、中文错误、ProblemDetails / ValidationProblem 语义一致。

### 4. 管理后台仓库详情

更新以下文件：

- `src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor`

建议新增：

- `src/dotnet/Vulgata.Web/Components/Pages/Management/RepositoryDetailDialog.razor`
- `src/dotnet/Vulgata.Web/Components/Pages/Management/RepositoryDatabaseConnectionPanel.razor`

实现要求：

- 当前 `DashboardPage.razor` 的仓库表格 `查看` 按钮仍是禁用占位，本故事应把它演进成真实详情入口。
- `数据库连接` 区块必须绑定仓库详情上下文，而不是独立散落到别的页面。
- 视觉和交互继续沿用 Fluent UI Blazor 与简体中文文案。

## 测试场景

- `ManagementUser_CanCreateDatabaseConnection_ForVisibleRepository_AndSecretsAreEncrypted`
- `UpdateDatabaseConnection_WithoutReplacingUsernameOrPassword_PreservesExistingSecrets`
- `Repository_CanHaveAtMostOneDatabaseConnection`
- `DatabaseConnectionTest_WithValidConfiguration_ReturnsChineseSuccessMessage`
- `DatabaseConnectionTest_WithInvalidCredentials_ReturnsChineseProblem`
- `UnauthorizedUser_CannotManageRepositoryDatabaseConnection`
- `RepositoryDetailPage_ShowsDatabaseConnectionSection`

建议测试文件：

- `tests/Vulgata.Tests/DatabaseConnectionConfigurationTests.cs`

测试风格应延续：

- `tests/Vulgata.Tests/RepositoryManagementTests.cs`
- `tests/Vulgata.Tests/LlmProviderConfigTests.cs`

特别注意：

- 当前测试基座对部分新表使用 SQLite 手工建表；新增 `DatabaseConnections` 后需要同步补充测试 DDL、唯一索引与清理逻辑。
- 断言必须覆盖中文文案、权限边界、唯一性、密文持久化与无明文泄露。
- 连接测试若依赖外部 provider，需要优先考虑可控的本地/模拟测试路径，避免脆弱集成。

## Dev Notes

- 不要把数据库连接字段直接塞进 `Repository` 表。需求明确要求独立 `DatabaseConnection` 实体，且一仓库零或一连接。
- 不要复用 `ApiKeyEncryptionService` 的接口名称和 protector purpose 直接去存数据库 secrets；模式可复用，但语义和 purpose string 应独立。
- 不要在 Razor 页面直接操作 `DbContext` 完成 CRUD 或解密；沿用 coordinator + repository + validator 的既有分层。
- 当前仓库管理只覆盖创建与删除，仓库详情还是占位态；本故事真正的 UI 工作重点之一是把“仓库详情”补齐到可承载数据库连接面板。
- 对 `Other` 数据库类型必须给出受控策略。若实现期不支持自动探活，也要返回明确中文提示，而不是悄悄跳过。
- NFR-2.4 要求数据库工具只连非生产或只读副本。本故事至少要在文案、实现和日志中保持这一约束，不要引入可写测试语句。
- 独立仓库当前对所有具备管理权限的用户可见；在没有新增独立仓库所有权模型前，数据库连接权限应遵循现有仓库可见性实现。

## 需遵循的架构与代码模式

- 复用 `RepositoryManagementCoordinator` 的 mutation result、可见性与页面集成模式。
- 复用 `ApiKeyEncryptionService` 的 ASP.NET Core Data Protection 实现方式，但保持数据库连接专属密钥用途隔离。
- 复用 `VulgataDbContext` + `Configurations/` 下的 `IEntityTypeConfiguration<T>` 组织方式。
- 复用 `DashboardPage.razor` 作为 `管理后台 -> 系统管理` 承载位置，不新增偏离 UX 规划的 route。
- 复用 `Program.cs` 中现有 `ManagementAccess`、ProblemDetails、ValidationProblem、DI 注册风格。
- 复用 `RepositoryManagementTests.cs` 与 `LlmProviderConfigTests.cs` 的集成测试与中文 HTML 断言模式。