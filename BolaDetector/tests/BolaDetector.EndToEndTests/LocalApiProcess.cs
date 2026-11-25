using System.Net;
using System.Net.Sockets;

namespace BolaDetector.EndToEndTests;

internal sealed class LocalApiProcess : IAsyncDisposable
{
    private readonly TestProcess process;

    private LocalApiProcess(TestProcess process, HttpClient client)
    {
        this.process = process;
        Client = client;
    }

    public HttpClient Client { get; }
    public string Diagnostics => process.Diagnostics;

    public static async Task<LocalApiProcess> StartAsync(string workingDirectory, int port = 0)
    {
        if (port != 0)
            CheckPort(port);

        var process = await TestProcess.StartAsync("targets", "BolaTestApi", workingDirectory,
            ["--urls", $"http://127.0.0.1:{port}", "--Logging:LogLevel:Microsoft.Hosting.Lifetime", "Information"]);
        HttpClient? client = null;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var address = await process.WaitForListenerAsync(deadline.Token);
            if (port != 0 && address.Port != port)
                throw new InvalidOperationException($"The test API did not bind the requested port {port}.");

            client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            {
                BaseAddress = address,
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var health = await client.GetAsync("/health", deadline.Token);
            health.EnsureSuccessStatusCode();
            return new LocalApiProcess(process, client);
        }
        catch (Exception error)
        {
            client?.Dispose();
            await process.DisposeAsync();
            throw new InvalidOperationException($"Local test API did not become ready.{Environment.NewLine}{process.Diagnostics}", error);
        }
    }

    private static void CheckPort(int port)
    {
        var probe = new TcpListener(IPAddress.Loopback, port) { ExclusiveAddressUse = true };
        try
        {
            probe.Start();
        }
        catch (SocketException error)
        {
            throw new InvalidOperationException(
                $"The authorized CLI process test requires an available 127.0.0.1:{port}. " +
                "The test starts its own API and leaves existing processes running.", error);
        }
        finally
        {
            probe.Stop();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await process.DisposeAsync();
    }
}
