using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.Facade.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Xunit;

namespace OsduCsharpClient.Tests;

/// <summary>
/// Covers choosing between several accounts in one token cache.
///
/// The acquisition paths themselves need a browser and a live tenant, so what is unit-tested
/// is the part that decides which identity is used: which cached account is tried, and the
/// refusal to return a token for an account nobody asked for.
/// </summary>
public class MsalAccountSelectionTests
{
    private sealed class FakeAccount(string username) : IAccount
    {
        public string Username { get; } = username;
        public string Environment => "login.microsoftonline.com";
        // Reads the property, not the constructor parameter: referencing the parameter here
        // would capture it into the class as well as initialise Username from it (CS9124).
        public AccountId HomeAccountId => new($"{Username}.tenant", Username, "tenant");
    }

    private static IReadOnlyList<IAccount> Cache(params string[] usernames) =>
        usernames.Select(u => (IAccount)new FakeAccount(u)).ToList();

    // ---- the published surface ----------------------------------------------------------

    [Fact]
    public void TheConstructorPublishedIn2_0_KeepsItsSignature()
    {
        // Optional parameters are resolved at compile time, so adding one would rewrite this
        // signature in metadata and an application compiled against 2.0.x would fail with
        // MissingMethodException on upgrade without recompiling. Account selection is an init
        // property for that reason; this test is what stops it drifting back.
        var constructor = typeof(MsalInteractiveTokenProvider).GetConstructor(
            [typeof(OsduConfig), typeof(string), typeof(ILoggerFactory)]);

        Assert.NotNull(constructor);
    }

    [Fact]
    public async Task ListingCachedAccountsHonoursCancellation()
    {
        // Cache registration serialises on a semaphore; a caller waiting behind it must be
        // able to give up. Asserted here because adding the token after release would change
        // a published signature — the break this type's init property exists to avoid.
        var provider = new MsalInteractiveTokenProvider(Config());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetCachedUsernamesAsync(new CancellationToken(canceled: true)));
    }

    private static OsduConfig Config() => new()
    {
        Server = "https://example.invalid",
        DataPartitionId = "test",
        Authority = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000",
        ClientId = "00000000-0000-0000-0000-000000000001",
        Scopes = "https://example.invalid/.default",
    };

    [Fact]
    public void UsernameIsNormalisedOnTheWayIn()
    {
        // Whitespace round a pasted address should not turn into a username nothing matches.
        Assert.Null(Normalise("   "));
        Assert.Null(Normalise(null));
        Assert.Equal("azure@equinor.com", Normalise("  azure@equinor.com  "));
    }

    private static string? Normalise(string? value)
    {
        var provider = (MsalInteractiveTokenProvider)System.Runtime.CompilerServices
            .RuntimeHelpers.GetUninitializedObject(typeof(MsalInteractiveTokenProvider));
        typeof(MsalInteractiveTokenProvider)
            .GetProperty(nameof(MsalInteractiveTokenProvider.Username))!
            .SetValue(provider, value);
        return provider.Username;
    }

    // ---- selection ----------------------------------------------------------------------

    [Fact]
    public void TheNamedAccountIsChosenEvenWhenItIsNotFirst()
    {
        var chosen = MsalInteractiveTokenProvider.SelectAccount(
            Cache("normal@equinor.com", "azure@equinor.com"), "azure@equinor.com");

        Assert.Equal("azure@equinor.com", chosen!.Username);
    }

    [Fact]
    public void MatchingIsCaseInsensitive()
    {
        // Entra echoes back whatever casing the user typed; the cache keeps that verbatim.
        var chosen = MsalInteractiveTokenProvider.SelectAccount(
            Cache("Azure@Equinor.com"), "azure@equinor.com");

        Assert.NotNull(chosen);
    }

    [Fact]
    public void AnUnknownUsernameSelectsNothingRatherThanFallingBackToTheFirst()
    {
        // Falling back would sign the user in as somebody else without saying so; returning
        // null sends the caller down the interactive path with a login hint instead.
        var chosen = MsalInteractiveTokenProvider.SelectAccount(
            Cache("normal@equinor.com"), "azure@equinor.com");

        Assert.Null(chosen);
    }

    [Fact]
    public void WithNoUsernameTheFirstAccountIsUsed()
    {
        var chosen = MsalInteractiveTokenProvider.SelectAccount(
            Cache("normal@equinor.com", "azure@equinor.com"), null);

        Assert.Equal("normal@equinor.com", chosen!.Username);
    }

    [Fact]
    public void AnEmptyCacheSelectsNothing()
    {
        Assert.Null(MsalInteractiveTokenProvider.SelectAccount(Cache(), "azure@equinor.com"));
        Assert.Null(MsalInteractiveTokenProvider.SelectAccount(Cache(), null));
    }

    // ---- the guard ----------------------------------------------------------------------

    [Fact]
    public void SigningInAsSomebodyElseIsAnError()
    {
        // A login hint only pre-fills the browser; the user can still pick another account.
        var error = Assert.Throws<OsduException>(() =>
            MsalInteractiveTokenProvider.EnsureExpectedAccount(
                "azure@equinor.com", "normal@equinor.com"));

        Assert.Contains("azure@equinor.com", error.Message);
        Assert.Contains("normal@equinor.com", error.Message);
    }

    [Fact]
    public void SigningInAsTheRequestedAccountIsAccepted()
    {
        MsalInteractiveTokenProvider.EnsureExpectedAccount(
            "azure@equinor.com", "Azure@Equinor.com");
    }

    [Fact]
    public void NoRequestedAccountMeansNothingToCheck()
    {
        MsalInteractiveTokenProvider.EnsureExpectedAccount(null, "anyone@equinor.com");
    }

    [Fact]
    public void AnUnverifiableIdentityIsRejectedRatherThanTrusted()
    {
        // A result with no account is not evidence of the wrong identity, but it is not
        // evidence of the right one either. Returning the token would hand back exactly the
        // unverified identity the caller asked to be protected from, so this fails closed.
        var error = Assert.Throws<OsduException>(() =>
            MsalInteractiveTokenProvider.EnsureExpectedAccount("azure@equinor.com", null));

        Assert.Contains("azure@equinor.com", error.Message);
        Assert.Contains("cannot be confirmed", error.Message);
    }

    [Fact]
    public void AnAbsentAccountIsFineWhenNoParticularIdentityWasAskedFor()
    {
        // Nothing was promised, so there is nothing to verify.
        MsalInteractiveTokenProvider.EnsureExpectedAccount(null, null);
    }
}
