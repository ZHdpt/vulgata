# Architecture adoption technology / reality review

Date: 2026-09-11. Lens: configured BMad Architecture technology/reality review; independent, bounded adoption pass.

Reviewed completely:

- `architecture/ARCHITECTURE-SPINE.md`, SHA-256 `13504EB83C7822B996E278113DFA31D5A70D07C2407035ADDBA3645BCDEB5A8B`.
- `architecture.md` gateway, SHA-256 `2F40212D9FF4F90593A6FC9D478E1237E6630064E466F2369CA62CF5B1FC98DC`.
- Observed source HEAD: `2bb83c3b44fc0afc07ae253ec1df5fab4399b964`.

## Verdict

**PASS for this adoption lens. No required correction, consequential technology mismatch, or fabricated current implementation/runtime claim found.** This verdict is neither integrated adoption acceptance nor implementation authorization or runtime qualification.

The gateway distinguishes preserved foundations, target responsibilities and historical evidence. The spine labels package/tool versions as observed seeds, not upgrades, and replacement behavior as future qualification obligations.

## Fresh repository checks

| Claim | Evidence and conclusion |
| --- | --- |
| Accepted external ADs preserved | The external source hash is `CC7E5AC92810734549607F38399720A027E3E237320ACEC30926FBF24D6FE093`. Direct comparison found 32 AD blocks in both copies, identical after newline normalization. Adoption framing does not alter their rules. |
| Six source projects and .NET/Blazor foundations | [Vulgata.slnx](G:/source/repos/vulgata/Vulgata.slnx:8) lists the six projects under `src/dotnet/`, alongside separately located spike/demo/test projects. Their targets are `net10.0`; [Program.cs](G:/source/repos/vulgata/src/dotnet/Vulgata.Web/Program.cs:60) registers Interactive Server components. Conceptual modules are correctly not presented as existing new projects. |
| Package seeds | [Directory.Packages.props](G:/source/repos/vulgata/Directory.Packages.props:11) declares MAF 1.13.0, EF Core packages 10.0.9, Npgsql EF 10.0.3 and StreamJsonRpc 2.25.29, plus Fluent UI and CommunityToolkit.Mvvm. This checks declared versions, not installed SDKs, restored/transitive dependencies or latest releases. |
| Tool/database seeds | [AnalysisRuntimeIdentity.cs](G:/source/repos/vulgata/src/dotnet/Vulgata.Core/DomainServices/AnalysisRuntimeIdentity.cs:41) pins CodeGraph 1.5.0, JDT-LS `1.61.0-202608141332`, Java 21 and StreamJsonRpc. [Compose](G:/source/repos/vulgata/docker-compose.yml:3) declares PostgreSQL 17. These remain historical runtime contracts, not qualification of the new container integration. |
| Identity placement and migrations | Identity context/migrations remain in [Web/Data](G:/source/repos/vulgata/src/dotnet/Vulgata.Web/Data/ApplicationDbContext.cs:6); domain context/configurations/migrations remain in [Infrastructure/Data](G:/source/repos/vulgata/src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs:103). [Program.cs](G:/source/repos/vulgata/src/dotnet/Vulgata.Web/Program.cs:231) uses the same PostgreSQL connection with distinct histories: `identity.__IdentityMigrationsHistory` and `public.__EFMigrationsHistory`; entity schemas are `identity` and `vulgata`. Startup migrations/role seeding are registered. The gateway does not demand relocation or imply histories both live in their entity schema. |
| Non-root target is preserved, not newly invented | The [archived architecture](G:/source/repos/vulgata/docs/bmad/history/pre-generic-adoption-2026-09-11/files/docs/bmad/planning-artifacts/architecture.md:271) explicitly required `USER app`; the [Dockerfile](G:/source/repos/vulgata/docker/Dockerfile:54) retains it. Gateway line 59 restores this baseline for the future bundled processes without claiming their existence. |
| Security/API/loading conventions have seeds | [Program.cs](G:/source/repos/vulgata/src/dotnet/Vulgata.Web/Program.cs:67) separates API 401/403 and status rewrites; Data Protection configuration, [secret protection](G:/source/repos/vulgata/src/dotnet/Vulgata.Web/Data/SecretEncryptionService.cs:12), bcrypt, seeded roles and the eight [LoadState values](G:/source/repos/vulgata/src/dotnet/Vulgata.Shared/LoadState.cs:3) exist. Target cross-contributor authorization remains an obligation, not inferred completeness. |
| Historical completion claims stay bounded | The [archived sprint status](G:/source/repos/vulgata/docs/bmad/history/pre-generic-adoption-2026-09-11/files/docs/bmad/implementation-artifacts/sprint-status.yaml:66) records Epics 1–4 and Stories 11.1–11.4/12.1–12.4 done, with 11.5 in review. The [12.5 termination record](G:/source/repos/vulgata/specs/014-grounded-llm-wiki-chat-release-evidence/evidence/handoffs/story-12-5-termination-blocked-01.md:10) explicitly says terminated-blocked-incomplete. The gateway does not turn these into generic completion. |

Named replacement-map paths exist. In particular, `PreparedScanPairBuilder` still depends on two-Repository coordination; it is correctly a replacement area rather than proof of independent generic initiation.

## Qualification observations, not adoption defects

1. **Bundled tools still need new platform-specific qualification.** Current Compose keeps Vulgata behind `post-deadline-app`; the Dockerfile installs Git/certificates/curl, not CodeGraph/JDT-LS, and declared app mounts do not establish retained-source/key recovery. The existing tool identity includes Windows-specific artifacts. AD-3/15/30–32 already require actual image/platform identities, containment, readiness and recovery evidence. Preserve non-root execution while implementing those obligations; do not credit today's image as their proof.

2. **Native integration direction is supported, exact compatibility is not established.** Fresh official [MAF MCP guidance](https://learn.microsoft.com/en-us/agent-framework/agents/tools/local-mcp-tools) supports local stdio clients and agent tool integration. [CodeGraph documentation](https://github.com/colbymchenry/codegraph#MCP-Tools) documents native MCP and project-path selection. Moving documentation does not prove every described feature exists in pinned 1.5.0 or enforce Vulgata's snapshot scope. [JDT-LS documentation](https://github.com/eclipse-jdtls/eclipse.jdt.ls#running-from-the-command-line) supports Java 21 and separate platform configuration/workspace data; it does not prove the pinned standalone no-import/build/restore profile. The gateway and spine correctly preserve those qualification gaps and explicitly avoid equating that temporary profile with network air-gapping.

3. **Recovery and Chat lifetime remain application work.** [PostgreSQL dumps](https://www.postgresql.org/docs/17/backup-dump.html) provide database-consistent snapshots, while global objects require separate handling. [Docker volumes](https://docs.docker.com/engine/storage/volumes/) and [Data Protection guidance](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0) support persistent storage/key configuration, not coherent Vulgata restore or usable protected-data recovery by themselves. Current [ChatPage disposal](G:/source/repos/vulgata/src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs:559) cancels work; it does not prove host-owned same-turn continuation. AD-25–28/31 and the verification table already demand the missing behavior and tests.

## Limits and disposition

Only this report was written. No build, test, service, Docker, provider, database, installation or Git mutation was performed. Code checks were narrow and static; no new upgrade recommendation or full architecture survey was undertaken. The prior reality review and memory guided lookup/evidence separation, not acceptance of new gateway claims. Required spine/gateway fixes from this lens: **none**. Carry the existing qualification obligations into owning-feature contracts; this report does not satisfy them.
