using AetherXIV.Core;
using AetherXIV.Data;
using AetherXIV.Launcher.Contracts;
using AetherXIV.Launcher.Host;

namespace AetherXIV.Launcher.Host.Tests;

public sealed class LauncherAuthServiceTests
{
    [Fact]
    public void Sha224MatchesKnownVectors()
    {
        Assert.Equal(
            "d14a028c2a3a2bc9476102bb288234c415a2b01f828ea62ac5b3e42f",
            Convert.ToHexString(LauncherPasswordHasher.ComputeSha224([])).ToLowerInvariant());
        Assert.Equal(
            "23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7",
            Convert.ToHexString(LauncherPasswordHasher.ComputeSha224("abc"u8)).ToLowerInvariant());
    }

    [Fact]
    public async Task LoginCreatesSessionForExistingAccount()
    {
        string salt = "0123456789abcdef0123456789abcdef0123456789abcdef01234567";
        FakeLauncherAccountSessionRepository repository = new()
        {
            ExistingAccount = new LauncherAccountRecord(
                new AccountId(7),
                "tester",
                DateTimeOffset.UtcNow,
                LauncherPasswordHasher.HashPassword("password", salt),
                salt,
                "tester@example.test"),
            SessionToken = "abc123"
        };
        LauncherAuthService service = CreateService(repository);

        LauncherAuthResponse response = await service.LoginAsync(new LauncherAuthRequest(" tester ", "password"));

        Assert.True(response.Success);
        Assert.Equal("tester", response.Username);
        Assert.Equal("abc123", response.SessionId);
        Assert.Equal(new AccountId(7), repository.RefreshedAccountId);
    }

    [Fact]
    public async Task LoginRejectsMissingOrInvalidCredentials()
    {
        string salt = "0123456789abcdef0123456789abcdef0123456789abcdef01234567";
        FakeLauncherAccountSessionRepository repository = new()
        {
            ExistingAccount = new LauncherAccountRecord(
                new AccountId(7),
                "tester",
                DateTimeOffset.UtcNow,
                LauncherPasswordHasher.HashPassword("correct", salt),
                salt,
                "tester@example.test")
        };
        LauncherAuthService service = CreateService(repository);

        LauncherAuthResponse response = await service.LoginAsync(new LauncherAuthRequest("missing", "password"));
        LauncherAuthResponse wrongPassword = await service.LoginAsync(new LauncherAuthRequest("tester", "wrong"));

        Assert.False(response.Success);
        Assert.Null(response.SessionId);
        Assert.Equal("Incorrect username or password.", response.Message);
        Assert.False(wrongPassword.Success);
        Assert.Null(wrongPassword.SessionId);
        Assert.Equal("Incorrect username or password.", wrongPassword.Message);
    }

    [Fact]
    public async Task CreateAccountStoresLauncherHashAndMintsSession()
    {
        FakeLauncherAccountSessionRepository repository = new()
        {
            CreatedAccount = new LauncherAccountRecord(
                new AccountId(9),
                "newtester",
                DateTimeOffset.UtcNow,
                null,
                null,
                null),
            SessionToken = "created-session"
        };
        LauncherAuthService service = CreateService(repository);

        LauncherAuthResponse response = await service.CreateAccountAsync(
            new LauncherCreateAccountRequest("newtester", "password", "password", "dev@example.test"));

        Assert.True(response.Success);
        Assert.Equal("Account credentials created.", response.Message);
        Assert.Equal("newtester", response.Username);
        Assert.Equal("created-session", response.SessionId);
        Assert.Equal(56, repository.CreatedPasswordHashSha224?.Length);
        Assert.Equal(56, repository.CreatedPasswordSalt?.Length);
        Assert.Equal("dev@example.test", repository.CreatedEmail);
        Assert.True(LauncherPasswordHasher.Verify(
            "password",
            repository.CreatedPasswordHashSha224,
            repository.CreatedPasswordSalt));
    }

    [Fact]
    public async Task CreateAccountRejectsCredentialedDuplicateAccount()
    {
        FakeLauncherAccountSessionRepository repository = new();
        LauncherAuthService service = CreateService(repository);

        LauncherAuthResponse response = await service.CreateAccountAsync(
            new LauncherCreateAccountRequest("existing", "password", "password", "existing@example.test"));

        Assert.False(response.Success);
        Assert.Equal("Username is already in use.", response.Message);
        Assert.Null(response.SessionId);
        Assert.Equal(56, repository.CreatedPasswordHashSha224?.Length);
        Assert.Equal(56, repository.CreatedPasswordSalt?.Length);
    }

    private static LauncherAuthService CreateService(FakeLauncherAccountSessionRepository repository)
    {
        LauncherServiceOptions options = new(
            new ServerEndpoint("127.0.0.1", 8080),
            new MariaDbOptions(),
            AllowLocalAccountCreation: true,
            TimeSpan.FromHours(24));
        return new LauncherAuthService(repository, options, new FixedClock());
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeLauncherAccountSessionRepository : ILauncherAccountSessionRepository
    {
        public LauncherAccountRecord? ExistingAccount { get; init; }
        public LauncherAccountRecord? CreatedAccount { get; init; }
        public string SessionToken { get; init; } = "";
        public AccountId? RefreshedAccountId { get; private set; }
        public string? CreatedPasswordHashSha224 { get; private set; }
        public string? CreatedPasswordSalt { get; private set; }
        public string? CreatedEmail { get; private set; }

        public ValueTask<LauncherAccountRecord?> FindAccountByLoginAsync(
            string loginName,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                ExistingAccount is not null && String.Equals(ExistingAccount.LoginName, loginName, StringComparison.Ordinal)
                    ? ExistingAccount
                    : null);
        }

        public ValueTask<LauncherAccountRecord?> CreateAccountAsync(
            string loginName,
            string passwordHashSha224,
            string passwordSalt,
            string email,
            CancellationToken cancellationToken = default)
        {
            CreatedPasswordHashSha224 = passwordHashSha224;
            CreatedPasswordSalt = passwordSalt;
            CreatedEmail = email;
            return ValueTask.FromResult(CreatedAccount);
        }

        public ValueTask<string> RefreshOrCreateSessionAsync(
            AccountId accountId,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            RefreshedAccountId = accountId;
            return ValueTask.FromResult(SessionToken);
        }
    }
}
