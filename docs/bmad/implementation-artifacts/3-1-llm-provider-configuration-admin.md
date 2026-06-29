---
story_key: 3-1-llm-provider-configuration-admin
title: Story 3.1: LLM Provider Configuration Admin
Status: ready-for-dev
Epic: 3
Story: 1
created: 2026-06-29
baseline_commit: c6f4e601d880dd1fc08ed0c57ef00bb0346e6d43
depends_on:
  - 1-5-role-seeding-and-authorization-policies
  - 1-6-administrator-role-assignment
  - 2-5-user-supplied-context
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/implementation-artifacts/2-5-user-supplied-context.md
  - src/dotnet/Vulgata.Web/Program.cs
  - src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor
  - src/dotnet/Vulgata.Core/Entities/GlobalContext.cs
  - src/dotnet/Vulgata.Core/Entities/PendingContextChange.cs
  - src/dotnet/Vulgata.Core/Entities/System.cs
  - src/dotnet/Vulgata.Core/Entities/Repository.cs
  - src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs
  - src/dotnet/Vulgata.Infrastructure/Data/Configurations/SystemConfiguration.cs
  - src/dotnet/Vulgata.Infrastructure/Data/Configurations/RepositoryConfiguration.cs
  - tests/Vulgata.Tests/RepositoryManagementTests.cs
---

# Story 3.1: LLM Provider Configuration Admin

## 用户故事

作为一名 `Administrator`，
我希望在 `管理后台 -> 设置` 中维护全局 LLM 提供商配置，
从而让 Vulgata 可以同时连接多个 LLM Provider，并为不同代理角色提供默认提供商与连通性验证能力。

## 目标边界

- 本故事覆盖全局级 LLM Provider 的新增、编辑、列表展示、启用多提供商并存，以及单条配置的连接测试入口。
- 本故事只覆盖 `Administrator` 的全局管理能力，不实现系统级 provider 覆盖；系统级覆盖留给 Story 3.2。
- 本故事要求为后续代理编排建立稳定的数据模型与读取契约，但不要求在本故事内把所有扫描/聊天代理真正切换到新配置。
- 本故事必须以简体中文呈现 UI，放置于现有 `管理后台 -> 设置` 页面信息架构下，延续 Fluent UI Blazor 和 Minimal API 模式。
- 本故事必须支持多个 Provider 同时存在，且每个 Provider 可以声明支持的 API 类型和默认代理角色归属。
- 本故事必须提供连接测试，但连接测试只验证配置是否可连通及凭据是否有效，不生成业务文档或触发扫描。

## 验收标准

### AC-1: 管理员查看并维护 Provider 列表

**假如** 我以 `Administrator` 身份进入 `管理后台 -> 设置`
**当** 我打开 `LLM 提供商配置` 区域
**那么** 我应看到现有 Provider 列表
**并且** 列表至少展示 `名称`、`基础地址`、`支持的 API 类型`、`默认代理角色`、`最后更新时间`
**并且** 页面上应提供 `新增提供商`、`编辑`、`测试连接` 操作入口

### AC-2: 管理员新增 LLM Provider

**假如** 当前尚未存在目标 Provider
**当** 我填写 `名称`、`基础地址 URL`、`API 密钥`、`支持的 API 类型`、`默认代理角色`
**并且** 点击 `保存`
**那么** 系统应持久化一条新的 `LlmProvider` 记录
**并且** API 密钥不得以明文形式存储
**并且** 保存成功后页面应显示中文成功反馈

### AC-3: 管理员编辑现有 LLM Provider

**假如** 某条 Provider 已存在
**当** 我修改其基础地址、API 密钥、支持的 API 类型或默认代理角色并保存
**那么** 系统应更新对应记录的 `UpdatedAt`
**并且** 若未重新输入 API 密钥，不得意外清空已保存密钥
**并且** 编辑后列表应显示最新配置摘要，而不是显示密钥明文

### AC-4: 支持多个 Provider 并存

