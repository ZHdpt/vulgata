using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public class IdentityRegistrationTests
{
    private static string GetRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vulgata.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Repository root could not be found. Ensure Vulgata.slnx exists at the solution root.");
    }

    private static IReadOnlyList<ValidationResult> ValidateRegisterInput(string email, string password, string confirmPassword)
    {
        object input = CreateRegisterInput(email, password, confirmPassword);

        ValidationContext context = new(input);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(input, context, results, validateAllProperties: true);
        return results;
    }

    private static Type GetRegisterComponentType() =>
        typeof(ApplicationUser).Assembly.GetType("Vulgata.Web.Components.Account.Pages.Register")
        ?? throw new InvalidOperationException("Register component type could not be found.");

    private static object CreateRegisterInput(string email, string password, string confirmPassword)
    {
        Type inputModelType = GetRegisterComponentType().GetNestedType("InputModel", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Register input model type could not be found.");
        object input = Activator.CreateInstance(inputModelType, nonPublic: true)
            ?? throw new InvalidOperationException("Register input model could not be created.");

        inputModelType.GetProperty("Email")!.SetValue(input, email);
        inputModelType.GetProperty("Password")!.SetValue(input, password);
        inputModelType.GetProperty("ConfirmPassword")!.SetValue(input, confirmPassword);
        return input;
    }

    private static RegistrationHarness CreateRegistrationHarness(
        string email,
        string password,
        string confirmPassword,
        IdentityResult? createResult = null,
        string? returnUrl = null)
    {
        Type registerType = GetRegisterComponentType();
        object component = Activator.CreateInstance(registerType, nonPublic: true)
            ?? throw new InvalidOperationException("Register component could not be created.");
        object input = CreateRegisterInput(email, password, confirmPassword);

        TestUserStore userStore = new();
        TestUserManager userManager = new(userStore)
        {
            CreateResult = createResult ?? IdentityResult.Success,
        };
        TestSignInManager signInManager = new(userManager);
        TestEmailSender emailSender = new();
        RecordingNavigationManager navigationManager = new();

        SetProperty(component, "UserManager", userManager);
        SetProperty(component, "UserStore", userStore);
        SetProperty(component, "SignInManager", signInManager);
        SetProperty(component, "EmailSender", emailSender);
        SetProperty(component, "Logger", GetNullLogger(registerType));
        SetProperty(component, "NavigationManager", navigationManager);
        SetProperty(component, "RedirectManager", CreateIdentityRedirectManager(navigationManager));
        SetProperty(component, "Input", input);
        SetProperty(component, "ReturnUrl", returnUrl);

        return new RegistrationHarness(component, input, userStore, userManager, signInManager, emailSender, navigationManager);
    }

    private static RegistrationHarness CreateExecutableRegistrationHarness(
        string email,
        string password,
        string confirmPassword,
        IEnumerable<ApplicationUser>? existingUsers = null,
        string? returnUrl = null)
    {
        Type registerType = GetRegisterComponentType();
        object component = Activator.CreateInstance(registerType, nonPublic: true)
            ?? throw new InvalidOperationException("Register component could not be created.");
        object input = CreateRegisterInput(email, password, confirmPassword);

        TestUserStore userStore = new(existingUsers);
        UserManager<ApplicationUser> userManager = CreateExecutableUserManager(userStore);
        TestSignInManager signInManager = new(userManager);
        TestEmailSender emailSender = new();
        RecordingNavigationManager navigationManager = new();

        SetProperty(component, "UserManager", userManager);
        SetProperty(component, "UserStore", userStore);
        SetProperty(component, "SignInManager", signInManager);
        SetProperty(component, "EmailSender", emailSender);
        SetProperty(component, "Logger", GetNullLogger(registerType));
        SetProperty(component, "NavigationManager", navigationManager);
        SetProperty(component, "RedirectManager", CreateIdentityRedirectManager(navigationManager));
        SetProperty(component, "Input", input);
        SetProperty(component, "ReturnUrl", returnUrl);

        return new RegistrationHarness(component, input, userStore, userManager, signInManager, emailSender, navigationManager);
    }

    private static UserManager<ApplicationUser> CreateExecutableUserManager(
        TestUserStore store,
        IPasswordHasher<ApplicationUser>? passwordHasher = null,
        IdentityErrorDescriber? errorDescriber = null) =>
        new ExecutableTestUserManager(
            store,
            passwordHasher ?? new BcryptPasswordHasher<ApplicationUser>(),
            errorDescriber ?? new ChineseIdentityErrorDescriber());

    private static IdentityOptions CreateProductionIdentityOptions()
    {
        IdentityOptions options = new();
        IdentityOptionsConfiguration.Configure(options);
        return options;
    }

    private static object CreateIdentityRedirectManager(NavigationManager navigationManager)
    {
        Type redirectManagerType = typeof(ApplicationUser).Assembly.GetType("Vulgata.Web.Components.Account.IdentityRedirectManager")
            ?? throw new InvalidOperationException("Identity redirect manager type could not be found.");

        return Activator.CreateInstance(
                   redirectManagerType,
                   BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                   binder: null,
                   args: [navigationManager],
                   culture: null)
               ?? throw new InvalidOperationException("Identity redirect manager could not be created.");
    }

    private static object GetNullLogger(Type componentType) =>
        typeof(NullLogger<>).MakeGenericType(componentType)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null)
        ?? typeof(NullLogger<>).MakeGenericType(componentType)
            .GetField("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null)
        ?? Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(componentType))
        ?? throw new InvalidOperationException("A null logger instance could not be created.");

    private static void SetProperty(object instance, string name, object? value)
    {
        PropertyInfo property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property '{name}' could not be found on '{instance.GetType().FullName}'.");
        property.SetValue(instance, value);
    }

    private static object? GetPropertyValue(object instance, string name)
    {
        PropertyInfo property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property '{name}' could not be found on '{instance.GetType().FullName}'.");
        return property.GetValue(instance);
    }

    [Fact]
    public void HashPasswordUsesBcryptPrefix()
    {
        BcryptPasswordHasher<ApplicationUser> hasher = new();

        string hash = hasher.HashPassword(new ApplicationUser(), "Aa1!Aa1!");

        Assert.True(
            hash.StartsWith("$2a$", StringComparison.Ordinal)
            || hash.StartsWith("$2b$", StringComparison.Ordinal)
            || hash.StartsWith("$2y$", StringComparison.Ordinal));
    }

    [Fact]
    public void VerifyHashedPasswordReturnsSuccessForMatchingPassword()
    {
        ApplicationUser user = new();
        BcryptPasswordHasher<ApplicationUser> hasher = new();
        string hash = hasher.HashPassword(user, "Aa1!Aa1!");

        PasswordVerificationResult result = hasher.VerifyHashedPassword(user, hash, "Aa1!Aa1!");

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPasswordReturnsFailedForDifferentPassword()
    {
        ApplicationUser user = new();
        BcryptPasswordHasher<ApplicationUser> hasher = new();
        string hash = hasher.HashPassword(user, "Aa1!Aa1!");

        PasswordVerificationResult result = hasher.VerifyHashedPassword(user, hash, "Different1!");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyHashedPasswordReturnsFailedForLegacyPbkdf2HashWithoutThrowing()
    {
        ApplicationUser user = new();
        string legacyHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "Aa1!Aa1!");
        BcryptPasswordHasher<ApplicationUser> hasher = new();

        PasswordVerificationResult result = hasher.VerifyHashedPassword(user, legacyHash, "Aa1!Aa1!");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void ChineseIdentityErrorDescriberReturnsChineseRegistrationMessages()
    {
        ChineseIdentityErrorDescriber describer = new();

        Assert.Equal("该邮箱已被注册", describer.DuplicateEmail("user@example.com").Description);
        Assert.Equal("邮箱格式无效", describer.InvalidEmail("invalid-email").Description);
        Assert.Equal("密码长度不能少于 8 位", describer.PasswordTooShort(8).Description);
        Assert.Equal("密码必须包含数字", describer.PasswordRequiresDigit().Description);
        Assert.Equal("密码必须包含小写字母", describer.PasswordRequiresLower().Description);
        Assert.Equal("密码必须包含大写字母", describer.PasswordRequiresUpper().Description);
        Assert.Equal("密码必须包含特殊字符", describer.PasswordRequiresNonAlphanumeric().Description);
        Assert.Equal("密码与确认密码不一致", describer.PasswordMismatch().Description);
        Assert.Equal("发生未知错误，请重试。", describer.DefaultError().Description);
    }

    [Fact]
    public void RegistrationComponentUsesChineseValidationMessages()
    {
        string repoRoot = GetRepoRoot();
        string registerPage = Path.Combine(repoRoot, "src", "dotnet", "Vulgata.Web", "Components", "Account", "Pages", "Register.razor");
        string content = File.ReadAllText(registerPage);

        Assert.Contains("[Required(ErrorMessage = \"邮箱字段是必需的。\")]", content, StringComparison.Ordinal);
        Assert.Contains("[EmailAddress(ErrorMessage = \"邮箱字段不是有效的电子邮件地址。\")]", content, StringComparison.Ordinal);
        Assert.Contains("[Display(Name = \"邮箱\")]", content, StringComparison.Ordinal);
        Assert.Contains("[Required(ErrorMessage = \"密码字段是必需的。\")]", content, StringComparison.Ordinal);
        Assert.Contains("[StringLength(100, ErrorMessage = \"{0}长度必须在 {2} 到 {1} 之间。\", MinimumLength = 8)]", content, StringComparison.Ordinal);
        Assert.Contains("[DataType(DataType.Password)]", content, StringComparison.Ordinal);
        Assert.Contains("[Display(Name = \"密码\")]", content, StringComparison.Ordinal);
        Assert.Contains("[Display(Name = \"确认密码\")]", content, StringComparison.Ordinal);
        Assert.Contains("[Compare(\"Password\", ErrorMessage = \"密码与确认密码不一致。\")]", content, StringComparison.Ordinal);
    }

    [Fact]
    public void DbContextSourceFilesConfigureExpectedSchemas()
    {
        string repoRoot = GetRepoRoot();
        string identityDbContext = Path.Combine(repoRoot, "src", "dotnet", "Vulgata.Web", "Data", "ApplicationDbContext.cs");
        string domainDbContext = Path.Combine(repoRoot, "src", "dotnet", "Vulgata.Infrastructure", "Data", "VulgataDbContext.cs");
        string identityContent = File.ReadAllText(identityDbContext);
        string domainContent = File.ReadAllText(domainDbContext);

        Assert.Contains("builder.HasDefaultSchema(\"identity\")", identityContent, StringComparison.Ordinal);
        Assert.Contains("modelBuilder.HasDefaultSchema(\"vulgata\")", domainContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterUserSignsInAndRedirectsToChatPageAfterSuccessfulRegistration()
    {
        RegistrationHarness harness = CreateRegistrationHarness(
            email: "user@example.com",
            password: "Aa1!Aa1!",
            confirmPassword: "Aa1!Aa1!");

        await harness.RegisterAsync();

        Assert.True(harness.UserStore.SetUserNameCalled);
        Assert.True(harness.UserStore.SetEmailCalled);
        Assert.Equal("user@example.com", harness.UserStore.StoredUserName);
        Assert.Equal("user@example.com", harness.UserStore.StoredEmail);
        Assert.True(harness.SignInManager.SignInCalled);
        Assert.False(harness.SignInManager.IsPersistent);
        Assert.Equal("/", harness.NavigationManager.LastNavigatedUri);
        Assert.Equal("user@example.com", harness.EmailSender.LastEmail);
    }

    [Fact]
    public async Task RegisterUserStoresBcryptPasswordHashThroughIdentityPipeline()
    {
        RegistrationHarness harness = CreateExecutableRegistrationHarness(
            email: "new.user@example.com",
            password: "Aa1!Aa1!",
            confirmPassword: "Aa1!Aa1!");

        await harness.RegisterAsync();

        Assert.NotNull(harness.UserStore.StoredPasswordHash);
        Assert.True(
            harness.UserStore.StoredPasswordHash!.StartsWith("$2a$", StringComparison.Ordinal)
            || harness.UserStore.StoredPasswordHash.StartsWith("$2b$", StringComparison.Ordinal)
            || harness.UserStore.StoredPasswordHash.StartsWith("$2y$", StringComparison.Ordinal));
        Assert.True(harness.SignInManager.SignInCalled);
        Assert.Equal("/", harness.NavigationManager.LastNavigatedUri);
    }

    [Fact]
    public async Task RegisterUserRejectsDuplicateEmailThroughProductionIdentityConfiguration()
    {
        ApplicationUser existingUser = new()
        {
            Id = "existing-user-id",
            UserName = "duplicate@example.com",
            NormalizedUserName = "DUPLICATE@EXAMPLE.COM",
            Email = "duplicate@example.com",
            NormalizedEmail = "DUPLICATE@EXAMPLE.COM",
        };

        RegistrationHarness harness = CreateExecutableRegistrationHarness(
            email: "duplicate@example.com",
            password: "Aa1!Aa1!",
            confirmPassword: "Aa1!Aa1!",
            existingUsers: [existingUser]);

        await harness.RegisterAsync();

        Assert.False(harness.SignInManager.SignInCalled);
        Assert.Null(harness.NavigationManager.LastNavigatedUri);
        Assert.Null(harness.UserStore.CreatedUser);
        Assert.Equal("错误：该邮箱已被注册", harness.Message);
    }

    [Fact]
    public async Task RegisterUserReturnsChineseDuplicateEmailMessageWhenCreationFails()
    {
        IdentityResult duplicateEmailFailure = IdentityResult.Failed(
            new IdentityError
            {
                Code = nameof(IdentityErrorDescriber.DuplicateEmail),
                Description = "该邮箱已被注册",
            });
        RegistrationHarness harness = CreateRegistrationHarness(
            email: "duplicate@example.com",
            password: "Aa1!Aa1!",
            confirmPassword: "Aa1!Aa1!",
            createResult: duplicateEmailFailure);

        await harness.RegisterAsync();

        Assert.False(harness.SignInManager.SignInCalled);
        Assert.Null(harness.NavigationManager.LastNavigatedUri);
        Assert.Equal("错误：该邮箱已被注册", harness.Message);
    }

    [Fact]
    public void ProgramConfigurationKeepsRegistrationRequirementsEnabled()
    {
        string repoRoot = GetRepoRoot();
        string programFile = Path.Combine(repoRoot, "src", "dotnet", "Vulgata.Web", "Program.cs");
        string content = File.ReadAllText(programFile);
        IdentityOptions options = CreateProductionIdentityOptions();

        Assert.Contains("AddIdentityCore<ApplicationUser>(IdentityOptionsConfiguration.Configure)", content, StringComparison.Ordinal);
        Assert.Contains(".AddErrorDescriber<ChineseIdentityErrorDescriber>()", content, StringComparison.Ordinal);
        Assert.Contains("AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher<ApplicationUser>>()", content, StringComparison.Ordinal);
        Assert.Contains("await identityDb.Database.MigrateAsync();", content, StringComparison.Ordinal);
        Assert.Contains("await vulgataDb.Database.MigrateAsync();", content, StringComparison.Ordinal);

        Assert.False(options.SignIn.RequireConfirmedAccount);
        Assert.True(options.User.RequireUniqueEmail);
        Assert.Equal(IdentitySchemaVersions.Version3, options.Stores.SchemaVersion);
        Assert.Equal(8, options.Password.RequiredLength);
        Assert.True(options.Password.RequireDigit);
        Assert.True(options.Password.RequireLowercase);
        Assert.True(options.Password.RequireUppercase);
        Assert.True(options.Password.RequireNonAlphanumeric);
    }

    [Fact]
    public void RegisterInputModelReturnsChineseValidationMessages()
    {
        IReadOnlyList<ValidationResult> emptyEmailResults = ValidateRegisterInput("", "Aa1!Aa1!", "Aa1!Aa1!");
        IReadOnlyList<ValidationResult> invalidEmailResults = ValidateRegisterInput("invalid-email", "Aa1!Aa1!", "Aa1!Aa1!");
        IReadOnlyList<ValidationResult> shortPasswordResults = ValidateRegisterInput("user@example.com", "abc", "abc");
        IReadOnlyList<ValidationResult> mismatchResults = ValidateRegisterInput("user@example.com", "Aa1!Aa1!", "Mismatch1!");

        Assert.Contains(emptyEmailResults, result => result.ErrorMessage == "邮箱字段是必需的。");
        Assert.Contains(invalidEmailResults, result => result.ErrorMessage == "邮箱字段不是有效的电子邮件地址。");
        Assert.Contains(shortPasswordResults, result => result.ErrorMessage == "密码长度必须在 8 到 100 之间。");
        Assert.Contains(mismatchResults, result => result.ErrorMessage == "密码与确认密码不一致。");
    }

    [Fact]
    public async Task PasswordSignInAsyncFailsCleanlyForLegacyPbkdf2Hashes()
    {
        ApplicationUser legacyUser = new()
        {
            Id = "legacy-user-id",
            UserName = "legacy@example.com",
            NormalizedUserName = "LEGACY@EXAMPLE.COM",
            Email = "legacy@example.com",
            NormalizedEmail = "LEGACY@EXAMPLE.COM",
        };
        legacyUser.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(legacyUser, "Aa1!Aa1!");

        TestUserStore userStore = new([legacyUser]);
        UserManager<ApplicationUser> userManager = CreateExecutableUserManager(userStore);
        TestSignInManager signInManager = new(userManager);

        SignInResult result = await signInManager.PasswordSignInAsync(
            "legacy@example.com",
            "Aa1!Aa1!",
            isPersistent: false,
            lockoutOnFailure: false);

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotAllowed);
        Assert.False(result.IsLockedOut);
        Assert.False(result.RequiresTwoFactor);
        Assert.False(signInManager.SignInCalled);
    }

    private sealed class RegistrationHarness(
        object component,
        object input,
        TestUserStore userStore,
        UserManager<ApplicationUser> userManager,
        TestSignInManager signInManager,
        TestEmailSender emailSender,
        RecordingNavigationManager navigationManager)
    {
        public TestUserStore UserStore { get; } = userStore;

        public UserManager<ApplicationUser> UserManager { get; } = userManager;

        public TestSignInManager SignInManager { get; } = signInManager;

        public TestEmailSender EmailSender { get; } = emailSender;

        public RecordingNavigationManager NavigationManager { get; } = navigationManager;

        public string? Message => GetPropertyValue(component, "Message") as string;

        public async Task RegisterAsync()
        {
            MethodInfo registerUser = component.GetType().GetMethod("RegisterUser", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException("RegisterUser method could not be found.");

            Task execution = (Task)(registerUser.Invoke(component, [new EditContext(input)])
                ?? throw new InvalidOperationException("RegisterUser invocation returned null."));
            await execution;
        }
    }

    private sealed class TestUserStore : IUserEmailStore<ApplicationUser>, IUserPasswordStore<ApplicationUser>, IUserRoleStore<ApplicationUser>
    {
        private readonly Dictionary<string, ApplicationUser> _usersById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ApplicationUser> _usersByNormalizedEmail = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ApplicationUser> _usersByNormalizedUserName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _rolesByUserId = new(StringComparer.Ordinal);

        public TestUserStore(IEnumerable<ApplicationUser>? seededUsers = null)
        {
            if (seededUsers is null)
            {
                return;
            }

            foreach (ApplicationUser user in seededUsers)
            {
                TrackUser(user);
            }
        }

        public bool SetUserNameCalled { get; private set; }

        public bool SetEmailCalled { get; private set; }

        public string? StoredUserName { get; private set; }

        public string? StoredEmail { get; private set; }

        public string? StoredPasswordHash { get; private set; }

        public ApplicationUser? CreatedUser { get; private set; }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            user.Id ??= Guid.NewGuid().ToString("N");
            CreatedUser = user;
            TrackUser(user);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            UntrackUser(user);
            return Task.FromResult(IdentityResult.Success);
        }

        public void Dispose()
        {
        }

        public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            _usersByNormalizedEmail.TryGetValue(normalizedEmail, out ApplicationUser? user);
            return Task.FromResult(user);
        }

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            _usersById.TryGetValue(userId, out ApplicationUser? user);
            return Task.FromResult(user);
        }

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            _usersByNormalizedUserName.TryGetValue(normalizedUserName, out ApplicationUser? user);
            return Task.FromResult(user);
        }

        public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.Email);

        public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedEmail);

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedUserName);

        public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.PasswordHash);

        public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(user.Id)
                || !_rolesByUserId.TryGetValue(user.Id, out HashSet<string>? roles))
            {
                return Task.FromResult<IList<string>>([]);
            }

            return Task.FromResult<IList<string>>(roles.OrderBy(role => role, StringComparer.Ordinal).ToArray());
        }

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.Id);

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.UserName);

        public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

        public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(user.Id)
                || !_rolesByUserId.TryGetValue(user.Id, out HashSet<string>? roles))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(roles.Contains(CanonicalizeRoleName(roleName)));
        }

        public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(user.Id))
            {
                throw new InvalidOperationException("Users must have an identifier before roles can be assigned.");
            }

            if (!_rolesByUserId.TryGetValue(user.Id, out HashSet<string>? roles))
            {
                roles = new HashSet<string>(StringComparer.Ordinal);
                _rolesByUserId[user.Id] = roles;
            }

            roles.Add(CanonicalizeRoleName(roleName));
            TrackUser(user);
            return Task.CompletedTask;
        }

        public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(user.Id)
                && _rolesByUserId.TryGetValue(user.Id, out HashSet<string>? roles))
            {
                roles.Remove(CanonicalizeRoleName(roleName));
                if (roles.Count == 0)
                {
                    _rolesByUserId.Remove(user.Id);
                }
            }

            return Task.CompletedTask;
        }

        public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            string canonicalRole = CanonicalizeRoleName(roleName);
            ApplicationUser[] users = _usersById.Values
                .Where(user => !string.IsNullOrEmpty(user.Id)
                    && _rolesByUserId.TryGetValue(user.Id, out HashSet<string>? roles)
                    && roles.Contains(canonicalRole))
                .DistinctBy(user => user.Id, StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult<IList<ApplicationUser>>(users);
        }

        public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
        {
            SetEmailCalled = true;
            StoredEmail = email;
            user.Email = email;
            return Task.CompletedTask;
        }

        public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
        {
            user.NormalizedEmail = normalizedEmail;
            if (!string.IsNullOrEmpty(normalizedEmail))
            {
                _usersByNormalizedEmail[normalizedEmail] = user;
            }
            return Task.CompletedTask;
        }

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            if (!string.IsNullOrEmpty(normalizedName))
            {
                _usersByNormalizedUserName[normalizedName] = user;
            }
            return Task.CompletedTask;
        }

        public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
        {
            StoredPasswordHash = passwordHash;
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            SetUserNameCalled = true;
            StoredUserName = userName;
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            TrackUser(user);
            return Task.FromResult(IdentityResult.Success);
        }

        private void TrackUser(ApplicationUser user)
        {
            if (!string.IsNullOrEmpty(user.Id))
            {
                _usersById[user.Id] = user;
            }

            if (!string.IsNullOrEmpty(user.NormalizedEmail))
            {
                _usersByNormalizedEmail[user.NormalizedEmail] = user;
            }

            if (!string.IsNullOrEmpty(user.NormalizedUserName))
            {
                _usersByNormalizedUserName[user.NormalizedUserName] = user;
            }
        }

        private void UntrackUser(ApplicationUser user)
        {
            if (!string.IsNullOrEmpty(user.Id))
            {
                _usersById.Remove(user.Id);
                _rolesByUserId.Remove(user.Id);
            }

            if (!string.IsNullOrEmpty(user.NormalizedEmail))
            {
                _usersByNormalizedEmail.Remove(user.NormalizedEmail);
            }

            if (!string.IsNullOrEmpty(user.NormalizedUserName))
            {
                _usersByNormalizedUserName.Remove(user.NormalizedUserName);
            }
        }

        private static string CanonicalizeRoleName(string roleName) => roleName.ToUpperInvariant() switch
        {
            "ADMINISTRATOR" => RoleNames.Administrator,
            "SYSTEMOWNER" => RoleNames.SystemOwner,
            "USER" => RoleNames.User,
            _ => roleName,
        };
    }

    private sealed class TestUserManager(TestUserStore store) : UserManager<ApplicationUser>(
        store,
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions
        {
            SignIn = { RequireConfirmedAccount = false },
            Password =
            {
                RequiredLength = 8,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireNonAlphanumeric = true,
            },
        }),
        new PasswordHasher<ApplicationUser>(),
        Array.Empty<IUserValidator<ApplicationUser>>(),
        Array.Empty<IPasswordValidator<ApplicationUser>>(),
        new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(),
        new NullServiceProvider(),
        NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public IdentityResult CreateResult { get; set; } = IdentityResult.Success;

        public override Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
        {
            user.Id ??= "test-user-id";

            if (!CreateResult.Succeeded)
            {
                return Task.FromResult(CreateResult);
            }

            return store.CreateAsync(user, CancellationToken.None);
        }

        public override Task<string> GenerateEmailConfirmationTokenAsync(ApplicationUser user) =>
            Task.FromResult("confirmation-token");

        public override Task<string> GetUserIdAsync(ApplicationUser user) =>
            Task.FromResult(user.Id ?? "test-user-id");
    }

    private sealed class ExecutableTestUserManager(
        TestUserStore store,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IdentityErrorDescriber errorDescriber) : UserManager<ApplicationUser>(
        store,
        Microsoft.Extensions.Options.Options.Create(IdentityRegistrationTests.CreateProductionIdentityOptions()),
        passwordHasher,
        new IUserValidator<ApplicationUser>[] { new UserValidator<ApplicationUser>(errorDescriber) },
        new IPasswordValidator<ApplicationUser>[] { new PasswordValidator<ApplicationUser>(errorDescriber) },
        new UpperInvariantLookupNormalizer(),
        errorDescriber,
        new NullServiceProvider(),
        NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public override Task<string> GenerateEmailConfirmationTokenAsync(ApplicationUser user) =>
            Task.FromResult("confirmation-token");
    }

    private sealed class TestSignInManager(UserManager<ApplicationUser> userManager) : SignInManager<ApplicationUser>(
        userManager,
        new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
        new TestClaimsPrincipalFactory(),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions { SignIn = { RequireConfirmedAccount = false } }),
        NullLogger<SignInManager<ApplicationUser>>.Instance,
        new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions())),
        new TestUserConfirmation())
    {
        public bool SignInCalled { get; private set; }

        public bool IsPersistent { get; private set; }

        public override Task SignInAsync(ApplicationUser user, bool isPersistent, string? authenticationMethod = null)
        {
            SignInCalled = true;
            IsPersistent = isPersistent;
            return Task.CompletedTask;
        }
    }

    private sealed class TestEmailSender : IEmailSender<ApplicationUser>
    {
        public string? LastEmail { get; private set; }

        public string? LastConfirmationLink { get; private set; }

        public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
        {
            LastEmail = email;
            LastConfirmationLink = confirmationLink;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
            Task.CompletedTask;

        public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
            Task.CompletedTask;
    }

    private sealed class TestClaimsPrincipalFactory : IUserClaimsPrincipalFactory<ApplicationUser>
    {
        public Task<ClaimsPrincipal> CreateAsync(ApplicationUser user) =>
            Task.FromResult(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    private sealed class TestUserConfirmation : IUserConfirmation<ApplicationUser>
    {
        public Task<bool> IsConfirmedAsync(UserManager<ApplicationUser> manager, ApplicationUser user) =>
            Task.FromResult(true);
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/Account/Register");
        }

        public string? LastNavigatedUri { get; private set; }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            LastNavigatedUri = uri;
        }
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}