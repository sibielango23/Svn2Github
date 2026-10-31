using System.Diagnostics;

namespace SVN2GitHub.Api.Services;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class ExternalCommandRunner
{
    public async Task<CommandResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var entry in environment)
            {
                if (entry.Value is null)
                {
                    startInfo.Environment.Remove(entry.Key);
                }
                else
                {
                    startInfo.Environment[entry.Key] = entry.Value;
                }
            }
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start command: {executable}");
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InvalidOperationException(
                $"Required command '{executable}' is not installed or is not available on PATH.",
                exception);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = timeout is null
            ? null
            : new CancellationTokenSource(timeout.Value);
        using var linkedSource = timeoutSource is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);
        var waitToken = linkedSource?.Token ?? cancellationToken;

        try
        {
            await process.WaitForExitAsync(waitToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            if (timeoutSource?.IsCancellationRequested == true &&
                !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Command '{executable}' exceeded its timeout of {timeout.GetValueOrDefault()}.");
            }

            throw;
        }

        return new CommandResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    public async Task<CommandResult> RunCheckedAsync(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(
            executable,
            arguments,
            workingDirectory,
            timeout,
            environment,
            cancellationToken);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;
            throw new InvalidOperationException(
                $"Command '{executable}' failed with exit code {result.ExitCode}: {detail.Trim()}");
        }

        return result;
    }
}