**假如** 我先后配置了多个 LLM Provider
**当** 我返回 Provider 列表
**那么** 系统应同时展示多条有效配置
**并且** 每条配置都能独立维护自己的 `支持 API 类型` 与 `默认代理角色`
**并且** 后续读取服务能够返回所有可用 Provider，而不是只保留最后一条

### AC-5: 单个 Provider 声明支持的 API 类型

**假如** 我在编辑 Provider
**当** 我勾选 `Chat Completions`、`Responses`、`Messages` 中的一项或多项
**那么** 系统应以可组合的 flags 方式持久化 `SupportedApiTypes`
**并且** 至少必须选择一项
**并且** 列表与详情区域应以中文可读文本显示当前支持能力

### AC-6: 单个 Provider 指定默认代理角色

**假如** 我在编辑 Provider
**当** 我选择 `编排代理`、`工作代理` 或 `对话代理` 之一作为默认代理角色
**那么** 系统应将该值持久化为 `DefaultAgentType`
**并且** 后续应用服务必须可以按代理角色查询默认 Provider 候选

### AC-7: 提供连接测试

**假如** 某条 Provider 已保存必要配置
**当** 我点击 `测试连接`
**那么** 系统应使用该 Provider 的基础地址与解密后的 API 密钥发起一次最小化连通性检查
**并且** 成功时返回中文成功消息
**并且** 失败时返回中文错误消息与可操作提示
**并且** 测试结果不得泄露 API 密钥明文

### AC-8: 权限边界与中文界面

**假如** 我不是 `Administrator`
**当** 我尝试访问 Provider 配置页面或直接调用相关写接口
**那么** 页面不应暴露新增/编辑/测试入口
**并且** 服务端必须拒绝未授权写操作
**并且** 所有标签、按钮、校验消息、错误消息都必须为简体中文

## Tasks / Subtasks

- [ ] Task 1: 建立 LLM Provider 领域模型与枚举 (AC-2, AC-4, AC-5, AC-6)
  - [ ] 1.1 在 `src/dotnet/Vulgata.Core/Entities/` 新增 `LlmProvider.cs`
  - [ ] 1.2 在同文件或相邻文件新增 `LlmProviderApiType` flags 枚举与 `AgentType` 枚举
  - [ ] 1.3 让实体负责名称、URL、API 密钥密文、支持能力、默认代理角色、时间戳的归一化与更新行为

- [ ] Task 2: 建立持久化映射与仓储抽象 (AC-2, AC-3, AC-4)
  - [ ] 2.1 在 `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs` 注册 `DbSet<LlmProvider>`
  - [ ] 2.2 在 `src/dotnet/Vulgata.Infrastructure/Data/Configurations/` 新增 `LlmProviderConfiguration.cs`
  - [ ] 2.3 在 `src/dotnet/Vulgata.Core/DomainServices/` 新增 `ILlmProviderRepository`
  - [ ] 2.4 在 `src/dotnet/Vulgata.Infrastructure/Data/` 新增 `LlmProviderRepository.cs`
  - [ ] 2.5 新增 EF Core migration，保持 `IEntityTypeConfiguration<T>` 模式，不使用数据注解

- [ ] Task 3: 建立加密与连接测试服务 (AC-2, AC-3, AC-7)
  - [ ] 3.1 在 `src/dotnet/Vulgata.Core/DomainServices/` 定义 `ILlmProviderSecretProtector` 与 `ILlmProviderConnectionTestService`
  - [ ] 3.2 在 `src/dotnet/Vulgata.Infrastructure/` 新增基于 ASP.NET Core Data Protection 的 API 密钥加解密实现
  - [ ] 3.3 在 `src/dotnet/Vulgata.Infrastructure/` 新增基于 OpenAI SDK 的连接测试实现，兼容 OpenAI-compatible endpoint
  - [ ] 3.4 约束连接测试为最小请求，不触发真实扫描或长时任务

