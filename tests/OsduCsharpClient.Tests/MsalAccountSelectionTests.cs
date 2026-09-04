using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.Facade.Auth;
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
        public AccountId HomeAccountId => new($"{username}.tenant", username, "tenant");
    }

    private static IReadOnlyList<IAccount> Cache(params string[] usernames) =>
        usernames.Select(u => (IAccount)new FakeAccount(u)).ToList();

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
    public void AnAbsentAccountOnTheResultIsNotTreatedAsAMismatch()
    {
        // Some flows return no Account; that is not evidence of the wrong identity, and
        // throwing would break them for no benefit.
        MsalInteractiveTokenProvider.EnsureExpectedAccount("azure@equinor.com", null);
    }
}
