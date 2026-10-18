using BeatmapHarvest.Core.Osu;
using BeatmapHarvest.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace BeatmapHarvest.App.Services;

/// <summary>The signed-in osu! user, saved (encrypted) in <c>account.bin</c>.</summary>
public sealed record SignedInUser(long UserId, string Username, string? AvatarUrl, OsuUserToken Token);

/// <summary>
/// Optional "Sign in with osu!". Opens osu!'s authorization page in the browser and waits for the redirect
/// to <see cref="LoopbackCallbackListener"/>. It only adds conveniences (own profile, friend list):
/// harvesting uses the app credentials, and downloads still come from mirrors.
/// </summary>
public sealed class OsuAccount(
    [FromKeyedServices(ServiceRegistration.OsuHttp)] HttpClient http,
    ICredentialStore credentials,
    OsuApiOptions options,
    TimeProvider time,
    AppPaths paths) : IDisposable
{
    private const string Purpose = "account";
    private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private SignedInUser? _user;
    private bool _loaded;
    private CancellationTokenSource? _signInCts;

    public event Action? Changed;

    public SignedInUser? User
    {
        get
        {
            if (!_loaded)
            {
                _user = DpapiFile.Read<SignedInUser>(paths.AccountFile, Purpose);
                _loaded = true;
            }
            return _user;
        }
    }

    public bool IsSigningIn => _signInCts is not null;

    public static Uri RedirectUri { get; } = LoopbackCallbackListener.RedirectUriFor();

    /// <summary>Runs the whole browser sign-in. Throws <see cref="OsuApiException"/> with a user-facing message on failure.</summary>
    public async Task SignInAsync()
    {
        var creds = credentials.Load()
            ?? throw new OsuAuthenticationException("Add your client id and secret first.");
        if (IsSigningIn)
            return;

        using var cts = new CancellationTokenSource(SignInTimeout);
        _signInCts = cts;
        Changed?.Invoke();
        try
        {
            LoopbackCallbackListener listener;
            try
            {
                listener = new LoopbackCallbackListener();
            }
            catch (System.Net.HttpListenerException ex)
            {
                throw new OsuApiException($"Could not listen on {RedirectUri} ({ex.Message}). Close other apps using port {LoopbackCallbackListener.DefaultPort} and try again.");
            }

            using (listener)
            {
                var state = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
                ShellLauncher.Open(OsuUserAuth.AuthorizeUri(options.BaseAddress, creds.ClientId, listener.RedirectUri, state).AbsoluteUri);

                IReadOnlyDictionary<string, string> query;
                try
                {
                    query = await listener.WaitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new OsuApiException("Sign-in was cancelled or took too long.");
                }

                if (query.TryGetValue("error", out var error))
                {
                    throw new OsuApiException(error == "access_denied"
                        ? "You declined the authorization on osu!."
                        : $"osu! returned an error: {error}. Check that {RedirectUri} is in your OAuth application's callback URLs.");
                }
                if (!query.TryGetValue("state", out var returned) || returned != state || !query.TryGetValue("code", out var code))
                    throw new OsuApiException("The sign-in reply was not valid. Try again.");

                var token = await OsuUserAuth.ExchangeCodeAsync(http, options.BaseAddress, creds, code, listener.RedirectUri, time, CancellationToken.None).ConfigureAwait(false);
                var me = await OsuUserAuth.GetMeAsync(http, options.BaseAddress, token.AccessToken, CancellationToken.None).ConfigureAwait(false);
                Save(new SignedInUser(me.Id, me.Username, me.AvatarUrl, token));
            }
        }
        finally
        {
            _signInCts = null;
            Changed?.Invoke();
        }
    }

    public void CancelSignIn() => _signInCts?.Cancel();

    public async Task SignOutAsync()
    {
        if (User is { } user)
        {
            try
            {
                await OsuUserAuth.RevokeAsync(http, options.BaseAddress, user.Token.AccessToken, CancellationToken.None).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                // Offline: the token is forgotten locally and expires on its own.
            }
        }
        DpapiFile.Delete(paths.AccountFile);
        _user = null;
        Changed?.Invoke();
    }

    public async Task<IReadOnlyList<OsuUser>> GetFriendsAsync()
    {
        var token = await AccessTokenAsync().ConfigureAwait(false);
        return await OsuUserAuth.GetFriendsAsync(http, options.BaseAddress, token, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>A valid user token, refreshed when it has expired. A rejected refresh signs the user out.</summary>
    private async Task<string> AccessTokenAsync()
    {
        await _tokenLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var user = User ?? throw new OsuAuthenticationException("Not signed in.");
            if (!user.Token.IsExpired(time.GetUtcNow()))
                return user.Token.AccessToken;

            var creds = credentials.Load() ?? throw new OsuAuthenticationException("Add your client id and secret first.");
            try
            {
                var token = await OsuUserAuth.RefreshAsync(http, options.BaseAddress, creds, user.Token.RefreshToken, time, CancellationToken.None).ConfigureAwait(false);
                Save(user with { Token = token });
                return token.AccessToken;
            }
            catch (OsuAuthenticationException)
            {
                DpapiFile.Delete(paths.AccountFile);
                _user = null;
                Changed?.Invoke();
                throw;
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private void Save(SignedInUser user)
    {
        DpapiFile.Write(paths.AccountFile, Purpose, user);
        _user = user;
        _loaded = true;
    }

    public void Dispose() => _tokenLock.Dispose();
}