- [ ] Task 4: 建立应用协调层与 DTO/校验 (AC-1, AC-2, AC-3, AC-5, AC-6, AC-7)
  - [ ] 4.1 在 `src/dotnet/Vulgata.Shared/` 新增 provider 请求/响应模型
  - [ ] 4.2 在 `src/dotnet/Vulgata.Shared/Validators/` 新增 FluentValidation 校验器
  - [ ] 4.3 在 `src/dotnet/Vulgata.Web/Data/` 新增 `LlmProviderManagementCoordinator.cs` 或等效协调器
  - [ ] 4.4 协调器统一处理新增、编辑、列表读取、密钥保留策略、连接测试返回模型

- [ ] Task 5: 暴露 Minimal API 端点并注册服务 (AC-1, AC-2, AC-3, AC-4, AC-7, AC-8)
  - [ ] 5.1 在 `src/dotnet/Vulgata.Web/Program.cs` 注册新仓储、加密服务、连接测试服务、协调器与验证器
  - [ ] 5.2 新增 `/api/llm-providers` 列表、创建、更新接口
  - [ ] 5.3 新增 `/api/llm-providers/{id:guid}/test-connection` 连接测试接口
  - [ ] 5.4 全部接口沿用现有 `AdministratorOnly`、ProblemDetails、ValidationProblem 风格

- [ ] Task 6: 演进设置页 UI (AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8)
  - [ ] 6.1 更新 `src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor`，把当前 `LLM 提供商配置（后续故事实现）` 占位内容替换为真实列表与编辑表单
  - [ ] 6.2 使用 Fluent UI Blazor 组件呈现 Provider 列表、编辑区、支持能力多选和连接测试反馈
  - [ ] 6.3 确保非管理员只能看到受限内容或被授权策略拦截，不能触发写操作
  - [ ] 6.4 所有交互文案保持简体中文

- [ ] Task 7: 为后续代理消费建立稳定读取面 (AC-4, AC-6)
  - [ ] 7.1 提供按 `DefaultAgentType` 查询默认 Provider 的服务方法
  - [ ] 7.2 提供读取全部可用 Provider 的服务方法，为 Story 3.2 多层覆盖和后续自动故障转移做准备
  - [ ] 7.3 明确当前故事只建立全局默认配置，不在此故事实现系统级 override 决策

- [ ] Task 8: 增加 Story 3.1 测试覆盖 (AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8)
  - [ ] 8.1 覆盖管理员创建 Provider 成功并持久化密文 API 密钥
  - [ ] 8.2 覆盖管理员编辑 Provider 时未输入新密钥则保留原密钥
  - [ ] 8.3 覆盖多个 Provider 并存且列表全部返回
  - [ ] 8.4 覆盖 `SupportedApiTypes` flags 的校验与读写
  - [ ] 8.5 覆盖非管理员访问页面/接口被拒绝
  - [ ] 8.6 覆盖连接测试成功与失败两条路径，且错误消息为中文

- [ ] Task 9: 验证与收尾
  - [ ] 9.1 运行 Story 3.1 相关集成测试
  - [ ] 9.2 运行完整 `dotnet build` 与 `dotnet test`
  - [ ] 9.3 更新故事文件的 Dev Agent Record、File List、Change Log 与最终状态

## 功能需求提炼

- FR-3.1: Administrators shall configure LLM Providers at the global level.
- FR-3.2: The system shall support multiple LLM Providers simultaneously.
- FR-3.4: The system shall provide a connection test for each configured LLM Provider.
- UI 必须位于 `管理后台 -> 设置`，并保持简体中文。
- LLM 客户端采用 OpenAI SDK，目标兼容 DeepSeek V4 的 OpenAI-compatible endpoint。
- API 密钥必须加密存储，不能以明文持久化。

## 领域模型详情

### LlmProvider 实体

建议新增文件：

- `src/dotnet/Vulgata.Core/Entities/LlmProvider.cs`

