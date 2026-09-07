namespace Equinor.OsduCsharpClient.Facade.Auth;

internal static class MsalConfigValidator
{
    internal static void Validate(OsduConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Require(config.Authority, nameof(config.Authority));
        Require(config.ClientId, nameof(config.ClientId));
        Require(config.Scopes, nameof(config.Scopes));
    }

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new OsduException($"MSAL configuration requires a non-empty {name}.");
    }
}
