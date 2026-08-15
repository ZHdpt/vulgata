using System.Security.Claims;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Data;
using Vulgata.Web.RequestPipeline;
using Vulgata.Web.Validators;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Web.Components.Pages.Management;

/// <summary>
/// Composition root for the /management dashboard. Owns data loading and routes panel
/// EventCallbacks to the appropriate coordinator + targeted panel refresh. Panels themselves
/// only render what they are given and fire events upward — no cross-panel state.
/// </summary>
public sealed partial class DashboardPage
{
    private List<SystemEntity>? _systems;
    private Dictionary<Guid, IReadOnlyList<RepositorySummaryDto>> _repositories = [];
    private Dictionary<Guid, IReadOnlyList<SystemLlmProviderOverrideSummaryDto>> _systemLlmOverrides = [];
    private List<RepositorySummaryDto> _standaloneRepositories = [];
    private Dictionary<Guid, DemoRepositoryScanSnapshot> _demoScanStates = [];
    private string? _loadError;
    private string? _saveError;
    private string? _repoError;
    private string? _standaloneRepoError;
    private bool _isAdministrator;
    private bool _isSaving;
    private bool _showForm;
    private bool _showRepositoryForm;
    private bool _showStandaloneRepositoryForm;
    private bool _showRepositoryDetail;
    private SystemEntity? _editingSystem;

    private IUserContext _userContext = new UserContext(new ClaimsPrincipal(), isAdministrator: false);

    private CreateSystemRequest _formModel = new();
    private CreateRepositoryRequest _repoFormModel = new();
    private CreateRepositoryRequest _standaloneRepoFormModel = new();
    private RepositorySummaryDto? _selectedRepository;
    private SystemEntity? _repoTargetSystem;

    private SystemEntity? _pendingDeleteSystem;
    private bool _showDeleteConfirmation;
    private SystemEntity? _ownerManagementSystem;
    private bool _showOwnerManagement;

    protected async override Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;
        _isAdministrator = principal.IsInRole(RoleNames.Administrator);
        _userContext = new UserContext(principal, _isAdministrator);

