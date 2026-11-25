using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;

namespace BolaDetector.EndToEndTests;

internal sealed class TestProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly ConcurrentQueue<string> output = new();
    private readonly ConcurrentQueue<string> errors = new();
    private readonly TaskCompletionSource<Uri> listening = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TestProcess(Process process)
    {
        this.process = process;
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not { } line)
                return;
            output.Enqueue(line);
            const string marker = "Now listening on: ";
            var trimmed = line.Trim();
            if (trimmed.StartsWith(marker, StringComparison.Ordinal) &&
                Uri.TryCreate(trimmed[marker.Length..], UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttp && uri.Host == "127.0.0.1")
                listening.TrySetResult(uri);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is { } line)
                errors.Enqueue(line);
        };
    }

    public string Diagnostics => $"stdout:{Environment.NewLine}{string.Join(Environment.NewLine, output)}" +
        $"{Environment.NewLine}stderr:{Environment.NewLine}{StandardError}";

    public string StandardError => string.Join(Environment.NewLine, errors);

    public static async Task<TestProcess> StartAsync(string projectFolder, string project, string workingDirectory,
        string[] arguments, IReadOnlyDictionary<string, string>? environment = null)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BolaDetector.sln")))
            directory = directory.Parent;
        if (directory is null)
            throw new InvalidOperationException("Run process tests from the source checkout.");

        var configuration = typeof(TestProcess).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var assembly = Path.Combine(directory.FullName, projectFolder, project, "bin", configuration, "net8.0", project + ".dll");
        if (!File.Exists(assembly))
            throw new FileNotFoundException("Build the test project and its process dependencies first.", assembly);

        var start = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(assembly);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var (name, value) in environment)
                start.Environment[name] = value;

        var child = new TestProcess(new Process { StartInfo = start });
        var started = false;
        try
        {
            started = child.process.Start();
            if (!started)
                throw new InvalidOperationException($"Could not start {project}.");
            child.process.BeginOutputReadLine();
            child.process.BeginErrorReadLine();
            return child;
        }
        catch
        {
            if (started)
                await child.DisposeAsync();
            else
                child.process.Dispose();
            throw;
        }
    }

    public async Task<Uri> WaitForListenerAsync(CancellationToken cancellationToken)
    {
        var exited = process.WaitForExitAsync(cancellationToken);
        await Task.WhenAny(listening.Task, exited);
        cancellationToken.ThrowIfCancellationRequested();
        if (process.HasExited)
        {
            await exited;
            throw new InvalidOperationException($"API exited with code {process.ExitCode} before readiness.{Environment.NewLine}{Diagnostics}");
        }
        return await listening.Task.WaitAsync(cancellationToken);
    }

    public async Task<int> WaitForExitAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"CLI did not exit within 15 seconds.{Environment.NewLine}{Diagnostics}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The child exited between the status check and the kill.
                }
            }

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(deadline.Token);
            TestContext.Out.WriteLine($"Confirmed exit of test process PID {process.Id}.");
        }
        finally
        {
            process.Dispose();
        }
    }
}
