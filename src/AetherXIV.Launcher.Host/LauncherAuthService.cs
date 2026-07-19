using AetherXIV.Core;
using AetherXIV.Data;
using AetherXIV.Launcher.Contracts;
using System.Net.Mail;

namespace AetherXIV.Launcher.Host;

public sealed class LauncherAuthService
{
    private readonly ILauncherAccountSessionRepository repository;
    private readonly LauncherServiceOptions options;
    private readonly IClock clock;

    public LauncherAuthService(
        ILauncherAccountSessionRepository repository,
        LauncherServiceOptions options,
        IClock clock)
    {
        this.repository = repository;
        this.options = options;
        this.clock = clock;
    }

    public async ValueTask<LauncherAuthResponse> LoginAsync(
        LauncherAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        string username = NormalizeUsername(request.Username);
        if (username.Length == 0)
            return Failed("Username is required.");
        if (String.IsNullOrWhiteSpace(request.Password))
            return Failed("Password is required.");

        LauncherAccountRecord? account = await repository.FindAccountByLoginAsync(username, cancellationToken).ConfigureAwait(false);
        if (account is null)
            return Failed("Incorrect username or password.");
        if (!LauncherPasswordHasher.Verify(request.Password, account.PasswordHashSha224, account.PasswordSalt))
            return Failed("Incorrect username or password.");

        string session = await repository.RefreshOrCreateSessionAsync(
            account.Id,
            clock.UtcNow.Add(options.SessionLifetime),
            cancellationToken).ConfigureAwait(false);

        return new LauncherAuthResponse(true, "Login accepted.", account.LoginName, session);
    }

    public async ValueTask<LauncherAuthResponse> CreateAccountAsync(
        LauncherCreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!options.AllowLocalAccountCreation)
            return Failed("Local account creation is disabled.");

        string username = NormalizeUsername(request.Username);
        if (username.Length == 0)
            return Failed("Username is required.");
        if (String.IsNullOrWhiteSpace(request.Password))
            return Failed("Password is required.");
        if (!String.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return Failed("Passwords do not match.");
        string email = NormalizeEmail(request.Email);
        if (!IsValidEmail(email))
            return Failed("A valid email address is required.");

        string salt = LauncherPasswordHasher.GenerateSalt();
        string passwordHash = LauncherPasswordHasher.HashPassword(request.Password, salt);
        LauncherAccountRecord? account = await repository.CreateAccountAsync(
            username,
            passwordHash,
            salt,
            email,
            cancellationToken).ConfigureAwait(false);
        if (account is null)
            return Failed("Username is already in use.");

        string session = await repository.RefreshOrCreateSessionAsync(
            account.Id,
            clock.UtcNow.Add(options.SessionLifetime),
            cancellationToken).ConfigureAwait(false);

        return new LauncherAuthResponse(
            true,
            "Account credentials created.",
            account.LoginName,
            session);
    }

    private static LauncherAuthResponse Failed(string message) => new(false, message, null, null);

    private static string NormalizeUsername(string? username) => (username ?? "").Trim();

    private static string NormalizeEmail(string? email) => (email ?? "").Trim();

    private static bool IsValidEmail(string email)
    {
        if (email.Length == 0)
            return false;

        try
        {
            MailAddress address = new(email);
            return String.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
