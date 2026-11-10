using System.Text;
using BolaDetector.Application;
using BolaDetector.Domain;
namespace BolaDetector.Infrastructure;

public sealed class HttpScenarioRequestExecutor(HttpClient client, ITestCredentialProvider credentials,
    DetectionConfiguration configuration, LocalTargetAddressPolicy targetPolicy) : IScenarioRequestExecutor
{
    private const int MaximumBodyBytes = 64 * 1024;

    public async Task<ObservedResponse> ExecuteAsync(ApiOperation operation, TestResource resource,
        TestIdentity identity, CancellationToken cancellationToken)
    {
        if (operation.Method != "GET") throw new ArgumentException("Only GET operations are supported.");
        var uri = targetPolicy.Build(configuration.TargetOrigin, operation.Path, resource.Id);
        if (configuration.TimeoutSeconds is < 1 or > 60) throw new ArgumentException("Timeout must be 1-60 seconds.");
        var response = await SendAsync(uri, identity, cancellationToken);
        return new ObservedResponse(uri.AbsolutePath, response.Observation, response.Body);
    }

    private async Task<(ResponseObservation Observation, string? Body)> SendAsync(Uri uri, TestIdentity identity, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Test-User", credentials.Resolve(identity));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            // Redirects are disabled by the registered primary handler. Never follow Location ourselves.
            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var bytes = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(bytes, timeout.Token)) > 0)
            {
                if (buffer.Length + count > MaximumBodyBytes)
                    return (new((int)response.StatusCode, 0, "ResponseTooLarge"), null);
                buffer.Write(bytes, 0, count);
            }
            return (new((int)response.StatusCode, (int)buffer.Length, null), Encoding.UTF8.GetString(buffer.ToArray()));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return (new(null, 0, "Timeout"), null); }
        catch (HttpRequestException) { return (new(null, 0, "HttpRequestFailed"), null); }
        catch (IOException) { return (new(null, 0, "ResponseReadFailed"), null); }
        catch (ArgumentException) { return (new(null, 0, "InvalidTestCredential"), null); }
        catch (InvalidOperationException) { return (new(null, 0, "MissingTestCredential"), null); }
    }

}
