using System.Text.Json;
using System.Text.Json.Serialization;
using BolaDetector.Application;
using BolaDetector.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BolaDetector.Demo;

internal static class DocumentSequenceDemo
{
    internal static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 1)
            throw new ArgumentException("Expected at most one configuration path.");

        var path = args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "demo-detection.json");
        var input = await new JsonDetectionConfigurationLoader().LoadAsync(path, cancellationToken);
        var configuration = ValidatedDetectionConfiguration.Create(input);
        using var services = new ServiceCollection().AddLocalDocumentSequences().BuildServiceProvider();
        var executor = services.GetRequiredService<IDocumentSequenceExecutor>();
        const string resource = "temporary-document";
        const string initial = "Temporary initial title";
        const string updated = "Temporary updated title";
        var definition = new DocumentSequence("local-document-lifecycle", "Authorized document lifecycle", "alice",
        [
            new("create", DocumentStepOperation.Create, resource, [], initial),
            new("read-initial", DocumentStepOperation.Read, resource, ["create"]),
            new("verify-initial", DocumentStepOperation.Verify, resource, ["create", "read-initial"], initial),
            new("update", DocumentStepOperation.Update, resource, ["create", "verify-initial"], updated),
            new("read-updated", DocumentStepOperation.Read, resource, ["create", "update"]),
            new("verify-updated", DocumentStepOperation.Verify, resource, ["create", "read-updated"], updated),
            new("delete", DocumentStepOperation.Delete, resource, ["create", "verify-updated"]),
            new("verify-deleted", DocumentStepOperation.VerifyAbsent, resource, ["create", "delete"])
        ]);
        var result = await executor.ExecuteAsync(definition, configuration, cancellationToken);
        var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        Console.WriteLine(JsonSerializer.Serialize(result, options));
        return result.Cancelled ? 130 : result.Succeeded ? 0 : 1;
    }
}