实体字段必须至少包括：

- `Id: Guid`
- `Name: string`
- `BaseEndpointUrl: string`
- `EncryptedApiKey: string`
- `SupportedApiTypes: LlmProviderApiType`
- `DefaultAgentType: AgentType`
- `CreatedAt: DateTimeOffset`
- `UpdatedAt: DateTimeOffset`

### 枚举设计

建议新增：

- `LlmProviderApiType`
  - `[Flags]`
  - `ChatCompletions = 1`
  - `Responses = 2`
  - `Messages = 4`

- `AgentType`
  - `Orchestrator = 0`
  - `Worker = 1`
  - `Chat = 2`

### 领域行为建议

- `Name` 需要去首尾空格并保留原始展示值。
- `BaseEndpointUrl` 需要标准化并保证为绝对 URL。
- `EncryptedApiKey` 只能由加密服务写入，UI 与 API 返回模型只暴露 `HasApiKey` 或掩码摘要，不返回明文。
- `SupportedApiTypes` 至少选择一项。
- `DefaultAgentType` 当前故事按单选处理，表示该 Provider 面向哪类代理作为默认候选。
- `UpdatedAt` 在任何配置修改后必须更新。

## 校验规则

- `Name` 必填，去空格后不能为空，建议最大长度 `200`，并保持全局唯一。
- `BaseEndpointUrl` 必填，必须是 `http` 或 `https` 绝对地址，建议最大长度 `2000`。
- `API 密钥` 在新建时必填，在编辑时可选；若为空则表示保留现有密钥。
- `SupportedApiTypes` 至少选择一项，不能保存为 `0`。
- `DefaultAgentType` 必须是 `Orchestrator`、`Worker`、`Chat` 之一。
- 连接测试前必须保证配置已通过基础校验；若无可用密钥则直接返回中文校验错误。
- 所有校验错误必须通过现有 FluentValidation + `Results.ValidationProblem(...)` 输出中文消息。

## 技术实现计划

### 1. 领域与持久化

新增或更新以下文件：

- `src/dotnet/Vulgata.Core/Entities/LlmProvider.cs`
- `src/dotnet/Vulgata.Core/DomainServices/ILlmProviderRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/LlmProviderRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/LlmProviderConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/` 下新增迁移

实现要求：

- 沿用 `System`、`Repository`、`GlobalContext` 的实体风格，由实体构造函数和更新方法负责归一化。
- 沿用 `RepositoryConfiguration.cs` / `SystemConfiguration.cs` 的 EF Core 映射模式。
- 不要把 provider 配置塞进 `GlobalContext` 或 `PendingContextChange`；LLM Provider 是独立聚合。

### 2. 加密与连接测试

新增或更新以下文件：

- `src/dotnet/Vulgata.Core/DomainServices/ILlmProviderSecretProtector.cs`
- `src/dotnet/Vulgata.Core/DomainServices/ILlmProviderConnectionTestService.cs`
- `src/dotnet/Vulgata.Infrastructure/Services/LlmProviderSecretProtector.cs`
- `src/dotnet/Vulgata.Infrastructure/Services/LlmProviderConnectionTestService.cs`

实现要求：

- 使用 ASP.NET Core Data Protection 对 API 密钥执行加密/解密。
- 使用 OpenAI SDK 构造兼容 OpenAI endpoint 的客户端，支持 DeepSeek V4 场景。
- 连接测试必须为最小化请求，并提供结构化日志，不记录密钥明文。

### 3. 应用协调层、DTO 与 API

新增或更新以下文件：

- `src/dotnet/Vulgata.Shared/LlmProviders/` 下新增请求与响应模型
- `src/dotnet/Vulgata.Shared/Validators/LlmProviders/` 下新增校验器
- `src/dotnet/Vulgata.Web/Data/LlmProviderManagementCoordinator.cs`
- `src/dotnet/Vulgata.Web/Program.cs`

建议路由：

