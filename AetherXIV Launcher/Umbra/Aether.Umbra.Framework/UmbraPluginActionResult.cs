namespace Aether.Umbra.Framework;

public sealed record UmbraPluginActionResult(bool Succeeded, string Message)
{
    public static UmbraPluginActionResult Success(string message) => new(true, message);

    public static UmbraPluginActionResult Failure(string message) => new(false, message);
}
