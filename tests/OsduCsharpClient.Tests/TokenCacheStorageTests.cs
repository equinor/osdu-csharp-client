using Equinor.OsduCsharpClient.Facade.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace OsduCsharpClient.Tests;

/// <summary>
/// Covers the migration away from the unencrypted token cache earlier versions wrote.
///
/// The encrypted path itself is not unit-tested: it talks to DPAPI, Keychain or libsecret,
/// which is environment-dependent and would prompt on a developer machine. What is testable,
/// and what actually closes the hole, is that a plaintext cache left behind by an older
/// version is deleted rather than left readable on disk.
/// </summary>
public class TokenCacheStorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "osdu-cache-tests-" + Guid.NewGuid().ToString("N"));

    public TokenCacheStorageTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Write(byte[] contents)
    {
        var path = Path.Combine(_directory, "msal_cache.bin");
        File.WriteAllBytes(path, contents);
        return path;
    }

    [Fact]
    public void DeletesAnUnencryptedMsalV3Cache()
    {
        // What the previous implementation wrote: SerializeMsalV3() straight to disk, which
        // is JSON and therefore starts with '{'.
        var path = Write("""{"AccessToken":{},"RefreshToken":{"secret":"leaked"}}"""u8.ToArray());

        TokenCacheStorage.RemoveLegacyPlaintextCache(path, NullLogger.Instance);

        Assert.False(File.Exists(path), "a plaintext token cache must not survive the upgrade");
    }

    [Fact]
    public void LeavesAnEncryptedCacheAlone()
    {
        // A DPAPI blob or Keychain placeholder is not JSON. Deleting it would throw away a
        // perfectly good cache and force a needless re-authentication.
        var path = Write([0x01, 0x00, 0x00, 0x00, 0xD0, 0x8C, 0x9D, 0xDF]);

        TokenCacheStorage.RemoveLegacyPlaintextCache(path, NullLogger.Instance);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LeavesAnEmptyFileAlone()
    {
        var path = Write([]);

        TokenCacheStorage.RemoveLegacyPlaintextCache(path, NullLogger.Instance);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void IsANoOpWhenNoCacheExists()
    {
        var path = Path.Combine(_directory, "absent.bin");

        TokenCacheStorage.RemoveLegacyPlaintextCache(path, NullLogger.Instance);

        Assert.False(File.Exists(path));
    }
}
