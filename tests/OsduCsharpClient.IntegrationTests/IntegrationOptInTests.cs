using Equinor.OsduCsharpClient.Facade;
using Xunit;

namespace OsduCsharpClient.IntegrationTests;

public class IntegrationOptInTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("false")]
    [InlineData(" FALSE ")]
    public void Disabled_HasExplicitSkipReason(string? value) =>
        Assert.Contains("OSDU_RUN_INTEGRATION_TESTS=true", OsduFixture.GetSkipReason(value));

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData(" true ")]
    public void Enabled_DoesNotSkip(string value) =>
        Assert.Null(OsduFixture.GetSkipReason(value));

    [Theory]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("tru")]
    public void InvalidSetting_FailsInsteadOfSkipping(string value) =>
        Assert.Throws<OsduException>(() => OsduFixture.GetSkipReason(value));
}
