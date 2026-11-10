using System.Text.Json;
using BolaDetector.Application;
using BolaDetector.Demo;
using BolaDetector.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

var configPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "demo-detection.json");
var outputPath = args.Length > 1 ? args[1] : "bola-report.json";
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    // Composition root alone selects concrete file, transport and presentation adapters.
    IDetectionConfigurationLoader loader = new JsonDetectionConfigurationLoader();
    var configuration = await loader.LoadAsync(configPath, cancellation.Token);
    var services = new ServiceCollection().AddBolaDetection(configuration);
    services.AddSingleton<TextWriter>(Console.Out);
    services.AddSingleton<ConsoleReportPresenter>();
    services.AddTransient<DetectionConsoleApplication>();
    using var provider = services.BuildServiceProvider();
    return await provider.GetRequiredService<DetectionConsoleApplication>().RunAsync(configuration, outputPath, cancellation.Token);
}
catch (OperationCanceledException) { Console.Error.WriteLine("Detection cancelled."); return 130; }
catch (Exception ex) when (ex is ArgumentException or IOException or JsonException or InvalidOperationException)
{ Console.Error.WriteLine($"Detection failed ({ex.GetType().Name}); verify configuration and local file permissions."); return 1; }
