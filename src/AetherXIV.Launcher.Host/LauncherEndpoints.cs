using System.Text.Json;
using AetherXIV.Launcher.Contracts;

namespace AetherXIV.Launcher.Host;

public static class LauncherEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app)
    {
        RouteGroupBuilder launcher = app.MapGroup("/launcher");

        launcher.MapGet("", async (LauncherContentService content, CancellationToken cancellationToken) =>
            await content.GetConfigAsync(cancellationToken).ConfigureAwait(false));
        launcher.MapGet("/config", async (LauncherContentService content, CancellationToken cancellationToken) =>
            await content.GetConfigAsync(cancellationToken).ConfigureAwait(false));
        launcher.MapGet("/status", async (LauncherContentService content, CancellationToken cancellationToken) =>
            await content.GetStatusAsync(cancellationToken).ConfigureAwait(false));
        launcher.MapGet("/news", async (LauncherContentService content, CancellationToken cancellationToken) =>
            await content.GetNewsAsync(cancellationToken).ConfigureAwait(false));
        launcher.MapGet("/patch-manifest", async (LauncherContentService content, CancellationToken cancellationToken) =>
            await content.GetPatchManifestAsync(cancellationToken).ConfigureAwait(false));
        launcher.MapGet("/runtime-catalog", async (
            string? platform,
            LauncherContentService content,
            CancellationToken cancellationToken) =>
            await content.GetRuntimeCatalogAsync(platform, cancellationToken).ConfigureAwait(false));
        launcher.MapGet("/umbra/framework-catalog", async (
            string? platform,
            LauncherContentService content,
            CancellationToken cancellationToken) =>
            await content.GetUmbraFrameworkCatalogAsync(platform, cancellationToken).ConfigureAwait(false));
        launcher.MapGet("/umbra/plugin-catalog", async (
            LauncherContentService content,
            CancellationToken cancellationToken) =>
            await content.GetUmbraPluginCatalogAsync(cancellationToken).ConfigureAwait(false));
        launcher.MapGet("/umbra/plugin-blocklist", async (
            LauncherContentService content,
            CancellationToken cancellationToken) =>
            await content.GetUmbraPluginBlocklistAsync(cancellationToken).ConfigureAwait(false));
        launcher.MapPost("/login", async (
            HttpRequest httpRequest,
            LauncherAuthService auth,
            CancellationToken cancellationToken) =>
        {
            LauncherAuthRequest? request = await ReadLoginRequestAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            LauncherAuthResponse response = request is null
                ? new LauncherAuthResponse(false, "Invalid login request.", null, null)
                : await auth.LoginAsync(request, cancellationToken).ConfigureAwait(false);
            return ToAuthResult(response);
        });
        launcher.MapPost("/create-account", async (
            HttpRequest httpRequest,
            LauncherAuthService auth,
            CancellationToken cancellationToken) =>
        {
            LauncherCreateAccountRequest? request = await ReadCreateAccountRequestAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            LauncherAuthResponse response = request is null
                ? new LauncherAuthResponse(false, "Invalid account creation request.", null, null)
                : await auth.CreateAccountAsync(request, cancellationToken).ConfigureAwait(false);
            return ToAuthResult(response);
        });

        app.MapGet("/login/index.php", () => Results.Content(
            """
            <!doctype html>
            <html><head><title>AetherXIV Launcher</title></head>
            <body><h1>AetherXIV Launcher</h1><p>Use AetherXIV Launcher 2.0 to start the local client.</p></body></html>
            """,
            "text/html"));
    }

    private static async ValueTask<LauncherAuthRequest?> ReadLoginRequestAsync(
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        if (httpRequest.HasFormContentType)
        {
            IFormCollection form = await httpRequest.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            return new LauncherAuthRequest(form["username"].ToString(), form["password"].ToString());
        }

        return await ReadJsonAsync<LauncherAuthRequest>(httpRequest, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<LauncherCreateAccountRequest?> ReadCreateAccountRequestAsync(
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        if (httpRequest.HasFormContentType)
        {
            IFormCollection form = await httpRequest.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            return new LauncherCreateAccountRequest(
                form["username"].ToString(),
                form["password"].ToString(),
                form["confirm_password"].ToString(),
                form["email"].ToString());
        }

        return await ReadJsonAsync<LauncherCreateAccountRequest>(httpRequest, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T?> ReadJsonAsync<T>(
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(
                httpRequest.Body,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static IResult ToAuthResult(LauncherAuthResponse response)
    {
        int statusCode = response.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest;
        return Results.Json(response, statusCode: statusCode);
    }
}
