# Development conventions

Read this for production code, API, UI, persistence or runtime work. The architecture
gateway/spine own design; this guide preserves project-specific implementation rules.
Check package/build files for installed versions rather than copying a dated list.

## Project layout and ownership

- `src/dotnet/Vulgata.Core`: entities and domain/service/repository contracts; no EF
  or Infrastructure/Web dependencies.
- `src/dotnet/Vulgata.Infrastructure`: domain `VulgataDbContext`, configurations and
  migrations, aggregate repositories, Git/source/tool/storage adapters.
- `src/dotnet/Vulgata.Shared`: DTOs, FluentValidation, `LoadState`, policy/role names.
- `src/dotnet/Vulgata.Agents`: bounded Agent execution, result contracts and embedded
  `Prompts/`; externalize prompts rather than hardcoding strings.
- `src/dotnet/Vulgata.Web.ViewModels`: presentation-model project; check the actual
  implementation before assuming the planned MVVM structure is populated.
- `src/dotnet/Vulgata.Web`: Blazor host, `Program.cs`, `Components/`, `Api/`, validators
  and the existing Identity context/migrations/seeding in `Data/`.
- `tests/Vulgata.Tests`: the single test project; see [testing](testing.md).

Identity and domain contexts share PostgreSQL with separate `identity`/`vulgata`
schemas and migration histories. The existing Web/Data Identity placement is an
explicit exception, not permission to move domain persistence into Web or an
instruction to relocate Identity as incidental work.

## Coding and persistence

- Preserve nullable/implicit-usings/analyzer settings in `Directory.Build.props`;
  central package versions are in `Directory.Packages.props`. Do not infer that every
  warning is an error: inspect configured severity and actual command results.
- Async methods end in `Async`; private fields use `_camelCase`; persistent table/
  column names use PascalCase. Domain events and hub methods use past tense.
- Use one specific repository per aggregate root, with contracts in Core and domain
  implementations in Infrastructure. No generic `IRepository<T>` or public domain
  `IQueryable`/`DbSet` leakage.
- Map domain entities using one `IEntityTypeConfiguration<T>` per aggregate under
  Infrastructure/Data/Configurations and assembly registration. No entity data
  annotations; use FluentValidation for domain/request input.
- Preserve canonical structured JSON CL/BL content, immutable versions, exact
  dependencies and the owning publication/read-selection contracts. Presentation
  Markdown is derived, not an alternate editable store.
- Add only the records/migrations owned by the active story. Existing schemas or
  provider receipts are not automatic authority for new generic semantics.

## APIs and security

- Minimal APIs use plural resources under `/api/`, grouped in `Web/Api/<area>/`
  through `MapVulgataApiEndpoints()`. Use ProblemDetails, not wrapping envelopes.
- Preserve API exclusion from cookie redirects and status-page HTML rewrites;
  authentication failures must remain proper 401/403 responses.
- Retain Administrator, SystemOwner and User roles and their management policies.
  Knowledge/history/citation reads use Knowledge authorization, not management-only
  authorization inherited from page placement.
- Preserve bcrypt hashing, Data Protection for provider/database secrets and current
  contributor/purpose/mode checks. Keep source, credentials and hidden reasoning out
  of logs/prompts outside their approved boundaries.
- Database tool restrictions remain read-only/non-production; deferred tools do not
  create an implicit grant. Use exact revision-bound source handles, not user/model-
  chosen physical paths.

## UI

Use the current [DESIGN](../bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md)
and [EXPERIENCE](../bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md)
for concrete interaction and visual rules. Preserve Simplified Chinese UI, Fluent
components, Library Oak styling, Noto Serif SC display text, Business/light and
IT/dark modes, chat-first navigation and the existing management shell.

- Identity errors use `ChineseIdentityErrorDescriber`. Any framework DataAnnotations
  still rendering Identity UI need explicit Chinese messages; this is not permission
  to put data annotations on domain entities.
- Data-loading ViewModels expose `LoadState`:
  `Idle|Loading|Loaded|Refreshing|Empty|NoResults|Error|Cancelling`.
- Distinguish missing/no-results/error/partial/stale data and provisional/completed/
  interrupted output. Use real Scan progress and genuine Chat deltas; the basic graph
  is manually refreshed. Planned diagram dependencies need actual integration.

## Verified command shapes

Run from the repository root. These are command shapes, not evidence that the target
runtime or a particular test suite is currently qualified.

```powershell
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
dotnet run --project src/dotnet/Vulgata.Web/Vulgata.Web.csproj
docker compose --profile post-deadline-app up --build
dotnet ef migrations add <Name> --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
```

The Compose command is the current baseline profile name, not a restored competition
scope or proof of the bundled-tool target. Inspect current Compose/configuration
before running. Web startup applies both contexts' migrations and role seeding and
fails on initialization error. Establish the authorized database/service boundary
first; listing a command does not authorize running it. Local development outside
Docker is possible with an explicitly configured PostgreSQL instance; the adopted
generic deployment target remains the two-service Docker arrangement.
