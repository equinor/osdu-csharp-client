namespace Equinor.OsduCsharpClient.Facade;

/// <summary>
/// <see cref="DelegatingHandler"/> that automatically injects the
/// <c>data-partition-id</c> header on every outgoing OSDU API request,
/// removing the need to set it manually on each call.
/// </summary>
internal sealed class DataPartitionHandler(string dataPartitionId) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("data-partition-id", dataPartitionId);
        return base.SendAsync(request, cancellationToken);
    }
}