        await LoadSystemsAsync();
    }

    private async Task LoadSystemsAsync()
    {
        // Resolve services from an isolated scope to prevent DbContext concurrency
        // when the InteractiveServer double-render (pre-render + interactive) or
        // Npgsql retry-on-failure overlaps queries on the same VulgataDbContext.
        await using var scope = ScopeFactory.CreateAsyncScope();
        var systemRepo =
            scope.ServiceProvider.GetRequiredService<ISystemRepository>();
        var repoCoord =
            scope.ServiceProvider.GetRequiredService<IRepositoryManagementCoordinator>();
        var overrideCoord =
            scope.ServiceProvider.GetRequiredService<ISystemLlmProviderOverrideCoordinator>();

        try
        {
            _loadError = null;
            _systems = (await systemRepo.ListVisibleAsync(_userContext.ToSystemOwnerFilter())).ToList();
            _standaloneRepositories = (await repoCoord
                    .ListStandaloneVisibleAsync(_userContext))
                .ToList();
            _repositories = [];
            _systemLlmOverrides = [];

            foreach (var system in _systems)
            {
                var repositories = await repoCoord.ListVisibleAsync(
                    system.Id,
                    _userContext);
                _repositories[system.Id] = repositories.ToList();

                var overridesResult =
                    await overrideCoord.ListAsync(system.Id, _userContext);

                _systemLlmOverrides[system.Id] =
                    overridesResult.Outcome == SystemLlmProviderOverrideMutationOutcome.Success && overridesResult.Value is not null
                        ? overridesResult.Value.ToList()
                        : [];
            }

            RefreshDemoScanStates();
        }
        catch (Exception ex)
        {
            _loadError = $"加载系统列表失败：{ex.Message}";
            _systems = [];
            _standaloneRepositories = [];
            _repositories = [];
            _systemLlmOverrides = [];
            _demoScanStates = [];
        }
    }

    private async Task ReloadRepositoriesAsync(Guid systemId)
    {
        var repositories = await RepositoryManagementCoordinator.ListVisibleAsync(
            systemId,
            _userContext);
        _repositories[systemId] = repositories.ToList();
    }

    private async Task ReloadStandaloneRepositoriesAsync()
    {
        var repositories = await RepositoryManagementCoordinator.ListStandaloneVisibleAsync(
            _userContext);
        _standaloneRepositories = repositories.ToList();
    }

    private async Task ReloadSystemLlmOverridesAsync(Guid systemId)
    {
        var result =
            await SystemLlmProviderOverrideCoordinator.ListAsync(systemId, _userContext);

        _systemLlmOverrides[systemId] =
            result.Outcome == SystemLlmProviderOverrideMutationOutcome.Success && result.Value is not null
                ? result.Value.ToList()
                : [];
    }

    private void StartCreateSystem()
    {
        _editingSystem = null;
        _formModel = new CreateSystemRequest();
        _saveError = null;
        _showForm = true;
    }

    private void StartEditSystem(SystemEntity system)
    {
        _editingSystem = system;
        _formModel = new CreateSystemRequest
        {
            Name = system.Name,
            Description = system.Description,
            Context = system.Context,
        };
        _saveError = null;
        _showForm = true;
    }

    private void CancelForm()
    {
        _showForm = false;
        _editingSystem = null;
        _saveError = null;
    }

    private void StartCreateRepository(SystemEntity system)
    {
        _repoTargetSystem = system;
        _repoFormModel = new CreateRepositoryRequest();
        _repoError = null;
        _showRepositoryForm = true;
    }

    private void CancelRepositoryForm()
    {
        _showRepositoryForm = false;
        _repoTargetSystem = null;
        _repoFormModel = new CreateRepositoryRequest();
        _repoError = null;
    }

    private void StartCreateStandaloneRepository()
    {
        _standaloneRepoFormModel = new CreateRepositoryRequest();
        _standaloneRepoError = null;
        _showStandaloneRepositoryForm = true;
    }

    private void CancelStandaloneRepositoryForm()
    {
        _showStandaloneRepositoryForm = false;
        _standaloneRepoFormModel = new CreateRepositoryRequest();
        _standaloneRepoError = null;
    }

    private Task OnRepositorySelectedAsync(Guid repositoryId)
    {
        var match = FindRepositoryById(repositoryId);
        if (match is null)
        {
            return Task.CompletedTask;
        }

        _selectedRepository = match;
        _showRepositoryDetail = true;
        return Task.CompletedTask;
    }

    private void StartDemoScan(Guid repositoryId)
    {
        DemoScanUiState.Start(repositoryId);
        _demoScanStates[repositoryId] = DemoScanUiState.GetSnapshot(repositoryId);
    }

    private void OpenDemoScanProgress(Guid repositoryId)
    {
        NavigationManager.NavigateTo($"/management/demo-scan/{repositoryId}");
    }

    private void RefreshDemoScanStates()
    {
        _demoScanStates = _standaloneRepositories
            .Concat(_repositories.Values.SelectMany(static repositories => repositories))
            .ToDictionary(
                static repository => repository.Id,
                repository => DemoScanUiState.GetSnapshot(repository.Id));
    }

    private RepositorySummaryDto? FindRepositoryById(Guid repositoryId)
    {
        var standalone = _standaloneRepositories.FirstOrDefault(r => r.Id == repositoryId);
        if (standalone is not null)
        {
            return standalone;
        }

        foreach ((_, var repositories) in _repositories)
        {
            var match = repositories.FirstOrDefault(r => r.Id == repositoryId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private void CloseRepositoryDetail()
    {
        _showRepositoryDetail = false;
        _selectedRepository = null;
    }

    private async Task OnRepositoryDetailSavedAsync(RepositorySummaryDto repository)
    {
        // Refresh whichever list currently contains the saved repository so the panel re-renders
        // with up-to-date scan status / document counts (today: a no-op for summaries, but the
        // wiring is here for when scanning lands in Epic 4).
        if (repository.SystemId.HasValue)
        {
            await ReloadRepositoriesAsync(repository.SystemId.Value);
        }
        else
        {
            await ReloadStandaloneRepositoriesAsync();
        }
    }

    private async Task OnLlmOverrideAddedAsync(Guid systemId)
    {
        await ReloadSystemLlmOverridesAsync(systemId);
    }

    private async Task OnLlmOverrideRemovedAsync((Guid SystemId, Guid OverrideId) args)
    {
        await ReloadSystemLlmOverridesAsync(args.SystemId);
    }

    private async Task CreateRepositoryAsync()
    {
        if (_repoTargetSystem is null)
        {
            return;
        }

        _repoError = await ValidateRepositoryRequestAsync(_repoFormModel);
        if (_repoError is not null)
        {
            return;
        }

        var systemId = _repoTargetSystem.Id;

        try
        {
            var result = await RepositoryManagementCoordinator.CreateAsync(
                systemId,
                _repoFormModel,
                _userContext);

            if (result.Outcome != RepositoryMutationOutcome.Success || result.Value is null)
            {
                _repoError = BuildRepositoryErrorMessage(result, "系统不存在或无权操作。");
                return;
            }

            _showRepositoryForm = false;
            _repoTargetSystem = null;
            _repoFormModel = new CreateRepositoryRequest();
            await ReloadRepositoriesAsync(systemId);
        }
        catch (Exception ex)
        {
            _repoError = $"创建仓库失败：{ex.Message}";
        }
    }

    private async Task CreateStandaloneRepositoryAsync()
    {
        _standaloneRepoError = await ValidateRepositoryRequestAsync(_standaloneRepoFormModel);
        if (_standaloneRepoError is not null)
        {
            return;
        }

        try
        {
            var result = await RepositoryManagementCoordinator.CreateStandaloneAsync(
                _standaloneRepoFormModel,
                _userContext);

            if (result.Outcome != RepositoryMutationOutcome.Success || result.Value is null)
            {
                _standaloneRepoError = BuildRepositoryErrorMessage(result, "独立仓库创建失败。");
                return;
            }

            _showStandaloneRepositoryForm = false;
            _standaloneRepoFormModel = new CreateRepositoryRequest();
            await ReloadStandaloneRepositoriesAsync();
        }
        catch (Exception ex)
        {
            _standaloneRepoError = $"创建独立仓库失败：{ex.Message}";
        }
    }

    private static string BuildRepositoryErrorMessage<T>(RepositoryMutationResult<T> result, string defaultMessage)
    {
        return result.Outcome switch
        {
            RepositoryMutationOutcome.SystemNotFound => defaultMessage,
            RepositoryMutationOutcome.RepositoryNotFound => "仓库不存在。",
            RepositoryMutationOutcome.DuplicateName => result.Message ?? "仓库名称已存在。",
            RepositoryMutationOutcome.GitAuthenticationRequired or RepositoryMutationOutcome.GitUnreachable =>
                $"Git URL 不可达：{result.Message}",
            _ => defaultMessage,
        };
    }

    private async Task<string?> ValidateRepositoryRequestAsync(CreateRepositoryRequest request)
    {
        var validationResult = await RepositoryRequestValidator.ValidateAsync(request);
        if (validationResult.IsValid)
        {
            return null;
        }

        return validationResult.Errors.Select(error => error.ErrorMessage).FirstOrDefault();
    }

    private void OpenOwnerManagement(SystemEntity system)
    {
        _ownerManagementSystem = system;
        _showOwnerManagement = true;
    }

    private Task OnOwnerManagementVisibilityChanged(bool visible)
    {
        _showOwnerManagement = visible;

        if (!visible)
        {
            _ownerManagementSystem = null;
        }

        return Task.CompletedTask;
    }

    private async Task SaveSystemAsync()
    {
        if (_isSaving) return;

        _isSaving = true;
        _saveError = null;

        try
        {
            if (_editingSystem is null)
            {
                bool nameExists = await SystemRepository.NameExistsAsync(_formModel.Name);
                if (nameExists)
                {
                    _saveError = "系统名称已存在。";
                    _isSaving = false;
                    return;
                }

                SystemEntity newSystem = new(
                    _formModel.Name,
                    _formModel.Description,
                    _formModel.Context,
                    DateTimeOffset.UtcNow);

                await SystemRepository.AddAsync(newSystem);
                await SystemRepository.SaveChangesAsync();
            }
            else
            {
                bool nameExists = await SystemRepository.NameExistsAsync(_formModel.Name, _editingSystem.Id);
                if (nameExists)
                {
                    _saveError = "系统名称已存在。";
                    _isSaving = false;
                    return;
                }

                var existing = await SystemRepository.GetByIdAsync(_editingSystem.Id);
                if (existing is null)
                {
                    _saveError = "系统不存在或已被删除。";
                    _isSaving = false;
                    return;
                }

                existing.UpdateDetails(
                    _formModel.Name,
                    _formModel.Description,
                    _formModel.Context,
                    DateTimeOffset.UtcNow);

                await SystemRepository.SaveChangesAsync();
            }

            _showForm = false;
            _editingSystem = null;
            await LoadSystemsAsync();
        }
        catch (Exception ex)
        {
            _saveError = $"保存失败：{ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private Task DeleteSystemAsync(SystemEntity system)
    {
        _pendingDeleteSystem = system;
        _showDeleteConfirmation = true;
        return Task.CompletedTask;
    }

    private void CancelDelete()
    {
        _pendingDeleteSystem = null;
        _showDeleteConfirmation = false;
        _loadError = null;
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_pendingDeleteSystem is null) return;

        var system = _pendingDeleteSystem;
        _pendingDeleteSystem = null;
        _showDeleteConfirmation = false;

        try
        {
            var result = await SystemRepository.DeleteIfNoDependenciesAsync(system.Id);

            if (result.Outcome == SystemDeleteOutcome.Deleted)
            {
                await SystemRepository.SaveChangesAsync();
                await LoadSystemsAsync();
            }
            else if (result.Outcome == SystemDeleteOutcome.HasDependencies)
            {
                _loadError = "请先移除依赖（仓库或系统所有者分配），再删除该系统。";
            }
            else
            {
                _loadError = "系统不存在或已被删除。";
            }
        }
        catch (Exception ex)
        {
            _loadError = $"删除失败：{ex.Message}";
        }
    }
}
