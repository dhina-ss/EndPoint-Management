using System.Diagnostics;
using System.Text;

namespace EMS.Agent.Helpers;

/// <summary>Result of running an external process to completion (or timeout).</summary>
public sealed record ProcessResult(bool TimedOut, int ExitCode, string Output);

/// <summary>
/// Runs a child process with a hard timeout, capturing stdout+stderr. The
/// timeout matters because the agent runs as SYSTEM in Session 0: an installer
/// that unexpectedly shows a dialog would otherwise wait forever with no one to
/// click it. On timeout the whole process tree is killed.
/// </summary>
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return new ProcessResult(false, process.ExitCode, output.ToString().Trim());
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new ProcessResult(true, -1, output.ToString().Trim());
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort; the process may have exited between the check and the kill.
        }
    }
}
