using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Equinor.OsduCsharpClient.Facade.Auth;

/// <summary>
/// Attaches an OS-encrypted persistent token cache to an MSAL public client.
/// </summary>
/// <remarks>
/// A refresh token is a bearer credential with a long life. Persisting one is what makes a
/// CLI usable across invocations, but it has to be persisted encrypted: DPAPI on Windows,
/// Keychain on macOS, and a libsecret keyring on Linux. <c>MsalCacheHelper</c> selects the
/// right one per platform.
///
/// Where no secure store is available — a container, a headless Linux box with no keyring —
/// this deliberately falls back to <b>in-memory only</b> rather than to a plaintext file.
/// The cost is re-authenticating each run; the alternative is writing refresh tokens to disk
/// in the clear, which is what this type exists to stop.
/// </remarks>
internal static class TokenCacheStorage
{
    private const string MacKeyChainService = "com.equinor.osducsharpclient";
    private const string MacKeyChainAccount = "MSALCache";
    private const string LinuxKeyringSchema = "com.equinor.osducsharpclient.tokencache";

    /// <summary>
    /// Registers an encrypted cache at <paramref name="cachePath"/> against
    /// <paramref name="cache"/>, and returns true when persistence is actually available.
    /// </summary>
    public static async Task<bool> TryRegisterAsync(
        ITokenCache cache, string cachePath, ILogger log)
    {
        var directory = Path.GetDirectoryName(cachePath)!;
        var fileName = Path.GetFileName(cachePath);
        Directory.CreateDirectory(directory);

        RemoveLegacyPlaintextCache(cachePath, log);

        try
        {
            var properties = new StorageCreationPropertiesBuilder(fileName, directory)
                .WithMacKeyChain(MacKeyChainService, MacKeyChainAccount)
                .WithLinuxKeyring(
                    LinuxKeyringSchema,
                    MsalCacheHelper.LinuxKeyRingDefaultCollection,
                    "OSDU client token cache",
                    new KeyValuePair<string, string>("Product", "OsduCsharpClient"),
                    new KeyValuePair<string, string>("Version", "1"))
                .Build();

            var helper = await MsalCacheHelper.CreateAsync(properties).ConfigureAwait(false);

            // Proves the platform store can actually be written and read back. Without this
            // an unusable keyring surfaces later as tokens that silently never persist.
            helper.VerifyPersistence();
            helper.RegisterCache(cache);

            log.LogDebug("Token cache persisted at {Path}, encrypted by the OS.", cachePath);
            return true;
        }
        catch (MsalCachePersistenceException exception)
        {
            log.LogWarning(exception,
                "No OS-encrypted token store is available, so tokens will be held in memory " +
                "only and re-authentication will be required each run. Refusing to fall back " +
                "to an unencrypted cache file.");
            return false;
        }
    }

    /// <summary>
    /// Deletes a cache file left by an earlier version that wrote
    /// <c>SerializeMsalV3()</c> straight to disk, unencrypted.
    /// </summary>
    /// <remarks>
    /// Detected by its first byte: the MSAL v3 blob is JSON, so it opens with <c>{</c>.
    /// A DPAPI blob or a Keychain placeholder does not. Leaving the old file in place would
    /// mean shipping the fix while the plaintext refresh token stayed readable on disk.
    /// </remarks>
    internal static void RemoveLegacyPlaintextCache(string cachePath, ILogger log)
    {
        try
        {
            if (!File.Exists(cachePath)) return;

            using (var stream = File.OpenRead(cachePath))
            {
                if (stream.Length == 0 || stream.ReadByte() != '{') return;
            }

            File.Delete(cachePath);
            log.LogWarning(
                "Removed an unencrypted token cache written by an earlier version " +
                "({Path}). You will be asked to sign in again once.", cachePath);
        }
        catch (IOException exception)
        {
            log.LogWarning(exception,
                "Could not inspect or remove the existing token cache at {Path}.", cachePath);
        }
    }
}