- `GET /api/llm-providers`
- `POST /api/llm-providers`
- `PUT /api/llm-providers/{id:guid}`
- `POST /api/llm-providers/{id:guid}/test-connection`

实现要求：

- 保持 `/api/` 前缀和 plural noun 风格。
- 全部写接口使用 `AdministratorOnly` 授权策略。
- 列表接口至少返回 UI 所需的摘要字段，不返回 API 密钥明文。
- `Program.cs` 中的注册风格应与现有 repository/coordinator/validator 注册保持一致。

### 4. 管理后台设置页

更新以下文件：

- `src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor`

实现要求：

- 在当前全局上下文区域下方增加 `LLM 提供商配置` 区块，替换现有占位文案。
- 使用 Fluent UI Blazor 组件实现列表、表单、状态反馈和连接测试按钮。
- 表单字段标签必须为简体中文：`名称`、`基础地址`、`API 密钥`、`支持的 API 类型`、`默认代理角色`。
- 对已保存的密钥只展示“已配置”或掩码摘要，不回显明文。

## 测试场景

- `Administrator_CanCreateLlmProvider_AndApiKeyIsEncrypted`
- `Administrator_CanUpdateLlmProvider_WithoutReplacingExistingApiKey`
- `Administrator_CanListMultipleLlmProviders`
- `CreateLlmProvider_WithInvalidEndpoint_ReturnsChineseValidationError`
- `CreateLlmProvider_WithoutApiTypes_ReturnsChineseValidationError`
- `NonAdministrator_CannotModifyLlmProviderSettings`
- `TestConnection_WithValidConfiguration_ReturnsSuccessMessage`
- `TestConnection_WithInvalidApiKey_ReturnsChineseProblem`

建议测试文件：

- `tests/Vulgata.Tests/LlmProviderConfigurationTests.cs`

测试风格应延续 `RepositoryManagementTests.cs`：

- 优先使用集成测试覆盖 Minimal API + 页面行为。
- 测试命名采用 `{Method}_{Scenario}_{ExpectedResult}`。
- 断言中文文案、权限边界、ProblemDetails/ValidationProblem 输出。

## Dev Notes

- 不要把 API 密钥明文存进数据库，也不要把密钥带入日志、异常消息或 HTML。
- 不要直接在 Razor 页面里操作 `DbContext` 完成完整业务流；应仿照现有管理协调器模式，把持久化、加密、连接测试放进服务层。
- 不要把 `DefaultAgentType` 设计成“一条 Provider 支持多个默认角色”；本故事按单一默认角色建模，系统级覆盖留给 Story 3.2。
- `SupportedApiTypes` 才是多选 flags；不要把多选语义误放到 `DefaultAgentType`。
- 本故事只建立全局级 provider 配置，不提前实现系统级 override UI，以免与 Story 3.2 边界重叠。
- 连接测试调用外部 endpoint 时要考虑超时、网络失败和 401/403，全部返回简体中文且保持 ProblemDetails 风格。
- 如果 OpenAI SDK 对目标 endpoint 的最小测试调用方式有限，优先选用最稳定、最短路径的探活调用，不要为了“更真实”引入复杂 prompt 或长响应。

## 需遵循的架构与代码模式

- 复用 `src/dotnet/Vulgata.Web/Program.cs` 中现有的 Minimal API、授权策略、ProblemDetails 与 DI 注册模式。
- 复用 `src/dotnet/Vulgata.Infrastructure/Data/Configurations/` 中的 `IEntityTypeConfiguration<T>` 映射方式。
- 复用 `src/dotnet/Vulgata.Core/Entities/System.cs` 与 `src/dotnet/Vulgata.Core/Entities/Repository.cs` 的实体归一化风格。
- 复用 `src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor` 作为 `管理后台 -> 设置` 承载位置，不新增偏离 IA 的路由。
- 复用 `tests/Vulgata.Tests/RepositoryManagementTests.cs` 的集成测试组织方式和中文断言风格。
